#if LUMINPACK_NEXT_GENERATOR
extern alias NinoLimits;

using Xunit;
using Fixture = NinoLimits::LuminPack.Tests.InputLimits.NinoCompleteInputFixture;

namespace LuminPack.Tests;

/// <summary>Validates the opt-in binary document read boundary.</summary>
public class NinoCompleteInputRegressionTest
{
    /// <summary>Each by-value document path rejects a byte after the last record field.</summary>
    [Fact]
    public void SpanSequenceBufferAndRegisteredDocumentsRejectSuffixes()
    {
        byte[] exact = LuminPackSerializer.Serialize(42);
        byte[] withSuffix = [.. exact, 0];
        Assert.Equal(42, Fixture.ReadSpanDocument(exact));
        Assert.Equal(42, Fixture.ReadSequenceDocument(exact, segmented: false));
        Assert.Equal(42, Fixture.ReadSequenceDocument(exact, segmented: true));
        Assert.Equal(42, Fixture.ReadBufferDocument(exact));
        Assert.Equal(42, Fixture.ReadProfiledDocument(exact));
        Assert.Equal(42, Fixture.ReadRegisteredDocument<int>(exact));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadSpanDocument(withSuffix));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadSequenceDocument(withSuffix, segmented: false));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadSequenceDocument(withSuffix, segmented: true));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadBufferDocument(withSuffix));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadProfiledDocument(withSuffix));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadRegisteredDocument<int>(withSuffix));
    }

    /// <summary>The null-marker read still checks document completion.</summary>
    [Fact]
    public void NullDocumentRejectsSuffix()
    {
        Assert.Null(Fixture.ReadNullableDocument([255]));
        Assert.Throws<InvalidDataException>(() => Fixture.ReadNullableDocument([255, 0]));
    }

    /// <summary>By-reference and advancing overloads consume only the first record.</summary>
    [Fact]
    public void PrefixReadsKeepTheirConsumedByteCount()
    {
        byte[] record = LuminPackSerializer.Serialize(42);
        byte[] withSuffix = [.. record, 0];
        Assert.Equal((42, record.Length), Fixture.ReadSpanPrefix(withSuffix));
        Assert.Equal((42, record.Length), Fixture.ReadAdvancingPrefix(withSuffix));
        Assert.Equal(42, LuminPackSerializer.Deserialize<int>(withSuffix));
    }

    /// <summary>Stream documents with or without the compatibility value reject suffixes.</summary>
    /// <param name="withValueArgument">Whether the overload takes the compatibility value argument.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamDocumentsRejectSuffixes(bool withValueArgument)
    {
        byte[] exact = LuminPackSerializer.Serialize(42);
        byte[] withSuffix = [.. exact, 0];
        Assert.Equal(42, await Fixture.ReadStreamDocument(exact, withValueArgument));
        await Assert.ThrowsAsync<InvalidDataException>(() => Fixture.ReadStreamDocument(withSuffix, withValueArgument));
    }
}
#endif
