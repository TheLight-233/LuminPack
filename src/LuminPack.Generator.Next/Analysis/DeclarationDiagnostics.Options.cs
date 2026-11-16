using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Diagnostics;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        private DiagnosticInfo[]? _assemblyDiagnostics;

        /// <summary>Validates assembly declarations once and shares the completed diagnostic snapshot.</summary>
        /// <returns>The captured assembly diagnostics.</returns>
        internal DiagnosticInfo[] DiagnoseAssembly()
        {
            lock (_gate)
            {
                if (_assemblyDiagnostics != null)
                    return _assemblyDiagnostics;
                var diagnostics = new DeclarationDiagnosticBag();
                ValidateCompiledOptions(_compilation.Assembly, ref diagnostics);
                _assemblyDiagnostics = diagnostics.ToArray();
                return _assemblyDiagnostics;
            }
        }

        /// <summary>Validates constants before any generated upper bound can rely on their encoding widths.</summary>
        /// <param name="symbol">An assembly, type, field or property declaration.</param>
        /// <param name="diagnostics">Receives errors at the original attribute syntax.</param>
        private static void ValidateCompiledOptions(ISymbol symbol, ref DeclarationDiagnosticBag diagnostics)
        {
            foreach (var attribute in symbol.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == OptionAttribute))
                foreach (var argument in attribute.NamedArguments)
                {
                    string? error = null;
                    if (argument.Key is "StringEncoding" or "StringRecording")
                    {
                        int value = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
                        if (value < 0 || value > (argument.Key == "StringRecording" ? 2 : 1))
                            error = "the value must be one of the declared encoding or recording enum members.";
                    }
                    if (argument.Key == "StandardFormat")
                    {
                        string? value = argument.Value.Value as string;
                        if (string.IsNullOrEmpty(value) || value.Length > 3 || value[0] > 127 || value.Length > 1 &&
                            (!byte.TryParse(value.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out byte precision) || precision > 99))
                            error = "use one ASCII format symbol followed by an optional precision from 0 through 99.";
                    }
                    if (argument.Key == "StructLayout" && Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture) is < 0 or > 2)
                        error = "the value must be a declared LuminPackStructLayout member.";
                    if (argument.Key == "StructLayout" && symbol is not (IAssemblySymbol or INamedTypeSymbol { TypeKind: TypeKind.Struct }))
                        error = "StructLayout applies only to assembly and struct declarations.";
                    if (argument.Key is "WideTag" or "SingleImplementation" && symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Interface })
                        error = "Union contract switches apply only to class and interface declarations.";
                    if (argument.Key == "ReuseRectangularArraySlots" && symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class })
                        error = "ReuseRectangularArraySlots applies only to the element class declaration.";
                    bool inputLimit = argument.Key is "MaxInputBytes" or "MaxDecompressedBytes" or "MaxAllocationBytes" or
                        "MaxCollectionLength" or "MaxStringLength" or "MaxObjectCount" or "MaxReadWork" or "MaxDepth";
                    if (inputLimit && Convert.ToInt64(argument.Value.Value, CultureInfo.InvariantCulture) < 0)
                        error = "input resource limits must be nonnegative.";
                    if (argument.Key == "MaxDepth" && Convert.ToInt64(argument.Value.Value, CultureInfo.InvariantCulture) < 1)
                        error = "external-input nesting depth must be at least one.";
                    if ((inputLimit || argument.Key == "UntrustedData") && symbol is not (IAssemblySymbol or INamedTypeSymbol))
                        error = "input policy applies to assembly and type declarations; nested values share their root budget.";
                    if (error != null)
                        diagnostics.Add(DiagnosticInfo.Capture(DiagnosticDescriptors.InvalidCompiledOption.Id,
                            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? symbol.Locations.FirstOrDefault(), new[] { argument.Key, error }));
                }
        }


        /// <summary>Checks a complete-record maximum before using it in generated capacity reservations.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <returns>Whether the declaration supplies a positive Int32 byte bound.</returns>
        private static bool ValidBound(INamedTypeSymbol type)
        {
            foreach (AttributeData attribute in type.GetAttributes())
                if (attribute.AttributeClass?.ToDisplayString() == BoundedAttribute)
                    return attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int value && value > 0;
            return true;
        }

        /// <summary>Checks packed storage before an unsupported field can fall back to raw bytes.</summary>
        /// <param name="type">The payload declaration.</param>
        /// <param name="diagnostics">Receives unsupported portable storage.</param>
        private static void ValidatePortableStorage(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            int? declared = ReadStructLayoutOption(type.GetAttributes());
            bool packed = type.TypeKind == TypeKind.Struct && (declared.HasValue ? declared.Value == 1 : type.IsUnmanagedType && LayoutStrategy(type) == 1);
            if (packed && (GenerationMode(type) != "Object" || !SupportedPortableStorage(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default))))
                diagnostics.Add(DiagnosticDescriptors.UnsupportedPortableStorage, type, type.ToDisplayString());
        }

        /// <summary>Validates generic portable storage without constructing closed native layout permutations.</summary>
        /// <param name="type">The field storage type.</param>
        /// <param name="visited">Storage nodes already checked in this declaration.</param>
        /// <returns>Whether every storage node has the legacy portable representation.</returns>
        private static bool SupportedPortableStorage(ITypeSymbol type, HashSet<ITypeSymbol> visited)
        {
            if (!visited.Add(type) || PortableScalarSize(type) != 0 || type is INamedTypeSymbol leaf && Opaque(leaf) != null)
                return true;
            if (type is ITypeParameterSymbol parameter)
                return parameter.HasUnmanagedTypeConstraint;
            if (type is not INamedTypeSymbol { IsUnmanagedType: true, TypeKind: TypeKind.Struct } named ||
                named.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr || IsRuntimeSizedVector(named) ||
                GenerationMode(named) == "Custom" || HasAttribute(named, "System.Runtime.CompilerServices.InlineArrayAttribute"))
                return false;
            PortablePlanField[]? fields = PlanFields(named, strict: true);
            return fields != null && fields.All(field => SupportedPortableStorage(field.Type, visited));
        }
    }
}
