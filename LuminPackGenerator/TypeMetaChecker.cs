using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

public static class TypeMetaChecker
{
    
    // Attributes
    private const string LUMIN_PACK_ABLE = "LuminPackableAttribute";
    private const string LUMIN_PACK_IGNORE = "LuminPackIgnoreAttribute";
    private const string LUMIN_PACK_INCLUDE = "LuminPackIncludeAttribute";
    private const string LUMIN_PACK_ORDER = "LuminPackOrderAttribute";
    private const string LUMIN_PACK_FIX_LENGTH = "LuminPackFixedLengthAttribute";
    private const string LUMIN_PACK_OBJECT = "LuminPackableObjectAttribute";
    private const string LUMIN_PACK_UNION = "LuminPackUnionAttribute";
    private const string LUMIN_PACK_ONSERIALIZING = "LuminPackOnSerializingAttribute";
    private const string LUMIN_PACK_ONSERIALIZED = "LuminPackOnSerializedAttribute";
    private const string LUMIN_PACK_ONDESERIALIZING = "LuminPackOnDeserializingAttribute";
    private const string LUMIN_PACK_ONDESERIALIZED = "LuminPackOnDeserializedAttribute";
    
    
    public static readonly List<Diagnostic> _reportContext = new List<Diagnostic>(10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReportContext(ref SourceProductionContext context)
    {
        if (_reportContext.Count > 0)
        {
            foreach (var report in _reportContext)
            {
                context.ReportDiagnostic(report);
            }
            _reportContext.Clear();
            return true;
        }
        
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratorType CheckGeneratorType(INamedTypeSymbol typeSymbol)
    {
        var attributeData = typeSymbol.GetAttributes();
        var packableAttribute = attributeData.FirstOrDefault(x => 
            x.AttributeClass?.Name == LUMIN_PACK_ABLE || 
            x.AttributeClass?.Name == "LuminPackableAttribute");
        
        if (packableAttribute != null)
        {
            // 检查位置参数
            if (packableAttribute.ConstructorArguments.Length > 0)
            {
                var arg = packableAttribute.ConstructorArguments[0];
                // 处理枚举值
                if (arg.Value is byte byteValue)
                {
                    return (GeneratorType)byteValue;
                }
                // 处理直接传递的枚举类型
                else if (arg.Value is GeneratorType generatorType)
                {
                    return generatorType;
                }
            }
            
            // 检查命名参数（如 [LuminPackable(generatorType: GeneratorType.CircleReference)]）
            foreach (var namedArg in packableAttribute.NamedArguments)
            {
                if (namedArg.Key == "generatorType")
                {
                    if (namedArg.Value.Value is byte byteVal)
                    {
                        return (GeneratorType)byteVal;
                    }
                    else if (namedArg.Value.Value is GeneratorType genType)
                    {
                        return genType;
                    }
                }
            }
        }
        
        // 默认返回 GeneratorType.Object
        return GeneratorType.Object;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIgnoreAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_IGNORE);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIgnoreAttribute(IFieldSymbol field)
    {
        var attributeData = field.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_IGNORE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIncludeAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_INCLUDE);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIncludeAttribute(IFieldSymbol field)
    {
        var attributeData = field.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_INCLUDE);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(ISymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetOrderAttribute(ref LuminDataField data, IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        var orderAttribute = attributeData.FirstOrDefault(x => x.AttributeClass?.Name == LUMIN_PACK_ORDER);
        if (orderAttribute != null)
        {
            if (orderAttribute.ConstructorArguments.Length > 0)
            {
                var orderValue = orderAttribute.ConstructorArguments[0].Value;
                if (orderValue is uint value)
                {
                    data.Order = value;
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetOrderAttribute(ref LuminDataField data, IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        var orderAttribute = attributeData.FirstOrDefault(x => x.AttributeClass?.Name == LUMIN_PACK_ORDER);
        if (orderAttribute != null)
        {
            if (orderAttribute.ConstructorArguments.Length > 0)
            {
                var orderValue = orderAttribute.ConstructorArguments[0].Value;
                if (orderValue is uint value)
                {
                    data.Order = value;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetOrderValue(ISymbol member, out uint orderValue)
    {
        orderValue = 0;
        var attributes = member.GetAttributes();
        var orderAttribute = attributes.FirstOrDefault(a => 
            a.AttributeClass?.Name == LUMIN_PACK_ORDER);
        
        if (orderAttribute == null)
            return false;
        
        if (orderAttribute.ConstructorArguments.Length > 0)
        {
            var arg = orderAttribute.ConstructorArguments[0];
            if (arg.Value is uint value)
            {
                orderValue = value;
                return true;
            }
            else if (arg.Value is int intValue && intValue >= 0)
            {
                orderValue = (uint)intValue;
                return true;
            }
        }
        
        return false;
    }
    
     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateOrderAttributesForType(
        INamedTypeSymbol typeSymbol, 
        IEnumerable<ISymbol> members, 
        Location location)
    {
        var membersWithoutOrder = new List<ISymbol>();
        var orderValueMap = new Dictionary<uint, List<ISymbol>>();
        
        foreach (var member in members)
        {
            // 跳过静态成员、常量和事件
            if (member.IsStatic || member is IEventSymbol)
                continue;
                
            // 如果是属性，检查是否需要忽略
            if (member is IPropertySymbol property)
            {
                if (TryCheckIgnoreAttribute(property))
                    continue;
            }
            // 如果是字段，检查是否需要忽略
            else if (member is IFieldSymbol field)
            {
                // 跳过常量字段
                if (field.IsConst)
                    continue;
                    
                if (TryCheckIgnoreAttribute(field))
                    continue;
            }
            else
            {
                // 如果不是属性或字段，跳过
                continue;
            }
            
            // 检查是否有Order属性
            if (!TryGetOrderValue(member, out uint orderValue))
            {
                membersWithoutOrder.Add(member);
                continue;
            }
            
            // 收集Order值
            if (!orderValueMap.ContainsKey(orderValue))
            {
                orderValueMap[orderValue] = new List<ISymbol>();
            }
            orderValueMap[orderValue].Add(member);
        }
        
        // 报告缺少Order属性的成员
        foreach (var member in membersWithoutOrder)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.CircularReferenceAndVersionTolerantRequiredOrder,
                location,
                member.Name
            ));
        }
        
        // 报告重复的Order值
        foreach (var kvp in orderValueMap)
        {
            if (kvp.Value.Count > 1)
            {
                var memberNames = string.Join(", ", kvp.Value.Select(m => m.Name));
                _reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CircularReferenceAndVersionTolerantDuplicateOrder,
                    location,
                    kvp.Key, memberNames
                ));
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(INamedTypeSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableAttribute(INamedTypeSymbol field)
    {
        var attributeData = field.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ABLE);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableAttribute(ITypeSymbol field)
    {
        var attributeData = field.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ABLE);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFixedLengthAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_FIX_LENGTH);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFixedLengthAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_FIX_LENGTH);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckUnionAttribute(INamedTypeSymbol symbol)
    {
        var attributeData = symbol.GetAttributes();
        
        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_UNION);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetFixedLengthAttribute(ref LuminDataField data, IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();
        
        var fixedLengthAttribute = 
            attributeData.FirstOrDefault(x => x.AttributeClass?.Name == LUMIN_PACK_FIX_LENGTH);
                
        if (fixedLengthAttribute != null)
        {
            if (fixedLengthAttribute.ConstructorArguments.Length > 0)
            {
                var lengthValue = fixedLengthAttribute.ConstructorArguments[0].Value;
                if (lengthValue is uint value)
                {
                    data.FixLength = value;
                }
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetFixedLengthAttribute(ref LuminDataField data, IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();
        
        var fixedLengthAttribute = 
            attributeData.FirstOrDefault(x => x.AttributeClass?.Name == LUMIN_PACK_FIX_LENGTH);
        
        if (fixedLengthAttribute != null)
        {
            if (fixedLengthAttribute.ConstructorArguments.Length > 0)
            {
                var lengthValue = fixedLengthAttribute.ConstructorArguments[0].Value;
                if (lengthValue is uint value)
                {
                    data.FixLength = value;
                }
            }
        }
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckConstructorHasPublic(INamedTypeSymbol symbol)
    {
        return symbol.Constructors.Any(x => x.DeclaredAccessibility is Accessibility.Public);
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFieldStructIsReadOnly(IPropertySymbol symbol)
    {
        if (symbol.IsReadOnly)
        {
            if (TryCheckPackableObjectAttribute(symbol)) return false;
            
            return true;
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFieldStructIsReadOnly(IFieldSymbol symbol)
    {
        if (symbol.IsReadOnly && symbol.Type.TypeKind == TypeKind.Struct)
        {
            if (TryCheckPackableObjectAttribute(symbol)) return false;
            
            return true;
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFieldStructIsReadOnly(INamedTypeSymbol symbol)
    {
        if (symbol.IsReadOnly && symbol.TypeKind == TypeKind.Struct)
        {
            if (TryCheckPackableObjectAttribute(symbol)) return false;
            
            return true;
        }
        
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckFieldIsLuminPackable(INamedTypeSymbol symbol, Location location)
    {
        if (!TryCheckPackableAttribute(symbol))
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.FieldMustBeLuminPackable,
                location,
                symbol.Name
            ));
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIsNotRefLike(ITypeSymbol symbol)
    {
        return symbol.IsRefLikeType;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckStructLayout(INamedTypeSymbol symbol, LuminDataInfo dataInfo)
    {
        const string fullAttributeName = "System.Runtime.InteropServices.StructLayoutAttribute";

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != fullAttributeName)
                continue;

            // 处理构造函数参数
            if (attr.ConstructorArguments.Length > 0)
            {
                var arg = attr.ConstructorArguments[0];
                if (arg.Value is int intValue)
                {
                    switch (intValue)
                    {
                        case (int)LayoutKind.Explicit:
                            dataInfo.structLayout = StructLayout.Explicit;
                            return true;
                        case (int)LayoutKind.Sequential:
                            dataInfo.structLayout = StructLayout.Sequential;
                            return true;
                        case (int)LayoutKind.Auto:
                            dataInfo.structLayout = StructLayout.Auto;
                            return true;
                    }
                }
            }

            // 处理命名参数 "Value"
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Value" && namedArg.Value.Value is int namedValue)
                {
                    switch (namedValue)
                    {
                        case (int)LayoutKind.Explicit:
                            dataInfo.structLayout = StructLayout.Explicit;
                            return true;
                        case (int)LayoutKind.Sequential:
                            dataInfo.structLayout = StructLayout.Sequential;
                            return true;
                        case (int)LayoutKind.Auto:
                            dataInfo.structLayout = StructLayout.Auto;
                            return true;
                    }
                }
            }
        }

        // 默认值
        dataInfo.structLayout = StructLayout.Default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckAndGetFiledOffsetAttribute(ISymbol symbol, out int offset)
    {
        offset = -1;
    
        const string attributeName = "System.Runtime.InteropServices.FieldOffsetAttribute";
    
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != attributeName)
                continue;
        
            // 检查构造函数参数
            if (attribute.ConstructorArguments.Length > 0)
            {
                var arg = attribute.ConstructorArguments[0];
                if (arg.Type?.SpecialType == SpecialType.System_Int32)
                {
                    offset = (int)arg.Value;
                    return true;
                }
            }
        }
    
        return false;

    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckSerializeCallBack(INamedTypeSymbol symbol, LuminDataInfo dataInfo)
    {
        foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes())
            {
                switch (attr.AttributeClass?.Name)
                {
                    case LUMIN_PACK_ONSERIALIZING :
                        if (TryCheckCallBackMethod(symbol, method)) 
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnSerializing, method.IsStatic)); break;
                    case LUMIN_PACK_ONSERIALIZED : 
                        if (TryCheckCallBackMethod(symbol, method)) 
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnSerialized, method.IsStatic)); break;
                    case LUMIN_PACK_ONDESERIALIZING : 
                        if (TryCheckCallBackMethod(symbol, method)) 
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnDeserializing, method.IsStatic)); break;
                    case LUMIN_PACK_ONDESERIALIZED : 
                        if (TryCheckCallBackMethod(symbol, method)) 
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnDeserialized, method.IsStatic)); break;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryCheckCallBackMethod(INamedTypeSymbol symbol, IMethodSymbol method)
    {
        if (method.DeclaredAccessibility
            is Accessibility.NotApplicable
            or Accessibility.Private
            or Accessibility.Protected
            or Accessibility.ProtectedAndInternal)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OnMethodIsPrivate, 
                symbol.Locations.FirstOrDefault(), 
                symbol.Name, 
                method.Name));
            
            return false;
        }

        if (method.Parameters.Length > 0)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OnMethodHasParameter, 
                symbol.Locations.FirstOrDefault(), 
                symbol.Name, 
                method.Name));
            
            return false;
        }
        
        return true;
    }
}