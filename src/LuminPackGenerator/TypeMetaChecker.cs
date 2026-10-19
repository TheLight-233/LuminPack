using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Validates serialization annotations and collects diagnostics for the current analysis scope.</summary>
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
    private const string LUMIN_PACK_WIDE_TAG = "LuminPackWideTagAttribute";
    private const string LUMIN_PACK_Compress = "LuminPackCompressAttribute";
    private const string LUMIN_PACK_ONSERIALIZING = "LuminPackOnSerializingAttribute";
    private const string LUMIN_PACK_ONSERIALIZED = "LuminPackOnSerializedAttribute";
    private const string LUMIN_PACK_ONDESERIALIZING = "LuminPackOnDeserializingAttribute";
    private const string LUMIN_PACK_ONDESERIALIZED = "LuminPackOnDeserializedAttribute";


    [ThreadStatic]
    private static List<Diagnostic> _diagnosticContext;

    [ThreadStatic]
    private static List<Diagnostic> _cachedDiagnosticContext;

    [ThreadStatic]
    private static HashSet<ISymbol> _reportedOverrideAnnotations;

    // All checker methods execute synchronously inside one syntax transform. A
    // scoped thread-local bag keeps the hot Add path allocation-free after setup,
    // while the completed bag is copied onto that transform's LuminDataInfo.
    /// <summary>Gets the report context.</summary>
    internal static List<Diagnostic> _reportContext =>
        _diagnosticContext ?? LuminPackExceptionHelper.ThrowInvalidOperationException<List<Diagnostic>>("No active generator diagnostic scope.");

    /// <summary>Gets the current diagnostics.</summary>
    internal static List<Diagnostic> CurrentDiagnostics => _reportContext;

    /// <summary>Starts a thread-local diagnostic collection scope and returns the previous scope.</summary>
    /// <returns>The result of the operation.</returns>
    internal static List<Diagnostic> PushDiagnosticScope()
    {
        var previous = _diagnosticContext;
        var current = _cachedDiagnosticContext;
        if (current is null)
        {
            current = new List<Diagnostic>(10);
        }
        else
        {
            _cachedDiagnosticContext = null;
            current.Clear();
        }

        _diagnosticContext = current;
        _reportedOverrideAnnotations?.Clear();
        return previous;
    }

    /// <summary>Restores the previous diagnostic scope and recycles the completed collection.</summary>
    /// <param name="previous">The previous used by this operation.</param>
    internal static void PopDiagnosticScope(List<Diagnostic> previous)
    {
        var completed = _diagnosticContext;
        _diagnosticContext = previous;
        if (previous is null && completed is not null)
        {
            completed.Clear();
            _cachedDiagnosticContext = completed;
        }
    }

    /// <summary>Checks generator type against the operation's requirements.</summary>
    /// <param name="typeSymbol">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratorType CheckGeneratorType(INamedTypeSymbol typeSymbol)
    {
        var attributeData = typeSymbol.GetAttributes();
        var packableAttribute = attributeData.FirstOrDefault(x =>
            x.AttributeClass?.Name == LUMIN_PACK_ABLE ||
            x.AttributeClass?.Name == "LuminPackableAttribute");

        if (packableAttribute != null)
        {
            // Read positional arguments.
            if (packableAttribute.ConstructorArguments.Length > 0)
            {
                var arg = packableAttribute.ConstructorArguments[0];
                // Process enum values.
                if (arg.Value is byte byteValue)
                {
                    return (GeneratorType)byteValue;
                }
                // Process the directly supplied enum type.
                else if (arg.Value is GeneratorType generatorType)
                {
                    return generatorType;
                }
            }

            // Read named arguments, such as generatorType: GeneratorType.CircleReference.
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

        // Default to GeneratorType.Object.
        return GeneratorType.Object;
    }

    /// <summary>Attempts to check ignore attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIgnoreAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        if (TryReportOverrideAnnotation(property, attributeData))
            return true;

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_IGNORE);
    }

    /// <summary>Attempts to check ignore attribute and reports whether it succeeded.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIgnoreAttribute(IFieldSymbol field)
    {
        var attributeData = field.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_IGNORE);
    }

    /// <summary>Attempts to check include attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIncludeAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        if (TryReportOverrideAnnotation(property, attributeData))
            return false;

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_INCLUDE);
    }

    /// <summary>Attempts to report override annotation and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <param name="attributes">The attributes used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryReportOverrideAnnotation(
        IPropertySymbol property,
        System.Collections.Immutable.ImmutableArray<AttributeData> attributes)
    {
        if (property.OverriddenProperty is null)
            return false;

        var attribute = attributes.FirstOrDefault(static attr =>
            attr.AttributeClass?.Name is LUMIN_PACK_IGNORE or LUMIN_PACK_INCLUDE);
        if (attribute is null)
            return false;

        _reportedOverrideAnnotations ??= new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (_reportedOverrideAnnotations.Add(property))
        {
            var attributeName = attribute.AttributeClass!.Name;
            if (attributeName.EndsWith("Attribute", StringComparison.Ordinal))
                attributeName = attributeName.Substring(0, attributeName.Length - 9);

            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OverrideMemberCantAddAnnotation,
                property.Locations.FirstOrDefault(),
                property.ContainingType.Name,
                property.Name,
                attributeName));
        }

        return true;
    }

    /// <summary>Attempts to check include attribute and reports whether it succeeded.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIncludeAttribute(IFieldSymbol field)
    {
        var attributeData = field.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_INCLUDE);
    }

    /// <summary>Attempts to check order attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }

    /// <summary>Attempts to check order attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }

    /// <summary>Attempts to check order attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckOrderAttribute(ISymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ORDER);
    }

    /// <summary>Attempts to check compress attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckCompressAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_Compress);
    }

    /// <summary>Attempts to check compress attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckCompressAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_Compress);
    }

    /// <summary>Attempts to check compress attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckCompressAttribute(ISymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_Compress);
    }

    /// <summary>Finds the and set order attribute in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="property">The property used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetOrderAttribute(ref LuminDataField data, IPropertySymbol property)
    {
        data.OldNames = GetOldNames(property);
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

    /// <summary>Finds the and set order attribute in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="property">The property used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FindAndSetOrderAttribute(ref LuminDataField data, IFieldSymbol property)
    {
        data.OldNames = GetOldNames(property);
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

    /// <summary>Reads former binary identities without retaining attribute symbols.</summary>
    /// <param name="member">The declared field or property.</param>
    /// <returns>The former names in attribute declaration order.</returns>
    private static string[] GetOldNames(ISymbol member)
        => member.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackOldNameAttribute")
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string).ToArray();

    /// <summary>Rejects empty or ambiguous identities and aliases outside the named binary mode.</summary>
    /// <param name="data">The complete filtered member contract, including inherited fields.</param>
    /// <param name="location">The model diagnostic location.</param>
    internal static void ValidateVersionNames(LuminDataInfo data, Location location)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var field in data.fields)
        {
            if (data.generatorType != GeneratorType.VersionTolerant)
            {
                if (field.OldNames.Length != 0)
                    _reportContext.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidVersionMemberName, location,
                        data.className, "[LuminPackOldName] requires VersionTolerant mode."));
                continue;
            }
            foreach (string name in new[] { field.Name }.Concat(field.OldNames))
            {
                string error = null;
                if (string.IsNullOrEmpty(name)) error = "Serialized member names must not be null or empty.";
                else if (!names.Add(name)) error = "Serialized member name '" + name + "' is ambiguous.";
                else
                {
                    try { new System.Text.UTF8Encoding(false, true).GetByteCount(name); }
                    catch (System.Text.EncoderFallbackException) { error = "Serialized member names must contain valid Unicode."; }
                }
                if (error != null) _reportContext.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidVersionMemberName, location, data.className, error));
            }
        }
    }

    /// <summary>Attempts to get order value and reports whether it succeeded.</summary>
    /// <param name="member">The member used by this operation.</param>
    /// <param name="orderValue">Receives the order value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Validates order attributes for type before the operation continues.</summary>
    /// <param name="typeSymbol">The type or symbol to analyze.</param>
    /// <param name="members">The members used by this operation.</param>
    /// <param name="location">The location used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateOrderAttributesForType(
        INamedTypeSymbol typeSymbol,
        IEnumerable<ISymbol> members,
        Location location)
    {
        var membersWithoutOrder = new List<ISymbol>();
        var orderValueMap = new Dictionary<uint, List<ISymbol>>();

        // First collect the backing field names of auto-properties.
        var autoPropertyBackingFields = new HashSet<string>();
        foreach (var property in members.OfType<IPropertySymbol>())
        {
            // Detect auto-properties through their backing fields.
            var backingFieldName = $"<{property.Name}>k__BackingField";
            var backingField = typeSymbol.GetMembers(backingFieldName).OfType<IFieldSymbol>().FirstOrDefault();
            if (backingField != null)
            {
                autoPropertyBackingFields.Add(backingFieldName);
            }
        }

        foreach (var member in members)
        {
            // Skip static members, constants, and events.
            if (member.IsStatic || member is IEventSymbol)
                continue;

            // Check whether the property should be ignored.
            if (member is IPropertySymbol property)
            {
                if (TryCheckIgnoreAttribute(property))
                    continue;
            }
            // Check whether the field should be ignored.
            else if (member is IFieldSymbol field)
            {
                // Skip constant fields.
                if (field.IsConst)
                    continue;

                // Skip auto-property backing fields.
                if (field.IsImplicitlyDeclared || autoPropertyBackingFields.Contains(field.Name))
                    continue;

                if (TryCheckIgnoreAttribute(field))
                    continue;
            }
            else
            {
                // Skip symbols that are neither fields nor properties.
                continue;
            }

            // Check for an order attribute.
            if (!TryGetOrderValue(member, out uint orderValue))
            {
                membersWithoutOrder.Add(member);
                continue;
            }

            // Collect member order values.
            if (!orderValueMap.ContainsKey(orderValue))
            {
                orderValueMap[orderValue] = new List<ISymbol>();
            }
            orderValueMap[orderValue].Add(member);
        }

        // Report members missing an order attribute.
        foreach (var member in membersWithoutOrder)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.CircularReferenceAndVersionTolerantRequiredOrder,
                member.Locations.FirstOrDefault() ?? location,
                member.Name
            ));
        }

        // Report duplicate member order values.
        foreach (var kvp in orderValueMap)
        {
            if (kvp.Value.Count > 1)
            {
                var memberNames = string.Join(", ", kvp.Value.Select(m => m.Name));
                _reportContext.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CircularReferenceAndVersionTolerantDuplicateOrder,
                    kvp.Value[0].Locations.FirstOrDefault() ?? location,
                    kvp.Key, memberNames
                ));
            }
        }
    }

    /// <summary>Attempts to check packable object attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }

    /// <summary>Attempts to check packable object attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }

    /// <summary>Attempts to check packable object attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableObjectAttribute(INamedTypeSymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_OBJECT);
    }

    /// <summary>Attempts to check packable attribute and reports whether it succeeded.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableAttribute(INamedTypeSymbol field)
    {
        var attributeData = field.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ABLE);
    }

    /// <summary>Attempts to check packable attribute and reports whether it succeeded.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckPackableAttribute(ITypeSymbol field)
    {
        var attributeData = field.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_ABLE);
    }

    /// <summary>Attempts to check fixed length attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFixedLengthAttribute(IPropertySymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_FIX_LENGTH);
    }

    /// <summary>Attempts to check fixed length attribute and reports whether it succeeded.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckFixedLengthAttribute(IFieldSymbol property)
    {
        var attributeData = property.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_FIX_LENGTH);
    }

    /// <summary>Attempts to check union attribute and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckUnionAttribute(INamedTypeSymbol symbol)
    {
        var attributeData = symbol.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_UNION);
    }

    /// <summary>Attempts to check wide tag attribute and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckWideTagAttribute(INamedTypeSymbol symbol)
    {
        var attributeData = symbol.GetAttributes();

        return attributeData.Any(x => x.AttributeClass!.Name is LUMIN_PACK_WIDE_TAG);
    }

    /// <summary>Finds the and set fixed length attribute in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="property">The property used by this operation.</param>
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

    /// <summary>Finds the and set fixed length attribute in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="property">The property used by this operation.</param>
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


    /// <summary>Attempts to check constructor has public and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckConstructorHasPublic(INamedTypeSymbol symbol)
    {
        return symbol.Constructors.Any(x => x.DeclaredAccessibility is Accessibility.Public);

    }

    /// <summary>Attempts to check field struct is read only and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Attempts to check field struct is read only and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Attempts to check field struct is read only and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Checks field is lumin packable against the operation's requirements.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <param name="location">The location used by this operation.</param>
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

    /// <summary>Attempts to check is not ref like and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckIsNotRefLike(ITypeSymbol symbol)
    {
        return symbol.IsRefLikeType;
    }

    /// <summary>Attempts to check struct layout and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <param name="dataInfo">The metadata describing the serializable type.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckStructLayout(INamedTypeSymbol symbol, LuminDataInfo dataInfo)
    {
        const string fullAttributeName = "System.Runtime.InteropServices.StructLayoutAttribute";

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != fullAttributeName)
                continue;

            // Process constructor arguments.
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

            // Read the named argument "Value".
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

        // Default value.
        dataInfo.structLayout = StructLayout.Default;
        return false;
    }

    /// <summary>
    /// Layout kind declared on <paramref name="symbol"/>, or <see cref="StructLayout.Default"/> when the
    /// type carries no <c>[StructLayout]</c>. Used to mirror the layout of nested member types onto
    /// their Local layout class.
    /// </summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StructLayout GetStructLayout(INamedTypeSymbol symbol)
    {
        var probe = new LuminDataInfo();
        return TryCheckStructLayout(symbol, probe) ? probe.structLayout : StructLayout.Default;
    }

    /// <summary>Attempts to check and get filed offset attribute and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <param name="offset">Receives the offset produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCheckAndGetFiledOffsetAttribute(ISymbol symbol, out int offset)
    {
        offset = -1;

        const string attributeName = "System.Runtime.InteropServices.FieldOffsetAttribute";

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != attributeName)
                continue;

            // Validate constructor parameters.
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

    /// <summary>Checks serialize call back against the operation's requirements.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <param name="dataInfo">The metadata describing the serializable type.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckSerializeCallBack(INamedTypeSymbol symbol, LuminDataInfo dataInfo)
    {
        foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes())
            {
                switch (attr.AttributeClass?.Name)
                {
                    case LUMIN_PACK_ONSERIALIZING:
                        if (TryCheckCallBackMethod(symbol, method))
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnSerializing, method.IsStatic)); break;
                    case LUMIN_PACK_ONSERIALIZED:
                        if (TryCheckCallBackMethod(symbol, method))
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnSerialized, method.IsStatic)); break;
                    case LUMIN_PACK_ONDESERIALIZING:
                        if (TryCheckCallBackMethod(symbol, method))
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnDeserializing, method.IsStatic)); break;
                    case LUMIN_PACK_ONDESERIALIZED:
                        if (TryCheckCallBackMethod(symbol, method))
                            dataInfo.callBackMethods.Add((method.Name, SerializeCallBackType.OnDeserialized, method.IsStatic)); break;
                }
            }
        }
    }

    /// <summary>Attempts to check call back method and reports whether it succeeded.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    /// <param name="method">The method used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryCheckCallBackMethod(INamedTypeSymbol symbol, IMethodSymbol method)
    {
        if (symbol.TypeKind == TypeKind.Struct && symbol.IsUnmanagedType)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OnMethodInUnmanagedType,
                method.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault(),
                symbol.Name,
                method.Name));

            return false;
        }

        if (method.DeclaredAccessibility
            is Accessibility.NotApplicable
            or Accessibility.Private
            or Accessibility.Protected
            or Accessibility.ProtectedAndInternal)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OnMethodIsPrivate,
                method.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault(),
                symbol.Name,
                method.Name));

            return false;
        }

        if (method.Parameters.Length > 0)
        {
            _reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.OnMethodHasParameter,
                method.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault(),
                symbol.Name,
                method.Name));

            return false;
        }

        return true;
    }

    /// <summary>Performs the build local class name step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    public static string BuildLocalClassName(LuminDataInfo data)
    {
        string classFullName = data.classFullName;

        if (classFullName.StartsWith("global::"))
        {
            classFullName = classFullName.Substring(8);
        }

        classFullName = classFullName.Replace(".", "_").Replace('+', '_');

        return "Local" + classFullName;
    }

    /// <summary>Performs the build local my class name step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    public static string BuildLocalMyClassName(LuminDataInfo data)
    {
        string classFullName = data.classFullName;

        if (classFullName.StartsWith("global::"))
        {
            classFullName = classFullName.Substring(8);
        }

        classFullName = classFullName.Replace(".", "_").Replace('+', '_');

        if (classFullName.Contains('<'))
        {
            classFullName = classFullName.Split('<').FirstOrDefault();
        }

        return "Local" + classFullName;
    }

    /// <summary>Performs the build formatter class name step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    public static string BuildFormatterClassName(LuminDataInfo data)
    {
        string classFullName = data.classFullName;

        if (classFullName.StartsWith("global::"))
        {
            classFullName = classFullName.Substring(8);
        }

        classFullName = classFullName.Replace(".", "_").Replace('+', '_');

        if (classFullName.Contains('<'))
        {
            classFullName = classFullName.Split('<').FirstOrDefault();
        }

        return classFullName + "Formatter";
    }

    /// <summary>Performs the build formatter class name step for the current operation.</summary>
    /// <param name="fullName">The full name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string BuildFormatterClassName(string fullName)
    {
        string classFullName = fullName;

        if (classFullName.StartsWith("global::"))
        {
            classFullName = classFullName.Substring(8);
        }

        classFullName = classFullName.Replace(".", "_").Replace('+', '_');

        if (classFullName.Contains('<'))
        {
            classFullName = classFullName.Split('<').FirstOrDefault();
        }

        return classFullName + "Formatter";
    }

    /// <summary>
    /// Finds the pool-rental method annotated with LuminPackPoolRentAttribute.
    /// </summary>
    /// <param name="typeSymbol">The type or symbol to analyze.</param>
    /// <param name="location">The location used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static IMethodSymbol AnalyzeRentPoolMethod(INamedTypeSymbol typeSymbol, Location location)
    {
        var rentMethods = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method =>
                method.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackPoolRentAttribute")
            )
            .ToList();

        if (rentMethods.Count == 0)
            return null;

        if (rentMethods.Count > 1)
        {
            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.MultipleRentPoolMethods,
                location,
                typeSymbol.Name
            ));
            return null;
        }

        var rentMethod = rentMethods[0];

        // Require a static method.
        if (!rentMethod.IsStatic)
        {
            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.RentPoolMethodMustBeStatic,
                rentMethod.Locations.FirstOrDefault() ?? location,
                rentMethod.Name, typeSymbol.Name
            ));
            return null;
        }

        // Require a parameterless method.
        if (rentMethod.Parameters.Length != 0)
        {
            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.RentPoolMethodHasParameters,
                rentMethod.Locations.FirstOrDefault() ?? location,
                rentMethod.Name, typeSymbol.Name
            ));
            return null;
        }

        // Require the return type to match the current type.
        if (!SymbolEqualityComparer.Default.Equals(rentMethod.ReturnType, typeSymbol))
        {
            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.RentPoolMethodReturnTypeMismatch,
                rentMethod.Locations.FirstOrDefault() ?? location,
                rentMethod.Name,
                typeSymbol.Name,
                typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            ));
            return null;
        }

        return rentMethod;
    }

    /// <summary>
    /// Detects whether the compilation targets a Unity project.
    /// </summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsUnityProject(Compilation compilation)
    {
        // Check whether UnityEngine.Application is available.
        var unityApplicationType = compilation.GetTypeByMetadataName("UnityEngine.Application");

        // Check for UnityEditor, which exists only in the editor.
        var unityEditorType = compilation.GetTypeByMetadataName("UnityEditor.Editor");

        return unityApplicationType != null || unityEditorType != null;
    }
}
