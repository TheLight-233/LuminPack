using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

internal static class DiagnosticDescriptors
{
    const string Category = "GenerateLuminPack";

    public static readonly DiagnosticDescriptor StaticClass = new(
        id: "LuminPack001",
        title: "LuminPackable object can't be Static",
        messageFormat: "LuminPackable object '{0}' can't be Static",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor AbstractMustUnion = new(
        id: "LuminPack002",
        title: "abstract/interface type of LuminPackable object must annotate with Union",
        messageFormat: "abstract/interface type of LuminPackable object '{0}' must annotate with Union",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor ConstructorNoPublic = new(
        id: "LuminPack003",
        title: "LuminPackPackObject's constructor must have public",
        messageFormat: "The LuminPackable object field '{0}' has no public constructor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor FieldMustBeLuminPackable = new(
        id: "LuminPack004",
        title: "LuminPackPackObject's field must be LuminPackable",
        messageFormat: "The LuminPackable object field '{0}' must be LuminPackable",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor ContainsDuplicateNameField = new(
        id: "LuminPack005",
        title: "LuminPackPackObject's Contains duplicate name field",
        messageFormat: "The LuminPackable object field '{0}' contains duplicate name in base class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor OnMethodHasParameter = new(
        id: "LuminPack006",
        title: "LuminPackObject's On*** methods must has no parameter",
        messageFormat: "The LuminPackable object '{0}''s '{1}' method must has no parameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor OnMethodIsPrivate = new(
        id: "LuminPack007",
        title: "LuminPackObject's On*** methods must public",
        messageFormat: "The LuminPackable object '{0}''s '{1}' method must public",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OnMethodInUnamannagedType = new(
        id: "LuminPack008",
        title: "LuminPackObject's On*** methods can't annotate in unamnaged struct",
        messageFormat: "The LuminPackable object '{0}' is unmanaged struct that can't annotate On***Attribute however '{1}' method annotaed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor OverrideMemberCantAddAnnotation = new(
        id: "LuminPack009",
        title: "Override member can't annotate Ignore/Include attribute",
        messageFormat: "The LuminPackable object '{0}' override member '{1}' can't annotate {2} attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SealedTypeCantBeUnion = new(
        id: "LuminPack010",
        title: "Sealed type can't be union",
        messageFormat: "The LuminPackable object '{0}' is sealed type so can't be Union",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    public static readonly DiagnosticDescriptor ConcreteTypeCantBeUnion = new(
        id: "LuminPack011",
        title: "Concrete type can't be union",
        messageFormat: "The LuminPackable object '{0}' can be Union, only allow abstract or interface",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    public static readonly DiagnosticDescriptor UnionTagDuplicate = new(
        id: "LuminPack012",
        title: "Union tag is duplicate",
        messageFormat: "The LuminPackable object '{0}' union tag value is duplicate",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    public static readonly DiagnosticDescriptor UnionMemberTypeNotImplementBaseType = new(
        id: "LuminPack013",
        title: "Union member not implement union interface",
        messageFormat: "The LuminPackable object '{0}' union member '{1}' not implement union interface",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    public static readonly DiagnosticDescriptor UnionMemberTypeNotDerivedBaseType = new(
        id: "LuminPack014",
        title: "Union member not dervided union base type",
        messageFormat: "The LuminPackable object '{0}' union member '{1}' not derived union type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnionMemberNotAllowStruct = new(
        id: "LuminPack015",
        title: "Union member can't be struct",
        messageFormat: "The LuminPackable object '{0}' union member '{1}' can't be member, not allows struct",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnionMemberMustBeLuminPackable = new(
        id: "LuminPack016",
        title: "Union member must be LuminPackable",
        messageFormat: "The LuminPackable object '{0}' union member '{1}' must be LuminPackable",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor UnionMemberGenericCountExceed = new (
        id: "LuminPack017",
        title: "Union member generic parameter count exceeds base type",
        messageFormat: "Union member '{0}' has more generic parameters ({2}) than base type '{1}' (max allowed: {1})",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    

    public static readonly DiagnosticDescriptor MembersCountOver250 = new(
        id: "LuminPack018",
        title: "Members count limit",
        messageFormat: "The LuminPackable object '{0}' member count is '{1}', however limit size is 249",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberCantSerializeType = new(
        id: "LuminPack019",
        title: "Member can't serialize type",
        messageFormat: "The LuminPackable object '{0}' member '{1}' type is '{2}' that can't serialize",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberIsNotLuminPackable = new(
        id: "LuminPack020",
        title: "Member is not LuminPackable object",
        messageFormat: "The LuminPackable object '{0}' member '{1}' type '{2}' is not LuminPackable. Annotate [LuminPackable] to '{2}' or if external type that can serialize, annotate `[LuminPackAllowSerialize]` to member",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeIsRefStruct = new(
        id: "LuminPack021",
        title: "Type is ref struct",
        messageFormat: "The LuminPackable object '{0}' is ref struct, it can not serialize",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberIsRefStruct = new(
        id: "LuminPack022",
        title: "Member is ref struct",
        messageFormat: "The LuminPackable object '{0}' member '{1}' type '{2}' is ref struct, it can not serialize",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor NetStandardClassOrStructMemberFieldCantInclude = new(
        id: "LuminPack023",
        title: "The LuminPackInclude attribute will not work in NetStandard 2.1",
        messageFormat: "The LuminPackable object '{0}' member '{1}' contains LuminPackInclude attribute, which is used in the . NetStandard 2.1 will not work, please use LuminPackObject instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantRequiredOrder = new(
        id: "LuminPack024",
        title: "CircularReference And VersionTolerant LuminPack Object member must require LuminPackOrder attribute",
        messageFormat: "Member '{0}' must be marked with [LuminPackOrder] for CircleReference/VersionTolerant types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor CircularReferenceAndVersionTolerantDuplicateOrder = new (
        id: "LuminPack025",
        title: "Duplicate LuminPackOrder value",
        messageFormat: "Order value '{0}' is used by multiple members: {1}",
        category: "LuminPack",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnamangedStructWithLayoutAutoField = new(
        id: "LuminPack026",
        title: "Before .NET 7 unmanaged struct must annotate LayoutKind.Auto or Explicit",
        messageFormat: "The unmanaged struct '{0}' has LayoutKind.Auto field('{1}'). Before .NET 7, if field contains Auto then automatically promote to LayoutKind.Auto but .NET 7 is Sequential so breaking binary compatibility when runtime upgraded. To safety, you have to annotate [StructLayout(LayoutKind.Auto)] or LayoutKind.Explicit to type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InheritTypeCanNotIncludeParentPrivateMember = new(
        id: "LuminPack027",
        title: "Inherit type can not include private member",
        messageFormat: "Type '{0}' can not include parent type's private member '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor UndefinedGenericParameterError = new(
        id: "LuminPack028",
        title: "LuminPackPackObject's Contains UndefinedGenericParameter",
        messageFormat: "The LuminPackable Object Contains UndefinedGenericParameter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor UnionMemberAutoDiscovered = new (
        id: "LuminPack029",
        title: "Union member automatically discovered",
        messageFormat: "Type '{0}' was automatically registered as union member of '{1}' with tag {2}",
        category: "LuminPack.Union",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "This type was automatically discovered and assigned a deterministic tag for polymorphic serialization."
    );

    public static readonly DiagnosticDescriptor TooManyUnionMembers = new (
        id: "LuminPack030",
        title: "Too many union members",
        messageFormat: "Union type '{0}' has {1} derived types, which exceeds the maximum of 256",
        category: "LuminPack.Union",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Union types can have at most 256 derived types (tags 0-255)."
    );
    
    public static readonly DiagnosticDescriptor MultipleConstructorsRequireAttribute = new(
        id: "LuminPack031",
        title: "Multiple constructors require LuminPackConstructor attribute",
        messageFormat: "Type '{0}' has multiple constructors, one must be marked with [LuminPackConstructor]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConstructorParameterNameMismatch = new(
        id: "LuminPack032",
        title: "Constructor parameter name does not match field name",
        messageFormat: "Constructor parameter '{0}' in type '{1}' does not match any field name (case-insensitive comparison)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoPublicConstructor = new(
        id: "LuminPack033",
        title: "No public constructor found",
        messageFormat: "Type '{0}' has no public constructor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor NestedClassMustBePublicOrInternal = new(
        id: "LuminPack034",
        title: "Nested class must be public or internal",
        messageFormat: "The LuminPackable nested class '{0}' must be public or internal to be serializable",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedClassAccessibilityError = new(
        id: "LuminPack035", 
        title: "Nested class accessibility error",
        messageFormat: "Nested class '{0}' has invalid accessibility '{1}'. Only public and internal are supported for serialization",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor MultipleRentPoolMethods = new(
        id: "LuminPack036",
        title: "Multiple RentPool methods",
        messageFormat: "Type '{0}' has multiple methods marked with LuminPackPoolRentAttribute. Only one is allowed.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RentPoolMethodHasParameters = new(
        id: "LuminPack037",
        title: "RentPool method must have no parameters",
        messageFormat: "RentPool method '{0}' in type '{1}' must have no parameters",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RentPoolMethodReturnTypeMismatch = new(
        id: "LuminPack038",
        title: "RentPool method return type mismatch",
        messageFormat: "RentPool method '{0}' in type '{1}' must return the same type as the containing type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RentPoolMethodIsStatic = new(
        id: "LuminPack039",
        title: "RentPool method must be static",
        messageFormat: "RentPool method '{0}' in type '{1}' must be static",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ManagedArrayDataCompress = new(
        id: "LuminPack040",
        title: "Array of managed type can't be compressed",
        messageFormat: "The LuminPackable object '{0}' member '{1}' is array of managed type '{2}', which can't be compressed.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
