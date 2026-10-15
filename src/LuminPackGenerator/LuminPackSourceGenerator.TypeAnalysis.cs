using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator
{
        public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {

        /// <summary>Obtains the type with attribute for the supplied input.</summary>
        /// <param name="context">The context used for analysis, source production, or initialization.</param>
        /// <param name="metadataName">The metadata name used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private static INamedTypeSymbol? GetTypeWithAttribute(
            GeneratorSyntaxContext context,
            string metadataName)
        {
            if (context.Node is not TypeDeclarationSyntax typeDeclaration)
                return null;

            var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            if (symbol == null)
                return null;

            foreach (var attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::" + metadataName)
                    return symbol;
            }

            return null;
        }

        /// <summary>
        /// Recursively unwraps array and list containers to the innermost element type,
        /// then uses Roslyn's IsUnmanagedType, including user-defined unmanaged structs.
        /// Example:
        ///   int[]           → leaf = int          → unmanaged ✓
        ///   List&lt;int&gt;       → leaf = int          → unmanaged ✓
        ///   List&lt;int[]&gt;     → leaf = int          → unmanaged ✓
        ///   List&lt;int&gt;[]    → leaf = int          → unmanaged ✓
        ///   List&lt;string&gt;   → leaf = string       → managed   ✗
        ///   List&lt;MyClass&gt;  → leaf = MyClass      → managed   ✗
        ///   List&lt;MyStruct&gt; → leaf = MyStruct (unmanaged struct) → unmanaged ✓
        /// </summary>
        /// <summary>
        /// Determines whether the original C# type is an array or List&lt;T&gt;,
        /// Do not rely on LuminFiledType after ProcessFieldType; collections with readonly struct leaves
        /// can resolve to Other and bypass compression classification.
        /// </summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private static bool IsOriginalTypeArrayOrList(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol)
                return true;

            if (type is INamedTypeSymbol nt && nt.IsGenericType)
            {
                var originalDef = nt.OriginalDefinition;
                var metadataName = $"{originalDef.ContainingNamespace}.{originalDef.MetadataName}";
                return metadataName == "System.Collections.Generic.List`1";
            }

            return false;
        }

        /// <summary>Determines whether the supplied value has the collection element type unmanaged classification.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private static bool IsCollectionElementTypeUnmanaged(ITypeSymbol type)
        {
            while (true)
            {
                // Unwrap one array layer: T[] to T, T[][] to T[], and so on.
                if (type is IArrayTypeSymbol arrayType)
                {
                    type = arrayType.ElementType;
                    continue;
                }

                // Unwrap one List<T> layer.
                if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
                {
                    var originalDef = namedType.OriginalDefinition;
                    var metadataName = $"{originalDef.ContainingNamespace}.{originalDef.MetadataName}";
                    if (metadataName == "System.Collections.Generic.List`1" &&
                        namedType.TypeArguments.Length == 1)
                    {
                        type = namedType.TypeArguments[0];
                        continue;
                    }
                }

                // At the innermost element, use Roslyn's IsUnmanagedType classification.
                // Return true for primitive values, enums, and structs whose fields are all unmanaged.
                return type.IsUnmanagedType;
            }
        }

        /// <summary>Converts the supplied input to the to lumin generics type representation.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <returns>The result of the operation.</returns>
        private static LuminGenericsType ConvertToLuminGenericsType(INamedTypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
            {
                return type.SpecialType switch
                {
                    SpecialType.System_Byte => LuminGenericsType.Byte,
                    SpecialType.System_Int16 => LuminGenericsType.Short,
                    SpecialType.System_UInt16 => LuminGenericsType.UShort,
                    SpecialType.System_Int32 => LuminGenericsType.Int,
                    SpecialType.System_UInt32 => LuminGenericsType.UInt,
                    SpecialType.System_Int64 => LuminGenericsType.Long,
                    SpecialType.System_UInt64 => LuminGenericsType.ULong,
                    SpecialType.System_Single => LuminGenericsType.Float,
                    SpecialType.System_Double => LuminGenericsType.Double,
                    SpecialType.System_String => LuminGenericsType.String,
                    SpecialType.System_Boolean => LuminGenericsType.Bool,
                    SpecialType.System_Decimal => LuminGenericsType.Decimal,
                    SpecialType.System_Char => LuminGenericsType.Char
                };
            }

            // Process nested generic types such as List<List<int>>.
            var listSymbol = type.ContainingAssembly.GetTypeByMetadataName("System.Collections.Generic.List`1");
            if (listSymbol != null && type.OriginalDefinition.Equals(listSymbol, SymbolEqualityComparer.Default))
            {
                return LuminGenericsType.List;
            }

            return type.TypeKind == TypeKind.Class ? LuminGenericsType.Class : LuminGenericsType.Struct;
        }

        /// <summary>Obtains the enum field type for the supplied input.</summary>
        /// <param name="enumType">The enum type used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private static LuminEnumFieldType GetEnumFieldType(INamedTypeSymbol enumType)
        {
            var underlyingType = enumType.EnumUnderlyingType;
            switch (underlyingType!.SpecialType)
            {
                case SpecialType.System_Byte:
                    return LuminEnumFieldType.Byte;
                case SpecialType.System_SByte:
                    return LuminEnumFieldType.Byte;
                case SpecialType.System_Int16:
                    return LuminEnumFieldType.Short;
                case SpecialType.System_UInt16:
                    return LuminEnumFieldType.UShort;
                case SpecialType.System_Int32:
                    return LuminEnumFieldType.Int;
                case SpecialType.System_UInt32:
                    return LuminEnumFieldType.UInt;
                case SpecialType.System_Int64:
                    return LuminEnumFieldType.Long;
                case SpecialType.System_UInt64:
                    return LuminEnumFieldType.ULong;
                default:
                    return LuminEnumFieldType.Int;
            }
        }

        /// <summary>
        /// Detects whether the property has compiler-generated backing storage, including generic properties.
        /// </summary>
        /// <param name="property">The property used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public static bool IsAutoProperty(IPropertySymbol property)
        {
            if (property == null)
                return false;

            // Require an instance property that is not abstract, extern, or static.
            if (property.IsStatic || property.IsAbstract || property.IsExtern)
                return false;

            var standardBackingFieldName = $"<{property.Name}>k__BackingField";

            // Find the compiler-generated property backing field.
            return property.ContainingType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f =>
                    f.Name == standardBackingFieldName);
        }
    }
}
