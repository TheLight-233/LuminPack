using System.Text.Json;

internal sealed partial class CppEmitter
{
    /// <summary>Recognizes CLR views whose wire retains the elements rather than the original backing storage.</summary>
    /// <param name="definition">The open formatter definition.</param>
    /// <returns>Whether the definition has a supported contiguous or flattened view.</returns>
    private static bool IsContiguousView(string definition) =>
        definition is "System.Memory" or "System.ReadOnlyMemory" or "System.ArraySegment" or
            "System.Buffers.ReadOnlySequence" or "System.Collections.Immutable.ImmutableArray";

    /// <summary>Uses the exported representation of an array or other contiguous element block.</summary>
    /// <param name="element">The closed element formatter.</param>
    /// <param name="profile">The active binary string profile.</param>
    /// <param name="label">The collection used in diagnostics.</param>
    /// <returns>The element block's Binary and JSON bindings.</returns>
    private Binding BindElementSequence(JsonElement element, int profile, string label)
    {
        if (element.GetProperty("nullableEncoding").GetInt32() != 0)
            return BindNullableBlock(element, profile, label);
        return Text(element, "definition") switch
        {
            PairDefinition => Sequence(BindPair(element, profile, label, collection: true)),
            TupleDefinition => Sequence(BindTuple(element, profile, label, collection: true)),
            _ => Sequence(BindFormatter(element, profile, label))
        };
    }

    /// <summary>Projects a managed memory view to owned elements with its original null policy.</summary>
    /// <param name="definition">The view's open CLR definition.</param>
    /// <param name="element">The closed element formatter.</param>
    /// <param name="profile">The active binary string profile.</param>
    /// <param name="label">The field or type used in diagnostics.</param>
    /// <returns>The owned C++ sequence and its codecs.</returns>
    private Binding BindContiguousView(string definition, JsonElement element, int profile, string label)
    {
        Binding sequence;
        if (definition == "System.Buffers.ReadOnlySequence")
        {
            Binding item = BindFormatter(element, profile, label);
            if (item.SequenceBinary != null || element.GetProperty("layout").ValueKind != JsonValueKind.Null ||
                element.GetProperty("nullableEncoding").GetInt32() != 0 ||
                Text(element, "definition") is PairDefinition or TupleDefinition)
                throw Missing(label, "a segmented view with a specialized element block");
            sequence = Sequence(item);
        }
        else
            sequence = BindElementSequence(element, profile, label);

        if (definition is "System.ArraySegment" or "System.Collections.Immutable.ImmutableArray")
            return sequence;

        string binary = Apply("luminpack::memory_codec", new[] { sequence.Binary });
        return new Binding(binary + "::value_type", binary, Apply("luminpack::json_memory_codec", new[] { sequence.Json }));
    }
}
