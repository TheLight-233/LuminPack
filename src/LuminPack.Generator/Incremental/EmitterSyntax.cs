using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Parses each emitter text once per compilation, although two stages start from it.</summary>
/// <remarks>The serializer projection of a packable root and the operation template of the same type begin with the same emitter
/// text. Whichever stage reaches the text first parses it and leaves the tree here, and the other stage takes it. A tree is keyed
/// by its exact text and parse options, so the taken tree is the tree a parse would return; a text nobody takes is released with
/// its compilation.</remarks>
internal static class EmitterSyntax
{
    private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<(string Source, CSharpParseOptions Options), CompilationUnitSyntax>> Pending = new();

    /// <summary>Takes the tree another stage parsed from the same text, or parses the text and leaves it for that stage.</summary>
    /// <param name="compilation">The compilation whose stages share the text.</param>
    /// <param name="source">The complete emitter text.</param>
    /// <param name="options">The parse options of both stages.</param>
    /// <returns>The parsed compilation unit.</returns>
    internal static CompilationUnitSyntax Parse(Compilation compilation, string source, CSharpParseOptions options)
    {
        var pending = Pending.GetValue(compilation, static _ => new());
        var key = (source, options);
        if (pending.TryRemove(key, out var root))
            return root;
        root = CSharpSyntaxTree.ParseText(source, options).GetCompilationUnitRoot();
        pending.TryAdd(key, root);
        return root;
    }
}
