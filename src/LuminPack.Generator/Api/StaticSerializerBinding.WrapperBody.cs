using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    /// <summary>Closes a source body while retaining the original overload and argument binding.</summary>
    private sealed class WrapperBody : NodeRewriter
    {
        private readonly StaticSerializerBinding _owner;
        private readonly Wrapper _wrapper;
        private readonly SemanticModel _model;
        private readonly Dictionary<ITypeParameterSymbol, ITypeSymbol> _arguments;
        private readonly int _depth;
        internal bool Valid = true;

        /// <summary>Creates a compilation-local body specialization.</summary>
        /// <param name="owner">The binding graph.</param>
        /// <param name="wrapper">The concrete body being produced.</param>
        /// <param name="model">The original source binding.</param>
        /// <param name="arguments">The correlated type substitutions.</param>
        /// <param name="depth">The bounded recursion depth.</param>
        internal WrapperBody(StaticSerializerBinding owner, Wrapper wrapper, SemanticModel model,
            Dictionary<ITypeParameterSymbol, ITypeSymbol> arguments, int depth)
        {
            _owner = owner;
            _wrapper = wrapper;
            _model = model;
            _arguments = arguments;
            _depth = depth;
        }

        /// <summary>Preserves selected methods and freezes omitted caller-information arguments.</summary>
        /// <param name="node">The original invocation.</param>
        /// <returns>The concretely bound invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            {
                var value = _model.GetConstantValue(node);
                return value.HasValue && value.Value is string name
                    ? SyntaxFactory.ParseExpression(SymbolDisplay.FormatLiteral(name, true)) : Fail(node);
            }
            if (_model.GetOperation(node) is not IInvocationOperation operation || operation.TargetMethod.ReducedFrom != null)
                return Fail(node);
            var method = Close(operation.TargetMethod);
            if (method == null || !IsClosed(method) || method.MethodKind != MethodKind.Ordinary ||
                method.ContainingNamespace.ToDisplayString() is "System.Reflection" or "System.Diagnostics")
                return Fail(node);
            ExpressionSyntax target;
            if (_owner.IsSerializer(method) && method.Name != "Register")
            {
                if (method.TypeArguments.Any(argument => !_owner._binding.IsSymbolAccessibleWithin(argument, _owner._binding.Assembly)))
                    return Fail(node);
                _wrapper.Operations.Add(Signature(method));
                target = SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer." + _owner.RequireOperation(method));
            }
            else if (_owner._serializationMethods.Contains(method.OriginalDefinition) &&
                (method.IsGenericMethod || method.ContainingType.IsGenericType))
            {
                var dependency = _owner.Specialize(method, _depth + 1);
                if (dependency == null)
                    return Fail(node);
                _wrapper.Dependencies.Add(dependency);
                target = SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer." + dependency.Name);
            }
            else
            {
                if (!Accessible(method))
                    return Fail(node);
                string name = Escape(method.Name) + (method.IsGenericMethod
                    ? "<" + string.Join(", ", method.TypeArguments.Select(TypeName)) + ">" : "");
                if (method.IsStatic)
                    target = SyntaxFactory.ParseExpression(TypeName(method.ContainingType) + "." + name);
                else if (node.Expression is MemberAccessExpressionSyntax member)
                {
                    if (OpenOperand(member.Expression) || method.ContainingType.IsValueType && !method.IsReadOnly)
                        return Fail(node);
                    target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        Cast(method.ContainingType, (ExpressionSyntax)Visit(member.Expression)), (SimpleNameSyntax)SyntaxFactory.ParseName(name));
                }
                else
                    return Fail(node);
            }
            var arguments = RewriteArguments(node.ArgumentList, operation.Arguments, method.Parameters);
            return node.WithExpression(target).WithArgumentList(arguments);
        }

        /// <summary>Keeps constructor overload selection and rejects type-parameter construction.</summary>
        /// <param name="node">The original object creation.</param>
        /// <returns>The qualified concrete construction.</returns>
        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            if (_model.GetOperation(node) is not IObjectCreationOperation operation || operation.Constructor == null ||
                _model.GetTypeInfo(node.Type).Type is ITypeParameterSymbol || !Accessible(operation.Constructor))
                return Fail(node);
            var constructor = Close(operation.Constructor);
            if (constructor == null)
                return Fail(node);
            return node.WithType(SyntaxFactory.ParseTypeName(TypeName(Resolve(constructor.ContainingType))))
                .WithArgumentList(RewriteArguments(node.ArgumentList ?? SyntaxFactory.ArgumentList(), operation.Arguments, constructor.Parameters))
                .WithInitializer((InitializerExpressionSyntax)Visit(node.Initializer));
        }

        /// <summary>Qualifies members without allowing specialization to change an implicit receiver.</summary>
        /// <param name="node">The original member access.</param>
        /// <returns>The qualified member access.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;
            if (symbol is ITypeSymbol type)
                return SyntaxFactory.ParseExpression(TypeName(Resolve(type))).WithTriviaFrom(node);
            if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol)
            {
                if (!Accessible(symbol))
                    return Fail(node);
                if (symbol.IsStatic)
                    return SyntaxFactory.ParseExpression(TypeName(Resolve(symbol.ContainingType)) + "." + Escape(symbol.Name)).WithTriviaFrom(node);
            }
            return node.WithExpression((ExpressionSyntax)Visit(node.Expression));
        }

        /// <summary>Replaces type parameters and removes dependence on the source file's imports.</summary>
        /// <param name="node">The original identifier.</param>
        /// <returns>The closed identifier or qualified static member.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;
            if (node.IsVar)
                return node;
            if (symbol is ITypeSymbol type)
                return SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node);
            if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol)
            {
                if (!symbol.IsStatic || !Accessible(symbol))
                    return Fail(node);
                return SyntaxFactory.ParseExpression(TypeName(Resolve(symbol.ContainingType)) + "." + Escape(symbol.Name)).WithTriviaFrom(node);
            }
            if (symbol is IMethodSymbol)
                return Fail(node);
            return node;
        }

        /// <summary>Closes complete named types rather than individual syntax fragments.</summary>
        /// <param name="node">The original generic name.</param>
        /// <returns>The closed qualified type.</returns>
        public override SyntaxNode VisitGenericName(GenericNameSyntax node)
            => _model.GetSymbolInfo(node).Symbol is ITypeSymbol type
                ? SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node) : Fail(node);

        /// <summary>Closes an entire qualified type name in one substitution.</summary>
        /// <param name="node">The original qualified name.</param>
        /// <returns>The closed type name.</returns>
        public override SyntaxNode VisitQualifiedName(QualifiedNameSyntax node)
            => _model.GetSymbolInfo(node).Symbol is ITypeSymbol type
                ? SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node) : base.VisitQualifiedName(node);

        /// <summary>Rejects operators whose binding could change when an open operand becomes concrete.</summary>
        /// <param name="node">The original binary operation.</param>
        /// <returns>The rewritten operation, or an opaque boundary.</returns>
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
            => OpenOperand(node.Left) || OpenOperand(node.Right) ? Fail(node) : base.VisitBinaryExpression(node);

        /// <summary>Retains open unary operators on their original generic path.</summary>
        /// <param name="node">The original prefix operation.</param>
        /// <returns>The rewritten operation, or an opaque boundary.</returns>
        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            => OpenOperand(node.Operand) ? Fail(node) : base.VisitPrefixUnaryExpression(node);

        /// <summary>Retains open increment operators on their original generic path.</summary>
        /// <param name="node">The original postfix operation.</param>
        /// <returns>The rewritten operation, or an opaque boundary.</returns>
        public override SyntaxNode VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
            => OpenOperand(node.Operand) ? Fail(node) : base.VisitPostfixUnaryExpression(node);

        /// <summary>Rejects compound assignment that could bind a different closed operator.</summary>
        /// <param name="node">The original assignment.</param>
        /// <returns>The rewritten assignment, or an opaque boundary.</returns>
        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
            => !node.IsKind(SyntaxKind.SimpleAssignmentExpression) && (OpenOperand(node.Left) || OpenOperand(node.Right))
                ? Fail(node) : base.VisitAssignmentExpression(node);

        /// <summary>Retains generic enumeration whose concrete type could select another enumerator.</summary>
        /// <param name="node">The original enumeration.</param>
        /// <returns>The rewritten loop, or an opaque boundary.</returns>
        public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
            => OpenOperand(node.Expression) ? Fail(node) : base.VisitForEachStatement(node);

        /// <summary>Checks a source operand before closing its generic type.</summary>
        /// <param name="node">The original expression.</param>
        /// <returns>Whether substitution could change operator binding.</returns>
        private bool OpenOperand(ExpressionSyntax node)
        {
            var type = _model.GetTypeInfo(node).Type;
            return type != null && (type.TypeKind == TypeKind.Dynamic || LuminPackExtensionGenerator.ContainsTypeParameter(type));
        }

        /// <summary>Marks unsupported relocation as a generic boundary without altering the original source.</summary>
        /// <param name="node">The original node.</param>
        /// <returns>The unchanged node.</returns>
        private SyntaxNode Fail(SyntaxNode node)
        {
            Valid = false;
            return node;
        }

        /// <summary>Substitutes all method and containing-type arguments together.</summary>
        /// <param name="type">The original type.</param>
        /// <returns>The concrete type.</returns>
        private ITypeSymbol Resolve(ITypeSymbol type) => ReachabilityAnalysisCache.Substitute(_owner._binding, type, _arguments);

        /// <summary>Checks whether a relocated helper can legally reference a source symbol.</summary>
        /// <param name="symbol">The referenced symbol.</param>
        /// <returns>Whether the generated assembly-level helper has access.</returns>
        private bool Accessible(ISymbol symbol) => _owner._binding.IsSymbolAccessibleWithin(symbol, _owner._binding.Assembly);

        /// <summary>Reconstructs a selected method in its substituted declaring type.</summary>
        /// <param name="method">The original bound method.</param>
        /// <returns>The same definition with closed arguments.</returns>
        private IMethodSymbol Close(IMethodSymbol method)
        {
            var containing = Resolve(method.ContainingType) as INamedTypeSymbol;
            var definition = containing?.GetMembers(method.Name).OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));
            return definition == null ? null : method.IsGenericMethod
                ? definition.Construct(method.TypeArguments.Select(Resolve).ToArray()) : definition;
        }

        /// <summary>Preserves source evaluation order and the original parameter conversions.</summary>
        /// <param name="syntax">The explicit source arguments.</param>
        /// <param name="operations">The bound explicit and default arguments.</param>
        /// <param name="parameters">The constructed method parameters.</param>
        /// <returns>The explicitly bound argument list.</returns>
        private ArgumentListSyntax RewriteArguments(ArgumentListSyntax syntax,
            System.Collections.Immutable.ImmutableArray<IArgumentOperation> operations,
            System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters)
        {
            var result = new List<ArgumentSyntax>();
            foreach (var argument in syntax.Arguments)
            {
                var operation = operations.FirstOrDefault(item => item.Syntax == argument);
                if (operation?.Parameter == null || operation.ArgumentKind == ArgumentKind.ParamArray)
                {
                    Valid = false;
                    return syntax;
                }
                var parameter = parameters[operation.Parameter.Ordinal];
                var expression = (ExpressionSyntax)Visit(argument.Expression);
                if (parameter.RefKind == RefKind.None)
                    expression = Cast(parameter.Type, expression);
                result.Add(argument.WithExpression(expression));
            }
            foreach (var operation in operations.Where(item => item.IsImplicit))
            {
                if (operation.ArgumentKind != ArgumentKind.DefaultValue || operation.Parameter == null || !operation.Value.ConstantValue.HasValue)
                {
                    Valid = false;
                    return syntax;
                }
                object constant = operation.Value.ConstantValue.Value;
                string literal = constant == null ? "default(" + TypeName(parameters[operation.Parameter.Ordinal].Type) + ")"
                    : Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(constant, quoteStrings: true, useHexadecimalNumbers: false);
                result.Add(SyntaxFactory.Argument(Cast(parameters[operation.Parameter.Ordinal].Type, SyntaxFactory.ParseExpression(literal)))
                    .WithNameColon(SyntaxFactory.NameColon(Escape(operation.Parameter.Name))));
            }
            return syntax.WithArguments(SyntaxFactory.SeparatedList(result));
        }

        /// <summary>Freezes the originally selected implicit conversion before overload resolution.</summary>
        /// <param name="type">The original constructed parameter type.</param>
        /// <param name="expression">The rewritten argument.</param>
        /// <returns>A parenthesized conversion expression.</returns>
        private static ExpressionSyntax Cast(ITypeSymbol type, ExpressionSyntax expression)
            => SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(TypeName(type)),
                SyntaxFactory.ParenthesizedExpression(expression)));
    }
}
