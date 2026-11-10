using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Direct;

/// <summary>Maps a Next payload model onto the render model the direct emitters consume.</summary>
/// <remarks>This adapter is the only place where the normalized <see cref="TypeModel"/> facts become emitter input. It covers
/// ordinary records whose members are public fields of a fixed scalar, text or a supported sequence. Features whose rendering
/// facts are not modeled yet return null so the pipeline emits nothing instead of partial code.</remarks>
internal static class DirectModelFactory
{
    /// <summary>Builds the render model of one payload.</summary>
    /// <param name="model">The normalized payload.</param>
    /// <param name="options">The project facts.</param>
    /// <returns>The render model, or null when this adapter does not cover the payload yet.</returns>
    internal static DirectTypeModel? Create(TypeModel model, GeneratorOptions options)
    {
        bool isStruct = model is StructModel { Storage: StructStorage.RawUnmanaged, Layout: null };
        if (!isStruct && model is not RecordModel { Versioned: false, Derived: false })
            return null;
        string extension = DirectNames.Extension(options.AssemblyName);
        var members = new DirectMember[model.Members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            DirectMember? member = Member(model.Members[index], extension);
            if (member == null)
                return null;
            members[index] = member;
        }
        string globalName = model.Identity.GlobalName;
        bool proven = model is RecordModel { Proven: true };
        return new DirectTypeModel(globalName, ReadableTypeName.Of(DirectNames.PayloadKey(globalName)), extension,
            DirectNames.JsonHelperName("Json_" + DirectNames.Sanitize(globalName)), freshResult: false,
            uniqueEntries: options.Interceptors, members, proven, mirror: null!, mirrorFields: null!, derived: false,
            isStruct, view: null!, viewElided: false, fixedUnionCase: false, measureRecord: isStruct && members.All(item => item.IsFixed),
            constructorArguments: null, constructorMembers: null, memberWrite: proven, net8: options.Net8,
            utf8Literals: options.Net8 && options.CSharpVersion > 1000, version: null!, referencePayload: false, layoutAttribute: null!,
            bounded: model.Policy.Bound, typeParameters: model.Identity.TypeParameters, typeConstraints: model.Constraints,
            trusted: model.Policy.Input == InputPolicy.Trusted);
    }

    private static DirectMember? Member(MemberModel member, string extension)
    {
        if (member.Mirrored)
            return null;
        switch (member)
        {
            case ScalarMember scalar when scalar.Raw && scalar.Builtin:
                return new DirectMember(scalar.Name, DirectMemberKind.Fixed, scalar.Scalar, scalar.Scalar, 0,
                    "global::LuminPack." + DirectNames.Owner(extension, scalar.Scalar), LuminFiledType.Struct, mirrored: false,
                    raw: true, builtin: true);
            case ScalarMember scalar:
                return new DirectMember(scalar.Name, DirectMemberKind.Fixed, scalar.Scalar, scalar.Scalar, scalar.Size, null!,
                    FieldType(scalar.Scalar), mirrored: false);
            case TextMember:
                return new DirectMember(member.Name, DirectMemberKind.Text, "string", "string", 0, null!, LuminFiledType.String,
                    mirrored: false);
            // Sequence members call the shared collection formatter owned by the formatter generator, which the Next project
            // does not publish yet. They stay out of the render model so the emitted record never references a missing entry.
            default:
                return null;
        }
    }

    private static LuminFiledType FieldType(string keyword) => keyword switch
    {
        "bool" => LuminFiledType.Bool,
        "byte" => LuminFiledType.Byte,
        "sbyte" => LuminFiledType.SByte,
        "short" => LuminFiledType.Short,
        "ushort" => LuminFiledType.UShort,
        "int" => LuminFiledType.Int,
        "uint" => LuminFiledType.UInt,
        "long" => LuminFiledType.Long,
        "ulong" => LuminFiledType.ULong,
        "float" => LuminFiledType.Float,
        "double" => LuminFiledType.Double,
        "decimal" => LuminFiledType.Decimal,
        "char" => LuminFiledType.Char,
        _ => LuminFiledType.Other,
    };
}
