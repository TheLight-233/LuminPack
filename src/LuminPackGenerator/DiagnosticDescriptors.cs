using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

internal static class DiagnosticDescriptors
{
    private const string Category = "LuminPack.SourceGeneration";
    private const string UnionCategory = "LuminPack.SourceGeneration.Union";

    // Packable type and member validation.
    public static readonly DiagnosticDescriptor StaticClass = Error(
        "LuminPack001",
        "Static type is not serializable",
        "Type '{0}' is static and cannot be annotated with [LuminPackable].");

    public static readonly DiagnosticDescriptor FieldMustBeLuminPackable = Error(
        "LuminPack002",
        "Nested type is not serializable",
        "Type '{0}' must be annotated with [LuminPackable] before it can be serialized as a nested object.");

    public static readonly DiagnosticDescriptor ContainsDuplicateNameField = Error(
        "LuminPack003",
        "Duplicate serialized member name",
        "Member '{0}' duplicates a serialized member name declared by a base type.");

    public static readonly DiagnosticDescriptor OnMethodHasParameter = Error(
        "LuminPack004",
        "Serialization callback has parameters",
        "Serialization callback '{1}' on '{0}' must not declare parameters.");

    public static readonly DiagnosticDescriptor OnMethodIsPrivate = Error(
        "LuminPack005",
        "Serialization callback is inaccessible",
        "Serialization callback '{1}' on '{0}' must be public or internal.");

    public static readonly DiagnosticDescriptor OnMethodInUnmanagedType = Error(
        "LuminPack006",
        "Serialization callback on unmanaged struct",
        "Unmanaged struct '{0}' cannot declare serialization callback '{1}'.");

    public static readonly DiagnosticDescriptor OverrideMemberCantAddAnnotation = Error(
        "LuminPack007",
        "Attribute is invalid on an override",
        "Overridden member '{1}' on '{0}' cannot use [{2}].");

    // Union validation and discovery.
    public static readonly DiagnosticDescriptor SealedTypeCantBeUnion = UnionError(
        "LuminPack008",
        "Sealed type cannot be a union root",
        "Union root '{0}' is sealed. Union roots must be abstract classes or interfaces.");

    public static readonly DiagnosticDescriptor ConcreteTypeCantBeUnion = UnionError(
        "LuminPack009",
        "Concrete type cannot be a union root",
        "Union root '{0}' must be an abstract class or an interface.");

    public static readonly DiagnosticDescriptor UnionTagDuplicate = UnionError(
        "LuminPack010",
        "Duplicate union tag",
        "Union root '{1}' declares tag '{0}' more than once.");

    public static readonly DiagnosticDescriptor UnionMemberTypeNotImplementBaseType = UnionError(
        "LuminPack011",
        "Union member does not implement the root interface",
        "Union member '{0}' does not implement union interface '{1}'.");

    public static readonly DiagnosticDescriptor UnionMemberTypeNotDerivedBaseType = UnionError(
        "LuminPack012",
        "Union member does not derive from the root type",
        "Union member '{0}' does not derive from union root '{1}'.");

    public static readonly DiagnosticDescriptor UnionMemberNotAllowStruct = UnionError(
        "LuminPack013",
        "Struct cannot be a union root",
        "Union root '{0}' is a struct. Union roots must be abstract classes or interfaces.");

    public static readonly DiagnosticDescriptor UnionMemberMustBeLuminPackable = UnionError(
        "LuminPack014",
        "Union member is not serializable",
        "Union member '{0}' must be annotated with [LuminPackable].");

    public static readonly DiagnosticDescriptor UnionMemberGenericCountExceed = UnionError(
        "LuminPack015",
        "Union member has too many generic parameters",
        "Union member '{0}' has {2} generic parameters; the union root supports at most {1}.");

    public static readonly DiagnosticDescriptor UnionMemberAutoDiscovered = UnionInfo(
        "LuminPack016",
        "Union member was auto-discovered",
        "Type '{0}' was automatically registered as a union member of '{1}' with tag {2}.");

    public static readonly DiagnosticDescriptor TooManyUnionMembers = UnionError(
        "LuminPack017",
        "Too many union members",
        "Union type '{0}' has {1} derived types, exceeding the 256-member limit.");

    // Serialized shape validation.
    public static readonly DiagnosticDescriptor MembersCountOver250 = Error(
        "LuminPack018",
        "Too many serialized members",
        "Type '{0}' has {1} serialized members; the binary object format supports at most 249.");

    public static readonly DiagnosticDescriptor TypeIsRefStruct = Error(
        "LuminPack019",
        "ref struct is not serializable",
        "Type '{0}' is a ref struct and cannot be serialized.");

    public static readonly DiagnosticDescriptor MemberIsRefStruct = Error(
        "LuminPack020",
        "ref struct member is not serializable",
        "Member '{1}' on '{0}' has ref struct type '{2}', which cannot be serialized.");

    public static readonly DiagnosticDescriptor NetStandardClassOrStructMemberFieldCantInclude = Warning(
        "LuminPack021",
        "[LuminPackInclude] requires a runtime accessor",
        "Member '{1}' on '{0}' uses [LuminPackInclude], which is unavailable for this target framework.");

    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantRequiredOrder = Error(
        "LuminPack022",
        "[LuminPackOrder] is required",
        "Member '{0}' must be marked with [LuminPackOrder] in circular-reference or version-tolerant types.");

    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantDuplicateOrder = Error(
        "LuminPack023",
        "Duplicate [LuminPackOrder] value",
        "Order value '{0}' is assigned to multiple members: {1}.");

    public static readonly DiagnosticDescriptor InheritTypeCanNotIncludeParentPrivateMember = Error(
        "LuminPack024",
        "Inherited private member cannot be included",
        "Type '{0}' cannot include private member '{1}' declared by a base type.");

    // Construction and accessibility validation.
    public static readonly DiagnosticDescriptor MultipleConstructorsRequireAttribute = Error(
        "LuminPack025",
        "Constructor selection is ambiguous",
        "Type '{0}' has multiple constructors; mark one with [LuminPackConstructor].");

    public static readonly DiagnosticDescriptor ConstructorParameterNameMismatch = Error(
        "LuminPack026",
        "Constructor parameter has no matching member",
        "Constructor parameter '{0}' on '{1}' does not match a serializable member (case-insensitive).");

    public static readonly DiagnosticDescriptor NoPublicConstructor = Error(
        "LuminPack027",
        "No public constructor",
        "Type '{0}' has no public constructor.");

    public static readonly DiagnosticDescriptor NestedClassMustBePublicOrInternal = Error(
        "LuminPack028",
        "Nested type is inaccessible to generated code",
        "Nested [LuminPackable] type '{0}' must be public or internal.");

    public static readonly DiagnosticDescriptor NestedClassAccessibilityError = Error(
        "LuminPack029",
        "Unsupported nested-type accessibility",
        "Nested type '{0}' has accessibility '{1}'. Only public and internal are supported.");

    public static readonly DiagnosticDescriptor MultipleRentPoolMethods = Error(
        "LuminPack030",
        "Multiple pool-rent methods",
        "Type '{0}' has multiple methods marked with [LuminPackPoolRent]. Only one is allowed.");

    public static readonly DiagnosticDescriptor RentPoolMethodHasParameters = Error(
        "LuminPack031",
        "Pool-rent method has parameters",
        "Pool-rent method '{0}' on '{1}' must not declare parameters.");

    public static readonly DiagnosticDescriptor RentPoolMethodReturnTypeMismatch = Error(
        "LuminPack032",
        "Pool-rent method returns the wrong type",
        "Pool-rent method '{0}' on '{1}' must return '{2}'.");

    public static readonly DiagnosticDescriptor RentPoolMethodMustBeStatic = Error(
        "LuminPack033",
        "Pool-rent method is not static",
        "Pool-rent method '{0}' on '{1}' must be static.");

    public static readonly DiagnosticDescriptor ManagedArrayDataCompress = Error(
        "LuminPack034",
        "Managed arrays cannot use [LuminPackCompress]",
        "Member '{1}' on '{0}' is an array of managed type '{2}' and cannot use [LuminPackCompress].");

    public static readonly DiagnosticDescriptor UnionParticipantMustBePartial = UnionError(
        "LuminPack035",
        "Union participant must be partial",
        "Union participant '{0}' and every containing type must be partial so virtual union dispatch can be generated for '{1}'.");

    // Generator failures retain a reserved high number so adding a validation diagnostic does
    // not change the meaning of an unexpected source-generation failure.
    public static readonly DiagnosticDescriptor GeneratorFailure = Error(
        "LuminPack099",
        "Source generation failed",
        "LuminPack source generation failed: {0}");

    // Custom formatter validation ([LuminPackable(GeneratorType.Custom)]).
    public static readonly DiagnosticDescriptor CustomMissingMethod = Error(
        "LuminPack100",
        "Custom formatter method missing",
        "Custom type '{0}' must declare static method '{1}' with signature '{2}'.");

    public static readonly DiagnosticDescriptor CustomMethodMustBeStatic = Error(
        "LuminPack101",
        "Custom formatter method must be static",
        "Custom formatter method '{1}' on '{0}' must be static.");

    public static readonly DiagnosticDescriptor CustomMethodSignatureMismatch = Error(
        "LuminPack102",
        "Custom formatter method signature mismatch",
        "Custom formatter method '{1}' on '{0}' must have signature '{2}'.");

    public static readonly DiagnosticDescriptor CustomMethodMustBeAccessible = Error(
        "LuminPack103",
        "Custom formatter method must be accessible",
        "Custom formatter method '{1}' on '{0}' must be public or internal so generated registration code can take its address.");

    public static readonly DiagnosticDescriptor CustomJsonMethodsIncomplete = Error(
        "LuminPack104",
        "Custom JSON formatter methods incomplete",
        "Custom type '{0}' declares '{1}' but not '{2}'; JSON registration requires both SerializeJson and DeserializeJson.");

    public static readonly DiagnosticDescriptor CustomCalculateOffsetRequiresJson = Error(
        "LuminPack105",
        "Custom CalculateOffset requires JSON methods",
        "Custom type '{0}' declares CalculateOffset, but the matching Register overload also requires SerializeJson and DeserializeJson.");

    public static readonly DiagnosticDescriptor CustomTypeMustNotBeGeneric = Error(
        "LuminPack106",
        "Custom formatter type cannot be generic",
        "Custom formatter type '{0}' cannot be generic; runtime registration happens per closed type.");

    public static readonly DiagnosticDescriptor CustomUnionNotSupported = Error(
        "LuminPack107",
        "Custom formatter type cannot be a union root",
        "Custom formatter type '{0}' cannot be abstract or a union root; union dispatch requires generated formatters.");

    public static readonly DiagnosticDescriptor CustomRegistrationSkippedNoUnsafe = Warning(
        "LuminPack108",
        "Custom registration skipped without unsafe",
        "Custom type '{0}' will not be registered automatically because the project does not enable AllowUnsafeBlocks (Register uses function pointers); enable unsafe or call LuminPackSerializer.Register manually.");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor UnionError(string id, string title, string message) =>
        new(id, title, message, UnionCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor UnionInfo(string id, string title, string message) =>
        new(id, title, message, UnionCategory, DiagnosticSeverity.Info, isEnabledByDefault: true);
}
