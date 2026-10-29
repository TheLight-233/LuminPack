using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateUnionDeserialize(LuminDataInfo data, StringBuilder sb, MetaInfo metaInfo)
    {
        int maxTag = data.UnionMembers.Count == 0 ? 0 : data.UnionMembers.Max(static member => member.Id);
        sb.AppendLine(maxTag < 250 && !data.IsWideTag
            ? "            if (!reader.TryPeekUnionHeader(out var tag))"
            : "            if (!reader.TryPeekWideUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = ReadExistingUnion(ref reader, value, tag);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        AppendUnionDeserializeSwitch(data, sb, metaInfo, reuse: false);
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static " + data.classFullName + "? ReadExistingUnion(ref global::LuminPack.Core.LuminPackReader reader, " + data.classFullName + "? value, ushort tag)");
        sb.AppendLine("            {");
        AppendUnionDeserializeSwitch(data, sb, metaInfo, reuse: true);
        sb.AppendLine("            }");
    }

    /// <summary>Retains the original fresh Union arms and isolates seeded case reads in a separate body.</summary>
    /// <param name="data">The original Union contract.</param>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="metaInfo">The original registration and target settings.</param>
    /// <param name="reuse">Whether an existing reference case can seed its payload.</param>
    private static void AppendUnionDeserializeSwitch(LuminDataInfo data, StringBuilder sb, MetaInfo metaInfo, bool reuse)
    {
        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        var outlined = new List<(LuminUnionMemberInfo Member, string Type, bool Cast)>();
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetUnionMemberType(data, member);
            var compatibility = GetUnionGenericCompatibility(data, member);
            sb.AppendLine("                case " + member.Id + ":");
            sb.AppendLine("                {");
            if (!reuse && !member.Type.IsValueType && HasVariablePayload(member.Type))
            {
                // A variable-size case body reads in its own method, so one Union switch expanded into a
                // collection loop does not inline every case's string and collection reads into that loop.
                outlined.Add((member, memberType, compatibility.RequiresCast));
                sb.AppendLine("                    value = ReadUnionCase" + member.Id + "(ref reader);");
                sb.AppendLine("                    return;");
                sb.AppendLine("                }");
                continue;
            }
            sb.AppendLine("                    " + memberType + " member = " + (!reuse || member.Type.IsValueType
                ? "default!;"
                : "(object?)value is { } existing && existing.GetType() == typeof(" + memberType + ") ? global::System.Runtime.CompilerServices.Unsafe.As<" + memberType + ">(existing) : default!;"));
            sb.AppendLine("                    reader.ReadPolymorphismValue(ref member);");
            sb.AppendLine(compatibility.RequiresCast
                ? "                    value = (" + data.classFullName + ")(object)member;"
                : "                    value = member;");
            sb.AppendLine(reuse ? "                    return value;" : "                    return;");
            sb.AppendLine("                }");
        }
        if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
        {
            sb.AppendLine("                default:");
            sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
            sb.AppendLine(reuse ? "                    return value;" : "                    return;");
        }
        else
        {
            sb.AppendLine("                default:");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (" + data.classFullName + ".TryGetRegisteredFormatter(tag, out var __registered) && __registered.ReadValue != 0)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        " + data.classFullName + " __registeredValue = default!;");
            sb.AppendLine("                        unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackReader, ref " + data.classFullName + ", void>)__registered.ReadValue)(ref reader, ref __registeredValue); }");
            sb.AppendLine("                        value = __registeredValue;");
            sb.AppendLine(reuse ? "                        return value;" : "                        return;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
            sb.AppendLine(reuse ? "                    return value;" : "                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("            }");
        foreach (var (member, memberType, cast) in outlined)
        {
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine("            static " + data.classFullName + "? ReadUnionCase" + member.Id + "(ref global::LuminPack.Core.LuminPackReader reader)");
            sb.AppendLine("            {");
            sb.AppendLine("                " + memberType + " member = default!;");
            sb.AppendLine("                reader.ReadPolymorphismValue(ref member);");
            sb.AppendLine(cast ? "                return (" + data.classFullName + ")(object)member;" : "                return member;");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Identifies a Union case whose payload contains a string, collection or other reference-typed member.</summary>
    /// <param name="type">The concrete case.</param>
    /// <returns>Whether some instance field or property of the case or its bases is not unmanaged storage.</returns>
    /// <remarks>Only the shape of the fresh read changes: such a case is read by an outlined body instead of inline.</remarks>
    private static bool HasVariablePayload(INamedTypeSymbol type)
    {
        for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared) continue;
                if (member is IFieldSymbol field && !field.Type.IsUnmanagedType) return true;
                if (member is IPropertySymbol { IsIndexer: false } property && !property.Type.IsUnmanagedType) return true;
            }
        return false;
    }

    /// <summary>
    /// Root binary deserialize for a .NET 11 union struct: read the tag, read the case value with the
    /// concrete <c>ReadValue</c>, then reconstruct the union through its single-parameter case constructor.
    /// </summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    private static void GenerateUnionDeserializeStruct(StringBuilder sb, LuminDataInfo data)
    {
        int maxTag = data.UnionMembers.Count == 0 ? 0 : data.UnionMembers.Max(static member => member.Id);
        sb.AppendLine(maxTag < 250 && !data.IsWideTag
            ? "            if (!reader.TryPeekUnionHeader(out var tag))"
            : "            if (!reader.TryPeekWideUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetUnionMemberType(data, member);
            sb.AppendLine("                case " + member.Id + ":");
            sb.AppendLine("                {");
            sb.AppendLine("                    " + memberType + " member = default!;");
            sb.AppendLine("                    reader.ReadValue(ref member);");
            sb.AppendLine("                    value = new " + data.classFullName + "(member);");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
        sb.AppendLine("                    return;");
        sb.AppendLine("            }");
    }

    /// <summary>Builds the union json extensions source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateUnionJsonExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
    {
        string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
        string nullable = data.isValueType ? string.Empty : "?";
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, " + (metaInfo.IsNet8 ? "scoped in " : "in ") + typeName + nullable + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        LuminPackUnionCodeGenerator.GenerateSerializeJsonCode(data, sb, typeName);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, " + (metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + nullable + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.IsNull()) { value = default; return; }");
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            ushort tag = 0;");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) return;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String) continue;");
        JsonPropertyNameEmitter.Append(sb, "$type", "$value");
        sb.AppendLine("                if (!reader.Read()) break;");
        sb.AppendLine("                if (property == 1) { tag = (ushort)reader.ReadInt(); continue; }");
        sb.AppendLine("                if (property != 2) { reader.Skip(); continue; }");
        sb.AppendLine("                switch (tag)");
        sb.AppendLine("                {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetUnionMemberType(data, member);
            var compatibility = GetUnionGenericCompatibility(data, member);
            sb.AppendLine("                    case " + member.Id + ":");
            sb.AppendLine("                    {");
            sb.AppendLine("                        " + memberType + " member = (object?)value is " + memberType + " existing && value!.GetType() == typeof(" + memberType + ") ? existing : default!;");
            sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref member);");
            sb.AppendLine(compatibility.RequiresCast
                ? "                        value = (" + data.classFullName + ")(object)member;"
                : "                        value = member;");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
        }
        if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
        {
            sb.AppendLine("                    default: global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + ")); break;");
        }
        else
        {
            sb.AppendLine("                    default:");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (" + typeName + ".TryGetRegisteredFormatter(tag, out var __registered) && __registered.ReadValueJson != 0)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            " + typeName + " __registeredValue = default!;");
            sb.AppendLine("                            unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref " + typeName + ", void>)__registered.ReadValueJson)(ref reader, ref __registeredValue); }");
            sb.AppendLine("                            value = __registeredValue;");
            sb.AppendLine("                            break;");
            sb.AppendLine("                        }");
            sb.AppendLine("                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + "));");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
        }
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Builds the union json extensions struct source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateUnionJsonExtensionsStruct(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
    {
        string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, " + (metaInfo.IsNet8 ? "scoped in " : "in ") + typeName + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        LuminPackUnionCodeGenerator.GenerateSerializeJsonCodeStruct(data, sb, typeName);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, " + (metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.IsNull()) { value = default; return; }");
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            ushort tag = 0;");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) return;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String) continue;");
        JsonPropertyNameEmitter.Append(sb, "$type", "$value");
        sb.AppendLine("                if (!reader.Read()) break;");
        sb.AppendLine("                if (property == 1) { tag = (ushort)reader.ReadInt(); continue; }");
        sb.AppendLine("                if (property != 2) { reader.Skip(); continue; }");
        sb.AppendLine("                switch (tag)");
        sb.AppendLine("                {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetUnionMemberType(data, member);
            sb.AppendLine("                    case " + member.Id + ":");
            sb.AppendLine("                    {");
            sb.AppendLine("                        " + memberType + " member = default!;");
            sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref member);");
            sb.AppendLine("                        value = new " + data.classFullName + "(member);");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
        }
        sb.AppendLine("                    default: global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + ")); break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Obtains the union member type for the supplied input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="member">The member used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetUnionMemberType(LuminDataInfo data, LuminUnionMemberInfo member)
    {
        if (!member.Type.IsUnboundGenericType)
        {
            return FormatterTypeName.Get(member.Type);
        }

        string namespacePrefix = member.Type.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + member.Type.ContainingNamespace.ToDisplayString() + ".";
        return namespacePrefix + member.Type.Name + "<" + string.Join(", ", data.GenericParameters) + ">";
    }

    /// <summary>Obtains the union generic compatibility for the supplied input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="member">The member used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static (string Condition, bool RequiresCast) GetUnionGenericCompatibility(LuminDataInfo data, LuminUnionMemberInfo member)
    {
        if (!data.isGeneric || data.TypeSymbol is not INamedTypeSymbol root)
        {
            return (string.Empty, false);
        }

        INamedTypeSymbol matchedRoot = FindUnionRootMatch(member.Type, root.OriginalDefinition);
        if (matchedRoot is null || matchedRoot.TypeArguments.Length != data.GenericParameters.Count)
        {
            // Be conservative for a malformed/indirect generic union declaration: do not emit
            // an impossible pattern conversion against AbstractRoot<T>.
            return (string.Empty, true);
        }

        var conditions = new List<string>();
        for (int index = 0; index < matchedRoot.TypeArguments.Length; index++)
        {
            ITypeSymbol argument = matchedRoot.TypeArguments[index];
            if (argument is ITypeParameterSymbol parameter && parameter.Name == data.GenericParameters[index])
            {
                continue;
            }

            conditions.Add("typeof(" + data.GenericParameters[index] + ") == typeof(" +
                FormatterTypeName.Get(argument) + ")");
        }

        return (string.Join(" && ", conditions), conditions.Count != 0);
    }

    /// <summary>Finds the union root match in the supplied metadata.</summary>
    /// <param name="member">The member used by this operation.</param>
    /// <param name="root">The root used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static INamedTypeSymbol FindUnionRootMatch(INamedTypeSymbol member, INamedTypeSymbol root)
    {
        if (root.TypeKind == TypeKind.Interface)
        {
            return member.AllInterfaces.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, root));
        }

        for (INamedTypeSymbol current = member; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
            {
                return current;
            }
        }

        return null;
    }

}
