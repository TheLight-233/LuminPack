using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>One closed sequence owner the compilation must publish.</summary>
/// <remarks>A scalar element carries its keyword and a packable element carries its value-equal render model, which the owner
/// expands in place. A forwarded element names its own generated owner. These facts contain no compiler objects.</remarks>
internal readonly struct SequenceOwnerFact : IEquatable<SequenceOwnerFact>
{
    /// <summary>The declared sequence family, which selects the owner template.</summary>
    internal readonly SequenceFormatter Family;

    /// <summary>The declared sequence spelling, such as <c>global::...ConcurrentQueue&lt;long&gt;</c>.</summary>
    internal readonly string DeclaredType;

    /// <summary>The raw scalar element keyword, or null when the element is a packable class.</summary>
    internal readonly string? Scalar;

    /// <summary>The packable element's render model, or null when the element is a raw scalar.</summary>
    internal readonly PayloadRenderModel? Element;

    /// <summary>The element type whose generated owner handles the element.</summary>
    internal readonly string? ForwardedType;

    /// <summary>The multidimensional rank, or zero for other sequence families.</summary>
    internal readonly int ArrayRank;

    /// <summary>Whether the binary reader can fill an existing array.</summary>
    internal readonly bool ReuseArraySlots;

    private SequenceOwnerFact(SequenceFormatter family, string declaredType, string? scalar, PayloadRenderModel? element,
        string? forwardedType = null, int arrayRank = 0, bool reuseArraySlots = false)
    {
        ArrayRank = arrayRank;
        ReuseArraySlots = reuseArraySlots;
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
    internal static SequenceOwnerFact ForScalar(SequenceFormatter family, string declaredType, string scalar)
        => new SequenceOwnerFact(family, declaredType, scalar, null);

    /// <summary>Captures a sequence of a packable class element.</summary>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="element">The element's render model.</param>
    /// <returns>The sequence owner fact.</returns>
    internal static SequenceOwnerFact ForPayload(SequenceFormatter family, string declaredType, PayloadRenderModel element)
        => new SequenceOwnerFact(family, declaredType, null, element);

    /// <summary>Captures a sequence whose elements use their own generated owner.</summary>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="declaredType">The declared sequence spelling.</param>
    /// <param name="elementType">The declared element spelling.</param>
    /// <returns>The sequence owner fact.</returns>
    internal static SequenceOwnerFact ForForwarding(SequenceFormatter family, string declaredType, string elementType)
        => new SequenceOwnerFact(family, declaredType, null, null, elementType);

    /// <summary>Captures the rank and reuse capabilities of a rectangular array owner.</summary>
    /// <param name="family">The registered formatter.</param>
    /// <param name="member">The analyzed array.</param>
    /// <returns>The closed formatter request.</returns>
    internal static SequenceOwnerFact ForRectangularArray(SequenceFormatter family, CollectionMember member) =>
        new SequenceOwnerFact(family, member.DeclaredType, member.Scalar, null,
            arrayRank: member.ArrayRank, reuseArraySlots: member.ReuseArraySlots);

    /// <inheritdoc/>
    public bool Equals(SequenceOwnerFact other) => Family == other.Family && DeclaredType == other.DeclaredType &&
        Scalar == other.Scalar && Equals(Element, other.Element) && ForwardedType == other.ForwardedType &&
        ArrayRank == other.ArrayRank && ReuseArraySlots == other.ReuseArraySlots;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SequenceOwnerFact other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (DeclaredType == null ? 0 : DeclaredType.GetHashCode()) ^
        (Scalar == null ? 0 : Scalar.GetHashCode()) ^ (Element == null ? 0 : Element.GetHashCode()) ^
        (ForwardedType == null ? 0 : ForwardedType.GetHashCode()) ^ (Family == null ? 0 : Family.GetHashCode()) ^ ArrayRank ^ (ReuseArraySlots ? 1 : 0);
}

/// <summary>The value-equal publication decision for the shared formatter owners.</summary>
/// <remarks>The set aggregates the owner identities of complete payload models. The requests are sorted, so the
/// same payloads always produce the same files regardless of the order the provider collected them in. Sequence owners and
/// scalar owners are kept sorted and deduplicated, so the decision stays value-equal and order-independent.</remarks>
internal readonly partial struct SharedFormatterSet : IEquatable<SharedFormatterSet>
{
    /// <summary>The assembly extension container the owners belong to, or null before the first complete model.</summary>
    internal readonly string? Extension;

    /// <summary>The owners the complete payloads require.</summary>
    internal readonly string[] Owners;

    /// <summary>The alias mask the consuming project leaves available.</summary>
    internal readonly int AvailableAliases;

    /// <summary>Whether the referenced runtime exposes the culture-info cache the shared owner resolves names through.</summary>
    internal readonly bool CultureInfoReuse;

    /// <summary>The sequence owners, sorted by declared sequence spelling.</summary>
    internal readonly SequenceOwnerFact[] SequenceOwners;

    /// <summary>The raw scalar element owners, sorted by keyword.</summary>
    internal readonly string[] Scalars;

    /// <summary>The dictionary owners, sorted by declared dictionary spelling.</summary>
    internal readonly DictionaryOwnerFact[] DictionaryOwners;

    /// <summary>The compilation facts that affect shared owner declarations.</summary>
    internal readonly GeneratorOptions Options;

    /// <summary>Processes the current formatter contract.</summary>
    /// <param name="extension">The extension used by this operation.</param>
    /// <param name="owners">The owners used by this operation.</param>
    /// <param name="availableAliases">The available aliases used by this operation.</param>
    /// <param name="cultureInfoReuse">The culture info reuse used by this operation.</param>
    /// <param name="sequenceOwners">The sequence owners used by this operation.</param>
    /// <param name="scalars">The scalars used by this operation.</param>
    /// <param name="dictionaryOwners">The dictionary owners used by this operation.</param>
    /// <param name="generalOwners">The general owners used by this operation.</param>
    /// <param name="options">The options used by this operation.</param>
    private SharedFormatterSet(string extension, string[] owners, int availableAliases, bool cultureInfoReuse,
        SequenceOwnerFact[] sequenceOwners, string[] scalars, DictionaryOwnerFact[] dictionaryOwners, FormatterModel[] generalOwners, GeneratorOptions options)
    {
        Options = options;
        GeneralOwners = generalOwners;
        DictionaryOwners = dictionaryOwners;
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
        (Owners == null || Owners.Length == 0) && (SequenceOwners == null || SequenceOwners.Length == 0) &&
        (Scalars == null || Scalars.Length == 0) && (DictionaryOwners == null || DictionaryOwners.Length == 0) &&
        (GeneralOwners == null || GeneralOwners.Length == 0);

    /// <summary>Adds the owners one complete payload model requires.</summary>
    /// <param name="model">The complete payload model.</param>
    /// <param name="options">The project facts of the payload.</param>
    /// <returns>The merged set.</returns>
    internal SharedFormatterSet Include(TypeModel model, GeneratorOptions options) => Collect(new[] { model }, options, this);

    /// <summary>Accumulates a publication batch once and sorts its immutable output only after dependency collection.</summary>
    /// <param name="models">The complete retained payloads.</param>
    /// <param name="options">The project facts.</param>
    /// <param name="seed">Existing requirements when one owner adds a dependency.</param>
    /// <returns>The value-equal complete publication set.</returns>
    internal static SharedFormatterSet Collect(IEnumerable<TypeModel> models, GeneratorOptions options, SharedFormatterSet seed = default)
    {
        string extension = seed.Extension ?? GeneratedNames.Extension(options.AssemblyName);
        int aliases = seed.Extension == null ? options.AvailableAliases : seed.AvailableAliases;
        bool cultureInfoReuse = seed.CultureInfoReuse;
        var facts = new List<SequenceOwnerFact>(seed.SequenceOwners ?? Array.Empty<SequenceOwnerFact>());
        var scalars = new List<string>(seed.Scalars ?? Array.Empty<string>());
        var owners = new List<string>(seed.Owners ?? Array.Empty<string>());
        var dictionaries = new List<DictionaryOwnerFact>(seed.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>());
        var general = new Dictionary<string, FormatterModel>(StringComparer.Ordinal);
        foreach (FormatterModel formatter in seed.GeneralOwners ?? Array.Empty<FormatterModel>())
            general[formatter.Type] = formatter;
        var payloads = new HashSet<string>(StringComparer.Ordinal);
        var requirements = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeModel model in models)
        {
            Walk(model, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, requirements);
            CollectGeneral(model, general, payloads);
        }
        foreach (FormatterModel formatter in general.Values)
        {
            cultureInfoReuse |= formatter.Supports("CanReuseCultureInfo");
            if (formatter.Payload != null)
                Walk(formatter.Payload, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, requirements);
        }
        return new SharedFormatterSet(extension, owners.Distinct(StringComparer.Ordinal).OrderBy(owner => owner, StringComparer.Ordinal).ToArray(),
            aliases, cultureInfoReuse, facts.GroupBy(fact => fact.DeclaredType, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(fact => fact.DeclaredType, StringComparer.Ordinal).ToArray(),
            scalars.Distinct(StringComparer.Ordinal).OrderBy(scalar => scalar, StringComparer.Ordinal).ToArray(),
            dictionaries.GroupBy(fact => fact.DeclaredType, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(fact => fact.DeclaredType, StringComparer.Ordinal).ToArray(),
            general.Values.OrderBy(formatter => formatter.Type, StringComparer.Ordinal).ToArray(), options);
    }

    /// <inheritdoc/>
    public bool Equals(SharedFormatterSet other) => Extension == other.Extension && SameStrings(Owners, other.Owners) &&
        AvailableAliases == other.AvailableAliases && CultureInfoReuse == other.CultureInfoReuse && Equals(Options, other.Options) &&
        SameFacts(SequenceOwners, other.SequenceOwners) && SameStrings(Scalars, other.Scalars) &&
        SameDictionaries(DictionaryOwners, other.DictionaryOwners) &&
        (GeneralOwners ?? Array.Empty<FormatterModel>()).SequenceEqual(other.GeneralOwners ?? Array.Empty<FormatterModel>());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SharedFormatterSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = (Extension == null ? 0 : Extension.GetHashCode()) ^ AvailableAliases ^
            (CultureInfoReuse ? 1 << 20 : 0);
        // The fact and scalar hashes combine with XOR, so the sorted order never changes the result.
        if (SequenceOwners != null)
            for (int index = 0; index < SequenceOwners.Length; index++)
                hash ^= SequenceOwners[index].GetHashCode();
        if (Scalars != null)
            for (int index = 0; index < Scalars.Length; index++)
                hash ^= Scalars[index] == null ? 0 : Scalars[index].GetHashCode();
        if (DictionaryOwners != null)
            for (int index = 0; index < DictionaryOwners.Length; index++)
                hash ^= DictionaryOwners[index].GetHashCode();
        foreach (string owner in Owners ?? Array.Empty<string>())
            hash ^= owner.GetHashCode();
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

    private static bool SameDictionaries(DictionaryOwnerFact[] left, DictionaryOwnerFact[] right)
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

    private static void Walk(TypeModel model, GeneratorOptions options, List<SequenceOwnerFact> facts,
        List<string> scalars, List<string> owners, List<DictionaryOwnerFact> dictionaries, ref bool cultureInfoReuse,
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
                case CultureInfoMember culture:
                    owners.Add(FormatterRegistry.CultureInfo);
                    cultureInfoReuse |= culture.CanReuse;
                    break;
                // A framework member always publishes its own owner; every direct write path calls the owner in place.
                case ScalarMember { Builtin: true } builtin:
                    AddScalar(scalars, builtin.Scalar);
                    break;
                case SpecialMember special when special.Formatter == null:
                    owners.Add(GeneratedNames.PayloadKey(special.Declared));
                    // A closed generic owner calls the element owners of its arguments.
                    if (GenericIdentity.TryParse(GeneratedNames.PayloadKey(special.Declared), out string definition, out string[] arguments))
                    {
                        foreach (string argument in arguments)
                        {
                            if (argument == "string")
                                owners.Add(FormatterRegistry.String);
                            else
                                AddScalar(scalars, argument);
                        }
                        // A grouping and a lookup write their elements through the enumerable owner, and a lookup through the grouping owner.
                        if (definition is GenericIdentity.Grouping or GenericIdentity.Lookup &&
                            FormatterRegistry.FindSequence("System.Collections.Generic.IEnumerable") is { } enumerable)
                        {
                            facts.Add(SequenceOwnerFact.ForScalar(enumerable, GroupingOwnerFile.Sequence(arguments[1]), arguments[1]));
                            if (definition == GenericIdentity.Lookup)
                                owners.Add("System.Linq.IGrouping<" + arguments[0] + "," + arguments[1] + ">");
                        }
                    }
                    break;
                case CollectionMember collection:
                    Collect(collection, options, facts, scalars, owners, dictionaries,
                        ref cultureInfoReuse, visited);
                    break;
                case DictionaryMember dictionary:
                    CollectDictionary(dictionary, options, scalars, owners, dictionaries, facts, ref cultureInfoReuse, visited);
                    break;
                case PayloadMember { Nested: { } nested }:
                    Walk(nested, options, facts, scalars, owners, dictionaries,
                        ref cultureInfoReuse, visited);
                    break;
            }
        }
        // A circle reference read and a version-tolerant record read call the shared string formatter owner.
        if (text && (model is CircleReferenceModel || model is RecordModel { Version: not null }))
            owners.Add(FormatterRegistry.String);
        if (model is UnionModel union)
            for (int index = 0; index < union.Cases.Length; index++)
                if (union.Cases[index].Model is { } item)
                    Walk(item, options, facts, scalars, owners, dictionaries,
                        ref cultureInfoReuse, visited);
    }

    /// <summary>Records the owners one dictionary member requires.</summary>
    /// <param name="member">The dictionary member.</param>
    /// <param name="scalars">The raw scalar owners collected so far.</param>
    /// <param name="owners">The standalone owners collected so far.</param>
    /// <param name="dictionaries">The dictionary owners collected so far.</param>
    private static void CollectDictionary(DictionaryMember member, GeneratorOptions options, List<string> scalars, List<string> owners,
        List<DictionaryOwnerFact> dictionaries, List<SequenceOwnerFact> facts, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        // A packable class value publishes a payload owner and the owners its record body needs.
        if (member.Nested != null)
        {
            if (FormatterRegistry.FindDictionary(member.DeclaredType) is { } payloadFamily &&
                RenderModelFactory.SequenceElement(member.Nested, options) is { } element)
            {
                if (member.KeyText)
                    owners.Add(FormatterRegistry.String);
                else if (member.KeyOwned != null)
                {
                    if (member.KeyOwned.Length != 0)
                        AddScalar(scalars, member.KeyOwned);
                }
                else if (member.KeyType != null)
                    AddScalar(scalars, member.KeyType);
                dictionaries.Add(new DictionaryOwnerFact(payloadFamily, member.DeclaredType, member.KeyText ? "string" : member.KeyType!,
                    element.GlobalName, element));
                // A forwarding family reads through the concrete dictionary owner of the same entries.
                string concrete = "global::System.Collections.Generic.Dictionary<" + (member.KeyText ? "string" : member.KeyType!) + ", " +
                    element.GlobalName + ">";
                if (payloadFamily.ForwardsToDictionary && FormatterRegistry.FindDictionary(concrete) is { } concreteFamily)
                    dictionaries.Add(new DictionaryOwnerFact(concreteFamily, concrete, member.KeyText ? "string" : member.KeyType!,
                        element.GlobalName, element));
                Walk(member.Nested, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, visited);
            }
            return;
        }
        if (member.KeyText || member.ValueText)
            owners.Add(FormatterRegistry.String);
        if (FormatterRegistry.FindDictionary(member.DeclaredType) is not { } family)
            return;
        string? key = member.KeyText ? "string" : member.KeyType, value = member.ValueText ? "string" : member.ValueType;
        if (key == null || value == null)
            return;
        if (member.ForwardedValue is { } forwarded && !RenderModelFactory.CanForward(forwarded, options))
            return;
        dictionaries.Add(new DictionaryOwnerFact(family, member.DeclaredType, key, value, forwardedValue: member.ForwardedValue != null));
        if (member.ForwardedValue != null)
        {
            if (!member.KeyText)
                AddScalar(scalars, member.KeyOwned ?? key);
            CollectForwarded(member.ForwardedValue, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, visited);
            return;
        }
        if (member.Sparse)
        {
            // The JSON roots call the nullable owner of the value, which in turn calls the scalar owner of its payload.
            string inner = value.Substring(0, value.Length - 1);
            owners.Add("System.Nullable<" + inner + ">");
            AddScalar(scalars, inner);
            AddScalar(scalars, key);
            return;
        }
        // A wrapper reads through the concrete dictionary owner of the same entries.
        if (family.ForwardsToDictionary)
        {
            string concrete = "global::System.Collections.Generic.Dictionary<" + key + ", " + value + ">";
            if (FormatterRegistry.FindDictionary(concrete) is { } dictionary)
                dictionaries.Add(new DictionaryOwnerFact(dictionary, concrete, key, value));
        }
        if (!member.KeyText && member.KeyOwned != string.Empty)
            AddScalar(scalars, member.KeyOwned ?? key);
        if (!member.ValueText && member.ValueOwned != string.Empty)
            AddScalar(scalars, member.ValueOwned ?? value);
    }

    private static void Collect(CollectionMember member, GeneratorOptions options,
        List<SequenceOwnerFact> facts, List<string> scalars,
        List<string> owners, List<DictionaryOwnerFact> dictionaries, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        // A list of raw key and value pairs publishes its own pair owner.
        if (member.Shape is { EntryType: not null, Key: not null } pair && (pair.ValueText || pair.Value != null))
        {
            string pairValue = pair.ValueText ? "string" : pair.Value!;
            dictionaries.Add(new DictionaryOwnerFact(FormatterRegistry.PairList, member.DeclaredType, pair.Key, pairValue));
            AddScalar(scalars, pair.Key);
            if (pair.ValueText)
                owners.Add(FormatterRegistry.String);
            else
                AddScalar(scalars, pairValue);
            return;
        }
        var definition = FormatterRegistry.FindSequence(member);
        if (definition != null)
        {
            SequenceOwnerFact? fact = null;
            if (member.ArrayRank > 1 && member.Scalar != null)
                fact = SequenceOwnerFact.ForRectangularArray(definition, member);
            else if (member.ForwardedElement is { } child && definition.Forwarded != null && RenderModelFactory.CanForward(child, options))
                fact = SequenceOwnerFact.ForForwarding(definition, member.DeclaredType,
                    child is CollectionMember sequence ? sequence.DeclaredType : ((DictionaryMember)child).DeclaredType);
            else if (member.Nested != null && definition.Payload != null &&
                RenderModelFactory.SequenceElement(member.Nested, options) is { } payload)
                fact = SequenceOwnerFact.ForPayload(definition, member.DeclaredType, payload);
            else if (member.Scalar == "string" && definition.Text != null)
                fact = SequenceOwnerFact.ForScalar(definition, member.DeclaredType, "string");
            else if (member.Nested == null && member.Shape == null && !member.FormatterCall &&
                member.Scalar is ("bool" or "byte" or "sbyte" or "short" or "ushort" or "char" or "int" or "uint" or
                    "long" or "ulong" or "float" or "double" or "decimal"))
                fact = SequenceOwnerFact.ForScalar(definition, member.DeclaredType, member.Scalar);
            // A list or array of an unmanaged struct, enum or framework built-in stores its span directly as well.
            else if (member.Nested == null && member.Shape == null && !member.FormatterCall && !member.ElementText &&
                (member.Kind is CollectionKind.List or CollectionKind.Array || member.OwnedScalar != null) && member.Scalar != null &&
                member.Scalar.StartsWith("global::", StringComparison.Ordinal))
                fact = SequenceOwnerFact.ForScalar(definition, member.DeclaredType, member.Scalar);

            if (fact is { } required)
            {
                facts.Add(required);
                if (required.Scalar == "string")
                    owners.Add(FormatterRegistry.String);
                else if (GenericIdentity.NullableScalar(required.Scalar) is { } nullable &&
                    GenericIdentity.TryParse(nullable, out _, out string[] arguments))
                {
                    owners.Add(nullable);
                    AddScalar(scalars, arguments[0]);
                }
                else if (required.Scalar != null && !required.Scalar.StartsWith("global::", StringComparison.Ordinal))
                    AddScalar(scalars, required.Scalar);
                else if (required.Scalar != null && !string.IsNullOrEmpty(member.OwnedScalar))
                    AddScalar(scalars, member.OwnedScalar!);
            }
        }
        if (member.ForwardedElement is { } element)
            CollectForwarded(element, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, visited);
        if (member.Nested is { } nested)
            Walk(nested, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, visited);
    }

    /// <summary>Publishes the static owner of a collection nested within another collection.</summary>
    private static void CollectForwarded(MemberModel member, GeneratorOptions options, List<SequenceOwnerFact> facts,
        List<string> scalars, List<string> owners, List<DictionaryOwnerFact> dictionaries, ref bool cultureInfoReuse, HashSet<string> visited)
    {
        if (member is CollectionMember sequence)
            Collect(sequence, options, facts, scalars, owners, dictionaries, ref cultureInfoReuse, visited);
        else if (member is DictionaryMember dictionary)
            CollectDictionary(dictionary, options, scalars, owners, dictionaries, facts, ref cultureInfoReuse, visited);
    }
}
