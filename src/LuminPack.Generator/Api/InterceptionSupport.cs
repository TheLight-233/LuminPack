using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Uses optional compiler APIs without raising the generator's Roslyn 4.3 loading requirement.</summary>
internal static class InterceptionSupport
{
    private static readonly MethodInfo LocationMethod = FindLocationMethod();

    /// <summary>Checks whether compiler interception or installed IL rewriting can bind static entries.</summary>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <returns>Whether the compilation uses a static entry backend.</returns>
    internal static bool IsEnabled(Compilation compilation)
        => IsEnabled(compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions);

    /// <summary>Checks compiler features and the installed IL integration on consumer parse options.</summary>
    /// <param name="options">The consumer's language and feature settings.</param>
    /// <returns>Whether compiler or IL interception is enabled.</returns>
    internal static bool IsEnabled(CSharpParseOptions options)
        => UsesIlRewriting(options) || UsesCompilerInterceptors(options);

    /// <summary>Selects the installed postprocessor when compiler interception is unavailable.</summary>
    /// <param name="options">The consumer's parse settings.</param>
    /// <returns>Whether the generated mapping requires build-time IL rewriting.</returns>
    internal static bool UsesIlRewriting(CSharpParseOptions options)
        => options != null && options.PreprocessorSymbolNames.Contains("LUMINPACK_IL_REWRITE") && !UsesCompilerInterceptors(options);

    /// <summary>Checks the compiler's native interception capability and namespace opt-in.</summary>
    /// <param name="options">The consumer's parse settings.</param>
    /// <returns>Whether versioned compiler interception can be emitted.</returns>
    internal static bool UsesCompilerInterceptors(CSharpParseOptions options)
        => LocationMethod != null && options != null && (int)options.LanguageVersion >= 1100 && options.Features.Any(pair =>
            pair.Key is "InterceptorsNamespaces" or "InterceptorsPreviewNamespaces" &&
            pair.Value.Split(';').Any(value => value.Trim() == "LuminPack"));

    /// <summary>Obtains a compiler-owned source location without retaining semantic objects in generated output.</summary>
    /// <param name="model">The semantic model containing the call.</param>
    /// <param name="call">The invocation being replaced.</param>
    /// <returns>The attribute spelling, or null when this call cannot be intercepted.</returns>
    internal static string Attribute(SemanticModel model, InvocationExpressionSyntax call)
    {
        if (UsesIlRewriting(call.SyntaxTree.Options as CSharpParseOptions))
            return string.Empty;
        if (LocationMethod == null)
            return null;
        object location = LocationMethod.Invoke(null, new object[] { model, call, CancellationToken.None });
        if (location == null)
            return null;
        var type = location.GetType();
        object version = type.GetProperty("Version").GetValue(location);
        string data = (string)type.GetProperty("Data").GetValue(location);
        return "[global::System.Runtime.CompilerServices.InterceptsLocation(" + version + ", " +
            SymbolDisplay.FormatLiteral(data, true) + ")]";
    }

    /// <summary>Finds the optional public compiler extension once per generator load.</summary>
    /// <returns>The location API on modern Roslyn, or null on older hosts.</returns>
    private static MethodInfo FindLocationMethod()
    {
        var assembly = typeof(CSharpCompilation).Assembly;
        foreach (string name in new[] { "Microsoft.CodeAnalysis.CSharp.CSharpExtensions", "Microsoft.CodeAnalysis.CSharpExtensions" })
        {
            var type = assembly.GetType(name);
            var method = type?.GetMethod("GetInterceptableLocation", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SemanticModel), typeof(InvocationExpressionSyntax), typeof(CancellationToken) }, null);
            if (method != null)
                return method;
        }
        return null;
    }
}
