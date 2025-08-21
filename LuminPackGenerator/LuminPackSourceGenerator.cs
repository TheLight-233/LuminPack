using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class LuminPackSourceGenerator : IIncrementalGenerator
    {
        private const string LUMIN_PACKABLE_ATTRIBUTE = "LuminPack.Attribute.LuminPackableAttribute";
        private const string LUMIN_UNION_ATTRIBUTE = "LuminPack.Attribute.LuminPackUnionAttribute";
        public const string LUMIN_GENERATED_NAMESPACE = "LuminPack.Generated";
        
        private static ISymbol _currentSymbol;
        private static ISymbol _mainSymbol;
        private static Location _location;
        private static MetaInfo _metadata;

        [ThreadStatic] private static HashSet<INamedTypeSymbol> ProcessedTypes;
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            try
            {
                 var metaInfo = context.ParseOptionsProvider.Select((parseOptions, _) =>
                {
                    var csOptions = (CSharpParseOptions)parseOptions;
                    var langVersion = csOptions.LanguageVersion;
                    var net8 = csOptions.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER");
                    _metadata = new MetaInfo(csOptions, langVersion, net8);
                    return _metadata;
                }).WithTrackingName("LuminPack.LuminPackable.0_ParseOptionsProvider");
                 
                
                var typeDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
                    LUMIN_PACKABLE_ATTRIBUTE,
                    static (node, _) => node 
                        is ClassDeclarationSyntax 
                        or StructDeclarationSyntax 
                        or InterfaceDeclarationSyntax 
                        or RecordDeclarationSyntax,
                    static (context, _) =>
                    {
                        ProcessedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                        return CreateLuminDataInfo(context.TargetSymbol);
                    }).WithTrackingName("LuminPack.LuminPackable.1_ForAttributeLuminPackableAttribute");

                var provider = typeDeclarations
                    .Combine(context.CompilationProvider)
                    .WithComparer(Comparer.Instance)
                    .Combine(metaInfo)
                    .WithTrackingName("LuminPack.LuminPackable.2_LuminPackCombined");
                
                context.RegisterSourceOutput(provider, static (context, source) =>
                {
                    if (TypeMetaChecker.TryReportContext(ref context))
                    {
                        return;
                    }

                    var dataInfo = source.Left.Item1;
                    var metaInfo = source.Right;
                    
                    var code = LuminPackCodeGenerator.CodeGenerator(dataInfo, metaInfo);
                    
                    if (string.IsNullOrEmpty(code)) return;
                    
                    var name = dataInfo.classFullName.Replace("<", "_").Replace(">", "_").Replace(",", "_");
                    
                    context.AddSource($"{name}Parser.g.cs", code);
                    
                });
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\LuminPack_log.txt", 
                    $"{DateTime.Now}: Generator failed\n{ex}");
            }
            
        }

        private static LuminDataInfo CreateLuminDataInfo(ISymbol symbol)
        {
            _mainSymbol = symbol;
            var typeSymbol = (INamedTypeSymbol)symbol;
            _location = symbol.Locations.FirstOrDefault();
            
            ProcessedTypes.Add(typeSymbol);
            
            var dataInfo = new LuminDataInfo
            {
                className = typeSymbol.Name,
                classFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                classNameSpace = typeSymbol.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                isGeneric = typeSymbol.IsGenericType,
                isValueType = typeSymbol.TypeKind == TypeKind.Struct,
                enableBurst = false,
                generatorType = TypeMetaChecker.CheckGeneratorType(typeSymbol)
            };

            #region Diagnostics

            //Check Layout
            TypeMetaChecker.TryCheckStructLayout(typeSymbol, dataInfo);
            
            if (symbol.IsAbstract)
            {
                if (!TypeMetaChecker.TryCheckUnionAttribute(typeSymbol))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.AbstractMustUnion,
                        _location,
                        symbol.Name
                    ));
                }
                
                dataInfo.isUnion = true;
            }
            
            if (TypeMetaChecker.TryCheckUnionAttribute(typeSymbol))
            {
                if (symbol.IsSealed)
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.SealedTypeCantBeUnion,
                        _location,
                        symbol.Name
                    ));
                }

                if (typeSymbol.TypeKind == TypeKind.Struct)
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.UnionMemberNotAllowStruct,
                        _location,
                        symbol.Name
                    ));
                }

                if (!symbol.IsAbstract)
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConcreteTypeCantBeUnion,
                        _location,
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
                    dataInfo.UnionMembers.Add(new LuminUnionMemberInfo(id, memberType));
                }
                
                foreach (var member in dataInfo.UnionMembers)
                {
                    // 1. 检查成员类型的泛型参数数量是否超过基类
                    int memberGenericCount = member.Type.OriginalDefinition.TypeParameters.Length;
                    
                    if (memberGenericCount > typeSymbol.TypeParameters.Length)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.UnionMemberGenericCountExceed,
                            _location,
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
                            _location,
                            member.Id,
                            symbol.Name
                        ));
                    }

                    // 3. check member is luminpackable
                    if (!TypeMetaChecker.TryCheckPackableAttribute(member.Type))
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.UnionMemberMustBeLuminPackable,
                            _location,
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
                                _location,
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
                                _location,
                                member.Type.Name,
                                typeSymbol.Name
                            ));
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
                    _location,
                    symbol.Name
                ));
            }

            if (typeSymbol.IsRefLikeType)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.TypeIsRefStruct,
                    _location,
                    symbol.Name));
            }

            #endregion

            //处理泛型限制
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
                        // 当存在 unmanaged 时，不标记 IsStruct
                        IsStruct = typeParam.HasValueTypeConstraint && !typeParam.HasUnmanagedTypeConstraint,
                        IsNotNull = typeParam.HasNotNullConstraint,
                        HasDefault = typeParam.HasUnmanagedTypeConstraint && !typeParam.HasUnmanagedTypeConstraint,
                        HasNewConstructor = typeParam.HasConstructorConstraint
                    };

                    // 添加类型约束（如接口、基类）
                    foreach (var constraintType in typeParam.ConstraintTypes)
                    {
                        constraint.Constraints.Add(
                            constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        );
                        
                    }

                    dataInfo.GenericConstraints.Add(constraint);
                }
            }
            
            //处理类所有字段和属性（按声明排序）
            SetClassLocalField(typeSymbol, dataInfo.localFields);
            
            // 递归收集基类成员（从直接基类开始，排除System.Object）
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
                    symbol.Name
                ));
            }
            
            TypeMetaChecker.CheckSerializeCallBack(typeSymbol, dataInfo);
            
            return dataInfo;

            #region Method
            
            static void ProcessBaseClassMembers(INamedTypeSymbol baseClassSymbol, LuminDataInfo dataInfo)
            {
                if (baseClassSymbol == null || baseClassSymbol.SpecialType == SpecialType.System_Object) return;

                // 先处理更上层的基类
                ProcessBaseClassMembers(baseClassSymbol.BaseType, dataInfo);

                // 处理当前基类的成员
                ProcessMember(baseClassSymbol, dataInfo);
            }
            
            static void ProcessMember(INamedTypeSymbol symbol, LuminDataInfo dataInfo)
            {
                if (symbol == null) return;
                
                // 处理属性（IPropertySymbol）
                foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
                {
                    _currentSymbol = member;
                    ProcessedTypes.Add(symbol);
                
                    if (member.IsStatic) continue;
                
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                
                    if (TypeMetaChecker.TryCheckIncludeAttribute(member)) goto Set;
                
                    if (member.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected) continue; // 忽略静态属性
                
                
                    Set:
                    var field = new LuminDataField
                    {
                        Name = member.Name, // 直接使用属性名称
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
                        isProperty = true
                    };
                
                    //Set Order
                    TypeMetaChecker.FindAndSetOrderAttribute(ref field, member);
                
                    TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, member);

                    if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                    {
                        field.Type = LuminFiledType.Other;
                    
                        dataInfo.fields.Add(field);

                        continue;
                    }
                
                    ProcessFieldType(member.Type, field, dataInfo.GenericParameters);
                
                
                    dataInfo.fields.Add(field);
                    
                    ProcessedTypes.Clear();
                }
            
                // 处理字段（IFieldSymbol）
                foreach (var member in symbol.GetMembers().OfType<IFieldSymbol>())
                {
                    _currentSymbol = member;
                    ProcessedTypes.Add(symbol);
                    
                    if (member.IsStatic) continue;
                
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                
                    if (TypeMetaChecker.TryCheckIncludeAttribute(member)) goto Set;
                
                    if (member.IsImplicitlyDeclared || 
                        member.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected) continue; // 忽略静态字段和自动生成的字段
                

                    Set:
                
                    var field = new LuminDataField
                    {
                        Name = member.Name,
                        NameSpace = member.Type.ContainingNamespace?.ToString() ?? 
                                    member.ContainingNamespace?.ToString() ?? 
                                    "Your.Data.Namespace",
                        TypeName = member.Type is INamedTypeSymbol namedType ? namedType.ToDisplayString() : member.Type.Name,
                        IsPrivate = member.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected,
                        Order = int.MaxValue,
                        FixLength = int.MaxValue
                    };
                
                
                    //Set Order
                    TypeMetaChecker.FindAndSetOrderAttribute(ref field, member);
                
                    TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, member);
                
                    if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                    {
                        field.Type = LuminFiledType.Other;
                    
                        dataInfo.fields.Add(field);

                        continue;
                    }
                
                    ProcessFieldType(member.Type, field, dataInfo.GenericParameters);
                    
                    dataInfo.fields.Add(field);
                    
                    ProcessedTypes.Clear();
                }
                
            }
            
            #endregion
            
        }

        private static void ProcessFieldType(ITypeSymbol type, LuminDataField field, List<string> genericParameters = null)
        {
            if (TypeMetaChecker.TryCheckIsNotRefLike(type))
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MemberIsRefStruct,
                    type.Locations.FirstOrDefault(),
                    type.Name
                ));
            }
            
            field.FieldType = type.IsReferenceType ? LuminDataType.Reference : LuminDataType.Value;
            
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
                
                switch (namedType.SpecialType)
                {
                    case SpecialType.System_Boolean:
                        field.Type = LuminFiledType.Bool;
                        break;
                    case SpecialType.System_Byte:
                        field.Type = LuminFiledType.Byte;
                        break;
                    case SpecialType.System_SByte:
                        field.Type = LuminFiledType.Byte;
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
                    case SpecialType.System_String:
                        field.Type = LuminFiledType.String;
                        break;
                    default:
                        if (namedType.TypeKind is TypeKind.Enum)
                        {
                            field.Type = LuminFiledType.Enum;
                            field.EnumType = GetEnumFieldType(namedType);
                        }
                        else if (namedType.TypeKind is TypeKind.Class or TypeKind.Struct)
                        {
                            var listSymbol = namedType.ContainingAssembly.GetTypeByMetadataName("System.Collections.Generic.List`1");
                            
                            if (listSymbol != null && namedType.OriginalDefinition.Equals(listSymbol, SymbolEqualityComparer.Default))
                            {
                                field.Type = LuminFiledType.List;
                                ProcessGenericArguments(namedType.TypeArguments, field, genericParameters);
                            }
                            else if (ParserMap.Parsers.Contains(metadataName))
                            {
                                field.Type = LuminFiledType.Other;
                            }
                            else if (namedType.IsGenericType)
                            {
                                
                                // 检查是否已处理过该类型
                                if (ProcessedTypes.Contains(namedType))
                                {
                                    field.Type = LuminFiledType.Other;
                                    return;
                                }
        
                                // 添加到已处理集合
                                ProcessedTypes.Add(namedType);
                                
                                // 处理普通泛型类（如Test<T>）
                                field.Type = namedType.TypeKind == TypeKind.Class ? LuminFiledType.Class : LuminFiledType.Struct;
                                field.ClassName = namedType.Name;
                                // 提取泛型参数并填充到ClassGenericType（例如Test<string> -> "string"）
                                field.ClassGenericType = string.Join(", ", namedType.TypeArguments.Select(t => t.Name));
                                
                                if (namedType.IsRecord || namedType.IsAbstract)
                                {
                                    field.Type = LuminFiledType.Other;
                                
                                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                
                                    return;
                                }
                                
                                SetClassLocalField(namedType, field.localFields);
                                
                                GetConstructParameterCount(namedType, field);
                
                                foreach (var nestedTypeArg in namedType.TypeArguments)
                                {
                                    if (genericParameters is not null && genericParameters.Contains(nestedTypeArg.Name))
                                    {
                                        field.Type = LuminFiledType.Other;
                                        
                                        return;
                                    }
                                    
                                }
                                
                                // 递归处理嵌套属性
                                foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
                                {
                                    if (member.IsStatic) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIncludeAttribute(member) && !member.Type.IsAnonymousType)
                                    {
                                        if (_metadata is not null && !_metadata.IsNet8)
                                        {
                                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                                _location,
                                                _mainSymbol.Name, type.Name
                                            ));
                                            continue;
                                        }
                                        
                                        goto Set;
                                    }
                                    
                                    if (member.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected) continue;
                                    
                                    Set:
                                    if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                                    {
                                        field.Type = LuminFiledType.Other;
                        
                                        //CheckLuminPackable
                                        TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                        
                                        return;
                                    }
                                    
                                    var nestedField = new LuminDataField
                                    {
                                        Name = member.Name,
                                        NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                                        TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                                        IsPrivate = member.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected,
                                        isProperty = true
                                    };
                                    
                                    if (genericParameters?.Contains(member.Type.Name) is true ||
                                        TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                                    {
                                        nestedField.Type = LuminFiledType.Other;
                                        
                                        field.ClassFields.Add(nestedField);
                                        
                                        continue;
                                    }
                                    
                                    ProcessFieldType(member.Type, nestedField, genericParameters);
                                    
                                    field.ClassFields.Add(nestedField);
                                }
                                
                                // 递归处理嵌套字段
                                foreach (var nestedMember in namedType.GetMembers().OfType<IFieldSymbol>())
                                {
                                    if (nestedMember.IsStatic) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember) && !nestedMember.Type.IsAnonymousType)
                                    {
                                        if (_metadata is not null && !_metadata.IsNet8)
                                        {
                                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                                _location,
                                                _mainSymbol.Name, type.Name
                                            ));
                                            continue;
                                        }
                                        
                                        goto Set;
                                    }
                                    
                                    if (nestedMember.IsImplicitlyDeclared || 
                                        nestedMember.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected) continue; // 忽略自动生成的字段
                                    
                                    Set:
                                    if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                                    {
                                        field.Type = LuminFiledType.Other;
                        
                                        //CheckLuminPackable
                                        TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                        
                                        return;
                                    }
                                    
                                    var nestedField = new LuminDataField
                                    {
                                        Name = nestedMember.Name,
                                        NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                                        TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                                        IsPrivate = nestedMember.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected
                                    };
                                    
                                    if (genericParameters?.Contains(nestedMember.Type.Name) is true || 
                                        TypeMetaChecker.TryCheckPackableObjectAttribute(nestedMember))
                                    {
                                        nestedField.Type = LuminFiledType.Other;
                                        
                                        field.ClassFields.Add(nestedField);
                                        
                                        continue;
                                    }
                                    
                                    ProcessFieldType(nestedMember.Type, nestedField, genericParameters);
                                    field.ClassFields.Add(nestedField);
                                }
                            }
                            else
                            {
                                
                                // 检查是否已处理过该类型
                                if (ProcessedTypes.Contains(namedType))
                                {
                                    field.Type = LuminFiledType.Other;
                                    return;
                                }
        
                                // 添加到已处理集合
                                ProcessedTypes.Add(namedType);
                                
                                field.Type = namedType.TypeKind == TypeKind.Class ? LuminFiledType.Class : LuminFiledType.Struct;
                                field.ClassName = namedType.Name;
                                
                                if (namedType.IsRecord || namedType.IsAbstract)
                                {
                                    field.Type = LuminFiledType.Other;
                                
                                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                
                                    return;
                                }
                                
                                SetClassLocalField(namedType, field.localFields);
                                
                                GetConstructParameterCount(namedType, field);
                                
                                // 递归处理嵌套属性
                                foreach (var member in namedType.GetMembers().OfType<IPropertySymbol>())
                                {
                
                                    if (member.IsStatic) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                                    
                                    if (TypeMetaChecker.TryCheckIncludeAttribute(member))
                                    {
                                        if (_metadata is not null && !_metadata.IsNet8)
                                        {
                                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                                _location,
                                                _mainSymbol.Name, type.Name
                                            ));
                                            continue;
                                        }
                                        goto Set;
                                    }
                                    
                                    if (member.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected) continue;
                                    
                                    Set:
                                    if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(member))
                                    {
                                        field.Type = LuminFiledType.Other;
                        
                                        //CheckLuminPackable
                                        TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                        
                                        return;
                                    }
                                    
                                    var nestedField = new LuminDataField
                                    {
                                        Name = member.Name,
                                        NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                                        TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                                        IsPrivate = member.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected,
                                        isProperty = true
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
                                
                                // 递归处理嵌套字段
                                foreach (var nestedMember in namedType.GetMembers().OfType<IFieldSymbol>())
                                {
                                    
                                    _currentSymbol = nestedMember;
                
                                    if (nestedMember.IsStatic) continue;
                
                                    if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;
                
                                    if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember))
                                    {
                                        if (_metadata is not null && !_metadata.IsNet8)
                                        {
                                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                                _location,
                                                _mainSymbol.Name, type.Name
                                            ));
                                            continue;
                                        }
                                        
                                        goto Set;
                                    }
                                    
                                    if (nestedMember.IsImplicitlyDeclared || 
                                        nestedMember.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected) continue; // 忽略自动生成的字段
                                    
                                    Set:
                                    if (TypeMetaChecker.TryCheckFieldStructIsReadOnly(nestedMember))
                                    {
                                        field.Type = LuminFiledType.Other;
                        
                                        //CheckLuminPackable
                                        TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                                        
                                        return;
                                    }
                                    
                                    var nestedField = new LuminDataField
                                    {
                                        Name = nestedMember.Name,
                                        NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                                        TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                                        IsPrivate = nestedMember.DeclaredAccessibility is 
                                            Accessibility.Private or 
                                            Accessibility.ProtectedAndInternal or 
                                            Accessibility.Protected
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
                        }
                        break;
                }
            }
            else if (type is IArrayTypeSymbol arrayType)
            {
                // 处理数组类型
                field.Type = LuminFiledType.Array;
                field.FieldType = LuminDataType.Reference;
                // 递归处理数组元素类型
                ProcessArrayArguments(arrayType, field, genericParameters);
            }
        }

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

                    if (ParserMap.Parsers.Contains(metadataName))
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

                if (ParserMap.Parsers.Contains(metadataName))
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

        private static void ProcessClassFiled(INamedTypeSymbol namedTypeArg, LuminDataField field, bool isFullName = true, List<string> genericParameters = null)
        {
            if (ProcessedTypes.Contains(namedTypeArg))
            {
                field.Type = LuminFiledType.Other;
                return;
            }
    
            // 添加到已处理集合
            ProcessedTypes.Add(namedTypeArg);
            
            if (namedTypeArg.IsGenericType)
            {
                field.ClassName = isFullName ? namedTypeArg.ToDisplayString() : namedTypeArg.Name;
                // 提取泛型参数并填充到ClassGenericType（例如Test<string> -> "string"）
                field.ClassGenericType = string.Join(", ", namedTypeArg.TypeArguments.Select(t => t.Name));
                field.NameSpace = namedTypeArg.ContainingNamespace.ToString() ?? "Your.Data.Namespace";
                
                
                if (namedTypeArg.IsRecord || namedTypeArg.IsAbstract)
                {
                    field.Type = LuminFiledType.Other;
                    
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedTypeArg, _currentSymbol.Locations.FirstOrDefault());
                                
                    return;
                }
                
                SetClassLocalField(namedTypeArg, field.localFields);
                
                GetConstructParameterCount(namedTypeArg, field);
                
                foreach (var nestedTypeArg in namedTypeArg.TypeArguments)
                {
                    if (genericParameters is not null && genericParameters.Contains(nestedTypeArg.Name))
                    {
                        field.Type = LuminFiledType.Other;
                        return;
                    }
                    
                }
                
                
                // 递归处理嵌套属性
                foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                {
                    if (member.IsStatic) continue;
                    
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                    if (TypeMetaChecker.TryCheckIncludeAttribute(member) && !member.Type.IsAnonymousType)
                    {
                        if (_metadata is not null && !_metadata.IsNet8)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                _location,
                                _mainSymbol.Name, namedTypeArg.Name
                            ));
                            continue;
                        }
                        goto Set;
                    }
                    
                    if (member.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected) continue; // 忽略静态属性
                    
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
                        NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                        IsPrivate = member.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected,
                        isProperty = true
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
                
                // 递归处理嵌套字段
                foreach (var nestedMember in namedTypeArg.GetMembers().OfType<IFieldSymbol>())
                {
                    if (nestedMember.IsStatic) continue;
                                    
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;

                    if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember) && !nestedMember.Type.IsAnonymousType)
                    {
                        if (_metadata is not null && !_metadata.IsNet8)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                _location,
                                _mainSymbol.Name, namedTypeArg.Name
                            ));
                            continue;
                        }
                        
                        goto Set;
                    }
                    
                    if (nestedMember.IsImplicitlyDeclared || 
                        nestedMember.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected) continue; // 忽略自动生成的字段
                    
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
                        NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                        IsPrivate = nestedMember.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected
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
                
                GetConstructParameterCount(namedTypeArg, field);
                
                // 递归处理嵌套属性
                foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                {
                    
                    _currentSymbol = member;
                
                    if (member.IsStatic) continue;
                
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                
                    if (TypeMetaChecker.TryCheckIncludeAttribute(member))
                    {
                        if (_metadata is not null && !_metadata.IsNet8)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                _location,
                                _mainSymbol.Name, namedTypeArg.Name
                            ));
                            continue;
                        }
                        
                        goto Set;
                    }
                                    
                    if (member.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected) continue; // 忽略自动生成的字段
                                    
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
                        NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                        IsPrivate = member.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected,
                        isProperty = true
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
                
                // 递归处理嵌套字段
                foreach (var nestedMember in namedTypeArg.GetMembers().OfType<IFieldSymbol>())
                {
                    _currentSymbol = nestedMember;
                
                    if (nestedMember.IsStatic) continue;
                
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(nestedMember)) continue;
                
                    if (TypeMetaChecker.TryCheckIncludeAttribute(nestedMember))
                    {
                        if (_metadata is not null && !_metadata.IsNet8)
                        {
                            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                                DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                                _location,
                                _mainSymbol.Name, namedTypeArg.Name
                            ));
                            continue;
                        }
                        
                        goto Set;
                    }
                    
                    if (nestedMember.IsImplicitlyDeclared || 
                        nestedMember.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected) continue; // 忽略静态字段和自动生成的字段
                    
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
                        NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                        IsPrivate = nestedMember.DeclaredAccessibility is 
                            Accessibility.Private or 
                            Accessibility.ProtectedAndInternal or 
                            Accessibility.Protected
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
        }

        private static LuminFiledType ConvertToLuminFieldType(INamedTypeSymbol symbol)
        {
            // 优先处理特殊类型（基础类型）
            switch (symbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return LuminFiledType.Bool;
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                    return LuminFiledType.Byte;
                case SpecialType.System_Int16:
                    return LuminFiledType.Short;
                case SpecialType.System_UInt16:
                    return LuminFiledType.UShort;
                case SpecialType.System_Int32:
                    return LuminFiledType.Int;
                case SpecialType.System_UInt32:
                    return LuminFiledType.UInt;
                case SpecialType.System_Int64:
                    return LuminFiledType.Long;
                case SpecialType.System_UInt64:
                    return LuminFiledType.ULong;
                case SpecialType.System_Single:
                    return LuminFiledType.Float;
                case SpecialType.System_Double:
                    return LuminFiledType.Double;
                case SpecialType.System_String:
                    return LuminFiledType.String;
            }

            // 处理类/结构体/枚举类型
            return symbol.TypeKind switch
            {
                TypeKind.Class => LuminFiledType.Class,
                TypeKind.Struct => LuminFiledType.Struct,
                TypeKind.Enum => LuminFiledType.Enum,
                _ => LuminFiledType.Class // 默认返回Class
            };
        }

        private static LuminGenericsType ConvertToLuminGenericsType(INamedTypeSymbol type)
        {
            if (type.SpecialType != SpecialType.None)
            {
                return type.SpecialType switch
                {
                    SpecialType.System_Byte => LuminGenericsType.Byte,
                    SpecialType.System_Int16 => LuminGenericsType.Short,
                    SpecialType.System_UInt16  => LuminGenericsType.UShort,
                    SpecialType.System_Int32 => LuminGenericsType.Int,
                    SpecialType.System_UInt32  => LuminGenericsType.UInt,
                    SpecialType.System_Int64 => LuminGenericsType.Long,
                    SpecialType.System_UInt64  => LuminGenericsType.ULong,
                    SpecialType.System_Single => LuminGenericsType.Float,
                    SpecialType.System_Double => LuminGenericsType.Double,
                    SpecialType.System_String => LuminGenericsType.String,
                    SpecialType.System_Boolean => LuminGenericsType.Bool,
                };
            }

            // 处理嵌套泛型（如List<List<int>>）
            var listSymbol = type.ContainingAssembly.GetTypeByMetadataName("System.Collections.Generic.List`1");
            if (listSymbol != null && type.OriginalDefinition.Equals(listSymbol, SymbolEqualityComparer.Default))
            {
                return LuminGenericsType.List;
            }
            
            return type.TypeKind == TypeKind.Class ? LuminGenericsType.Class : LuminGenericsType.Struct;
        }

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

        private static void SetClassLocalField(INamedTypeSymbol typeSymbol, List<LuminLocalFieldData> localFields)
        {
            
            // 递归处理基类（排除System.Object）
            if (typeSymbol.BaseType != null && 
                typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
            {
                SetClassLocalField(typeSymbol.BaseType, localFields);
            }

            // 获取当前类的成员并按声明顺序排序
            var members = typeSymbol.GetMembers()
                .Where(m => m is IPropertySymbol or IFieldSymbol)
                .OrderBy(m =>
                {
                    var syntaxRef = m.DeclaringSyntaxReferences.FirstOrDefault();
                    return syntaxRef?.GetSyntax()?.SpanStart ?? int.MaxValue;
                })
                .ToList();

            // 添加当前类的成员到列表
            foreach (var member in members)
            {
                if (member.IsImplicitlyDeclared) continue;

                string typeName = "";
                bool isValue = false;
                
                if (member is IPropertySymbol property)
                {
                    typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    isValue = property.Type.IsValueType;
                }
                else if (member is IFieldSymbol field)
                {
                    typeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    isValue = field.Type.IsValueType;
                }
                    

                if (localFields.Any(x => x.Name == member.Name))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ContainsDuplicateNameField,
                        typeSymbol.Locations.FirstOrDefault(),
                        member.Name
                    ));
                }

                TypeMetaChecker.TryCheckAndGetFiledOffsetAttribute(member, out var offset);
                
                localFields.Add(new LuminLocalFieldData
                {
                    TypeName = typeName,
                    Name = member.Name,
                    filedOffset = offset,
                    IsValue = isValue,
                });
            }
        }

        private static void GetConstructParameterCount(INamedTypeSymbol typeSymbol, LuminDataField field)
        {
            if (!TypeMetaChecker.TryCheckConstructorHasPublic(typeSymbol))
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.ConstructorNoPublic,
                    _currentSymbol.Locations.FirstOrDefault(),
                    _currentSymbol.Name
                ));
                
                return;
            }
            
            var constructors = typeSymbol.Constructors
                .ToList()
                .Where(x => x.DeclaredAccessibility is Accessibility.Public)
                .Min(x => x.Parameters.Length);
            
            field.ConstructParameterCount = constructors;
        }
        
    }
}