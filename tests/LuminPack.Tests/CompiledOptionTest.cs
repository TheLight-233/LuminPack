using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects declaration precedence, profile propagation and operation ownership.</summary>
public partial class CompiledOptionTest
{
    /// <summary>Checks fixed member scopes, null strings, containers, recursion and exact size evaluation.</summary>
    [Fact]
    public void DeclaredMemberProfilesSurviveContainersAndRecursiveBoundaries()
    {
        var value = new DeclaredRecord { Text = "\u4E2D\uD83D\uDE00", Utf8 = "ascii", Children = [new() { Text = "child" }],
            Head = new() { Text = "head", Next = new() { Text = "tail" } }, Map = new() { ["one"] = "\u4E2D", ["two"] = "" } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        var copy = LuminPackSerializer.Deserialize<DeclaredRecord>(bytes);
        Assert.Equal(value.Text, copy.Text);
        Assert.Equal(value.Utf8, copy.Utf8);
        Assert.Equal(value.Children[0].Text, copy.Children[0].Text);
        Assert.Equal(value.Head.Next!.Text, copy.Head.Next!.Text);
        Assert.Equal(value.Map, copy.Map);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(value));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        string json = LuminPackSerializer.SerializeJson(value);
        var jsonCopy = LuminPackSerializer.DeserializeJson<DeclaredRecord>(json);
        Assert.Equal(value.Head.Next.Text, jsonCopy.Head.Next!.Text);
        Assert.Equal(value.Map, jsonCopy.Map);
    }

    /// <summary>Exercises production convenience, async, buffer and segmented APIs under explicit stateless profiles.</summary>
    /// <returns>The completed asynchronous profile matrix.</returns>
    [Fact]
    public async Task ExplicitProfilesReachEveryConvenienceBoundary()
    {
        await CheckProfile<LuminPackDefaultOptions>();
        await CheckProfile<LuminPackUtf8TokenOptions>();
        await CheckProfile<LuminPackUtf16LengthOptions>();
        await CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks one closed profile without a runtime configuration parameter.</summary>
    /// <typeparam name="TOptions">The stateless profile.</typeparam>
    /// <returns>The completed regression.</returns>
    private static async Task CheckProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var value = new PlainRecord { Text = "\u4E2D\uD83D\uDE00", Items = ["a", "", "tail"] };
        byte[] bytes = LuminPackSerializer.Serialize<PlainRecord, TOptions>(value);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<PlainRecord, TOptions>(value));
        Assert.Equal(value.Text, LuminPackSerializer.Deserialize<PlainRecord, TOptions>(bytes).Text);
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize<PlainRecord, TOptions>(value, buffer);
        Assert.Equal(bytes, buffer.GetSpan().ToArray());
        Assert.Equal(value.Items, LuminPackSerializer.Deserialize<PlainRecord, TOptions>(buffer).Items);
        string json = LuminPackSerializer.SerializeJson<PlainRecord, TOptions>(value);
        Assert.Equal(value.Items, LuminPackSerializer.DeserializeJson<PlainRecord, TOptions>(json).Items);
        var streamBytes = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<PlainRecord, TOptions>(value, streamBytes);
        PlainRecord parsed = null!;
        Assert.Equal(streamBytes.WrittenCount, LuminPackSerializer.DeserializeJson<PlainRecord, TOptions>(streamBytes.WrittenSpan, ref parsed));
        Assert.Equal(value.Text, parsed.Text);
        using var stream = new MemoryStream();
        await LuminPackSerializer.SerializeAsync<PlainRecord, TOptions>(stream, value);
        Assert.Equal(bytes, stream.ToArray());
        stream.Position = 0;
        Assert.Equal(value.Items, (await LuminPackSerializer.DeserializeAsync<PlainRecord, TOptions>(stream)).Items);
    }

    /// <summary>Checks null and empty framing lengths without writing past exact caller spans.</summary>
    [Fact]
    public void StringRecordSizesAndNullBoundariesMatchEachClosedProfile()
    {
        CheckStringBounds<LuminPackDefaultOptions>();
        CheckStringBounds<LuminPackUtf8TokenOptions>();
        CheckStringBounds<LuminPackUtf16LengthOptions>();
        CheckStringBounds<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks that every nested collection publishes its final cursor, including empty and null-only payloads.</summary>
    [Fact]
    public void EmptyAndNullOnlyNestedCollectionsPreserveFollowingFields()
    {
        foreach (string?[] items in new string?[][] { [], [null], [null, null], ["", null, "tail"] })
        {
            var value = new CollectionTail { List = items.ToList(), Array = items, Tail = 0x12345678 };
            CheckCollectionTail<LuminPackDefaultOptions>(value);
            CheckCollectionTail<LuminPackUtf8TokenOptions>(value);
            CheckCollectionTail<LuminPackUtf16LengthOptions>(value);
            CheckCollectionTail<LuminPackUtf16TokenOptions>(value);
        }
    }

    /// <summary>Verifies one closed profile's nested collection framing and final field.</summary>
    /// <typeparam name="TOptions">The stateless wire profile.</typeparam>
    /// <param name="value">The collection and trailing-field fixture.</param>
    private static void CheckCollectionTail<TOptions>(CollectionTail value) where TOptions : struct, ILuminPackOptions
    {
        byte[] bytes = LuminPackSerializer.Serialize<CollectionTail, TOptions>(value);
        var copy = LuminPackSerializer.Deserialize<CollectionTail, TOptions>(bytes);
        bool token = default(TOptions).StringRecording == LuminPackStringRecording.Token;
        Assert.Equal(value.List.Select(item => token ? item ?? "" : item), copy.List);
        Assert.Equal(value.Array.Select(item => token ? item ?? "" : item), copy.Array);
        Assert.Equal(value.Tail, copy.Tail);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<CollectionTail, TOptions>(value));
    }

    /// <summary>Checks closed Custom pointers, fixed member scopes and complete JSON document encoding.</summary>
    [Fact]
    public void CustomProfilesSurviveNestedAndExplicitBoundaries()
    {
        CheckCustomProfile<LuminPackDefaultOptions>();
        CheckCustomProfile<LuminPackUtf8TokenOptions>();
        CheckCustomProfile<LuminPackUtf16LengthOptions>();
        CheckCustomProfile<LuminPackUtf16TokenOptions>();
        CheckCustomProfile<DeepUtf16Token>();
        var equivalent = new OpaqueValue { Text = "equivalent profile" };
        byte[] equivalentBytes = LuminPackSerializer.Serialize<OpaqueValue, EquivalentUtf16Token>(equivalent);
        Assert.Equal(equivalent.Text, LuminPackSerializer.Deserialize<OpaqueValue, EquivalentUtf16Token>(equivalentBytes).Text);
        var value = new CustomHost { Value = new() { Text = "custom" } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        var restored = LuminPackSerializer.Deserialize<CustomHost>(bytes);
        Assert.Equal(value.Value.Text, restored.Value.Text);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(value));
    }

    /// <summary>Verifies an explicitly registered Custom profile through the production generic facade.</summary>
    /// <typeparam name="TOptions">The stateless wire profile.</typeparam>
    private static void CheckCustomProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        // The generated Custom registry already owns the declaration profile; registering it again is a duplicate.
        if (typeof(TOptions) != typeof(LuminPackDefaultOptions)) _ = CustomRegistration<TOptions>.Ready;
        var value = new OpaqueValue { Text = "custom-\u4E2D\uD83D\uDE00" };
        byte[] bytes = LuminPackSerializer.Serialize<OpaqueValue, TOptions>(value);
        Assert.Equal(value.Text, LuminPackSerializer.Deserialize<OpaqueValue, TOptions>(bytes).Text);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<OpaqueValue, TOptions>(value));
        string json = LuminPackSerializer.SerializeJson<OpaqueValue, TOptions>(value);
        Assert.Equal(value.Text, LuminPackSerializer.DeserializeJson<OpaqueValue, TOptions>(json).Text);
    }

    /// <summary>Closes and registers pointers once per profile, without binding configuration to an operation state.</summary>
    /// <typeparam name="TOptions">The stateless profile used by all five method pointers.</typeparam>
    private static class CustomRegistration<TOptions> where TOptions : struct, ILuminPackOptions
    {
        internal static readonly bool Ready = Initialize();

        /// <summary>Registers the five closed static Custom operations.</summary>
        /// <returns>Whether initialization completed.</returns>
        private static unsafe bool Initialize()
        {
            LuminPackSerializer.Register<OpaqueValue, TOptions>(&OpaqueValue.WriteCompiled<TOptions>, &OpaqueValue.ReadCompiled<TOptions>,
                &OpaqueValue.WriteJsonCompiled<TOptions>, &OpaqueValue.ReadJsonCompiled<TOptions>, &OpaqueValue.SizeCompiled<TOptions>);
            return true;
        }
    }

    /// <summary>Owns its representation and routes all string work through closed profile APIs.</summary>
    [LuminPackable(GeneratorType.Custom)]
    public sealed class OpaqueValue
    {
        public string Text = "";
        /// <summary>Writes the declaration-default representation.</summary>
        /// <param name="writer">The binary writer.</param>
        /// <param name="value">The Custom payload.</param>
        public static void Serialize(ref LuminPackWriter writer, in OpaqueValue value) => WriteCompiled<LuminPackDefaultOptions>(ref writer, value);
        /// <summary>Reads the declaration-default representation.</summary>
        /// <param name="reader">The binary reader.</param>
        /// <param name="value">The restored payload.</param>
        public static void Deserialize(ref LuminPackReader reader, ref OpaqueValue value) => ReadCompiled<LuminPackDefaultOptions>(ref reader, ref value);
        /// <summary>Writes the default UTF-8 document representation.</summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The Custom payload.</param>
        public static void SerializeJson(ref LuminPackJsonWriter writer, in OpaqueValue value) => WriteJsonCompiled<LuminPackDefaultOptions>(ref writer, value);
        /// <summary>Reads the default UTF-8 document representation.</summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="value">The restored payload.</param>
        public static void DeserializeJson(ref LuminPackJsonReader reader, ref OpaqueValue value) => ReadJsonCompiled<LuminPackDefaultOptions>(ref reader, ref value);
        /// <summary>Adds the default binary record size.</summary>
        /// <param name="evaluator">The evaluator.</param>
        /// <param name="value">The Custom payload.</param>
        public static void CalculateOffset(ref LuminPackEvaluator evaluator, in OpaqueValue value) => SizeCompiled<LuminPackDefaultOptions>(ref evaluator, value);
        /// <summary>Writes one explicitly configured binary string record.</summary>
        /// <typeparam name="TOptions">The stateless profile.</typeparam>
        /// <param name="writer">The binary writer.</param>
        /// <param name="value">The Custom payload.</param>
        public static void WriteCompiled<TOptions>(ref LuminPackWriter writer, in OpaqueValue value) where TOptions : struct, ILuminPackOptions
            => LuminPackSerializer.Serialize<string, TOptions>(ref writer, value.Text);
        /// <summary>Reads one explicitly configured binary string record.</summary>
        /// <typeparam name="TOptions">The stateless profile.</typeparam>
        /// <param name="reader">The binary reader.</param>
        /// <param name="value">The restored payload.</param>
        public static void ReadCompiled<TOptions>(ref LuminPackReader reader, ref OpaqueValue value) where TOptions : struct, ILuminPackOptions
        {
            string text = "";
            LuminPackSerializer.Deserialize<string, TOptions>(ref reader, ref text);
            value = new() { Text = text };
        }
        /// <summary>Writes one explicitly configured JSON string.</summary>
        /// <typeparam name="TOptions">The stateless profile.</typeparam>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The Custom payload.</param>
        public static void WriteJsonCompiled<TOptions>(ref LuminPackJsonWriter writer, in OpaqueValue value) where TOptions : struct, ILuminPackOptions
            => LuminPackSerializer.SerializeJson<string, TOptions>(ref writer, value.Text);
        /// <summary>Reads one explicitly configured JSON string.</summary>
        /// <typeparam name="TOptions">The stateless profile.</typeparam>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="value">The restored payload.</param>
        public static void ReadJsonCompiled<TOptions>(ref LuminPackJsonReader reader, ref OpaqueValue value) where TOptions : struct, ILuminPackOptions
        {
            string text = "";
            LuminPackSerializer.DeserializeJson<string, TOptions>(ref reader, ref text);
            value = new() { Text = text };
        }
        /// <summary>Adds one explicitly configured binary string record size.</summary>
        /// <typeparam name="TOptions">The stateless profile.</typeparam>
        /// <param name="evaluator">The evaluator.</param>
        /// <param name="value">The Custom payload.</param>
        public static void SizeCompiled<TOptions>(ref LuminPackEvaluator evaluator, in OpaqueValue value) where TOptions : struct, ILuminPackOptions
        {
            string text = value.Text;
            LuminPackSerializer.Sizeof<string, TOptions>(ref evaluator, ref text);
        }
    }

    /// <summary>Retains a fixed member profile across an opaque pointer boundary.</summary>
    [LuminPackable, LuminPackOption(MaxJsonDepth = 32)]
    public sealed class CustomHost
    {
        [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16, StringRecording = LuminPackStringRecording.Token)]
        public OpaqueValue Value = new();
    }

    /// <summary>Uses the same complete constants as the built-in token profile without another registration.</summary>
    public readonly struct EquivalentUtf16Token : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 1024;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Matches the composite profile generated by the host's depth and member's encoding declarations.</summary>
    public readonly struct DeepUtf16Token : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 32;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Verifies actual string records and their exact fixed-span capacity.</summary>
    /// <typeparam name="TOptions">The stateless wire profile.</typeparam>
    private static void CheckStringBounds<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        foreach (string? text in new[] { null, "", "abc" })
        {
            int header = default(TOptions).StringRecording == LuminPackStringRecording.Token
                ? default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 1
                : default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 ? 4 : 8;
            // Nonempty ASCII text in a UTF-16 length record uses the compact one-byte form.
            bool wide = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 &&
                (default(TOptions).StringRecording == LuminPackStringRecording.Token || string.IsNullOrEmpty(text));
            int exact = header + (text?.Length ?? 0) * (wide ? 2 : 1);
            byte[] guarded = Enumerable.Repeat((byte)0xCC, exact + 2).ToArray();
            Span<byte> target = guarded.AsSpan(1, exact);
            var bounded = new LuminPackWriter(ref target);
            bounded.Write<TOptions>(text);
            Assert.Equal(exact, bounded.Position);
            Assert.Equal(0xCC, guarded[0]);
            Assert.Equal(0xCC, guarded[^1]);
        }
    }

    /// <summary>Combines binary member-specific encodings with a whole UTF-16 JSON document.</summary>
    [LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16)]
    public sealed class DeclaredRecord
    {
        public string Text = "";
        [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)] public string Utf8 = "";
        public List<PlainRecord> Children = [];
        [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token)]
        public Node Head = new();
        [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token)]
        public Dictionary<string, string> Map = new();
    }

    /// <summary>Provides an unmarked payload whose strings inherit the compiled operation profile.</summary>
    [LuminPackable]
    public sealed class PlainRecord
    {
        public string Text = "";
        public List<string> Items = [];
    }

    /// <summary>Retains the recursive formatter boundary while inheriting a containing member's compiled profile.</summary>
    [LuminPackable]
    public sealed class Node
    {
        public string Text = "";
        public Node? Next;
    }

    /// <summary>Places a fixed field after two independently inlined dynamic containers.</summary>
    [LuminPackable]
    public sealed class CollectionTail
    {
        public List<string?> List = [];
        public string?[] Array = [];
        public int Tail;
    }
}
