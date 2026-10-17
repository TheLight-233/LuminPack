using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator
{
        public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {

        /// <summary>Determines whether nested storage must be accessed through a Local mirror.</summary>
        /// <param name="member">The serialized field or auto-property.</param>
        /// <returns>Whether ordinary access cannot read and restore the backing storage.</returns>
        private static bool RequiresLocalAccess(ISymbol member)
        {
            if (member.DeclaredAccessibility is Accessibility.Private or Accessibility.ProtectedAndInternal or Accessibility.Protected)
                return true;
            if (member is not IPropertySymbol property) return false;
            return property.SetMethod == null || property.SetMethod.IsInitOnly ||
                property.SetMethod.DeclaredAccessibility is Accessibility.Private or Accessibility.ProtectedAndInternal or Accessibility.Protected ||
                property.GetMethod?.DeclaredAccessibility is Accessibility.Private or Accessibility.ProtectedAndInternal or Accessibility.Protected;
        }

        /// <summary>Records field type in the current field metadata.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessFieldType(ITypeSymbol type, LuminDataField field, List<string> genericParameters = null)
        {
            field.TypeSymbol = type;
            if (TypeMetaChecker.TryCheckIsNotRefLike(type))
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MemberIsRefStruct,
                    _currentSymbol?.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault(),
                    _mainSymbol?.Name ?? field.belongClassName,
                    field.Name,
                    type.ToDisplayString()
                ));
            }

            field.FieldType = type.IsReferenceType ? LuminDataType.Reference : LuminDataType.Value;

            // Custom formatters own their framing. Explicit external roots use the shared operation
            // plan so their complete Local storage metadata is also used when nested here.
            if (global::LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
                _analysisState.Types.IsAssemblyPackable(type))
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            if (type is ITypeParameterSymbol typeSymbol)
            {

                if (genericParameters is not null && genericParameters.Contains(typeSymbol.Name))
                {
                    field.Type = LuminFiledType.Other;
                    return;
                }
            }

            if (type is INamedTypeSymbol namedType)
            {

                INamedTypeSymbol originalSymbol = namedType.OriginalDefinition;
                string metadataName = $"{originalSymbol.ContainingNamespace}.{originalSymbol.MetadataName}";

                if (CodeEmitterTypeCatalog.SupportedMetadataNames.Contains(metadataName))
                {
                    field.Type = LuminFiledType.Other;
                    return;
                }

                switch (namedType.SpecialType)
                {
                    case SpecialType.System_Boolean:
                        field.Type = LuminFiledType.Bool;
                        break;
                    case SpecialType.System_Byte:
                        field.Type = LuminFiledType.Byte;
                        break;
                    case SpecialType.System_SByte:
                        field.Type = LuminFiledType.SByte;
                        break;
                    case SpecialType.System_Int16:
                        field.Type = LuminFiledType.Short;
                        break;
                    case SpecialType.System_UInt16:
                        field.Type = LuminFiledType.UShort;
                        break;
                    case SpecialType.System_Int32:
                        field.Type = LuminFiledType.Int;
                        break;
                    case SpecialType.System_UInt32:
                        field.Type = LuminFiledType.UInt;
                        break;
                    case SpecialType.System_Int64:
                        field.Type = LuminFiledType.Long;
                        break;
                    case SpecialType.System_UInt64:
                        field.Type = LuminFiledType.ULong;
                        break;
                    case SpecialType.System_Single:
                        field.Type = LuminFiledType.Float;
                        break;
                    case SpecialType.System_Double:
                        field.Type = LuminFiledType.Double;
                        break;
                    case SpecialType.System_Decimal:
                        field.Type = LuminFiledType.Decimal;
                        break;
                    case SpecialType.System_Char:
                        field.Type = LuminFiledType.Char;
                        break;
                    case SpecialType.System_String:
                        field.Type = LuminFiledType.String;
                        break;
                    default:
                        if (namedType.TypeKind is TypeKind.Enum)
                        {
                            field.Type = LuminFiledType.Enum;
                            field.EnumType = GetEnumFieldType(namedType);
                        }
                        else if (namedType.TypeKind is TypeKind.Interface or TypeKind.Dynamic)
                        {
                            field.Type = LuminFiledType.Other;
                        }
                        else if (namedType.TypeKind is TypeKind.Class or TypeKind.Struct)
                        {
                            var listSymbol = namedType.ContainingAssembly.GetTypeByMetadataName("System.Collections.Generic.List`1");

                            if (listSymbol != null && namedType.OriginalDefinition.Equals(listSymbol, SymbolEqualityComparer.Default))
                            {
                                field.Type = LuminFiledType.List;
                                ProcessGenericArguments(namedType.TypeArguments, field, genericParameters);
                            }
                            else if (CodeEmitterTypeCatalog.SupportedMetadataNames.Contains(metadataName))
                            {
                                field.Type = LuminFiledType.Other;
                            }
                            else if (namedType.IsGenericType)
                            {
                                if (namedType.ContainingType != null)
                                {
                                    CheckNestedClassAccessibility(namedType);
                                }

                                ProcessGenericClassOrStruct(namedType, field, genericParameters);
                            }
                            else
                            {
                                if (namedType.ContainingType != null)
                                {
                                    CheckNestedClassAccessibility(namedType);
                                }

                                ProcessNonGenericClassOrStruct(namedType, field, genericParameters);
                            }
                        }
                        break;
                }
            }
            else if (type is IArrayTypeSymbol arrayType)
            {
                // Process array types.
                field.Type = LuminFiledType.Array;
                field.FieldType = LuminDataType.Reference;
                // Recursively process array element types.
                ProcessArrayArguments(arrayType, field, genericParameters);
            }
        }

        /// <summary>Records generic arguments in the current field metadata.</summary>
        /// <param name="typeArguments">The type arguments used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessGenericArguments(IReadOnlyList<ITypeSymbol> typeArguments, LuminDataField field, List<string> genericParameters = null)
        {
            foreach (var typeArg in typeArguments)
            {

                if (typeArg is ITypeParameterSymbol typeSymbol)
                {
                    if (genericParameters is not null && genericParameters.Contains(typeSymbol.Name))
                    {
                        field.Type = LuminFiledType.Other;
                        return;
                    }
                }

                if (typeArg is INamedTypeSymbol namedTypeArg)
                {

                    INamedTypeSymbol originalSymbol = namedTypeArg.OriginalDefinition;
                    string metadataName = $"{originalSymbol.ContainingNamespace}.{originalSymbol.MetadataName}";

                    if (CodeEmitterTypeCatalog.SupportedMetadataNames.Contains(metadataName) ||
                        global::LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(namedTypeArg) ||
                        _analysisState.Types.IsAssemblyPackable(namedTypeArg))
                    {
                        field.Type = LuminFiledType.Other;

                        return;
                    }

                    var type = ConvertToLuminGenericsType(namedTypeArg);
                    field.GenericType.Add(type);

                    switch (type)
                    {
                        case LuminGenericsType.List:
                            ProcessGenericArguments(namedTypeArg.TypeArguments, field, genericParameters);
                            break;
                        case LuminGenericsType.Class or LuminGenericsType.Struct:
                            if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(namedTypeArg))
                            {
                                field.Type = LuminFiledType.Other;

                                //CheckLuminPackable
                                TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                                return;
                            }

                            ProcessClassFiled(namedTypeArg, field, false, genericParameters: genericParameters);
                            break;
                    }
                }
                else if (typeArg is IArrayTypeSymbol arrayTypeArg)
                {
                    field.GenericType.Add(LuminGenericsType.Array);
                    ProcessArrayArguments(arrayTypeArg, field, genericParameters);
                }
            }
        }

        /// <summary>Records array arguments in the current field metadata.</summary>
        /// <param name="arrayType">The array type used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessArrayArguments(IArrayTypeSymbol arrayType, LuminDataField field, List<string> genericParameters = null)
        {

            if (arrayType.Rank > 1)
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            if (arrayType.ElementType is ITypeParameterSymbol typeSymbol)
            {
                if (genericParameters is not null && genericParameters.Contains(typeSymbol.Name))
                {
                    field.Type = LuminFiledType.Other;
                    return;
                }

            }

            if (arrayType.ElementType is INamedTypeSymbol elementNamedType)
            {

                INamedTypeSymbol originalSymbol = elementNamedType.OriginalDefinition;
                string metadataName = $"{originalSymbol.ContainingNamespace}.{originalSymbol.MetadataName}";

                if (CodeEmitterTypeCatalog.SupportedMetadataNames.Contains(metadataName) ||
                    global::LuminPack.Code.Core.LuminPackExtensionGenerator.IsCustomGeneratorType(elementNamedType) ||
                    _analysisState.Types.IsAssemblyPackable(elementNamedType))
                {
                    field.Type = LuminFiledType.Other;

                    return;
                }

                var genericsType = ConvertToLuminGenericsType(elementNamedType);
                field.GenericType.Add(genericsType);

                if (genericsType is LuminGenericsType.Class or LuminGenericsType.Struct)
                {
                    if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(elementNamedType))
                    {
                        field.Type = LuminFiledType.Other;

                        //CheckLuminPackable
                        TypeMetaChecker.CheckFieldIsLuminPackable(elementNamedType, _currentSymbol.Locations.FirstOrDefault());

                        return;
                    }

                    ProcessClassFiled(elementNamedType, field, genericParameters: genericParameters);
                }
                else if (genericsType is LuminGenericsType.List && elementNamedType.IsGenericType)
                {
                    ProcessGenericArguments(elementNamedType.TypeArguments, field, genericParameters);
                }

            }
            else if (arrayType.ElementType is IArrayTypeSymbol nestedArray)
            {
                field.GenericType.Add(LuminGenericsType.Array);
                ProcessArrayArguments(nestedArray, field, genericParameters);
            }


        }

        /// <summary>Records class filed in the current field metadata.</summary>
        /// <param name="namedTypeArg">The named type arg used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="isFullName">The is full name used by this operation.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessClassFiled(INamedTypeSymbol namedTypeArg, LuminDataField field, bool isFullName = true, List<string> genericParameters = null)
        {
            if (namedTypeArg.ContainingType != null)
            {
                CheckNestedClassAccessibility(namedTypeArg);
            }

            if (ProcessedTypes.Contains(namedTypeArg))
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            // Mark the type as processed.
            ProcessedTypes.Add(namedTypeArg);

            try
            {
                if (namedTypeArg.IsGenericType)
                {
                    field.ClassName = isFullName ? namedTypeArg.ToDisplayString() : namedTypeArg.Name;
                    // Extract generic arguments into ClassGenericType, for example Test<string> to string.
                    field.ClassGenericType = string.Join(", ", namedTypeArg.TypeArguments.Select(t => t.Name));
                    field.NameSpace = namedTypeArg.ContainingNamespace.ToString() ?? "Your.Data.Namespace";

                    if (namedTypeArg.IsRecord || namedTypeArg.IsAbstract)
                    {
                        field.Type = LuminFiledType.Other;

                        TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                        return;
                    }

                    SetClassLocalField(namedTypeArg, field.localFields);

                    foreach (var nestedTypeArg in namedTypeArg.TypeArguments)
                    {
                        if (genericParameters is not null && genericParameters.Contains(nestedTypeArg.Name))
                        {
                            field.Type = LuminFiledType.Other;
                            return;
                        }
                    }

                    // Recursively process inherited members, excluding System.Object.
                    if (namedTypeArg.BaseType?.SpecialType != SpecialType.System_Object &&
                        namedTypeArg.BaseType?.SpecialType != SpecialType.System_ValueType)
                    {
                        ProcessBaseClassMembersForNestedClass(namedTypeArg.BaseType, field, genericParameters);
                    }

                    // Recursively process nested properties.
                    foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                    {
                        if (member.IsStatic) continue;

                        if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                        if (!IsAutoProperty(member)) continue;

                        if (TypeMetaChecker.TryCheckIncludeAttribute(member) && !member.Type.IsAnonymousType)
                        {
                            goto Set;
                        }

                        if (member.DeclaredAccessibility is
                            Accessibility.Private or
                            Accessibility.ProtectedAndInternal or
                            Accessibility.Protected) continue; // Skip static properties.

                    Set:
                        if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                        {
                            field.Type = LuminFiledType.Other;

                            //CheckLuminPackable
                            TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                            return;
                        }

                        var nestedField = new LuminDataField
                        {
                            Name = member.Name,
                            FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                            IsPrivate = RequiresLocalAccess(member),
                            isProperty = true,
                            belongClassName = namedTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                        {
                            nestedField.Type = LuminFiledType.Other;

                            field.ClassFields.Add(nestedField);

                            continue;
                        }
                        ProcessFieldType(member.Type, nestedField);
                        field.ClassFields.Add(nestedField);
                    }

                    // Recursively process nested fields.
                    foreach (var nestedMember in namedTypeArg.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (nestedMember.IsStatic) continue;

                        if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;

                        if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember) && !nestedMember.Type.IsAnonymousType)
                        {
                            goto Set;
                        }

                        if (nestedMember.IsImplicitlyDeclared ||
                            nestedMember.DeclaredAccessibility is
                                Accessibility.Private or
                                Accessibility.ProtectedAndInternal or
                                Accessibility.Protected) continue; // Skip compiler-generated fields.

                    Set:
                        if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                        {
                            field.Type = LuminFiledType.Other;

                            //CheckLuminPackable
                            TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                            return;
                        }

                        var nestedField = new LuminDataField
                        {
                            Name = nestedMember.Name,
                            FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                            IsPrivate = RequiresLocalAccess(nestedMember),
                            IsVolatile = nestedMember.IsVolatile,
                            belongClassName = namedTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(nestedMember))
                        {
                            nestedField.Type = LuminFiledType.Other;

                            field.ClassFields.Add(nestedField);

                            continue;
                        }

                        ProcessFieldType(nestedMember.Type, nestedField);
                        field.ClassFields.Add(nestedField);
                    }

                }
                else
                {
                    field.ClassName = namedTypeArg.ToDisplayString();
                    field.NameSpace = namedTypeArg.ContainingNamespace.ToString() ?? "Your.Data.Namespace";

                    if (namedTypeArg.IsRecord || namedTypeArg.IsAbstract)
                    {
                        field.Type = LuminFiledType.Other;

                        TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                        return;
                    }

                    SetClassLocalField(namedTypeArg, field.localFields);

                    // Recursively process inherited members, excluding System.Object.
                    if (namedTypeArg.BaseType?.SpecialType != SpecialType.System_Object &&
                        namedTypeArg.BaseType?.SpecialType != SpecialType.System_ValueType)
                    {
                        ProcessBaseClassMembersForNestedClass(namedTypeArg.BaseType, field, genericParameters);
                    }

                    // Recursively process nested properties.
                    foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                    {

                        _currentSymbol = member;

                        if (member.IsStatic) continue;

                        if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                        if (!IsAutoProperty(member)) continue;

                        if (TypeMetaChecker.TryCheckIncludeAttribute(member))
                        {
                            goto Set;
                        }

                        if (member.DeclaredAccessibility is
                            Accessibility.Private or
                            Accessibility.ProtectedAndInternal or
                            Accessibility.Protected) continue; // Skip compiler-generated fields.

                    Set:
                        if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                        {
                            field.Type = LuminFiledType.Other;

                            //CheckLuminPackable
                            TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                            return;
                        }

                        var nestedField = new LuminDataField
                        {
                            Name = member.Name,
                            FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                            IsPrivate = RequiresLocalAccess(member),
                            isProperty = true,
                            belongClassName = namedTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                        {
                            nestedField.Type = LuminFiledType.Other;

                            field.ClassFields.Add(nestedField);

                            continue;
                        }

                        ProcessFieldType(member.Type, nestedField);
                        field.ClassFields.Add(nestedField);
                    }

                    // Recursively process nested fields.
                    foreach (var nestedMember in namedTypeArg.GetMembers().OfType<IFieldSymbol>())
                    {
                        _currentSymbol = nestedMember;

                        if (nestedMember.IsStatic) continue;

                        if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;

                        if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember))
                        {
                            goto Set;
                        }

                        if (nestedMember.IsImplicitlyDeclared ||
                            nestedMember.DeclaredAccessibility is
                                Accessibility.Private or
                                Accessibility.ProtectedAndInternal or
                                Accessibility.Protected) continue; // Skip static and compiler-generated fields.

                    Set:
                        if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                        {
                            field.Type = LuminFiledType.Other;

                            //CheckLuminPackable
                            TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());

                            return;
                        }

                        var nestedField = new LuminDataField
                        {
                            Name = nestedMember.Name,
                            FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                            IsPrivate = RequiresLocalAccess(nestedMember),
                            IsVolatile = nestedMember.IsVolatile,
                            belongClassName = namedTypeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(nestedMember))
                        {
                            nestedField.Type = LuminFiledType.Other;

                            field.ClassFields.Add(nestedField);

                            continue;
                        }

                        ProcessFieldType(nestedMember.Type, nestedField);
                        field.ClassFields.Add(nestedField);
                    }
                }

                AnalyzeNestedClassConstructors(namedTypeArg, field);

                field.RentPoolMethod = TypeMetaChecker.AnalyzeRentPoolMethod(namedTypeArg, _currentSymbol?.Locations.FirstOrDefault() ?? namedTypeArg.Locations.FirstOrDefault());
            }
            finally
            {
                ProcessedTypes.Remove(namedTypeArg);
            }
        }

        /// <summary>Updates the class local field used by the current operation.</summary>
        /// <param name="typeSymbol">The type or symbol to analyze.</param>
        /// <param name="localFields">The local fields used by this operation.</param>
        private static void SetClassLocalField(INamedTypeSymbol typeSymbol, List<LuminLocalFieldData> localFields)
        {
            if (typeSymbol.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType))
                typeSymbol = global::LuminPack.Code.Core.ReferencedTypeMetadata.Resolve(typeSymbol, _analysisState.Compilation);

            // Recursively process base types, excluding System.Object.
            if (typeSymbol.BaseType != null &&
                typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                SetClassLocalField(typeSymbol.BaseType, localFields);
            }

            var members = GetOrderedStorageMembers(typeSymbol);

            // Append members declared on the current type.
            foreach (var member in members)
            {
                if (member.IsImplicitlyDeclared || member.IsStatic) continue;

                string typeName = "";
                bool isValue = false;
                bool isProperty = false;

                if (member is IPropertySymbol property)
                {
                    if (!IsAutoProperty(property)) continue;

                    typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    isValue = property.Type.IsValueType;
                    isProperty = true;
                }
                else if (member is IFieldSymbol field)
                {
                    typeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    isValue = field.Type.IsValueType;
                }

                // A Local layout mirror is only required for storage slots that the generated
                // extension class cannot name: explicit private/protected fields, and the always
                // private backing field of an auto-property.
                bool isPrivate = isProperty ||
                    member.DeclaredAccessibility is
                        Accessibility.Private or
                        Accessibility.ProtectedAndInternal or
                        Accessibility.Protected;

                if (localFields.Any(x => x.Name == member.Name))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ContainsDuplicateNameField,
                        member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                        member.Name
                    ));
                }

                // An auto-property's storage is its backing field, and a [FieldOffset] on an
                // explicit-layout type sits on that backing field rather than on the property, so the
                // offset has to be read from the field for the mirror to reproduce an Explicit layout.
                ISymbol offsetOwner = member;
                if (member is IPropertySymbol offsetProperty)
                {
                    string backingFieldName = "<" + offsetProperty.Name + ">k__BackingField";
                    offsetOwner = typeSymbol.GetMembers()
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault(f => f.Name == backingFieldName) ?? member;
                }

                TypeMetaChecker.TryCheckAndGetFiledOffsetAttribute(offsetOwner, out var offset);

                localFields.Add(new LuminLocalFieldData
                {
                    TypeName = typeName,
                    TypeSymbol = member switch
                    {
                        IPropertySymbol propertySymbol => propertySymbol.Type,
                        IFieldSymbol fieldSymbol => fieldSymbol.Type,
                        _ => null
                    },
                    Name = member.Name,
                    DeclaringTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    filedOffset = offset,
                    IsValue = isValue,
                    IsPrivate = isPrivate,
                    IsProperty = isProperty,
                });
            }
        }

        /// <summary>Preserves declaration order for both source members and imported backing storage.</summary>
        /// <param name="type">The type whose field and property order is required.</param>
        /// <returns>Source-ordered members or metadata fields with backing fields replaced by their properties.</returns>
        private static List<ISymbol> GetOrderedStorageMembers(INamedTypeSymbol type)
        {
            if (type.DeclaringSyntaxReferences.Length != 0)
                return type.GetMembers().Where(member => member is IPropertySymbol or IFieldSymbol)
                    .OrderBy(member => member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.SpanStart ?? int.MaxValue).ToList();

            // Metadata groups properties separately from fields. Field-table order retains each
            // automatic property's original storage position among ordinary and hidden fields.
            var properties = type.GetMembers().OfType<IPropertySymbol>().Where(IsAutoProperty)
                .ToDictionary(property => "<" + property.Name + ">k__BackingField", property => (ISymbol)property);
            return type.GetMembers().OfType<IFieldSymbol>()
                .Select(field => properties.TryGetValue(field.Name, out var property) ? property : field).ToList();
        }

        /// <summary>Records generic class or struct in the current field metadata.</summary>
        /// <param name="namedType">The named type used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessGenericClassOrStruct(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // Check whether this type has already been analyzed.
            if (ProcessedTypes.Contains(namedType))
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            ProcessedTypes.Add(namedType);

            try
            {
                field.Type = namedType.TypeKind == TypeKind.Class ? LuminFiledType.Class : LuminFiledType.Struct;
                field.ClassName = namedType.Name;
                field.ClassGenericType = string.Join(", ", namedType.TypeArguments.Select(t => t.Name));

                // Use Other only when the type requires an external formatter.
                if (namedType.IsRecord || namedType.IsAbstract)
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                SetClassLocalField(namedType, field.localFields);

                // Use Other only for generic parameters declared by the current type.
                if (genericParameters != null)
                {
                    foreach (var nestedTypeArg in namedType.TypeArguments)
                    {
                        if (nestedTypeArg is ITypeParameterSymbol typeParam &&
                            genericParameters.Contains(typeParam.Name))
                        {
                            field.Type = LuminFiledType.Other;
                            return;
                        }
                    }
                }

                // Process inherited members.
                if (namedType.BaseType?.SpecialType != SpecialType.System_Object &&
                    namedType.BaseType?.SpecialType != SpecialType.System_ValueType)
                {
                    ProcessBaseClassMembersForNestedClass(namedType.BaseType, field, genericParameters);
                }

                ProcessNestedMembers(namedType, field, genericParameters);
            }
            finally
            {
                AnalyzeNestedClassConstructors(namedType, field);
                field.RentPoolMethod = TypeMetaChecker.AnalyzeRentPoolMethod(namedType, _currentSymbol?.Locations.FirstOrDefault() ?? namedType.Locations.FirstOrDefault());
                ProcessedTypes.Remove(namedType);
            }
        }

        /// <summary>Records non generic class or struct in the current field metadata.</summary>
        /// <param name="namedType">The named type used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessNonGenericClassOrStruct(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // Check whether this type has already been analyzed.
            if (ProcessedTypes.Contains(namedType))
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            ProcessedTypes.Add(namedType);

            try
            {
                field.Type = namedType.TypeKind == TypeKind.Class ? LuminFiledType.Class : LuminFiledType.Struct;
                field.ClassName = namedType.Name;

                // Use Other only when the type requires an external formatter.
                if (namedType.IsRecord || namedType.IsAbstract)
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                SetClassLocalField(namedType, field.localFields);

                // Process inherited members.
                if (namedType.BaseType?.SpecialType != SpecialType.System_Object &&
                    namedType.BaseType?.SpecialType != SpecialType.System_ValueType)
                {
                    ProcessBaseClassMembersForNestedClass(namedType.BaseType, field, genericParameters);
                }

                ProcessNestedMembers(namedType, field, genericParameters);
            }
            finally
            {
                AnalyzeNestedClassConstructors(namedType, field);
                field.RentPoolMethod = TypeMetaChecker.AnalyzeRentPoolMethod(namedType, _currentSymbol?.Locations.FirstOrDefault() ?? namedType.Locations.FirstOrDefault());
                ProcessedTypes.Remove(namedType);
            }
        }

        /// <summary>
        /// Process the base members of the nested type.
        /// </summary>
        /// <param name="baseClassSymbol">The base class symbol used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessBaseClassMembersForNestedClass(INamedTypeSymbol baseClassSymbol, LuminDataField field, List<string> genericParameters = null)
        {
            if (baseClassSymbol == null ||
                baseClassSymbol.SpecialType == SpecialType.System_Object ||
                baseClassSymbol.SpecialType == SpecialType.System_ValueType) return;

            baseClassSymbol = global::LuminPack.Code.Core.ReferencedTypeMetadata.Resolve(baseClassSymbol, _analysisState.Compilation);

            // Process the most distant base types first.
            ProcessBaseClassMembersForNestedClass(baseClassSymbol.BaseType, field, genericParameters);

            // Process members of the current base type.
            ProcessNestedClassBaseMembers(baseClassSymbol, field, genericParameters);
        }

        /// <summary>
        /// Process concrete members inherited by the nested type.
        /// </summary>
        /// <param name="classSymbol">The class symbol used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessNestedClassBaseMembers(INamedTypeSymbol classSymbol, LuminDataField field, List<string> genericParameters = null)
        {
            // Process base type properties.
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                _currentSymbol = member;

                if (member.IsStatic) continue;

                if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                if (!IsAutoProperty(member)) continue;

                if (member.DeclaredAccessibility == Accessibility.Private &&
                    TypeMetaChecker.TryCheckIncludeAttribute(member))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.InheritTypeCanNotIncludeParentPrivateMember,
                        member.Locations.FirstOrDefault() ?? _location,
                        _mainSymbol.Name,
                        member.Name));
                    continue;
                }

                if (TypeMetaChecker.TryCheckIncludeAttribute(member))
                {
                    goto Set;
                }

                if (member.DeclaredAccessibility is
                    Accessibility.Private or
                    Accessibility.ProtectedAndInternal or
                    Accessibility.Protected) continue;

            Set:
                if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                {
                    // Use an external formatter for readonly structs without rejecting the containing type.
                    continue;
                }

                var nestedField = new LuminDataField
                {
                    Name = member.Name,
                    FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    TypeSymbol = member.Type,
                    NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                    IsPrivate = RequiresLocalAccess(member),
                    isProperty = true,
                    belongClassName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };

                if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                {
                    nestedField.Type = LuminFiledType.Other;
                    field.ClassFields.Add(nestedField);
                    continue;
                }

                ProcessFieldType(member.Type, nestedField, genericParameters);
                field.ClassFields.Add(nestedField);
            }

            // Process base type fields.
            foreach (var nestedMember in classSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                _currentSymbol = nestedMember;

                if (nestedMember.IsStatic) continue;

                if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;

                if (nestedMember.DeclaredAccessibility == Accessibility.Private &&
                    TypeMetaChecker.TryCheckIncludeAttribute(nestedMember))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.InheritTypeCanNotIncludeParentPrivateMember,
                        nestedMember.Locations.FirstOrDefault() ?? _location,
                        _mainSymbol.Name,
                        nestedMember.Name));
                    continue;
                }

                if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember))
                {
                    goto Set;
                }

                if (nestedMember.IsImplicitlyDeclared ||
                    nestedMember.DeclaredAccessibility is
                        Accessibility.Private or
                        Accessibility.ProtectedAndInternal or
                        Accessibility.Protected) continue;

            Set:
                if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                {
                    // Use an external formatter for readonly structs without rejecting the containing type.
                    continue;
                }

                var nestedField = new LuminDataField
                {
                    Name = nestedMember.Name,
                    FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    TypeSymbol = nestedMember.Type,
                    NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                    IsPrivate = RequiresLocalAccess(nestedMember),
                    IsVolatile = nestedMember.IsVolatile,
                    belongClassName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };

                if (TypeMetaChecker.TryCheckPackableObjectAttribute(nestedMember))
                {
                    nestedField.Type = LuminFiledType.Other;
                    field.ClassFields.Add(nestedField);
                    continue;
                }

                ProcessFieldType(nestedMember.Type, nestedField, genericParameters);
                field.ClassFields.Add(nestedField);
            }
        }

        /// <summary>Records nested members in the current field metadata.</summary>
        /// <param name="namedType">The named type used by this operation.</param>
        /// <param name="field">The metadata describing the field being processed.</param>
        /// <param name="genericParameters">The generic parameters in the current type scope.</param>
        private static void ProcessNestedMembers(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // Process nested properties.
            foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.IsStatic) continue;

                if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                if (!IsAutoProperty(member)) continue;

                bool shouldInclude = TypeMetaChecker.TryCheckIncludeAttribute(member);

                if (!shouldInclude && member.DeclaredAccessibility is
                        Accessibility.Private or
                        Accessibility.ProtectedAndInternal or
                        Accessibility.Protected)
                {
                    continue;
                }

                // Classify only actual readonly structs as Other.
                if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                var nestedField = new LuminDataField
                {
                    Name = member.Name,
                    FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                    IsPrivate = RequiresLocalAccess(member),
                    isProperty = true,
                    belongClassName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };

                // Use Other for explicit PackableObject members or actual type parameters.
                if (TypeMetaChecker.TryCheckPackableObjectAttribute(member) ||
                    (member.Type is ITypeParameterSymbol typeParam && genericParameters?.Contains(typeParam.Name) == true))
                {
                    nestedField.Type = LuminFiledType.Other;
                    field.ClassFields.Add(nestedField);
                    continue;
                }

                ProcessFieldType(member.Type, nestedField, genericParameters);
                field.ClassFields.Add(nestedField);
            }

            // Process nested fields using the same rules as properties.
            foreach (var nestedMember in namedType.GetMembers().OfType<IFieldSymbol>())
            {
                if (nestedMember.IsStatic) continue;

                if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;

                bool shouldInclude = TypeMetaChecker.TryCheckIncludeAttribute(nestedMember);

                if (!shouldInclude && (nestedMember.IsImplicitlyDeclared ||
                                       nestedMember.DeclaredAccessibility is
                                           Accessibility.Private or
                                           Accessibility.ProtectedAndInternal or
                                           Accessibility.Protected))
                {
                    continue;
                }

                // Classify only actual readonly structs as Other.
                if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                var nestedField = new LuminDataField
                {
                    Name = nestedMember.Name,
                    FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                    IsPrivate = RequiresLocalAccess(nestedMember),
                    IsVolatile = nestedMember.IsVolatile,
                    belongClassName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };

                // Use Other for explicit PackableObject members or actual type parameters.
                if (TypeMetaChecker.TryCheckPackableObjectAttribute(nestedMember) ||
                    (nestedMember.Type is ITypeParameterSymbol typeParam && genericParameters?.Contains(typeParam.Name) == true))
                {
                    nestedField.Type = LuminFiledType.Other;
                    field.ClassFields.Add(nestedField);
                    continue;
                }

                ProcessFieldType(nestedMember.Type, nestedField, genericParameters);
                field.ClassFields.Add(nestedField);
            }
        }
    }
}
