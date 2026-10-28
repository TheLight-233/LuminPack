using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Confirms that exact-profile proofs bind the actual referenced runtime rather than consumer-defined names.</summary>
internal static class ProvenWriteProfiles
{
    /// <summary>Requires every generated profile identity to resolve to the same runtime assembly as the Writer.</summary>
    /// <param name="compilation">The unchanged consumer compilation.</param>
    /// <returns>Whether the built-in profile spellings have their established runtime semantics.</returns>
    internal static bool AreCanonical(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (writer == null || SymbolEqualityComparer.Default.Equals(writer.ContainingAssembly, compilation.Assembly)) return false;
        return new[] { "LuminPack.Option.LuminPackUtf16LengthOptions", "LuminPack.Option.LuminPackDefaultOptions",
                "LuminPack.Option.LuminPackFixedOptions`1" }
            .All(name => compilation.GetTypeByMetadataName(name) is { } profile &&
                SymbolEqualityComparer.Default.Equals(profile.ContainingAssembly, writer.ContainingAssembly));
    }
}
