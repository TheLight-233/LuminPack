using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal sealed partial class CompiledOperationProjection
{
    /// <summary>Selects document-generic kernels and preserves existing virtual and registry boundaries.</summary>
    private sealed class Body : CSharpSyntaxRewriter
    {
        private string _profile;
        private readonly bool _writer;
        private readonly string _state;
        private readonly bool _dispatch;
        private readonly string _payload;
        private readonly string _stateType;
        private readonly string _fallback;
        /// <summary>Captures one generic operation context.</summary>
        /// <param name="profile">The collision-free generated profile parameter.</param>
        /// <param name="stateType">The actual binary, JSON or evaluator state family.</param>
        /// <param name="writer">Whether the state writes a binary or JSON payload.</param>
        /// <param name="state">The original state receiver identifier.</param>
        /// <param name="dispatch">Whether known types select their declaration default.</param>
        /// <param name="payload">The generic fallback's payload parameter, when present.</param>
        /// <param name="fallback">The consuming assembly's complete profile for unknown registered types.</param>
        internal Body(string profile, string stateType, bool writer, string state, bool dispatch, string payload, string fallback = "global::LuminPack.Option.LuminPackDefaultOptions")
        { _profile = profile; _stateType = stateType; _writer = writer; _state = state; _dispatch = dispatch; _payload = payload; _fallback = fallback; }

        /// <summary>Retains an explicitly declared binary member profile through opaque and virtual boundaries.</summary>
        /// <param name="node">The original expanded operation scope.</param>
        /// <returns>The same scope with its statically selected profile.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            string enclosing = _profile;
            string scope = node.GetAnnotations(BinaryEntryInlining.OptionScopeAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
            if (scope != null && !_stateType.Contains("Json")) _profile = scope;
            try { return base.VisitBlock(node); }
            finally { _profile = enclosing; }
        }
        /// <summary>Replaces configuration constants with the enclosing document's compile-time profile.</summary>
        /// <param name="node">A generated option expression.</param>
        /// <returns>The generic profile expression or unchanged member access.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.ValueText == "Option" && node.Expression.ToString() == _state)
                return SyntaxFactory.ParseExpression("default(" + _profile + ")").WithTriviaFrom(node);
            if (node.Expression is DefaultExpressionSyntax && node.Name.Identifier.ValueText is
                "StringEncoding" or "StringRecording" or "MaxJsonDepth" or "ValidateJsonSyntax" or "StandardFormat")
                return node.WithExpression(SyntaxFactory.DefaultExpression(SyntaxFactory.IdentifierName(_profile)));
            return base.VisitMemberAccessExpression(node);
        }
        /// <summary>Propagates profiles through kernels, concrete overloads, recursive calls and virtual Union slots.</summary>
        /// <param name="node">An existing generated call.</param>
        /// <returns>The same operation bound to its document profile.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (result.Expression is SimpleNameSyntax local && IsFacadeOperation(local.Identifier.ValueText))
            {
                var localArguments = local is GenericNameSyntax localGeneric ? localGeneric.TypeArgumentList.Arguments : default;
                if (localArguments.Count == 0 && _payload != null) localArguments = localArguments.Add(SyntaxFactory.ParseTypeName(_payload));
                localArguments = localArguments.Add(SyntaxFactory.ParseTypeName(_profile));
                return result.WithExpression(SyntaxFactory.GenericName(local.Identifier, SyntaxFactory.TypeArgumentList(localArguments)));
            }
            if (result.Expression is not MemberAccessExpressionSyntax access) return result;
            string name = access.Name.Identifier.ValueText;
            if (name == "TryGetValue" && access.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry")
                return result.WithExpression(access.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackFormatterRegistry<" +
                    (_dispatch ? "global::LuminPack.Option.LuminPackFallbackOptions<" + _profile + ", " + _fallback + ">" : _profile) + ">"))
                    .WithName(SyntaxFactory.IdentifierName(_stateType.Contains("Json") ? "TryGetJsonValue" : "TryGetValue")));
            bool json = _stateType.Contains("Json");
            string stateType = _stateType.Replace("global::", string.Empty);
            if (!stateType.StartsWith("LuminPack.Core.", StringComparison.Ordinal)) stateType = "LuminPack.Core." + stateType;
            bool state = access.Expression.ToString() == _state || access.Expression is IdentifierNameSyntax receiver &&
                receiver.Identifier.GetAnnotations(OperationTemplateRewriter.StateAnnotation).Any(annotation => annotation.Data == stateType);
            if (state && !_dispatch && _payload != null && name is "WriteValue" or "ReadValue" or "CalculateOffset")
            {
                string operation = name == "CalculateOffset" ? "Sizeof" : name == "WriteValue" ? json ? "SerializeJson" : "Serialize" : json ? "DeserializeJson" : "Deserialize";
                var forwarded = result.ArgumentList.Arguments.Insert(0, SyntaxFactory.Argument(access.Expression).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)));
                return result.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer." + operation + "<" + _payload + ", " + _profile + ">"))
                    .WithArgumentList(result.ArgumentList.WithArguments(forwarded));
            }
            var kernels = json ? _writer ? CompiledOptionProjection.JsonWriterMethods : CompiledOptionProjection.JsonReaderMethods :
                _stateType.Contains("Evaluator") ? CompiledOptionProjection.EvaluatorMethods : _writer ? CompiledOptionProjection.WriterMethods : CompiledOptionProjection.ReaderMethods;
            bool kernel = state && kernels.Contains(name);
            if (kernel && !json && access.Name is GenericNameSyntax fixedProfile && fixedProfile.TypeArgumentList.Arguments[0].ToString().Contains("LuminPackFixedOptions")) return result;
            bool formatter = access.Expression.ToString().EndsWith("FormatterDispatch", StringComparison.Ordinal);
            bool serializer = (IsFacadeOperation(name) || name is "SerializeUnion" or "DeserializeUnion" or "DeserializeFresh" or "SerializeWithCompress" or "DeserializeWithCompress") &&
                (access.Expression.ToString().Contains("LuminPackSerializer") || formatter);
            if (formatter && name is "__LuminPackJsonEncoding" or "__LuminPackJsonReadBoundary") serializer = true;
            bool union = name.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) || name.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) ||
                name.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal);
            bool boundary = union || name == "TryReadOrderedJsonProperty";
            if (!kernel && !serializer && !boundary) return result;
            var arguments = access.Name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments : default;
            if (serializer && !formatter && !_dispatch && arguments.Count == 0 && _payload != null) arguments = arguments.Add(SyntaxFactory.ParseTypeName(_payload));
            if (union)
            {
                string root = node.GetAnnotations(UnionRootAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
                if (root != null)
                    return SyntaxFactory.ParseExpression(root + "." + name + "_Compiled<" + _profile + ">(" + node.ArgumentList.Arguments[0] + ", " + access.Expression + ")").WithTriviaFrom(node);
            }
            if (formatter && arguments.Count == 0 && _payload != null) arguments = arguments.Add(SyntaxFactory.ParseTypeName(_payload));
            if (kernel && access.Name is GenericNameSyntax) arguments = arguments.RemoveAt(0);
            arguments = kernel ? arguments.Insert(0, SyntaxFactory.IdentifierName(_profile)) : arguments.Add(SyntaxFactory.IdentifierName(_profile));
            result = result.WithExpression(access.WithName(SyntaxFactory.GenericName(access.Name.Identifier, SyntaxFactory.TypeArgumentList(arguments))));
            return result;
        }

        /// <summary>Folds declaration-default selection inside the existing generic type dispatch.</summary>
        /// <param name="node">The existing case's concrete Serializer call.</param>
        /// <returns>A type-constant profile choice or the unchanged statement.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            string enclosing = _profile;
            string scope = node.GetAnnotations(BinaryEntryInlining.OptionScopeAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
            if (scope != null && !_stateType.Contains("Json")) _profile = scope;
            ExpressionStatementSyntax rewritten;
            try { rewritten = (ExpressionStatementSyntax)base.VisitExpressionStatement(node); }
            finally { _profile = enclosing; }
            if (!_dispatch || node.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } ||
                access.Name.Identifier.ValueText is not ("SerializeJson" or "DeserializeJson" or "Serialize" or "Deserialize" or "Sizeof")) return rewritten;
            return SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression("typeof(" + _profile + ") == typeof(global::LuminPack.Option.LuminPackDeclaredOptions)"),
                SyntaxFactory.Block(node), SyntaxFactory.ElseClause(SyntaxFactory.Block(rewritten)));
        }
    }
}
