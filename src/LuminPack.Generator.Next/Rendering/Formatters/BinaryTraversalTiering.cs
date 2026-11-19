using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Selects binary element loops that can let the JIT profile formatter calls.</summary>
internal static class BinaryTraversalTiering
{
    internal const string TieredWriteStrategy = "TieredBinaryWriteStrategy";

    // Start with small, flat records. Larger bodies can spend the entire warmup in first-tier code.
    private const int FlatRecordMemberLimit = 8;

    /// <summary>Checks whether one element's binary traversal can benefit from dynamic profile data.</summary>
    /// <param name="type">The canonical element spelling.</param>
    /// <param name="contract">The normalized formatter fact, when the family uses static forwarding.</param>
    /// <param name="payload">The direct payload render, when the family expands the record body.</param>
    /// <returns>Whether to leave tier selection to the JIT.</returns>
    internal static bool PreferPgo(string type, FormatterModel? contract, PayloadRenderModel? payload = null)
    {
        // Union collection loops rely on full optimization to inline the exact type dispatch and its case bodies.
        if (payload?.UnionRoot == true || contract is { Payload: UnionModel } or
            { Definition: FormatterDefinitions.GenericUnion or FormatterDefinitions.CompilerUnion })
            return false;
        if (type == "string")
            return true;
        if (contract is { RawCollection: true } or { NativeNullableElement: true } or { Layout: not null })
            return false;
        if (contract != null && (contract.Supports(TieredWriteStrategy) || PreferPgoForCompound(contract)))
            return true;
        if (payload != null)
        {
            return payload is { IsStruct: false, UnionRoot: false, CamelSnapshots: false, FixedUnionCase: false, TrackReferences: false, Recursive: false,
                Version: null, Rent: null, Rejection: null, SequenceBase: null, Bounded: null, HasCallbacks: false,
                ConstructorArguments: null } &&
                (payload.Members.Any(member => member.Kind is RenderMemberKind.Dictionary or RenderMemberKind.SortedList or
                    RenderMemberKind.NestedList or RenderMemberKind.ForwardedCollection or RenderMemberKind.Sequence) ||
                payload.Members.Length <= FlatRecordMemberLimit && payload.Members.All(member => !member.Volatile &&
                    (member.Kind is RenderMemberKind.Fixed or RenderMemberKind.Text || member.ReservableSpan ||
                    member.Kind is RenderMemberKind.Array or RenderMemberKind.List &&
                    member.Nested == null && !member.FormatterCall && member.Scalar != null && member.Scalar != "string")));
        }
        return contract is { OrdinaryObject: true, Payload: RecordModel { Derived: false, Recursive: false,
            FixedUnionCase: false, Versioned: false, Rent: null, Rejection: null, SequenceBase: null, Callbacks: null,
            Constructor: null, Members.Length: <= FlatRecordMemberLimit } record } && record.Policy is { Bound: null, TrackReferences: false } &&
            record.Members.All(member => !member.Compressed && member is
                (ScalarMember { Volatile: false } or TextMember or
                SpecialMember { NativeFrameworkScalar: true } or SpecialMember { ReservableSpan: true } or
                CollectionMember { Kind: CollectionKind.Array or CollectionKind.List, Nested: null, FormatterCall: false,
                    Scalar: not null, ElementText: false, ArrayRank: <= 1 }));
    }

    /// <summary>Identifies a non-native compound element that benefits from tiered binary traversal.</summary>
    /// <param name="element">The concrete element strategy of the collection.</param>
    /// <returns>Whether the traversal should tier up when it reaches this strategy.</returns>
    internal static bool PreferPgoForCompound(FormatterModel element)
    {
        if (element is { RawStorage: true } or { NativeNullableElement: true } or { Layout: not null })
            return false;
        return element.Definition == GenericIdentity.Nullable ||
            element is { Payload: RecordModel { Versioned: false, Callbacks: null, Rejection: null } record } &&
            record.Members.Any(member => !member.Compressed && member is
                (DictionaryMember or CollectionMember or PayloadMember { Nested: UnionModel } or
                 SpecialMember { Formatter: not null }) &&
                (member is not SpecialMember special || IsCompoundMember(special.Formatter!)));
    }

    /// <summary>Identifies forwarded Unions and collection contracts in positional records.</summary>
    /// <param name="formatter">The normalized member formatter.</param>
    /// <returns>Whether the forwarded writer traverses components or dispatches Union cases.</returns>
    private static bool IsCompoundMember(FormatterModel formatter) =>
        formatter.Payload is UnionModel || formatter.Definition is FormatterDefinitions.GenericUnion or FormatterDefinitions.Array ||
        formatter.Definition.StartsWith("System.Collections.", System.StringComparison.Ordinal);

    /// <summary>Checks a mixed native / string / ordinary-record dictionary entry.</summary>
    /// <param name="key">The canonical key spelling.</param>
    /// <param name="keyContract">The normalized key formatter fact when available.</param>
    /// <param name="value">The canonical value spelling.</param>
    /// <param name="valueContract">The normalized value formatter fact when available.</param>
    /// <param name="payload">The direct value payload render when available.</param>
    /// <returns>Whether at least one entry component can benefit from tiering and the other has known work.</returns>
    internal static bool PreferPgo(string key, FormatterModel? keyContract, string value, FormatterModel? valueContract,
        PayloadRenderModel? payload)
    {
        bool tierKey = PreferPgo(key, keyContract);
        bool tierValue = PreferPgo(value, valueContract, payload);
        return (tierKey || tierValue) && (tierKey || Native(key, keyContract)) && (tierValue || Native(value, valueContract));
    }

    /// <summary>Identifies a raw component that does not need its own tiered writer.</summary>
    /// <param name="type">The component spelling.</param>
    /// <param name="contract">The analyzed formatter contract, if available.</param>
    /// <returns>Whether the component uses native storage.</returns>
    private static bool Native(string type, FormatterModel? contract) => contract == null
        ? GenericIdentity.IsRaw(type)
        : contract is { Raw: true, RawStorage: true, Layout: null };
}
