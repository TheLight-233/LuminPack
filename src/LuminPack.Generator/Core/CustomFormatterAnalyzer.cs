using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>
/// Validates user-written static formatter methods on [LuminPackable(GeneratorType.Custom)]
/// types. The generator emits no extension methods for Custom types; instead these methods are
/// automatically registered through LuminPackSerializer.Register&lt;T&gt;.
/// <para>
/// Register has two overloads and the method set must match one of them exactly:
/// binary (Serialize + Deserialize) or binary + JSON (also SerializeJson + DeserializeJson).
/// </para>
/// </summary>
internal static class CustomFormatterAnalyzer
{
    internal const string SerializeName = "Serialize";
    internal const string DeserializeName = "Deserialize";
    internal const string SerializeJsonName = "SerializeJson";
    internal const string DeserializeJsonName = "DeserializeJson";

    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    internal static string SerializeSignature(string typeName) =>
        $"static void {SerializeName}(ref global::LuminPack.Core.LuminPackWriter writer, in {typeName} value)";
    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    internal static string DeserializeSignature(string typeName) =>
        $"static void {DeserializeName}(ref global::LuminPack.Core.LuminPackReader reader, ref {typeName} value)";
    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    internal static string SerializeJsonSignature(string typeName) =>
        $"static void {SerializeJsonName}(ref global::LuminPack.Core.LuminPackJsonWriter writer, in {typeName} value)";
    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    internal static string DeserializeJsonSignature(string typeName) =>
        $"static void {DeserializeJsonName}(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {typeName} value)";

    /// <summary>
    /// Collects and validates every Custom formatter type declared in the compilation's own
    /// assembly. Returns the models plus the diagnostics to report.
    /// </summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="reportDiagnostic">The report diagnostic used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Analyzes  and records the resulting metadata or diagnostics.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    internal static CustomFormatterModel Analyze(INamedTypeSymbol type)
    {
        string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var model = new CustomFormatterModel(type, typeName);

        model.Serialize = FindMethod(type, SerializeName);
        model.Deserialize = FindMethod(type, DeserializeName);
        model.SerializeJson = FindMethod(type, SerializeJsonName);
        model.DeserializeJson = FindMethod(type, DeserializeJsonName);

        return model;
    }

    /// <summary>Finds the method in the supplied metadata.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static IMethodSymbol? FindMethod(INamedTypeSymbol type, string name)
    {
        // DeclaredAccessibility on the method itself is validated separately; resolve the
        // first member with the requested name regardless of accessibility so a private
        // mismatch produces an actionable diagnostic instead of a confusing "missing".
        return type.GetMembers(name)
            .OfType<IMethodSymbol>()
            .FirstOrDefault(static method => method.MethodKind == MethodKind.Ordinary);
    }

    /// <summary>Performs the collect diagnostics step for the current operation.</summary>
    /// <param name="model">The model used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

        model.HasErrors = anyError;
        return diagnostics;
    }

    /// <summary>Validates method before the operation continues.</summary>
    /// <param name="method">The method used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="expectedSignature">The expected signature used by this operation.</param>
    /// <param name="firstParameterType">The first parameter type used by this operation.</param>
    /// <param name="secondParameterType">The second parameter type used by this operation.</param>
    /// <param name="secondParameterRefKind">The second parameter ref kind used by this operation.</param>
    /// <param name="ownerDisplay">The owner display used by this operation.</param>
    /// <param name="ownerLocation">The owner location used by this operation.</param>
    /// <param name="diagnostics">The diagnostics used by this operation.</param>
    /// <param name="anyError">The any error used by this operation.</param>
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

        if (method.IsGenericMethod || !method.ReturnsVoid || method.Parameters.Length != 2 ||
            !ParameterMatches(method.Parameters[0], firstParameterType, RefKind.Ref) ||
            !ParameterMatches(method.Parameters[1], secondParameterType, secondParameterRefKind))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.CustomMethodSignatureMismatch, location, ownerDisplay, name, expectedSignature));
            anyError = true;
        }
    }

    /// <summary>Performs the parameter matches step for the current operation.</summary>
    /// <param name="parameter">The parameter used by this operation.</param>
    /// <param name="expectedTypeName">The expected type name used by this operation.</param>
    /// <param name="expectedRefKind">The expected ref kind used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool ParameterMatches(IParameterSymbol parameter, string expectedTypeName, RefKind expectedRefKind)
    {
        if (parameter.RefKind != expectedRefKind) return false;

        string actual = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return actual == expectedTypeName || actual == FullyQualifiedAlias(expectedTypeName);
    }

    /// <summary>Performs the fully qualified alias step for the current operation.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string FullyQualifiedAlias(string typeName)
        => typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : "global::" + typeName;

    /// <summary>Determines whether union attribute are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasUnionAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute");
    }

    /// <summary>Determines whether packable attribute are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasPackableAttribute(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
    }
}
