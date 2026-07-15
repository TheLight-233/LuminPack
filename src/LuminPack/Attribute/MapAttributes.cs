using System;

namespace LuminPack.Attribute;

// ─────────────────────────────────────────────────────────────────────────────
//  LuminMapper: AutoMapper-equivalent, zero-GC, source-generator driven
//
//  两条使用路径（可混用）:
//
//  ① 自动路径（推荐，零样板）
//     只需在源类型上声明 [LuminMapTo(typeof(TDest))]，
//     源生成器自动生成完整的 Mapper 类和注册代码，用户无需写任何 Mapper 类。
//
//     [LuminPackable]
//     [LuminMapTo(typeof(PlayerDto))]
//     public class PlayerData { ... }
//
//     // 生成器自动产出 PlayerDataMapper.g.cs，包含:
//     //   PlayerData → PlayerDto  ToDto(in PlayerData)
//     //   PlayerData → PlayerDto  MapIntoDto(in PlayerData, ref PlayerDto)
//     //   PlayerData → PlayerDto[]  ToDtoArray(PlayerData[])
//     //   PlayerData → List<PlayerDto>  ToDtoList(List<PlayerData>)
//
//  ② 手动路径（需要自定义方法名 / 多对多映射 / 特殊参数时使用）
//     在 static partial class 上标记 [LuminMapper]，声明 partial 方法，
//     生成器实现方法体，用户保留对方法名和签名的完全控制。
//
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 【自动路径】在源类型（TSource）上声明，指定它应被映射到哪些目标类型。
/// 源生成器自动生成名为 <c>{SourceTypeName}Mapper</c> 的静态类，
/// 无需用户手写任何 Mapper 类或 partial 方法。
///
/// <para>可重复标记，支持一对多映射：</para>
/// <code>
/// [LuminMapTo(typeof(PlayerDto))]
/// [LuminMapTo(typeof(PlayerSnapshot))]
/// public class PlayerData { ... }
/// </code>
///
/// <para>生成的方法命名规则（以目标类型名去掉与源相同的前缀为后缀）：</para>
/// <code>
/// public static PlayerDto ToDto(in PlayerData source);
/// public static void      MapIntoDto(in PlayerData source, ref PlayerDto dest);
/// public static PlayerDto[] ToDtoArray(PlayerData[] sources);
/// public static List&lt;PlayerDto&gt; ToDtoList(List&lt;PlayerData&gt; sources);
/// </code>
///
/// <para>目标类型的成员上可使用 [LuminMapIgnore] / [LuminMapFromMember] 等特性
/// 精细控制字段映射关系，生成器会自动读取。</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class LuminMapToAttribute : System.Attribute
{
    public Type DestinationType { get; }

    /// <summary>
    /// 可选：覆盖自动推断的方法名后缀。
    /// 默认后缀 = 目标类型名去掉与源相同的前缀（如 PlayerData→PlayerDto 推断后缀 Dto）。
    /// 若自动推断结果不理想，可显式指定：
    /// <c>[LuminMapTo(typeof(PlayerDto), MethodSuffix = "DtoV2")]</c>
    /// → 生成 ToDtoV2 / MapIntoDtoV2 / ToDtoV2Array / ToDtoV2List
    /// </summary>
    public string? MethodSuffix { get; set; }

    /// <summary>
    /// 是否生成集合映射方法（ToDtoArray / ToDtoList）。默认 true。
    /// </summary>
    public bool GenerateCollectionMethods { get; set; } = true;

    public LuminMapToAttribute(Type destinationType)
    {
        DestinationType = destinationType;
    }
}

/// <summary>
/// 【手动路径】将一个 static partial class 标记为 LuminMapper 映射容器。
/// 用户声明 static partial 方法签名，源生成器生成方法体实现。
///
/// <para>适合以下场景：</para>
/// <list type="bullet">
///   <item>需要自定义映射方法名</item>
///   <item>一个 Mapper 类中包含多个不同源/目标类型的映射</item>
///   <item>需要在方法内添加自定义逻辑（在 partial 实现外包一层）</item>
/// </list>
///
/// <para>支持的 partial 方法签名：</para>
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
/// 标记目标类型的某个字段/属性在映射时应被忽略（不赋值）。
/// 适用于目标类型（TDest）的成员。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapIgnoreAttribute : System.Attribute
{
}

/// <summary>
/// 指定目标成员从源类型中不同名的成员映射。
/// 适用于目标类型（TDest）的成员。
///
/// 示例：目标的 DisplayName 从源的 NickName 读取
///   [LuminMapFromMember("NickName")]
///   public string DisplayName { get; set; }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapFromMemberAttribute : System.Attribute
{
    /// <summary>源类型中对应成员的名称</summary>
    public string SourceMemberName { get; }

    public LuminMapFromMemberAttribute(string sourceMemberName)
    {
        SourceMemberName = sourceMemberName;
    }
}

/// <summary>
/// 指定目标成员使用静态转换方法进行映射。转换方法必须是 static，
/// 接受源字段类型作为唯一参数，返回目标字段类型。
/// 适用于目标类型（TDest）的成员。
///
/// 示例：将源的 string Birthday 转换为目标的 DateTime BirthDate
///   [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
///   public DateTime BirthDate { get; set; }
///
///   // 转换方法：
///   public static class DateConverters {
///       public static DateTime ParseDate(string value) => DateTime.Parse(value);
///   }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapUsingAttribute : System.Attribute
{
    /// <summary>包含静态转换方法的类型</summary>
    public Type ConverterType { get; }

    /// <summary>静态转换方法名称（建议配合 nameof 使用）</summary>
    public string MethodName { get; }

    public LuminMapUsingAttribute(Type converterType, string methodName)
    {
        ConverterType = converterType;
        MethodName = methodName;
    }
}

/// <summary>
/// 可与 [LuminMapUsing] 联合使用：当需要从不同名的源成员读取，
/// 并同时经过自定义转换器时，用此特性指定源成员名称。
///
/// 示例：目标 BirthDate 从源 BirthdayString 读取，并用 ParseDate 转换
///   [LuminMapFromMember("BirthdayString")]
///   [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
///   public DateTime BirthDate { get; set; }
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapConditionAttribute : System.Attribute
{
    /// <summary>
    /// 静态条件检查方法，签名为 static bool Method(in TSource source)。
    /// 当条件返回 false 时，目标成员保持默认值，不做赋值。
    /// </summary>
    public Type ConditionType { get; }
    public string MethodName { get; }

    public LuminMapConditionAttribute(Type conditionType, string methodName)
    {
        ConditionType = conditionType;
        MethodName = methodName;
    }
}

/// <summary>
/// 标记目标类型的某个字段/属性使用常量/默认值填充，忽略源类型中的对应成员。
///
/// 注意：常量值在编译期由源生成器写死到生成代码中，仅支持
/// string, bool, int, long, float, double, 以及 null。
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class LuminMapConstantAttribute : System.Attribute
{
    public object? Value { get; }

    public LuminMapConstantAttribute(object? value)
    {
        Value = value;
    }
}


