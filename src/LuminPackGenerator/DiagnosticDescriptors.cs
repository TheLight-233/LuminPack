using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

internal static class DiagnosticDescriptors
{
    private const string Category = "LuminPack.SourceGeneration";
    private const string UnionCategory = "LuminPack.SourceGeneration.Union";

    // Packable type and member validation.
    public static readonly DiagnosticDescriptor StaticClass = Error("LuminPack001", "Static type is not serializable", "Type '{0}' is static and cannot be annotated with [LuminPackable].");
    public static readonly DiagnosticDescriptor AbstractMustUnion = UnionError("LuminPack002", "Abstract type requires union metadata", "Abstract or interface type '{0}' must declare [LuminPackUnion].");
    public static readonly DiagnosticDescriptor ConstructorNoPublic = Error("LuminPack003", "No public constructor", "Member '{0}' belongs to a LuminPackable type without a public constructor.");
    public static readonly DiagnosticDescriptor FieldMustBeLuminPackable = Error("LuminPack004", "Nested type is not serializable", "Type '{0}' must be annotated with [LuminPackable] before it can be serialized as a nested object.");
    public static readonly DiagnosticDescriptor ContainsDuplicateNameField = Error("LuminPack005", "Duplicate serialized member name", "Member '{0}' duplicates a serialized member name declared by a base type.");
    public static readonly DiagnosticDescriptor OnMethodHasParameter = Error("LuminPack006", "Serialization callback has parameters", "Serialization callback '{1}' on '{0}' must not declare parameters.");
    public static readonly DiagnosticDescriptor OnMethodIsPrivate = Error("LuminPack007", "Serialization callback is inaccessible", "Serialization callback '{1}' on '{0}' must be public or internal.");
    public static readonly DiagnosticDescriptor OnMethodInUnamannagedType = Error("LuminPack008", "Serialization callback on unmanaged struct", "Unmanaged struct '{0}' cannot declare serialization callback '{1}'.");
    public static readonly DiagnosticDescriptor OverrideMemberCantAddAnnotation = Error("LuminPack009", "Attribute is invalid on an override", "Overridden member '{1}' on '{0}' cannot use [{2}].");

    // Union validation.
    public static readonly DiagnosticDescriptor SealedTypeCantBeUnion = UnionError("LuminPack010", "Sealed type cannot be a union root", "Union root '{0}' is sealed. Union roots must be abstract classes or interfaces.");
    public static readonly DiagnosticDescriptor ConcreteTypeCantBeUnion = UnionError("LuminPack011", "Concrete type cannot be a union root", "Union root '{0}' must be an abstract class or an interface.");
    public static readonly DiagnosticDescriptor UnionTagDuplicate = UnionError("LuminPack012", "Duplicate union tag", "Union root '{1}' declares tag '{0}' more than once.");
    public static readonly DiagnosticDescriptor UnionMemberTypeNotImplementBaseType = UnionError("LuminPack013", "Union member does not implement the root interface", "Union member '{0}' does not implement union interface '{1}'.");
    public static readonly DiagnosticDescriptor UnionMemberTypeNotDerivedBaseType = UnionError("LuminPack014", "Union member does not derive from the root type", "Union member '{0}' does not derive from union root '{1}'.");
    public static readonly DiagnosticDescriptor UnionMemberNotAllowStruct = UnionError("LuminPack015", "Struct cannot be a union root", "Union root '{0}' is a struct. Union roots must be abstract classes or interfaces.");
    public static readonly DiagnosticDescriptor UnionMemberMustBeLuminPackable = UnionError("LuminPack016", "Union member is not serializable", "Union member '{0}' must be annotated with [LuminPackable].");
    public static readonly DiagnosticDescriptor UnionMemberGenericCountExceed = UnionError("LuminPack017", "Union member has too many generic parameters", "Union member '{0}' has {2} generic parameters; the union root supports at most {1}.");

    // Serialized shape validation.
    public static readonly DiagnosticDescriptor MembersCountOver250 = Error("LuminPack018", "Too many serialized members", "Type '{0}' has {1} serialized members; the binary object format supports at most 249.");
    public static readonly DiagnosticDescriptor MemberCantSerializeType = Error("LuminPack019", "Member type is not supported", "Member '{1}' on '{0}' has unsupported type '{2}'.");
    public static readonly DiagnosticDescriptor MemberIsNotLuminPackable = Error("LuminPack020", "Member type requires serialization metadata", "Member '{1}' on '{0}' uses '{2}', which must be [LuminPackable] or explicitly allowed for serialization.");
    public static readonly DiagnosticDescriptor TypeIsRefStruct = Error("LuminPack021", "ref struct is not serializable", "Type '{0}' is a ref struct and cannot be serialized.");
    public static readonly DiagnosticDescriptor MemberIsRefStruct = Error("LuminPack022", "ref struct member is not serializable", "Member '{1}' on '{0}' has ref struct type '{2}', which cannot be serialized.");
    public static readonly DiagnosticDescriptor NetStandardClassOrStructMemberFieldCantInclude = Warning("LuminPack023", "[LuminPackInclude] requires a runtime accessor", "Member '{1}' on '{0}' uses [LuminPackInclude], which is unavailable for this target framework.");
    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantRequiredOrder = Error("LuminPack024", "[LuminPackOrder] is required", "Member '{0}' must be marked with [LuminPackOrder] in circular-reference or version-tolerant types.");
    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantDuplicateOrder = Error("LuminPack025", "Duplicate [LuminPackOrder] value", "Order value '{0}' is assigned to multiple members: {1}.");
    public static readonly DiagnosticDescriptor UnamangedStructWithLayoutAutoField = Error("LuminPack026", "Unmanaged layout is not stable", "Unmanaged struct '{0}' contains auto-layout field '{1}'. Specify an explicit layout to preserve binary compatibility.");
    public static readonly DiagnosticDescriptor InheritTypeCanNotIncludeParentPrivateMember = Error("LuminPack027", "Inherited private member cannot be included", "Type '{0}' cannot include private member '{1}' declared by a base type.");
    public static readonly DiagnosticDescriptor UndefinedGenericParameterError = Error("LuminPack028", "Undefined generic parameter", "LuminPackable type contains an undefined generic parameter.");

    // Union discovery.
    public static readonly DiagnosticDescriptor UnionMemberAutoDiscovered = UnionInfo("LuminPack029", "Union member was auto-discovered", "Type '{0}' was automatically registered as a union member of '{1}' with tag {2}.");
    public static readonly DiagnosticDescriptor TooManyUnionMembers = UnionError("LuminPack030", "Too many union members", "Union type '{0}' has {1} derived types, exceeding the 256-member limit.");

    // Construction and accessibility validation.
    public static readonly DiagnosticDescriptor MultipleConstructorsRequireAttribute = Error("LuminPack031", "Constructor selection is ambiguous", "Type '{0}' has multiple constructors; mark one with [LuminPackConstructor].");
    public static readonly DiagnosticDescriptor ConstructorParameterNameMismatch = Error("LuminPack032", "Constructor parameter has no matching member", "Constructor parameter '{0}' on '{1}' does not match a serializable member (case-insensitive).");
    public static readonly DiagnosticDescriptor NoPublicConstructor = Error("LuminPack033", "No public constructor", "Type '{0}' has no public constructor.");
    public static readonly DiagnosticDescriptor NestedClassMustBePublicOrInternal = Error("LuminPack034", "Nested type is inaccessible to generated code", "Nested [LuminPackable] type '{0}' must be public or internal.");
    public static readonly DiagnosticDescriptor NestedClassAccessibilityError = Error("LuminPack035", "Unsupported nested-type accessibility", "Nested type '{0}' has accessibility '{1}'. Only public and internal are supported.");
    public static readonly DiagnosticDescriptor MultipleRentPoolMethods = Error("LuminPack036", "Multiple pool-rent methods", "Type '{0}' has multiple methods marked with [LuminPackPoolRent]. Only one is allowed.");
    public static readonly DiagnosticDescriptor RentPoolMethodHasParameters = Error("LuminPack037", "Pool-rent method has parameters", "Pool-rent method '{0}' on '{1}' must not declare parameters.");
    public static readonly DiagnosticDescriptor RentPoolMethodReturnTypeMismatch = Error("LuminPack038", "Pool-rent method returns the wrong type", "Pool-rent method '{0}' on '{1}' must return '{1}'.");
    public static readonly DiagnosticDescriptor RentPoolMethodIsStatic = Error("LuminPack039", "Pool-rent method is not static", "Pool-rent method '{0}' on '{1}' must be static.");
    public static readonly DiagnosticDescriptor ManagedArrayDataCompress = Error("LuminPack040", "Managed arrays cannot use [LuminPackCompress]", "Member '{1}' on '{0}' is an array of managed type '{2}' and cannot use [LuminPackCompress].");
    public static readonly DiagnosticDescriptor UnionParticipantMustBePartial = UnionError("LuminPack041", "Union participant must be partial", "Union participant '{0}' and every containing type must be partial so virtual union dispatch can be generated for '{1}'.");

    // Generator failures.
    public static readonly DiagnosticDescriptor GeneratorFailure = Error("LuminPack099", "Source generation failed", "LuminPack source generation failed: {0}");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor UnionError(string id, string title, string message) =>
        new(id, title, message, UnionCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor UnionInfo(string id, string title, string message) =>
        new(id, title, message, UnionCategory, DiagnosticSeverity.Info, isEnabledByDefault: true);
}
