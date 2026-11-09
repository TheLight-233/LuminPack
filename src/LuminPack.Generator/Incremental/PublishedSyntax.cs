using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Hands the syntax of a published text to the next publication stage, so the text is not parsed again.</summary>
/// <remarks>Incremental steps exchange value-compared text. A stage that still holds the syntax of its result attaches it to that
/// exact string instance, and the stage that owns the string next takes it, falling back to a parse when nothing is attached (a
/// cached step, another generator, or a released entry). Taking removes the entry, so a tree lives only until its owner reads
/// the text, while the string itself may stay in the driver's tables.</remarks>
internal static class PublishedSyntax
{
    private static readonly ConditionalWeakTable<string, CompilationUnitSyntax> Trees = new();

    /// <summary>Attaches the syntax of a text produced from it.</summary>
    /// <param name="text">The exact printed text of <paramref name="root"/>.</param>
    /// <param name="root">The syntax the text was printed from.</param>
    /// <returns>The same text.</returns>
    internal static string Attach(string text, CompilationUnitSyntax root)
    {
        if (!string.IsNullOrEmpty(text) && root != null)
            lock (Trees)
            {
                Trees.Remove(text);
                Trees.Add(text, root);
            }
        return text;
    }

    /// <summary>Takes the syntax attached to a text and releases it.</summary>
    /// <param name="text">The text whose owner reads it now.</param>
    /// <returns>The attached syntax, or null when the text must be parsed.</returns>
    internal static CompilationUnitSyntax Take(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        lock (Trees)
        {
            if (!Trees.TryGetValue(text, out var root))
                return null;
            Trees.Remove(text);
            return root;
        }
    }
}
