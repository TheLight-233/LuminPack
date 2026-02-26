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
        public const string LUMIN_PACKABLE_ATTRIBUTE = "LuminPack.Attribute.LuminPackableAttribute";
        public const string LUMIN_UNION_ATTRIBUTE = "LuminPack.Attribute.LuminPackUnionAttribute";
        public const string LUMIN_GENERATED_NAMESPACE = "LuminPack.Generated";
        public const string LUMIN_REGISTERS_NAMESPACE = "LuminPackRegisters";
        
        private static ISymbol _currentSymbol;
        private static ISymbol _mainSymbol;
        private static Location _location;
        private static MetaInfo _metadata;

        [ThreadStatic] private static HashSet<INamedTypeSymbol> ProcessedTypes;
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            try
            {
                var parseOptionsInfo = context.ParseOptionsProvider.Select((parseOptions, _) =>
                {
                    var csOptions = (CSharpParseOptions)parseOptions;
                    var langVersion = csOptions.LanguageVersion;
                    var net8 = csOptions.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER");
                    return (csOptions, langVersion, net8);
                }).WithTrackingName("LuminPack.LuminPackable.0_ParseOptionsProvider");
                
                var metaInfo = parseOptionsInfo
                    .Combine(context.CompilationProvider)
                    .Select((combined, _) =>
                    {
                        var (parseInfo, compilation) = combined;
                        var (csOptions, langVersion, net8) = parseInfo;
                
                        // 从 CompilationOptions 获取 unsafe 标志
                        var allowUnsafe = compilation.Options is CSharpCompilationOptions csharpOptions 
                            ? csharpOptions.AllowUnsafe 
                            : false;
                
                        _metadata = new MetaInfo(csOptions, langVersion, net8, allowUnsafe);
                        return _metadata;
                    }).WithTrackingName("LuminPack.LuminPackable.0_MetaInfo");

        
                var typeDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
                    LUMIN_PACKABLE_ATTRIBUTE,
                    static (node, _) => node 
                        is ClassDeclarationSyntax 
                        or StructDeclarationSyntax 
                        or InterfaceDeclarationSyntax 
                        or RecordDeclarationSyntax,
                    static (context, _) =>
                    {
                        try
                        {
                            ProcessedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                            return CreateLuminDataInfo(context.TargetSymbol, context.SemanticModel.Compilation);
                        }
                        catch (Exception ex)
                        {
                            // // 捕获创建LuminDataInfo时的异常
                            // var errorMsg = $"{DateTime.Now}: CreateLuminDataInfo failed for {context.TargetSymbol?.Name}\n{ex}\nStack: {ex.StackTrace}";
                            // File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LuminPack_CreateLuminDataInfo_error.txt"), errorMsg);
                            throw;
                        }
                    }).WithTrackingName("LuminPack.LuminPackable.1_ForAttributeLuminPackableAttribute");

                var provider = typeDeclarations
                    .Combine(context.CompilationProvider)
                    .WithComparer(Comparer.Instance)
                    .Combine(metaInfo)
                    .WithTrackingName("LuminPack.LuminPackable.2_LuminPackCombined");
        
                context.RegisterSourceOutput(provider, static (context, source) =>
                {
                    try
                    {
                        if (TypeMetaChecker.TryReportContext(ref context))
                        {
                            return;
                        }

                        var dataInfo = source.Left.Item1;
                        var compilation = source.Left.Item2;
                        var metaInfo = source.Right;
                
                        var code = LuminPackCodeGenerator.CodeGenerator(dataInfo, metaInfo);
                        var extension = LuminPackExtensionGenerator.CodeGenerator(dataInfo, metaInfo, compilation);
                        if (string.IsNullOrEmpty(code)) return;
                
                        var name = dataInfo.classFullName;
                        if (name.StartsWith("global::"))
                        {
                            name = name.Substring(8);
                        }
                        name = name.Replace("<", "_").Replace('>', '_');
                        
                        context.AddSource($"{name}Parser.g.cs", code);
                        context.AddSource($"{name}Parser.Extension.g.cs", extension);
                        
                        
                    }
                    catch (Exception ex)
                    {
                        // // 捕获代码生成时的异常
                        // System.Diagnostics.StackTrace trace = new System.Diagnostics.StackTrace(ex, true);
                        // var errorMsg = $"{DateTime.Now}: Code generation failed\n{ex}\nStack: {trace}";
                        // File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LuminPack_CodeGen_error.txt"), errorMsg);
                        //
                        // // 同时报告诊断信息，让用户在IDE中看到错误
                        // context.ReportDiagnostic(Diagnostic.Create(
                        //     new DiagnosticDescriptor(
                        //         "LUMINPACK001",
                        //         "Code Generation Failed",
                        //         $"LuminPack code generation failed: {ex.Message}",
                        //         "LuminPack",
                        //         DiagnosticSeverity.Error,
                        //         true),
                        //     Location.None));
                    }
                });
            }
            catch (Exception ex)
            {
                // 捕获初始化过程中的异常
                // var errorMsg = $"{DateTime.Now}: Generator initialization failed\n{ex}\nStack: {ex.StackTrace}";
                // File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LuminPack_Init_error.txt"), errorMsg);
            }
        }

        private static LuminDataInfo CreateLuminDataInfo(ISymbol symbol, Compilation compilation)
        {
            _mainSymbol = symbol;
            var typeSymbol = (INamedTypeSymbol)symbol;
            _location = symbol.Locations.FirstOrDefault();

            if (typeSymbol.ContainingType != null)
            {
                CheckNestedClassAccessibility(typeSymbol);
            }
            
            ProcessedTypes.Add(typeSymbol);
            
            var dataInfo = new LuminDataInfo
            {
                className = typeSymbol.Name,
                classFileName = typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                classFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                classNameSpace = typeSymbol.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                isGeneric = typeSymbol.IsGenericType,
                isValueType = typeSymbol.TypeKind == TypeKind.Struct,
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
            
            #region Diagnostics

            //Check Layout
            TypeMetaChecker.TryCheckStructLayout(typeSymbol, dataInfo);
            
            if (symbol.IsAbstract)
            {
                // if (!TypeMetaChecker.TryCheckUnionAttribute(typeSymbol))
                // {
                //     TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                //         DiagnosticDescriptors.AbstractMustUnion,
                //         _location,
                //         symbol.Name
                //     ));
                // }
                
                dataInfo.isUnion = true;

                dataInfo.IsWideTag = TypeMetaChecker.TryCheckWideTagAttribute(typeSymbol);
            }
            
            if (TypeMetaChecker.TryCheckUnionAttribute(typeSymbol) || symbol.IsAbstract)
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
                
                DiscoverAndRegisterDerivedTypes(typeSymbol, dataInfo, compilation);
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
            
            AnalyzeMainClassConstructors(typeSymbol, dataInfo);
            
            dataInfo.RentPoolMethod = TypeMetaChecker.AnalyzeRentPoolMethod(typeSymbol, _location);
            
            return dataInfo;

            #region Method
            
            static void ProcessBaseClassMembers(INamedTypeSymbol baseClassSymbol, LuminDataInfo dataInfo)
            {
                if (baseClassSymbol == null || 
                    baseClassSymbol.SpecialType == SpecialType.System_Object ||
                    baseClassSymbol.SpecialType == SpecialType.System_ValueType) return;

                #region 父类初始化

                var currentParent = dataInfo;
                while (currentParent.Parent != null)
                {
                    currentParent = currentParent.Parent;
                }
                
                // 现在 currentParent.Parent == null
                currentParent.Parent = new LuminDataInfo()
                {
                    className = baseClassSymbol.Name,
                    classFileName = baseClassSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    classFullName = baseClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    classNameSpace = baseClassSymbol.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    isGeneric = baseClassSymbol.IsGenericType,
                    isValueType = baseClassSymbol.TypeKind == TypeKind.Struct,
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
                
                // 先处理更上层的基类
                ProcessBaseClassMembers(baseClassSymbol.BaseType, dataInfo);

                // 处理当前基类的成员
                ProcessMember(baseClassSymbol, dataInfo, currentParent.Parent);
            }
            
            static void ProcessMember(INamedTypeSymbol symbol, LuminDataInfo dataInfo, LuminDataInfo parent = null)
            {
                if (symbol == null) return;
                
                // 处理属性（IPropertySymbol）
                foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
                {
                    _currentSymbol = member;
                    ProcessedTypes.Add(symbol);
                
                    if (member.IsStatic) continue;
                
                    if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
                    
                    if (!IsAutoProperty(member)) continue;
                
                    if (TypeMetaChecker.TryCheckIncludeAttribute(member)) goto Set;
                
                    if (member.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected) continue; // 忽略静态属性
                
                
                    Set:
                    var field = new LuminDataField
                    {
                        Name = member.Name, // 直接使用属性名称
                        FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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
                
                    //Set Order
                    TypeMetaChecker.FindAndSetOrderAttribute(ref field, member);
                
                    TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, member);

                    if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                    {
                        field.Type = LuminFiledType.Other;
                    
                        dataInfo.fields.Add(field);

                        parent?.fields.Add(field);
                        
                        continue;
                    }
                
                    ProcessFieldType(member.Type, field, dataInfo.GenericParameters);
                
                
                    dataInfo.fields.Add(field);
                    
                    parent?.fields.Add(field);
                    
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
                        FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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
                        belongClassName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    };
                
                
                    //Set Order
                    TypeMetaChecker.FindAndSetOrderAttribute(ref field, member);
                
                    TypeMetaChecker.FindAndSetFixedLengthAttribute(ref field, member);
                
                    if (TypeMetaChecker.TryCheckPackableObjectAttribute(member))
                    {
                        field.Type = LuminFiledType.Other;
                    
                        dataInfo.fields.Add(field);

                        parent?.fields.Add(field);
                        
                        continue;
                    }
                
                    ProcessFieldType(member.Type, field, dataInfo.GenericParameters);
                    
                    dataInfo.fields.Add(field);
                    
                    parent?.fields.Add(field);
                    
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
                
                if (ParserMap.Parsers.Contains(metadataName))
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
                            else if (ParserMap.Parsers.Contains(metadataName))
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
            if (namedTypeArg.ContainingType != null)
            {
                CheckNestedClassAccessibility(namedTypeArg);
            }
            
            if (ProcessedTypes.Contains(namedTypeArg))
            {
                field.Type = LuminFiledType.Other;
                return;
            }

            // 添加到已处理集合
            ProcessedTypes.Add(namedTypeArg);
    
            try
            {
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
            
                    foreach (var nestedTypeArg in namedTypeArg.TypeArguments)
                    {
                        if (genericParameters is not null && genericParameters.Contains(nestedTypeArg.Name))
                        {
                            field.Type = LuminFiledType.Other;
                            return;
                        }
                    }
            
                    // 新增：递归处理基类成员（排除System.Object）
                    if (namedTypeArg.BaseType?.SpecialType != SpecialType.System_Object &&
                        namedTypeArg.BaseType?.SpecialType != SpecialType.System_ValueType)
                    {
                        ProcessBaseClassMembersForNestedClass(namedTypeArg.BaseType, field, genericParameters);
                    }
            
                    // 递归处理嵌套属性
                    foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                    {
                        if (member.IsStatic) continue;
                
                        if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                        if (!IsAutoProperty(member)) continue;
                        
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
                            FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                            IsPrivate = member.DeclaredAccessibility is 
                                Accessibility.Private or 
                                Accessibility.ProtectedAndInternal or 
                                Accessibility.Protected,
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
                            FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                            IsPrivate = nestedMember.DeclaredAccessibility is 
                                Accessibility.Private or 
                                Accessibility.ProtectedAndInternal or 
                                Accessibility.Protected,
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
            
                    // 新增：递归处理基类成员（排除System.Object）
                    if (namedTypeArg.BaseType?.SpecialType != SpecialType.System_Object && 
                        namedTypeArg.BaseType?.SpecialType != SpecialType.System_ValueType)
                    {
                        ProcessBaseClassMembersForNestedClass(namedTypeArg.BaseType, field, genericParameters);
                    }
            
                    // 递归处理嵌套属性
                    foreach (var member in namedTypeArg.GetMembers().OfType<IPropertySymbol>())
                    {
                
                        _currentSymbol = member;
            
                        if (member.IsStatic) continue;
            
                        if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;
            
                        if (!IsAutoProperty(member)) continue;
                        
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
                            FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                            IsPrivate = member.DeclaredAccessibility is 
                                Accessibility.Private or 
                                Accessibility.ProtectedAndInternal or 
                                Accessibility.Protected,
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
                            FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                            TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                            IsPrivate = nestedMember.DeclaredAccessibility is 
                                Accessibility.Private or 
                                Accessibility.ProtectedAndInternal or 
                                Accessibility.Protected,
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
                    SpecialType.System_Decimal => LuminGenericsType.Decimal,
                    SpecialType.System_Char => LuminGenericsType.Char
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
                if (member.IsImplicitlyDeclared || member.IsStatic) continue;

                string typeName = "";
                bool isValue = false;
                
                if (member is IPropertySymbol property)
                {
                    if (!IsAutoProperty(property)) continue;
                    
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

        /// <summary>
        /// 自动发现当前程序集内所有继承自指定基类的派生类，并分配确定性 Tag
        /// </summary>
        private static void DiscoverAndRegisterDerivedTypes(
            INamedTypeSymbol baseTypeSymbol,
            LuminDataInfo dataInfo,
            Compilation compilation) => LuminPackDiscovery.Run(baseTypeSymbol, dataInfo, compilation);
        
        private static void ProcessGenericClassOrStruct(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // 检查是否已处理过该类型
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

                // 修复：只有当类型确实是不可序列化时才标记为 Other
                if (namedType.IsRecord || namedType.IsAbstract)
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                SetClassLocalField(namedType, field.localFields);

                // 修复：只有当泛型参数确实在当前类的泛型参数列表中时才标记为 Other
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

                // 新增：处理基类成员
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

        private static void ProcessNonGenericClassOrStruct(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // 检查是否已处理过该类型
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

                // 修复：只有当类型确实是不可序列化时才标记为 Other
                if (namedType.IsRecord || namedType.IsAbstract)
                {
                    field.Type = LuminFiledType.Other;
                    TypeMetaChecker.CheckFieldIsLuminPackable(namedType, _currentSymbol.Locations.FirstOrDefault());
                    return;
                }

                SetClassLocalField(namedType, field.localFields);
        
                // 新增：处理基类成员
                if (namedType.BaseType?.SpecialType != SpecialType.System_Object&&
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
        /// 处理嵌套类的基类成员
        /// </summary>
        private static void ProcessBaseClassMembersForNestedClass(INamedTypeSymbol baseClassSymbol, LuminDataField field, List<string> genericParameters = null)
        {
            if (baseClassSymbol == null || 
                baseClassSymbol.SpecialType == SpecialType.System_Object ||
                baseClassSymbol.SpecialType == SpecialType.System_ValueType) return;

            // 先处理更上层的基类
            ProcessBaseClassMembersForNestedClass(baseClassSymbol.BaseType, field, genericParameters);

            // 处理当前基类的成员
            ProcessNestedClassBaseMembers(baseClassSymbol, field, genericParameters);
        }
        
        /// <summary>
        /// 处理嵌套类基类的具体成员
        /// </summary>
        private static void ProcessNestedClassBaseMembers(INamedTypeSymbol classSymbol, LuminDataField field, List<string> genericParameters = null)
        {
            // 处理基类属性
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                _currentSymbol = member;

                if (member.IsStatic) continue;

                if (TypeMetaChecker.TryCheckIgnoreAttribute(member)) continue;

                if (!IsAutoProperty(member)) continue;
                
                if (TypeMetaChecker.TryCheckIncludeAttribute(member))
                {
                    if (_metadata is not null && !_metadata.IsNet8)
                    {
                        TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                            DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                            _location,
                            _mainSymbol.Name, classSymbol.Name
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
                    // 只读结构体标记为不可序列化，但不影响整个类
                    continue;
                }

                var nestedField = new LuminDataField
                {
                    Name = member.Name,
                    FullTypeName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NameSpace = member.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = member.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : member.Type.Name,
                    IsPrivate = member.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected,
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

            // 处理基类字段
            foreach (var nestedMember in classSymbol.GetMembers().OfType<IFieldSymbol>())
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
                            _mainSymbol.Name, classSymbol.Name
                        ));
                        continue;
                    }
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
                    // 只读结构体标记为不可序列化，但不影响整个类
                    continue;
                }

                var nestedField = new LuminDataField
                {
                    Name = nestedMember.Name,
                    FullTypeName = nestedMember.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    NameSpace = nestedMember.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                    TypeName = nestedMember.Type is INamedTypeSymbol nestedNamedType ? nestedNamedType.ToDisplayString() : nestedMember.Type.Name,
                    IsPrivate = nestedMember.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected,
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
        
        private static void ProcessNestedMembers(INamedTypeSymbol namedType, LuminDataField field, List<string> genericParameters = null)
        {
            // 处理嵌套属性
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
        
                if (shouldInclude && _metadata is not null && !_metadata.IsNet8)
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                        _location,
                        _mainSymbol.Name, namedType.Name
                    ));
                    continue;
                }
        
                // 修复：只对真正的只读结构体标记为 Other
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
                    IsPrivate = member.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected,
                    isProperty = true,
                    belongClassName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };
        
                // 修复：只有当明确标记 PackableObject 或确实是泛型参数时才使用 Other
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
    
            // 处理嵌套字段（逻辑与属性类似）
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
        
                if (shouldInclude && _metadata is not null && !_metadata.IsNet8)
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.NetStandardClassOrStructMemberFieldCantInclude,
                        _location,
                        _mainSymbol.Name, namedType.Name
                    ));
                    continue;
                }
        
                // 修复：只对真正的只读结构体标记为 Other
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
                    IsPrivate = nestedMember.DeclaredAccessibility is 
                        Accessibility.Private or 
                        Accessibility.ProtectedAndInternal or 
                        Accessibility.Protected,
                    belongClassName = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                };
        
                // 修复：只有当明确标记 PackableObject 或确实是泛型参数时才使用 Other
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
        
        /// <summary>
        /// 通用的构造函数分析方法，适用于主类和嵌套类
        /// </summary>
        private static LuminConstructorData AnalyzeTypeConstructors(
            INamedTypeSymbol typeSymbol, 
            List<LuminDataField> availableFields,
            Location location,
            bool isNestedClass = false)
        {
            // 获取所有显式定义的公共非静态构造函数
            var explicitConstructors = typeSymbol.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && 
                            !c.IsStatic && 
                            !c.IsImplicitlyDeclared)
                .ToList();

            // 如果没有显式定义的构造函数
            if (explicitConstructors.Count == 0)
            {
                // 对于结构体，总是有隐式无参构造函数
                if (typeSymbol.TypeKind == TypeKind.Struct)
                {
                    return new LuminConstructorData
                    {
                        Accessibility = Accessibility.Public,
                        IsMarkedWithAttribute = false,
                        Parameters = new List<ConstructorParameter>()
                    };
                }
        
                // 对于类，如果没有显式构造函数，使用隐式无参构造函数
                if (typeSymbol.TypeKind == TypeKind.Class)
                {
                    return new LuminConstructorData
                    {
                        Accessibility = Accessibility.Public,
                        IsMarkedWithAttribute = false,
                        Parameters = new List<ConstructorParameter>()
                    };
                }
        
                return null;
            }

            // 收集所有可用字段名称（不区分大小写）
            var allFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in availableFields)
            {
                allFieldNames.Add(field.Name);
            }

            var allConstructorData = new List<LuminConstructorData>();

            // 处理每个显式构造函数
            foreach (var constructor in explicitConstructors)
            {
                var constructorData = new LuminConstructorData
                {
                    Accessibility = constructor.DeclaredAccessibility,
                    IsMarkedWithAttribute = constructor.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute")
                };

                foreach (var parameter in constructor.Parameters)
                {
                    var paramData = new ConstructorParameter
                    {
                        Name = parameter.Name,
                        Type = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        TypeNamespace = parameter.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace"
                    };

                    // 检查参数名是否匹配字段名
                    var matchingField = availableFields.FirstOrDefault(f => 
                        string.Equals(f.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
            
                    if (matchingField != null)
                    {
                        paramData.MatchesField = true;
                        paramData.MatchingFieldName = matchingField.Name;
                    }
                    else
                    {
                        paramData.MatchesField = false;
                    }

                    constructorData.Parameters.Add(paramData);
                }

                allConstructorData.Add(constructorData);
            }

            // 选择构造函数逻辑
            var markedConstructors = allConstructorData.Where(c => c.IsMarkedWithAttribute).ToList();
    
            LuminConstructorData selectedConstructor = null;

            if (markedConstructors.Count > 1)
            {
                // 多个标记的构造函数，报错
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleConstructorsRequireAttribute,
                    location,
                    typeSymbol.Name
                ));
                return null;
            }
            else if (markedConstructors.Count == 1)
            {
                // 使用标记的构造函数
                selectedConstructor = markedConstructors[0];
            }
            else
            {
                // 没有标记的构造函数，根据数量决定
                if (allConstructorData.Count == 1)
                {
                    // 只有一个显式构造函数，自动选择它
                    selectedConstructor = allConstructorData[0];
                }
                else if (allConstructorData.Count > 1)
                {
                    // 多个显式构造函数但没有标记，报错
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.MultipleConstructorsRequireAttribute,
                        location,
                        typeSymbol.Name
                    ));
                    return null;
                }
                // 如果 allConstructorData.Count == 0，使用隐式无参构造函数（已在前面处理）
            }

            // 验证选中的构造函数参数是否都匹配字段（只有有参数时才验证）
            if (selectedConstructor != null && selectedConstructor.Parameters.Count > 0)
            {
                ValidateConstructorParameters(selectedConstructor, typeSymbol, allFieldNames, location, isNestedClass);
            }

            return selectedConstructor;
        }

        /// <summary>
        /// 验证构造函数参数是否匹配字段
        /// </summary>
        private static void ValidateConstructorParameters(
            LuminConstructorData constructor, 
            INamedTypeSymbol typeSymbol, 
            HashSet<string> fieldNames,
            Location location,
            bool isNestedClass = false)
        {
            // 无参构造函数不需要验证
            if (constructor.Parameters.Count == 0)
                return;

            foreach (var parameter in constructor.Parameters)
            {
                if (!fieldNames.Contains(parameter.Name))
                {
                    TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConstructorParameterNameMismatch,
                        location,
                        parameter.Name, typeSymbol.Name
                    ));
                }
            }
        }
        
        /// <summary>
        /// 分析主类的构造函数
        /// </summary>
        private static void AnalyzeMainClassConstructors(INamedTypeSymbol typeSymbol, LuminDataInfo dataInfo)
        {
            dataInfo.SelectedConstructor = AnalyzeTypeConstructors(
                typeSymbol, 
                dataInfo.fields,
                typeSymbol.Locations.FirstOrDefault(),
                isNestedClass: false);
    
            // 收集所有构造函数信息（用于诊断和调试）
            dataInfo.AllConstructors = typeSymbol.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
                .Select(constructor => new LuminConstructorData
                {
                    Accessibility = constructor.DeclaredAccessibility,
                    IsMarkedWithAttribute = constructor.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackConstructorAttribute"),
                    Parameters = constructor.Parameters.Select(parameter => new ConstructorParameter
                    {
                        Name = parameter.Name,
                        Type = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        TypeNamespace = parameter.Type.ContainingNamespace?.ToString() ?? "Your.Data.Namespace",
                        MatchesField = dataInfo.fields.Any(f => 
                            string.Equals(f.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    }).ToList()
                }).ToList();
        }
        
        /// <summary>
        /// 分析嵌套类的构造函数
        /// </summary>
        private static void AnalyzeNestedClassConstructors(INamedTypeSymbol typeSymbol, LuminDataField field)
        {
            try
            {
                field.SelectedConstructor = AnalyzeTypeConstructors(
                    typeSymbol, 
                    field.ClassFields,
                    _currentSymbol?.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                    isNestedClass: true);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Nested class"))
            {
                // 嵌套类构造函数参数不匹配，标记为不可序列化
                field.Type = LuminFiledType.Other;
                TypeMetaChecker.CheckFieldIsLuminPackable(typeSymbol, _currentSymbol?.Locations.FirstOrDefault());
            }
        }
        
        /// <summary>
        /// 精确检测属性是否会生成后台字段（支持泛型属性）
        /// </summary>
        public static bool IsAutoProperty(IPropertySymbol property)
        {
            if (property == null)
                return false;

            // 1. 基本检查：必须是实例属性，不能是抽象、外部或静态的
            if (property.IsStatic || property.IsAbstract || property.IsExtern)
                return false;

            var standardBackingFieldName = $"<{property.Name}>k__BackingField";
    
            // 查找标准命名的后台字段
            return property.ContainingType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => 
                    f.Name == standardBackingFieldName);
        }
        
        /// <summary>
        /// 检查嵌套类的可访问性
        /// </summary>
        private static void CheckNestedClassAccessibility(INamedTypeSymbol nestedType)
        {
            if (nestedType.DeclaredAccessibility != Accessibility.Public && 
                nestedType.DeclaredAccessibility != Accessibility.Internal)
            {
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.NestedClassMustBePublicOrInternal,
                    _location,
                    nestedType.Name
                ));
                
                TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.NestedClassAccessibilityError,
                    _location,
                    nestedType.Name,
                    nestedType.DeclaredAccessibility.ToString()
                ));
                
            }
        }
    }
}