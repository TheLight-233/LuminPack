using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppNativeIntegerFixtures;

/// <summary>Exchanges host-width integer documents with generated C++ codecs.</summary>
internal static class Program
{
    /// <summary>Writes managed fixtures or checks the native round-trip outputs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero after a successful exchange or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.NativeInteger <write|read> <directory>");
            return 2;
        }

        bool write = args[0] == "write";
        if (write)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "native-integer-width.txt"), IntPtr.Size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Exchange(directory, write, "native-integer", new NativeIntegerPacket
        {
            Signed = nint.MinValue,
            Unsigned = nuint.MaxValue,
            SignedArray = [nint.MaxValue, -29, 0],
            UnsignedList = [1, nuint.MaxValue, 0],
            Map = new() { [-37] = nuint.MaxValue },
            Optional = -71,
            Memory = new nuint[] { 101, nuint.MaxValue },
            Sequence = new ReadOnlySequence<nint>(new nint[] { nint.MinValue, 103 }),
        });
        Exchange(directory, write, "version-native-integer", new VersionNativeIntegerPacket
        {
            Signed = nint.MaxValue,
            Unsigned = nuint.MaxValue,
            Values = [nint.MinValue, -113, 0],
        });
        if (IntPtr.Size == 4)
        {
            NativeIntegerPacket? narrowed = null;
            LuminPackSerializer.DeserializeJson<NativeIntegerPacket?, LuminPackUtf8LengthOptions>(
                "{\"Signed\":4294967295,\"Unsigned\":4294967296}"u8, ref narrowed);
            if (narrowed?.Signed != -1 || narrowed.Unsigned != 0)
                throw new InvalidDataException("The managed native integer JSON conversion did not narrow to the current ABI.");
        }
        Console.WriteLine($".NET {IntPtr.Size * 8}-bit native integer exchange completed.");
        return 0;
    }

    /// <summary>Exchanges a fixture as Binary, UTF-8 JSON, and UTF-16 JSON.</summary>
    /// <typeparam name="T">The managed record type.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to produce the managed files.</param>
    /// <param name="name">The common fixture basename.</param>
    /// <param name="expected">The source managed object.</param>
    private static void Exchange<T>(string directory, bool write, string name, T expected)
    {
        string path = Path.Combine(directory, name);
        if (write)
        {
            File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(expected));
            var output = new ArrayBufferWriter<byte>();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf8.json", output.WrittenSpan.ToArray());
            output.Clear();
            LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(expected, output);
            File.WriteAllBytes(path + ".utf16.json", output.WrittenSpan.ToArray());
        }
        else
        {
            T fromUtf8 = default!;
            T fromUtf16 = default!;
            LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref fromUtf8);
            LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref fromUtf16);
            T fromBinary = LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path + ".bin"));
            string json = LuminPackSerializer.SerializeJson(expected);
            if (LuminPackSerializer.SerializeJson(fromBinary) != json || LuminPackSerializer.SerializeJson(fromUtf8) != json ||
                LuminPackSerializer.SerializeJson(fromUtf16) != json)
                throw new InvalidDataException("C++ changed the native integer fixture " + name + ".");
        }
    }
}

/// <summary>Ordinary native integer values and their common collection shapes.</summary>
[LuminPackable]
public sealed class NativeIntegerPacket
{
    public nint Signed;
    public System.UIntPtr Unsigned;
    public System.IntPtr[]? SignedArray;
    public List<nuint>? UnsignedList;
    public Dictionary<nint, nuint>? Map;
    public nint? Optional;
    public ReadOnlyMemory<nuint> Memory;
    public ReadOnlySequence<nint> Sequence;
}

/// <summary>Version framing for architecture-dependent element widths.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class VersionNativeIntegerPacket
{
    public System.IntPtr Signed;
    public nuint Unsigned;
    public nint[]? Values;
}
