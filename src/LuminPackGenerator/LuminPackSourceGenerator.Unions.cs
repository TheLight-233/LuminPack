using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator
{
        public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {

        /// <summary>
        /// Discovers derived types in the current assembly and assigns deterministic union tags.
        /// </summary>
        /// <param name="baseTypeSymbol">The base type symbol used by this operation.</param>
        /// <param name="dataInfo">The metadata describing the serializable type.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        private static void DiscoverAndRegisterDerivedTypes(
            INamedTypeSymbol baseTypeSymbol,
            LuminDataInfo dataInfo,
            Compilation compilation) => LuminPackDiscovery.Run(baseTypeSymbol, dataInfo, compilation);

        /// <summary>
        /// Detects a .NET 11 / C# 15 union.  Two forms are recognized:
        /// 1. An explicit <c>[System.Runtime.CompilerServices.Union]</c> attribute on a struct (the
        ///    custom-union pattern) — the attribute and case constructors are visible to the generator.
        /// 2. A <c>union Pet(Cat, Dog);</c> declaration, whose <c>UnionDeclarationSyntax</c> reaches
        ///    the generator as a <c>TypeDeclarationSyntax</c> carrying a <c>union</c> keyword.  Its
        ///    synthesized <c>[Union]</c> attribute and constructors are added during lowering (after
        ///    generators run), so it is detected from syntax and the case types are read from the
        ///    parameter list.
        /// The <c>System.Runtime.CompilerServices.UnionAttribute</c> metadata type exists only on
        /// .NET 11+, which is the high/low version gate: older TFMs never satisfy this branch.
        /// </summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private static bool IsCs11UnionStruct(INamedTypeSymbol type, Compilation compilation)
        {
            INamedTypeSymbol? unionAttribute = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute");
            if (unionAttribute is not null &&
                type.GetAttributes().Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, unionAttribute)))
            {
                return true;
            }

            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                SyntaxNode node = reference.GetSyntax();
                if (node is TypeDeclarationSyntax tds &&
                    (tds.Keyword.Text == "union" || tds.Modifiers.Any(static m => m.Text == "union")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collects the case types of a .NET 11 union.  Prefers the compiler-generated case
        /// constructors (visible for the explicit <c>[Union]</c> form).  For <c>union</c> declarations
        /// the constructors are synthesized after generator execution, so the case types are read from
        /// the declaration's parameter list instead.
        /// </summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <param name="dataInfo">The metadata describing the serializable type.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        private static void CollectCs11UnionStructMembers(INamedTypeSymbol type, LuminDataInfo dataInfo, Compilation compilation)
        {
            ushort tag = 0;
            var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

            foreach (IMethodSymbol ctor in type.InstanceConstructors)
            {
                if (ctor.DeclaredAccessibility != Accessibility.Public || ctor.Parameters.Length != 1)
                {
                    continue;
                }

                if (ctor.Parameters[0].Type is not INamedTypeSymbol caseType ||
                    !seen.Add(caseType))
                {
                    continue;
                }

                dataInfo.UnionMembers.Add(new LuminUnionMemberInfo(tag++, caseType));
            }

            if (tag == 0)
            {
                CollectCs11UnionCaseTypesFromSyntax(type, dataInfo, compilation, seen);
            }

            dataInfo.IsWideTag = tag > 249;
        }

        /// <summary>Performs the collect cs11 union case types from syntax step for the current operation.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <param name="dataInfo">The metadata describing the serializable type.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        /// <param name="seen">The seen used by this operation.</param>
        private static void CollectCs11UnionCaseTypesFromSyntax(
            INamedTypeSymbol type,
            LuminDataInfo dataInfo,
            Compilation compilation,
            HashSet<ITypeSymbol> seen)
        {
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                SyntaxNode node = reference.GetSyntax();
                if (node is not TypeDeclarationSyntax tds ||
                    (tds.Keyword.Text != "union" && !tds.Modifiers.Any(static m => m.Text == "union")))
                {
                    continue;
                }

                // UnionDeclarationSyntax carries the case types in a parameter list; reach it through
                // reflection so the generator (built against older Roslyn) works in the .NET 11 host.
                object? parameterList = node.GetType().GetProperty("ParameterList")?.GetValue(node);
                object? parameters = parameterList?.GetType().GetProperty("Parameters")?.GetValue(parameterList);
                if (parameters is not System.Collections.IEnumerable enumerable)
                {
                    continue;
                }

                SemanticModel model = compilation.GetSemanticModel(node.SyntaxTree);
                foreach (object? parameter in enumerable)
                {
                    object? typeSyntax = parameter?.GetType().GetProperty("Type")?.GetValue(parameter);
                    if (typeSyntax is not SyntaxNode typeNode)
                    {
                        continue;
                    }

                    if (model.GetTypeInfo(typeNode).Type is not INamedTypeSymbol caseType ||
                        !seen.Add(caseType))
                    {
                        continue;
                    }

                    dataInfo.UnionMembers.Add(new LuminUnionMemberInfo((ushort)dataInfo.UnionMembers.Count, caseType));
                }
            }
        }
    }
}
