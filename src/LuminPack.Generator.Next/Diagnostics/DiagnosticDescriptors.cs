using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// The identifiers mirror the legacy generator and are not tracked through AnalyzerReleases files.
#pragma warning disable RS2008

namespace LuminPack.SourceGenerator.Next.Diagnostics;

/// <summary>The declaration diagnostics the Next pipeline reports.</summary>
/// <remarks>The identifiers, titles, categories and messages are the legacy generator's descriptors, so a consumer observes the
/// same diagnostics when it switches pipelines. The set grows as more of the legacy validation is ported; shapes the renderers do
/// not cover yet stay silent instead of warning.</remarks>
internal static class DiagnosticDescriptors
{
    private const string Category = "LuminPack.SourceGeneration";

    /// <summary>A static type cannot be a payload.</summary>
    internal static readonly DiagnosticDescriptor StaticClass = Error("LuminPack001",
        "Static type is not serializable", "Type '{0}' is static and cannot be annotated with [LuminPackable].");

    /// <summary>A type cannot declare more serialized members than the binary object format stores.</summary>
    internal static readonly DiagnosticDescriptor MembersCountOver250 = Error("LuminPack018",
        "Too many serialized members",
        "Type '{0}' has {1} serialized members; the binary object format supports at most 249.");

    /// <summary>A ref struct cannot be stored across a generated operation.</summary>
    internal static readonly DiagnosticDescriptor TypeIsRefStruct = Error("LuminPack019",
        "ref struct is not serializable", "Type '{0}' is a ref struct and cannot be serialized.");

    /// <summary>A payload class needs a public constructor.</summary>
    internal static readonly DiagnosticDescriptor NoPublicConstructor = Error("LuminPack027",
        "No public constructor", "Type '{0}' has no public constructor.");

    /// <summary>A Union root cannot declare more cases than its tag surface.</summary>
    internal static readonly DiagnosticDescriptor TooManyUnionMembers = Error("LuminPack017",
        "Too many union members",
        "Union type '{0}' has {1} derived types, exceeding the 256-member limit.");

    /// <summary>An unexpected failure keeps a reserved high number.</summary>
    internal static readonly DiagnosticDescriptor GeneratorFailure = Error("LuminPack099",
        "Source generation failed", "LuminPack source generation failed: {0}");

    /// <summary>Rebuilds the Roslyn diagnostic of one captured declaration diagnostic.</summary>
    /// <param name="info">The captured diagnostic.</param>
    /// <returns>The diagnostic the pipeline reports.</returns>
    internal static Diagnostic Create(DiagnosticInfo info)
        => Diagnostic.Create(Descriptor(info.Id), Location(info), info.Arguments ?? System.Array.Empty<string>());

    /// <summary>Finds the descriptor of a captured identifier.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <returns>The matching descriptor.</returns>
    private static DiagnosticDescriptor Descriptor(string id) => id switch
    {
        "LuminPack001" => StaticClass,
        "LuminPack017" => TooManyUnionMembers,
        "LuminPack018" => MembersCountOver250,
        "LuminPack019" => TypeIsRefStruct,
        "LuminPack027" => NoPublicConstructor,
        _ => GeneratorFailure,
    };

    /// <summary>Rebuilds the location of a captured declaration diagnostic.</summary>
    /// <param name="info">The captured diagnostic.</param>
    /// <returns>The source location, or none.</returns>
    private static Location Location(DiagnosticInfo info) => !info.HasLocation
        ? Microsoft.CodeAnalysis.Location.None
        : Microsoft.CodeAnalysis.Location.Create(info.FilePath!, new TextSpan(info.SpanStart, info.SpanLength),
            new LinePositionSpan(new LinePosition(info.StartLine, info.StartCharacter),
                new LinePosition(info.EndLine, info.EndCharacter)));

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
