using LuminPack;
using LuminPack.Attribute;

#if LUMINPACK_CPP_STRICT_BINARY_INPUT
[assembly: LuminPackOption(RequireCompleteBinaryInput = true)]
namespace LuminPackCppStrictBinaryInput;
#else
namespace LuminPackCppPrefixBinaryInput;
#endif

/// <summary>Checks the managed document boundary and advances through the same data as a prefix.</summary>
internal static class Program
{
    internal const int MaximumInput = 128;

#if LUMINPACK_CPP_STRICT_BINARY_INPUT
    private static bool RequireComplete => true;
#else
    private static bool RequireComplete => false;
#endif

    private delegate int ReadPrefix<T>(byte[] bytes, ref T value);

    /// <summary>Writes the managed fixtures or checks values rewritten by C++.</summary>
    /// <param name="args">The operation and fixture directory.</param>
    /// <returns>Zero on success, or two for invalid arguments.</returns>
    private static int Main(string[] args)
    {
        if (args is not ["write" or "read", var directory])
        {
            Console.Error.WriteLine("Usage: LuminPack.Tests.Cpp.BinaryInput <write|read> <directory>");
            return 2;
        }

        bool write = args[0] == "write";
        if (write)
            Directory.CreateDirectory(directory);
        Exchange(directory, write, "message", new Message { Value = -901, Values = [1, 2, 3] },
            static bytes => LuminPackSerializer.Deserialize<Message>(bytes),
            static (byte[] bytes, ref Message value) => LuminPackSerializer.Deserialize(bytes, ref value), static value =>
        {
            if (value.Value != -901 || value.Values == null || !value.Values.SequenceEqual([1, 2, 3]))
                throw new InvalidOperationException("The Version message changed.");
        });

        var graph = new Node { Value = 103 };
        graph.Next = graph;
        Exchange(directory, write, "graph", graph,
            static bytes => LuminPackSerializer.Deserialize<Node>(bytes),
            static (byte[] bytes, ref Node value) => LuminPackSerializer.Deserialize(bytes, ref value), static value =>
        {
            if (value.Value != 103 || !ReferenceEquals(value, value.Next))
                throw new InvalidOperationException("The tracked self reference changed.");
        });
        Console.WriteLine(RequireComplete ? "Checked complete input." : "Checked prefix input.");
        return 0;
    }

    /// <summary>Checks exact reads, concatenated reads, and the input bound for a single model.</summary>
    /// <typeparam name="T">The root model.</typeparam>
    /// <param name="directory">The fixture directory.</param>
    /// <param name="write">Whether to emit or verify the fixture.</param>
    /// <param name="name">The common filename stem.</param>
    /// <param name="model">The original managed value.</param>
    /// <param name="read">The statically bound value-returning read.</param>
    /// <param name="readPrefix">The statically bound advancing read.</param>
    /// <param name="check">The assertions for the decoded value.</param>
    private static void Exchange<T>(string directory, bool write, string name, T model,
        Func<byte[], T> read, ReadPrefix<T> readPrefix, Action<T> check) where T : class
    {
        string path = Path.Combine(directory, name);
        byte[] exact = write ? LuminPackSerializer.Serialize(model) : File.ReadAllBytes(path + ".bin");
        byte[] concatenated = [.. exact, .. exact];
        if (write)
        {
            File.WriteAllBytes(path + ".bin", exact);
            File.WriteAllBytes(path + "-joined.bin", concatenated);
        }

        check(read(exact));
        T prefix = null!;
        int consumed = readPrefix(concatenated, ref prefix);
        if (consumed != exact.Length)
            throw new InvalidOperationException("The advancing managed overload changed the consumed width.");
        check(prefix);
        if (RequireComplete)
            RequireRejected(() => _ = read(concatenated));
        else
            check(read(concatenated));

        byte[] oversized = new byte[MaximumInput + 1];
        exact.CopyTo(oversized, 0);
        RequireRejected(() => _ = read(oversized));
    }

    /// <summary>Checks the managed input policy's rejection.</summary>
    /// <param name="read">The operation expected to fail.</param>
    private static void RequireRejected(Action read)
    {
        try
        {
            read();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("The managed input policy accepted invalid input.");
    }
}

/// <summary>A root with a scoped Version table.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, MaxInputBytes = Program.MaximumInput)]
public sealed partial class Message
{
    public int Value;
    public List<int>? Values;
}

/// <summary>A root that owns a reference graph during decode.</summary>
[LuminPackable, LuminPackOption(TrackReferences = true, MaxInputBytes = Program.MaximumInput)]
public sealed partial class Node
{
    public int Value;
    public Node? Next;
}
