using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackCppBinaryProfileFixtures;

/// <summary>Exchanges one model under the six built-in Binary operation profiles.</summary>
internal static class Program
{
    private static readonly string[] Profiles =
        ["utf8-length", "utf8-token", "utf16-length", "utf16-token", "utf8-varint", "utf16-varint"];

    /// <summary>Writes managed documents or verifies the documents encoded by C++.</summary>
    /// <param name="args">The operation and the model document directory.</param>
    /// <returns>Zero on completion, or two for an unsupported invocation.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.BinaryProfile <write|read> <directory>");
            return 2;
        }

        var document = new ProfileDocument
        {
            Text = "héllo Ω \U0001F600",
            Items = ["single", "", "café", "\U0001F600"],
            FixedField = "member Ω",
            Declared = new DeclaredRecord { Text = "déclaration value", MemberOverride = "override Ω" },
            Version = new ProfileVersion { Text = "version Ω", Code = 73 }
        };
        if (args[0] == "write")
            Write(directory, document);
        else
            Verify(directory, document);
        return 0;
    }

    /// <summary>Writes the actual managed encodings, checking that each explicit profile is used.</summary>
    /// <param name="directory">The destination for the fixture files.</param>
    /// <param name="document">The fixture's common value.</param>
    private static void Write(string directory, ProfileDocument document)
    {
        Directory.CreateDirectory(directory);
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int profile = 0; profile < Profiles.Length; profile++)
        {
            byte[] bytes = Encode(document, profile);
            if (!distinct.Add(Convert.ToHexString(bytes)))
                throw new InvalidDataException("Two explicit Binary profiles produced the same fixture.");
            File.WriteAllBytes(Path.Combine(directory, Profiles[profile] + ".bin"), bytes);
        }

        byte[] declared = LuminPackSerializer.Serialize(document);
        if (!declared.AsSpan().SequenceEqual(Encode(document, 2)))
            throw new InvalidDataException("Default Binary no longer matches explicit UTF-16 Length.");
        File.WriteAllBytes(Path.Combine(directory, "default.bin"), declared);
        byte[] pinned = LuminPackSerializer.Serialize<DeclaredRecord, LuminPackUtf8LengthOptions>(document.Declared);
        if (!pinned.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(document.Declared)))
            throw new InvalidDataException("The operation overrode the record's declared profile.");
        File.WriteAllBytes(Path.Combine(directory, "pinned-root.bin"), pinned);

        var output = new ArrayBufferWriter<byte>();
        LuminPackSerializer.SerializeJson<ProfileDocument, LuminPackUtf8LengthOptions>(document, output);
        File.WriteAllBytes(Path.Combine(directory, "expected.utf8.json"), output.WrittenSpan.ToArray());
        output.Clear();
        LuminPackSerializer.SerializeJson<ProfileDocument, LuminPackUtf16LengthOptions>(document, output);
        File.WriteAllBytes(Path.Combine(directory, "expected.utf16.json"), output.WrittenSpan.ToArray());
    }

    /// <summary>Decodes all native output under the corresponding options and verifies the common value.</summary>
    /// <param name="directory">The directory containing the native documents.</param>
    /// <param name="document">The original managed fixture.</param>
    private static void Verify(string directory, ProfileDocument document)
    {
        string expected = LuminPackSerializer.SerializeJson(document);
        for (int profile = 0; profile < Profiles.Length; profile++)
        {
            string path = Path.Combine(directory, Profiles[profile]);
            Check(Decode(File.ReadAllBytes(path + ".bin"), profile), expected);
            ProfileDocument result = null!;
            LuminPackSerializer.DeserializeJson<ProfileDocument, LuminPackUtf8LengthOptions>(File.ReadAllBytes(path + ".utf8.json"), ref result);
            Check(result, expected);
            result = null!;
            LuminPackSerializer.DeserializeJson<ProfileDocument, LuminPackUtf16LengthOptions>(File.ReadAllBytes(path + ".utf16.json"), ref result);
            Check(result, expected);
        }

        Check(LuminPackSerializer.Deserialize<ProfileDocument>(File.ReadAllBytes(Path.Combine(directory, "default.bin"))), expected);
        var pinned = LuminPackSerializer.Deserialize<DeclaredRecord, LuminPackUtf8LengthOptions>(
            File.ReadAllBytes(Path.Combine(directory, "pinned-root.bin")));
        if (pinned?.Text != document.Declared.Text || pinned.MemberOverride != document.Declared.MemberOverride)
            throw new InvalidDataException("The declared root values changed.");
    }

    /// <summary>Checks one decoded instance through its declaration's JSON representation.</summary>
    /// <param name="decoded">The native model decoded by the managed runtime.</param>
    /// <param name="expected">The managed model's original JSON string.</param>
    private static void Check(ProfileDocument? decoded, string expected)
    {
        if (decoded == null || LuminPackSerializer.SerializeJson(decoded) != expected)
            throw new InvalidDataException("The operation profile changed the model values.");
    }

    /// <summary>Writes one of the statically bound managed profile entry points.</summary>
    /// <param name="document">The value written with each profile.</param>
    /// <param name="profile">The built-in operation profile index.</param>
    /// <returns>The managed binary bytes.</returns>
    private static byte[] Encode(ProfileDocument document, int profile) => profile switch
    {
        0 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf8LengthOptions>(document),
        1 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf8TokenOptions>(document),
        2 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf16LengthOptions>(document),
        3 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf16TokenOptions>(document),
        4 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf8VarIntOptions>(document),
        5 => LuminPackSerializer.Serialize<ProfileDocument, LuminPackUtf16VarIntOptions>(document),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };

    /// <summary>Reads one of the statically bound managed profile entry points.</summary>
    /// <param name="bytes">The native binary bytes.</param>
    /// <param name="profile">The built-in operation profile index.</param>
    /// <returns>The decoded fixture or null if the document encodes null.</returns>
    private static ProfileDocument? Decode(byte[] bytes, int profile) => profile switch
    {
        0 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf8LengthOptions>(bytes),
        1 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf8TokenOptions>(bytes),
        2 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf16LengthOptions>(bytes),
        3 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf16TokenOptions>(bytes),
        4 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf8VarIntOptions>(bytes),
        5 => LuminPackSerializer.Deserialize<ProfileDocument, LuminPackUtf16VarIntOptions>(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(profile))
    };
}

/// <summary>Mixes inherited operation profiles with independently declared string scopes.</summary>
[LuminPackable]
public sealed class ProfileDocument
{
    public string Text = "";
    public List<string> Items = [];
    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Length)]
    public string FixedField = "";
    public DeclaredRecord Declared = new();
    public ProfileVersion Version = new();
}

/// <summary>Uses its own Binary profile and a more specific member override.</summary>
[LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF16, StringRecording = LuminPackStringRecording.VarInt)]
public sealed class DeclaredRecord
{
    public string Text = "";
    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token)]
    public string MemberOverride = "";
}

/// <summary>Retains the Binary operation profile in a Version record nested in an ordinary object.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class ProfileVersion
{
    public string Text = "";
    public int Code;
}
