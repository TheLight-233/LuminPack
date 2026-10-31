using System.Security.Cryptography;
using LuminPack;
using LuminPack.Attribute;

[assembly: LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]

/// <summary>Executes real generated operations in JIT, trimmed and native executables without reflection.</summary>
internal static class Program
{
    /// <summary>Checks ordinary reuse, private storage, Union, versioning, cycles, portable batches and JSON.</summary>
    /// <param name="args">Optional retained wire fixture operation and directory.</param>
    /// <returns>Zero after all invariants hold; otherwise an exception terminates the executable.</returns>
    private static int Main(string[] args)
    {
        if (args.Length != 0)
        {
            if (args is not ["--wire", "write" or "verify", var directory])
                throw new ArgumentException("Usage: --wire <write|verify> <directory>");
            directory = Path.GetFullPath(directory);
            if (args[1] == "write")
            {
                Directory.CreateDirectory(directory);
                LuminPack.WireCompatibility.WireFixtures.Write(directory);
            }
            else LuminPack.WireCompatibility.WireFixtures.Verify(directory);
            Console.WriteLine("WIRE " + args[1].ToUpperInvariant() + " OK: " + directory);
            return 0;
        }
        CheckPlatform();
        CheckStringPaths();
        CheckLayoutPolicies();
        CheckBclJsonShapes();
        CheckExternalInput();
#if LUMINPACK_TYPE_CAPABILITY_CHECKS
        CheckTypeCapabilities();
#endif
        var source = new Message
        {
            Id = 42, Name = "\u00E9cafe\u4E2D\uD83D\uDE00", Values = new List<int> { 1, 3, 5 },
            Nodes = new[] { new Node { Value = 7 } }, Choice = new Selected { Value = 123 },
            Portable = new List<Portable> { new Portable { Tag = 9, Value = 123456789012345 } }
        };
        byte[] payload = LuminPackSerializer.Serialize(source);
        Message copy = LuminPackSerializer.Deserialize<Message>(payload);
        var nodes = copy.Nodes;
        var child = copy.Nodes[0];
        var values = copy.Values;
        var name = copy.Name;
        LuminPackSerializer.Deserialize(payload, ref copy);
        Check(copy.Id == 42 && copy.Name == source.Name && copy.Nodes[0].Value == 7 && copy.Choice is Selected { Value: 123 }, "Ordinary or Union read failed.");
        Check(ReferenceEquals(nodes, copy.Nodes) && ReferenceEquals(child, copy.Nodes[0]) && ReferenceEquals(values, copy.Values) && ReferenceEquals(name, copy.Name), "Existing storage was replaced.");
        Check(copy.Portable[0].Value == source.Portable[0].Value && LuminPackSerializer.Serialize(source.Portable).Length == 13, "Portable list used native padding.");
        string json = LuminPackSerializer.SerializeJson(source);
        Check(LuminPackSerializer.DeserializeJson<Message>(json).Name == source.Name, "JSON read failed.");
        using (var stream = new MemoryStream())
        {
            LuminPackSerializer.SerializeAsync(stream, source).GetAwaiter().GetResult();
            Check(stream.ToArray().AsSpan().SequenceEqual(payload), "Async write changed the payload.");
            stream.Position = 0;
            var asyncCopy = LuminPackSerializer.DeserializeAsync(stream, source).GetAwaiter().GetResult();
            Check(!ReferenceEquals(source, asyncCopy) && asyncCopy.Id == source.Id && stream.CanRead, "Async read or stream ownership failed.");
        }
        byte[] profileBytes = LuminPackSerializer.Serialize<LuminPack.Option.LuminPackUtf16LengthOptions>(source);
        var profileCopy = new Message();
        LuminPackSerializer.Deserialize<LuminPack.Option.LuminPackUtf16LengthOptions>(profileBytes.AsSpan(), ref profileCopy);
        Check(profileCopy.Name == source.Name, "Concrete compiled profile read failed.");

        byte[] versioned = LuminPackSerializer.Serialize(new VersionOne { Id = 13, Name = "player", Removed = 17 });
        var evolved = LuminPackSerializer.Deserialize<VersionTwo>(versioned);
        Check(evolved.Id == 13 && evolved.DisplayName == "player" && evolved.Added == 0, "Named version matching failed.");
        var tracked = new Tracked { Value = 19 };
        tracked.Next = tracked;
        byte[] trackedBytes = LuminPackSerializer.Serialize(tracked);
        var trackedCopy = LuminPackSerializer.Deserialize<Tracked>(trackedBytes);
        Check(ReferenceEquals(trackedCopy, trackedCopy.Next), "Tracked identity was lost.");
        byte[] privateBytes = LuminPackSerializer.Serialize(new PrivateModel(29));
        Check(LuminPackSerializer.Deserialize<PrivateModel>(privateBytes).GetValue() == 29, "Private Local/As storage failed.");

        byte[] portable = LuminPackSerializer.Serialize(new Portable { Tag = 9, Value = 123456789012345 });
        Check(portable.Length == 9 && BitConverter.ToInt64(portable, 1) == 123456789012345, "Portable wire width changed.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(payload);
        hash.AppendData(versioned);
        hash.AppendData(trackedBytes);
        hash.AppendData(privateBytes);
        hash.AppendData(portable);
        Console.WriteLine("payload sha256: " + Convert.ToHexString(hash.GetHashAndReset()));
        Console.WriteLine("Generated serialization smoke tests passed.");
        return 0;
    }

#if LUMINPACK_TYPE_CAPABILITY_CHECKS
    /// <summary>Checks name-based Type resolution and its explicit boundary without dynamic code.</summary>
    private static void CheckTypeCapabilities()
    {
        Type value = typeof(int);
        byte[] bytes = LuminPackSerializer.Serialize(value);
        string json = LuminPackSerializer.SerializeJson(value);
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            Check(LuminPackSerializer.Deserialize<Type>(bytes) == value, "JIT Type binary parsing changed.");
            Check(LuminPackSerializer.DeserializeJson<Type>(json) == value, "JIT Type JSON parsing changed.");
        }
        else
        {
            bool binaryRejected = false, jsonRejected = false;
            try { _ = LuminPackSerializer.Deserialize<Type>(bytes); } catch (PlatformNotSupportedException) { binaryRejected = true; }
            try { _ = LuminPackSerializer.DeserializeJson<Type>(json); } catch (PlatformNotSupportedException) { jsonRejected = true; }
            Check(binaryRejected && jsonRejected, "Dynamic Type reads must reject unsupported native execution.");
        }
        Type missing = null!;
        Check(LuminPackSerializer.Deserialize<Type>(LuminPackSerializer.Serialize(missing)) is null, "Null Type binary parsing changed.");
        Check(LuminPackSerializer.DeserializeJson<Type>("null") is null, "Null Type JSON parsing changed.");
        Console.WriteLine("Type capability checks passed.");
    }
#endif

    /// <summary>Executes wide packed shuffles, foreign tagged payloads and recursive generic size evaluation after publication.</summary>
    private static void CheckLayoutPolicies()
    {
        var packed = Enumerable.Range(0, 101).Select(value => new Portable { Tag = (byte)value, Value = value * 123456789L }).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(packed);
        Check(bytes.Length == 4 + packed.Length * 9, "Packed array acquired padding or a layout tag.");
        var restored = LuminPackSerializer.Deserialize<Portable[]>(bytes);
        Check(restored[^1].Value == packed[^1].Value, "Published packed shuffle lost its tail.");
        byte[] foreign = new byte[5 + 101 * 12];
        BitConverter.GetBytes(101).CopyTo(foreign, 0);
        foreign[4] = 2;
        for (int i = 0; i < 101; i++)
        {
            foreign[5 + i * 12] = (byte)i;
            BitConverter.GetBytes(i * 123456789L).CopyTo(foreign, 9 + i * 12);
        }
        var converted = LuminPackSerializer.Deserialize<AotTagged[]>(foreign);
        Check(converted[^1].Tag == 100 && converted[^1].Value == packed[^1].Value, "Published foreign-layout conversion lost data.");
        AotSizeNode? node = null;
        for (int i = 0; i < 128; i++) node = new AotSizeNode { Value = i, Next = node };
        Check(LuminPackSerializer.Deserialize<AotSizeNode>(LuminPackSerializer.Serialize<AotSizeNode>(node!))?.Value == 127, "Published recursive generic fallback failed.");
    }

    /// <summary>Verifies the requested process architecture and CPU vendor before testing serialization.</summary>
    private static void CheckPlatform()
    {
        string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string requested = Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_ARCH");
        if (!string.IsNullOrEmpty(requested)) Check(architecture == requested, "The executable is running with the wrong process architecture.");
        string vendor = "non-x86";
        if (System.Runtime.Intrinsics.X86.X86Base.IsSupported)
        {
            var registers = System.Runtime.Intrinsics.X86.X86Base.CpuId(0, 0);
            Span<byte> bytes = stackalloc byte[12];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, registers.Ebx);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4), registers.Edx);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8), registers.Ecx);
            vendor = System.Text.Encoding.ASCII.GetString(bytes);
        }
        string expectedVendor = Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_VENDOR");
        if (!string.IsNullOrEmpty(expectedVendor)) Check(vendor == expectedVendor, "The selected runner has the wrong CPU vendor.");
        Console.WriteLine("Process architecture: " + architecture + "; CPU vendor: " + vendor);
    }

    /// <summary>Checks long ASCII, Latin1, two-byte and three-byte runs in the actual published executable.</summary>
    private static void CheckStringPaths()
    {
        if (Environment.GetEnvironmentVariable("LUMINPACK_REQUIRE_AVX512") == "1")
            Check(System.Runtime.Intrinsics.X86.Avx512BW.IsSupported, "The selected runner does not expose AVX-512BW.");
        foreach (string text in new[] { new string('x', 129), string.Concat(Enumerable.Repeat("\u00E9cafe\u00FF", 24)), new string('\u07FF', 129), new string('\u4E2D', 129) })
        {
            byte[] expected = System.Text.Encoding.UTF8.GetBytes(text);
            byte[] actual = new byte[expected.Length];
            var status = LuminPack.Utility.StringSerializer.Serialize<LuminPack.Option.LuminPackUtf8LengthOptions>(text, actual, out int read, out int written);
            Check(status == System.Buffers.OperationStatus.Done && read == text.Length && written == expected.Length &&
                actual.AsSpan().SequenceEqual(expected), "Published UTF-8 vector encoding failed.");
            char[] decoded = new char[text.Length];
            status = LuminPack.Utility.StringSerializer.Deserialize<LuminPack.Option.LuminPackUtf8LengthOptions>(actual, decoded, out read, out written);
            Check(status == System.Buffers.OperationStatus.Done && read == actual.Length && written == text.Length &&
                text.AsSpan().SequenceEqual(decoded), "Published UTF-8 profile decoding failed.");
            byte[] utf16 = new byte[text.Length * 2];
            status = LuminPack.Utility.StringSerializer.Serialize<LuminPack.Option.LuminPackUtf16LengthOptions>(text, utf16, out read, out written);
            Check(status == System.Buffers.OperationStatus.Done && written == utf16.Length, "Published UTF-16 profile encoding failed.");
            status = LuminPack.Utility.StringSerializer.Deserialize<LuminPack.Option.LuminPackUtf16LengthOptions>(utf16, decoded, out read, out written);
            Check(status == System.Buffers.OperationStatus.Done && read == utf16.Length && written == text.Length &&
                text.AsSpan().SequenceEqual(decoded), "Published UTF-16 profile decoding failed.");
        }
        Console.WriteLine("String ISA: AVX512BW=" + System.Runtime.Intrinsics.X86.Avx512BW.IsSupported +
            ", AVX2=" + System.Runtime.Intrinsics.X86.Avx2.IsSupported + ", SSSE3=" + System.Runtime.Intrinsics.X86.Ssse3.IsSupported);
    }

    /// <summary>Checks complete BCL envelopes and native array construction in the published executable.</summary>
    private static void CheckBclJsonShapes()
    {
        var lazy = new Lazy<string?>(() => null);
        var restored = LuminPackSerializer.DeserializeJson<Lazy<string?>>(LuminPackSerializer.SerializeJson(lazy));
        Check(restored is not null && restored.Value is null, "Lazy JSON presence was lost.");
        var array = LuminPackSerializer.DeserializeJson<int[,]>("{\"Lengths\":[1,2],\"LowerBounds\":[0,0],\"Values\":[7,9]}");
        Check(array[0, 1] == 9, "Flat JSON elements were lost.");
        const string bounded = "{\"Lengths\":[1,2],\"LowerBounds\":[-2,4],\"Values\":[7,9]}";
#if LUMINPACK_NATIVE_AOT
            bool rejected = false;
            try { _ = LuminPackSerializer.DeserializeJson<int[,]>(bounded); }
            catch (PlatformNotSupportedException) { rejected = true; }
            Check(rejected, "NativeAOT must retain the runtime's rejection of nonzero array bounds.");
#else
        array = LuminPackSerializer.DeserializeJson<int[,]>(bounded);
        Check(array.GetLowerBound(0) == -2 && array[-2, 5] == 9, "Nonzero JSON bounds were lost.");
#endif
        var empty = new int[0, 3];
        var copy = LuminPackSerializer.DeserializeJson<int[,]>(LuminPackSerializer.SerializeJson(empty));
        Check(copy.GetLength(0) == 0 && copy.GetLength(1) == 3, "Empty JSON dimensions were lost.");
    }

    /// <summary>Exercises the option-selected external boundary without dynamic code or reflection.</summary>
    private static void CheckExternalInput()
    {
        var source = new AotExternal { Id = 17, Text = "bounded", Values = new[] { 1, 2, 3 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var target = new AotExternal { Id = 91 };
        var original = target;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Check(ReferenceEquals(original, target) && target.Id == 17, "External decoding lost ordinary reuse.");
        Check(LuminPackSerializer.DeserializeJson<AotExternal>(LuminPackSerializer.SerializeJson(source)).Text == source.Text, "External JSON failed.");
        bytes[24] ^= 1;
        bool rejected = false;
        try { LuminPackSerializer.Deserialize(bytes, ref target); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && target.Id == 17, "External schema validation changed its destination.");
        rejected = false;
        try { _ = LuminPackSerializer.Deserialize<AotExternal>(LuminPackSerializer.Serialize(new AotExternal { Values = new int[5] })); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "External collection quota was bypassed.");
    }

    /// <summary>Fails the executable if an observable generated operation changed.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The invariant's failure detail.</param>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

/// <summary>Checks the same external policy in JIT, package, trimming and NativeAOT executables.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 4)]
public partial class AotExternal
{
    public int Id;
    public string Text = "";
    public int[] Values = Array.Empty<int>();
}

/// <summary>Combines ordinary generated fields with an exact matching Union case and portable storage.</summary>
[LuminPackable]
public sealed class Message
{
    public int Id;
    public string Name = "";
    public List<int> Values = new();
    public Node[] Nodes = Array.Empty<Node>();
    public Choice? Choice;
    public List<Portable> Portable = new();
}

/// <summary>Provides a reusable reference leaf.</summary>
[LuminPackable]
public sealed class Node { public int Value; }

/// <summary>Provides the ordinary virtual Union root.</summary>
[LuminPackable, LuminPackUnion(1, typeof(Selected))]
public abstract partial class Choice { }

/// <summary>Provides the selected concrete virtual case.</summary>
[LuminPackable]
public sealed partial class Selected : Choice { public int Value; }

/// <summary>Defines the original named schema.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed class VersionOne { public int Id; public string Name = ""; public long Removed; }

/// <summary>Defines an independently reordered, renamed and extended schema.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed class VersionTwo { public int Added; [LuminPackOldName("Name")] public string DisplayName = ""; public int Id; }

/// <summary>Exercises reference tracking before child reads.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed class Tracked { [LuminPackOrder(0)] public int Value; [LuminPackOrder(1)] public Tracked? Next; }

/// <summary>Requires declaration-ordered portable storage under the assembly opt-in.</summary>
[LuminPackable]
public struct Portable { public byte Tag; public long Value; }

/// <summary>Overrides the assembly's packed policy with tagged native payloads.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public struct AotTagged { public byte Tag; public long Value; }

/// <summary>Exercises the first-use recursive evaluator in native code.</summary>
[LuminPackable]
public sealed class AotSizeNode { public int Value; public AotSizeNode? Next; }

/// <summary>Exercises a private included field through its original storage view.</summary>
[LuminPackable]
public sealed class PrivateModel
{
    [LuminPackInclude] private int _value;

    /// <summary>Creates the default deserialization target.</summary>
    [LuminPackConstructor]
    public PrivateModel() { }

    /// <summary>Sets the included private storage.</summary>
    /// <param name="value">The private scalar value.</param>
    public PrivateModel(int value) => _value = value;

    /// <summary>Observes the included private field.</summary>
    /// <returns>The stored value.</returns>
    public int GetValue() => _value;
}
