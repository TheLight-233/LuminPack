using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>Reads the project facts a model and its renderer need from the consumer compilation.</summary>
internal static class GeneratorOptionsFactory
{
    /// <summary>Captures the project facts.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The immutable project facts.</returns>
    internal static GeneratorOptions From(Compilation compilation)
    {
        var csharp = compilation as CSharpCompilation;
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        bool interceptors = false;
        foreach (var tree in compilation.SyntaxTrees)
            if (tree.Options is CSharpParseOptions parse)
            {
                foreach (string symbol in parse.PreprocessorSymbolNames)
                    symbols.Add(symbol);
                if (parse.Features.ContainsKey("InterceptorsNamespaces") || parse.Features.ContainsKey("InterceptorsPreviewNamespaces"))
                    interceptors = true;
            }
        bool net8 = symbols.Contains("NET8_0_OR_GREATER") || symbols.Contains("NET9_0_OR_GREATER") ||
            symbols.Contains("NET10_0_OR_GREATER");
        bool unity = symbols.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal));
        return new GeneratorOptions(net8, csharp == null ? 0 : (int)csharp.LanguageVersion, csharp?.Options.AllowUnsafe ?? false, unity,
            RegisterMode.Enabled, availableAliases: -1, compilation.AssemblyName ?? "Assembly", interceptors);
    }
}
