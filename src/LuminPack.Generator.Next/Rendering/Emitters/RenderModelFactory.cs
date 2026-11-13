using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

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
    internal static PayloadRenderModel? Create(TypeModel model, GeneratorOptions options, bool json = false)
    {
        if (model is UnionModel union)
            return Union(union, options);
        if (model is CircleReferenceModel circle)
            return Circle(circle, options);
        if (model is StructModel { Versioned: true, Version: null })
            return null;
        bool raw = model is StructModel { Storage: StructStorage.RawUnmanaged, Layout: null } ||
            json && model is StructModel { StoragePlan: not null };
        // Managed value types retain record framing together with their construction, callback and storage facts.
        bool managed = model is StructModel { Storage: StructStorage.ManagedRecord } managedStruct &&
            (managedStruct.Mirror != null || managedStruct.Members.All(member => !member.Mirrored));
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
        if (record is { Versioned: true } versionedRecord && versionedRecord.Version == null)
            return null;
        StructModel? structure = model as StructModel;
        string? mirror = record?.Mirror ?? structure?.Mirror;
        if (record != null && mirror == null && record.Members.Any(member => member.Mirrored))
            return null;
        if (portable != null && !portable.Fields.All(field => field.Leaves.Length != 0))
            return null;
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
        if (!managed && !json && isStruct && !raw && !members.All(member => member.IsFixed))
            return null;
        string globalName = model.Identity.GlobalName;
        // The selected constructor, pool rent and serialization callbacks scale the record's construction and framing; the
        // direct emitters consume the same facts the legacy model states.
        ConstructorModel? constructor = record?.Constructor ?? structure?.Construction;
        CallbackModel? callbacks = record?.Callbacks ?? structure?.SerializationCallbacks;
        string? rent = record?.Rent ?? structure?.RentCall;
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
            uniqueEntries: options.Interceptors, members, proven, mirror: mirror!, mirrorFields: record?.MirrorFields ?? structure?.MirrorFields ?? null!,
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
            utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: Schema(record?.Version ?? structure?.Version),
            referencePayload: model.Members.Any(item => item is not ScalarMember),
            layoutAttribute: portable?.LayoutAttribute ?? record?.MirrorLayout ?? structure?.MirrorLayout,
            bounded: model.Policy.Bound, typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints,
            trusted: model.Policy.Input == InputPolicy.Trusted, portableView: portableView, mirrorOffsets: record?.MirrorOffsets ?? structure?.MirrorOffsets,
            managedStruct: managed, rent: rent!,
            writeBefore: callbacks?.WriteBefore!, writeAfter: callbacks?.WriteAfter!,
            readBefore: callbacks?.ReadBefore!, readAfter: callbacks?.ReadAfter!,
            declaredOrder: constructor?.DeclaredOrder!, rentByRef: structure?.RentByRef ?? false,
            declaredProfile: model.Policy.Declared ? ProfileNames.Type(model.Policy.Strings) : null,
            constantSpans: options.Net8 && options.CSharpVersion >= 1200, mirrors: record?.Mirrors!, rejection: record?.Rejection,
            limits: model.Policy.Input == InputPolicy.Untrusted ? model.Policy.Limits : null, recursive: record?.Recursive ?? false);
    }

    /// <summary>Projects the modeled schema onto the emitter's schema contract.</summary>
    /// <param name="schema">The analyzed schema, or null for an ordinary record.</param>
    /// <returns>The emitter schema, or null.</returns>
    private static VersionSchema? Schema(VersionSchemaModel? schema) => schema == null ? null :
        new VersionSchema(schema.Schema, schema.Metadata, schema.Prefix, schema.FieldEntries, schema.TempTypes, schema.TypeNames,
            schema.QualifiedTypes, schema.Layout, schema.LayoutFields, schema.OldNames, schema.Dynamic, schema.Widths);

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
        // A declared string profile selects the root's standard slot; every canonical slot is published either way.
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
                item.FixedCursor, caseModel!, item.Readable, item.Chain, item.Namespace!, item.Arguments, item.Definition);
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
            unionChain: model.Chain, unionNamespace: model.Namespace!, unionReferenced: model.Referenced,
            unionBinarySlots: Slots(model.BinarySlots), unionJsonSlots: Slots(model.JsonSlots),
            unionCanonical: model.Canonical, unionStandardBinary: model.StandardBinary, unionStandardJson: model.StandardJson,
            unionTier: (int)model.Tier, unionTierCases: model.TierCases, unionTierSealed: model.TierSealed, unionWide: model.WideTags,
            trusted: model.Policy.Input == InputPolicy.Trusted, registration: options.RegisterMode != RegisterMode.Disabled);
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
        // The reference-tracked renderer requires complete physical storage and the byte-sized object slot surface.
        if (model.MirrorFields == null || model.Identity.IsGeneric ||
            model.Members.Length > 249 || model.Slots.Any(slot => slot >= 249))
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
        string globalName = model.Identity.GlobalName;
        string readable = ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName));
        return new PayloadRenderModel(globalName, readable, extension,
            GeneratedNames.JsonHelperName("Json_" + GeneratedNames.Sanitize(globalName)), freshResult: false,
            uniqueEntries: options.Interceptors, members, proven: false, mirror: model.Mirrors.Length == 0 ? MirrorName(model, globalName) : model.Mirrors[model.Mirrors.Length - 1].Name,
            mirrorFields: model.MirrorFields, derived: false, isStruct: false, view: null!, viewElided: false,
            fixedUnionCase: false, measureRecord: false, constructorArguments: model.Construction?.Arguments!, constructorMembers: model.Construction?.Members!, memberWrite: false,
            net8: options.Net8, utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: null!,
            referencePayload: model.Members.Any(item => item is not ScalarMember), layoutAttribute: model.MirrorLayout,
            circle: true, circleGuarded: model.Guarded, mirrorOffsets: model.MirrorOffsets,
            typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints,
            rent: model.RentCall!, rentByRef: model.RentByRef, writeBefore: model.SerializationCallbacks?.WriteBefore!,
            writeAfter: model.SerializationCallbacks?.WriteAfter!, readBefore: model.SerializationCallbacks?.ReadBefore!,
            readAfter: model.SerializationCallbacks?.ReadAfter!, trusted: model.Policy.Input == InputPolicy.Trusted,
            declaredProfile: model.Policy.Declared ? ProfileNames.Type(model.Policy.Strings) : null,
            circleSlots: model.Slots, circleWidths: model.Widths, mirrors: model.Mirrors, bounded: model.Policy.Bound,
            limits: model.Policy.Input == InputPolicy.Untrusted ? model.Policy.Limits : null);
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
                    mirrored: member.Mirrored, nested: nested);
            default:
                return Member(member, extension, options);
        }
    }

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

    /// <summary>Projects member operations and their declaration-owned binary scope.</summary>
    /// <param name="member">The immutable member contract.</param>
    /// <param name="extension">The generated assembly container.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The complete member operation, or null.</returns>
    internal static RenderMember? Member(MemberModel member, string extension, GeneratorOptions options)
    {
        RenderMember? rendered = MemberCore(member, extension, options);
        if (rendered != null && member.Compressed)
            rendered = rendered.Compressed("global::LuminPack." + GeneratedNames.Owner(extension, rendered.TypeName));
        return rendered == null || !member.Policy.Declared ? rendered : rendered.WithScope(ProfileNames.Type(member.Policy.Strings));
    }

    /// <summary>Projects one member's storage and formatter operation.</summary>
    /// <param name="member">The immutable member contract.</param>
    /// <param name="extension">The generated assembly container.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The complete member operation, or null.</returns>
    private static RenderMember? MemberCore(MemberModel member, string extension, GeneratorOptions options)
    {
        // Auto-property and included non-public storage keeps the same wire shapes as a public field; the mirror facts on the
        // payload decide how the emitters reach it.
        bool mirrored = member.Mirrored, property = member.Storage == MemberStorage.AutoProperty;
        switch (member)
        {
            case TypeParameterMember parameter:
                return new RenderMember(parameter.Name, RenderMemberKind.Parameter, parameter.ParameterName,
                    parameter.ParameterName, 0, null!, ScalarKind.Other, mirrored, property: property,
                    valueTypeParameter: parameter.ValueType, bareParameter: parameter.ParameterName.IndexOf('<') < 0 &&
                        parameter.ParameterName.IndexOf('[') < 0, referencePayload: parameter.ReferenceType);
            case ScalarMember scalar when scalar.Raw && scalar.Builtin:
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, scalar.Scalar), ScalarKind.Struct, mirrored,
                    raw: true, builtin: true, property: property, isVolatile: scalar.Volatile);
            case ScalarMember scalar when scalar.Raw:
                // A raw declared struct is fixed storage of its runtime width; JSON writes it through the struct's owner.
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, scalar.Scalar), ScalarKind.Struct, mirrored,
                    raw: true, property: property, isVolatile: scalar.Volatile);
            case ScalarMember scalar:
                // An enum is stored as its underlying scalar; the binary emitters spell the enum itself, JSON casts through
                // the underlying keyword.
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.EnumType ?? scalar.Scalar,
                    scalar.EnumType ?? scalar.Scalar, scalar.Size, null!,
                    scalar.EnumType != null ? ScalarKind.Enum : FieldType(scalar.Scalar), mirrored,
                    underlying: scalar.Underlying, property: property, isVolatile: scalar.Volatile);
            case TextMember:
                return new RenderMember(member.Name, RenderMemberKind.Text, "string", "string", 0, null!, ScalarKind.String,
                    mirrored, property: property);
            case CultureInfoMember culture:
                return new RenderMember(culture.Name, RenderMemberKind.CultureInfo, culture.Declared, culture.Declared, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, culture.Declared), ScalarKind.Other, mirrored,
                    property: property);
            case SpecialMember special:
                return new RenderMember(special.Name, RenderMemberKind.Special, ReadableTypeName.Of(special.Declared), special.Declared, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, special.Declared), ScalarKind.Other, mirrored,
                    property: property, nullObjectGuard: special.Formatter?.OrdinaryObject == true, referencePayload: special.Nullable,
                    seedExisting: special.Formatter?.Payload is StructModel { Storage: StructStorage.ManagedRecord });
            case PayloadMember { Shape: PayloadShape.Inline, Nested: RecordModel { Derived: false } nested } payload
                when !mirrored && nested.Policy.Bound == null && Element(nested, options) is { } element:
                return new RenderMember(payload.Name, RenderMemberKind.Object, payload.Type.GlobalName, payload.Type.GlobalName,
                    0, "global::LuminPack." + element.Owner, ScalarKind.Other, mirrored, nested: element, property: property);
            case PayloadMember { Shape: PayloadShape.Inline, Nested: RecordModel nestedRecord } payload when Create(nestedRecord, options) is { } called:
            {
                // A nested record that cannot expand in place, such as one reached through mirrored storage, calls its own payload
                // owner, which writes the same null marker, member count and members.
                string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, payload.Type.GlobalName);
                return new RenderMember(payload.Name, RenderMemberKind.Special, ReadableTypeName.Of(payload.Type.GlobalName),
                    payload.Type.GlobalName, 0, formatter,
                    ScalarKind.Other, mirrored, property: property, referencePayload: true, nullObjectGuard: true);
            }
            case CollectionMember collection:
                return Collection(collection, extension, options);
            case DictionaryMember dictionary:
                return Dictionary(dictionary, extension, options);
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
        if (member.ForwardedElement is { } child && !CanForward(child, options))
            return null;
        if (member.ForwardedElement != null || member.ArrayRank > 1 && member.Scalar != null ||
            member.Kind is CollectionKind.Array or CollectionKind.List && GenericIdentity.NullableScalar(member.Scalar) != null ||
            member.Kind == CollectionKind.Blocking && member.Scalar != null)
            return new RenderMember(member.Name, RenderMemberKind.ForwardedCollection, member.Element.GlobalName,
                member.DeclaredType, 0, formatter, ScalarKind.Other, mirrored, property: property);
        // A concurrent or immutable sequence publishes an owner per closed element type: a raw scalar or shared string
        // variant, or a packable element whose record body the owner expands in place.
        if (FormatterRegistry.FindSequence(member)?.WriteMember != null)
        {
            // The analysis group also covers families this renderer does not reproduce yet.
            SequenceFormatter? family = SupportedFamily(member);
            if (family == null)
                return null;
            // A packable element carries its declared spelling in Scalar, so the nested model decides the shape first.
            if (member.Nested != null)
                return PayloadElement(member, family, formatter, mirrored, property, options);
            return member.Scalar == null ? null :
                new RenderMember(member.Name, RenderMemberKind.Sequence, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, property: property, sequence: family);
        }
        if (member.Scalar == null)
            return null;
        switch (member.Kind)
        {
            case CollectionKind.Array:
                return new RenderMember(member.Name, RenderMemberKind.Array, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Array, mirrored, raw: member.Raw, formatterCall: member.FormatterCall, property: property);
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
    private static RenderMember? Dictionary(DictionaryMember member, string extension, GeneratorOptions options)
    {
        bool mirrored = member.Mirrored, property = member.Storage == MemberStorage.AutoProperty;
        string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, member.DeclaredType);
        // A packable class value reads and writes through its dictionary owner when the element body is expandable.
        if (member.Nested != null)
            return FormatterRegistry.FindDictionary(member.DeclaredType) != null && SequenceElement(member.Nested, options) != null ?
                new RenderMember(member.Name, RenderMemberKind.ForwardedCollection, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, property: property) : null;
        // An enum or framework built-in entry is stored through the dictionary owner, whose entry store spells the type.
        if (member.ForwardedValue is { } child && !CanForward(child, options))
            return null;
        if (member.KeyOwned != null || member.ValueOwned != null || member.ForwardedValue != null)
            return FormatterRegistry.FindDictionary(member.DeclaredType) != null ?
                new RenderMember(member.Name, RenderMemberKind.ForwardedCollection, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, property: property) : null;
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
            // A registered paired family reads and writes through its shared owner.
            case DictionaryKind.SortedList or DictionaryKind.SortedDictionary or DictionaryKind.Concurrent or DictionaryKind.Immutable or DictionaryKind.ReadOnly or
                DictionaryKind.Frozen or DictionaryKind.Interface or DictionaryKind.Priority when (member.KeyText || member.KeyType != null) && (member.ValueText || member.ValueType != null) &&
                FormatterRegistry.FindDictionary(member.DeclaredType) != null:
                return new RenderMember(member.Name, RenderMemberKind.ForwardedCollection, member.DeclaredType, member.DeclaredType, 0,
                    formatter, ScalarKind.Other, mirrored, property: property);
            case DictionaryKind.Dictionary when member.KeyText || member.KeyType != null:
                return new RenderMember(member.Name, RenderMemberKind.Dictionary, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored, raw: true, property: property,
                    dictionary: new RenderDictionary(member.Pair, member.KeyText, member.ValueText, member.KeyType, member.ValueType,
                        member.DeclaredType, formatter));
            default:
                return null;
        }
    }

    /// <summary>Checks that each recursively referenced collection can publish its static owner.</summary>
    internal static bool CanForward(MemberModel member, GeneratorOptions options) => member switch
    {
        CollectionMember { ForwardedElement: { } child } => CanForward(child, options),
        CollectionMember { Nested: { } nested } => SequenceElement(nested, options) != null,
        DictionaryMember { ForwardedValue: { } child } => CanForward(child, options),
        _ => true,
    };

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
    private static RenderMember? PayloadElement(CollectionMember member, SequenceFormatter family, string formatter, bool mirrored,
        bool property, GeneratorOptions options)
    {
        PayloadRenderModel? element = SequenceElement(member.Nested, options);
        if (element == null)
            return null;
        return new RenderMember(member.Name, RenderMemberKind.Sequence, member.Scalar, member.DeclaredType, 0, formatter, ScalarKind.Other, mirrored,
            nested: element, property: property, sequence: family);
    }

    /// <summary>Resolves the sequence family a concurrent or immutable member declares.</summary>
    /// <param name="member">The sequence member.</param>
    /// <returns>The supported family, or null when the declared family, element form or string element is not reproduced.</returns>
    /// <remarks>The analysis group covers every concurrent and immutable collection, while the shared owners currently cover the
    /// concurrent queue, stack and bag, the immutable array, stack, list, queue, hash set and sorted set; the other families stay
    /// unmapped instead of rendering with the wrong storage protocol. The concurrent stack and bag and the immutable stack, list,
    /// queue and both sets currently support raw scalar elements only, so their string and packable elements stay unmapped.</remarks>
    internal static SequenceFormatter? SupportedFamily(CollectionMember member)
    {
        var family = FormatterRegistry.FindSequence(member);
        if (family == null)
            return null;
        if (member.ForwardedElement != null)
            return family.Forwarded == null ? null : family;
        if (member.Nested != null)
            return family.Payload == null ? null : family;
        if (member.Scalar == "string")
            return family.Text == null ? null : family;
        return member.Scalar == null ? null : family;
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
