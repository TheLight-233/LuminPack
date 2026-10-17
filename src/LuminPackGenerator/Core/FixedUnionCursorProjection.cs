using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Shares one fixed case implementation between original state calls and returning virtual slots.</summary>
internal static class FixedUnionCursorProjection
{
    /// <summary>Retains the original entry and appends its unique index-returning payload implementation.</summary>
    /// <param name="method">The expanded, eligibility-checked Union payload method.</param>
    /// <param name="members">The receiving Serializer declarations.</param>
    internal static void Append(MethodDeclarationSyntax method, List<MemberDeclarationSyntax> members)
    {
        var cursors = method.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(variable =>
            variable.Identifier.GetAnnotations(BinaryEntryInlining.OperationLocalAnnotation).Any() &&
            IsWriterCall(variable.Initializer?.Value, "GetCurrentSpanIndex")).ToArray();
        var publications = method.Body.DescendantNodes().OfType<ExpressionStatementSyntax>().Where(statement =>
            statement.Expression is AssignmentExpressionSyntax assignment && IsWriterCall(assignment.Left, "GetCurrentSpanOffset")).ToArray();
        bool fixedBody = cursors.Length == 1 && publications.Length == 1 &&
            publications[0].Expression is AssignmentExpressionSyntax { Right: IdentifierNameSyntax name } &&
            name.Identifier.ValueText == cursors[0].Identifier.ValueText &&
            !method.Body.DescendantNodes().OfType<ReturnStatementSyntax>().Any() &&
            method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>().All(call =>
                call.Expression is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "writer" } } access ||
                access.Name.Identifier.ValueText is "GetSpanReference" or "GetCurrentSpanIndex" or "GetCurrentSpanOffset" or "WriteUnmanaged");

        BlockSyntax body;
        if (fixedBody)
        {
            string cursor = cursors[0].Identifier.ValueText;
            body = method.Body.ReplaceNode(publications[0], SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(cursor)));
            var initializer = body.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == cursor);
            var declaration = initializer.Ancestors().OfType<LocalDeclarationStatementSyntax>().First();
            body = body.InsertNodesAfter(declaration, new[] { SyntaxFactory.ParseStatement(
                "if (unionTag.HasValue) { writer.GetSpanReference(" + cursor + ") = unionTag.GetValueOrDefault(); " + cursor + " += 1; }") });
            initializer = body.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(variable => variable.Identifier.ValueText == cursor);
            body = body.ReplaceNode(initializer.Initializer.Value, SyntaxFactory.IdentifierName("initialIndex"));
            members.Add(method.WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                "writer.GetCurrentSpanOffset() = SerializeUnionAt(ref writer, input, writer.GetCurrentSpanIndex(), null);"))));
        }
        else
        {
            // A future emitter may add a state-observing boundary. Preserve its complete
            // original body instead of guessing that its publications can be removed.
            members.Add(method);
            body = SyntaxFactory.Block(SyntaxFactory.ParseStatement("writer.GetCurrentSpanOffset() = initialIndex;"),
                SyntaxFactory.ParseStatement("if (unionTag.HasValue) writer.WriteUnionHeader(unionTag.GetValueOrDefault());"),
                SyntaxFactory.ParseStatement("SerializeUnion(ref writer, input);"),
                SyntaxFactory.ParseStatement("return writer.GetCurrentSpanIndex();"));
        }
        members.Add(method.WithIdentifier(SyntaxFactory.Identifier("SerializeUnionAt")).WithReturnType(SyntaxFactory.ParseTypeName("int"))
            .WithBody(body).WithParameterList(method.ParameterList.AddParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("initialIndex")).WithType(SyntaxFactory.ParseTypeName("int")),
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("unionTag")).WithType(SyntaxFactory.ParseTypeName("byte?"))))
            .WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia(
                "/// <summary>Writes a fixed Union case payload and optionally its narrow tag at an explicit cursor.</summary>" + Environment.NewLine +
                "/// <param name=\"writer\">The caller-owned active binary operation.</param>" + Environment.NewLine +
                "/// <param name=\"input\">The concrete case whose existing raw field representation is written.</param>" + Environment.NewLine +
                "/// <param name=\"initialIndex\">The absolute destination byte index, which may not yet be published.</param>" + Environment.NewLine +
                "/// <param name=\"unionTag\">The narrow case tag, or null when the original virtual slot already wrote it.</param>" + Environment.NewLine +
                "/// <returns>The absolute next byte index, not the number of bytes written.</returns>" + Environment.NewLine +
                "/// <remarks>The caller owns operation completion and publishes the returned cursor before an observing boundary.</remarks>" + Environment.NewLine)));
    }

    /// <summary>Recognizes the compiler's state access without matching a similarly named user member.</summary>
    /// <param name="expression">The generated state expression.</param>
    /// <param name="name">The exact zero-argument Writer accessor.</param>
    /// <returns>Whether the expression accesses the original Writer state.</returns>
    private static bool IsWriterCall(ExpressionSyntax expression, string name)
        => expression is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0,
            Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "writer" } } access } &&
            access.Name.Identifier.ValueText == name;
}
