using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Captures optional formatter APIs as immutable names after validating their actual public signatures.</summary>
internal static class FormatterCapabilities
{
    /// <summary>Captures capabilities for one closed family independently of its rendering template.</summary>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "modern">The modern used by this operation.</param>
    /// <param name = "unity">The unity used by this operation.</param>
    /// <returns>The normalized result of the operation.</returns>
    internal static string[] Capture(Compilation compilation, ITypeSymbol type, bool modern, bool unity)
    {
        var result = new List<string>();
        FormatterBuiltinCapabilities.Capture(compilation, type, result);
        if (type is not INamedTypeSymbol { IsGenericType: true } named)
            return result.ToArray();
        string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name;
        if (definition.StartsWith("System.Collections.Immutable.", StringComparison.Ordinal))
        {
            string concrete = named.Name.StartsWith("IImmutable", StringComparison.Ordinal) ? named.Name.Substring(1) : named.Name;
            if (concrete == "ImmutableSet")
                concrete = "ImmutableHashSet";
            if (concrete.StartsWith("Immutable", StringComparison.Ordinal) && FormatterImmutableTraits.Available(compilation, concrete.Substring(9), named.Arity))
                result.Add("ImmutableBuilder");
            if (concrete is "ImmutableQueue" or "ImmutableList" && FormatterRuntimeTraits.CanCreateImmutableSequenceFromSpan(compilation, concrete))
                result.Add("ImmutableSpan");
        }

        if (definition == "System.Collections.Generic.Dictionary" && named.TypeArguments[0].SpecialType == SpecialType.System_String && FormatterRuntimeTraits.HasEnumeratedStringKeyCheck(compilation))
            result.Add("EnumeratedStringKey");
        if (definition == "System.Collections.Generic.Dictionary" && modern && !unity && CallbackFree(named.TypeArguments[0]))
        {
            ITypeSymbol value = named.TypeArguments[1];
            if (value.SpecialType == SpecialType.System_String || CallbackFree(value))
                result.Add("DictionaryScalarSlots");
            else if (Leaf(value, compilation))
                result.Add("DictionaryLeafSlots");
            else if (value is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } root)
            {
                // As the original slot plan, an abstract class Union root refills its existing exact fixed cases in place; the
                // slot emitter selects those cases and every other value takes the staged continuation.
                var cases = root.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute").ToArray();
                if (cases.Any(attribute => attribute.ConstructorArguments.Length >= 2 && attribute.ConstructorArguments[0].Value is ushort id && id < 250 && attribute.ConstructorArguments[1].Value is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false }))
                    result.Add("DictionaryUnionSlots");
            }
        }

        if (definition is "System.Collections.Generic.IEnumerable" or "System.Collections.Generic.ICollection" or "System.Collections.Generic.IReadOnlyCollection" or "System.Collections.Generic.IList" or "System.Collections.Generic.IReadOnlyList")
        {
            ITypeSymbol element = named.TypeArguments[0];
            bool ordinary = element is INamedTypeSymbol { TypeKind: TypeKind.Class } payload && payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackableAttribute" && (attribute.ConstructorArguments.Length == 0 || Convert.ToInt32(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture) == 0)) && !payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute");
            if (element.SpecialType == SpecialType.System_String || element.IsUnmanagedType && (element.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) || ordinary)
                result.Add("InterfaceTargetReuse");
        }

        if (definition == "System.Collections.Generic.HashSet" && FormatterRuntimeTraits.CanGroupSmallHashSetWrites(named.TypeArguments[0], compilation))
            result.Add("SmallHashSetWrite");
        if (definition == "System.Collections.Generic.SortedSet")
        {
            if (FormatterRuntimeTraits.CanBuildSortedSet(named.TypeArguments[0], compilation))
                result.Add("BuildSortedSet");
            if (FormatterRuntimeTraits.CanRebuildSortedSet(named.TypeArguments[0], compilation))
                result.Add("RebuildSortedSet");
        }

        if (definition == "System.Collections.Generic.SortedList")
        {
            if (FormatterRuntimeTraits.CanAppendSortedListOrdered(named.TypeArguments[0], compilation))
                result.Add("AppendSortedList");
            ITypeSymbol key = named.TypeArguments[0];
            if (key.SpecialType == SpecialType.System_String || key.TypeKind == TypeKind.Enum || key.IsUnmanagedType && (key.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))
                result.Add("MatchSortedListKey");
        }

        if (definition == "System.Collections.Concurrent.ConcurrentBag")
        {
            if (FormatterConcurrentTraits.HasBagSnapshot(compilation, false))
                result.Add("BagSnapshot");
            if (FormatterConcurrentTraits.HasBagSnapshot(compilation, true))
                result.Add("BagRawWrite");
        }

        if (definition == "System.Collections.Concurrent.ConcurrentStack" && FormatterConcurrentTraits.CanStageStack(compilation, named.TypeArguments[0]))
            result.Add("StageConcurrentStack");
        if (definition is "System.Collections.Concurrent.ConcurrentQueue" or "System.Collections.Concurrent.ConcurrentStack" or "System.Collections.Concurrent.ConcurrentBag"
            && FormatterConcurrentTraits.HasJsonIntegerElement(compilation, named.TypeArguments[0]))
            result.Add("ConcurrentJsonIntegerElement");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" &&
            FormatterConcurrentTraits.HasJsonIntegerPairs(compilation))
            result.Add("ConcurrentJsonIntegerPair");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsUnmanagedType &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("ConcurrentBinaryTextPair");
        if (definition.StartsWith("System.Collections.Concurrent.Concurrent", StringComparison.Ordinal) && FormatterConcurrentTraits.Available(compilation, named.Name, named.Arity))
            result.Add("ConcurrentTraversal");
        if (definition == "System.Collections.Frozen.FrozenSet" && FormatterFrozenTraits.Available(compilation, "Set", 1))
            result.Add("RentFrozenSet");
        if (definition == "System.Collections.Frozen.FrozenDictionary")
        {
            if (FormatterFrozenTraits.Available(compilation, "Dictionary", 2))
                result.Add("RentFrozenDictionary");
            if (FormatterFrozenTraits.CanWritePairs(compilation))
                result.Add("FrozenRawPairs");
        }

        return result.ToArray();
    }

    /// <summary>Recognizes a callback-free fixed framework key or a raw enum.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    private static bool CallbackFree(ITypeSymbol type) => type is INamedTypeSymbol { IsValueType: true, IsGenericType: false } && (type.TypeKind == TypeKind.Enum || type.IsUnmanagedType && (type.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));
    /// <summary>Certifies a nonnull sealed leaf whose read cannot invoke user code while a dictionary entry ref is retained.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    private static bool Leaf(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } named || named.BaseType?.SpecialType != SpecialType.System_Object || !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly))
            return false;
        var attributes = named.GetAttributes();
        var packable = attributes.FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackableAttribute");
        if (packable == null || packable.ConstructorArguments.Length != 0 && Convert.ToInt32(packable.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture)is not (0 or 1) || attributes.Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute"))
            return false;
        var fields = named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic && !field.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIgnoreAttribute")).ToArray();
        return fields.Length != 0 && fields.All(field => !field.IsImplicitlyDeclared && !field.IsVolatile && (field.Type.SpecialType == SpecialType.System_String || field.Type.IsUnmanagedType && field.Type.SpecialType != SpecialType.None) && !field.GetAttributes().Any(attribute => attribute.AttributeClass?.Name.IndexOf("Compress", StringComparison.Ordinal) >= 0)) && !named.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute => attribute.AttributeClass?.Name.StartsWith("LuminPackOn", StringComparison.Ordinal) == true && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute"));
    }
}
