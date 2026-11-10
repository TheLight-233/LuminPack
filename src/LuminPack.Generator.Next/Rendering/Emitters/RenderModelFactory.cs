using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Maps a Next payload model onto the render model the direct emitters consume.</summary>
/// <remarks>This adapter is the only place where the normalized <see cref="TypeModel"/> facts become emitter input. It covers
/// ordinary records whose members are public fields of a fixed scalar, text, a supported sequence or a supported keyed family.
/// Features whose rendering facts are not modeled yet return null so the pipeline emits nothing instead of partial code.</remarks>
internal static class RenderModelFactory
{
    /// <summary>Builds the render model of one payload.</summary>
    /// <param name="model">The normalized payload.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render model, or null when this adapter does not cover the payload yet.</returns>
    internal static PayloadRenderModel? Create(TypeModel model, GeneratorOptions options)
    {
        bool isStruct = model is StructModel { Storage: StructStorage.RawUnmanaged, Layout: null };
        if (!isStruct && model is not RecordModel { Versioned: false, Derived: false })
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
        string globalName = model.Identity.GlobalName;
        // The proven object write needs .NET 8, unsafe code and a non-Unity project. The named member write only needs every
        // variable member to be a string, so a project outside those facts still publishes it.
        bool proven = model is RecordModel { Proven: true };
        bool memberWrite = model is RecordModel { Versioned: false, Derived: false } && model.Policy.Bound == null &&
            model.Members.Length != 0 && model.Members.Any(member => member is TextMember) &&
            model.Members.All(member => member is ScalarMember or TextMember);
        return new PayloadRenderModel(globalName, ReadableTypeName.Of(GeneratedNames.PayloadKey(globalName)), extension,
            GeneratedNames.JsonHelperName("Json_" + GeneratedNames.Sanitize(globalName)), freshResult: false,
            uniqueEntries: options.Interceptors, members, proven, mirror: null!, mirrorFields: null!, derived: false,
            isStruct, view: null!, viewElided: false, fixedUnionCase: false, measureRecord: isStruct && members.All(item => item.IsFixed),
            constructorArguments: null, constructorMembers: null, memberWrite: memberWrite, net8: options.Net8,
            utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: null!,
            referencePayload: model.Members.Any(item => item is not ScalarMember), layoutAttribute: null!,
            bounded: model.Policy.Bound, typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints,
            trusted: model.Policy.Input == InputPolicy.Trusted);
    }

    private static RenderMember? Member(MemberModel member, string extension, GeneratorOptions options)
    {
        if (member.Mirrored)
            return null;
        switch (member)
        {
            case ScalarMember scalar when scalar.Raw && scalar.Builtin:
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, 0,
                    "global::LuminPack." + GeneratedNames.Owner(extension, scalar.Scalar), ScalarKind.Struct, mirrored: false,
                    raw: true, builtin: true);
            case ScalarMember scalar:
                return new RenderMember(scalar.Name, RenderMemberKind.Fixed, scalar.Scalar, scalar.Scalar, scalar.Size, null!,
                    FieldType(scalar.Scalar), mirrored: false);
            case TextMember:
                return new RenderMember(member.Name, RenderMemberKind.Text, "string", "string", 0, null!, ScalarKind.String,
                    mirrored: false);
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
        if (member.Scalar == null)
            return null;
        string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, member.DeclaredType);
        switch (member.Kind)
        {
            case CollectionKind.Array:
                return new RenderMember(member.Name, RenderMemberKind.Array, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Array, mirrored: false, raw: member.Raw, formatterCall: member.FormatterCall);
            case CollectionKind.Stack:
                return new RenderMember(member.Name, RenderMemberKind.Stack, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored: false, raw: true);
            case CollectionKind.SortedSet:
                return new RenderMember(member.Name, RenderMemberKind.SortedSet, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored: false, raw: true);
            case CollectionKind.List:
            case CollectionKind.NestedList:
                // A list of key/value pairs writes each pair's key and value in one checkable loop.
                if (member.Shape is { EntryType: not null } pair)
                    return new RenderMember(member.Name, RenderMemberKind.PairList, member.Scalar, member.DeclaredType, 0, formatter,
                        ScalarKind.Other, mirrored: false, raw: true,
                        collection: new RenderCollection(pair.Key, pair.Value, pair.ValueText, entryType: pair.EntryType));
                // A list of lists writes both levels in place around the element's expanded record body. Its JSON write
                // keeps the list formatter call, so it is not a raw member.
                if (member.Shape is { ItemType: not null } nested)
                {
                    PayloadRenderModel? element = Element(member.Nested, options);
                    return element == null ? null :
                        new RenderMember(member.Name, RenderMemberKind.NestedList, member.Scalar, member.DeclaredType, 0, formatter,
                            ScalarKind.Other, mirrored: false, nested: element,
                            collection: new RenderCollection(null, null, false, itemType: nested.ItemType));
                }
                // A list of packable classes expands each element's record body in the loop.
                if (member.Nested != null)
                {
                    PayloadRenderModel? element = Element(member.Nested, options);
                    return element == null ? null :
                        new RenderMember(member.Name, RenderMemberKind.List, member.Scalar, member.DeclaredType, 0, formatter,
                            ScalarKind.List, mirrored: false, nested: element);
                }
                return new RenderMember(member.Name, RenderMemberKind.List, member.Scalar, member.DeclaredType, 0, formatter,
                    ScalarKind.List, mirrored: false);
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
        string formatter = "global::LuminPack." + GeneratedNames.Owner(extension, member.DeclaredType);
        switch (member.Kind)
        {
            case DictionaryKind.SortedList when member.KeyType != null && member.ValueType != null:
                return new RenderMember(member.Name, RenderMemberKind.SortedList, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored: false, raw: true,
                    collection: new RenderCollection(member.KeyType, member.ValueType, member.ValueText));
            case DictionaryKind.Dictionary when member.Sparse:
                return new RenderMember(member.Name, RenderMemberKind.SparseDictionary, member.DeclaredType, member.DeclaredType, 0,
                    formatter, ScalarKind.Other, mirrored: false, raw: true,
                    collection: new RenderCollection(member.KeyType!, member.ValueType!, false));
            case DictionaryKind.Dictionary when member.KeyText || member.KeyType != null:
                return new RenderMember(member.Name, RenderMemberKind.Dictionary, member.DeclaredType, member.DeclaredType, 0, formatter,
                    ScalarKind.Other, mirrored: false, raw: true,
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
