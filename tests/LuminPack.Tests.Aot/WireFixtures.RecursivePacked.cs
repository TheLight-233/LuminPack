#if LUMINPACK_NEXT_WIRE
using System.Runtime.InteropServices;
using LuminPack.Attribute;

namespace LuminPack.WireCompatibility;

internal static partial class WireFixtures
{
    private const string RecursivePackedName = "recursive-packed.bin";

    /// <summary>Writes the same packed fields from an ordinary struct, framework pair, and value tuple.</summary>
    /// <param name="directory">The destination fixture directory.</param>
    private static void WriteRecursivePackedFixture(string directory)
    {
        var values = RecursivePackedValues();
        var pairs = values.Select(value => KeyValuePair.Create(value.Key, value.Value)).ToArray();
        var tuples = values.Select(value => (value.Key, value.Value)).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(values);
        Check(bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(pairs)), "recursive packed pair wire");
        Check(bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(tuples)), "recursive packed tuple wire");
        File.WriteAllBytes(Path.Combine(directory, RecursivePackedName), bytes);
    }

    /// <summary>Verifies the canonical record against all three generated readers.</summary>
    /// <param name="directory">The retained fixture directory.</param>
    private static void VerifyRecursivePackedFixture(string directory)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, RecursivePackedName));
        var expected = RecursivePackedValues();
        WireRecursivePackedOuter[]? values = null;
        Check(LuminPackSerializer.Deserialize(bytes, ref values) == bytes.Length && values!.SequenceEqual(expected),
            "recursive packed struct read");
        KeyValuePair<int, WirePackedValue>[]? pairs = null;
        Check(LuminPackSerializer.Deserialize(bytes, ref pairs) == bytes.Length &&
            pairs!.SequenceEqual(expected.Select(value => KeyValuePair.Create(value.Key, value.Value))), "recursive packed pair read");
        (int, WirePackedValue)[]? tuples = null;
        Check(LuminPackSerializer.Deserialize(bytes, ref tuples) == bytes.Length &&
            tuples!.SequenceEqual(expected.Select(value => (value.Key, value.Value))), "recursive packed tuple read");
    }

    /// <summary>Seeds the unused storage with a known value before setting each serialized field.</summary>
    /// <returns>The fixture records.</returns>
    private static WireRecursivePackedOuter[] RecursivePackedValues()
    {
        var values = new WireRecursivePackedOuter[2];
        MemoryMarshal.AsBytes(values.AsSpan()).Fill(0xCC);
        values[0].Key = 11;
        values[0].Value.Code = 0x12;
        values[0].Value.Amount = 0x0102030405060708L;
        values[1].Key = -7;
        values[1].Value.Code = 0x34;
        values[1].Value.Amount = -0x0102030405060708L;
        return values;
    }
}

/// <summary>Supplies a nested packed value whose native storage contains padding.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct WirePackedValue
{
    /// <summary>The first packed field.</summary>
    public byte Code;

    /// <summary>The second packed field.</summary>
    public long Amount;
}

/// <summary>Exercises a Tagged aggregate that contains a Packed field.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public partial struct WireRecursivePackedOuter
{
    /// <summary>The field before the nested value.</summary>
    public int Key;

    /// <summary>The field whose packed contract applies recursively.</summary>
    public WirePackedValue Value;
}
#endif
