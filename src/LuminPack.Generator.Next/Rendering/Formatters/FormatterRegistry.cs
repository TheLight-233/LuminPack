using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits a closed sequence owner for the requested operations.</summary>
/// <param name="set">The set used by this generator operation.</param>
/// <param name="fact">The fact used by this generator operation.</param>
/// <param name="operations">The operations used by this generator operation.</param>
/// <returns>The generated owner source.</returns>
internal delegate string RenderSequenceOwner(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations);

/// <summary>Emits a formatter's binary write at a record member boundary.</summary>
/// <param name="body">The body used by this generator operation.</param>
/// <param name="member">The member used by this generator operation.</param>
/// <param name="owner">The owner used by this generator operation.</param>
/// <param name="depth">The depth used by this generator operation.</param>
/// <param name="writer">The writer used by this generator operation.</param>
/// <param name="names">The names used by this generator operation.</param>
/// <param name="ends">The ends used by this generator operation.</param>
/// <param name="cursors">The cursors used by this generator operation.</param>
/// <param name="spans">The spans used by this generator operation.</param>
internal delegate void WriteFormatterMember(List<string> body, RenderMember member, string owner, int depth, CodeWriter writer,
    Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans);

/// <summary>Emits a formatter member write that only consumes the shared exit labels.</summary>
/// <param name="body">The body used by this generator operation.</param>
/// <param name="member">The member used by this generator operation.</param>
/// <param name="owner">The owner used by this generator operation.</param>
/// <param name="writer">The writer used by this generator operation.</param>
/// <param name="names">The names used by this generator operation.</param>
/// <param name="ends">The ends used by this generator operation.</param>
internal delegate void WriteSimpleFormatterMember(List<string> body, RenderMember member, string owner, CodeWriter writer,
    Dictionary<string, int> names, ref int ends);

/// <summary>Describes one open sequence definition and its supported element forms.</summary>
internal sealed class SequenceFormatter
{
    /// <summary>The analysis shape of this sequence.</summary>
    internal CollectionKind Kind { get; }

    /// <summary>The renderer for a raw scalar element.</summary>
    internal RenderSequenceOwner Scalar { get; }

    /// <summary>The renderer for a string element, when supported.</summary>
    internal RenderSequenceOwner? Text { get; }

    /// <summary>The renderer for an expandable packable element, when supported.</summary>
    internal RenderSequenceOwner? Payload { get; }

    /// <summary>The renderer for an element with its own concrete formatter, when supported.</summary>
    internal RenderSequenceOwner? Forwarded { get; }

    /// <summary>Whether the family's element block uses the contiguous storage contract.</summary>
    internal bool ContiguousElements { get; }

    /// <summary>The default renderer for an element with an independent static formatter.</summary>
    internal RenderSequenceOwner General { get; }

    /// <summary>The optional inline record write.</summary>
    internal WriteFormatterMember? WriteMember { get; }

    /// <summary>Whether the inline writer carries the profile into string or payload elements.</summary>
    internal bool ProfileElements { get; }

    /// <summary>Whether the inline writer has a loop even for scalar elements.</summary>
    internal bool AlwaysLoops { get; }

    /// <summary>Whether an expandable payload introduces an inline loop.</summary>
    internal bool PayloadLoops { get; }

    /// <summary>Whether this sequence uses the shared weak candidate cache when reading binary.</summary>
    internal bool UsesCandidateCache { get; }

    /// <summary>Registers a sequence's supported operations and record placement facts.</summary>
    /// <param name="kind">The analysis shape.</param>
    /// <param name="scalar">The raw scalar owner renderer.</param>
    /// <param name="text">The string-element owner renderer.</param>
    /// <param name="payload">The packable-element owner renderer.</param>
    /// <param name="forwarded">The static forwarding owner renderer.</param>
    /// <param name="writeMember">The inline record writer.</param>
    /// <param name="profileElements">Whether string and payload writes carry the profile.</param>
    /// <param name="alwaysLoops">Whether scalar writes already need a loop.</param>
    /// <param name="payloadLoops">Whether expandable payload writes need a loop.</param>
    /// <param name="usesCandidateCache">Whether binary reads need the shared candidate cache.</param>
    /// <param name="general">The general used by this operation.</param>
    /// <param name="contiguousElements">The contiguous elements used by this operation.</param>
    internal SequenceFormatter(CollectionKind kind, RenderSequenceOwner scalar, RenderSequenceOwner? text = null,
        RenderSequenceOwner? payload = null, RenderSequenceOwner? forwarded = null, WriteFormatterMember? writeMember = null,
        bool profileElements = false, bool alwaysLoops = false, bool payloadLoops = false, bool usesCandidateCache = false,
        RenderSequenceOwner? general = null, bool contiguousElements = false)
    {
        ContiguousElements = contiguousElements;
        General = general ?? forwarded ?? payload ?? text ?? scalar;
        Kind = kind;
        Scalar = scalar;
        Text = text;
        Payload = payload;
        Forwarded = forwarded;
        WriteMember = writeMember;
        ProfileElements = profileElements;
        AlwaysLoops = alwaysLoops;
        PayloadLoops = payloadLoops;
        UsesCandidateCache = usesCandidateCache;
    }
}

/// <summary>Maps open type definitions to their generator-side formatter implementations.</summary>
internal static class FormatterRegistry
{
    /// <summary>The standalone string owner identity.</summary>
    internal const string String = "string";

    /// <summary>The standalone culture-info owner identity.</summary>
    internal const string CultureInfo = "System.Globalization.CultureInfo";

    private static readonly Dictionary<string, Func<SharedFormatterSet, GeneratedOperations, string>> Standalone =
        new Dictionary<string, Func<SharedFormatterSet, GeneratedOperations, string>>(StringComparer.Ordinal)
        {
            [String] = StringOwnerFile.Render,
            [CultureInfo] = CultureInfoOwnerFile.Render,
            ["System.Uri"] = UriOwnerFile.Render,
            ["System.Version"] = VersionOwnerFile.Render,
            ["System.Numerics.BigInteger"] = BigIntegerOwnerFile.Render,
            ["System.Collections.BitArray"] = BitArrayOwnerFile.Render,
            ["System.Text.StringBuilder"] = StringBuilderOwnerFile.Render,
            ["System.Type"] = TypeOwnerFile.Render,
            ["System.TimeZoneInfo"] = TimeZoneInfoOwnerFile.Render,
            ["UnityEngine.AnimationCurve"] = (set, operations) => UnityObjectOwnerFile.Render(set, operations, "AnimationCurve", "global::UnityEngine.AnimationCurve"),
            ["UnityEngine.Gradient"] = (set, operations) => UnityObjectOwnerFile.Render(set, operations, "Gradient", "global::UnityEngine.Gradient"),
            ["UnityEngine.RectOffset"] = (set, operations) => UnityObjectOwnerFile.Render(set, operations, "RectOffset", "global::UnityEngine.RectOffset"),
        };

    internal static readonly SequenceFormatter Array = new SequenceFormatter(CollectionKind.Array, ArrayScalarOwnerFile.Render,
        text: ArrayTextOwnerFile.Render, payload: ArrayTextOwnerFile.Render, forwarded: ArrayForwardingOwnerFile.Render);

    private static readonly SequenceFormatter RectangularArray = new SequenceFormatter(CollectionKind.Array,
        RectangularArrayOwnerFile.Render, text: RectangularArrayOwnerFile.Render);

    private static readonly Dictionary<string, SequenceFormatter> Sequences = new Dictionary<string, SequenceFormatter>(StringComparer.Ordinal)
    {
        ["System.Collections.Generic.List"] = new SequenceFormatter(CollectionKind.List, ListScalarOwnerFile.Render,
            text: ListStringOwnerFile.Render, payload: ListPayloadOwnerFile.Render, forwarded: ListForwardingOwnerFile.Render,
            general: ListForwardingOwnerFile.RenderDefault),
        ["System.Collections.Generic.Stack"] = new SequenceFormatter(CollectionKind.Stack, StackOwnerFile.Render,
            text: StackStringOwnerFile.Render, payload: StackPayloadOwnerFile.Render,
            writeMember: Orderable(Adapt(StackMemberWriter.Write), OrderedPayloadMemberWriter.WriteStack, OrderedPayloadMemberWriter.WriteStackText),
            alwaysLoops: true,
            profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.LinkedList"] = new SequenceFormatter(CollectionKind.Linked, LinkedListOwnerFile.Render, text: LinkedListOwnerFile.Render, payload: LinkedListOwnerFile.Render,
            writeMember: PayloadAware(Adapt(LinkedListMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.HashSet"] = new SequenceFormatter(CollectionKind.HashSet, HashSetOwnerFile.Render, text: HashSetOwnerFile.Render, payload: HashSetOwnerFile.Render,
            writeMember: PayloadAware(Adapt(HashSetMemberWriter.Write)), profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.Queue"] = new SequenceFormatter(CollectionKind.Queue, QueueOwnerFile.Render, text: QueueOwnerFile.Render,
            payload: QueueOwnerFile.Render, writeMember: Orderable(Adapt(QueueMemberWriter.Write), OrderedPayloadMemberWriter.WriteQueue),
            profileElements: true, payloadLoops: true),
        ["System.Collections.ObjectModel.Collection"] = new SequenceFormatter(CollectionKind.Wrapper, CollectionOwnerFile.Render, text: CollectionOwnerFile.Render, payload: CollectionOwnerFile.Render,
            writeMember: PayloadAware(Adapt(WrapperMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.ObjectModel.ObservableCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ObservableCollectionOwnerFile.Render, text: ObservableCollectionOwnerFile.Render, payload: ObservableCollectionOwnerFile.Render,
            writeMember: PayloadAware(Adapt(WrapperMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.ObjectModel.ReadOnlyCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyCollectionOwnerFile.Render, text: ReadOnlyCollectionOwnerFile.Render, payload: ReadOnlyCollectionOwnerFile.Render,
            writeMember: PayloadAware(Adapt(WrapperMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.ObjectModel.ReadOnlyObservableCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyObservableCollectionOwnerFile.Render, text: ReadOnlyObservableCollectionOwnerFile.Render, payload: ReadOnlyObservableCollectionOwnerFile.Render,
            writeMember: PayloadAware(Adapt(WrapperMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Runtime.CompilerServices.ReadOnlyCollectionBuilder"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyCollectionBuilderOwnerFile.Render, text: ReadOnlyCollectionBuilderOwnerFile.Render, payload: ReadOnlyCollectionBuilderOwnerFile.Render,
            writeMember: PayloadAware(Adapt(WrapperMemberWriter.WriteBuilder)), profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.Frozen.FrozenSet"] = new SequenceFormatter(CollectionKind.Frozen, FrozenSetOwnerFile.Render, text: FrozenSetOwnerFile.Render, payload: FrozenSetOwnerFile.Render,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.WriteFrozen)), profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.IEnumerable"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderEnumerable, text: InterfaceOwnerFile.RenderEnumerable, payload: InterfaceOwnerFile.RenderEnumerable,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.ICollection"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderCollection, text: InterfaceOwnerFile.RenderCollection, payload: InterfaceOwnerFile.RenderCollection,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.IReadOnlyCollection"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderCollection, text: InterfaceOwnerFile.RenderCollection, payload: InterfaceOwnerFile.RenderCollection,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.IList"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderList, text: InterfaceOwnerFile.RenderList, payload: InterfaceOwnerFile.RenderList,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.IReadOnlyList"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderCollection, text: InterfaceOwnerFile.RenderCollection, payload: InterfaceOwnerFile.RenderCollection,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.ISet"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderSet, text: InterfaceOwnerFile.RenderSet, payload: InterfaceOwnerFile.RenderSet,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.IReadOnlySet"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderSet, text: InterfaceOwnerFile.RenderSet, payload: InterfaceOwnerFile.RenderSet,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.Write)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.IImmutableList"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderList, text: ImmutableInterfaceOwnerFile.RenderList, payload: ImmutableInterfaceOwnerFile.RenderList,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.WriteCounted)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.IImmutableSet"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderSet, text: ImmutableInterfaceOwnerFile.RenderSet, payload: ImmutableInterfaceOwnerFile.RenderSet,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.WriteCounted)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.IImmutableQueue"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderQueue, text: ImmutableInterfaceOwnerFile.RenderQueue, payload: ImmutableInterfaceOwnerFile.RenderQueue,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.WriteCounted)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.IImmutableStack"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderStack, text: ImmutableInterfaceOwnerFile.RenderStack, payload: ImmutableInterfaceOwnerFile.RenderStack,
            writeMember: PayloadAware(Adapt(InterfaceMemberWriter.WriteCounted)), alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Generic.SortedSet"] = new SequenceFormatter(CollectionKind.SortedSet, SortedSetOwnerFile.Render,
            text: SortedSetTextOwnerFile.Render, payload: SortedSetTextOwnerFile.Render,
            writeMember: PayloadAware(TextAware(Adapt(SortedSetMemberWriter.Write))), alwaysLoops: true,
            profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.Concurrent.BlockingCollection"] = new SequenceFormatter(CollectionKind.Blocking, BlockingCollectionOwnerFile.Render,
            text: BlockingCollectionOwnerFile.Render, forwarded: BlockingCollectionOwnerFile.Render),
        ["System.Collections.Concurrent.ConcurrentQueue"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentQueueOwnerFile.Render,
            text: ConcurrentQueueStringOwnerFile.Render, payload: ConcurrentQueuePayloadOwnerFile.Render,
            forwarded: ConcurrentQueueForwardingOwnerFile.Render, writeMember: CountedSequenceMemberWriter.Write,
            profileElements: true, alwaysLoops: true),
        ["System.Collections.Concurrent.ConcurrentStack"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentStackOwnerFile.Render,
            text: ConcurrentStackTextOwnerFile.Render, payload: ConcurrentStackTextOwnerFile.Render,
            writeMember: Orderable(ConcurrentStackTextMemberWriter.Write(Adapt(ConcurrentStackMemberWriter.Write)),
                OrderedPayloadMemberWriter.WriteConcurrentStack),
            alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.Concurrent.ConcurrentBag"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentBagOwnerFile.Render,
            text: ConcurrentBagTextOwnerFile.Render, payload: ConcurrentBagTextOwnerFile.Render, writeMember: PayloadAware(TextAware(Adapt(ConcurrentBagMemberWriter.Write))),
            alwaysLoops: true, profileElements: true, payloadLoops: true, contiguousElements: true),
        ["System.Collections.Immutable.ImmutableArray"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableArrayOwnerFile.Render,
            text: ImmutableArrayStringOwnerFile.Render, payload: ImmutableArrayPayloadOwnerFile.Render,
            writeMember: ImmutableArrayMemberWriter.Write, profileElements: true, payloadLoops: true,
            general: (set, fact, operations) => ContiguousOwnerFile.Render(set, set.FindFormatter(fact.DeclaredType)!, operations, ContiguousShape.ImmutableArray)),
        ["System.Collections.Immutable.ImmutableStack"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableStackOwnerFile.Render,
            text: ImmutableInterfaceOwnerFile.RenderStack, payload: ImmutableInterfaceOwnerFile.RenderStack, writeMember: CountedSequenceMemberWriter.Write, alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.ImmutableQueue"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableQueueOwnerFile.Render,
            text: ImmutableInterfaceOwnerFile.RenderQueue, payload: ImmutableInterfaceOwnerFile.RenderQueue, writeMember: PayloadAware(TextAware(Adapt(ImmutableQueueMemberWriter.Write))),
            alwaysLoops: true, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.ImmutableList"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableListOwnerFile.Render,
            text: ImmutableInterfaceOwnerFile.RenderList, payload: ImmutableInterfaceOwnerFile.RenderList, writeMember: PayloadAware(TextAware(Adapt(ImmutableListMemberWriter.Write))),
            alwaysLoops: false, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.ImmutableHashSet"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableHashSetOwnerFile.Render,
            text: ImmutableInterfaceOwnerFile.RenderSet, payload: ImmutableInterfaceOwnerFile.RenderSet, writeMember: PayloadAware(TextAware(Adapt(ImmutableTreeMemberWriter.Write))),
            profileElements: true, usesCandidateCache: true, payloadLoops: true),
        ["System.Collections.Immutable.ImmutableSortedSet"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableSortedSetOwnerFile.Render,
            text: ImmutableInterfaceOwnerFile.RenderSet, payload: ImmutableInterfaceOwnerFile.RenderSet, writeMember: PayloadAware(TextAware(Adapt(ImmutableTreeMemberWriter.Write))),
            profileElements: true, usesCandidateCache: true, payloadLoops: true),
    };

    private static readonly Dictionary<string, DictionaryFormatter> Dictionaries = new Dictionary<string, DictionaryFormatter>(StringComparer.Ordinal)
    {
        ["System.Collections.Generic.Dictionary"] = new DictionaryFormatter(DictionaryOwnerFile.Render, payload: PayloadDictionaryOwnerFile.Render, jsonReuseStore: "value[key] = itemValue;", nativeEntries: true, general: DictionaryOwnerFile.RenderDefault),
        ["System.Collections.Generic.SortedDictionary"] = new DictionaryFormatter(SortedDictionaryOwnerFile.Render, jsonReuseStore: "value.Add(key!, itemValue);"),
        ["System.Collections.Generic.SortedList"] = new DictionaryFormatter(SortedListOwnerFile.Render, jsonReuseStore: "value.Add(key!, itemValue);"),
        ["System.Collections.Generic.PriorityQueue"] = new DictionaryFormatter(PriorityQueueOwnerFile.Render),
        ["System.Collections.Concurrent.ConcurrentDictionary"] = new DictionaryFormatter(ConcurrentDictionaryOwnerFile.Render, jsonReuseStore: "value.TryAdd(key!, itemValue);"),
        ["System.Collections.Immutable.ImmutableDictionary"] = new DictionaryFormatter(ImmutableDictionaryOwnerFile.Render),
        ["System.Collections.Immutable.ImmutableSortedDictionary"] = new DictionaryFormatter(ImmutableSortedDictionaryOwnerFile.Render),
        ["System.Collections.ObjectModel.ReadOnlyDictionary"] = new DictionaryFormatter(ReadOnlyDictionaryOwnerFile.Render, forwardsToDictionary: true),
        ["System.Collections.Generic.IDictionary"] = new DictionaryFormatter(DictionaryInterfaceOwnerFile.RenderMutable, forwardsToDictionary: true),
        ["System.Collections.Generic.IReadOnlyDictionary"] = new DictionaryFormatter(DictionaryInterfaceOwnerFile.RenderMutable, forwardsToDictionary: true),
        ["System.Collections.Immutable.IImmutableDictionary"] = new DictionaryFormatter(DictionaryInterfaceOwnerFile.RenderImmutable),
        ["System.Collections.Frozen.FrozenDictionary"] = new DictionaryFormatter(FrozenDictionaryOwnerFile.Render),
    };

    /// <summary>The owner of a list of key and value pairs.</summary>
    internal static readonly DictionaryFormatter PairList = new DictionaryFormatter(PairListOwnerFile.Render);

    /// <summary>Finds the dictionary definition behind a declared dictionary spelling.</summary>
    /// <param name="declaredType">The declared dictionary spelling.</param>
    /// <returns>The registered dictionary, or null.</returns>
    internal static DictionaryFormatter? FindDictionary(string declaredType)
    {
        string type = GeneratedNames.PayloadKey(declaredType);
        int generic = type.IndexOf('<');
        return generic > 0 && Dictionaries.TryGetValue(type.Substring(0, generic), out var formatter) ? formatter : null;
    }

    /// <summary>Finds a registered open sequence by its fully qualified type definition.</summary>
    /// <param name="definition">The namespace and name of the type definition.</param>
    /// <returns>The registered sequence, or null.</returns>
    internal static SequenceFormatter? FindSequence(string definition) =>
        Sequences.TryGetValue(definition, out var formatter) ? formatter : null;

    /// <summary>Finds the formatter definition associated with a modeled collection.</summary>
    /// <param name="member">The symbol-free collection model.</param>
    /// <returns>The matching definition, or null.</returns>
    internal static SequenceFormatter? FindSequence(CollectionMember member)
    {
        if (member.Kind == CollectionKind.Array)
            return member.ArrayRank > 1 ? RectangularArray : Array;
        string type = GeneratedNames.PayloadKey(member.DeclaredType);
        int generic = type.IndexOf('<');
        return generic < 0 ? null : FindSequence(type.Substring(0, generic));
    }

    /// <summary>Tests whether a framework type has a standalone formatter.</summary>
    /// <param name="type">The normalized type identity.</param>
    /// <returns>Whether the type has a registered owner.</returns>
    internal static bool HasStandalone(string type) => Standalone.ContainsKey(type) || GenericIdentity.Of(type) == type;

    /// <summary>Renders a registered standalone owner.</summary>
    /// <param name="type">The normalized type identity.</param>
    /// <param name="set">The publication context.</param>
    /// <param name="operations">The selected operations.</param>
    /// <returns>The complete source file.</returns>
    internal static string RenderStandalone(string type, SharedFormatterSet set, GeneratedOperations operations) =>
        Standalone.TryGetValue(type, out var render) ? render(set, operations) : GenericOwnerFile.Render(type, set, operations);

    /// <summary>Routes a packable element to the family's ordered payload write and every other element to its write.</summary>
    /// <param name="inner">The family's member writer for raw scalars and strings.</param>
    /// <param name="payload">The family's member writer for packable elements.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember Orderable(WriteFormatterMember inner, WriteFormatterMember payload,
        WriteFormatterMember? text = null) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter writer, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) =>
        {
            if (text != null && member.Scalar == "string" && member.Nested == null)
                text(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
            else if (member.Nested != null)
                payload(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
            else
                inner(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
        };

    /// <summary>Routes a packable element to the counted payload write and every other element to the family's write.</summary>
    /// <param name="inner">The family's member writer for raw scalars and strings.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember PayloadAware(WriteFormatterMember inner) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter writer, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) =>
        {
            if (member.Nested != null)
                CountedSequenceMemberWriter.Write(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
            else
                inner(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
        };

    /// <summary>Routes a string element to the counted text write and every other element to the family's raw write.</summary>
    /// <param name="raw">The family's raw member writer.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember TextAware(WriteFormatterMember raw) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter writer, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) =>
        {
            if (member.Scalar == "string")
                InterfaceMemberWriter.WriteCounted(body, member, owner, writer, names, ref ends);
            else
                raw(body, member, owner, depth, writer, names, ref ends, ref cursors, ref spans);
        };

    /// <summary>Adapts writers that do not need the element nesting or cursor and span counters.</summary>
    /// <param name="write">The formatter's member writer.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember Adapt(WriteSimpleFormatterMember write) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter writer, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) => write(body, member, owner, writer, names, ref ends);
}
