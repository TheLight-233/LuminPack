#if LUMINPACK_NEXT_WIRE
using System.Runtime.InteropServices;
using LuminPack.Attribute;

namespace LuminPack.WireCompatibility;

internal static partial class WireFixtures
{
    /// <summary>Verifies both retained layout tags and the corresponding native write of an automatic aggregate.</summary>
    /// <param name="directory">The retained fixture directory.</param>
    private static void VerifyTaggedAutomaticFixtures(string directory)
    {
        byte[] layout32 = File.ReadAllBytes(Path.Combine(directory, "tagged-auto-layout-32.bin"));
        byte[] layout64 = File.ReadAllBytes(Path.Combine(directory, "tagged-auto-layout-64.bin"));
        WireTaggedAutomatic[] expected = new WireTaggedAutomatic[2];
        MemoryMarshal.AsBytes(expected.AsSpan()).Clear();
        expected[0].Key = 1;
        expected[0].Value.First = 2;
        expected[0].Value.Second = 3;
        expected[1].Key = 4;
        expected[1].Value.First = 5;
        expected[1].Value.Second = 6;

        WireTaggedAutomatic[]? values = null;
        Check(LuminPackSerializer.Deserialize(layout32, ref values) == layout32.Length && values!.SequenceEqual(expected),
            "tagged automatic layout 32 read");
        Check(LuminPackSerializer.Deserialize(layout64, ref values) == layout64.Length && values!.SequenceEqual(expected),
            "tagged automatic layout 64 read");

        byte[] native = LuminPackSerializer.Serialize(expected);
        Check(native.AsSpan().SequenceEqual(layout32) || native.AsSpan().SequenceEqual(layout64),
            "tagged automatic write uses a retained model");
    }
}

/// <summary>Supplies the aggregate field whose enclosing stride can vary between runtimes.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public partial struct WireTaggedAutomaticValue
{
    /// <summary>The first field of the embedded struct.</summary>
    public int First;

    /// <summary>The second field of the embedded struct.</summary>
    public int Second;
}

/// <summary>Exercises an automatic layout in the Packed-default fixture assembly without inheriting Packed storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
[StructLayout(LayoutKind.Auto)]
public partial struct WireTaggedAutomatic
{
    /// <summary>The primitive field of the automatic aggregate.</summary>
    public int Key;

    /// <summary>The embedded value field of the automatic aggregate.</summary>
    public WireTaggedAutomaticValue Value;
}
#endif
