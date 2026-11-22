using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    /// <summary>Exports the native layout models of a raw struct through the opt-in C++ contract path.</summary>
    /// <param name="model">The existing normalized struct contract.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The normalized project facts.</param>
    /// <returns>The layout strategy and native models, or null when the C++ transfer cannot be proven.</returns>
    internal static (int Strategy, FormatterStorageModel Layout)? TryCreateCppNativeStorage(
        StructModel model, Compilation compilation, GeneratorOptions options)
    {
        if (model.Storage != StructStorage.RawUnmanaged || model.Identity.IsGeneric)
            return null;

        string metadataName = model.Identity.MetadataName;
        // Closed generic contract identities use C# type arguments, not Roslyn metadata lookup names.
        if (metadataName.IndexOf('<') >= 0)
            return null;

        Compilation view = Complete(compilation);
        var candidates = view.GetTypesByMetadataName(metadataName);
        if (candidates.Length != 1 || !SymbolEqualityComparer.Default.Equals(candidates[0].ContainingAssembly, view.Assembly))
            return null;

        Session session = Sessions.GetValue(view, item => new Session(item, options));
        if (!session.Options.Equals(options))
            session = new Session(view, options);
        return session.CppNativeStorage(candidates[0], model);
    }

    private sealed partial class Session
    {
        /// <summary>Normalizes existing CLR layout models without changing the struct's selected wire path.</summary>
        /// <param name="symbol">The resolved local struct.</param>
        /// <param name="model">The struct's existing serialized members.</param>
        /// <returns>The tag-free native C++ storage facts, or null when they are unsupported.</returns>
        internal (int Strategy, FormatterStorageModel Layout)? CppNativeStorage(INamedTypeSymbol symbol, StructModel model)
        {
            int strategy = LayoutStrategy(symbol);
            if (strategy is not (0 or 2) || AnalyzePortable(symbol, strict: true) is not { Fields: { Length: > 0 } fields } plan ||
                !CanExportCppNativeStorage(plan))
                return null;

            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemberModel member in model.Members)
                if (member.Compressed || !members.Add(member.Name))
                    return null;
            if (fields.Length != members.Count || fields.Any(field => !members.Contains(field.Name)))
                return null;

            // For sequential storage BuildModels yields the 64-bit and 32-bit placements in that order,
            // retaining one model when their stride and byte offsets are identical.
            plan.Models = BuildModels(plan);
            if (plan.Models.Length == 0 || strategy == 0 && plan.Models.Length != 1)
                return null;
            foreach (PortableStorageModel layout in plan.Models)
            {
                var offsets = new HashSet<int>();
                if (layout.Size <= 0 || layout.Offsets.Length != plan.WireSize ||
                    layout.Offsets.Any(offset => (uint)offset >= (uint)layout.Size || !offsets.Add(offset)))
                    return null;
            }
            return (strategy, NormalizeStorage(plan));
        }

        /// <summary>Restricts raw copies to sequential fields and supported scalar representations.</summary>
        /// <param name="plan">The candidate CLR layout.</param>
        /// <returns>Whether every field is exposed and its wire bytes can be copied by C++.</returns>
        private bool CanExportCppNativeStorage(PortablePlan plan)
        {
            if (plan.Fields == null)
            {
                ITypeSymbol type = plan.Type is INamedTypeSymbol { EnumUnderlyingType: { } underlying } ? underlying : plan.Type;
                return type.SpecialType != SpecialType.System_Boolean && PortableScalarSize(type) is 1 or 2 or 4 or 8;
            }
            if (plan.Kind != 0 || plan.Type is not INamedTypeSymbol named || !named.IsDefinition || plan.Fields.Length == 0 ||
                !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, _compilation.Assembly))
                return false;
            if (named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == StructLayoutAttribute &&
                (attribute.ConstructorArguments.Length == 0 || attribute.ConstructorArguments[0].Value is not int)))
                return false;

            IFieldSymbol[] fields = StorageFields(named);
            if (fields.Length != plan.Fields.Length)
                return false;
            for (int index = 0; index < fields.Length; index++)
            {
                IFieldSymbol field = fields[index];
                if (field.DeclaredAccessibility != Accessibility.Public || field.IsImplicitlyDeclared || field.IsVolatile ||
                    HasAttribute(field, IgnoreAttribute) || field.Name != plan.Fields[index].Name ||
                    !CanExportCppNativeStorage(plan.Children![index]))
                    return false;
            }
            return true;
        }
    }
}
