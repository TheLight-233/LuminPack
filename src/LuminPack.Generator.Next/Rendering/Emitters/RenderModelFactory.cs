using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Maps a Next payload model onto the render model the direct emitters consume.</summary>
/// <remarks>This adapter is the only place where the normalized <see cref="TypeModel"/> facts become emitter input. It covers
/// ordinary records whose members are public fields of a fixed scalar, text, a supported sequence or a supported keyed family,
/// raw, portable and managed structs, and reference-tracked nodes whose complete framing the circle emitters reproduce.
/// Features whose rendering facts are not modeled return null so the pipeline emits nothing instead of partial code.</remarks>
internal static class RenderModelFactory
{
    /// <summary>Builds the render model of one payload.</summary>
    /// <param name="model">The normalized payload.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render model, or null when this adapter does not cover the payload yet.</returns>
    internal static PayloadRenderModel? Create(TypeModel model, GeneratorOptions options)
    {
        if (model is UnionModel union)
            return Union(union, options);
        if (model is CircleReferenceModel circle)
            return Circle(circle, options);
        // A version-tolerant struct writes a schema prefix the struct roots do not reproduce yet.
        if (model is StructModel { Versioned: true })
            return null;
        bool raw = model is StructModel { Storage: StructStorage.RawUnmanaged, Layout: null };
        // A struct with reference members keeps the record protocol: public fields of fixed scalars, strings and built-in
        // values only. Mirrored storage, construction, callbacks, rent, a version schema and a bound change that framing and
        // are not modeled yet.
        bool managed = model is StructModel { Storage: StructStorage.ManagedRecord } managedStruct &&
            !managedStruct.Versioned && !managedStruct.Rent && !managedStruct.Constructors && !managedStruct.Callbacks &&
            !managedStruct.Identity.IsGeneric && managedStruct.Policy.Bound == null &&
            managedStruct.Members.All(member => !member.Mirrored && member is ScalarMember or TextMember);
        PortableLayout? portable = model is StructModel { Storage: StructStorage.PortableUnmanaged } portableStruct
            ? portableStruct.Layout : null;
        if (portable is not { Complete: true })
            portable = null;
        bool isStruct = raw || portable != null || managed;
        if (!isStruct && model is not RecordModel)
            return null;
        // The layout mirror reaches auto-property and included non-public storage. A record whose mirror could not be
        // reproduced exactly stays unmapped instead of emitting partial code.
        RecordModel? record = isStruct ? null : (RecordModel)model;
        // A version-tolerant record renders only when its analysis produced the complete schema and rejected every shape the
        // version emitter does not reproduce.
        if (record is { Versioned: true } versionedRecord && (versionedRecord.Version == null || versionedRecord.Derived))
            return null;
        // The version-tolerant body writes its own member schema and does not reproduce the shared culture owner call.
        if (record is { Versioned: true } && model.Members.Any(member => member is CultureInfoMember))
            return PayloadEmitter.Reject<PayloadRenderModel>("versioned culture info member");
        string? mirror = record?.Mirror;
        if (record != null && mirror == null && record.Members.Any(member => member.Mirrored))
            return null;
        if (portable != null && !portable.Fields.All(field => field.Leaves.Length != 0))
            return null;
        // The dictionary write path owns its member loop and cannot frame a culture-info member, so that record stays with
        // the original pipeline instead of emitting a partial body.
        if (model.Members.Any(member => member is CultureInfoMember) && model.Members.Any(member => member is DictionaryMember))
            return PayloadEmitter.Reject<PayloadRenderModel>("culture info beside a dictionary member");
        string extension = GeneratedNames.Extension(options.AssemblyName);
        var members = new RenderMember[model.Members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            RenderMember? member = Member(model.Members[index], extension, options);
            if (member == null)
                return null;
            members[index] = member;
        }
        // A raw struct copies its complete storage; a portable struct transfers only fixed fields. A managed struct writes a
        // record body, so its strings and built-in values keep their variable-length records.
        if (!managed && isStruct && !members.All(member => member.IsFixed))
            return null;
        string globalName = model.Identity.GlobalName;
        // The selected constructor, pool rent and serialization callbacks scale the record's construction and framing; the
        // direct emitters consume the same facts the legacy model states.
        ConstructorModel? constructor = record?.Constructor;
        CallbackModel? callbacks = record?.Callbacks;
        string? rent = record?.Rent;
        bool hasCallbacks = callbacks != null;
        // The proven object write needs .NET 8, unsafe code and a non-Unity project. The named member write only needs every
        // variable member to be a string, so a project outside those facts still publishes it. A mirrored record reaches
        // its storage through the mirror, so neither write is its original record body. A callback keeps both out, as the
        // legacy model does.
        bool proven = model is RecordModel { Proven: true } && !hasCallbacks;
        bool memberWrite = !hasCallbacks && !model.Members.Any(member => member.Mirrored) &&
            model is RecordModel { Versioned: false, Derived: false } && model.Policy.Bound == null &&
            model.Members.Length != 0 && model.Members.Any(member => member is TextMember) &&
            model.Members.All(member => member is ScalarMember or TextMember);
        string? view = portable?.ViewName;
        PortableView? portableView = portable == null ? null : View(portable);
        var names = new System.Collections.Generic.HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
        bool viewElided = portableView != null && Enumerable.Range(0, members.Length)
            .All(index => names.Contains("F" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return new PayloadRenderModel(globalName, ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName)), extension,
            GeneratedNames.JsonHelperName("Json_" + GeneratedNames.Sanitize(globalName)),
            freshResult: isStruct || model is RecordModel { FreshResult: true },
            uniqueEntries: options.Interceptors, members, proven, mirror: mirror!, mirrorFields: record?.MirrorFields ?? null!,
            derived: model is RecordModel { Derived: true },
            isStruct, view: view!, viewElided, fixedUnionCase: record?.FixedUnionCase ?? false,
            measureRecord: members.All(item => item.IsFixed) && model switch
            {
                StructModel structModel => structModel.RecordWidth,
                RecordModel recordModel => recordModel.RecordWidth,
                _ => false,
            },
            constructorArguments: constructor?.Arguments!, constructorMembers: constructor?.Members!,
            memberWrite: memberWrite, net8: options.Net8,
            utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: Schema(record?.Version),
            referencePayload: model.Members.Any(item => item is not ScalarMember),
            layoutAttribute: portable?.LayoutAttribute ?? record?.MirrorLayout,
            bounded: model.Policy.Bound, typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints,
            trusted: model.Policy.Input == InputPolicy.Trusted, portableView: portableView, mirrorOffsets: record?.MirrorOffsets,
            managedStruct: managed, rent: rent!,
            writeBefore: callbacks?.WriteBefore!, writeAfter: callbacks?.WriteAfter!,
            readBefore: callbacks?.ReadBefore!, readAfter: callbacks?.ReadAfter!,
            declaredOrder: constructor?.DeclaredOrder!);
    }

    /// <summary>Projects the modeled schema onto the emitter's schema contract.</summary>
    /// <param name="schema">The analyzed schema, or null for an ordinary record.</param>
    /// <returns>The emitter schema, or null.</returns>
    private static VersionSchema? Schema(VersionSchemaModel? schema) => schema == null ? null :
        new VersionSchema(schema.Schema, schema.Metadata, schema.Prefix, schema.FieldEntries, schema.TempTypes, schema.TypeNames,
            schema.QualifiedTypes, schema.Layout, schema.LayoutFields, schema.OldNames);

    /// <summary>Maps a Union root when its declared profile, slots, cases and framing are the ones the direct Union emitters
    /// reproduce.</summary>
    /// <param name="model">The normalized Union root.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render model, or null when the root is outside the direct Union emitters.</returns>
    private static PayloadRenderModel? Union(UnionModel model, GeneratorOptions options)
    {
        StringProfile profile = model.Policy.Strings;
        if (profile.Format != 'G' || profile.Precision != byte.MaxValue || profile.Depth != 1024 || profile.Strict)
            return PayloadEmitter.Reject<PayloadRenderModel>("union declared profile");
        if (profile.Encoding != StringEncoding.Utf16 || profile.Recording != StringRecording.Length)
            return PayloadEmitter.Reject<PayloadRenderModel>("union compiled profile slots");
        if (model.Identity.IsGeneric)
            return PayloadEmitter.Reject<PayloadRenderModel>("union generic");
        if (model.Cases.Length == 0)
            return PayloadEmitter.Reject<PayloadRenderModel>("union cases");
        var cases = new UnionRenderCase[model.Cases.Length];
        for (int index = 0; index < cases.Length; index++)
        {
            UnionCaseModel item = model.Cases[index];
            PayloadRenderModel? caseModel = item.Model == null ? null : Create(item.Model, options);
            // A case arm either calls the case's own read entry or expands a complete fixed record body.
            if (!item.OwnEntry && (caseModel == null || caseModel.UnionRoot || caseModel.IsStruct || caseModel.Mirror != null ||
                caseModel.Version != null || caseModel.ConstructorArguments != null || caseModel.Members.Length == 0 ||
                !caseModel.Members.All(member => member.IsFixed)))
                return PayloadEmitter.Reject<PayloadRenderModel>("union case read: " + item.Type.DisplayName);
            cases[index] = new UnionRenderCase(item.Tag, item.Type.GlobalName, item.OwnEntry, item.ReadGeneric, item.Sealed,
                item.FixedCursor, caseModel!, item.Readable, item.Chain, item.Namespace!);
        }
        string globalName = model.Identity.GlobalName;
        return new PayloadRenderModel(globalName, ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName)),
            GeneratedNames.Extension(options.AssemblyName), GeneratedNames.JsonHelperName("Json_" + GeneratedNames.Sanitize(globalName)),
            freshResult: false, uniqueEntries: options.Interceptors, System.Array.Empty<RenderMember>(), proven: false,
            mirror: null!, mirrorFields: null!, derived: false, isStruct: false, view: null!, viewElided: false,
            fixedUnionCase: false, measureRecord: false, constructorArguments: null, constructorMembers: null, memberWrite: false,
            net8: options.Net8, utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: null!,
            referencePayload: false, layoutAttribute: null!, typeParameters: model.Identity.TypeParameters,
            typeConstraints: model.Constraints, unionRoot: true, unionInterface: model.IsInterface,
            unionSuffix: model.UnionSuffix, unionCases: cases, unionCursor: model.Cursor, unionReadGeneric: model.ReadGeneric,
            unionChain: model.Chain, unionNamespace: model.Namespace!,
            unionBinarySlots: Slots(model.BinarySlots), unionJsonSlots: Slots(model.JsonSlots),
            unionCanonical: model.Canonical, unionStandardBinary: model.StandardBinary, unionStandardJson: model.StandardJson,
            unionTier: (int)model.Tier, unionTierCases: model.TierCases, unionTierSealed: model.TierSealed, unionWide: model.WideTags,
            trusted: model.Policy.Input == InputPolicy.Trusted);
    }

    /// <summary>Maps the model's compiled profile slots onto the emitter's slots.</summary>
    /// <param name="slots">The modeled slots.</param>
    /// <returns>The emitter slots.</returns>
    private static UnionRenderSlot[] Slots(UnionSlotModel[] slots)
    {
        var result = new UnionRenderSlot[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            UnionSlotModel slot = slots[index];
            result[index] = new UnionRenderSlot(slot.Suffix, slot.Profile, slot.Identity, slot.Encoding, slot.Recording);
        }
        return result;
    }

    /// <summary>Maps a reference-tracked node when every legacy circle check passes.</summary>
    /// <param name="model">The normalized node.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render model, or null when the node's framing is outside the circle emitters.</returns>
    private static PayloadRenderModel? Circle(CircleReferenceModel model, GeneratorOptions options)
    {
        // A base payload, a rent, a selected constructor, callbacks, a bound, a trusted declaration, a declared string
        // profile and a mirrored storage that cannot be reproduced exactly all change the framing the circle roots spell.
        if (model.Derived || model.Rent || model.SelectedConstructor || model.Callbacks || model.MirrorFields == null ||
            model.Policy.Bound != null || model.Policy.Input == InputPolicy.Trusted || !DefaultProfile(model.Policy.Strings) ||
            model.Members.Length > 249 || !model.FieldsInOrder)
            return null;
        string extension = GeneratedNames.Extension(options.AssemblyName);
        var members = new RenderMember[model.Members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            RenderMember? member = CircleMember(model, model.Members[index], extension, options);
            if (member == null)
                return null;
            members[index] = member;
        }
        // Reserve-able members may only precede the reference members; a later one would reopen a reservation group.
        int firstObject = Array.FindIndex(members, member => member.Kind == RenderMemberKind.Object);
        if (firstObject >= 0 && members.Skip(firstObject).Any(member => !member.IsPayload))
            return null;
        string globalName = model.Identity.GlobalName;
        string readable = ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName));
        return new PayloadRenderModel(globalName, readable, extension,
            GeneratedNames.JsonHelperName("Json_" + GeneratedNames.Sanitize(globalName)), freshResult: false,
            uniqueEntries: options.Interceptors, members, proven: false, mirror: MirrorName(model, globalName),
            mirrorFields: model.MirrorFields, derived: false, isStruct: false, view: null!, viewElided: false,
            fixedUnionCase: false, measureRecord: false, constructorArguments: null, constructorMembers: null, memberWrite: false,
            net8: options.Net8, utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: null!,
            referencePayload: model.Members.Any(item => item is not ScalarMember), layoutAttribute: model.MirrorLayout,
            circle: true, circleGuarded: model.Guarded, mirrorOffsets: model.MirrorOffsets,
            typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints);
    }

    /// <summary>Maps one circle member: a fixed scalar of at most four bytes, a string or another reference-tracked node.</summary>
    /// <param name="owner">The declaring node.</param>
    /// <param name="member">The normalized member.</param>
    /// <param name="extension">The assembly's extension container.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render member, or null when the member is outside the circle framing.</returns>
    private static RenderMember? CircleMember(CircleReferenceModel owner, MemberModel member, string extension, GeneratorOptions options)
    {
        switch (member)
        {
            case ScalarMember { Raw: false, Size: > 0 and <= 4 } scalar:
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, scalar.Size, null!,
                    FieldType(scalar.Scalar), mirrored: false);
            case TextMember:
                return new RenderMember(member.Name, RenderMemberKind.Text, "string", "string", 0, null!, ScalarKind.String,
                    mirrored: false);
            case PayloadMember { Shape: PayloadShape.ReferenceTracked } payload:
                // A self member reuses this model; any other member is modeled as its own reference-tracked record.
                PayloadRenderModel? nested = null;
                if (payload.Nested != null)
                {
                    nested = Create(payload.Nested, options);
                    if (nested == null || !nested.Circle)
                        return null;
                }
                else if (payload.Type.GlobalName != owner.Identity.GlobalName)
                    return null;
                return new RenderMember(payload.Name, RenderMemberKind.Object, payload.Type.GlobalName, payload.Type.GlobalName,
                    0, "global::LuminPack." + GeneratedNames.Owner(extension, payload.Type.GlobalName), ScalarKind.Other,
                    mirrored: false, nested: nested);
            default:
                return null;
        }
    }

    /// <summary>Whether a node inherits the simple string profile the circle roots spell.</summary>
    /// <param name="profile">The effective string profile.</param>
    /// <returns>Whether the profile is the canonical default.</returns>
    private static bool DefaultProfile(StringProfile profile)
        => profile.Format == 'G' && profile.Precision == byte.MaxValue && profile.Depth == 1024 && !profile.Strict;

    /// <summary>Spells the node's storage mirror the way the legacy model names it.</summary>
    /// <param name="model">The normalized node.</param>
    /// <param name="globalName">The node's fully qualified spelling.</param>
    /// <returns>The mirror declaration name.</returns>
    private static string MirrorName(CircleReferenceModel model, string globalName)
    {
        string digest;
        using (SHA256 hash = SHA256.Create())
            digest = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(globalName))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        uint slot = 2166136261;
        unchecked
        {
            foreach (char character in globalName)
            {
                slot ^= character;
                slot *= 16777619;
            }
        }
        string readable = new string(globalName.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
        string local = model.Identity.DisplayName;
        if (local.StartsWith("global::", StringComparison.Ordinal))
            local = local.Substring(8);
        local = local.Replace(".", "_").Replace('+', '_');
        int generic = local.IndexOf('<');
        if (generic >= 0)
            local = local.Substring(0, generic);
        string parameters = model.Identity.TypeParameters.Length == 0
            ? string.Empty : "<" + string.Join(", ", model.Identity.TypeParameters) + ">";
        return "LuminLayout_" + digest + "_" + readable + "_" + slot.ToString("x8", CultureInfo.InvariantCulture) + "_Local" + local +
            parameters;
    }

    /// <summary>Maps the model's positional view onto the emitter's view.</summary>
    /// <param name="layout">The complete portable layout.</param>
    /// <returns>The view with every scalar transfer in wire order.</returns>
    private static PortableView View(PortableLayout layout)
    {
        var fields = new PortableViewField[layout.Fields.Length];
        for (int index = 0; index < fields.Length; index++)
        {
            PortableFieldModel field = layout.Fields[index];
            var leaves = new PortableLeaf[field.Leaves.Length];
            for (int leaf = 0; leaf < leaves.Length; leaf++)
            {
                PortableLeafModel source = field.Leaves[leaf];
                leaves[leaf] = new PortableLeaf(source.Access, source.Scalar, source.Size, source.Volatile);
            }
            fields[index] = new PortableViewField(field.Name, field.Type, field.Volatile, field.Offset, leaves);
        }
        return new PortableView(layout.ViewName, fields);
    }

    private static RenderMember? Member(MemberModel member, string extension, GeneratorOptions options)
    {
        // Auto-property and included non-public storage keeps the same wire shapes as a public field; the mirror facts on the
        // payload decide how the emitters reach it.
        bool mirrored = member.Mirrored, property = member.Storage == MemberStorage.AutoProperty;
        switch (member)
        {
            case ScalarMember scalar when scalar.Raw && scalar.Builtin:
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, scalar.Scalar), ScalarKind.Struct, mirrored,
                    raw: true, builtin: true, property: property);
            case ScalarMember scalar:
                // An enum is stored as its underlying scalar; the binary emitters spell the enum itself, JSON casts through
                // the underlying keyword.
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.EnumType ?? scalar.Scalar,
                    scalar.EnumType ?? scalar.Scalar, scalar.Size, null!,
                    scalar.EnumType != null ? ScalarKind.Enum : FieldType(scalar.Scalar), mirrored,
                    underlying: scalar.Underlying, property: property);
            case TextMember:
                return new RenderMember(member.Name, RenderMemberKind.Text, "string", "string", 0, null!, ScalarKind.String,
                    mirrored, property: property);
            case CultureInfoMember culture:
                return new RenderMember(culture.Name, RenderMemberKind.CultureInfo, culture.Declared, culture.Declared, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, culture.Declared), ScalarKind.Other, mirrored,
                    property: property);
            case CollectionMember collection:
                return Collection(collection, extension, options);
            case DictionaryMember dictionary:
                return Dictionary(dictionary, extension);
            default:
                return null;
        }
    }

    /// <summary>Maps a sequence member onto the emitter's collection shapes.</summary>
    /// <param name="member">The sequence member.</param>
    /// <param name="extension">The assembly's extension container.</param>
    /// <param name="options">The project facts, which decide whether an element payload is covered.</param>
    /// <returns>The render member, or null when the family is outside the direct emitters.</returns>
    private static RenderMember? Collection(CollectionMember member, string extension, GeneratorOptions options)
    {
        bool mirrored = member.Mirrored, property = member.Storage == MemberStorage.AutoProperty;
        string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, member.DeclaredType);
        if (member.ForwardedElement != null || member.Kind == CollectionKind.Blocking && member.Scalar != null)
            return new RenderMember(member.Name, RenderMemberKind.ForwardedCollection, member.Element.GlobalName,
                member.DeclaredType, 0, formatter, ScalarKind.Other, mirrored, property: property);
        // A concurrent or immutable sequence publishes an owner per closed element type: a raw scalar or shared string
        // variant, or a packable element whose record body the owner expands in place.
        if (member.Kind is CollectionKind.Concurrent or CollectionKind.Immutable)
        {
            // The analysis group also covers families this renderer does not reproduce yet.
            SequenceFamily? family = SupportedFamily(member);
            if (family == null)
                return null;
            RenderMemberKind kind = family switch
            {
                SequenceFamily.ConcurrentQueue => RenderMemberKind.ConcurrentQueue,
                SequenceFamily.ImmutableStack => RenderMemberKind.ImmutableStack,
                SequenceFamily.ImmutableList => RenderMemberKind.ImmutableList,
                SequenceFamily.ImmutableHashSet => RenderMemberKind.ImmutableHashSet,
                SequenceFamily.ImmutableSortedSet => RenderMemberKind.ImmutableSortedSet,
                _ => RenderMemberKind.ImmutableArray,
            };
            // A packable element carries its declared spelling in Scalar, so the nested model decides the shape first.
            if (member.Nested != null)
                return PayloadElement(member, family.Value, formatter, mirrored, property, options);
            return member.Scalar == null ? null :
                new RenderMember(member.Name, kind, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, property: property);
        }
        if (member.Scalar == null)
            return null;
        switch (member.Kind)
        {
            case CollectionKind.Array:
                return new RenderMember(member.Name, RenderMemberKind.Array, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Array, mirrored, raw: member.Raw, formatterCall: member.FormatterCall, property: property);
            case CollectionKind.Stack:
                return new RenderMember(member.Name, RenderMemberKind.Stack, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, raw: true, property: property);
            case CollectionKind.SortedSet:
                return new RenderMember(member.Name, RenderMemberKind.SortedSet, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, raw: true, property: property);
            case CollectionKind.List:
            case CollectionKind.NestedList:
                // A list of key/value pairs writes each pair's key and value in one checkable loop.
                if (member.Shape is { EntryType: not null } pair)
                    return new RenderMember(member.Name, RenderMemberKind.PairList, member.Scalar, member.DeclaredType, 0, formatter,
                        ScalarKind.Other, mirrored, raw: true, property: property,
                        collection: new RenderCollection(pair.Key, pair.Value, pair.ValueText, entryType: pair.EntryType));
                // A list of lists writes both levels in place around the element's expanded record body. Its JSON write
                // keeps the list formatter call, so it is not a raw member.
                if (member.Shape is { ItemType: not null } nested)
                {
                    PayloadRenderModel? element = Element(member.Nested, options);
                    return element == null ? null :
                        new RenderMember(member.Name, RenderMemberKind.NestedList, member.Scalar, member.DeclaredType, 0, formatter,
                            ScalarKind.Other, mirrored, nested: element, property: property,
                            collection: new RenderCollection(null, null, false, itemType: nested.ItemType));
                }
                // A list of packable classes expands each element's record body in the loop.
                if (member.Nested != null)
                {
                    PayloadRenderModel? element = Element(member.Nested, options);
                    return element == null ? null :
                        new RenderMember(member.Name, RenderMemberKind.List, member.Scalar, member.DeclaredType, 0, formatter,
                            ScalarKind.List, mirrored, nested: element, property: property);
                }
                return new RenderMember(member.Name, RenderMemberKind.List, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.List, mirrored, property: property);
            default:
                return null;
        }
    }

    /// <summary>Maps a keyed member onto the emitter's dictionary or sorted-list shape.</summary>
    /// <param name="member">The keyed member.</param>
    /// <param name="extension">The assembly's extension container.</param>
    /// <returns>The render member, or null when the family is outside the direct emitters.</returns>
    private static RenderMember? Dictionary(DictionaryMember member, string extension)
    {
        bool mirrored = member.Mirrored, property = member.Storage == MemberStorage.AutoProperty;
        string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, member.DeclaredType);
        switch (member.Kind)
        {
            case DictionaryKind.SortedList when member.KeyType != null && member.ValueType != null:
                return new RenderMember(member.Name, RenderMemberKind.SortedList, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, raw: true, property: property,
                    collection: new RenderCollection(member.KeyType, member.ValueType, member.ValueText));
            case DictionaryKind.Dictionary when member.Sparse:
                return new RenderMember(member.Name, RenderMemberKind.SparseDictionary, member.DeclaredType, member.DeclaredType, 0,
                    formatter, ScalarKind.Other, mirrored, raw: true, property: property,
                    collection: new RenderCollection(member.KeyType!, member.ValueType!, false));
            case DictionaryKind.Dictionary when member.KeyText || member.KeyType != null:
                return new RenderMember(member.Name, RenderMemberKind.Dictionary, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, raw: true, property: property,
                    dictionary: new RenderDictionary(member.Pair, member.KeyText, member.ValueText, member.KeyType, member.ValueType,
                        member.DeclaredType, formatter));
            default:
                return null;
        }
    }

    /// <summary>Builds the render model of a list element, which the loop expands in place.</summary>
    /// <param name="model">The element payload model, or null.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The element render model, or null when the element is outside the expandable record shape.</returns>
    private static PayloadRenderModel? Element(TypeModel? model, GeneratorOptions options)
    {
        if (model == null)
            return null;
        PayloadRenderModel? element = Create(model, options);
        return element != null && Covered(element) ? element : null;
    }

    /// <summary>Accepts the expandable element record shapes the direct emitters write in place.</summary>
    /// <param name="element">The element render model.</param>
    /// <returns>Whether the element's complete record body expands in a loop.</returns>
    private static bool Covered(PayloadRenderModel element) => !element.IsStruct && element.Version == null &&
        element.ConstructorArguments == null && element.Mirror == null && !element.HasCallbacks && element.Rent == null &&
        !element.MixedRecord && element.Members.Length != 0 &&
        element.Members.All(member => !member.Mirrored && !(member.Kind == RenderMemberKind.Dictionary && member.Dictionary.Pair) &&
            member.Kind is RenderMemberKind.Fixed or RenderMemberKind.Text or RenderMemberKind.Object or RenderMemberKind.Array or
                RenderMemberKind.List or RenderMemberKind.Dictionary);

    /// <summary>Maps a payload-element concurrent or immutable sequence onto the emitter's sequence shape.</summary>
    /// <param name="member">The sequence member, whose nested model is the element payload.</param>
    /// <param name="family">The declared sequence family.</param>
    /// <param name="formatter">The sequence's generated formatter owner.</param>
    /// <param name="mirrored">Whether the member is reached through the layout mirror.</param>
    /// <param name="property">Whether the member is an auto-property.</param>
    /// <param name="options">The project facts, which decide whether the element payload is covered.</param>
    /// <returns>The render member, or null when the element's record body is outside the direct emitters.</returns>
    private static RenderMember? PayloadElement(CollectionMember member, SequenceFamily family, string formatter, bool mirrored,
        bool property, GeneratorOptions options)
    {
        PayloadRenderModel? element = SequenceElement(member.Nested, options);
        if (element == null)
            return null;
        RenderMemberKind kind = family switch
        {
            SequenceFamily.ConcurrentQueue => RenderMemberKind.ConcurrentQueue,
            SequenceFamily.ImmutableArray => RenderMemberKind.ImmutableArray,
            SequenceFamily.ImmutableStack => RenderMemberKind.ImmutableStack,
            SequenceFamily.ImmutableList => RenderMemberKind.ImmutableList,
            SequenceFamily.ImmutableHashSet => RenderMemberKind.ImmutableHashSet,
            SequenceFamily.ImmutableSortedSet => RenderMemberKind.ImmutableSortedSet,
            _ => RenderMemberKind.ImmutableList,
        };
        return new RenderMember(member.Name, kind, member.Scalar, member.DeclaredType, 0, formatter, ScalarKind.Other, mirrored,
            nested: element, property: property);
    }

    /// <summary>Resolves the sequence family a concurrent or immutable member declares.</summary>
    /// <param name="member">The sequence member.</param>
    /// <returns>The supported family, or null when the declared family, element form or string element is not reproduced.</returns>
    /// <remarks>The analysis group covers every concurrent and immutable collection, while the shared owners currently cover the
    /// queue, the array, the stack, the list, the hash set and the sorted set; the other families stay unmapped instead of
    /// rendering with the wrong storage protocol. The stack, the list and both sets currently support raw scalar elements only,
    /// so their string and packable elements stay unmapped.</remarks>
    internal static SequenceFamily? SupportedFamily(CollectionMember member)
    {
        string family = SequenceFamilyName(member.DeclaredType);
        if (member.Kind == CollectionKind.Concurrent)
            return family == "ConcurrentQueue" ? SequenceFamily.ConcurrentQueue : null;
        if (member.Kind != CollectionKind.Immutable)
            return null;
        if (family == "ImmutableArray")
            return SequenceFamily.ImmutableArray;
        if (member.Nested != null || member.Scalar == null || member.Scalar == "string")
            return null;
        return family switch
        {
            "ImmutableStack" => SequenceFamily.ImmutableStack,
            "ImmutableList" => SequenceFamily.ImmutableList,
            "ImmutableHashSet" => SequenceFamily.ImmutableHashSet,
            "ImmutableSortedSet" => SequenceFamily.ImmutableSortedSet,
            _ => null,
        };
    }

    /// <summary>Whether a concurrent or immutable sequence belongs to the families the sequence owners reproduce.</summary>
    /// <param name="member">The sequence member.</param>
    /// <returns>Whether the declared family is supported for the member's element form.</returns>
    internal static bool IsSupportedSequence(CollectionMember member) => SupportedFamily(member) != null;

    /// <summary>Extracts the simple name of a declared sequence family.</summary>
    /// <param name="declaredType">The declared spelling, such as <c>global::...ImmutableArray&lt;int&gt;</c>.</param>
    /// <returns>The family name without namespace or type arguments.</returns>
    private static string SequenceFamilyName(string declaredType)
    {
        string text = GeneratedNames.PayloadKey(declaredType);
        int generic = text.IndexOf('<');
        if (generic >= 0)
            text = text.Substring(0, generic);
        int dot = text.LastIndexOf('.');
        return dot < 0 ? text : text.Substring(dot + 1);
    }

    /// <summary>Builds the render model of a sequence's payload element.</summary>
    /// <param name="nested">The element's normalized model, or null.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The element render model, or null when the element's record body is not expanded by a sequence owner.</returns>
    /// <remarks>A sequence owner writes its elements in place with the reservation template, so only a covered element whose
    /// members are fixed fields and strings is reproduced; a nested object or collection element keeps the original pipeline.</remarks>
    internal static PayloadRenderModel? SequenceElement(TypeModel? nested, GeneratorOptions options)
    {
        PayloadRenderModel? element = Element(nested, options);
        if (element == null || !element.ProfilePayload ||
            !element.Members.All(member => member.Kind is RenderMemberKind.Fixed or RenderMemberKind.Text))
            return null;
        return element;
    }

    private static ScalarKind FieldType(string keyword) => keyword switch
    {
        "bool" => ScalarKind.Bool,
        "byte" => ScalarKind.Byte,
        "sbyte" => ScalarKind.SByte,
        "short" => ScalarKind.Short,
        "ushort" => ScalarKind.UShort,
        "int" => ScalarKind.Int,
        "uint" => ScalarKind.UInt,
        "long" => ScalarKind.Long,
        "ulong" => ScalarKind.ULong,
        "float" => ScalarKind.Float,
        "double" => ScalarKind.Double,
        "decimal" => ScalarKind.Decimal,
        "char" => ScalarKind.Char,
        _ => ScalarKind.Other,
    };
}
