using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        private readonly Dictionary<INamedTypeSymbol, UnionCaseDeclaration[]> _unionDeclarations =
            new Dictionary<INamedTypeSymbol, UnionCaseDeclaration[]>(SymbolEqualityComparer.Default);
        private INamedTypeSymbol[]? _unionCandidates;

        /// <summary>Collects explicit cases and deterministic source-discovered cases once for each Union root.</summary>
        /// <param name="root">The Union root.</param>
        /// <returns>The declared and discovered cases in legacy registration order.</returns>
        private UnionCaseDeclaration[] UnionCaseDeclarations(INamedTypeSymbol root)
        {
            lock (_gate)
            {
                if (_unionDeclarations.TryGetValue(root, out UnionCaseDeclaration[]? cached))
                    return cached;
                var cases = new List<UnionCaseDeclaration>();
                var tags = new HashSet<int>();
                foreach (AttributeData attribute in root.GetAttributes())
                    if (attribute.AttributeClass?.ToDisplayString() == UnionAttribute && attribute.ConstructorArguments.Length >= 2 &&
                        attribute.ConstructorArguments[0].Value != null && attribute.ConstructorArguments[1].Value is INamedTypeSymbol member)
                    {
                        int tag = Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
                        cases.Add(new UnionCaseDeclaration(tag, member, false));
                        tags.Add(tag);
                    }
                // A consumer cannot repeat source discovery in the declaring assembly. Its dispatch tags come from metadata.
                if (!IsLocal(root))
                {
                    UnionCaseDeclaration[] referenced = cases.ToArray();
                    _unionDeclarations[root] = referenced;
                    return referenced;
                }
                if (_unionCandidates == null)
                {
                    var declarations = new List<INamedTypeSymbol>();
                    CollectUnionCandidates(_compilation.Assembly.GlobalNamespace, declarations);
                    // As the original discovery, a generic declaration contributes the closed constructions the source uses, and
                    // its open definition only when it is never constructed. The original sorted with the default comparer.
                    Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> closed = ClosedConstructions(declarations);
                    var candidates = new List<INamedTypeSymbol>();
                    foreach (INamedTypeSymbol declaration in declarations)
                        if (declaration.Arity != 0 && closed.TryGetValue(declaration, out List<INamedTypeSymbol>? constructions))
                            candidates.AddRange(constructions);
                        else
                            candidates.Add(declaration);
                    _unionCandidates = candidates.OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), Comparer<string>.Default)
                        .ToArray();
                }
                int nextTag = 0;
                foreach (INamedTypeSymbol candidate in _unionCandidates)
                {
                    if (candidate.IsAbstract || candidate.IsStatic || !ValidDerivedCase(candidate, root.OriginalDefinition) ||
                        cases.Any(item => SymbolEqualityComparer.Default.Equals(item.Type, candidate) ||
                            (item.Type.IsUnboundGenericType || ContainsTypeParameter(item.Type)) &&
                            SymbolEqualityComparer.Default.Equals(item.Type.OriginalDefinition, candidate.OriginalDefinition)))
                        continue;
                    while (tags.Contains(nextTag))
                        nextTag++;
                    cases.Add(new UnionCaseDeclaration(nextTag, candidate, true));
                    tags.Add(nextTag++);
                }
                UnionCaseDeclaration[] result = cases.ToArray();
                _unionDeclarations[root] = result;
                return result;
            }
        }

        /// <summary>Applies the original derived-case rule: a case of a closed base is fully closed, and a case of a base over type
        /// parameters is generic over exactly those parameters, in order, with the same constraints.</summary>
        private static bool ValidDerivedCase(INamedTypeSymbol candidate, INamedTypeSymbol root)
        {
            INamedTypeSymbol? matched = UnionRootMatch(candidate, root);
            if (matched == null)
                return false;
            if (root.Arity == 0)
                return true;
            if (matched.TypeArguments.All(argument => argument is not ITypeParameterSymbol))
                return !ContainsTypeParameter(candidate);
            if (candidate.TypeParameters.Length != root.Arity)
                return false;
            for (int index = 0; index < root.Arity; index++)
            {
                if (matched.TypeArguments[index] is not ITypeParameterSymbol parameter || parameter.Ordinal != index)
                    return false;
                ITypeParameterSymbol derived = candidate.TypeParameters[index], original = root.TypeParameters[index];
                if (derived.HasConstructorConstraint != original.HasConstructorConstraint ||
                    derived.HasReferenceTypeConstraint != original.HasReferenceTypeConstraint ||
                    derived.HasValueTypeConstraint != original.HasValueTypeConstraint ||
                    derived.HasUnmanagedTypeConstraint != original.HasUnmanagedTypeConstraint ||
                    derived.HasNotNullConstraint != original.HasNotNullConstraint ||
                    !System.Linq.Enumerable.SequenceEqual<ITypeSymbol>(derived.ConstraintTypes, original.ConstraintTypes, SymbolEqualityComparer.Default))
                    return false;
            }
            return true;
        }

        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>? _closedConstructions;

        /// <summary>Indexes the closed constructions of local generic packable declarations that the source names.</summary>
        /// <param name="declarations">The local packable declarations that can contribute a Union case.</param>
        /// <returns>The constructions of each generic definition, without duplicates.</returns>
        private Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> ClosedConstructions(List<INamedTypeSymbol> declarations)
        {
            if (_closedConstructions != null)
                return _closedConstructions;
            var result = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var names = new HashSet<(string Name, int Arity)>();
            foreach (INamedTypeSymbol declaration in declarations)
                if (declaration.Arity != 0 && CanContributeGenericCase(declaration))
                    names.Add((declaration.Name, declaration.Arity));
            if (names.Count == 0)
                return _closedConstructions = result;
            foreach (SyntaxTree tree in _compilation.SyntaxTrees)
            {
                SemanticModel? model = null;
                foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
                {
                    if (node is not GenericNameSyntax name || !names.Contains((name.Identifier.ValueText, name.Arity)))
                        continue;
                    SyntaxNode expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
                    if (expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression)
                        continue;
                    model ??= BindingSemanticModel.For(_compilation, tree);
                    if (model.GetSymbolInfo(name).Symbol is not INamedTypeSymbol { IsGenericType: true } type || ContainsTypeParameter(type) ||
                        !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly) || !IsPackable(type.OriginalDefinition))
                        continue;
                    Visit(type);
                }
            }
            _closedConstructions = result;
            return result;

            void Visit(INamedTypeSymbol type)
            {
                if (!seen.Add(type))
                    return;
                if (!result.TryGetValue(type.OriginalDefinition, out List<INamedTypeSymbol>? list))
                    result[type.OriginalDefinition] = list = new List<INamedTypeSymbol>();
                list.Add(type);
            }
        }

        /// <summary>Rejects generic definitions whose inheritance cannot contribute a local Union case.</summary>
        /// <param name="declaration">A source-owned packable definition.</param>
        /// <returns>Whether one of its base definitions can own the Union slot.</returns>
        private bool CanContributeGenericCase(INamedTypeSymbol declaration)
        {
            if (declaration.IsAbstract || declaration.IsStatic)
                return false;
            for (INamedTypeSymbol? parent = declaration.BaseType; parent != null; parent = parent.BaseType)
                if (SymbolEqualityComparer.Default.Equals(parent.ContainingAssembly, _compilation.Assembly) && IsPackable(parent.OriginalDefinition))
                    return true;
            foreach (INamedTypeSymbol contract in declaration.AllInterfaces)
                if (SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, _compilation.Assembly) && IsPackable(contract.OriginalDefinition))
                    return true;
            return false;
        }

        /// <summary>Indexes source-owned attributed types without revisiting syntax in each Union analysis.</summary>
        /// <param name="scope">The current namespace or enclosing type.</param>
        /// <param name="candidates">Receives candidate declarations.</param>
        private static void CollectUnionCandidates(INamespaceOrTypeSymbol scope, List<INamedTypeSymbol> candidates)
        {
            foreach (ISymbol member in scope.GetMembers())
            {
                if (member is INamespaceSymbol space)
                    CollectUnionCandidates(space, candidates);
                else if (member is INamedTypeSymbol type)
                {
                    if (IsPackable(type))
                        candidates.Add(type);
                    CollectUnionCandidates(type, candidates);
                }
            }
        }

        /// <summary>Checks Union roots, case identities, generic arities, derivation and injectable partial chains.</summary>
        /// <param name="type">The Union root.</param>
        /// <param name="diagnostics">Receives Union diagnostics and discovery information.</param>
        private void ValidateUnion(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            if (!CanGeneratePartial(type))
                diagnostics.Add(DiagnosticDescriptors.UnionParticipantMustBePartial, type, type.Name, type.Name);
            if (type.IsSealed)
                diagnostics.Add(DiagnosticDescriptors.SealedTypeCantBeUnion, type, type.Name);
            if (type.TypeKind == TypeKind.Struct)
                diagnostics.Add(DiagnosticDescriptors.UnionMemberNotAllowStruct, type, type.Name);
            if (!type.IsAbstract)
                diagnostics.Add(DiagnosticDescriptors.ConcreteTypeCantBeUnion, type, type.Name);
            UnionCaseDeclaration[] cases = UnionCaseDeclarations(type);
            if (cases.Length > 256)
                diagnostics.Add(DiagnosticDescriptors.TooManyUnionMembers, type, type.Name, cases.Length.ToString(CultureInfo.InvariantCulture));
            var tags = new HashSet<int>();
            foreach (UnionCaseDeclaration item in cases)
            {
                INamedTypeSymbol member = item.Type;
                if (item.Automatic)
                    diagnostics.Add(DiagnosticDescriptors.UnionMemberAutoDiscovered, member, member.Name, type.Name,
                        item.Tag.ToString(CultureInfo.InvariantCulture));
                else
                {
                    int arity = member.OriginalDefinition.TypeParameters.Length;
                    if (arity > type.TypeParameters.Length)
                        diagnostics.Add(DiagnosticDescriptors.UnionMemberGenericCountExceed, member, member.Name,
                            type.TypeParameters.Length.ToString(CultureInfo.InvariantCulture), arity.ToString(CultureInfo.InvariantCulture));
                    if (!tags.Add(item.Tag))
                        diagnostics.Add(DiagnosticDescriptors.UnionTagDuplicate, type, item.Tag.ToString(CultureInfo.InvariantCulture), type.Name);
                    if (!IsPackable(member))
                        diagnostics.Add(DiagnosticDescriptors.UnionMemberMustBeLuminPackable, member, member.Name);
                    if (UnionRootMatch(member, type.OriginalDefinition) == null)
                        diagnostics.Add(type.TypeKind == TypeKind.Interface ? DiagnosticDescriptors.UnionMemberTypeNotImplementBaseType :
                            DiagnosticDescriptors.UnionMemberTypeNotDerivedBaseType, member, member.Name, type.Name);
                }
                if (SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, _compilation.Assembly) && !CanGeneratePartial(member))
                    diagnostics.Add(DiagnosticDescriptors.UnionParticipantMustBePartial, member, member.Name, type.Name);
            }
        }

        /// <summary>Checks the legacy declared-member order requirements for circle-reference records.</summary>
        /// <param name="type">The circle declaration.</param>
        /// <param name="diagnostics">Receives missing and duplicate orders.</param>
        private static void ValidateCircleOrders(INamedTypeSymbol type, ref DeclarationDiagnosticBag diagnostics)
        {
            var orders = new Dictionary<uint, List<ISymbol>>();
            foreach (ISymbol member in type.GetMembers())
            {
                if (member.IsStatic || member is IFieldSymbol { IsConst: true } || member.IsImplicitlyDeclared)
                    continue;
                if (member is not (IFieldSymbol or IPropertySymbol) || HasAttribute(member, IgnoreAttribute) || InvalidOverrideAnnotation(member) != null)
                    continue;
                if (Order(member) is not uint order)
                {
                    diagnostics.Add(DiagnosticDescriptors.CircularReferenceAndVersionTolerantRequiredOrder, member, member.Name);
                    continue;
                }
                if (!orders.TryGetValue(order, out List<ISymbol>? entries))
                    orders.Add(order, entries = new List<ISymbol>());
                entries.Add(member);
            }
            foreach (KeyValuePair<uint, List<ISymbol>> pair in orders)
                if (pair.Value.Count > 1)
                    diagnostics.Add(DiagnosticDescriptors.CircularReferenceAndVersionTolerantDuplicateOrder, pair.Value[0],
                        pair.Key.ToString(CultureInfo.InvariantCulture), string.Join(", ", pair.Value.Select(member => member.Name)));
        }

        /// <summary>An analysis-only case declaration, never retained by incremental models.</summary>
        private readonly struct UnionCaseDeclaration
        {
            internal readonly int Tag;
            internal readonly INamedTypeSymbol Type;
            internal readonly bool Automatic;

            /// <summary>Captures the case identity and source of its tag.</summary>
            /// <param name="tag">The assigned binary tag.</param>
            /// <param name="type">The concrete case declaration.</param>
            /// <param name="automatic">Whether source discovery assigned the tag.</param>
            internal UnionCaseDeclaration(int tag, INamedTypeSymbol type, bool automatic)
            {
                Tag = tag;
                Type = type;
                Automatic = automatic;
            }
        }
    }
}
