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

    /// <summary>The <c>string</c> roots and owner class.</summary>
    String = 4,

    /// <summary>The <c>System.Globalization.CultureInfo</c> roots and owner class.</summary>
    CultureInfo = 8,

    /// <summary>The <c>System.Collections.Concurrent.ConcurrentQueue&lt;string&gt;</c> roots and owner class.</summary>
    ConcurrentQueueOfString = 16,

    /// <summary>The <c>System.Collections.Immutable.ImmutableArray&lt;string&gt;</c> roots and owner class.</summary>
    ImmutableArrayOfString = 32,
}

/// <summary>One closed sequence owner the compilation must publish.</summary>
/// <remarks>A scalar element carries its keyword and a packable element carries its value-equal render model, which the owner
/// expands in place. A forwarded element names its own generated owner. These facts contain no compiler objects.</remarks>
internal readonly struct SequenceOwnerFact : IEquatable<SequenceOwnerFact>
{
    /// <summary>The declared sequence family, which selects the owner template.</summary>
    internal readonly SequenceFamily Family;

    /// <summary>The declared sequence spelling, such as <c>global::...ConcurrentQueue&lt;long&gt;</c>.</summary>
    internal readonly string DeclaredType;

    /// <summary>The raw scalar element keyword, or null when the element is a packable class.</summary>
    internal readonly string? Scalar;

    /// <summary>The packable element's render model, or null when the element is a raw scalar.</summary>
    internal readonly PayloadRenderModel? Element;

    /// <summary>The element type whose generated owner handles the element.</summary>
    internal readonly string? ForwardedType;

    private SequenceOwnerFact(SequenceFamily family, string declaredType, string? scalar, PayloadRenderModel? element,
        string? forwardedType = null)
    {
        ForwardedType = forwardedType;
        Element = element;
        Scalar = scalar;
        DeclaredType = declaredType;
        Family = family;
    }

    /// <summary>Captures a sequence of a raw scalar element.</summary>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="scalar">The raw scalar element keyword.</param>
    /// <returns>The sequence owner fact.</returns>
    internal static SequenceOwnerFact ForScalar(SequenceFamily family, string declaredType, string scalar)
        => new SequenceOwnerFact(family, declaredType, scalar, null);

    /// <summary>Captures a sequence of a packable class element.</summary>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="element">The element's render model.</param>
    /// <returns>The sequence owner fact.</returns>
    internal static SequenceOwnerFact ForPayload(SequenceFamily family, string declaredType, PayloadRenderModel element)
        => new SequenceOwnerFact(family, declaredType, null, element);

    /// <summary>Captures a sequence whose elements use their own generated owner.</summary>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="elementType">The declared element spelling.</param>
    /// <returns>The sequence owner fact.</returns>
    internal static SequenceOwnerFact ForForwarding(SequenceFamily family, string declaredType, string elementType)
        => new SequenceOwnerFact(family, declaredType, null, null, elementType);

    /// <inheritdoc/>
    public bool Equals(SequenceOwnerFact other) => Family == other.Family && DeclaredType == other.DeclaredType &&
        Scalar == other.Scalar && Equals(Element, other.Element) && ForwardedType == other.ForwardedType;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SequenceOwnerFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (DeclaredType == null ? 0 : DeclaredType.GetHashCode()) ^
        (Scalar == null ? 0 : Scalar.GetHashCode()) ^ (Element == null ? 0 : Element.GetHashCode()) ^
        (ForwardedType == null ? 0 : ForwardedType.GetHashCode()) ^ ((int)Family + 1) << 21;
}

/// <summary>The value-equal publication decision for the shared formatter owners.</summary>
/// <remarks>The set aggregates the owner flags of every complete payload model; the flags are order-independent, so the
/// same payloads always produce the same files regardless of the order the provider collected them in. Sequence owners and
/// scalar owners are kept sorted and deduplicated, so the decision stays value-equal and order-independent.</remarks>
internal readonly struct SharedFormatterSet : IEquatable<SharedFormatterSet>
{
    /// <summary>The assembly extension container the owners belong to, or null before the first complete model.</summary>
    internal readonly string? Extension;

    /// <summary>The owners the complete payloads require.</summary>
    internal readonly SharedFormatterKind Owners;

    /// <summary>The alias mask the consuming project leaves available.</summary>
    internal readonly int AvailableAliases;

    /// <summary>Whether the referenced runtime exposes the culture-info cache the shared owner resolves names through.</summary>
    internal readonly bool CultureInfoReuse;

    /// <summary>The sequence owners, sorted by declared sequence spelling.</summary>
    internal readonly SequenceOwnerFact[] SequenceOwners;

    /// <summary>The raw scalar element owners, sorted by keyword.</summary>
    internal readonly string[] Scalars;

    private SharedFormatterSet(string extension, SharedFormatterKind owners, int availableAliases, bool cultureInfoReuse,
        SequenceOwnerFact[] sequenceOwners, string[] scalars)
    {
        Scalars = scalars;
        SequenceOwners = sequenceOwners;
        CultureInfoReuse = cultureInfoReuse;
        AvailableAliases = availableAliases;
        Owners = owners;
        Extension = extension;
    }

    /// <summary>A set that publishes nothing.</summary>
    internal static SharedFormatterSet Empty => default;

    /// <summary>Whether the set publishes no owner file.</summary>
    internal bool IsEmpty => string.IsNullOrEmpty(Extension) ||
        Owners == SharedFormatterKind.None && (SequenceOwners == null || SequenceOwners.Length == 0);

    /// <summary>Adds the owners one complete payload model requires.</summary>
    /// <param name="model">The complete payload model.</param>
    /// <param name="options">The project facts of the payload.</param>
    /// <returns>The merged set.</returns>
    internal SharedFormatterSet Include(TypeModel model, GeneratorOptions options)
    {
        string extension = Extension ?? GeneratedNames.Extension(options.AssemblyName);
        int aliases = Extension == null ? options.AvailableAliases : AvailableAliases;
        bool cultureInfoReuse = CultureInfoReuse;
        var facts = new List<SequenceOwnerFact>(SequenceOwners ?? Array.Empty<SequenceOwnerFact>());
        var scalars = new List<string>(Scalars ?? Array.Empty<string>());
        SharedFormatterKind owners = Owners | Required(model, options, facts, scalars, ref cultureInfoReuse);
        // The string sequence roots transfer their elements through the shared string owner.
        if ((owners & (SharedFormatterKind.ConcurrentQueueOfString | SharedFormatterKind.ImmutableArrayOfString)) != 0)
            owners |= SharedFormatterKind.String;
        SequenceOwnerFact[] sequenceOwners = facts.GroupBy(fact => fact.DeclaredType, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.DeclaredType, StringComparer.Ordinal)
            .ToArray();
        return new SharedFormatterSet(extension, owners, aliases, cultureInfoReuse, sequenceOwners,
            scalars.Distinct(StringComparer.Ordinal).OrderBy(scalar => scalar, StringComparer.Ordinal).ToArray());
    }

    /// <inheritdoc/>
    public bool Equals(SharedFormatterSet other) => Extension == other.Extension && Owners == other.Owners &&
        AvailableAliases == other.AvailableAliases && CultureInfoReuse == other.CultureInfoReuse &&
        SameFacts(SequenceOwners, other.SequenceOwners) && SameStrings(Scalars, other.Scalars);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SharedFormatterSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = (Extension == null ? 0 : Extension.GetHashCode()) ^ (int)Owners ^ AvailableAliases ^
            (CultureInfoReuse ? 1 << 20 : 0);
        // The fact and scalar hashes combine with XOR, so the sorted order never changes the result.
        if (SequenceOwners != null)
            for (int index = 0; index < SequenceOwners.Length; index++)
                hash ^= SequenceOwners[index].GetHashCode();
        if (Scalars != null)
            for (int index = 0; index < Scalars.Length; index++)
                hash ^= Scalars[index] == null ? 0 : Scalars[index].GetHashCode();
        return hash;
    }

    private static void AddScalar(List<string> scalars, string scalar)
    {
        if (!scalars.Contains(scalar))
            scalars.Add(scalar);
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

    private static bool SameStrings(string[] left, string[] right)
    {
        if (left == null || left.Length == 0)
            return right == null || right.Length == 0;
        if (right == null || left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (left[index] != right[index])
                return false;
        return true;
    }

    private static SharedFormatterKind Required(TypeModel model, GeneratorOptions options,
        List<SequenceOwnerFact> facts, List<string> scalars, ref bool cultureInfoReuse)
    {
        SharedFormatterKind owners = SharedFormatterKind.None;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Walk(model, options, facts, scalars, ref owners, ref cultureInfoReuse, visited);
        return owners;
    }

    private static void Walk(TypeModel model, GeneratorOptions options, List<SequenceOwnerFact> facts,
        List<string> scalars, ref SharedFormatterKind owners, ref bool cultureInfoReuse,
        HashSet<string> visited)
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
                    Collect(collection, options, facts, scalars, ref owners,
                        ref cultureInfoReuse, visited);
                    break;
                case DictionaryMember { KeyText: true }:
                case DictionaryMember { ValueText: true }:
                    owners |= SharedFormatterKind.String;
                    break;
                case PayloadMember { Nested: { } nested }:
                    Walk(nested, options, facts, scalars, ref owners,
                        ref cultureInfoReuse, visited);
                    break;
            }
        }
        // A circle reference read and a version-tolerant record read call the shared string formatter owner.
        if (text && (model is CircleReferenceModel || model is RecordModel { Version: not null }))
            owners |= SharedFormatterKind.String;
        if (model is UnionModel union)
            for (int index = 0; index < union.Cases.Length; index++)
                if (union.Cases[index].Model is { } item)
                    Walk(item, options, facts, scalars, ref owners,
                        ref cultureInfoReuse, visited);
    }

    /// <summary>Whether the direct record renderer covers a payload that declares a culture-info member.</summary>
    /// <param name="model">The payload that declares the member.</param>
    /// <returns>Whether the payload renders with the shared culture-info owner.</returns>
    private static bool CultureInfoRenders(TypeModel model) => model is RecordModel { Versioned: false } record &&
        !record.Members.Any(member => member is DictionaryMember);

    private static void Collect(CollectionMember member, GeneratorOptions options,
        List<SequenceOwnerFact> facts, List<string> scalars,
        ref SharedFormatterKind owners, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        if (member.ForwardedElement is { } element)
        {
            SequenceFamily? family = member.Kind switch
            {
                CollectionKind.Array => SequenceFamily.Array,
                CollectionKind.Blocking => SequenceFamily.BlockingCollection,
                // Only the families with a published forwarded owner participate; the remaining ones stay unmapped.
                CollectionKind.Concurrent or CollectionKind.Immutable => RenderModelFactory.SupportedFamily(member),
                // The list owner forwards an element that carries its own generated owner; the stack and the sorted set have
                // no forwarding template, so those elements stay unmapped.
                CollectionKind.List or CollectionKind.NestedList => SequenceFamily.List,
                _ => null,
            };
            if (family is { } forwarded)
                facts.Add(SequenceOwnerFact.ForForwarding(forwarded, member.DeclaredType, element.DeclaredType));
            Collect(element, options, facts, scalars, ref owners, ref cultureInfoReuse, visited);
        }
        else if (member.Kind == CollectionKind.Blocking && member.Scalar is { } blockingElement)
        {
            facts.Add(SequenceOwnerFact.ForScalar(SequenceFamily.BlockingCollection, member.DeclaredType, blockingElement));
            if (blockingElement == "string")
                owners |= SharedFormatterKind.String;
            else
                AddScalar(scalars, blockingElement);
        }
        else if (member.Kind == CollectionKind.List && member.ElementText)
        {
            facts.Add(SequenceOwnerFact.ForScalar(SequenceFamily.List, member.DeclaredType, "string"));
            owners |= SharedFormatterKind.String;
        }
        else if (member.Kind is CollectionKind.Array or CollectionKind.List && !member.FormatterCall &&
            member.Nested == null && member.Shape == null && member.Scalar is
            ("bool" or "byte" or "sbyte" or "short" or "ushort" or "char" or "int" or "uint" or "long" or "ulong" or
             "float" or "double" or "decimal"))
        {
            var family = member.Kind == CollectionKind.Array ? SequenceFamily.Array : SequenceFamily.List;
            facts.Add(SequenceOwnerFact.ForScalar(family, member.DeclaredType, member.Scalar));
            AddScalar(scalars, member.Scalar);
        }
        else if (member.Kind is CollectionKind.Concurrent or CollectionKind.Immutable)
            Sequence(member, options, facts, scalars, ref owners);
        else if (member.Kind is CollectionKind.Stack or CollectionKind.SortedSet)
            StackOrSortedSet(member, facts, scalars, ref owners);
        if (member.Nested is { } nested)
            Walk(nested, options, facts, scalars, ref owners, ref cultureInfoReuse, visited);
    }

    /// <summary>Records the owner one concurrent or immutable sequence member requires.</summary>
    /// <param name="member">The sequence member.</param>
    /// <param name="options">The project facts, which decide whether a payload element is covered.</param>
    /// <param name="facts">The sequence owner facts collected so far.</param>
    /// <param name="scalars">The raw scalar owners collected so far.</param>
    /// <param name="owners">The fixed owner flags collected so far.</param>
    private static void Sequence(CollectionMember member, GeneratorOptions options, List<SequenceOwnerFact> facts,
        List<string> scalars, ref SharedFormatterKind owners)
    {
        // The analysis group also covers families whose owners are not ported yet, and the stack covers scalars only.
        if (RenderModelFactory.SupportedFamily(member) is not { } family)
            return;
        // A packable-class sequence publishes its own owner, whose write and read expand the element record in place.
        if (member.Nested != null)
        {
            if (RenderModelFactory.SequenceElement(member.Nested, options) is { } element)
                facts.Add(SequenceOwnerFact.ForPayload(family, member.DeclaredType, element));
            return;
        }
        if (member.Scalar == "string")
        {
            // The string variants have their own templates and transfer elements through the shared string owner.
            owners |= family == SequenceFamily.ImmutableArray
                ? SharedFormatterKind.ImmutableArrayOfString : SharedFormatterKind.ConcurrentQueueOfString;
            return;
        }
        if (member.Scalar != null)
        {
            facts.Add(SequenceOwnerFact.ForScalar(family, member.DeclaredType, member.Scalar));
            AddScalar(scalars, member.Scalar);
        }
    }

    /// <summary>Records the owner one stack or sorted set member requires.</summary>
    /// <param name="member">The sequence member.</param>
    /// <param name="facts">The sequence owner facts collected so far.</param>
    /// <param name="scalars">The raw scalar owners collected so far.</param>
    /// <param name="owners">The fixed owner flags collected so far.</param>
    private static void StackOrSortedSet(CollectionMember member, List<SequenceOwnerFact> facts, List<string> scalars,
        ref SharedFormatterKind owners)
    {
        // Both families publish a raw scalar owner; the stack additionally transfers string elements through the shared owner.
        if (member.Nested != null || member.Shape != null || member.Scalar == null)
            return;
        if (member.Kind == CollectionKind.Stack)
        {
            facts.Add(SequenceOwnerFact.ForScalar(SequenceFamily.Stack, member.DeclaredType, member.Scalar));
            if (member.Scalar == "string")
                owners |= SharedFormatterKind.String;
            else
                AddScalar(scalars, member.Scalar);
            return;
        }
        // A sorted set stores its ordered tree as raw unmanaged storage, so a string element stays unmapped.
        if (member.Scalar == "string")
            return;
        facts.Add(SequenceOwnerFact.ForScalar(SequenceFamily.SortedSet, member.DeclaredType, member.Scalar));
        AddScalar(scalars, member.Scalar);
    }
}
