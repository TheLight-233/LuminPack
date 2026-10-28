using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Collapses former array/span implementation variants onto the shared contiguous memory core.</summary>
internal sealed class ContiguousMemoryProjection : CSharpSyntaxRewriter
{
    /// <summary>Retains old template framing while removing the corresponding state-specific runtime APIs.</summary>
    /// <param name="node">An original array or span implementation call.</param>
    /// <returns>The single contiguous core with its original framing and actual-byte result.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var call = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        var access = call.Expression as MemberAccessExpressionSyntax;
        var method = access?.Name ?? call.Expression as SimpleNameSyntax;
        if (method == null) return call;
        string name = method.Identifier.ValueText;
        if (!(name.Contains("UnmanagedArray") || name.Contains("UnmanagedSpan")) ||
            name.EndsWith("Record", StringComparison.Ordinal) || name == "WriteUnmanagedList") return call;
        bool write = name.StartsWith("Write", StringComparison.Ordinal) || name.StartsWith("DangerousWrite", StringComparison.Ordinal);
        bool read = name.StartsWith("Read", StringComparison.Ordinal) || name.StartsWith("DangerousRead", StringComparison.Ordinal);
        if (!write && !read) return call;
        string receiver = access == null ? string.Empty : access.Expression + ".";
        var arguments = call.ArgumentList.Arguments;
        bool explicitPosition = arguments.Count >= 2 && arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword);
        string position = explicitPosition ? arguments[0].Expression.ToString() : receiver + "Position";
        if (explicitPosition) arguments = arguments.RemoveAt(0);
        if (arguments.Count == 0) return call;
        var payload = arguments[0];
        arguments = arguments.RemoveAt(0);
        var output = arguments.LastOrDefault(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        var count = arguments.FirstOrDefault(argument => argument != output);
        bool header = write ? !name.Contains("WithOutHeader") : count == null && !name.Contains("WithOutHeader");
        bool compressed = name.Contains("WithCompress");
        string countText = count?.Expression.ToString() ?? (write ? name.Contains("Array")
            ? "(" + payload.Expression + " is null ? -1 : " + payload.Expression + ".Length)"
            : payload.Expression + ".Length" : "0");
        string generic = method is GenericNameSyntax types ? types.TypeArgumentList.ToString() : string.Empty;
        string storage = write && name.Contains("Array") ? payload.Expression + ".AsSpan()" : payload.Expression.ToString();
        string flags = ", " + countText + ", " + (header ? "true" : "false") + ", " + (compressed ? "true" : "false");
        string input = write
            ? receiver + "Reserve(" + position + ", global::LuminPack.Core.LuminPackWriter.Size" + generic + "(" + storage + flags + "))"
            : receiver + "GetSpan().Slice(" + position + ")";
        string value = write ? storage : "ref " + storage;
        string source = "global::LuminPack.Core.LuminPack" + (write ? "Writer.Write" : "Reader.Read") + generic + "(" + input + ", " + value + flags +
            (output == null ? string.Empty : ", " + output) + ")";
        if (read && count != null && output == null && !name.StartsWith("Dangerous", StringComparison.Ordinal) && !name.StartsWith("ReadFresh", StringComparison.Ordinal))
            source = receiver + "Advance(" + source + ")";
        return SyntaxFactory.ParseExpression(source).WithTriviaFrom(node);
    }
}
