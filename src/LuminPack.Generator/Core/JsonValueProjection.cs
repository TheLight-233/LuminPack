using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Uses the same complete value vocabulary for statically selected JSON and binary operations.</summary>
internal sealed class JsonValueProjection : CSharpSyntaxRewriter
{
    private static readonly HashSet<string> Writes = new("WriteString WriteInt WriteUInt WriteByte WriteSByte WriteShort WriteUShort WriteLong WriteULong WriteFloat WriteDouble WriteDecimal WriteChar WriteBool WriteDateTime WriteDateTimeOffset WriteGuid WriteTimeSpan".Split(' '));
    private static readonly Dictionary<string, string> Reads = new()
    {
        ["ReadString"] = "string", ["ReadInt"] = "int", ["ReadUInt"] = "uint", ["ReadByte"] = "byte", ["ReadSByte"] = "sbyte",
        ["ReadShort"] = "short", ["ReadUShort"] = "ushort", ["ReadLong"] = "long", ["ReadULong"] = "ulong", ["ReadFloat"] = "float",
        ["ReadDouble"] = "double", ["ReadDecimal"] = "decimal", ["ReadChar"] = "char", ["GetBoolean"] = "bool"
    };
    private HashSet<string> _writers, _readers;

    /// <summary>Tracks only the JSON state parameters owned by this generated operation.</summary>
    /// <param name="node">The generated operation.</param>
    /// <returns>The operation using canonical value calls.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var writers = _writers; var readers = _readers;
        States(node, out var currentWriters, out var currentReaders);
        if (currentWriters.Count == 0 && currentReaders.Count == 0) return node;
        _writers = currentWriters; _readers = currentReaders;
        try { return base.VisitMethodDeclaration(node); }
        finally { _writers = writers; _readers = readers; }
    }

    /// <summary>Recognizes declared JSON states without matching ordinary user member names.</summary>
    /// <param name="node">The generated method.</param>
    /// <param name="writers">Receives the JSON writer parameter and local receiver names.</param>
    /// <param name="readers">Receives the JSON reader parameter and local receiver names.</param>
    private static void States(MethodDeclarationSyntax node, out HashSet<string> writers, out HashSet<string> readers)
    {
        writers = new HashSet<string>();
        readers = new HashSet<string>();
        foreach (var descendant in node.DescendantNodes())
        {
            string type, name;
            if (descendant is ParameterSyntax parameter)
            {
                type = parameter.Type?.ToString();
                name = parameter.Identifier.ValueText;
            }
            else if (descendant is VariableDeclaratorSyntax { Initializer.Value: ObjectCreationExpressionSyntax creation } variable)
            {
                type = creation.Type.ToString();
                name = variable.Identifier.ValueText;
            }
            else continue;
            if (type?.EndsWith("LuminPackJsonWriter") == true) writers.Add(name);
            if (type?.EndsWith("LuminPackJsonReader") == true) readers.Add(name);
        }
    }

    /// <summary>Selects complete overloads after the declaration profile has already been propagated.</summary>
    /// <param name="node">The original scalar or string invocation.</param>
    /// <returns>The canonical Write or statically typed Read invocation.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        if (result.Expression is not MemberAccessExpressionSyntax access) return result;
        string name = access.Name.Identifier.ValueText, receiver = access.Expression.ToString();
        if (_writers?.Contains(receiver) == true && name == "Write")
            return result.WithArgumentList(result.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(result.ArgumentList.Arguments.Select(argument =>
                argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword) ? argument.WithRefKindKeyword(default) : argument))));
        if (_writers?.Contains(receiver) == true && Writes.Contains(name))
            return result.WithExpression(access.WithName(access.Name is GenericNameSyntax generic ? generic.WithIdentifier(SyntaxFactory.Identifier("Write")) : SyntaxFactory.IdentifierName("Write")));
        if (_readers?.Contains(receiver) != true || !Reads.TryGetValue(name, out string type) || result.ArgumentList.Arguments.Count != 0) return result;
        string profile = access.Name is GenericNameSyntax selected ? selected.TypeArgumentList.Arguments.ToString() : "global::LuminPack.Option.LuminPackDefaultOptions";
        return result.WithExpression(access.WithName(SyntaxFactory.GenericName(SyntaxFactory.Identifier("Read"), SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(new[] { SyntaxFactory.ParseTypeName(type), SyntaxFactory.ParseTypeName(profile) })))));
    }
}
