using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Retains custom registration validation and exact source fragments without method or type symbols.</summary>
internal sealed class FrozenCustomRegistration
{
    internal readonly string Registration;
    internal readonly ImmutableArray<FrozenDiagnostic> Diagnostics;
    internal readonly ImmutableArray<FrozenDiagnostic> RegistrationDiagnostics;

    /// <summary>Captures the immutable output of one custom formatter's validation.</summary>
    /// <param name="registration">Its exact valid legacy registration block, or an empty string.</param>
    /// <param name="diagnostics">Frozen validation and registration-suppression diagnostics.</param>
    /// <param name="registrationDiagnostics">Deferred suppression diagnostics, preserving the legacy all-validation-first order.</param>
    private FrozenCustomRegistration(string registration, ImmutableArray<FrozenDiagnostic> diagnostics, ImmutableArray<FrozenDiagnostic> registrationDiagnostics)
    { Registration = registration; Diagnostics = diagnostics; RegistrationDiagnostics = registrationDiagnostics; }

    /// <summary>Runs unchanged custom method validation only for a dependency cache miss.</summary>
    /// <param name="type">The selected current custom formatter type.</param>
    /// <param name="metadata">The complete current generation settings.</param>
    /// <returns>Its immutable validation diagnostics and registry contribution.</returns>
    internal static FrozenCustomRegistration Build(INamedTypeSymbol type, MetaInfo metadata)
    {
        Interlocked.Increment(ref IncrementalGenerationMetrics.CustomRegistrationBuilds);
        var model = CustomFormatterAnalyzer.Analyze(type);
        var diagnostics = CustomFormatterAnalyzer.CollectDiagnostics(model).ToList();
        var registrationDiagnostics = ImmutableArray<FrozenDiagnostic>.Empty;
        string registration = string.Empty;
        if (!model.HasErrors && model.RegisterTier > 0)
        {
            if (!metadata.AllowUnsafe || metadata.RegisterMode == LuminPackRegisterMode.Disabled)
                registrationDiagnostics = ImmutableArray.Create(FrozenDiagnostic.From(Diagnostic.Create(DiagnosticDescriptors.CustomRegistrationSkippedNoUnsafe,
                    type.Locations.FirstOrDefault() ?? Location.None, type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))));
            else registration = CustomFormatterRegistrationGenerator.GenerateRegistrationBlock(model.TypeName, model.RegisterTier);
        }
        return new FrozenCustomRegistration(registration, diagnostics.Select(FrozenDiagnostic.From).ToImmutableArray(), registrationDiagnostics);
    }
}
