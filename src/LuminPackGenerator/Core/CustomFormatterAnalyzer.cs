using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Analysis result for one [LuminPackable(GeneratorType.Custom)] type.</summary>
internal sealed class CustomFormatterModel
{
    internal CustomFormatterModel(INamedTypeSymbol type, string typeName)
    {
        Type = type;
        TypeName = typeName;
    }

    internal INamedTypeSymbol Type { get; }

    /// <summary>global::-qualified type name usable in generated code.</summary>
    internal string TypeName { get; }

    internal IMethodSymbol? Serialize { get; set; }
    internal IMethodSymbol? Deserialize { get; set; }
    internal IMethodSymbol? SerializeJson { get; set; }
    internal IMethodSymbol? DeserializeJson { get; set; }
    internal IMethodSymbol? CalculateOffset { get; set; }

    internal bool HasErrors { get; set; }

    /// <summary>Register overload tier: 1 = binary, 2 = +JSON, 3 = +JSON+calculate. 0 = invalid.</summary>
    internal int RegisterTier
    {
        get
        {
            if (Serialize is null || Deserialize is null) return 0;
            if (CalculateOffset is not null) return 3;
            if (SerializeJson is not null && DeserializeJson is not null) return 2;
            return 1;
        }
    }
}

/// <summary>
/// Validates user-written static formatter methods on [LuminPackable(GeneratorType.Custom)]
/// types. The generator emits no extension methods for Custom types; instead these methods are
/// automatically registered through LuminPackSerializer.Register&lt;T&gt;.
/// <para>
/// Register has three overloads and the method set must match one of them exactly:
/// binary (Serialize + Deserialize), binary + JSON (also SerializeJson + DeserializeJson),
/// or binary + JSON + size (also CalculateOffset).
/// </para>
/// </summary>
internal static class CustomFormatterAnalyzer
{
    internal const string SerializeName = "Serialize";
    internal const string DeserializeName = "Deserialize";
    internal const string SerializeJsonName = "SerializeJson";
    internal const string DeserializeJsonName = "DeserializeJson";
    internal const string CalculateOffsetName = "CalculateOffset";

    internal static string SerializeSignature(string typeName) =>
        $"static void {SerializeName}(ref global::LuminPack.Core.LuminPackWriter writer, in {typeName} value)";
    internal static string DeserializeSignature(string typeName) =>
        $"static void {DeserializeName}(ref global::LuminPack.Core.LuminPackReader reader, ref {typeName} value)";
    internal static string SerializeJsonSignature(string typeName) =>
        $"static void {SerializeJsonName}(ref global::LuminPack.Core.LuminPackJsonWriter writer, in {typeName} value)";
    internal static string DeserializeJsonSignature(string typeName) =>
        $"static void {DeserializeJsonName}(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {typeName} value)";
    internal static string CalculateOffsetSignature(string typeName) =>
        $"static void {CalculateOffsetName}(ref global::LuminPack.Core.LuminPackEvaluator evaluator, in {typeName} value)";

    /// <summary>
    /// Collects and validates every Custom formatter type declared in the compilation's own
    /// assembly. Returns the models plus the diagnostics to report.
    /// </summary>
    internal static List<CustomFormatterModel> AnalyzeAll(
        Compilation compilation,
        Action<Diagnostic> reportDiagnostic)
    {
        var models = new List<CustomFormatterModel>();

        foreach (ProjectTypeData declared in CompilationTypeAnalysisCache.GetOrCreate(compilation).DeclaredTypes)
        {
            INamedTypeSymbol type = declared.Symbol;
            if (!HasPackableAttribute(type)) continue;
            if (TypeMetaChecker.CheckGeneratorType(type) != GeneratorType.Custom) continue;

            // Only types declared in this assembly get generated/registered here; Unity
            // references plug-in assemblies into every compilation and must not duplicate.
            if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) continue;

            var model = Analyze(type);
            models.Add(model);
            foreach (Diagnostic diagnostic in CollectDiagnostics(model))
            {
                reportDiagnostic(diagnostic);
            }
        }

        return models;
    }

    internal static CustomFormatterModel Analyze(INamedTypeSymbol type)
    {
        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var model = new CustomFormatterModel(type, typeName);

        model.Serialize = FindMethod(type, SerializeName);
        model.Deserialize = FindMethod(type, DeserializeName);
        model.SerializeJson = FindMethod(type, SerializeJsonName);
        model.DeserializeJson = FindMethod(type, DeserializeJsonName);
        model.CalculateOffset = FindMethod(type, CalculateOffsetName);

        return model;
    }

    private static IMethodSymbol? FindMethod(INamedTypeSymbol type, string name)
    {
        // DeclaredAccessibility on the method itself is validated separately; resolve the
        // first member with the requested name regardless of accessibility so a private
        // mismatch produces an actionable diagnostic instead of a confusing "missing".
        return type.GetMembers(name)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(static method => method.MethodKind == MethodKind.Ordinary);
    }

    internal static List<Diagnostic> CollectDiagnostics(CustomFormatterModel model)
    {
        var diagnostics = new List<Diagnostic>();
        INamedTypeSymbol type = model.Type;
        string display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        Location location = type.Locations.FirstOrDefault() ?? Location.None;
        bool anyError = false;

        if (type.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.CustomTypeMustNotBeGeneric, location, display));
            anyError = true;
        }

        if (type.IsAbstract || HasUnionAttribute(type))
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.CustomUnionNotSupported, location, display));
            anyError = true;
        }

        // Binary pair is mandatory and each member must be valid.
        ValidateMethod(model.Serialize, SerializeName, SerializeSignature(model.TypeName),
            "global::LuminPack.Core.LuminPackWriter", model.TypeName, RefKind.In,
            display, location, diagnostics, ref anyError);
        ValidateMethod(model.Deserialize, DeserializeName, DeserializeSignature(model.TypeName),
            "global::LuminPack.Core.LuminPackReader", model.TypeName, RefKind.Ref,
            display, location, diagnostics, ref anyError);

        bool hasSerializeJson = model.SerializeJson is not null;
        bool hasDeserializeJson = model.DeserializeJson is not null;
        if (hasSerializeJson || hasDeserializeJson)
        {
            // JSON registration is all-or-nothing.
            if (!hasSerializeJson)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CustomJsonMethodsIncomplete,
                    location, display, DeserializeJsonName, SerializeJsonName));
                anyError = true;
            }
            if (!hasDeserializeJson)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CustomJsonMethodsIncomplete,
                    location, display, SerializeJsonName, DeserializeJsonName));
                anyError = true;
            }
        }

        ValidateMethod(model.SerializeJson, SerializeJsonName, SerializeJsonSignature(model.TypeName),
            "global::LuminPack.Core.LuminPackJsonWriter", model.TypeName, RefKind.In,
            display, location, diagnostics, ref anyError);
        ValidateMethod(model.DeserializeJson, DeserializeJsonName, DeserializeJsonSignature(model.TypeName),
            "global::LuminPack.Core.LuminPackJsonReader", model.TypeName, RefKind.Ref,
            display, location, diagnostics, ref anyError);
        ValidateMethod(model.CalculateOffset, CalculateOffsetName, CalculateOffsetSignature(model.TypeName),
            "global::LuminPack.Core.LuminPackEvaluator", model.TypeName, RefKind.In,
            display, location, diagnostics, ref anyError);

        if (model.CalculateOffset is not null && !(hasSerializeJson && hasDeserializeJson))
        {
            // Register's third overload is binary + JSON + calculate; without the JSON pair
            // there is no overload that accepts CalculateOffset.
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.CustomCalculateOffsetRequiresJson, location, display));
            anyError = true;
        }

        model.HasErrors = anyError;
        return diagnostics;
    }

    private static void ValidateMethod(
        IMethodSymbol? method,
        string name,
        string expectedSignature,
        string firstParameterType,
        string secondParameterType,
        RefKind secondParameterRefKind,
        string ownerDisplay,
        Location ownerLocation,
        List<Diagnostic> diagnostics,
        ref bool anyError)
    {
        if (method is null)
        {
            // Only report "missing" for mandatory methods; optional ones are validated by
            // the pair/tier rules above.
            if (name is SerializeName or DeserializeName)
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CustomMissingMethod,
                    ownerLocation,
                    ownerDisplay,
                    name,
                    expectedSignature));
                anyError = true;
            }
            return;
        }

        Location location = method.Locations.FirstOrDefault() ?? ownerLocation;

        if (!method.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.CustomMethodMustBeStatic, location, ownerDisplay, name));
            anyError = true;
        }

        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.CustomMethodMustBeAccessible, location, ownerDisplay, name));
            anyError = true;
        }

        if (!method.ReturnsVoid || method.Parameters.Length != 2 ||
            !ParameterMatches(method.Parameters[0], firstParameterType, RefKind.Ref) ||
            !ParameterMatches(method.Parameters[1], secondParameterType, secondParameterRefKind))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.CustomMethodSignatureMismatch, location, ownerDisplay, name, expectedSignature));
            anyError = true;
        }
    }

    private static bool ParameterMatches(IParameterSymbol parameter, string expectedTypeName, RefKind expectedRefKind)
    {
        if (parameter.RefKind != expectedRefKind) return false;

        string actual = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return actual == expectedTypeName || actual == FullyQualifiedAlias(expectedTypeName);
    }

    private static string FullyQualifiedAlias(string typeName)
        => typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : "global::" + typeName;

    private static bool HasUnionAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute");
    }

    private static bool HasPackableAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
    }
}

/// <summary>Emits the module initializer that registers every valid Custom formatter type.</summary>
internal static class CustomFormatterRegistrationGenerator
{
    /// <summary>
    /// Generates the registration source for the valid Custom models, or an empty string when
    /// there is nothing to register.
    /// </summary>
    internal static string Generate(
        IReadOnlyList<CustomFormatterModel> models,
        Compilation compilation,
        MetaInfo metaInfo,
        bool isUnity)
    {
        var registerable = models.Where(static model => !model.HasErrors && model.RegisterTier > 0).ToArray();
        if (registerable.Length == 0)
        {
            return string.Empty;
        }

        string asmRaw = compilation.AssemblyName ?? "Unknown";
        string asmSafe = SanitizeId(asmRaw);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminPack Custom formatter registration — do not modify");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack;");
        sb.AppendLine();

        bool hasNs = asmSafe != "Unknown";
        if (hasNs) { sb.AppendLine($"namespace LuminPackRegisters.{asmSafe}"); sb.AppendLine("{"); }

        sb.AppendLine("    internal static unsafe class LuminPackCustomFormatterRegistry");
        sb.AppendLine("    {");

        if (isUnity)
        {
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("        [global::UnityEditor.InitializeOnLoadMethod]");
            sb.AppendLine("#endif");
            sb.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
        }
        else
        {
            sb.AppendLine("#if NET5_0_OR_GREATER");
            sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializerAttribute]");
            sb.AppendLine("#else");
            sb.AppendLine("        // 需要手动调用初始化");
            sb.AppendLine($"        // 请在程序入口调用 LuminPackRegisters.{asmSafe}.LuminPackCustomFormatterRegistry.Initialize()");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("        internal static void Initialize()");
        sb.AppendLine("        {");

        foreach (CustomFormatterModel model in registerable)
        {
            string type = model.TypeName;
            sb.AppendLine($"            global::LuminPack.LuminPackSerializer.Register<{type}>(");
            sb.AppendLine($"                &{type}.{CustomFormatterAnalyzer.SerializeName},");
            sb.AppendLine($"                &{type}.{CustomFormatterAnalyzer.DeserializeName}");
            if (model.RegisterTier >= 2)
            {
                sb.AppendLine($"                , &{type}.{CustomFormatterAnalyzer.SerializeJsonName}");
                sb.AppendLine($"                , &{type}.{CustomFormatterAnalyzer.DeserializeJsonName}");
            }
            if (model.RegisterTier >= 3)
            {
                sb.AppendLine($"                , &{type}.{CustomFormatterAnalyzer.CalculateOffsetName}");
            }
            sb.AppendLine("            );");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    private static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0) sb.Append(char.IsLetter(c) || c == '_' ? c : '_');
            else sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.ToString();
    }
}
