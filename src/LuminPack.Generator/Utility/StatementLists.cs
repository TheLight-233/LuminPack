using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Rebuilds statement lists only when a pass changed them.</summary>
/// <remarks>
/// A statement pass that always wraps its visited statements in a new list allocates a new block and, through it, a new
/// spine up to the unit for every block it visits. Returning the original list when every statement is the same node keeps
/// the tree, and so the generated text, exactly as before.
/// </remarks>
internal static class StatementLists
{
    /// <summary>Returns the original list when the visited statements are the same nodes in the same order.</summary>
    /// <param name="original">The statements before the pass.</param>
    /// <param name="statements">The statements after the pass.</param>
    /// <returns>The original list, or a new list of the visited statements.</returns>
    internal static SyntaxList<StatementSyntax> Rebuild(SyntaxList<StatementSyntax> original, List<StatementSyntax> statements)
    {
        if (statements.Count == original.Count)
        {
            int index = 0;
            while (index < statements.Count && ReferenceEquals(statements[index], original[index]))
                index++;
            if (index == statements.Count)
                return original;
        }
        return SyntaxFactory.List(statements);
    }

    /// <summary>Updates a block only when its statements changed.</summary>
    /// <param name="block">The original block.</param>
    /// <param name="statements">The visited statements.</param>
    /// <returns>The same block, or the block with the new statements.</returns>
    internal static BlockSyntax Rebuild(BlockSyntax block, List<StatementSyntax> statements)
    {
        var list = Rebuild(block.Statements, statements);
        return list == block.Statements ? block : block.WithStatements(list);
    }
}
