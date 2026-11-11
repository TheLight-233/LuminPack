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
    internal SequenceFormatter(CollectionKind kind, RenderSequenceOwner scalar, RenderSequenceOwner? text = null,
        RenderSequenceOwner? payload = null, RenderSequenceOwner? forwarded = null, WriteFormatterMember? writeMember = null,
        bool profileElements = false, bool alwaysLoops = false, bool payloadLoops = false, bool usesCandidateCache = false)
    {
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
        };

    private static readonly SequenceFormatter Array = new SequenceFormatter(CollectionKind.Array, ArrayScalarOwnerFile.Render,
        forwarded: ArrayForwardingOwnerFile.Render);

    private static readonly Dictionary<string, SequenceFormatter> Sequences = new Dictionary<string, SequenceFormatter>(StringComparer.Ordinal)
    {
        ["System.Collections.Generic.List"] = new SequenceFormatter(CollectionKind.List, ListScalarOwnerFile.Render,
            text: ListStringOwnerFile.Render, forwarded: ListForwardingOwnerFile.Render),
        ["System.Collections.Generic.Stack"] = new SequenceFormatter(CollectionKind.Stack, StackOwnerFile.Render,
            text: StackStringOwnerFile.Render, writeMember: Adapt(StackMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.LinkedList"] = new SequenceFormatter(CollectionKind.Linked, LinkedListOwnerFile.Render,
            writeMember: Adapt(LinkedListMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.HashSet"] = new SequenceFormatter(CollectionKind.HashSet, HashSetOwnerFile.Render,
            writeMember: Adapt(HashSetMemberWriter.Write)),
        ["System.Collections.Generic.Queue"] = new SequenceFormatter(CollectionKind.Queue, QueueOwnerFile.Render,
            writeMember: Adapt(QueueMemberWriter.Write)),
        ["System.Collections.ObjectModel.Collection"] = new SequenceFormatter(CollectionKind.Wrapper, CollectionOwnerFile.Render,
            writeMember: Adapt(WrapperMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.ObjectModel.ObservableCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ObservableCollectionOwnerFile.Render,
            writeMember: Adapt(WrapperMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.ObjectModel.ReadOnlyCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyCollectionOwnerFile.Render,
            writeMember: Adapt(WrapperMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.ObjectModel.ReadOnlyObservableCollection"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyObservableCollectionOwnerFile.Render,
            writeMember: Adapt(WrapperMemberWriter.Write), alwaysLoops: true),
        ["System.Runtime.CompilerServices.ReadOnlyCollectionBuilder"] = new SequenceFormatter(CollectionKind.Wrapper, ReadOnlyCollectionBuilderOwnerFile.Render,
            writeMember: Adapt(WrapperMemberWriter.WriteBuilder)),
        ["System.Collections.Generic.IEnumerable"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderEnumerable,
            writeMember: Adapt(InterfaceMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.ICollection"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderCollection,
            writeMember: Adapt(InterfaceMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.IReadOnlyCollection"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderCollection,
            writeMember: Adapt(InterfaceMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.IList"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderList,
            writeMember: Adapt(InterfaceMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.IReadOnlyList"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderList,
            writeMember: Adapt(InterfaceMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Generic.ISet"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderSet,
            writeMember: Adapt(InterfaceMemberWriter.WriteSet), alwaysLoops: true),
        ["System.Collections.Generic.IReadOnlySet"] = new SequenceFormatter(CollectionKind.Interface, InterfaceOwnerFile.RenderSet,
            writeMember: Adapt(InterfaceMemberWriter.WriteSet), alwaysLoops: true),
        ["System.Collections.Immutable.IImmutableList"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderList,
            writeMember: Adapt(InterfaceMemberWriter.WriteCounted), alwaysLoops: true),
        ["System.Collections.Immutable.IImmutableSet"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderSet,
            writeMember: Adapt(InterfaceMemberWriter.WriteCounted), alwaysLoops: true),
        ["System.Collections.Immutable.IImmutableQueue"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderQueue,
            writeMember: Adapt(InterfaceMemberWriter.WriteCounted), alwaysLoops: true),
        ["System.Collections.Immutable.IImmutableStack"] = new SequenceFormatter(CollectionKind.Interface, ImmutableInterfaceOwnerFile.RenderStack,
            writeMember: Adapt(InterfaceMemberWriter.WriteCounted), alwaysLoops: true),
        ["System.Collections.Generic.SortedSet"] = new SequenceFormatter(CollectionKind.SortedSet, SortedSetOwnerFile.Render,
            writeMember: Adapt(SortedSetMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Concurrent.BlockingCollection"] = new SequenceFormatter(CollectionKind.Blocking, BlockingCollectionOwnerFile.Render,
            text: BlockingCollectionOwnerFile.Render, forwarded: BlockingCollectionOwnerFile.Render),
        ["System.Collections.Concurrent.ConcurrentQueue"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentQueueOwnerFile.Render,
            text: ConcurrentQueueStringOwnerFile.Render, payload: ConcurrentQueuePayloadOwnerFile.Render,
            forwarded: ConcurrentQueueForwardingOwnerFile.Render, writeMember: CountedSequenceMemberWriter.Write,
            profileElements: true, alwaysLoops: true),
        ["System.Collections.Concurrent.ConcurrentStack"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentStackOwnerFile.Render,
            writeMember: Adapt(ConcurrentStackMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Concurrent.ConcurrentBag"] = new SequenceFormatter(CollectionKind.Concurrent, ConcurrentBagOwnerFile.Render,
            writeMember: Adapt(ConcurrentBagMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Immutable.ImmutableArray"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableArrayOwnerFile.Render,
            text: ImmutableArrayStringOwnerFile.Render, payload: ImmutableArrayPayloadOwnerFile.Render,
            writeMember: ImmutableArrayMemberWriter.Write, profileElements: true, payloadLoops: true),
        ["System.Collections.Immutable.ImmutableStack"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableStackOwnerFile.Render,
            writeMember: CountedSequenceMemberWriter.Write, alwaysLoops: true),
        ["System.Collections.Immutable.ImmutableQueue"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableQueueOwnerFile.Render,
            writeMember: Adapt(ImmutableQueueMemberWriter.Write), alwaysLoops: true),
        ["System.Collections.Immutable.ImmutableList"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableListOwnerFile.Render,
            writeMember: Adapt(ImmutableListMemberWriter.Write)),
        ["System.Collections.Immutable.ImmutableHashSet"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableHashSetOwnerFile.Render,
            writeMember: Adapt(ImmutableTreeMemberWriter.Write), usesCandidateCache: true),
        ["System.Collections.Immutable.ImmutableSortedSet"] = new SequenceFormatter(CollectionKind.Immutable, ImmutableSortedSetOwnerFile.Render,
            writeMember: Adapt(ImmutableTreeMemberWriter.Write), usesCandidateCache: true),
    };

    private static readonly Dictionary<string, DictionaryFormatter> Dictionaries = new Dictionary<string, DictionaryFormatter>(StringComparer.Ordinal)
    {
        ["System.Collections.Generic.Dictionary"] = new DictionaryFormatter(DictionaryOwnerFile.Render),
        ["System.Collections.Generic.SortedDictionary"] = new DictionaryFormatter(SortedDictionaryOwnerFile.Render),
        ["System.Collections.Generic.SortedList"] = new DictionaryFormatter(SortedListOwnerFile.Render),
        ["System.Collections.Concurrent.ConcurrentDictionary"] = new DictionaryFormatter(ConcurrentDictionaryOwnerFile.Render),
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
            return Array;
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

    /// <summary>Adapts writers that do not need the element nesting or cursor and span counters.</summary>
    /// <param name="write">The formatter's member writer.</param>
    /// <returns>The member writer used by records.</returns>
    internal static WriteFormatterMember Adapt(WriteSimpleFormatterMember write) =>
        (List<string> body, RenderMember member, string owner, int depth, CodeWriter writer, Dictionary<string, int> names,
            ref int ends, ref int cursors, ref int spans) => write(body, member, owner, writer, names, ref ends);
}
