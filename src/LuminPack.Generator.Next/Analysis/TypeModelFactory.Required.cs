using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        private const string RequiredMemberAttribute = "System.Runtime.CompilerServices.RequiredMemberAttribute";
        private const string SetsRequiredMembersAttribute = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";
        private readonly Dictionary<INamedTypeSymbol, ISymbol[]> _requiredMembers =
            new Dictionary<INamedTypeSymbol, ISymbol[]>(SymbolEqualityComparer.Default);

        /// <summary>Finds required members using syntax on Roslyn hosts that lack the symbol property.</summary>
        /// <param name="type">The type being constructed.</param>
        /// <returns>The required members from its inherited and declared contracts.</returns>
        private ISymbol[] RequiredMembers(INamedTypeSymbol type)
        {
            lock (_gate)
                if (_requiredMembers.TryGetValue(type, out ISymbol[]? cached))
                    return cached;

            List<ISymbol>? members = null;
            Dictionary<string, int>? slots = null;
            foreach (INamedTypeSymbol level in Levels(type))
            {
                if (!MayDeclareRequiredMembers(level))
                    continue;
                foreach (ISymbol member in level.GetMembers())
                {
                    if (!IsRequiredMember(member))
                        continue;
                    slots ??= new Dictionary<string, int>(StringComparer.Ordinal);
                    members ??= new List<ISymbol>();
                    if (slots.TryGetValue(member.Name, out int slot))
                        members[slot] = member;
                    else
                    {
                        slots.Add(member.Name, members.Count);
                        members.Add(member);
                    }
                }
            }
            ISymbol[] result = members?.ToArray() ?? Array.Empty<ISymbol>();
            lock (_gate)
                _requiredMembers[type] = result;
            return result;
        }

        /// <summary>Checks type metadata or direct source modifiers before inspecting individual symbols.</summary>
        /// <param name="type">One declaration in the inheritance chain.</param>
        /// <returns>Whether the declaration could introduce a required member.</returns>
        private static bool MayDeclareRequiredMembers(INamedTypeSymbol type)
        {
            if (type.DeclaringSyntaxReferences.IsEmpty)
                return HasAttribute(type, RequiredMemberAttribute);
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not TypeDeclarationSyntax declaration)
                    continue;
                foreach (MemberDeclarationSyntax member in declaration.Members)
                    if (member is PropertyDeclarationSyntax property && property.Modifiers.Any(token => token.ValueText == "required") ||
                        member is FieldDeclarationSyntax field && field.Modifiers.Any(token => token.ValueText == "required"))
                        return true;
            }
            return false;
        }

        /// <summary>Checks that a fresh generated read can satisfy the required members without bypassing constructor semantics.</summary>
        /// <param name="type">The declaration being diagnosed.</param>
        /// <param name="members">The filtered serialized storage.</param>
        /// <param name="mode">The declaration's serialization mode.</param>
        /// <param name="diagnostics">Receives unsafe construction diagnostics.</param>
        private void ValidateRequiredConstruction(INamedTypeSymbol type, List<ISymbol> members, PayloadGeneration mode,
            ref DeclarationDiagnosticBag diagnostics)
        {
            if (mode == PayloadGeneration.LegacyManual)
                return;
            ISymbol[] required = RequiredMembers(type);
            if (required.Length == 0 || TracksTypeReferences(type))
                return;
            AnalyzeRequiredConstruction(type, required, mode, Rent(type), members.Count, name => members.Any(member => member.Name == name),
                out ISymbol? member, out string? issue);
            if (member != null)
                diagnostics.Add(DiagnosticDescriptors.RequiredMemberConstruction, member, member.Name, type.Name, issue);
        }

        /// <summary>Models the initializer assignments for fresh reads, sharing the same checks as declaration diagnostics.</summary>
        /// <param name="type">The owned or imported payload.</param>
        /// <param name="members">The complete serialized member models.</param>
        /// <param name="mode">The effective serialization mode.</param>
        /// <param name="pooled">Whether a rent method supplies the instance.</param>
        /// <param name="issues">Receives unsupported construction facts for unpublished models.</param>
        /// <returns>The explicit initializer assignments, or null when none are needed.</returns>
        private RequiredConstructionModel? RequiredInitialization(INamedTypeSymbol type, MemberModel[] members, PayloadGeneration mode,
            bool pooled, List<string> issues)
        {
            ISymbol[] required = RequiredMembers(type);
            if (required.Length == 0 || TracksTypeReferences(type))
                return null;
            RequiredConstructionModel? result = AnalyzeRequiredConstruction(type, required, mode, pooled, members.Length,
                name => Array.Exists(members, member => member.Name == name), out ISymbol? member, out string? issue);
            if (member != null)
                issues.Add("required member '" + member.Name + "': " + issue);
            return result;
        }

        /// <summary>Determines which existing construction paths need explicit C# initializers.</summary>
        /// <param name="type">The payload to construct.</param>
        /// <param name="required">The required member symbols.</param>
        /// <param name="mode">The selected serialization mode.</param>
        /// <param name="pooled">Whether a rental ordinarily supplies the value.</param>
        /// <param name="memberCount">The number of serialized members.</param>
        /// <param name="serialized">Whether the required member is serialized.</param>
        /// <param name="failedMember">The first unsupported required member.</param>
        /// <param name="issue">The reason its constructor must own initialization.</param>
        /// <returns>The construction facts, or null if initialization is unnecessary or unsupported.</returns>
        private RequiredConstructionModel? AnalyzeRequiredConstruction(INamedTypeSymbol type, ISymbol[] required, PayloadGeneration mode,
            bool pooled, int memberCount, Func<string, bool> serialized, out ISymbol? failedMember, out string? issue)
        {
            failedMember = null;
            issue = null;
            IMethodSymbol? selected = SelectConstructor(type, out _);
            IMethodSymbol? parameterless = type.InstanceConstructors.FirstOrDefault(constructor => constructor.Parameters.Length == 0);
            bool sequence = memberCount == 0 && mode == PayloadGeneration.Default && SequenceBase(type) != null;
            bool useDefaults = (type.IsValueType || mode == PayloadGeneration.Default &&
                (!pooled || sequence) && (selected == null || selected.Parameters.IsEmpty)) && !SetsRequired(parameterless);
            bool useDecoded = !pooled && !sequence && !SetsRequired(selected ?? parameterless);
            if (!useDefaults && !useDecoded)
                return null;

            // Early reads may already observe constructor values through callbacks, reference reuse, or partial publication.
            // The generated defaults are equivalent only when required storage starts at its zero-initialized value.
            bool canInitializeDefaults = !useDefaults || Levels(type).All(level => level.InstanceConstructors.Any(constructor =>
                constructor.Parameters.IsEmpty && constructor.IsImplicitlyDeclared));
            foreach (ISymbol member in required)
            {
                if (!serialized(member.Name) || member is IPropertySymbol property && !HasAutoRequiredSetter(property))
                    issue = "the member is not writable serialized storage";
                else if (useDecoded && selected != null && selected.Parameters.Any(parameter =>
                    string.Equals(parameter.Name, member.Name, StringComparison.OrdinalIgnoreCase)))
                    issue = "the selected constructor owns this member's value";
                else if (useDefaults && (!canInitializeDefaults || HasRequiredStorageInitializer(member)))
                    issue = "the fresh read must preserve values established before its member reads";
                if (issue != null)
                {
                    failedMember = member;
                    return null;
                }
            }

            string[] names = required.Select(member => member.Name).ToArray();
            return new RequiredConstructionModel(useDefaults ? names : Array.Empty<string>(), useDecoded ? names : Array.Empty<string>());
        }

        /// <summary>Whether the selected constructor guarantees the required members itself.</summary>
        /// <param name="constructor">The selected constructor.</param>
        /// <returns>Whether C# does not require an initializer for that constructor.</returns>
        private static bool SetsRequired(IMethodSymbol? constructor) =>
            constructor != null && HasAttribute(constructor, SetsRequiredMembersAttribute);

        /// <summary>Checks that assigning the required property initializes its field without calling application code.</summary>
        /// <param name="property">The required property.</param>
        /// <returns>Whether the renderer may safely assign the property in an initializer.</returns>
        private bool HasAutoRequiredSetter(IPropertySymbol property)
        {
            IMethodSymbol? setter = property.SetMethod;
            return IsAutoProperty(property) && setter != null &&
                _compilation.IsSymbolAccessibleWithin(setter, _compilation.Assembly) &&
                (setter.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is AccessorDeclarationSyntax
                    { Body: null, ExpressionBody: null }) ||
                    setter.DeclaringSyntaxReferences.IsEmpty &&
                    HasAttribute(setter, "System.Runtime.CompilerServices.CompilerGeneratedAttribute"));
        }

        /// <summary>Checks whether a serialized required member has a declaration initializer.</summary>
        /// <param name="member">The serialized storage symbol.</param>
        /// <returns>Whether applying a default initializer would discard an earlier value.</returns>
        private static bool HasRequiredStorageInitializer(ISymbol member) => member.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null } or PropertyDeclarationSyntax { Initializer: not null });
    }
}
