using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>
/// Produces source-ready type names without losing the metadata identity of tuple types.
/// Roslyn 4.3 displays ValueTuple symbols using parenthesized tuple syntax, including when
/// they are nested in another generic type. Formatter lookup uses metadata type names, so
/// only graphs which contain a tuple need to be expanded explicitly.
/// </summary>
internal static class FormatterTypeName
{
	internal static string Get(ITypeSymbol type)
	{
		// Roslyn represents the native aliases as distinct symbols even though C#
		// considers nint/IntPtr and nuint/UIntPtr identical method signatures.
		// Canonicalize them before string-based formatter de-duplication.
		if (type is INamedTypeSymbol { IsNativeIntegerType: true } nativeInteger)
		{
			return nativeInteger.SpecialType == SpecialType.System_UIntPtr
				? "global::System.UIntPtr"
				: "global::System.IntPtr";
		}

		return ContainsTuple(type)
			? Build(type)
			: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
	}

	private static string Build(ITypeSymbol type)
	{
		switch (type)
		{
			case IArrayTypeSymbol array:
				return Build(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
			case IPointerTypeSymbol pointer:
				return Build(pointer.PointedAtType) + "*";
			case ITypeParameterSymbol parameter:
				return EscapeIdentifier(parameter.Name);
			case IDynamicTypeSymbol:
				return "dynamic";
			case INamedTypeSymbol named:
				return BuildNamed(named);
			default:
				return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		}
	}

	private static string BuildNamed(INamedTypeSymbol type)
	{
		if (type.IsTupleType)
		{
			return "global::System.ValueTuple<" +
				string.Join(", ", type.TypeArguments.Select(Build)) + ">";
		}

		if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
			type.TypeArguments.Length == 1)
		{
			return Build(type.TypeArguments[0]) + "?";
		}

		string prefix = type.ContainingType is not null
			? BuildNamed(type.ContainingType) + "."
			: BuildNamespace(type.ContainingNamespace);
		string name = prefix + EscapeIdentifier(type.Name);
		return type.TypeArguments.Length == 0
			? name
			: name + "<" + string.Join(", ", type.TypeArguments.Select(Build)) + ">";
	}

	private static string BuildNamespace(INamespaceSymbol typeNamespace)
	{
		if (typeNamespace is null || typeNamespace.IsGlobalNamespace)
		{
			return "global::";
		}

		return BuildNamespace(typeNamespace.ContainingNamespace) +
			EscapeIdentifier(typeNamespace.Name) + ".";
	}

	private static bool ContainsTuple(ITypeSymbol type)
	{
		switch (type)
		{
			case IArrayTypeSymbol array:
				return ContainsTuple(array.ElementType);
			case IPointerTypeSymbol pointer:
				return ContainsTuple(pointer.PointedAtType);
			case INamedTypeSymbol named:
				return named.IsTupleType ||
					(named.ContainingType is not null && ContainsTuple(named.ContainingType)) ||
					named.TypeArguments.Any(ContainsTuple);
			default:
				return false;
		}
	}

	private static string EscapeIdentifier(string name)
	{
		return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
			SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
			? "@" + name
			: name;
	}
}
