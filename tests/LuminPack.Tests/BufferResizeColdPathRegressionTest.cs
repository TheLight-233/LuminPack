using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides buffer resize cold path regression test functionality.</summary>
internal static class BufferResizeColdPathRegressionTest
{
    private const int GrowthBlockLength = 230_000;

    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ManagedCursorsSurviveCompactionAndGrowth), ManagedCursorsSurviveCompactionAndGrowth);
        RunCase(results, nameof(RentedStorageIsAlignedAndCleared), RentedStorageIsAlignedAndCleared);
        RunCase(results, nameof(BufferWriterContractAppendsAfterPayload), BufferWriterContractAppendsAfterPayload);
        RunCase(results, nameof(CapacityPolicyUses32KiBAndFiftyPercentGrowth),
            CapacityPolicyUses32KiBAndFiftyPercentGrowth);
        RunCase(results, nameof(BinaryCheckPreservesBytesAcrossMultipleGrowths),
            BinaryCheckPreservesBytesAcrossMultipleGrowths);
        RunCase(results, nameof(LazyInitialAllocationOccursOnceAndPreservesBytes),
            LazyInitialAllocationOccursOnceAndPreservesBytes);
        RunCase(results, nameof(JsonCheckPreservesValidPayloadAcrossMultipleGrowths),
            JsonCheckPreservesValidPayloadAcrossMultipleGrowths);
        RunCase(results, nameof(LargeGeneratedStringAndArrayRoundTripAfterGrowth),
            LargeGeneratedStringAndArrayRoundTripAfterGrowth);
    }

    /// <summary>Checks live binary cursors across compacting collections and relocating growth.</summary>
    private static void ManagedCursorsSurviveCompactionAndGrowth()
    {
        using var buffer = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(buffer);
        writer.Write(1234567);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        writer.Write(7654321);
        writer.EnsureAdditionalCapacity(100_000);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        writer.Write(2468);
        var payload = writer.GetSpan();
        Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload) == 1234567 &&
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4)) == 7654321 &&
            System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8)) == 2468,
            "Compaction or growth invalidated a live binary cursor.");
    }

    /// <summary>Checks the rented start alignment, in-place growth and clearing of reused storage.</summary>
    private static void RentedStorageIsAlignedAndCleared()
    {
        using (var dirty = new LuminBufferWriter(true))
            dirty.GetFullSpan().Fill(0xA5);

        using var buffer = new LuminBufferWriter(true);
        var initial = buffer.GetFullSpan();
        Assert((Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref MemoryMarshal.GetReference(initial)) & 63) == 0,
            "The rented storage did not start on a cache-line boundary.");
        Assert(initial.IndexOfAnyExcept((byte)0) < 0, "Rented storage exposed bytes of an earlier owner.");

        initial[0] = 0x6D;
        ref byte start = ref MemoryMarshal.GetReference(initial);
        buffer.EnsureCapacity(buffer.TotalLength + 1);
        var grown = buffer.GetFullSpan();
        Assert(Unsafe.AreSame(ref start, ref MemoryMarshal.GetReference(grown)) && grown.Length == 48 * 1024,
            "A growth that fits the power-of-two rental relocated the storage.");
        Assert(grown[0] == 0x6D && grown.Slice(1).IndexOfAnyExcept((byte)0) < 0,
            "In-place growth lost the payload or exposed stale bytes.");
    }

    /// <summary>Checks the <see cref="System.Buffers.IBufferWriter{T}"/> contract against the published payload.</summary>
    private static void BufferWriterContractAppendsAfterPayload()
    {
        using var buffer = new LuminBufferWriter(false);
        System.Buffers.IBufferWriter<byte> output = buffer;
        var span = output.GetSpan();
        Assert(span.Length > 0 && buffer.UseFirstBuffer, "GetSpan(0) did not return a nonempty buffer.");
        span[0] = 1;
        span[1] = 2;
        output.Advance(2);
        output.GetMemory(3).Span[0] = 3;
        output.Advance(1);
        Assert(buffer.CurrentIndex == 3 && buffer.GetSpan().SequenceEqual(new byte[] { 1, 2, 3 }),
            "Advance did not append after the published payload.");

        int large = buffer.TotalLength * 4;
        Assert(output.GetSpan(large).Length >= large && buffer.GetSpan().SequenceEqual(new byte[] { 1, 2, 3 }),
            "A size hint beyond capacity did not grow while preserving the payload.");

        bool rejected = false;
        try { output.Advance(buffer.TotalLength); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected && buffer.CurrentIndex == 3, "Advance accepted a count beyond the free capacity.");
    }

    /// <summary>Verifies capacity policy uses32 ki b and fifty percent growth.</summary>
    private static void CapacityPolicyUses32KiBAndFiftyPercentGrowth()
    {
        using var buffer = new LuminBufferWriter(true);
        Assert(buffer.TotalLength == 32 * 1024,
            "The native writer should start at 32 KiB instead of retaining 256 KiB per pooled instance.");

        var initialCapacity = buffer.TotalLength;
        buffer.EnsureCapacity(initialCapacity + 1);
        Assert(buffer.TotalLength == initialCapacity * 3 / 2,
            "The first capacity expansion did not use the configured 1.5x growth factor.");

        var firstGrowthCapacity = buffer.TotalLength;
        buffer.EnsureCapacity(firstGrowthCapacity + 1);
        Assert(buffer.TotalLength == firstGrowthCapacity * 3 / 2,
            "Repeated capacity expansion did not preserve the configured 1.5x growth factor.");

        using var largeReserve = new LuminBufferWriter(false);
        largeReserve.EnsureCapacity(1_000_000);
        Assert(largeReserve.TotalLength >= 1_000_000 && largeReserve.TotalLength <= 1_170_000,
            "A direct large reservation retained excessive headroom.");
    }

    /// <summary>Verifies lazy initial allocation occurs once and preserves bytes.</summary>
    private static void LazyInitialAllocationOccursOnceAndPreservesBytes()
    {
        using var buffer = new LuminBufferWriter(false);
        Assert(!buffer.UseFirstBuffer && buffer.TotalLength == 0,
            "A lazy buffer unexpectedly allocated storage in its constructor.");

        var first = buffer.GetFullSpan();
        Assert(buffer.UseFirstBuffer && first.Length > 0,
            "GetFullSpan did not perform the lazy initial allocation.");
        first[0] = 0x6D;
        var firstCapacity = buffer.TotalLength;

        var second = buffer.GetFullSpan();
        Assert(buffer.TotalLength == firstCapacity && second[0] == 0x6D,
            "An existing buffer was reallocated or cleared by a later GetFullSpan call.");
    }

    /// <summary>Verifies binary check preserves bytes across multiple growths.</summary>
    private static void BinaryCheckPreservesBytesAcrossMultipleGrowths()
    {
        using var buffer = new LuminBufferWriter(true);
        var initialCapacity = buffer.TotalLength;
        var writer = new LuminPackWriter(buffer);

        WriteBlockAndCheck(ref writer, GrowthBlockLength, 0x3A);
        var firstGrowthCapacity = buffer.TotalLength;
        WriteBlockAndCheck(ref writer, GrowthBlockLength, 0xC7);
        var secondGrowthCapacity = buffer.TotalLength;

        Assert(firstGrowthCapacity > initialCapacity,
            "The first binary threshold crossing did not grow the buffer.");
        Assert(secondGrowthCapacity > firstGrowthCapacity,
            "The second binary threshold crossing did not grow the buffer.");

        var payload = writer.GetSpan();
        Assert(payload.Length == GrowthBlockLength * 2,
            "Binary growth published an unexpected byte count.");
        Assert(AllEqual(payload[..GrowthBlockLength], 0x3A) &&
               AllEqual(payload[GrowthBlockLength..], 0xC7),
            "Binary growth lost or changed bytes written before a native-buffer relocation.");
    }

    /// <summary>Verifies json check preserves valid payload across multiple growths.</summary>
    private static void JsonCheckPreservesValidPayloadAcrossMultipleGrowths()
    {
        using var buffer = new LuminBufferWriter(true);
        var state = new LuminPackWriterOptionalState();
        var writer = new LuminPackJsonWriter(buffer, state);
        var first = new string('A', GrowthBlockLength);
        var second = new string('B', GrowthBlockLength);
        var initialCapacity = buffer.TotalLength;

        writer.WriteArrayStartFor(new TestSerializationProfile
        {
            StringEncoding = LuminPackStringEncoding.UTF8
        });
        writer.WriteFor(new TestSerializationProfile
        {
            StringEncoding = LuminPackStringEncoding.UTF8
        },first);
        buffer.Check(ref writer);
        var firstGrowthCapacity = buffer.TotalLength;
        writer.WriteFor(new TestSerializationProfile
        {
            StringEncoding = LuminPackStringEncoding.UTF8
        },second);
        buffer.Check(ref writer);
        var secondGrowthCapacity = buffer.TotalLength;
        writer.WriteArrayEndFor(new TestSerializationProfile
        {
            StringEncoding = LuminPackStringEncoding.UTF8
        });

        Assert(firstGrowthCapacity > initialCapacity,
            "The first JSON threshold crossing did not grow the buffer.");
        Assert(secondGrowthCapacity > firstGrowthCapacity,
            "The second JSON threshold crossing did not grow the buffer.");

        using var document = JsonDocument.Parse(writer.GetSpan().ToArray());
        var values = document.RootElement.EnumerateArray().ToArray();
        Assert(values.Length == 2 && values[0].GetString() == first && values[1].GetString() == second,
            "JSON growth corrupted a string written before or after a native-buffer relocation.");
    }

    /// <summary>Verifies large generated string and array round trip after growth.</summary>
    private static void LargeGeneratedStringAndArrayRoundTripAfterGrowth()
    {
        var value = new MultiGrowthPayload
        {
            Prefix = new string('\u754c', 100_000),
            Values = Enumerable.Range(0, 200_000).ToArray(),
            Suffix = new string('Z', 230_000)
        };
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(value, buffer);

        Assert(buffer.CurrentIndex > 1_000_000 && buffer.TotalLength > 1_000_000,
            "The generated binary payload did not cross the expected growth boundaries.");
        // The buffer holds the default profile's records, so it is read with that same profile.
        var result = LuminPackSerializer.Deserialize<MultiGrowthPayload>(buffer.GetSpan());
        Assert(result is not null && result.Prefix == value.Prefix && result.Suffix == value.Suffix &&
               result.Values.SequenceEqual(value.Values),
            "A generated payload containing both a large string and a large array failed after growth.");
    }

    /// <summary>Writes block and check to the current output.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void WriteBlockAndCheck(ref LuminPackWriter writer, int count, byte value)
    {
        writer.EnsureAdditionalCapacity(count);
        MemoryMarshal.CreateSpan(ref writer.CurrentReference, count).Fill(value);
        writer.Advance(count);
        writer.CheckBuffer();
    }

    /// <summary>Verifies all equal.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool AllEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        foreach (var value in bytes)
        {
            if (value != expected)
                return false;
        }

        return true;
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

/// <summary>Provides multi growth payload functionality.</summary>
[LuminPackable]
public sealed partial class MultiGrowthPayload
{
    /// <summary>Gets or sets the prefix.</summary>
    public string Prefix { get; set; } = string.Empty;
    /// <summary>Gets or sets the values.</summary>
    public int[] Values { get; set; } = [];
    /// <summary>Gets or sets the suffix.</summary>
    public string Suffix { get; set; } = string.Empty;
}
