using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Confirms that exact-profile proofs bind the actual referenced runtime rather than consumer-defined names.</summary>
internal static class ProvenWriteProfiles
{
    private static readonly ConditionalWeakTable<Compilation, StrongBox<bool>> Checks = new();
    /// <summary>Requires every generated profile identity to resolve to the same runtime assembly as the Writer.</summary>
    /// <param name="compilation">The unchanged consumer compilation.</param>
    /// <returns>Whether the built-in profile spellings have their established runtime semantics.</returns>
    internal static bool AreCanonical(Compilation compilation)
        => Checks.GetValue(compilation, static current => new StrongBox<bool>(Check(current))).Value;

    /// <summary>Checks profile identities once for an immutable compilation without retaining its symbols.</summary>
    /// <param name="compilation">The unchanged consumer compilation.</param>
    /// <returns>Whether every exact profile belongs to the referenced runtime.</returns>
    private static bool Check(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (writer == null || SymbolEqualityComparer.Default.Equals(writer.ContainingAssembly, compilation.Assembly)) return false;
        return new[] { "LuminPack.Option.LuminPackUtf16LengthOptions", "LuminPack.Option.LuminPackDefaultOptions",
                "LuminPack.Option.LuminPackFixedOptions`1" }
            .All(name => compilation.GetTypeByMetadataName(name) is { } profile &&
                SymbolEqualityComparer.Default.Equals(profile.ContainingAssembly, writer.ContainingAssembly));
    }
}
