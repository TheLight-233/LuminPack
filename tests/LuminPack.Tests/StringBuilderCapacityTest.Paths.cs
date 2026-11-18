using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary and JSON builder paths together, including reuse and failed publication.</summary>
public partial class StringBuilderCapacityTest
{
    /// <summary>Checks exact UTF-16 records, retained buffers, fresh construction and reused targets.</summary>
    [Fact]
    public void BinaryRecordsKeepBytesBoundsAndReuse()
    {
        using var buffer = new LuminBufferWriter(true);
        foreach (int length in new[] { 0, 1, 31, 32, 255, 256, 257, 8192 })
        {
            var value = Create(length);
            string text = value.ToString();
            byte[] expected = new byte[4 + length * 2];
            BinaryPrimitives.WriteInt32LittleEndian(expected, length);
            MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(expected.AsSpan(4));
            Assert.Equal(expected, LuminPackSerializer.Serialize(value));
            buffer.ResetWrittenCount();
            LuminPackSerializer.Serialize(value, buffer);
            Assert.True(buffer.GetSpan().SequenceEqual(expected));
            StringBuilder? restored = null;
            Assert.Equal(expected.Length, LuminPackSerializer.Deserialize(expected, ref restored));
            Assert.Equal(text, restored!.ToString());
            var original = restored;
            LuminPackSerializer.Deserialize(expected, ref restored);
            Assert.Same(original, restored);
            Assert.Equal(text, restored!.ToString());
            foreach (int cut in new[] { 0, 3, expected.Length - 1 })
            {
                restored.Clear().Append("keep");
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(expected.AsSpan(0, cut), ref restored));
                Assert.Same(original, restored);
                Assert.Equal("keep", restored!.ToString());
            }
        }
        StringBuilder? absent = null;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize(absent));
    }

    /// <summary>Checks quoted text, both encodings, fresh capacity and existing identity across all snapshot sizes.</summary>
    [Fact]
    public void JsonPathsKeepFormatsConstructionAndReuse()
    {
        foreach (int length in new[] { 0, 1, 31, 32, 255, 256, 257, 8192 })
        {
            var value = Create(length);
            CheckJson<LuminPackUtf8LengthOptions>(value, Encoding.UTF8);
            CheckJson<LuminPackUtf16LengthOptions>(value, Encoding.Unicode);
        }
        CheckJson<LuminPackUtf8LengthOptions>(null, Encoding.UTF8);
        CheckJson<LuminPackUtf16LengthOptions>(null, Encoding.Unicode);
    }

    /// <summary>Checks token failures preserve the target and completed oversized values retain the established clear-before-append behavior.</summary>
    [Fact]
    public void JsonFailuresKeepPublicationOrder()
    {
        CheckFailure<LuminPackUtf8LengthOptions>(Encoding.UTF8);
        CheckFailure<LuminPackUtf16LengthOptions>(Encoding.Unicode);
    }

    /// <summary>Checks repeated binary and JSON operations do not allocate a chunk index or intermediate string.</summary>
    [Fact]
    public void RetainedBuilderPathsAvoidIntermediateAllocations()
    {
        var value = Create(8192);
#if NET8_0_OR_GREATER
        int chunks = 0;
        foreach (var chunk in value.GetChunks()) chunks++;
        Assert.True(chunks > 8);
#endif
        using var buffer = new LuminBufferWriter(true);
        byte[] json = Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson<StringBuilder, LuminPackUtf16LengthOptions>(value));
        byte[] utf8 = Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson<StringBuilder, LuminPackUtf8LengthOptions>(value));
        var restored = new StringBuilder(value.ToString());
        for (int i = 0; i < 256; i++) Exercise(value, buffer, json, utf8, ref restored);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) Exercise(value, buffer, json, utf8, ref restored);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Checks fresh JSON builders allocate only the object and its final character storage.</summary>
    [Fact]
    public void FreshJsonBuildersAvoidIntermediateStrings()
    {
        foreach (int length in new[] { 0, 1, 16, 257, 8192 })
        {
            string text = Create(length).ToString();
            CheckFreshAllocation<LuminPackUtf8LengthOptions>(text, Encoding.UTF8);
            CheckFreshAllocation<LuminPackUtf16LengthOptions>(text, Encoding.Unicode);
        }
    }

    /// <summary>Compares result allocations with the established BCL string constructor after pool warmup.</summary>
    /// <param name="text">The original character content.</param>
    /// <param name="encoding">The JSON document encoding.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckFreshAllocation<TOptions>(string text, Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        byte[] input = encoding.GetBytes(LuminPackSerializer.SerializeJson<string, TOptions>(text));
        string decoded = string.Empty;
        LuminPackSerializer.DeserializeJson<string, TOptions>(input, ref decoded);
        StringBuilder? target = null;
        for (int i = 0; i < 64; i++)
        {
            GC.KeepAlive(CreateNecessaryBuilder(decoded));
            target = null;
            LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(input, ref target);
        }
        // Use the lowest repeated batch for each path so one-time runtime allocations
        // do not change the steady-state budget.
        long necessary = long.MaxValue;
        long allocated = long.MaxValue;
        for (int sample = 0; sample < 4; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++) GC.KeepAlive(CreateNecessaryBuilder(decoded));
            necessary = Math.Min(necessary, GC.GetAllocatedBytesForCurrentThread() - before);

            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                target = null;
                LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(input, ref target);
            }
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.True(allocated <= necessary,
            $"{typeof(TOptions).Name} at {text.Length} characters allocated {allocated} bytes per batch with a {necessary}-byte builder budget.");
        var expected = new StringBuilder(decoded);
        Assert.Equal(decoded, target!.ToString());
        Assert.Equal(expected.Capacity, target.Capacity);
        Assert.Equal(expected.MaxCapacity, target.MaxCapacity);
    }

    /// <summary>Creates an escaping result through the original constructor without a temporary token.</summary>
    /// <param name="text">The already prepared token text.</param>
    /// <returns>The fresh result used as the necessary-allocation reference.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static StringBuilder CreateNecessaryBuilder(string text) => new StringBuilder(text);

    /// <summary>Creates multiple chunks containing escapes, non-ASCII text and surrogate boundaries.</summary>
    /// <param name="length">The character count.</param>
    /// <returns>The fragmented source.</returns>
    private static StringBuilder Create(int length)
    {
        const string text = "abcd12345\u00e9\u4e2d\"\\\n\ud83d\ude00";
        var result = new StringBuilder(1);
        for (int i = 0; i < length; i++) result.Append(text[i % text.Length]);
        return result;
    }

    /// <summary>Compares builder JSON with the unchanged string codec and its constructor result.</summary>
    /// <param name="value">The source builder.</param>
    /// <param name="encoding">The document encoding.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckJson<TOptions>(StringBuilder? value, Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        string json = LuminPackSerializer.SerializeJson<StringBuilder, TOptions>(value);
        Assert.Equal(LuminPackSerializer.SerializeJson<string, TOptions>(value?.ToString()), json);
        byte[] bytes = encoding.GetBytes(json);
        string? expected = null;
        LuminPackSerializer.DeserializeJson<string, TOptions>(bytes, ref expected);
        StringBuilder? restored = null;
        LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(bytes, ref restored);
        Assert.Equal(expected, restored?.ToString());
        if (restored is not null) Assert.Equal(new StringBuilder(expected).Capacity, restored.Capacity);
        restored = new StringBuilder("previous", 128);
        var original = restored;
        LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(bytes, ref restored);
        Assert.Equal(expected, restored?.ToString());
        if (value is not null) Assert.Same(original, restored);
    }

    /// <summary>Checks failure timing relative to clearing an existing builder.</summary>
    /// <param name="encoding">The document encoding.</param>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckFailure<TOptions>(Encoding encoding) where TOptions : struct, ILuminPackOptions
    {
        foreach (string malformed in new[] { "\"unterminated", "\"bad\\u12\"", "\"raw\ncontrol\"" })
        {
            var value = new StringBuilder("keep");
            var original = value;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(encoding.GetBytes(malformed), ref value));
            Assert.Same(original, value);
            Assert.Equal("keep", value.ToString());
        }
        var bounded = new StringBuilder(2, 2).Append('a');
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<StringBuilder, TOptions>(encoding.GetBytes("\"four\""), ref bounded));
        Assert.Equal(string.Empty, bounded.ToString());
    }

    /// <summary>Exercises all retained-buffer paths after warming their pools and code.</summary>
    /// <param name="value">The source builder.</param>
    /// <param name="buffer">The retained output.</param>
    /// <param name="json">The input document.</param>
    /// <param name="utf8">The UTF-8 input document.</param>
    /// <param name="restored">The reused target.</param>
    private static void Exercise(StringBuilder value, LuminBufferWriter buffer, byte[] json, byte[] utf8, ref StringBuilder restored)
    {
        buffer.ResetWrittenCount(); LuminPackSerializer.Serialize(value, buffer);
        buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<StringBuilder, LuminPackUtf8LengthOptions>(value, buffer);
        buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<StringBuilder, LuminPackUtf16LengthOptions>(value, buffer);
        LuminPackSerializer.DeserializeJson<StringBuilder, LuminPackUtf16LengthOptions>(json, ref restored);
        LuminPackSerializer.DeserializeJson<StringBuilder, LuminPackUtf8LengthOptions>(utf8, ref restored);
    }
}
