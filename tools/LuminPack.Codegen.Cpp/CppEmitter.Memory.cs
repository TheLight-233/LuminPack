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
        Binding sequence = definition == "System.Buffers.ReadOnlySequence"
            ? BindSegmentedElements(element, profile, label)
            : BindElementSequence(element, profile, label);

        if (definition is "System.ArraySegment" or "System.Collections.Immutable.ImmutableArray")
            return sequence;

        string binary = Apply("luminpack::memory_codec", new[] { sequence.Binary });
        return new Binding(binary + "::value_type", binary, Apply("luminpack::json_memory_codec", new[] { sequence.Json }));
    }

    /// <summary>Binds the element operations used by the CLR sequence owner across all segments.</summary>
    /// <param name="element">The normalized element formatter.</param>
    /// <param name="profile">The active binary string profile.</param>
    /// <param name="label">The sequence used in diagnostics.</param>
    /// <returns>The flattened count and element stream.</returns>
    private Binding BindSegmentedElements(JsonElement element, int profile, string label)
    {
        if (Flag(element, "rawCollection") && element.GetProperty("layout").ValueKind == JsonValueKind.Null)
            return BindElementSequence(element, profile, label);

        // Each segment contributes to one count. Modeled aggregates use their standalone element
        // representation, and a proven native Nullable<T> slot has no per-collection layout tag.
        Binding item = BindFormatter(element, profile, label);
        if (Text(element, "definition") == NullableDefinition && element.GetProperty("nullableEncoding").GetInt32() != 0)
        {
            if (!element.TryGetProperty("nativeNullableElement", out JsonElement native))
                throw Missing(label, "an updated manifest with the nullable sequence element layout");
            if (native.GetBoolean())
            {
                var storage = NullableStorage(element, profile, label);
                if (storage.Models.Length != 1)
                    throw Missing(label, "an untagged nullable sequence element without a unique proven CLR layout");
                string codec = Apply("luminpack::nullable_storage_codec", new[] { storage.Payload.Binary, storage.Models[0] });
                item = item with { Binary = codec };
            }
        }
        return Sequence(item with { SequenceBinary = null });
    }
}
