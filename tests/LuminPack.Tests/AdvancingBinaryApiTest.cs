using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks complete advancing operations and concrete extension selection.</summary>
public class AdvancingBinaryApiTest
{
    /// <summary>Preserves definition-owned object framing when a closed generic struct becomes unmanaged.</summary>
    [Fact]
    public void ClosedGenericStructRetainsItsDeclaredFormatter()
    {
        var value = new FluentOpenStruct<int> { Value = 42 };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Assert.Equal(5, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(42, LuminPackSerializer.Deserialize<FluentOpenStruct<int>>(bytes).Value);
        var result = new FluentOpenStruct<int> { Value = -1 };
        LuminPackSerializer.Deserialize(bytes, ref result);
        Assert.Equal(42, result.Value);
        Assert.Equal(42, LuminPackSerializer.DeserializeJson<FluentOpenStruct<int>>(LuminPackSerializer.SerializeJson(value)).Value);
    }

    /// <summary>Keeps nullable reference-free collection payloads on their raw storage path.</summary>
    [Fact]
    public void NullableRawCollectionsKeepTheirBulkContract()
    {
        var value = new FluentNullableCollections
        {
            Array = new DateTime?[] { new(1729), null, new(91) },
            List = new List<DateTime?> { null, new(8128) }
        };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<FluentNullableCollections>(bytes);
        Assert.Equal(value.Array, result.Array);
        Assert.Equal(value.List, result.List);
        var array = result.Array;
        var list = result.List;
        LuminPackSerializer.Deserialize(bytes, ref result);
        Assert.Same(array, result.Array);
        Assert.Same(list, result.List);
        Assert.Equal(value.Array, result.Array);
        Assert.Equal(value.List, result.List);
    }

    /// <summary>Uses the public relative cursor and keeps fixed-span collection checks on their hot path.</summary>
    [Fact]
    public void PublicCursorAndFixedSpanCapacityCompose()
    {
        Span<byte> storage = new byte[64];
        var writer = new LuminPackWriter(ref storage);
        Assert.Equal(uint.MaxValue, writer.BufferCheckLimit);
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref writer.Cursor, 1729);
        writer.Advance(sizeof(int));
        for (int index = 0; index < 64; index++) Assert.Equal(index, writer.CheckBuffer(index));
        writer.Write<int>(new[] { 3, 5, 7 }.AsSpan(), 3);
        Assert.True(System.Runtime.CompilerServices.Unsafe.AreSame(ref writer.Cursor, ref storage[20]));
        ReadOnlySpan<byte> payload = storage[..writer.Position];
        var reader = new LuminPackReader(ref payload);
        Assert.Equal(1729, reader.Read<int>());
        Assert.Equal(3, reader.ReadLength(sizeof(int)));
        Span<int> items = stackalloc int[3];
        reader.Read(items);
        Assert.Equal(new[] { 3, 5, 7 }, items.ToArray());
        Assert.True(System.Runtime.CompilerServices.Unsafe.AreSame(ref reader.Cursor, ref storage[20]));
    }

    /// <summary>Copies borrowed native source storage before the completed-record threshold can replace its buffer.</summary>
    [Fact]
    public void RawSpanSourceSurvivesThresholdGrowth()
    {
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(owner);
        for (int index = 0; index < 64; index++) writer.Buffer[index] = (byte)index;
        byte[] expected = writer.Buffer[..64].ToArray();
        int start = (int)writer.BufferCheckLimit - 8;
        writer.Advance(start);
        int capacity = owner.TotalLength;
        ReadOnlySpan<byte> source = writer.Buffer[..64];
        writer.Write<byte>(source, source.Length);
        Assert.True(owner.TotalLength > capacity);
        Assert.Equal(expected, writer.GetSpan().Slice(start + sizeof(int), 64).ToArray());
        Assert.Equal(start + sizeof(int) + 64, writer.Position);
    }

    /// <summary>Uses the actual retained window even when an owned Writer temporarily receives smaller storage.</summary>
    [Fact]
    public void RawRecordChecksTheRetainedWindow()
    {
        using var owner = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(owner);
        Span<byte> window = new byte[32];
        window.Fill(0xCC);
        writer.SetSpan(ref window);
        writer.Write<int>(new int[16].AsSpan(), 16);
        Assert.Equal(sizeof(int) * 17, writer.Position);
        Assert.Equal(owner.TotalLength, writer.Buffer.Length);
        Assert.All(window.ToArray(), item => Assert.Equal((byte)0xCC, item));
    }

    /// <summary>Consumes concatenated object, collection and Union headers through cursor-owned operations.</summary>
    [Fact]
    public void HeadersOwnTheirCursorAdvancement()
    {
        Span<byte> storage = new byte[64];
        var writer = new LuminPackWriter(ref storage);
        writer.WriteObjectHeader(2);
        writer.WriteNullObjectHeader();
        writer.WriteCollectionHeader(3);
        writer.WriteNullCollectionHeader();
        writer.WriteUnionHeader(7);
        writer.WriteWideUnionHeader(1000);
        writer.WriteNullUnionHeader();
        writer.Write((byte)91);
        Assert.Equal(16, writer.Position);
        ReadOnlySpan<byte> payload = storage[..writer.Position];
        var reader = new LuminPackReader(ref payload);
        Assert.True(reader.TryReadObjectHead(out byte members));
        Assert.Equal(2, members);
        Assert.True(reader.PeekIsNullObject());
        Assert.Equal(1, reader.Position);
        Assert.False(reader.TryReadObjectHead());
        Assert.True(reader.TryReadCollectionHead(out int count));
        Assert.Equal(3, count);
        Assert.True(reader.PeekIsNullCollection());
        Assert.False(reader.TryReadCollectionHead(out count));
        Assert.Equal(-1, count);
        Assert.True(reader.TryPeekUnionHeader(out ushort tag));
        Assert.Equal(7, tag);
        Assert.True(reader.TryPeekWideUnionHeader(out tag));
        Assert.Equal(1000, tag);
        Assert.False(reader.TryPeekUnionHeader(out _));
        Assert.Equal(91, reader.Read<byte>());
        Assert.Equal(writer.Position, reader.Position);
    }

    /// <summary>Uses complete JSON operations for scalar values and generated collection contracts.</summary>
    [Fact]
    public void JsonValuesUseCanonicalOperations()
    {
        using var output = new LuminBufferWriter(true);
        var writer = new LuminPackJsonWriter(output, output.WriterState);
        writer.WriteArrayStart();
        writer.Write(true);
        writer.Write(1729);
        writer.Write("text");
        writer.Write(new List<int> { 3, 5, 7 });
        writer.WriteArrayEnd();
        ReadOnlySpan<byte> payload = writer.GetSpan();
        // The default JSON byte encoding is UTF-16.
        Assert.Equal("[true,1729,\"text\",[3,5,7]]", System.Text.Encoding.Unicode.GetString(payload));
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref payload, state);
        reader.TryConsumeArrayStart();
        bool flag = false; int number = 0; string text = string.Empty; List<int> values = new();
        reader.Read(ref flag);
        reader.Read(ref number);
        reader.Read(ref text);
        reader.Read(ref values);
        Assert.True(flag);
        Assert.Equal(1729, number);
        Assert.Equal("text", text);
        Assert.Equal(new[] { 3, 5, 7 }, values);
        reader.ConsumeArrayEnd();
        reader.EnsureEndOfDocument();
        Assert.Equal(payload.Length, reader.Position);
    }

    /// <summary>Retains generic payload constraints and explicit profiles in fluent forwarding shells.</summary>
    [Fact]
    public void GenericPayloadUsesFluentOperations()
    {
        Span<byte> bytes = new byte[256];
        var writer = new LuminPackWriter(ref bytes);
        var value = new FluentGenericValue<int> { Value = 1729, Text = "generic" };
        writer.Write<int, LuminPackUtf16LengthOptions>(value);
        ReadOnlySpan<byte> payload = bytes[..writer.Position];
        var reader = new LuminPackReader(ref payload);
        var result = new FluentGenericValue<int>();
        var original = result;
        reader.Read<int, LuminPackUtf16LengthOptions>(ref result);
        Assert.Same(original, result);
        Assert.Equal(value.Value, result.Value);
        Assert.Equal(value.Text, result.Text);
        Assert.Equal(writer.Position, reader.Position);
    }

    /// <summary>Uses logical bit counts rather than excess backing capacity and leaves the next record intact.</summary>
    /// <param name="length">The logical bit count written before a following scalar.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(32)]
    [InlineData(65)]
    [InlineData(4097)]
    public void BitArrayUsesLogicalPayloadLength(int length)
    {
        var value = new System.Collections.BitArray(length + 128, true) { Length = length };
        for (int index = 0; index < length; index++) value[index] = index % 3 == 0;
        Span<byte> bytes = new byte[4096];
        var writer = new LuminPackWriter(ref bytes);
        writer.Write(value);
        Assert.Equal(4 + ((length + 31) / 32) * 4, writer.Position);
        writer.Write(1729);
        ReadOnlySpan<byte> payload = bytes[..writer.Position];
        var reader = new LuminPackReader(ref payload);
        var result = new System.Collections.BitArray(length, false);
        var original = result;
        reader.Read(ref result);
        Assert.Same(original, result);
        Assert.Equal(length, result.Length);
        for (int index = 0; index < length; index++) Assert.Equal(value[index], result[index]);
        Assert.Equal(1729, reader.Read<int>());
        Assert.Equal(writer.Position, reader.Position);
    }

    /// <summary>Composes scalar, string and closed BCL operations without external cursor bookkeeping.</summary>
    [Fact]
    public void CompleteOperationsAdvanceExactlyOnceAndReuseCollections()
    {
        Span<byte> bytes = new byte[1024];
        var writer = new LuminPackWriter(ref bytes);
        int[] numbers = { 3, 5, 7 };
        var dates = new List<DateTime> { new(123456789), new(987654321) };
        var map = new Dictionary<int, string> { [2] = "second", [9] = "ninth" };
        writer.Write(1729);
        writer.Write<LuminPackUtf16LengthOptions>("text");
        writer.Write(numbers);
        writer.Write(dates);
        writer.Write(map);
        writer.Write((byte)91);
        int length = writer.Position;
        ReadOnlySpan<byte> payload = bytes[..length];
        var reader = new LuminPackReader(ref payload);
        Assert.Equal(1729, reader.Read<int>());
        string? text = "text";
        reader.Read<LuminPackUtf16LengthOptions>(ref text);
        Assert.Equal("text", text);
        var arrayTarget = new int[3];
        var listTarget = new List<DateTime>(2) { default, default };
        var mapTarget = new Dictionary<int, string> { [2] = "second", [9] = "ninth" };
        var originalArray = arrayTarget;
        var originalList = listTarget;
        var originalMap = mapTarget;
        reader.Read(ref arrayTarget);
        reader.Read(ref listTarget);
        reader.Read(ref mapTarget);
        Assert.Equal((byte)91, reader.Read<byte>());
        Assert.Equal(length, reader.Position);
        Assert.Same(originalArray, arrayTarget);
        Assert.Same(originalList, listTarget);
        Assert.Same(originalMap, mapTarget);
        Assert.Equal(numbers, arrayTarget);
        Assert.Equal(dates, listTarget);
        Assert.Equal(map, mapTarget);
    }

    /// <summary>Keeps portable contracts on the generated serializer entry while raw instance operations retain native storage.</summary>
    [Fact]
    public void PortableScalarSelectsItsConcreteContract()
    {
        var value = new FluentPortableValue { Tag = 17, Number = 0x123456789abcdef };
        Span<byte> bytes = new byte[32];
        var writer = new LuminPackWriter(ref bytes);
        LuminPackSerializer.Serialize(ref writer, in value);
        Assert.Equal(9, writer.Position);
        Assert.Equal((byte)17, bytes[0]);
        Assert.Equal(value.Number, BitConverter.ToInt64(bytes[1..]));
        ReadOnlySpan<byte> payload = bytes[..writer.Position];
        var reader = new LuminPackReader(ref payload);
        FluentPortableValue result = default;
        LuminPackSerializer.Deserialize(ref reader, ref result);
        Assert.Equal(value, result);
        Assert.Equal(9, reader.Position);
    }

    /// <summary>A failed compound write leaves the buffer's last complete root boundary published.</summary>
    [Fact]
    public void FailedRootDoesNotPublishPartialBytes()
    {
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(1729, buffer);
        byte[] prefix = buffer.GetSpan().ToArray();
        var value = new FluentThrowingValue { Text = "complete field before the callback" };
        Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(value, buffer));
        Assert.Equal(prefix, buffer.GetSpan().ToArray());
        buffer.ResetForReuse();
        LuminPackSerializer.Serialize(91, buffer);
        Assert.Equal(BitConverter.GetBytes(91), buffer.GetSpan().ToArray());
    }
}

/// <summary>Exercises nullable unmanaged collection storage through generated roots.</summary>
[LuminPackable]
public partial class FluentNullableCollections
{
    public DateTime?[] Array = [];
    public List<DateTime?> List = [];
}

/// <summary>A definition-owned object contract that can close over either values or references.</summary>
[LuminPackable]
public struct FluentOpenStruct<T>
{
    public T Value;
}

/// <summary>Provides an open payload shape for concrete generic extension generation.</summary>
[LuminPackable]
public partial class FluentGenericValue<T> where T : unmanaged
{
    public T Value;
    public string Text = string.Empty;
}

/// <summary>Uses a packed wire contract while retaining the CLR's padded storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Sequential)]
public partial struct FluentPortableValue
{
    public byte Tag;
    public long Number;
}

/// <summary>Throws after writing its fields to test successful-boundary publication.</summary>
[LuminPackable]
public partial class FluentThrowingValue
{
    public string Text = string.Empty;

    /// <summary>Throws the user exception after serialization has consumed the member data.</summary>
    [LuminPackOnSerialized]
    public void AfterWrite() => throw new InvalidOperationException("Expected post-write callback failure.");
}
