using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

/// <summary>Verifies archived and current Version documents using the managed runtime.</summary>
internal static class Program
{
    /// <summary>Writes the archive-based fixtures or verifies the native migrations.</summary>
    /// <param name="args">The operation, fixture directory, and archive path when writing.</param>
    /// <returns>Zero for success or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is ["write", var destination, var archive])
            Write(destination, archive);
        else if (args is ["read", var directory])
            Verify(directory);
        else
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.VersionCompatibility <write directory archive|read directory>");
            return 2;
        }
        return 0;
    }

    /// <summary>Uses the released revision-1 fixture and adds focused compatibility records.</summary>
    /// <param name="directory">The destination for managed documents.</param>
    /// <param name="archive">The existing revision-1 wire fixture from the repository.</param>
    private static void Write(string directory, string archive)
    {
        Directory.CreateDirectory(directory);
        byte[] source = File.ReadAllBytes(archive);
        CompatVersion original = LuminPackSerializer.Deserialize<CompatVersion>(source);
        Check(original);
        var evolved = LuminPackSerializer.Deserialize<EvolvedVersion>(source);
        Require(evolved is { Id: 7, DisplayName: "name", Child.Stamp: 99, Added: 0 });
        File.WriteAllBytes(Path.Combine(directory, "archive.bin"), source);
        File.WriteAllBytes(Path.Combine(directory, "migrated.bin"), LuminPackSerializer.Serialize(original));
        File.WriteAllBytes(Path.Combine(directory, "evolved.bin"), LuminPackSerializer.Serialize(evolved));

        byte[] scoped = Legacy(2, ("Child", "P@CompatVersionChild", LuminPackSerializer.Serialize(original.Child)));
        Require(LuminPackSerializer.Deserialize<CompatVersion>(scoped) is { Id: 0, Child.Stamp: 99 });
        File.WriteAllBytes(Path.Combine(directory, "mixed-scope.bin"), scoped);

        byte[] nullable = Legacy(0, ("Number", "\0", [17, 0, 0, 0]),
            ("Maybe", "\u0013@System.Nullable`1<#i4;>", [255]), ("Unknown", "ignored", new byte[137]));
        var decoded = LuminPackSerializer.Deserialize<LegacyNullable, LuminPackUtf8LengthOptions>(nullable);
        Require(decoded is { Number: 17, Maybe: null });
        File.WriteAllBytes(Path.Combine(directory, "nullable.bin"), nullable);
        File.WriteAllBytes(Path.Combine(directory, "nullable-modern.bin"),
            LuminPackSerializer.Serialize<LegacyNullable, LuminPackUtf8LengthOptions>(decoded));

        byte[] aliasDuplicate = Legacy(2, ("Name", "L", [255, 255, 255, 255]),
            ("DisplayName", "L", [255, 255, 255, 255]));
        RejectLegacy(aliasDuplicate);
        File.WriteAllBytes(Path.Combine(directory, "duplicate-alias.bin"), aliasDuplicate);
        byte[] changedContract = Legacy(2, ("Name", "@", [255, 255, 255, 255]));
        RejectLegacy(changedContract);
        File.WriteAllBytes(Path.Combine(directory, "changed-contract.bin"), changedContract);

        var scalar = new ClosedVersion<int> { Item = -17 };
        File.WriteAllBytes(Path.Combine(directory, "closed-scalar.bin"), LuminPackSerializer.Serialize(scalar));
        var collection = new ClosedVersion<List<int>> { Item = [2, 5, 7] };
        File.WriteAllBytes(Path.Combine(directory, "closed-list.bin"), LuminPackSerializer.Serialize(collection));
    }

    /// <summary>Decodes native files through their managed counterparts.</summary>
    /// <param name="directory">The native output directory.</param>
    private static void Verify(string directory)
    {
        Check(LuminPackSerializer.Deserialize<CompatVersion>(File.ReadAllBytes(Path.Combine(directory, "migrated.bin"))));
        Require(LuminPackSerializer.Deserialize<EvolvedVersion>(File.ReadAllBytes(Path.Combine(directory, "evolved.bin")))
            is { Id: 7, DisplayName: "name", Child.Stamp: 99, Added: 0 });
        Require(LuminPackSerializer.Deserialize<LegacyNullable, LuminPackUtf8LengthOptions>(
            File.ReadAllBytes(Path.Combine(directory, "nullable-modern.bin"))) is { Number: 17, Maybe: null });
        Require(LuminPackSerializer.Deserialize<ClosedVersion<int>>(File.ReadAllBytes(Path.Combine(directory, "closed-scalar.bin")))
            is { Item: -17 });
        Require(LuminPackSerializer.Deserialize<ClosedVersion<List<int>>>(File.ReadAllBytes(Path.Combine(directory, "closed-list.bin")))
            is { Item: [2, 5, 7] });
    }

    /// <summary>Checks that a managed read rejects incorrect legacy field metadata.</summary>
    /// <param name="input">The complete malformed revision-1 document.</param>
    private static void RejectLegacy(byte[] input)
    {
        try
        {
            _ = LuminPackSerializer.Deserialize<EvolvedVersion>(input);
        }
        catch (Exception error) when (error is FormatException or InvalidDataException)
        {
            return;
        }
        throw new InvalidDataException("The managed reader accepted malformed revision-1 metadata.");
    }

    /// <summary>Checks the exact values of the archived previous-release model.</summary>
    /// <param name="value">The archived record decoded by a compatible reader.</param>
    private static void Check(CompatVersion? value) =>
        Require(value is { Id: 7, Name: "name", Values: [1, 2, 3], Child: { Flag: true, Stamp: 99 } });

    /// <summary>Builds a small legacy table for the checked compatibility cases.</summary>
    /// <param name="profile">The profile byte in the legacy format.</param>
    /// <param name="fields">The complete names, contracts and byte payloads.</param>
    /// <returns>The bounded revision-1 fixture.</returns>
    private static byte[] Legacy(byte profile, params (string Name, string Contract, byte[] Payload)[] fields)
    {
        using var output = new MemoryStream();
        output.Write([251, 1, profile, checked((byte)fields.Length)]);
        foreach (var field in fields)
        {
            Metadata(output, field.Name);
            Metadata(output, field.Contract);
        }
        foreach (var field in fields)
        {
            if (field.Payload.Length > 127)
                output.WriteByte(unchecked((byte)-121));
            output.WriteByte(checked((byte)field.Payload.Length));
        }
        foreach (var field in fields)
            output.Write(field.Payload);
        return output.ToArray();
    }

    /// <summary>Writes one short metadata string to a fixture header.</summary>
    /// <param name="output">The fixture writer.</param>
    /// <param name="value">The exact name or contract.</param>
    private static void Metadata(Stream output, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        output.WriteByte(checked((byte)bytes.Length));
        output.Write(bytes);
    }

    /// <summary>Fails the fixture for an unexpected wire result.</summary>
    /// <param name="condition">Whether the observed data matches the contract.</param>
    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidDataException("The Version compatibility fixture did not match the managed result.");
    }
}

/// <summary>The archived row's original Version shape.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class CompatVersion
{
    public int Id;
    public string Name = "";
    public int[] Values = [];
    public CompatVersionChild? Child;
}

/// <summary>The archived child retains its original full CLR name.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class CompatVersionChild
{
    public bool Flag;
    public long Stamp;
}

/// <summary>Reads the old name and skips a removed field in the archived document.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class EvolvedVersion
{
    public int Id;
    [LuminPackOldName("Name")]
    public string DisplayName = "";
    public CompatVersionChild? Child;
    public int Added;
}

/// <summary>Reads an unpadded legacy nullable and a contract containing a zero byte.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, StringEncoding = LuminPackStringEncoding.UTF8)]
public sealed class LegacyNullable
{
    public int Number;
    public int? Maybe;
}

/// <summary>Separates an open descriptor from each statically closed Version construction.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class ClosedVersion<T>
{
    public T Item = default!;
}
