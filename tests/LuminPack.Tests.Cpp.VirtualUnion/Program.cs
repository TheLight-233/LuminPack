using System.Buffers;
using System.Reflection;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPackCppVirtualUnionFixtures;

/// <summary>Exchanges large virtual Union records in Binary and both JSON encodings.</summary>
internal static class Program
{
    private const ushort ExternalTag = 612;
    private static bool jsonUsesUtf8;

    /// <summary>Writes the managed fixtures or checks the C++ outputs.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero for success and two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.VirtualUnion <write|read> <directory>");
            return 2;
        }

        LuminPackSerializer.Register<LargeGraph, Registered>(ExternalTag, WriteBinary, ReadBinary, WriteJson, ReadJson);
        RequireCache(typeof(WideValue<int>), registered: true);
        RequireCache(typeof(WideValue<string>), registered: true);
        RequireCache(typeof(WideValue<>).MakeGenericType(typeof(double)), registered: false);
        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);

        var leaf = new DerivedCase { Number = 141 };
        var first = new WideValue<int> { Value = -57 };
        var second = new WideValue<string> { Value = "shared café Ω" };
        var document = new LargeDocument { Items = Cases(), Default = second, Alternate = second };
        var tracked = new Tracked { Count = 89 };
        tracked.Next = tracked;
        var graph = new GraphDocument
        {
            Anchor = tracked,
            ThroughUnion = tracked,
            Items = [tracked, leaf, first, null, new Registered { Value = 113 }, second, tracked]
        };

        Exchange<ILarge>(directory, write, "generic-int", first);
        Exchange<ILarge>(directory, write, "generic-string", second);
        Exchange<ILarge>(directory, write, "inherited-case", leaf);
        Exchange<ILarge?>(directory, write, "null-case", null);
        Exchange(directory, write, "all-cases", document);
        Exchange<LargeGraph>(directory, write, "direct-graph", tracked, CheckTrackedRoot);
        Exchange(directory, write, "mixed-graph", graph, CheckGraph);
        Console.WriteLine(write ? ".NET wrote the large virtual Union fixtures." : ".NET verified the C++ virtual Union fixtures.");
        return 0;
    }

    /// <summary>Verifies the managed virtual generic hash cache for both closed cases and an unknown construction.</summary>
    /// <param name="type">The closed managed generic case.</param>
    /// <param name="registered">Whether it has a declared case hash.</param>
    private static void RequireCache(Type type, bool registered)
    {
        var caches = type.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("LuminPackUnionDispatchSlot_", StringComparison.Ordinal)).ToArray();
        if (caches.Length != 2 || caches.Any(field => ((int)field.GetValue(null)! != -1) != registered))
            throw new InvalidDataException("The fixture did not use the managed generic virtual hash cache.");
    }

    /// <summary>Constructs one of every ordinary declared case in wire order.</summary>
    /// <returns>The ordinary and closed generic Union values.</returns>
    private static List<ILarge?> Cases() =>
        [
            new Case01 { Number = 7 },
            new Case02 { Number = 14 },
            new Case03 { Number = 21 },
            new Case04 { Number = 28 },
            new Case05 { Number = 35 },
            new Case06 { Number = 42 },
            new Case07 { Number = 49 },
            new Case08 { Number = 56 },
            new Case09 { Number = 63 },
            new Case10 { Number = 70 },
            new Case11 { Number = 77 },
            new Case12 { Number = 84 },
            new Case13 { Number = 91 },
            new Case14 { Number = 98 },
            new Case15 { Number = 105 },
            new Case16 { Number = 112 },
            new Case17 { Number = 119 },
            new Case18 { Number = 126 },
            new Case19 { Number = 133 },
            new Case20 { Number = 140 },
            new Case21 { Number = 147 },
            new Case22 { Number = 154 },
            new Case23 { Number = 161 },
            new Case24 { Number = 168 },
            new Case25 { Number = 175 },
            new Case26 { Number = 182 },
            new Case27 { Number = 189 },
            new Case28 { Number = 196 },
            new Case29 { Number = 203 },
            new Case30 { Number = 210 },
            new Case31 { Number = 217 },
            new Case32 { Number = 224 },
            new WideValue<int> { Value = 337 },
            new WideValue<string> { Value = "Latin-1 café" }
        ];

    /// <summary>Exchanges a case or containing model in binary and both JSON encodings.</summary>
    /// <typeparam name="T">The complete managed payload type.</typeparam>
    /// <param name="directory">The destination directory.</param>
    /// <param name="write">Whether to produce the managed documents.</param>
    /// <param name="name">The common fixture basename.</param>
    /// <param name="expected">The original managed payload.</param>
    /// <param name="verify">The optional graph identity assertions.</param>
    private static void Exchange<T>(string directory, bool write, string name, T expected, Action<T>? verify = null)
    {
        string path = Path.Combine(directory, name);
        string canonical = LuminPackSerializer.SerializeJson(expected);
        if (write)
        {
            File.WriteAllBytes(path + ".bin", LuminPackSerializer.Serialize(expected));
            var buffer = new ArrayBufferWriter<byte>();
            jsonUsesUtf8 = true;
            LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(expected, buffer);
            File.WriteAllBytes(path + ".utf8.json", buffer.WrittenSpan.ToArray());
            buffer.Clear();
            jsonUsesUtf8 = false;
            LuminPackSerializer.SerializeJson<T, LuminPackUtf16LengthOptions>(expected, buffer);
            File.WriteAllBytes(path + ".utf16.json", buffer.WrittenSpan.ToArray());
            return;
        }

        Check(LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(path + ".bin")));
        T result = default!;
        jsonUsesUtf8 = true;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref result);
        jsonUsesUtf8 = false;
        Check(result);
        result = default!;
        LuminPackSerializer.DeserializeJson<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref result);
        Check(result);

        void Check(T actual)
        {
            verify?.Invoke(actual);
            if (LuminPackSerializer.SerializeJson(actual) != canonical)
                throw new InvalidDataException("The C++ virtual Union changed " + name + ".");
        }
    }

    /// <summary>Checks that a tracked virtual case resolves through its own back reference.</summary>
    /// <param name="value">The decoded Union case.</param>
    private static void CheckTrackedRoot(LargeGraph value)
    {
        if (value is not Tracked { Count: 89 } tracked || !ReferenceEquals(tracked, tracked.Next))
            throw new InvalidDataException("The virtual tracked case lost its cycle.");
    }

    /// <summary>Checks identity across the graph's direct member and multiple Union edges.</summary>
    /// <param name="value">The decoded containing record.</param>
    private static void CheckGraph(GraphDocument value)
    {
        if (value is not { Anchor: not null, Items.Count: 7 } || !ReferenceEquals(value.Anchor, value.ThroughUnion) ||
            !ReferenceEquals(value.Anchor, value.Items[0]) || !ReferenceEquals(value.Anchor, value.Items[6]) ||
            value.Items[1] is not Case32 { Number: 141 } || value.Items[4] is not Registered { Value: 113 })
            throw new InvalidDataException("The virtual Union lost graph identity or the selected registered case.");
        CheckTrackedRoot(value.Anchor);
    }

    /// <summary>Writes an app-owned numeric tag and raw Binary payload.</summary>
    /// <param name="writer">The active Binary writer.</param>
    /// <param name="value">The registered derived value.</param>
    private static void WriteBinary(ref LuminPackWriter writer, ref LargeGraph value)
    {
        writer.WriteWideUnionHeader(ExternalTag);
        writer.Write(((Registered)value).Value ^ 23);
    }

    /// <summary>Reads a registered Binary payload into the application type.</summary>
    /// <param name="reader">The Binary reader after the tag.</param>
    /// <param name="value">Receives the registered value.</param>
    private static void ReadBinary(ref LuminPackReader reader, ref LargeGraph value)
        => value = new Registered { Value = reader.Read<int>() ^ 23 };

    /// <summary>Writes the registered JSON envelope in the fixture's document encoding.</summary>
    /// <param name="writer">The active JSON writer.</param>
    /// <param name="value">The registered derived value.</param>
    private static void WriteJson(ref LuminPackJsonWriter writer, ref LargeGraph value)
    {
        int payload = ((Registered)value).Value ^ 73;
        if (jsonUsesUtf8)
            WriteJson<LuminPackUtf8LengthOptions>(ref writer, payload);
        else
            WriteJson<LuminPackUtf16LengthOptions>(ref writer, payload);
    }

    /// <summary>Writes the numeric tag and app-owned scalar payload.</summary>
    /// <typeparam name="TProfile">The document encoding.</typeparam>
    /// <param name="writer">The active JSON writer.</param>
    /// <param name="payload">The transformed case value.</param>
    private static void WriteJson<TProfile>(ref LuminPackJsonWriter writer, int payload)
        where TProfile : struct, ILuminPackOptions
    {
        writer.WriteObjectStart<TProfile>();
        writer.WritePropertyName<TProfile>("$type");
        writer.Write<TProfile>((int)ExternalTag);
        writer.WritePropertyName<TProfile>("$value");
        writer.Write<TProfile>(payload);
        writer.WriteObjectEnd<TProfile>();
    }

    /// <summary>Decodes the app-owned scalar in the fixture's document encoding.</summary>
    /// <param name="reader">The JSON reader positioned at the payload.</param>
    /// <param name="value">Receives the registered value.</param>
    private static void ReadJson(ref LuminPackJsonReader reader, ref LargeGraph value)
        => value = new Registered { Value = (jsonUsesUtf8 ? reader.Read<int, LuminPackUtf8LengthOptions>() :
            reader.Read<int, LuminPackUtf16LengthOptions>()) ^ 73 };
}

/// <summary>An interface with more cases than the managed exact-dispatch tier supports.</summary>
[LuminPackable]
[LuminPackUnion(1, typeof(Case01))]
[LuminPackUnion(2, typeof(Case02))]
[LuminPackUnion(3, typeof(Case03))]
[LuminPackUnion(4, typeof(Case04))]
[LuminPackUnion(5, typeof(Case05))]
[LuminPackUnion(6, typeof(Case06))]
[LuminPackUnion(7, typeof(Case07))]
[LuminPackUnion(8, typeof(Case08))]
[LuminPackUnion(9, typeof(Case09))]
[LuminPackUnion(10, typeof(Case10))]
[LuminPackUnion(11, typeof(Case11))]
[LuminPackUnion(12, typeof(Case12))]
[LuminPackUnion(13, typeof(Case13))]
[LuminPackUnion(14, typeof(Case14))]
[LuminPackUnion(15, typeof(Case15))]
[LuminPackUnion(16, typeof(Case16))]
[LuminPackUnion(17, typeof(Case17))]
[LuminPackUnion(18, typeof(Case18))]
[LuminPackUnion(19, typeof(Case19))]
[LuminPackUnion(20, typeof(Case20))]
[LuminPackUnion(21, typeof(Case21))]
[LuminPackUnion(22, typeof(Case22))]
[LuminPackUnion(23, typeof(Case23))]
[LuminPackUnion(24, typeof(Case24))]
[LuminPackUnion(25, typeof(Case25))]
[LuminPackUnion(26, typeof(Case26))]
[LuminPackUnion(27, typeof(Case27))]
[LuminPackUnion(28, typeof(Case28))]
[LuminPackUnion(29, typeof(Case29))]
[LuminPackUnion(30, typeof(Case30))]
[LuminPackUnion(31, typeof(Case31))]
[LuminPackUnion(32, typeof(Case32))]
[LuminPackUnion(101, typeof(WideValue<int>)), LuminPackUnion(102, typeof(WideValue<string>))]
public partial interface ILarge
{
}

/// <summary>A class Union that combines the virtual tier, reference tracking, and wide registration.</summary>
[LuminPackable, LuminPackWideTag]
[LuminPackUnion(1, typeof(Case01))]
[LuminPackUnion(2, typeof(Case02))]
[LuminPackUnion(3, typeof(Case03))]
[LuminPackUnion(4, typeof(Case04))]
[LuminPackUnion(5, typeof(Case05))]
[LuminPackUnion(6, typeof(Case06))]
[LuminPackUnion(7, typeof(Case07))]
[LuminPackUnion(8, typeof(Case08))]
[LuminPackUnion(9, typeof(Case09))]
[LuminPackUnion(10, typeof(Case10))]
[LuminPackUnion(11, typeof(Case11))]
[LuminPackUnion(12, typeof(Case12))]
[LuminPackUnion(13, typeof(Case13))]
[LuminPackUnion(14, typeof(Case14))]
[LuminPackUnion(15, typeof(Case15))]
[LuminPackUnion(16, typeof(Case16))]
[LuminPackUnion(17, typeof(Case17))]
[LuminPackUnion(18, typeof(Case18))]
[LuminPackUnion(19, typeof(Case19))]
[LuminPackUnion(20, typeof(Case20))]
[LuminPackUnion(21, typeof(Case21))]
[LuminPackUnion(22, typeof(Case22))]
[LuminPackUnion(23, typeof(Case23))]
[LuminPackUnion(24, typeof(Case24))]
[LuminPackUnion(25, typeof(Case25))]
[LuminPackUnion(26, typeof(Case26))]
[LuminPackUnion(27, typeof(Case27))]
[LuminPackUnion(28, typeof(Case28))]
[LuminPackUnion(29, typeof(Case29))]
[LuminPackUnion(30, typeof(Case30))]
[LuminPackUnion(31, typeof(Case31))]
[LuminPackUnion(32, typeof(Case32))]
[LuminPackUnion(101, typeof(WideValue<int>)), LuminPackUnion(102, typeof(WideValue<string>)), LuminPackUnion(103, typeof(Tracked))]
public abstract partial class LargeGraph
{
}

[LuminPackable] public sealed partial class Case01 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case02 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case03 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case04 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case05 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case06 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case07 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case08 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case09 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case10 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case11 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case12 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case13 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case14 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case15 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case16 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case17 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case18 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case19 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case20 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case21 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case22 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case23 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case24 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case25 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case26 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case27 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case28 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case29 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case30 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public sealed partial class Case31 : LargeGraph, ILarge { public int Number; }
[LuminPackable] public partial class Case32 : LargeGraph, ILarge { public int Number; }

/// <summary>One generic declaration supplies two cached concrete dispatch hashes to each root.</summary>
[LuminPackable]
public sealed partial class WideValue<T> : LargeGraph, ILarge
{
    public T Value = default!;
}

/// <summary>A subclass inherits the ordinary generated virtual slot.</summary>
public sealed class DerivedCase : Case32
{
}

/// <summary>A versioned graph case serializes its own Union self-reference.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true, GeneratorType = GeneratorType.Version)]
public sealed partial class Tracked : LargeGraph
{
    public int Count;
    public LargeGraph? Next;
}

/// <summary>A case bound to the root's typed registry at runtime.</summary>
public sealed class Registered : LargeGraph
{
    public int Value;
}

/// <summary>Exercises every ordinary slot and the declared and alternate profile writers.</summary>
[LuminPackable]
public sealed class LargeDocument
{
    public List<ILarge?>? Items;
    public ILarge? Default;
    [LuminPackOption(StringRecording = LuminPackStringRecording.VarInt)] public ILarge? Alternate;
}

/// <summary>Shares a tracked object between direct and virtual Union members.</summary>
[LuminPackable]
public sealed class GraphDocument
{
    public Tracked? Anchor;
    public LargeGraph? ThroughUnion;
    public List<LargeGraph?>? Items;
}
