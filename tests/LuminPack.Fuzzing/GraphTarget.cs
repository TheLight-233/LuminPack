using System.Buffers.Binary;
using LuminPack;
using LuminPack.Fuzzing;

namespace Fuzzing;

/// <summary>Defines a parser target and its seeds independently of the record model it reads.</summary>
internal interface IFuzzTarget
{
    /// <summary>Gets the stable target name used by corpora and the native bridge.</summary>
    string Name { get; }

    /// <summary>Runs one input, accepting only known malformed-input exceptions from the first read.</summary>
    /// <param name="input">The original encoded bytes.</param>
    /// <returns>Whether the initial read succeeded and all invariants passed.</returns>
    bool Run(ReadOnlySpan<byte> input);

    /// <summary>Encodes the positive seeds and verifies each through the complete target.</summary>
    /// <returns>The named seed bytes.</returns>
    IEnumerable<(string Name, byte[] Bytes)> Seeds();

    /// <summary>Provides malformed seeds that the target must reject.</summary>
    /// <returns>The named malformed bytes.</returns>
    IEnumerable<(string Name, byte[] Bytes)> Malformed();
}

/// <summary>Lists every fuzz target.</summary>
internal static class Targets
{
    internal static readonly IFuzzTarget[] All = [.. FuzzTarget.All, new GraphTarget()];
}

/// <summary>Reads binary graphs of version-tolerant, reference-tracked and Union values.</summary>
/// <remarks>Graphs can contain cycles, so values are compared through their canonical encoding.</remarks>
internal sealed class GraphTarget : IFuzzTarget
{
    /// <inheritdoc/>
    public string Name => "binary-graph";

    /// <inheritdoc/>
    public bool Run(ReadOnlySpan<byte> input)
    {
        if (input.Length > FuzzTarget.MaxInputBytes) return false;
        FuzzGraph? fresh = null;
        int consumed;
        try
        {
            consumed = LuminPackSerializer.Deserialize(input, ref fresh);
        }
        catch (Exception exception) when (FuzzTarget.IsRejection(exception, json: false))
        {
            return false;
        }

        FuzzTarget.Require(consumed >= 0 && consumed <= input.Length, "The reader reported an invalid consumed length.");
        byte[] canonical = LuminPackSerializer.Serialize(fresh!);
        FuzzGraph? reused = Reusable();
        var original = reused;
        byte[] beforeReuse = LuminPackSerializer.Serialize(reused!);
        FuzzTarget.Require(LuminPackSerializer.Deserialize(input, ref reused) == consumed, "Fresh and reuse reads consumed different lengths.");
        // A binary null root intentionally preserves an existing target.
        if (fresh is null)
            FuzzTarget.Require(ReferenceEquals(original, reused) && beforeReuse.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(reused!)),
                "A binary null root changed the existing target.");
        else
            FuzzTarget.Require(canonical.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(reused!)), "Fresh and reuse reads produced different values.");

        if (canonical.Length > FuzzTarget.MaxInputBytes) return true;
        FuzzGraph? roundTrip = null;
        FuzzTarget.Require(LuminPackSerializer.Deserialize(canonical, ref roundTrip) == canonical.Length, "A canonical record was not fully consumed.");
        FuzzTarget.Require(canonical.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(roundTrip!)), "Canonical reserialization changed the record.");
        return true;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, byte[] Bytes)> Seeds()
    {
        foreach (var (name, value) in Values())
        {
            byte[] bytes = LuminPackSerializer.Serialize(value!);
            FuzzGraph? copy = null;
            FuzzTarget.Require(LuminPackSerializer.Deserialize(bytes, ref copy) == bytes.Length &&
                bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(copy!)), $"Positive seed {name} did not preserve its graph in {Name}.");
            FuzzTarget.Require(Run(bytes), $"Positive seed {name} was rejected by {Name}.");
            yield return (name, bytes);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, byte[] Bytes)> Malformed()
    {
        yield return ("invalid", [250, 255, 255, 255, 127]);
        // A back reference to an id that was never registered.
        yield return ("unknown-reference", [4, 255, 250, 9, 255, 255]);
        // An undeclared Union tag.
        yield return ("unknown-tag", [4, 255, 255, 1, 0, 0, 0, 7, 255]);
        // Placement: root member count, versioned first member, then the remaining three members.
        yield return ("unknown-schema", [4, 251, 0x52, 0, 255, 0, 0, 0, 0, 255]);

        byte[] bytes = LuminPackSerializer.Serialize(new FuzzGraph { Versioned = new FuzzVersioned() });
        const int descriptors = 12;
        int descriptorEnd = descriptors + bytes[3] * 8;
        yield return ("truncated-descriptors", bytes[..(descriptorEnd - 1)]);

        var duplicate = (byte[])bytes.Clone();
        Array.Copy(duplicate, descriptors, duplicate, descriptors + 8, 4);
        StampSchema(duplicate, descriptorEnd);
        yield return ("duplicate-descriptor-key", duplicate);

        var width = (byte[])bytes.Clone();
        width[descriptors + 7] = 251;
        StampSchema(width, descriptorEnd);
        yield return ("invalid-descriptor-width", width);

        // The leaf was registered inside Items, so its full table must appear again at the outer Tail.
        byte[] scoped = LuminPackSerializer.Serialize(Scoped());
        byte[] tail = LuminPackSerializer.Serialize(new FuzzVersionLeaf { Value = 17 });
        FuzzTarget.Require(scoped.AsSpan().EndsWith(tail), "The outer leaf lacks a complete descriptor table.");
        yield return ("escaped-schema-scope", [.. scoped[..^tail.Length], 251, 0x52, 1, 17, 0, 0, 0]);
    }

    /// <summary>Updates the valid schema digest after changing the descriptor bytes.</summary>
    /// <param name="record">The outer record holding the mutated first member.</param>
    /// <param name="descriptorEnd">The position after the mutated descriptor table.</param>
    private static void StampSchema(byte[] record, int descriptorEnd)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong digest = unchecked((offset ^ record[3]) * prime);
        foreach (byte octet in record.AsSpan(12, descriptorEnd - 12))
            digest = unchecked((digest ^ octet) * prime);
        BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(4), digest);
    }

    /// <summary>Creates scoped full and short records followed by a full record outside their scope.</summary>
    /// <returns>The graph used by the positive seed and the forged ordinal test.</returns>
    private static FuzzGraph Scoped() => new()
    {
        Versioned = new FuzzVersioned { Items = [new FuzzVersionLeaf { Value = 11 }, new FuzzVersionLeaf { Value = 13 }] },
        Tail = new FuzzVersionLeaf { Value = 17 }
    };

    /// <summary>Builds representative graphs, including a cycle and a recursive Union case.</summary>
    /// <returns>The named positive seed graphs.</returns>
    private static IEnumerable<(string Name, FuzzGraph? Value)> Values()
    {
        yield return ("null", null);
        yield return ("empty", new FuzzGraph());
        yield return ("schema-scopes", Scoped());
        var first = new FuzzNode { Id = 1, Flag = true, Text = "first" };
        var second = new FuzzNode { Id = 2, Text = "second é", Next = first };
        first.Next = second;
        first.Other = first;
        yield return ("nested", new FuzzGraph
        {
            Versioned = new FuzzVersioned { Id = 7, Flag = true, Name = "versioned", Values = [1, -1, int.MaxValue], Next = new FuzzVersioned { Id = 8 } },
            Nodes = first,
            Shapes = [new FuzzCircle { Filled = true, Radius = 1.5 }, null, new FuzzSquare { Side = 3, Label = "outer", Inner = new FuzzCircle { Radius = -2 } }]
        });
        yield return ("deep", new FuzzGraph
        {
            Versioned = Chain(12),
            Shapes = [new FuzzSquare { Side = 1, Inner = new FuzzSquare { Side = 2, Inner = new FuzzSquare { Side = 3, Inner = new FuzzCircle() } } }]
        });
    }

    /// <summary>Builds a version-tolerant chain within the depth limit.</summary>
    /// <param name="length">The number of linked records.</param>
    /// <returns>The chain's first record.</returns>
    private static FuzzVersioned Chain(int length)
    {
        FuzzVersioned? current = null;
        for (int index = length; index > 0; index--) current = new FuzzVersioned { Id = index, Flag = index % 2 == 0, Next = current };
        return current!;
    }

    /// <summary>Creates a nonempty graph that a successful read must fully overwrite.</summary>
    /// <returns>The reusable target.</returns>
    private static FuzzGraph Reusable()
    {
        var node = new FuzzNode { Id = -1, Flag = true, Text = "reusable" };
        node.Next = node;
        return new FuzzGraph
        {
            Versioned = new FuzzVersioned { Id = -1, Name = "old", Values = [5] },
            Nodes = node,
            Shapes = [new FuzzSquare { Side = -1, Label = "old" }]
        };
    }
}
