using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

/// <summary>Loads the model-based analyzer separately from the legacy analyzer with the same assembly and type names.</summary>
internal static class NextAnalyzer
{
    /// <summary>The model-based generator entry type.</summary>
    internal static Type EntryType { get; } = new AssemblyLoadContext(nameof(NextAnalyzer))
        .LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "next", "LuminPack.Generator.dll"))
        .GetType(typeof(LuminPackSourceGenerator).FullName!, throwOnError: true)!;

    /// <summary>Creates the generator for a fresh Roslyn driver.</summary>
    /// <returns>The model-based generator.</returns>
    internal static ISourceGenerator Create() => ((IIncrementalGenerator)Activator.CreateInstance(EntryType)!).AsSourceGenerator();
}
