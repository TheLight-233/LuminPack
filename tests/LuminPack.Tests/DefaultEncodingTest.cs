using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the default UTF-16 contract and independent explicit UTF-8 paths.</summary>
public sealed class DefaultEncodingTest
{
    /// <summary>Checks exact default framing and concrete by-reference reads.</summary>
    /// <param name="value">A nullable Unicode fixture.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ASCII")]
    [InlineData("A\u00e9\u4e2d\ud83d\ude00")]
    public void DefaultStringRecordsUseUtf16(string? value)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        // Nonempty Latin-1 text uses the compact one-byte form with an odd header; other text keeps raw code units.
        bool compact = !string.IsNullOrEmpty(value) && value.All(c => c <= '\u00ff');
        Assert.Equal(4 + (value?.Length ?? 0) * (compact ? 1 : 2), bytes.Length);
        Assert.Equal(value == null ? -1 : compact ? (value.Length << 1) | 1 : value.Length * 2, BitConverter.ToInt32(bytes));
        string? restored = "previous";
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref restored));
        Assert.Equal(value, restored);
        Assert.Equal(LuminPackStringEncoding.UTF16, default(LuminPackDefaultOptions).StringEncoding);
        Assert.Equal(LuminPackStringEncoding.UTF16, new LuminPackOptionAttribute().StringEncoding);
    }

    /// <summary>Checks explicit UTF-8 lengths and token records after changing the library default.</summary>
    [Fact]
    public void ExplicitUtf8ProfilesRemainIndependent()
    {
        const string value = "A\u00e9\u4e2d\ud83d\ude00";
        byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(value);
        Assert.Equal(8 + Encoding.UTF8.GetByteCount(value), bytes.Length);
        Assert.Equal(Encoding.UTF8.GetBytes(value), bytes.AsSpan(8).ToArray());
        string? restored = null;
        LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes, ref restored);
        Assert.Equal(value, restored);
        byte[] token = LuminPackSerializer.Serialize<LuminPackUtf8TokenOptions>(value);
        Assert.Equal(0, token[^1]);
        LuminPackSerializer.Deserialize<LuminPackUtf8TokenOptions>(token, ref restored);
        Assert.Equal(value, restored);
    }

    /// <summary>Checks chunked UTF-8 comparison without routing through the UTF-16 default converter.</summary>
    [Fact]
    public void ExplicitUtf8LongReuseRetainsTheExistingString()
    {
        string value = new string('\u00e9', 400) + "\u4e2d\ud83d\ude00";
        byte[] bytes = LuminPackSerializer.Serialize<LuminPackUtf8LengthOptions>(value);
        string? restored = new string(value.ToCharArray());
        string existing = restored;
        LuminPackSerializer.Deserialize<LuminPackUtf8LengthOptions>(bytes, ref restored);
        Assert.Same(existing, restored);
    }

    /// <summary>Checks that explicit member profiles and reusable nested lists keep the same decoded values.</summary>
    [Fact]
    public void MixedMemberProfilesPreserveReuse()
    {
        var value = new MixedModel { Text = "\u4e2d", Utf8 = "\u00e9", Items = new() { "\ud83d\ude00", null, "" } };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        MixedModel restored = null!;
        LuminPackSerializer.Deserialize(bytes, ref restored);
        var originalList = restored.Items;
        Assert.Equal(value.Text, restored.Text);
        Assert.Equal(value.Utf8, restored.Utf8);
        Assert.Equal(value.Items, restored.Items);
        LuminPackSerializer.Deserialize(bytes, ref restored);
        Assert.Same(originalList, restored.Items);
    }

    /// <summary>Checks default conversion and the explicit UTF-8 mode without record framing.</summary>
    [Fact]
    public void ConversionDefaultsAndExplicitModesAgree()
    {
        const string value = "\u00e9\u4e2d\ud83d\ude00";
        Span<byte> output = stackalloc byte[64];
        Assert.Equal(OperationStatus.Done, StringSerializer.Serialize(value, output, out _, out int count));
        Assert.Equal(Encoding.Unicode.GetBytes(value), output[..count].ToArray());
        Span<char> text = stackalloc char[32];
        Assert.Equal(OperationStatus.Done, StringSerializer.Deserialize(output[..count], text, out _, out int written));
        Assert.Equal(value, text[..written].ToString());
        Assert.Equal(OperationStatus.Done, StringSerializer.Serialize(value, output, out _, out count, SerializeMode.Utf8));
        Assert.Equal(Encoding.UTF8.GetBytes(value), output[..count].ToArray());
    }

    /// <summary>Checks a declared UTF-8 document's first token through the concrete Reader extension.</summary>
    [Fact]
    public void JsonStateReadUsesTheDeclaredEncoding()
    {
        var value = new Utf8Document { Text = "\u00e9\u4e2d" };
        string json = LuminPackSerializer.SerializeJson(value);
        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(json);
        var reader = new LuminPackJsonReader(ref input, new LuminPackReaderOptionalState());
        Utf8Document restored = null!;
        reader.Read(ref restored);
        Assert.Equal(value.Text, restored.Text);
    }

    /// <summary>Supplies a mixed binary profile with a default-encoding JSON document.</summary>
    [LuminPackable]
    public sealed class MixedModel
    {
        /// <summary>Stores the default-encoded member.</summary>
        public string Text = "";
        /// <summary>Stores the explicitly UTF-8 member.</summary>
        [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
        public string Utf8 = "";
        /// <summary>Stores reusable nullable strings.</summary>
        public List<string?> Items = new();
    }

    /// <summary>Supplies a document whose root overrides the library encoding.</summary>
    [LuminPackable]
    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]
    public sealed class Utf8Document
    {
        /// <summary>Stores non-ASCII document text.</summary>
        public string Text = "";
    }
}
