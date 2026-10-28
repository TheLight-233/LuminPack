using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Publishes closed binary extensions backed by each type's single concrete operation.</summary>
internal static class BinaryExtensionProjection
{
    /// <summary>Creates thin default and compiled-profile operations for the concrete types in this contribution.</summary>
    /// <param name="container">The assembly-specific extension container identity.</param>
    /// <param name="operations">The concrete state operations before profile specialization.</param>
    /// <param name="compilation">The compilation supplying declaration-owned document profiles.</param>
    /// <returns>One partial extension container per payload, with no formatter objects or interface dispatch.</returns>
    /// <remarks>C# compares same-named, same-shaped members of one type pairwise when checking duplicate signatures, so
    /// each payload's shells live in their own static class. Extension lookup gathers every class of the namespace, so
    /// callers bind to the same shells.</remarks>
    internal static ClassDeclarationSyntax[] Create(string container, IEnumerable<MemberDeclarationSyntax> operations, Compilation compilation)
    {
        var groups = new Dictionary<string, List<MemberDeclarationSyntax>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var method in operations.OfType<MethodDeclarationSyntax>().Where(method =>
            method.ParameterList.Parameters.Count == 2 && method.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson"))
        {
            var state = method.ParameterList.Parameters[0];
            bool json = state.Type.ToString().Contains("Json");
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            bool scalar = calls.Length == 1 && calls[0].Expression.ToString() is
                "global::LuminPack.Core.LuminPackWriter.Write" or "global::LuminPack.Core.LuminPackReader.Read";
            bool write = state.Type.ToString().EndsWith("Writer", StringComparison.Ordinal);
            string name = write ? "Write" : "Read";
            string payload = method.ParameterList.Parameters[1].Type.ToString();
            var payloadParameters = method.TypeParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText).ToArray() ?? Array.Empty<string>();
            if (payloadParameters.Contains(payload)) continue;
            string profileName = "TOptions";
            while (payloadParameters.Contains(profileName)) profileName += "_";
            string passing = write ? "in" : "ref";
            bool rootOwnsCursor = !json && (payload.StartsWith("global::System.", StringComparison.Ordinal) || payload.Contains("[")) &&
                method.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax);
            bool compound = !json && !rootOwnsCursor && method.DescendantNodes().Any(node => node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or LocalFunctionStatementSyntax);
            foreach (bool profile in new[] { false, true })
            {
                // A concrete raw construction must outrank an open generic formatter,
                // whose unknown arguments may require a different contract or dispatch.
                if (scalar && !profile && (payloadParameters.Length != 0 || !payload.Contains("<"))) continue;
                var source = new StringBuilder();
                source.AppendLine("/// <summary>Executes the complete closed binary operation for this value.</summary>");
                source.AppendLine("/// <param name=\"state\">The advancing binary state.</param>");
                source.AppendLine("/// <param name=\"value\">The value storage participating in the operation.</param>");
                foreach (string parameter in payloadParameters)
                    source.AppendLine("/// <typeparam name=\"" + parameter + "\">The declared payload type argument.</typeparam>");
                if (profile) source.AppendLine("/// <typeparam name=\"" + profileName + "\">The stateless binary profile.</typeparam>");
                source.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                var arguments = profile ? payloadParameters.Concat(new[] { profileName }).ToArray() : payloadParameters;
                string typeArguments = arguments.Length == 0 ? string.Empty : "<" + string.Join(", ", arguments) + ">";
                source.Append("internal static void ").Append(name).Append(typeArguments)
                    .Append("(this ref ").Append(state.Type).Append(" state, ").Append(passing).Append(' ').Append(payload).Append(" value)")
                    .Append(" ").Append(string.Join(" ", method.ConstraintClauses.Select(clause => clause.ToString())))
                    .Append(profile ? " where " + profileName + " : struct, global::LuminPack.Option.ILuminPackOptions" : string.Empty);
                string call = "global::LuminPack.LuminPackSerializer." + method.Identifier.ValueText + typeArguments;
                if (scalar) call = "global::LuminPack.Core.LuminPack" + (write ? "Writer.Write" : "Reader.Read");
                if (compound)
                {
                    source.Append(" { ").Append(state.Type).Append(" isolated = default; state.CopyTo(ref isolated); ")
                        .Append(call).Append("(ref isolated, ").Append(passing).Append(" value); isolated.CopyTo(ref state); }");
                }
                else if (json && !write)
                {
                    CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypesByName.TryGetValue(payload.TrimEnd('?'), out var payloadType);
                    string selected = profile ? profileName : CompiledOptions.Resolve(payloadType, compilation).TypeName;
                    source.Append(" { if (state.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.None && !state.Read<")
                        .Append(selected).Append(">()) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"JSON input does not contain a value.\"); ")
                        .Append(call).Append("(ref state, ref value); state.CompleteValue(); }");
                }
                else source.Append(" => ").Append(call).Append("(ref state, ").Append(passing).AppendLine(" value);");
                string owner = "Binary_" + container + "_" + RootContainerProjection.Hash(RootContainerProjection.PayloadKey(payload));
                if (!groups.TryGetValue(owner, out var members))
                {
                    groups.Add(owner, members = new List<MemberDeclarationSyntax>());
                    order.Add(owner);
                }
                members.Add(SyntaxFactory.ParseMemberDeclaration(source.ToString()));
            }
        }
        return order.Select(owner => SyntaxFactory.ClassDeclaration(owner)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithMembers(SyntaxFactory.List(groups[owner]))).ToArray();
    }

    /// <summary>Uses the public advancing vocabulary after all profile and dispatch decisions are complete.</summary>
    internal sealed class Calls : CSharpSyntaxRewriter
    {
        private HashSet<string> _states;

        /// <summary>Rewrites operation bodies while preserving their extension-to-root forwarders.</summary>
        /// <param name="node">The generated method.</param>
        /// <returns>The same operation calling closed Write and Read extensions.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Parent is ClassDeclarationSyntax owner && owner.Identifier.ValueText.StartsWith("Binary_", StringComparison.Ordinal)) return node;
            var previous = _states;
            _states = new HashSet<string>(node.DescendantNodesAndSelf().OfType<ParameterSyntax>().Where(parameter =>
                parameter.Type?.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty) is "LuminPackWriter" or "LuminPackReader" or
                    "LuminPackJsonWriter" or "LuminPackJsonReader").Select(parameter => parameter.Identifier.ValueText), StringComparer.Ordinal);
            foreach (var variable in node.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                    creation.Type.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty) is "LuminPackWriter" or "LuminPackReader" or "LuminPackJsonWriter" or "LuminPackJsonReader")
                    _states.Add(variable.Identifier.ValueText);
            try { return base.VisitMethodDeclaration(node); }
            finally { _states = previous; }
        }

        /// <summary>Turns concrete state calls into their typed advancing operations.</summary>
        /// <param name="node">The fully specialized invocation.</param>
        /// <returns>The typed Write or Read call, retaining explicit profile and ref-value semantics.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (_states == null || rewritten.Expression is not MemberAccessExpressionSyntax access) return rewritten;
            string name = access.Name.Identifier.ValueText;
            if (_states.Contains(access.Expression.ToString()) && name is "WriteStringRecord" or "ReadStringRecord")
            {
                string target = name == "WriteStringRecord" ? "Write" : "Read";
                if (name == "ReadStringRecord" && rewritten.ArgumentList.Arguments.Count == 0)
                    rewritten = rewritten.WithArgumentList(rewritten.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.ParseExpression("(string?)null"))));
                return rewritten.WithExpression(access.WithName(access.Name is GenericNameSyntax generic ? generic.WithIdentifier(SyntaxFactory.Identifier(target)) : SyntaxFactory.IdentifierName(target)));
            }
            if (name is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson") || !access.Expression.ToString().Contains("LuminPackSerializer") ||
                rewritten.ArgumentList.Arguments.Count != 2 || access.Name is GenericNameSyntax { TypeArgumentList.Arguments.Count: > 1 }) return rewritten;
            if (access.Name is GenericNameSyntax selectedProfile)
            {
                string profile = selectedProfile.TypeArgumentList.Arguments.Last().ToString();
                if (!profile.StartsWith("__LuminProfile", StringComparison.Ordinal) &&
                    !profile.StartsWith("global::LuminPack.Option.", StringComparison.Ordinal)) return rewritten;
            }
            var state = rewritten.ArgumentList.Arguments[0];
            if (!_states.Contains(state.Expression.ToString())) return rewritten;
            if (node.HasAnnotations("LuminPack.StorageContract")) return rewritten;
            string operation = name.StartsWith("Serialize", StringComparison.Ordinal) ? "Write" : "Read";
            SimpleNameSyntax selected = access.Name is GenericNameSyntax options ? options.WithIdentifier(SyntaxFactory.Identifier(operation)) : SyntaxFactory.IdentifierName(operation);
            return rewritten.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, state.Expression, selected))
                .WithArgumentList(rewritten.ArgumentList.WithArguments(rewritten.ArgumentList.Arguments.RemoveAt(0)));
        }
    }
}
