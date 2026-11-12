using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>Reads the project facts a model and its renderer need from the consumer compilation.</summary>
internal static class GeneratorOptionsFactory
{
    private static readonly ConditionalWeakTable<Compilation, GeneratorOptions> Cache = new();

    /// <summary>Captures the project facts once per compilation.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The immutable project facts.</returns>
    /// <remarks>The per-payload transform calls this for every attributed type. The compilation is a weak key, so the
    /// options are shared by all payloads of one run without retaining a finished compilation.</remarks>
    internal static GeneratorOptions From(Compilation compilation) => Cache.GetValue(compilation, static item => Create(item));

    /// <summary>Reads the registration argument by enum identity across both supported assembly attribute constructors.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The declared registration mode.</returns>
    private static RegisterMode Registration(Compilation compilation)
    {
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute")
                foreach (TypedConstant argument in attribute.ConstructorArguments)
                    if (argument.Type?.ToDisplayString() == "LuminPack.Attribute.LuminPackRegisterMode" &&
                        Convert.ToInt32(argument.Value, System.Globalization.CultureInfo.InvariantCulture) == 1)
                        return RegisterMode.Disabled;
        return RegisterMode.Enabled;
    }

    /// <summary>Captures target, language, registration and entry-binding facts.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The immutable project facts.</returns>
    private static GeneratorOptions Create(Compilation compilation)
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
            symbols.Contains("NET10_0_OR_GREATER") || symbols.Contains("NET11_0_OR_GREATER");
        bool unity = symbols.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal)) ||
            compilation.GetTypeByMetadataName("UnityEngine.Application") != null || compilation.GetTypeByMetadataName("UnityEditor.Editor") != null;
        // Without compiler interception, the installed IL rewriter binds the same static entries after compilation.
        bool ilRewrite = !interceptors && symbols.Contains("LUMINPACK_IL_REWRITE");
        return new GeneratorOptions(net8, csharp == null ? 0 : (int)csharp.LanguageVersion, csharp?.Options.AllowUnsafe ?? false, unity,
            Registration(compilation), availableAliases: -1, compilation.AssemblyName ?? "Assembly", interceptors || ilRewrite, ilRewrite);
    }
}
