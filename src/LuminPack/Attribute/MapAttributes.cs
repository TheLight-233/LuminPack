namespace LuminPack.Attribute;

// ─────────────────────────────────────────────────────────────────────────────
//  LuminMapper: AutoMapper-equivalent, zero-GC, source-generator driven
//
//  Two mapping paths that can be combined:
//
//  ① Automatic mapping with no handwritten mapper class.
//     Annotate the source type with LuminMapTo(typeof(TDest)).
//     The generator emits the mapper class and registration code without a handwritten mapper.
//
//     [LuminPackable]
//     [LuminMapTo(typeof(PlayerDto))]
//     public class PlayerData { ... }
//
//     // The generator emits PlayerDataMapper.g.cs containing:
//     //   PlayerData → PlayerDto  ToDto(in PlayerData)
//     //   PlayerData → PlayerDto  MapIntoDto(in PlayerData, ref PlayerDto)
//     //   PlayerData → PlayerDto[]  ToDtoArray(PlayerData[])
//     //   PlayerData → List<PlayerDto>  ToDtoList(List<PlayerData>)
//
//  ② Manual mapping for custom method names, multiple type pairs, or specialized parameters.
//     Annotate a static partial class with LuminMapper and declare partial methods.
//     The generator implements the body while the user controls method names and signatures.
//
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Declares an automatically generated mapping from the annotated source type to a destination type.
/// The generator emits a static <c>{SourceTypeName}Mapper</c> class;
/// No handwritten mapper class or partial method is required.
///
/// <para>Repeat this attribute to map one source to multiple destinations:</para>
/// <code>
/// [LuminMapTo(typeof(PlayerDto))]
/// [LuminMapTo(typeof(PlayerSnapshot))]
/// public class PlayerData { ... }
/// </code>
///
/// <para>Generated method names use the destination type name after removing its shared source prefix:</para>
/// <code>
/// public static PlayerDto ToDto(in PlayerData source);
/// public static void      MapIntoDto(in PlayerData source, ref PlayerDto dest);
/// public static PlayerDto[] ToDtoArray(PlayerData[] sources);
/// public static List&lt;PlayerDto&gt; ToDtoList(List&lt;PlayerData&gt; sources);
/// </code>
///
/// <para>Destination members can use LuminMapIgnore, LuminMapFromMember, and related attributes
/// to configure member bindings read by the generator.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class LuminMapToAttribute : System.Attribute
{
    /// <summary>Gets the destination type.</summary>
    public Type DestinationType { get; }

    /// <summary>
    /// Optionally overrides the inferred method suffix.
    /// The default suffix removes the shared source prefix; PlayerData to PlayerDto yields Dto.
    /// Specify the suffix explicitly when inference does not produce the desired name:
    /// <c>[LuminMapTo(typeof(PlayerDto), MethodSuffix = "DtoV2")]</c>
    /// → Generates ToDtoV2, MapIntoDtoV2, ToDtoV2Array, and ToDtoV2List.
    /// </summary>
    public string? MethodSuffix { get; set; }

    /// <summary>
    /// Whether to generate collection mapping methods, ToDtoArray and ToDtoList. Defaults to true.
    /// </summary>
    public bool GenerateCollectionMethods { get; set; } = true;

    /// <summary>Initializes a new instance of <c>LuminMapToAttribute</c>.</summary>
    /// <param name="destinationType">The destination type used by this operation.</param>
    public LuminMapToAttribute(Type destinationType)
    {
        DestinationType = destinationType;
    }
}

/// <summary>
/// Marks a static partial class as a container for manually declared mapping methods.
/// The user declares static partial method signatures and the generator supplies implementations.
///
/// <para>Use this declaration for the following scenarios:</para>
/// <list type="bullet">
///   <item>Use custom mapping method names.</item>
///   <item>Group multiple source and destination pairs in one mapper.</item>
///   <item>Wrap generated partial implementations with custom application logic.</item>
/// </list>
///
/// <para>Supported partial method signatures:</para>
/// <code>
/// public static partial TDest     ToXxx(in TSource source);
/// public static partial void      MapIntoXxx(in TSource source, ref TDest dest);
/// public static partial TDest[]   ToXxxArray(TSource[] sources);
/// public static partial List&lt;TDest&gt; ToXxxList(List&lt;TSource&gt; sources);
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapperAttribute : System.Attribute
{
}

/// <summary>
/// Excludes a destination field or property from mapping assignments.
/// Applies to destination type members.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapIgnoreAttribute : System.Attribute
{
}

/// <summary>
/// Maps a destination member from a source member with a different name.
/// Applies to destination type members.
///
/// For example, reads the source NickName into the destination DisplayName.
///   [LuminMapFromMember("NickName")]
///   public string DisplayName { get; set; }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapFromMemberAttribute : System.Attribute
{
    /// <summary>The name of the corresponding source member.</summary>
    public string SourceMemberName { get; }

    /// <summary>Initializes a new instance of <c>LuminMapFromMemberAttribute</c>.</summary>
    /// <param name="sourceMemberName">The source member name used by this operation.</param>
    public LuminMapFromMemberAttribute(string sourceMemberName)
    {
        SourceMemberName = sourceMemberName;
    }
}

/// <summary>
/// Maps the destination member through a static conversion method, which
/// Accepts the source member type as its only parameter and returns the destination member type.
/// Applies to destination type members.
///
/// For example, converts the source Birthday string to the destination BirthDate DateTime.
///   [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
///   public DateTime BirthDate { get; set; }
///
///   // Conversion method:
///   public static class DateConverters {
///       public static DateTime ParseDate(string value) => DateTime.Parse(value);
///   }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapUsingAttribute : System.Attribute
{
    /// <summary>The type that declares the static conversion methods.</summary>
    public Type ConverterType { get; }

    /// <summary>The static conversion method name; use nameof to keep references synchronized.</summary>
    public string MethodName { get; }

    /// <summary>Initializes a new instance of <c>LuminMapUsingAttribute</c>.</summary>
    /// <param name="converterType">The converter type used by this operation.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminMapUsingAttribute(Type converterType, string methodName)
    {
        ConverterType = converterType;
        MethodName = methodName;
    }
}

/// <summary>
/// Combine with LuminMapUsing to read a differently named source member
/// Specifies the source name when combining renaming with a custom conversion method.
///
/// For example, reads BirthdayString and converts it through ParseDate into BirthDate.
///   [LuminMapFromMember("BirthdayString")]
///   [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
///   public DateTime BirthDate { get; set; }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapConditionAttribute : System.Attribute
{
    /// <summary>
    /// A static condition method with signature static bool Method(in TSource source).
    /// When the condition returns false, the destination member retains its default value and is not assigned.
    /// </summary>
    public Type ConditionType { get; }
    /// <summary>Gets the method name.</summary>
    public string MethodName { get; }

    /// <summary>Initializes a new instance of <c>LuminMapConditionAttribute</c>.</summary>
    /// <param name="conditionType">The condition type used by this operation.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    public LuminMapConditionAttribute(Type conditionType, string methodName)
    {
        ConditionType = conditionType;
        MethodName = methodName;
    }
}

/// <summary>
/// Fills a destination field or property with a constant or default value instead of reading the corresponding source member.
///
/// The generator embeds constants at compile time. Supported values are
/// string, bool, int, long, float, double, and null.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapConstantAttribute : System.Attribute
{
    /// <summary>Gets the value.</summary>
    public object? Value { get; }

    /// <summary>Initializes a new instance of <c>LuminMapConstantAttribute</c>.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    public LuminMapConstantAttribute(object? value)
    {
        Value = value;
    }
}
