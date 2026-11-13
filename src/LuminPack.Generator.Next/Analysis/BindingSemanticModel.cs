using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Shares a semantic model that binds identities without running nullable flow analysis.</summary>
/// <remarks>The generator loads on Roslyn 4.3. Hosts that predate the compiler's nullable-analysis option use their
/// normal semantic model. Declaration annotations still come from the compiler's symbols.</remarks>
internal static class BindingSemanticModel
{
    private static readonly Func<Compilation, SyntaxTree, SemanticModel>? Create = FindFactory();
    private static readonly ConditionalWeakTable<Compilation, ConditionalWeakTable<SyntaxTree, SemanticModel>> Models = new();

    /// <summary>Returns the binding model for one source tree.</summary>
    /// <param name="compilation">The compilation owning the tree.</param>
    /// <param name="tree">The source tree to bind.</param>
    /// <returns>The cached semantic model.</returns>
    internal static SemanticModel For(Compilation compilation, SyntaxTree tree) =>
        Models.GetValue(compilation, static _ => new ConditionalWeakTable<SyntaxTree, SemanticModel>())
            .GetValue(tree, item => Create?.Invoke(compilation, item) ?? compilation.GetSemanticModel(item));

    /// <summary>Resolves the optional public semantic model overload once for the compiler host.</summary>
    /// <returns>The binding factory, or null when the compiler does not expose the option.</returns>
    private static Func<Compilation, SyntaxTree, SemanticModel>? FindFactory()
    {
        Type? options = typeof(Compilation).Assembly.GetType("Microsoft.CodeAnalysis.SemanticModelOptions");
        if (options == null || !Enum.IsDefined(options, "DisableNullableAnalysis"))
            return null;
        MethodInfo? method = typeof(Compilation).GetMethod("GetSemanticModel", new[] { typeof(SyntaxTree), options });
        if (method == null)
            return null;
        object disableNullableAnalysis = Enum.Parse(options, "DisableNullableAnalysis");
        return (compilation, tree) => (SemanticModel)method.Invoke(compilation, new[] { (object)tree, disableNullableAnalysis })!;
    }
}
