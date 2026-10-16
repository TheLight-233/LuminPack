using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{


    /// <summary>Obtains the ordered formatter types for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    internal static ITypeSymbol[] GetOrderedFormatterTypes(Compilation compilation)
        => GetOrderedFormatterTypes(compilation, reachability: null);

    /// <summary>Obtains the ordered formatter types for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="reachability">The reachability used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static ITypeSymbol[] GetOrderedFormatterTypes(Compilation compilation, ReachabilityAnalysis? reachability)
    {
        CompilationTypeAnalysis analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);

        // Full mode lists every formatter type in the project.  Prune tiers list exactly the emitted
        // set (emitted owned graphs plus the reachable no-owner complement); the generic dispatch
        // switch and the serializer entry overloads must reference exactly the types for which a
        // concrete extension method is emitted.
        IEnumerable<ITypeSymbol> types = reachability is null
            ? analysis.FormatterTypes
            : reachability.EmittedTypes;

        // Full mode walks every distinct symbol, so distinct symbols that canonicalize to the
        // same formatter name (native ints nint/IntPtr, nuint/UIntPtr, ValueTuple vs the
        // (a,b) syntax, or same-named types from different assemblies) must collapse to one
        // entry. Otherwise the LuminPackSerializer per-type overloads and the generic dispatch
        // would both emit identical signatures -> CS0001/CS0111 duplicate members.
        return types
            .Where(static type => !ContainsTypeParameter(type))
            .Where(IsAotVisible)
            .Where(type => !IsCustomGeneratorType(type) || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
            .Where(type => IsStaticFormatterCandidate(type, analysis))
            .OrderBy(static type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .GroupBy(static type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }


    /// <summary>Performs the can register formatter step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="json">The json used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool CanRegisterFormatter(ITypeSymbol type, Compilation compilation, bool json)
    {
        if (IsCustomGeneratorType(type))
        {
            var custom = CustomFormatterAnalyzer.Analyze((INamedTypeSymbol)type);
            return !json || custom.SerializeJson != null && custom.DeserializeJson != null;
        }
        if (type.TypeKind == TypeKind.Enum ||
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return true;
        }

        if (type is INamedTypeSymbol named && HasPackableAttribute(named))
        {
            return SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly);
        }

        var formatter = CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(type));
        return json
            ? formatter.WriteJson is not null && formatter.ReadJson is not null
            : formatter.Write is not null && formatter.Read is not null;
    }

    /// <summary>Performs the can register calculate formatter step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool CanRegisterCalculateFormatter(ITypeSymbol type, Compilation compilation)
    {
        if (IsCustomGeneratorType(type))
            return CustomFormatterAnalyzer.Analyze((INamedTypeSymbol)type).CalculateOffset != null;
        if (type.TypeKind == TypeKind.Enum ||
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return true;
        }

        if (type is INamedTypeSymbol named && HasPackableAttribute(named))
        {
            return SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly);
        }

        return EvaluatorEmitterRegistry.GetEmitter(FormatterTypeName.Get(type)) is not null;
    }

    /// <summary>Determines whether the supplied value has the static formatter candidate classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool IsStaticFormatterCandidate(ITypeSymbol type)
        => IsStaticFormatterCandidate(type, analysis: null);

    /// <summary>Determines whether a type has a generated or built-in formatter in the current compilation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="analysis">The compilation-local packable index, when available.</param>
    /// <returns>Whether a static formatter can be generated for the type.</returns>
    internal static bool IsStaticFormatterCandidate(ITypeSymbol type, CompilationTypeAnalysis analysis)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullable)
        {
            return IsStaticFormatterCandidate(nullable.TypeArguments[0], analysis);
        }

        // Arrays are only known when their leaf graph is known. Treating every
        // source-mentioned array as serializable made Unity package types such as
        // InputControl[] leak into generated formatter sets.
        if (type is IArrayTypeSymbol array)
        {
            return IsStaticFormatterCandidate(array.ElementType, analysis);
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }
        if (named.IsStatic)
        {
            return false;
        }

        bool isPackable = analysis?.IsPackable(named) ?? HasPackableAttribute(named);
        if (named.ContainingType is not null && !isPackable)
        {
            return false;
        }

        if (isPackable)
        {
            return true;
        }

        var formatter = CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(type));
        bool registered = formatter.Write is not null || formatter.Read is not null ||
            formatter.WriteJson is not null || formatter.ReadJson is not null;
        if (!registered)
        {
            return false;
        }

        // Registered generic formatters are templates. Emit an extension only for
        // a closed construction whose entire argument graph is itself known or
        // [LuminPackable].
        return !named.IsUnboundGenericType && named.TypeArguments.All(argument => IsStaticFormatterCandidate(argument, analysis));
    }

    /// <summary>Builds the formatter extensions source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="analyzedTypes">The analyzed types used by this operation.</param>
    /// <param name="emitOptionalVariants">The emit optional variants used by this operation.</param>
    private static void GenerateFormatterExtensions(
        StringBuilder sb,
        LuminLocalFieldData field,
        MetaInfo metaInfo,
        HashSet<string> analyzedTypes,
        bool emitOptionalVariants = true)
    {
        string typeName = field.TypeName;
        if (IsCustomGeneratorType(field.TypeSymbol))
        {
            // Custom state overloads are emitted once by the assembly-wide invocation partition.
            return;
        }
        bool isEnum = field.TypeSymbol?.TypeKind == TypeKind.Enum;
        bool isNullable = field.TypeSymbol is INamedTypeSymbol nullableSymbol &&
            nullableSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        if (isEnum)
        {
            if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
            {
                return;
            }

            (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter =
                (EnumEmitter.GenerateSerializeCode, EnumEmitter.GenerateDeserializeCode);
            (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) jsonFormatter =
                (EnumEmitter.GenerateJsonSerializeCode, EnumEmitter.GenerateJsonDeserializeCode);
            AppendBinaryFormatterExtension(sb, typeName, field, formatter, metaInfo);
            AppendJsonFormatterExtension(sb, typeName, field, jsonFormatter, metaInfo);
            AppendCalculateFormatterExtension(sb, typeName, field, EnumEmitter.GenerateCalculateOffsetCode, metaInfo);
            return;
        }

        if (isNullable)
        {
            if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
            {
                return;
            }

            (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter =
                (NullableEmitter.GenerateSerializeCode, NullableEmitter.GenerateDeserializeCode);
            (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) jsonFormatter =
                (NullableEmitter.GenerateJsonSerializeCode, NullableEmitter.GenerateJsonDeserializeCode);
            AppendBinaryFormatterExtension(sb, typeName, field, formatter, metaInfo);
            AppendJsonFormatterExtension(sb, typeName, field, jsonFormatter, metaInfo);
            AppendCalculateFormatterExtension(sb, typeName, field, NullableEmitter.GenerateCalculateOffsetCode, metaInfo);
            return;
        }

        var staticFormatter = CodeEmitterRegistry.GetEmitter(typeName);
        if ((staticFormatter.Write is null || staticFormatter.Read is null) &&
            (staticFormatter.WriteJson is null || staticFormatter.ReadJson is null))
        {
            return;
        }

        if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
        {
            return;
        }

        if (staticFormatter.Write is not null && staticFormatter.Read is not null)
        {
            AppendBinaryFormatterExtension(sb, typeName, field, (staticFormatter.Write, staticFormatter.Read), metaInfo);
            if (emitOptionalVariants)
            {
                var compressed = CodeEmitterRegistry.GetCompressEmitter(typeName);
                if (compressed.Item1 is not null && compressed.Item2 is not null)
                {
                    GenerateWithCompressExtension(sb, typeName, field, compressed, metaInfo);
                }
                if (IsExactListType(typeName))
                {
                    GenerateFreshListDeserializeExtension(sb, typeName, field, metaInfo);
                }
                else if (IsExactDictionaryType(typeName))
                {
                    GenerateFreshDictionaryDeserializeExtension(sb, typeName, field, metaInfo);
                }
                else if (IsExactManagedArrayType(field.TypeSymbol))
                {
                    GenerateFreshArrayDeserializeExtension(sb, typeName, field, metaInfo);
                }
            }
        }

        if (staticFormatter.WriteJson is not null && staticFormatter.ReadJson is not null)
        {
            AppendJsonFormatterExtension(sb, typeName, field, (staticFormatter.WriteJson, staticFormatter.ReadJson), metaInfo);
        }

        Action<LuminLocalFieldData, StringBuilder> evaluatorFormatter = EvaluatorEmitterRegistry.GetEmitter(typeName);
        if (evaluatorFormatter is not null)
        {
            AppendCalculateFormatterExtension(sb, typeName, field, evaluatorFormatter, metaInfo);
        }
    }

    /// <summary>Builds the binary formatter extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void AppendBinaryFormatterExtension(StringBuilder sb, string typeName, LuminLocalFieldData field,
        (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter,
        MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(ShouldAggressivelyInlineBinaryWrite(field)
            ? "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]"
            : "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void WriteValue(ref this LuminPackWriter writer, scoped in " + typeName + " value)"
            : "        public static void WriteValue(ref this LuminPackWriter writer, in " + typeName + " value)");
        sb.AppendLine("        {");
        formatter.Serialize(field, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void ReadValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)"
            : "        public static void ReadValue(ref this LuminPackReader reader, ref " + typeName + " value)");
        sb.AppendLine("        {");
        formatter.Deserialize(field, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Builds the calculate formatter extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void AppendCalculateFormatterExtension(
        StringBuilder sb,
        string typeName,
        LuminLocalFieldData field,
        Action<LuminLocalFieldData, StringBuilder> formatter,
        MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void CalculateOffset(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + typeName + " value)"
            : "        public static void CalculateOffset(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, ref " + typeName + " value)");
        sb.AppendLine("        {");
        formatter(field, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

}
