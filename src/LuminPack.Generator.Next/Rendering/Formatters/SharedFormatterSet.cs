using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>The shared formatter owners one compilation needs.</summary>
[Flags]
internal enum SharedFormatterKind
{
    /// <summary>No shared owner is required.</summary>
    None = 0,

    /// <summary>The <c>int[]</c> roots and owner class.</summary>
    IntArray = 1,

    /// <summary>The <c>List&lt;int&gt;</c> roots and owner class.</summary>
    ListOfInt = 2,

    /// <summary>The <c>string</c> roots and owner class.</summary>
    String = 4,

    /// <summary>The <c>int</c> roots and owner class.</summary>
    Int = 8,

    /// <summary>The <c>System.Globalization.CultureInfo</c> roots and owner class.</summary>
    CultureInfo = 16,

    /// <summary>The <c>System.Collections.Concurrent.ConcurrentQueue&lt;int&gt;</c> roots and owner class.</summary>
    ConcurrentQueueOfInt = 32,

    /// <summary>The <c>System.Collections.Immutable.ImmutableArray&lt;int&gt;</c> roots and owner class.</summary>
    ImmutableArrayOfInt = 64,

    /// <summary>The <c>System.Collections.Concurrent.ConcurrentQueue&lt;string&gt;</c> roots and owner class.</summary>
    ConcurrentQueueOfString = 128,

    /// <summary>The <c>System.Collections.Immutable.ImmutableArray&lt;string&gt;</c> roots and owner class.</summary>
    ImmutableArrayOfString = 256,

    /// <summary>A <c>System.Collections.Concurrent.ConcurrentQueue&lt;T&gt;</c> of a packable class whose write and read
    /// expand the element's record body.</summary>
    ConcurrentQueueOfPayload = 512,

    /// <summary>A <c>System.Collections.Immutable.ImmutableArray&lt;T&gt;</c> of a packable class whose write and read
    /// expand the element's record body.</summary>
    ImmutableArrayOfPayload = 1024,
}

/// <summary>One payload-element sequence owner the compilation must publish.</summary>
/// <remarks>The fact carries the element's value-equal render model, which the owner renderer expands in place. It never
/// retains a symbol, a syntax node or a compilation.</remarks>
internal readonly struct SequenceOwnerFact : IEquatable<SequenceOwnerFact>
{
    /// <summary>Whether the sequence is an <c>ImmutableArray&lt;T&gt;</c> rather than a <c>ConcurrentQueue&lt;T&gt;</c>.</summary>
    internal readonly bool Immutable;

    /// <summary>The declared sequence spelling, such as <c>global::...ConcurrentQueue&lt;global::N.Item&gt;</c>.</summary>
    internal readonly string DeclaredType;

    /// <summary>The element's render model, whose record body the owner expands in place.</summary>
    internal readonly PayloadRenderModel Element;

    /// <summary>Captures one payload-element sequence owner.</summary>
    /// <param name="immutable">Whether the sequence is an immutable array.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="element">The element's render model.</param>
    internal SequenceOwnerFact(bool immutable, string declaredType, PayloadRenderModel element)
    {
        Immutable = immutable;
        DeclaredType = declaredType;
        Element = element;
    }

    /// <inheritdoc/>
    public bool Equals(SequenceOwnerFact other) => Immutable == other.Immutable && DeclaredType == other.DeclaredType &&
        Element.Equals(other.Element);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SequenceOwnerFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (DeclaredType == null ? 0 : DeclaredType.GetHashCode()) ^
        (Element == null ? 0 : Element.GetHashCode()) ^ (Immutable ? 1 << 21 : 0);
}

/// <summary>The value-equal publication decision for the shared formatter owners.</summary>
/// <remarks>The set aggregates the owner flags of every complete payload model; the flags are order-independent, so the
/// same payloads always produce the same files regardless of the order the provider collected them in. A payload-element
/// sequence owner also carries its element's render model, keyed by the declared sequence spelling and kept sorted, so the
/// decision stays value-equal and order-independent.</remarks>
internal readonly struct SharedFormatterSet : IEquatable<SharedFormatterSet>
{
    private const string IntArrayType = "int[]";
    private const string ListOfIntType = "global::System.Collections.Generic.List<int>";
    private const string ConcurrentQueueOfIntType = "global::System.Collections.Concurrent.ConcurrentQueue<int>";
    private const string ImmutableArrayOfIntType = "global::System.Collections.Immutable.ImmutableArray<int>";
    private const string ConcurrentQueueOfStringType = "global::System.Collections.Concurrent.ConcurrentQueue<string>";
    private const string ImmutableArrayOfStringType = "global::System.Collections.Immutable.ImmutableArray<string>";

    /// <summary>The assembly extension container the owners belong to, or null before the first complete model.</summary>
    internal readonly string? Extension;

    /// <summary>The owners the complete payloads require.</summary>
    internal readonly SharedFormatterKind Owners;

    /// <summary>The alias mask the consuming project leaves available.</summary>
    internal readonly int AvailableAliases;

    /// <summary>Whether the referenced runtime exposes the culture-info cache the shared owner resolves names through.</summary>
    internal readonly bool CultureInfoReuse;

    /// <summary>The payload-element sequence owners, sorted by declared sequence spelling.</summary>
    internal readonly SequenceOwnerFact[] SequenceOwners;

    private SharedFormatterSet(string extension, SharedFormatterKind owners, int availableAliases, bool cultureInfoReuse,
        SequenceOwnerFact[] sequenceOwners)
    {
        SequenceOwners = sequenceOwners;
        CultureInfoReuse = cultureInfoReuse;
        AvailableAliases = availableAliases;
        Owners = owners;
        Extension = extension;
    }

    /// <summary>A set that publishes nothing.</summary>
    internal static SharedFormatterSet Empty => default;

    /// <summary>Whether the set publishes no owner file.</summary>
    internal bool IsEmpty => Owners == SharedFormatterKind.None || string.IsNullOrEmpty(Extension);

    /// <summary>Adds the owners one complete payload model requires.</summary>
    /// <param name="model">The complete payload model.</param>
    /// <param name="options">The project facts of the payload.</param>
    /// <returns>The merged set.</returns>
    internal SharedFormatterSet Include(TypeModel model, GeneratorOptions options)
    {
        string extension = Extension ?? GeneratedNames.Extension(options.AssemblyName);
        int aliases = Extension == null ? options.AvailableAliases : AvailableAliases;
        bool cultureInfoReuse = CultureInfoReuse;
        var facts = new List<SequenceOwnerFact>(SequenceOwners ?? System.Array.Empty<SequenceOwnerFact>());
        SharedFormatterKind owners = Owners | Required(model, extension, options, facts, ref cultureInfoReuse);
        // The array, list and sequence JSON roots transfer their elements through the shared scalar owner.
        if ((owners & (SharedFormatterKind.IntArray | SharedFormatterKind.ListOfInt | SharedFormatterKind.ConcurrentQueueOfInt |
            SharedFormatterKind.ImmutableArrayOfInt)) != 0)
            owners |= SharedFormatterKind.Int;
        // A string-element sequence transfers its elements through the shared string owner instead.
        if ((owners & (SharedFormatterKind.ConcurrentQueueOfString | SharedFormatterKind.ImmutableArrayOfString)) != 0)
            owners |= SharedFormatterKind.String;
        SequenceOwnerFact[] sequenceOwners = facts.GroupBy(fact => fact.DeclaredType, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.DeclaredType, StringComparer.Ordinal)
            .ToArray();
        return new SharedFormatterSet(extension, owners, aliases, cultureInfoReuse, sequenceOwners);
    }

    /// <inheritdoc/>
    public bool Equals(SharedFormatterSet other) => Extension == other.Extension && Owners == other.Owners &&
        AvailableAliases == other.AvailableAliases && CultureInfoReuse == other.CultureInfoReuse &&
        SameFacts(SequenceOwners, other.SequenceOwners);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SharedFormatterSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = (Extension == null ? 0 : Extension.GetHashCode()) ^ (int)Owners ^ AvailableAliases ^
            (CultureInfoReuse ? 1 << 20 : 0);
        // The fact hashes combine with XOR, so the sorted fact order never changes the result.
        if (SequenceOwners != null)
            for (int index = 0; index < SequenceOwners.Length; index++)
                hash ^= SequenceOwners[index].GetHashCode();
        return hash;
    }

    private static bool SameFacts(SequenceOwnerFact[] left, SequenceOwnerFact[] right)
    {
        if (left == null || left.Length == 0)
            return right == null || right.Length == 0;
        if (right == null || left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!left[index].Equals(right[index]))
                return false;
        return true;
    }

    private static SharedFormatterKind Required(TypeModel model, string extension, GeneratorOptions options,
        List<SequenceOwnerFact> facts, ref bool cultureInfoReuse)
    {
        SharedFormatterKind owners = SharedFormatterKind.None;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Walk(model, extension, options, facts, GeneratedNames.Owner(extension, IntArrayType), GeneratedNames.Owner(extension, ListOfIntType),
            ref owners, ref cultureInfoReuse, visited);
        return owners;
    }

    private static void Walk(TypeModel model, string extension, GeneratorOptions options, List<SequenceOwnerFact> facts,
        string intArrayOwner, string listOfIntOwner, ref SharedFormatterKind owners, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        if (!visited.Add(model.Identity.GlobalName))
            return;
        bool text = false;
        for (int index = 0; index < model.Members.Length; index++)
        {
            MemberModel member = model.Members[index];
            switch (member)
            {
                case TextMember:
                    text = true;
                    break;
                // The culture owner is published only when the payload that declares the member actually renders; a
                // managed struct, a versioned record or a record beside a dictionary keeps the original pipeline.
                case CultureInfoMember culture when CultureInfoRenders(model):
                    owners |= SharedFormatterKind.CultureInfo;
                    cultureInfoReuse |= culture.CanReuse;
                    break;
                case CollectionMember collection:
                    Collect(collection, extension, options, facts, intArrayOwner, listOfIntOwner, ref owners, ref cultureInfoReuse, visited);
                    break;
                case DictionaryMember { KeyText: true }:
                case DictionaryMember { ValueText: true }:
                    owners |= SharedFormatterKind.String;
                    break;
                case PayloadMember { Nested: { } nested }:
                    Walk(nested, extension, options, facts, intArrayOwner, listOfIntOwner, ref owners, ref cultureInfoReuse, visited);
                    break;
            }
        }
        // A circle reference read and a version-tolerant record read call the shared string formatter owner.
        if (text && (model is CircleReferenceModel || model is RecordModel { Version: not null }))
            owners |= SharedFormatterKind.String;
        if (model is UnionModel union)
            for (int index = 0; index < union.Cases.Length; index++)
                if (union.Cases[index].Model is { } item)
                    Walk(item, extension, options, facts, intArrayOwner, listOfIntOwner, ref owners, ref cultureInfoReuse, visited);
    }

    /// <summary>Whether the direct record renderer covers a payload that declares a culture-info member.</summary>
    /// <param name="model">The payload that declares the member.</param>
    /// <returns>Whether the payload renders with the shared culture-info owner.</returns>
    private static bool CultureInfoRenders(TypeModel model) => model is RecordModel { Versioned: false } record &&
        !record.Members.Any(member => member is DictionaryMember);

    private static void Collect(CollectionMember member, string extension, GeneratorOptions options, List<SequenceOwnerFact> facts,
        string intArrayOwner, string listOfIntOwner, ref SharedFormatterKind owners, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        if (member.Kind == CollectionKind.Array &&
            GeneratedNames.Owner(extension, member.DeclaredType) == intArrayOwner)
            owners |= SharedFormatterKind.IntArray;
        else if (member.Kind == CollectionKind.List &&
            GeneratedNames.Owner(extension, member.DeclaredType) == listOfIntOwner)
            owners |= SharedFormatterKind.ListOfInt;
        else if (member.Kind == CollectionKind.Concurrent &&
            GeneratedNames.Owner(extension, member.DeclaredType) == GeneratedNames.Owner(extension, ConcurrentQueueOfIntType))
            owners |= SharedFormatterKind.ConcurrentQueueOfInt;
        else if (member.Kind == CollectionKind.Immutable &&
            GeneratedNames.Owner(extension, member.DeclaredType) == GeneratedNames.Owner(extension, ImmutableArrayOfIntType))
            owners |= SharedFormatterKind.ImmutableArrayOfInt;
        else if (member.Kind == CollectionKind.Concurrent &&
            GeneratedNames.Owner(extension, member.DeclaredType) == GeneratedNames.Owner(extension, ConcurrentQueueOfStringType))
            owners |= SharedFormatterKind.ConcurrentQueueOfString;
        else if (member.Kind == CollectionKind.Immutable &&
            GeneratedNames.Owner(extension, member.DeclaredType) == GeneratedNames.Owner(extension, ImmutableArrayOfStringType))
            owners |= SharedFormatterKind.ImmutableArrayOfString;
        else if (member.Kind is CollectionKind.Concurrent or CollectionKind.Immutable &&
            RenderModelFactory.SequenceElement(member.Nested, options) is { } element)
        {
            // A packable-class sequence publishes its own owner, whose write and read expand the element record in place.
            bool immutable = member.Kind == CollectionKind.Immutable;
            owners |= immutable ? SharedFormatterKind.ImmutableArrayOfPayload : SharedFormatterKind.ConcurrentQueueOfPayload;
            facts.Add(new SequenceOwnerFact(immutable, member.DeclaredType, element));
        }
        if (member.Nested is { } nested)
            Walk(nested, extension, options, facts, intArrayOwner, listOfIntOwner, ref owners, ref cultureInfoReuse, visited);
    }
}
