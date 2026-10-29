using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>Builds concrete root operations, shared formatter operations, and generic dispatch for one consuming assembly.</summary>
public static partial class LuminPackExtensionGenerator
{
    /// <summary>Builds the inline operations for one serializable root.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
    {
        StringBuilder sb = new StringBuilder();
        string typeName = GetGlobalTypeName(data);
        if (data.isUnion && data.isValueType)
        {
            LuminPackUnionCodeGenerator.GenerateCaseMapStruct(data, sb);
        }

        // The root owns its full inline implementation. Shared non-root formatters are emitted
        // once by the assembly invocation pipeline, so multiple roots can reuse one state API.
        GenerateRootBinaryExtensions(sb, data, typeName, metaInfo);
        GenerateRootJsonExtensions(sb, data, typeName, metaInfo);
        // Local layout mirrors and accessors are partitioned into shared assembly files.

        return GenerateExtension(sb, GetExtensionClassName(compilation));
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="reachability">The reachability used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GenerateSerializerInvocationSupport(Compilation compilation, MetaInfo metaInfo, ReachabilityAnalysis? reachability = null)
    {
        var files = GenerateInvocationFiles(compilation, metaInfo, reachability, new InvocationSupportCache());
        var roots = files.Select(file => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(file.Text, metaInfo.ParseOptions).GetCompilationUnitRoot()).ToArray();
        var builder = new StringBuilder("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649" + Environment.NewLine);
        foreach (string directive in roots.SelectMany(root => root.Usings).Select(directive => directive.ToString()).Distinct(StringComparer.Ordinal))
            builder.AppendLine(directive);
        foreach (var root in roots)
            foreach (var member in root.Members) builder.AppendLine(member.ToFullString());
        return builder.ToString();
    }



    /// <summary>
    /// True when the type declares [LuminPackable(GeneratorType.Custom)]: user-written static
    /// methods are registered once and reached through generated Serializer state overloads.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool IsCustomGeneratorType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named &&
               HasPackableAttribute(named) &&
               TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Custom;
    }

    /// <summary>Obtains the extension class name for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetExtensionClassName(Compilation compilation)
    {
        return "LuminPackExtensions_" + SanitizeAssemblyName(compilation.AssemblyName ?? "Assembly");
    }

    /// <summary>
    /// Assembly-wide extension class name. The dedicated Local layout file and the per-type extension
    /// files must agree on it so the Locals are visible from every generated formatter.
    /// </summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    public static string GetExtensionClassNameFor(Compilation compilation)
    {
        return GetExtensionClassName(compilation);
    }

    /// <summary>Determines whether packable attribute are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasPackableAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
    }

    /// <summary>Performs the should aggressively inline binary write step for the current operation.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool ShouldAggressivelyInlineBinaryWrite(LuminLocalFieldData field)
    {
        if (field.TypeSymbol is IArrayTypeSymbol array)
        {
            if (array.Rank != 1)
            {
                return true;
            }

            // Keep the closed unmanaged bulk copy in its caller. Managed arrays contain
            // a loop plus null/growth slow paths, which are cheaper behind one call.
            return EmitterTypeTraits.CanUseUnmanagedSerialization(array.ElementType);
        }

        if (field.TypeSymbol is not INamedTypeSymbol named)
        {
            return true;
        }

        string namespaceName = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (namespaceName == "System.Collections.Generic" && named.Name == "Dictionary" &&
            named.TypeArguments.Length == 2)
        {
            return false;
        }

        if (namespaceName == "System.Collections.Generic" && named.Name == "List" &&
            named.TypeArguments.Length == 1)
        {
            return EmitterTypeTraits.CanUseUnmanagedSerialization(named.TypeArguments[0]);
        }

        return true;
    }

    /// <summary>Builds the root binary extensions source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateRootBinaryExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
    {
        AppendRootMethodHeader(sb, "WriteValue", "LuminPackWriter", typeName, data, metaInfo, read: false);
        GenerateMyselfSerialize(data, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
        AppendRootMethodHeader(sb, "ReadValue", "LuminPackReader", typeName, data, metaInfo, read: true);
        if (data.isUnion)
        {
            if (data.isValueType)
            {
                GenerateUnionDeserializeStruct(sb, data);
            }
            else
            {
                GenerateUnionDeserialize(data, sb, metaInfo);
            }
        }
        else
        {
            GenerateMyselfDeserialize(data, sb);
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        // A union header replaces the concrete object's normal object header. Keep the
        // original polymorphism path as a distinct generated overload so virtual union
        // dispatch never enters the normal root serializer/deserializer.
        if (!data.isUnion)
        {
            AppendRootMethodHeader(sb, "WritePolymorphismValue", "LuminPackWriter", typeName, data, metaInfo, read: false);
            GenerateMyselfSerialize(data, sb, polymorphism: true);
            sb.AppendLine("        }");
            sb.AppendLine();
            AppendRootMethodHeader(sb, "ReadPolymorphismValue", "LuminPackReader", typeName, data, metaInfo, read: true);
            GenerateMyselfDeserialize(data, sb, polymorphism: true);
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        GenerateRootCalculateOffsetExtension(sb, data, typeName, metaInfo);
        if (data.generatorType == GeneratorType.VersionTolerant && !data.isUnion)
            LuminPackVersionTolerantCodeGenerator.GenerateSchema(data, sb, metaInfo);
    }

    /// <summary>Rejects an exact binary width outside the generated direct-record contract.</summary>
    internal const string UnsupportedWidth =
        "global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Exact binary widths are evaluated only for direct records.\");";

    /// <summary>Builds the root calculate offset extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateRootCalculateOffsetExtension(
        StringBuilder sb,
        LuminDataInfo data,
        string typeName,
        MetaInfo metaInfo)
    {
        string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
        string nullable = data.isValueType ? string.Empty : "?";
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        public static void CalculateOffset" + genericParameters +
            "(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, " +
            (metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + nullable + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        // Only fixed Object records can be direct records; other layouts keep a callable but unsupported width.
        if (!data.isUnion && data.generatorType == GeneratorType.Object)
            LuminPackCodeGenerator.GenerateCalculateOffsetCode(data, sb);
        else
            sb.AppendLine("            " + UnsupportedWidth);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Builds the root method header source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    /// <param name="ioType">The io type used by this operation.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="read">The read used by this operation.</param>
    private static void AppendRootMethodHeader(StringBuilder sb, string methodName, string ioType, string typeName, LuminDataInfo data, MetaInfo metaInfo, bool read)
    {
        string parameters = string.Join(", ", data.GenericParameters);
        string nullable = data.isValueType ? string.Empty : "?";
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        string modifier = read ? "ref " : "in ";
        string scoped = metaInfo.IsNet8 ? "scoped " : string.Empty;
        sb.AppendLine("        public static void " + methodName + (data.isGeneric ? "<" + parameters + ">" : string.Empty) + "(ref this " + ioType + " " + (read ? "reader" : "writer") + ", " + scoped + modifier + typeName + nullable + " " + (read ? "value" : "input") + ")");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        if (!read)
        {
            sb.AppendLine("            var value = input;");
        }
    }

    /// <summary>Attempts to add analyzed type and reports whether it succeeded.</summary>
    /// <param name="analyzedTypes">The analyzed types used by this operation.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryAddAnalyzedType(HashSet<string> analyzedTypes, string typeName)
    {
        lock (analyzedTypes)
        {
            return analyzedTypes.Add(typeName);
        }
    }

    /// <summary>Builds the extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="extensionClassName">The extension class name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GenerateExtension(StringBuilder sb, string extensionClassName = "LuminPackExtensions")
    {
        string assemblySuffix = SanitizeAssemblyName(extensionClassName.StartsWith("LuminPackExtensions_", StringComparison.Ordinal)
                ? extensionClassName.Substring("LuminPackExtensions_".Length)
                : extensionClassName);
        string extensionType = "global::LuminPack.Generated.LuminPackExtensions_" + assemblySuffix;
        string value = sb.ToString()
            .Replace("global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in ", "writer.WriteValue(in ")
            .Replace("global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref ", "reader.ReadValue(ref ")
            .Replace("writer.WriteValueWithCompress(", extensionType + ".WriteValueWithCompress(ref writer, in ")
            .Replace("reader.ReadValueWithCompress(ref ", extensionType + ".ReadValueWithCompress(ref reader, ref ")
            .Replace("reader.ReadFreshValue(ref ", extensionType + ".ReadFreshValue(ref reader, ref ");
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("using global::System;");
        stringBuilder.AppendLine("using global::System.Collections.Generic;");
        stringBuilder.AppendLine("using global::System.Runtime.CompilerServices;");
        stringBuilder.AppendLine("using global::System.Runtime.InteropServices;");
        stringBuilder.AppendLine("using global::System.Threading.Tasks;");
        stringBuilder.AppendLine("using global::LuminPack;");
        stringBuilder.AppendLine("using global::LuminPack.Code;");
        stringBuilder.AppendLine("using global::LuminPack.Core;");
        stringBuilder.AppendLine("using global::LuminPack.Utility;");
        stringBuilder.AppendLine("using global::LuminPack.Attribute;");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649");
        stringBuilder.AppendLine("namespace LuminPack.Generated");
        stringBuilder.AppendLine("{");
        stringBuilder.AppendLine();
        // Formatter overloads are an assembly implementation detail. Keeping the partial
        // extension container internal prevents identical scalar overloads emitted by a
        // referenced contract assembly from participating in the consumer's overload set.
        stringBuilder.AppendLine("    internal static partial class " + extensionClassName);
        stringBuilder.AppendLine("    {");
        stringBuilder.Append(value);
        stringBuilder.AppendLine("    }");
        stringBuilder.AppendLine("}");
        return stringBuilder.ToString();
    }

    /// <summary>Builds the with compress extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="localData">The local data used by this operation.</param>
    /// <param name="compress">The compress used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateWithCompressExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, (Action<LuminLocalFieldData, StringBuilder> ser, Action<LuminLocalFieldData, StringBuilder> deser) compress, MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValueWithCompress(ref this LuminPackWriter writer, scoped in " + typeName + " value)") : ("        public static void WriteValueWithCompress(ref this LuminPackWriter writer, in " + typeName + " value)"));
        sb.AppendLine("        {");
        compress.ser(localData, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValueWithCompress(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadValueWithCompress(ref this LuminPackReader reader, ref " + typeName + " value)"));
        sb.AppendLine("        {");
        compress.deser(localData, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="localData">The local data used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateFreshListDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
    {
        bool inlineBulkRead = localData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol listType &&
                              listType.TypeArguments.Length == 1 &&
                              EmitterTypeTraits.CanUseUnmanagedSerialization(listType.TypeArguments[0]);
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(inlineBulkRead
            ? "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]"
            : "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
        sb.AppendLine("        {");
        ListEmitter.GenerateFreshDeserializeCode(localData, sb, metaInfo.IsNet8);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Determines whether the supplied value has the exact list type classification.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExactListType(string typeName)
    {
        if (typeName.EndsWith(">", StringComparison.Ordinal))
        {
            if (!typeName.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal))
            {
                return typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal);
            }
            return true;
        }
        return false;
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="localData">The local data used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateFreshDictionaryDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
        sb.AppendLine("        {");
        DictionaryEmitter.GenerateFreshDeserializeCode(localData, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="localData">The local data used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateFreshArrayDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
        sb.AppendLine("        {");
        ArrayEmitter.GenerateFreshDeserializeCode(localData, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Determines whether the supplied value has the exact managed array type classification.</summary>
    /// <param name="typeSymbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExactManagedArrayType(ITypeSymbol typeSymbol)
    {
        return typeSymbol is IArrayTypeSymbol { Rank: 1 } array &&
               !EmitterTypeTraits.CanUseUnmanagedSerialization(array.ElementType) && !ContainsTypeParameter(array.ElementType);
    }

    /// <summary>Determines whether the supplied value has the exact dictionary type classification.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExactDictionaryType(string typeName)
    {
        if (typeName.EndsWith(">", StringComparison.Ordinal))
        {
            if (!typeName.StartsWith("global::System.Collections.Generic.Dictionary<", StringComparison.Ordinal))
            {
                return typeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);
            }
            return true;
        }
        return false;
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    private static void GenerateMyselfSerialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        if (data.isUnion)
        {
            if (data.isValueType)
            {
                LuminPackUnionCodeGenerator.GenerateSerializeCodeStruct(data, sb);
            }
            else
            {
                LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
            }
            return;
        }
        switch (data.generatorType)
        {
            case GeneratorType.Object:
                LuminPackCodeGenerator.GenerateSerializeCode(data, sb, extension: true, polymorphism);
                break;
            case GeneratorType.CircleReference:
                LuminPackCircleReferenceCodeGenerator.GenerateSerializeCode(data, sb);
                break;
            case GeneratorType.VersionTolerant:
                LuminPackVersionTolerantCodeGenerator.GenerateSerializeCode(data, sb);
                break;
        }
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    private static void GenerateMyselfDeserialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        if (data.isUnion)
        {
            if (data.isValueType)
            {
                LuminPackUnionCodeGenerator.GenerateDeserializeCodeStruct(data, sb);
            }
            else
            {
                LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
            }
            return;
        }
        switch (data.generatorType)
        {
            case GeneratorType.Object:
                LuminPackCodeGenerator.GenerateDeserializeCode(data, sb, polymorphism);
                break;
            case GeneratorType.CircleReference:
                LuminPackCircleReferenceCodeGenerator.GenerateDeserializeCode(data, sb);
                break;
            case GeneratorType.VersionTolerant:
                LuminPackVersionTolerantCodeGenerator.GenerateDeserializeCode(data, sb);
                break;
        }
    }

    /// <summary>Performs the sanitize assembly name step for the current operation.</summary>
    /// <param name="assemblyName">The assembly name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static string SanitizeAssemblyName(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return "Unknown";
        }
        StringBuilder stringBuilder = new StringBuilder(assemblyName.Length);
        for (int i = 0; i < assemblyName.Length; i++)
        {
            char c = assemblyName[i];
            if (i == 0)
            {
                if (char.IsLetter(c) || c == '_')
                {
                    stringBuilder.Append(c);
                }
                else if (char.IsDigit(c))
                {
                    stringBuilder.Append('_').Append(c);
                }
                else
                {
                    stringBuilder.Append('_');
                }
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                stringBuilder.Append(c);
            }
            else
            {
                stringBuilder.Append('_');
            }
        }
        return stringBuilder.ToString();
    }

}
