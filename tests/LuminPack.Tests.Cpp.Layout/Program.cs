using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackCppLayoutFixtures;

/// <summary>Exercises portable layout fixtures through the managed default serializer.</summary>
internal static class Program
{
    /// <summary>Creates or verifies the fixtures for the generated C++ layout codecs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero for a completed operation, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is ["write-native", var nativeOutput])
        {
            Directory.CreateDirectory(nativeOutput);
            Write(nativeOutput, "layout-native-padded", CreateNative());
            return 0;
        }
        if (args is ["read-native", var nativeInput])
        {
            var native = CreateNative();
            Verify(nativeInput, "layout-native-padded", native);
            VerifyValue(Path.Combine(nativeInput, "layout-native-padded-preserved.bin"), native);
            Console.WriteLine(".NET verified C++ padded Native and single-model Tagged layouts.");
            return 0;
        }

        var packed = new LayoutPackedPacket
        {
            Scalar = new LayoutPacked { Code = 0x12, Amount = 0x0102030405060708, Enabled = true, End = -1027 },
            Nested =
            [
                new LayoutNested { Prefix = -17, Data = new LayoutPacked { Code = 0x34, Amount = long.MinValue, End = 127 } },
                new LayoutNested { Prefix = 77, Data = new LayoutPacked { Code = 0x56, Amount = long.MaxValue, Enabled = true, End = -128 } },
            ],
            Native = [new LayoutNative { Low = 0x89abcdef, High = 0x01234567 }],
        };
        var tagged = new LayoutTaggedPacket
        {
            Scalar = new LayoutTagged { Code = 21, Amount = 0x2122232425262728, Flag = 0x31 },
            Values =
            [
                new LayoutTagged { Code = -2, Amount = -3, Flag = 0x47 },
                new LayoutTagged { Code = 0x174, Amount = 0x1112131415161718, Flag = 0x59 },
            ],
            Items = [new LayoutTagged { Code = 61, Amount = long.MinValue, Flag = 0x7F }],
            ByKey = new() { [9] = new LayoutTagged { Code = -19, Amount = long.MaxValue, Flag = 0xF1 } },
        };

        if (args is ["write", var output])
        {
            Directory.CreateDirectory(output);
            Write(output, "layout-packed", packed);
            Write(output, "layout-tagged", tagged);
            return 0;
        }
        if (args is ["read", var input])
        {
            Verify(input, "layout-packed", packed);
            Verify(input, "layout-tagged", tagged);
            VerifyValue(Path.Combine(input, "layout-tagged-alternate.bin"), tagged);
            Console.WriteLine(".NET verified C++ Packed, Tagged, and dense Native layouts.");
            return 0;
        }
        Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.Layout <read|write|read-native|write-native> <directory>");
        return 2;
    }

    /// <summary>Prepares native values whose physical padding is distinguishable from their logical fields.</summary>
    /// <returns>The padded Native and single-model Tagged values.</returns>
    private static LayoutNativePaddedPacket CreateNative()
    {
        var packet = new LayoutNativePaddedPacket
        {
            Items =
            [
                new LayoutNativePadded { Amount = 0x0102030405060708, Code = -31, Flag = 0x41 },
                new LayoutNativePadded { Amount = long.MinValue, Code = 19, Flag = 0x59 },
            ],
            Scalar = new LayoutNativePadded { Amount = long.MaxValue, Code = -76, Flag = 0x71 },
            SingleModelTagged = [new LayoutTaggedPadded { Code = 103, Flag = 0x7F }],
        };
        Span<byte> items = MemoryMarshal.AsBytes(packet.Items.AsSpan());
        Span<byte> scalar = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref packet.Scalar, 1));
        Span<byte> tagged = MemoryMarshal.AsBytes(packet.SingleModelTagged.AsSpan());
        if (items.Length != 32 || scalar.Length != 16 || tagged.Length != 8)
            throw new InvalidDataException("The managed runtime has an unexpected native fixture layout.");
        items.Slice(13, 3).Fill(0xA1);
        items.Slice(29, 3).Fill(0xB2);
        scalar.Slice(13, 3).Fill(0xC3);
        tagged.Slice(5, 3).Fill(0xD4);
        return packet;
    }

    /// <summary>Writes the managed Binary and JSON fixtures used by the C++ runner.</summary>
    /// <typeparam name="T">The generated fixture type.</typeparam>
    /// <param name="directory">The destination directory.</param>
    /// <param name="name">The shared fixture file name.</param>
    /// <param name="value">The modeled value.</param>
    private static void Write<T>(string directory, string name, T value)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(value));
        File.WriteAllBytes(path + ".json", Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(value)));
    }

    /// <summary>Reads the C++ Binary and JSON outputs through the generated managed codecs.</summary>
    /// <typeparam name="T">The generated fixture type.</typeparam>
    /// <param name="directory">The output directory of the C++ runner.</param>
    /// <param name="name">The shared fixture file name.</param>
    /// <param name="expected">The original modeled value.</param>
    private static void Verify<T>(string directory, string name, T expected)
    {
        string path = Path.Combine(directory, name);
        VerifyValue(path + ".bin", expected);
        VerifyJson(path + ".json", LuminPackSerializer.DeserializeJson<T>(File.ReadAllBytes(path + ".json")), expected);
    }

    /// <summary>Verifies that the managed decoder accepts a C++ Binary fixture.</summary>
    /// <typeparam name="T">The generated fixture type.</typeparam>
    /// <param name="path">The Binary fixture path.</param>
    /// <param name="expected">The original modeled value.</param>
    private static void VerifyValue<T>(string path, T expected)
    {
        VerifyJson(path, LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path)), expected);
    }

    /// <summary>Compares decoded fields, ignoring native padding and the selected storage model.</summary>
    /// <typeparam name="T">The generated fixture type.</typeparam>
    /// <param name="path">The fixture name used in the error message.</param>
    /// <param name="actual">The decoded value.</param>
    /// <param name="expected">The original modeled value.</param>
    private static void VerifyJson<T>(string path, T actual, T expected)
    {
        JsonNode? actualFields = JsonNode.Parse(LuminPackSerializer.SerializeJson(actual));
        JsonNode? expectedFields = JsonNode.Parse(LuminPackSerializer.SerializeJson(expected));
        if (!JsonNode.DeepEquals(actualFields, expectedFields))
            throw new InvalidDataException("The decoded layout value differs from the fixture " + path + ".");
    }
}

/// <summary>Exercises mixed Packed fields and a contiguous, single-model Native array.</summary>
[LuminPackable]
public partial class LayoutPackedPacket
{
    public LayoutPacked Scalar { get; set; }
    public List<LayoutNested> Nested { get; set; } = [];
    public LayoutNative[] Native { get; set; } = [];
}

/// <summary>Exercises both Tagged collection paths and standalone values inside a dictionary.</summary>
[LuminPackable]
public partial class LayoutTaggedPacket
{
    public LayoutTagged Scalar { get; set; }
    public LayoutTagged[] Values { get; set; } = [];
    public List<LayoutTagged> Items { get; set; } = [];
    public Dictionary<int, LayoutTagged> ByKey { get; set; } = new();
}

/// <summary>Has alignment-sensitive native offsets and a fixed Packed form.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct LayoutPacked
{
    public byte Code;
    public long Amount;
    public bool Enabled;
    public short End;
}

/// <summary>Inherits the Packed boundary from a nested physical value field.</summary>
[LuminPackable]
public partial struct LayoutNested
{
    public int Prefix;
    public LayoutPacked Data;
}

/// <summary>Has an invariant native representation with two adjacent 32-bit values.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public partial struct LayoutNative
{
    public uint Low;
    public uint High;
}

/// <summary>Has different array strides in the 64-bit and 32-bit Tagged models.</summary>
[LuminPackable]
public partial struct LayoutTagged
{
    public int Code;
    public long Amount;
    public byte Flag;
}

/// <summary>Exercises tag-free raw layouts that contain compiler padding.</summary>
[LuminPackable]
public partial class LayoutNativePaddedPacket
{
    public LayoutNativePadded[] Items = [];
    public LayoutNativePadded Scalar;
    public LayoutTaggedPadded[] SingleModelTagged = [];
}

/// <summary>Has an explicit Native boundary over three unequal-width fields.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public partial struct LayoutNativePadded
{
    public long Amount;
    public int Code;
    public byte Flag;
}

/// <summary>Has a single native Tagged model with trailing padding.</summary>
[LuminPackable]
public partial struct LayoutTaggedPadded
{
    public int Code;
    public byte Flag;
}
