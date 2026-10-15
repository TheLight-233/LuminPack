using System;
using System.Linq;
using System.Threading;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Retains exact mapper source, diagnostics, and registry text without mutable analysis models.</summary>
internal sealed class FrozenMapperEmission : IEquatable<FrozenMapperEmission>
{
    internal readonly FrozenAggregateResult Output;
    internal readonly string Registration;

    /// <summary>Captures one mapper's completed observable contribution.</summary>
    /// <param name="output">The exact source and diagnostic values.</param>
    /// <param name="registration">Its legacy aggregate registry block, including the original comment.</param>
    private FrozenMapperEmission(FrozenAggregateResult output, string registration) { Output = output; Registration = registration; }

    /// <summary>Analyzes and renders one automatic mapper on a dependency cache miss.</summary>
    /// <param name="type">The selected current source type.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>The complete immutable automatic-mapper contribution.</returns>
    internal static FrozenMapperEmission Automatic(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        Interlocked.Increment(ref IncrementalGenerationMetrics.AutomaticMapperBuilds);
        var output = new AggregateResultBuilder();
        var info = LuminAutoMapAnalyzer.Analyze(type, compilation);
        if (info == null || info.Pairs.Count == 0) return new FrozenMapperEmission(output.Freeze(), string.Empty);
        try
        {
            string source = LuminAutoMapCodeGenerator.GenerateAutoMapperClass(info, metadata);
            if (!string.IsNullOrEmpty(source)) output.AddSource(SafeFileName(info.SourceFullName) + "Mapper.g.cs", GeneratedSourceDocumentation.Complete(source, metadata.ParseOptions));
        }
        catch (Exception exception)
        {
            output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None,
                exception.GetType().FullName + ": " + exception.Message + " | " + (exception.StackTrace ?? string.Empty).Replace("\r", " ").Replace("\n", " ")));
        }
        string registration = "            // Auto: " + info.SourceFullName + Environment.NewLine + LuminAutoMapCodeGenerator.GenerateAutoRegistrationLines(info);
        return new FrozenMapperEmission(output.Freeze(), registration);
    }

    /// <summary>Analyzes and renders one manual mapper on a dependency cache miss.</summary>
    /// <param name="type">The selected current mapper type.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>The complete immutable manual-mapper contribution.</returns>
    internal static FrozenMapperEmission Manual(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        Interlocked.Increment(ref IncrementalGenerationMetrics.ManualMapperBuilds);
        var output = new AggregateResultBuilder();
        var info = LuminMapAnalyzer.Analyze(type, compilation);
        if (info.Methods.Count == 0) return new FrozenMapperEmission(output.Freeze(), string.Empty);
        try
        {
            string source = LuminMapCodeGenerator.GenerateMapperImpl(info, metadata);
            if (!string.IsNullOrEmpty(source)) output.AddSource(SafeFileName(info.ClassFullName) + ".Mapper.g.cs", GeneratedSourceDocumentation.Complete(source, metadata.ParseOptions));
        }
        catch (Exception exception)
        {
            output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, exception.ToString()));
        }
        string registration = "            // Manual: " + info.ClassFullName + Environment.NewLine + LuminMapCodeGenerator.GenerateManualRegistrationLines(info);
        return new FrozenMapperEmission(output.Freeze(), registration);
    }

    /// <summary>Preserves the existing mapper hint-name transformation exactly.</summary>
    /// <param name="name">The fully qualified source or mapper name.</param>
    /// <returns>The original safe hint-name prefix.</returns>
    private static string SafeFileName(string name) => name.Replace("global::", "").Replace(".", "_").Replace("<", "_").Replace(">", "_");

    /// <summary>Compares all observable per-declaration output and aggregate text.</summary>
    /// <param name="other">The previous contribution.</param>
    /// <returns>Whether downstream source production and registry projection can remain cached.</returns>
    public bool Equals(FrozenMapperEmission other) => ReferenceEquals(this, other) || other != null && Output.Equals(other.Output) && Registration == other.Registration;

    /// <summary>Compares an arbitrary object to this contribution.</summary>
    /// <param name="obj">The object being compared.</param>
    /// <returns>Whether the immutable contributions are equal.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenMapperEmission);

    /// <summary>Hashes the immutable registry contribution.</summary>
    /// <returns>A hash consistent with equality.</returns>
    public override int GetHashCode() => Registration.GetHashCode();
}
