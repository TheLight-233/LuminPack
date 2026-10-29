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
        var advancing = new AdvancingBody(method.ParameterList.Parameters[0].Identifier.ValueText);
        bool advancingBody = method.Body.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression.ToString() == advancing.Writer)
            .All(access => access.Name.Identifier.ValueText is "Cursor" or "Write" or "Advance" or "EnsureAdditionalCapacity");
        advancingBody &= method.Body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.Identifier.ValueText == advancing.Writer &&
                !(identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier))
            .All(identifier => identifier.Parent is MemberAccessExpressionSyntax member && member.Expression == identifier &&
                member.Name.Identifier.ValueText is "Cursor" or "Write" or "Advance" or "EnsureAdditionalCapacity");
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
        if (advancingBody)
        {
            body = (BlockSyntax)advancing.Visit(method.Body);
            body = body.WithStatements(body.Statements.InsertRange(0, new[]
            {
                SyntaxFactory.ParseStatement("int __fixedUnionIndex = initialIndex;"),
                SyntaxFactory.ParseStatement("if (unionTag.HasValue) { writer.GetSpanReference(__fixedUnionIndex) = unionTag.GetValueOrDefault(); __fixedUnionIndex++; }")
            }).Add(SyntaxFactory.ParseStatement("return __fixedUnionIndex;")));
            members.Add(method.WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                "writer.Position = SerializeUnionAt(ref writer, input, writer.Position, null);"))));
        }
        else if (fixedBody)
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

    /// <summary>Keeps fixed payload writes on the incoming local index without publishing Writer fields per member.</summary>
    private sealed class AdvancingBody : CSharpSyntaxRewriter
    {
        internal readonly string Writer;

        /// <summary>Captures the actual generated state parameter.</summary>
        /// <param name="writer">The selected Writer parameter identity.</param>
        internal AdvancingBody(string writer) { Writer = writer; }

        /// <summary>Addresses the current fixed payload byte through its unpublished local index.</summary>
        /// <param name="node">A direct cursor reference.</param>
        /// <returns>The equivalent reference at the local fixed cursor.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => node.Expression.ToString() == Writer && node.Name.Identifier.ValueText == "Cursor"
                ? SyntaxFactory.ParseExpression(Writer + ".GetSpanReference(__fixedUnionIndex)").WithTriviaFrom(node)
                : base.VisitMemberAccessExpression(node);

        /// <summary>Owns fixed advancement locally and publishes it before a complete capacity reservation.</summary>
        /// <param name="node">The original complete Writer statement.</param>
        /// <returns>The same fixed storage operation, publishing only at a retained capacity boundary.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call ||
                access.Expression.ToString() != Writer) return base.VisitExpressionStatement(node);
            string name = access.Name.Identifier.ValueText;
            var arguments = call.ArgumentList.Arguments;
            if (name == "EnsureAdditionalCapacity" && arguments.Count == 1)
                return SyntaxFactory.ParseStatement("{ " + Writer + ".Position = __fixedUnionIndex; " + Writer + ".EnsureAdditionalCapacity(" +
                    Visit(arguments[0].Expression) + "); }").WithTriviaFrom(node);
            if (name == "Advance" && arguments.Count == 1)
                return SyntaxFactory.ParseStatement("__fixedUnionIndex += " + Visit(arguments[0].Expression) + ";").WithTriviaFrom(node);
            if (name != "Write") return base.VisitExpressionStatement(node);
            string destination = Writer + ".GetSpanReference(__fixedUnionIndex)";
            if (arguments.Count == 2 && arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                return SyntaxFactory.ParseStatement("{ global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref " + destination +
                    ", ref " + Visit(arguments[0].Expression) + ", (uint)(" + Visit(arguments[1].Expression) + ")); __fixedUnionIndex += " +
                    Visit(arguments[1].Expression) + "; }").WithTriviaFrom(node);
            if (arguments.Count != 1) return base.VisitExpressionStatement(node);
            return SyntaxFactory.ParseStatement("__fixedUnionIndex += global::LuminPack.Core.LuminPackWriter." + access.Name +
                "(ref " + destination + ", " + Visit(arguments[0]) + ");").WithTriviaFrom(node);
        }
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
