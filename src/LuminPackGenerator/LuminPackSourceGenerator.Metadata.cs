using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator
{
        public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {

        /// <summary>Obtains the same member contract used by formatter generation for overload documentation.</summary>
        /// <param name="type">The annotated type whose contract is documented.</param>
        /// <param name="compilation">The compilation containing the type.</param>
        /// <param name="metadata">The current generation settings.</param>
        /// <returns>The ordered, filtered serialization metadata for the type.</returns>
        internal static LuminDataInfo GetSerializationMetadata(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata)
        {
            return CreateLuminDataInfo(type, compilation, metadata);
        }

        /// <summary>Analyzes a type inside an isolated metadata and diagnostic scope.</summary>
        /// <param name="symbol">The type or symbol to analyze.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        /// <param name="metadata">The language, framework, and generation settings.</param>
        /// <returns>The result of the operation.</returns>
        private static LuminDataInfo CreateLuminDataInfo(
            ISymbol symbol,
            Compilation compilation,
            MetaInfo metadata)
        {
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.MetadataAnalyses);
            var previousState = _analysisState;
            var previousDiagnostics = TypeMetaChecker.PushDiagnosticScope();
            try
            {
                var types = CompilationTypeAnalysisCache.GetOrCreate(compilation);
                if (symbol is INamedTypeSymbol rootType && !rootType.IsAbstract)
                    symbol = ReferencedTypeMetadata.Resolve(rootType, compilation);
                _analysisState = new AnalysisState(symbol, metadata, types, compilation);
                var dataInfo = CreateLuminDataInfoCore(symbol, compilation);
                if (TypeMetaChecker.CurrentDiagnostics.Count != 0)
                {
                    dataInfo.Diagnostics = TypeMetaChecker.CurrentDiagnostics.ToArray();
                }
                return dataInfo;
            }
            finally
            {
                TypeMetaChecker.PopDiagnosticScope(previousDiagnostics);
                _analysisState = previousState;
            }
        }

        /// <summary>Collects the serializable members and generation settings of the annotated type.</summary>
        /// <param name="symbol">The type or symbol to analyze.</param>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        /// <returns>The result of the operation.</returns>
        private static LuminDataInfo CreateLuminDataInfoCore(ISymbol symbol, Compilation compilation)
        {
            var typeSymbol = (INamedTypeSymbol)symbol;

            if (typeSymbol.ContainingType != null)
            {
                CheckNestedClassAccessibility(typeSymbol);
            }

            _analysisState.ProcessedTypes.Add(typeSymbol);

            var dataInfo = new LuminDataInfo
            {
                className = typeSymbol.Name,
                classFileName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                classFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                classNameSpace = typeSymbol.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                isGeneric = typeSymbol.IsGenericType,
                isValueType = typeSymbol.TypeKind == TypeKind.Struct,
                TypeSymbol = typeSymbol,
                CanGenerateUnionDispatch = LuminPackUnionDispatchUtilities.CanGeneratePartial(typeSymbol, compilation) ||
                    LuminPackUnionDispatchUtilities.CanUseReferencedDispatch(typeSymbol, compilation),
                enableBurst = false,
                generatorType = TypeMetaChecker.CheckGeneratorType(typeSymbol),
            };

            foreach (var iface in typeSymbol.AllInterfaces)
            {
                var fullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!dataInfo.interfaces.Contains(fullName))
                {
                    dataInfo.interfaces.Add(fullName);
                }
            }

            // Custom formatter types implement serialization manually; the generator emits no
            // extension methods for them and skips member/union analysis entirely. Validation
            // and registration are handled by CustomFormatterAnalyzer in the aggregate pipeline.
            if (dataInfo.generatorType == GeneratorType.Custom)
            {
                return dataInfo;
            }

            #region Diagnostics

            //Check Layout
            TypeMetaChecker.TryCheckStructLayout(typeSymbol, dataInfo);

            // .NET 11 (C# 15) union declarations lower to a struct carrying
            // [System.Runtime.CompilerServices.Union] + IUnion + one public single-parameter
            // constructor per case type.  Treat them as union roots exactly like
            // abstract-class/interface unions.  The attribute exists only on .NET 11+, which is the
            // high/low version gate: older TFMs never satisfy this branch.
            bool isCs11UnionStruct = IsCs11UnionStruct(typeSymbol, compilation);
            if (isCs11UnionStruct)
            {
                dataInfo.isUnion = true;
                CollectCs11UnionStructMembers(typeSymbol, dataInfo, compilation);
            }
            else
            {
                if (symbol.IsAbstract)
                {
                    dataInfo.isUnion = true;

                    dataInfo.IsWideTag = TypeMetaChecker.TryCheckWideTagAttribute(typeSymbol);
                }

                if (TypeMetaChecker.TryCheckUnionAttribute(typeSymbol) || symbol.IsAbstract)
                {
                    if (!dataInfo.CanGenerateUnionDispatch)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.UnionParticipantMustBePartial,
                            typeSymbol.Locations.FirstOrDefault(),
                            typeSymbol.Name,
                            typeSymbol.Name));
                    }

                    if (symbol.IsSealed)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.SealedTypeCantBeUnion,
                            typeSymbol.Locations.FirstOrDefault() ?? _location,
                            symbol.Name
                        ));
                    }

                    if (typeSymbol.TypeKind == TypeKind.Struct)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.UnionMemberNotAllowStruct,
                            typeSymbol.Locations.FirstOrDefault() ?? _location,
                            symbol.Name
                        ));
                    }

                    if (!symbol.IsAbstract)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.ConcreteTypeCantBeUnion,
                            typeSymbol.Locations.FirstOrDefault() ?? _location,
                            symbol.Name
                        ));
                    }

                    var idSet = new HashSet<int>();

                    // collect all attr
                    foreach (var attr in typeSymbol.GetAttributes()
                                 .Where(a => a.AttributeClass?.ToDisplayString() == LUMIN_UNION_ATTRIBUTE))
                    {
                        if (attr.ConstructorArguments.Length < 2) continue;

                        var id = (ushort)attr.ConstructorArguments[0].Value!;
                        var memberType = (INamedTypeSymbol)attr.ConstructorArguments[1].Value;
                        dataInfo.UnionMembers.Add(new LuminUnionMemberInfo(
                            id,
                            memberType,
                            LuminPackUnionDispatchUtilities.CanGeneratePartial(memberType, compilation)));
                    }

                    foreach (var member in dataInfo.UnionMembers)
                    {
                        // Check whether the derived type has more generic parameters than its base.
                        int memberGenericCount = member.Type.OriginalDefinition.TypeParameters.Length;

                        if (memberGenericCount > typeSymbol.TypeParameters.Length)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnionMemberGenericCountExceed,
                                member.Type.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? _location,
                                member.Type.Name,
                                typeSymbol.TypeParameters.Length,
                                memberGenericCount
                            ));
                        }

                        // 2. check id duplicate
                        if (!idSet.Add(member.Id))
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnionTagDuplicate,
                                typeSymbol.Locations.FirstOrDefault() ?? _location,
                                member.Id,
                                symbol.Name
                            ));
                        }

                        // 3. check member is luminpackable
                        if (!TypeMetaChecker.TryCheckPackableAttribute(member.Type))
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnionMemberMustBeLuminPackable,
                                member.Type.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? _location,
                                member.Type.Name
                            ));
                        }


                        // 4. check member isValidInheritance or isImplementBaseType
                        bool isValidMember = false;

                        if (typeSymbol.TypeKind == TypeKind.Class)
                        {
                            var current = member.Type.OriginalDefinition;
                            while (current != null)
                            {
                                if (SymbolEqualityComparer.Default.Equals(
                                        current.OriginalDefinition,
                                        typeSymbol.OriginalDefinition))
                                {
                                    isValidMember = true;

                                    dataInfo.UnionGenericTypes[member.Type] =
                                        current.TypeArguments.Select(t => t.ToDisplayString()).ToList();

                                    break;
                                }
                                current = current.BaseType?.OriginalDefinition;
                            }

                            if (!isValidMember)
                            {
                                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                    DiagnosticDescriptors.UnionMemberTypeNotDerivedBaseType,
                                    member.Type.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? _location,
                                    member.Type.Name,
                                    typeSymbol.Name
                                ));
                            }
                        }
                        else if (typeSymbol.TypeKind == TypeKind.Interface)
                        {
                            foreach (var implementedInterface in member.Type.AllInterfaces)
                            {
                                if (SymbolEqualityComparer.Default.Equals(
                                        implementedInterface.OriginalDefinition,
                                        typeSymbol.OriginalDefinition))
                                {
                                    isValidMember = true;

                                    dataInfo.UnionGenericTypes[member.Type] =
                                        implementedInterface.TypeArguments.Select(t => t.ToDisplayString()).ToList();

                                    break;
                                }
                            }

                            if (!isValidMember)
                            {
                                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                    DiagnosticDescriptors.UnionMemberTypeNotImplementBaseType,
                                    member.Type.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault() ?? _location,
                                    member.Type.Name,
                                    typeSymbol.Name
                                ));
                            }
                        }

                    }

                    DiscoverAndRegisterDerivedTypes(typeSymbol, dataInfo, compilation);

                    foreach (var member in dataInfo.UnionMembers)
                    {
                        if (!LuminPackUnionDispatchUtilities.IsCurrentCompilation(member.Type, compilation))
                            continue;

                        if (!member.CanGenerateDispatch)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.UnionParticipantMustBePartial,
                                member.Type.OriginalDefinition.Locations.FirstOrDefault(),
                                member.Type.Name,
                                typeSymbol.Name));
                        }
                    }

                }
            }

            if (dataInfo.generatorType is GeneratorType.CircleReference or GeneratorType.VersionTolerant)
            {
                TypeMetaChecker.ValidateOrderAttributesForType(typeSymbol, typeSymbol.GetMembers(), _location);
            }

            if (symbol.IsStatic)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.StaticClass,
                    typeSymbol.Locations.FirstOrDefault() ?? _location,
                    symbol.Name
                ));
            }

            if (typeSymbol.IsRefLikeType)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.TypeIsRefStruct,
                    typeSymbol.Locations.FirstOrDefault() ?? _location,
                    symbol.Name));
            }

            #endregion

            //Collect generic parameter constraints.
            if (dataInfo.isGeneric)
            {
                dataInfo.GenericParameters
                    .AddRange(typeSymbol.TypeParameters
                        .Select(t => t.Name));

                foreach (var typeParam in typeSymbol.TypeParameters)
                {

                    var constraint = new GenericParameterConstraint
                    {
                        ParameterName = typeParam.Name,
                        IsUnmanaged = typeParam.HasUnmanagedTypeConstraint,
                        IsClass = typeParam.HasReferenceTypeConstraint,
                        // Do not mark IsStruct when an unmanaged constraint is present.
                        IsStruct = typeParam.HasValueTypeConstraint && !typeParam.HasUnmanagedTypeConstraint,
                        IsNotNull = typeParam.HasNotNullConstraint,
                        HasDefault = typeParam.HasUnmanagedTypeConstraint && !typeParam.HasUnmanagedTypeConstraint,
                        HasNewConstructor = typeParam.HasConstructorConstraint
                    };

                    // Append interface and base type constraints.
                    foreach (var constraintType in typeParam.ConstraintTypes)
                    {
                        constraint.Constraints.Add(
                            constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        );

                    }

                    dataInfo.GenericConstraints.Add(constraint);
                }
            }

            //Process fields and properties in declaration order.
            SetClassLocalField(typeSymbol, dataInfo.localFields);

            // Collect inherited members starting at the immediate base, excluding System.Object.
            if (typeSymbol.BaseType?.SpecialType != SpecialType.System_Object)
            {
                ProcessBaseClassMembers(typeSymbol.BaseType, dataInfo);
            }

            ProcessMember(typeSymbol, dataInfo);


            dataInfo.fields.Sort((x, y) => x.Order.CompareTo(y.Order));

            //check member count
            if (dataInfo.fields.Count > 249)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MembersCountOver250,
                    _location,
                    symbol.Name,
                    dataInfo.fields.Count
                ));
            }

            TypeMetaChecker.CheckSerializeCallBack(typeSymbol, dataInfo);

            if (!dataInfo.isUnion)
            {
                AnalyzeMainClassConstructors(typeSymbol, dataInfo);
            }

            dataInfo.RentPoolMethod = TypeMetaChecker.AnalyzeRentPoolMethod(typeSymbol, _location);

            return dataInfo;

            #region Method

            // <summary>Records base class members in the current field metadata.</summary>
            // <param name="baseClassSymbol">The base class symbol used by this operation.</param>
            // <param name="dataInfo">The metadata describing the serializable type.</param>
            static void ProcessBaseClassMembers(INamedTypeSymbol baseClassSymbol, LuminDataInfo dataInfo)
            {
                if (baseClassSymbol == null ||
                    baseClassSymbol.SpecialType == SpecialType.System_Object ||
                    baseClassSymbol.SpecialType == SpecialType.System_ValueType) return;

                baseClassSymbol = ReferencedTypeMetadata.Resolve(baseClassSymbol, _analysisState.Compilation);

                #region Base type initialization

                var currentParent = dataInfo;
                while (currentParent.Parent != null)
                {
                    currentParent = currentParent.Parent;
                }

                // At this point currentParent.Parent is null.
                currentParent.Parent = new LuminDataInfo()
                {
                    className = baseClassSymbol.Name,
                    classFileName = baseClassSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    classFullName = baseClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    classNameSpace = baseClassSymbol.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    isGeneric = baseClassSymbol.IsGenericType,
                    isValueType = baseClassSymbol.TypeKind == TypeKind.Struct,
                    TypeSymbol = baseClassSymbol,
                    enableBurst = false,
                    generatorType = TypeMetaChecker.CheckGeneratorType(baseClassSymbol),
                };

                foreach (var iface in baseClassSymbol.AllInterfaces)
                {
                    var fullName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!currentParent.Parent.interfaces.Contains(fullName))
                    {
                        currentParent.Parent.interfaces.Add(fullName);
                    }
                }

                TypeMetaChecker.TryCheckStructLayout(baseClassSymbol, currentParent.Parent);

                if (currentParent.Parent.isGeneric)
                {
                    currentParent.Parent.GenericParameters.AddRange(
                        baseClassSymbol.TypeParameters.Select(t => t.Name)
                    );

                    foreach (var typeParam in baseClassSymbol.TypeParameters)
                    {
                        var constraint = new GenericParameterConstraint
                        {
                            ParameterName = typeParam.Name,
                            IsUnmanaged = typeParam.HasUnmanagedTypeConstraint,
                            IsClass = typeParam.HasReferenceTypeConstraint,
                            IsStruct = typeParam.HasValueTypeConstraint && !typeParam.HasUnmanagedTypeConstraint,
                            IsNotNull = typeParam.HasNotNullConstraint,
                            HasDefault = false,
                            HasNewConstructor = typeParam.HasConstructorConstraint
                        };

                        foreach (var constraintType in typeParam.ConstraintTypes)
                        {
                            constraint.Constraints.Add(
                                constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                            );
                        }

                        currentParent.Parent.GenericConstraints.Add(constraint);
                    }
                }


                #endregion

                // Process the most distant base types first.
                ProcessBaseClassMembers(baseClassSymbol.BaseType, dataInfo);

                // Process members of the current base type.
                ProcessMember(baseClassSymbol, dataInfo, currentParent.Parent);
            }

            // <summary>Records member in the current field metadata.</summary>
            // <param name="symbol">The type or symbol to analyze.</param>
            // <param name="dataInfo">The metadata describing the serializable type.</param>
            // <param name="parent">The parent used by this operation.</param>
            static void ProcessMember(INamedTypeSymbol symbol, LuminDataInfo dataInfo, LuminDataInfo parent = null)
            {
                if (symbol == null) return;

                var allMembers = GetOrderedStorageMembers(symbol);

                foreach (var rawMember in allMembers)
                {
                    _currentSymbol = rawMember;
                    ProcessedTypes.Add(symbol);

                    if (rawMember is IPropertySymbol member)
                    {
                        if (member.IsStatic) continue;
                        if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                        if (!IsAutoProperty(member)) continue;
                        if (parent != null &&
                            member.DeclaredAccessibility == Accessibility.Private &&
                            TypeMetaChecker.TryCheckIncludeAttribute(member))
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.InheritTypeCanNotIncludeParentPrivateMember,
                                member.Locations.FirstOrDefault() ?? _location,
                                _mainSymbol.Name,
                                member.Name));
                            continue;
                        }
                        if (!TypeMetaChecker.TryCheckIncludeAttribute(member))
                        {
                            if (member.DeclaredAccessibility is
                                Accessibility.Private or
                                Accessibility.ProtectedAndInternal or
                                Accessibility.Protected) continue;
                        }

                        var field = new LuminDataField
                        {
                            Name = member.Name,
                            FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            TypeSymbol = member.Type,
                            NameSpace = member.Type.ContainingNamespace?.ToString() ??
                                        member.ContainingNamespace?.ToString() ??
                                        "Your.Data.Namespace",
                            TypeName = member.Type is INamedTypeSymbol namedType ? namedType.ToDisplayString() : member.Type.Name,
                            IsPrivate = member.DeclaredAccessibility is
                                Accessibility.Private or
                                Accessibility.ProtectedAndInternal or
                                Accessibility.Protected,
                            Order = int.MaxValue,
                            FixLength = int.MaxValue,
                            isProperty = true,
                            belongClassName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        TypeMetaChecker.FindAndSetOrderAttribute(ref field, member);
                        if (field.Order == (uint)int.MaxValue)
                            field.Order = (uint)(rawMember.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.SpanStart ?? int.MaxValue);

                        TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, member);

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                        {
                            field.Type = LuminFiledType.Other;
                            if (TypeMetaChecker.TryCheckCompressAttribute(member))
                                field.IsCompress = true;
                            dataInfo.fields.Add(field);
                            parent?.fields.Add(field);
                            continue;
                        }

                        ProcessFieldType(member.Type, field, dataInfo.GenericParameters);

                        if (IsOriginalTypeArrayOrList(member.Type))
                        {
                            if (TypeMetaChecker.TryCheckCompressAttribute(member))
                            {
                                field.IsCompress = true;
                                if (!IsCollectionElementTypeUnmanaged(member.Type))
                                {
                                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                        DiagnosticDescriptors.ManagedArrayDataCompress,
                                        _currentSymbol.Locations.FirstOrDefault() ?? _location,
                                        _mainSymbol.Name,
                                        field.Name,
                                        field.FullTypeName
                                    ));
                                }
                            }
                        }

                        dataInfo.fields.Add(field);
                        parent?.fields.Add(field);
                    }
                    else if (rawMember is IFieldSymbol fieldMember)
                    {
                        if (fieldMember.IsStatic) continue;
                        if (TypeMetaChecker.TryCheckIgnoreAttribute(fieldMember)) continue;
                        if (parent != null &&
                            fieldMember.DeclaredAccessibility == Accessibility.Private &&
                            TypeMetaChecker.TryCheckIncludeAttribute(fieldMember))
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.InheritTypeCanNotIncludeParentPrivateMember,
                                fieldMember.Locations.FirstOrDefault() ?? _location,
                                _mainSymbol.Name,
                                fieldMember.Name));
                            continue;
                        }
                        if (!TypeMetaChecker.TryCheckIncludeAttribute(fieldMember))
                        {
                            if (fieldMember.IsImplicitlyDeclared ||
                                fieldMember.DeclaredAccessibility is
                                    Accessibility.Private or
                                    Accessibility.ProtectedAndInternal or
                                    Accessibility.Protected) continue;
                        }

                        var field = new LuminDataField
                        {
                            Name = fieldMember.Name,
                            FullTypeName = fieldMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            TypeSymbol = fieldMember.Type,
                            IsVolatile = fieldMember.IsVolatile,
                            NameSpace = fieldMember.Type.ContainingNamespace?.ToString() ??
                                        fieldMember.ContainingNamespace?.ToString() ??
                                        "Your.Data.Namespace",
                            TypeName = fieldMember.Type is INamedTypeSymbol namedType ? namedType.ToDisplayString() : fieldMember.Type.Name,
                            IsPrivate = fieldMember.DeclaredAccessibility is
                                Accessibility.Private or
                                Accessibility.ProtectedAndInternal or
                                Accessibility.Protected,
                            Order = int.MaxValue,
                            FixLength = int.MaxValue,
                            belongClassName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        };

                        TypeMetaChecker.FindAndSetOrderAttribute(ref field, fieldMember);
                        if (field.Order == (uint)int.MaxValue)
                            field.Order = (uint)(rawMember.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.SpanStart ?? int.MaxValue);

                        TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, fieldMember);

                        if (TypeMetaChecker.TryCheckPackableObjectAttribute(fieldMember))
                        {
                            field.Type = LuminFiledType.Other;
                            if (TypeMetaChecker.TryCheckCompressAttribute(fieldMember))
                                field.IsCompress = true;
                            dataInfo.fields.Add(field);
                            parent?.fields.Add(field);
                            continue;
                        }

                        ProcessFieldType(fieldMember.Type, field, dataInfo.GenericParameters);

                        if (IsOriginalTypeArrayOrList(fieldMember.Type))
                        {
                            if (TypeMetaChecker.TryCheckCompressAttribute(fieldMember))
                            {
                                field.IsCompress = true;
                                if (!IsCollectionElementTypeUnmanaged(fieldMember.Type))
                                {
                                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                        DiagnosticDescriptors.ManagedArrayDataCompress,
                                        _currentSymbol.Locations.FirstOrDefault() ?? _location,
                                        _mainSymbol.Name,
                                        field.Name,
                                        field.FullTypeName
                                    ));
                                }
                            }
                        }

                        dataInfo.fields.Add(field);
                        parent?.fields.Add(field);
                    }
                }
            }

            #endregion

        }
    }
}
