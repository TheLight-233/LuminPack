// ═══════════════════════════════════════════════════════════════════════════
//  LuminMapper 完整使用示例
//  展示零样板「自动路径」与自定义「手动路径」
// ═══════════════════════════════════════════════════════════════════════════

using LuminPack.Attribute;
using LuminPack.Mapping;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  转换器 & 条件检查（供特性引用）
// ─────────────────────────────────────────────────────────────────────────────

public static class LevelConverters
{
    public static string IntToString(int level) => $"Lv.{level}";
}

public static class DateConverters
{
    public static System.DateTime ParseDate(string value)
        => System.DateTime.TryParse(value, out var dt) ? dt : System.DateTime.MinValue;
}

public static class PlayerConditions
{
    // [LuminMapCondition] 要求签名 static bool Method(in TSource source)
    public static bool IsActive(in PlayerData source) => source.IsActive;
}

// ═══════════════════════════════════════════════════════════════════════════
//  路径①：自动路径 — 用户只需在源类型上标 [LuminMapTo]，0 样板代码
//           源生成器自动产出 PlayerDataMapper.g.cs，内含完整 Mapper 类
// ═══════════════════════════════════════════════════════════════════════════

[LuminPackable]
[LuminMapTo(typeof(PlayerDto), MethodSuffix = "Dto", GenerateCollectionMethods = true)]    // ← 唯一需要写的东西
[LuminMapTo(typeof(PlayerSnapshot), MethodSuffix = "Snapshot", GenerateCollectionMethods = false)]
public class PlayerData
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = "";
    public string NickName    { get; set; } = "";
    public string Password    { get; set; } = "";
    public float  Health      { get; set; }
    public int    LevelRaw    { get; set; }
    public string BirthdayStr { get; set; } = "";
    public bool   IsActive    { get; set; }
}

// 目标类型：通过成员特性精细控制字段映射
[LuminPackable]
public class PlayerDto
{
    public int    Id           { get; set; }
    public string Name         { get; set; } = "";

    [LuminMapFromMember("NickName")]              // 源字段改名
    public string DisplayName  { get; set; } = "";

    [LuminMapIgnore]                              // 不从源映射
    public string Password     { get; set; } = "";

    public float  Health       { get; set; }

    [LuminMapFromMember("LevelRaw")]
    [LuminMapUsing(typeof(LevelConverters), nameof(LevelConverters.IntToString))]
    public string Level        { get; set; } = "";   // int → string 转换

    [LuminMapFromMember("BirthdayStr")]
    [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
    public System.DateTime BirthDate { get; set; }   // 改名 + 类型转换

    [LuminMapFromMember("Health")]
    [LuminMapCondition(typeof(PlayerConditions), nameof(PlayerConditions.IsActive))]
    public float  ActiveHealth { get; set; }          // 条件映射

    [LuminMapConstant("v2")]                      // 常量填充
    public string SchemaVersion { get; set; } = "";
}

[LuminPackable]
public class PlayerSnapshot
{
    public int    Id     { get; set; }
    public string Name   { get; set; } = "";
    public float  Health { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  源生成器自动产出 PlayerDataMapper.g.cs（节选）：
//
//  namespace MyGame           // 与 PlayerData 同命名空间
//  {
//      public static class PlayerDataMapper   // 全部由生成器产出，用户无需写任何内容
//      {
//          [Preserve, MethodImpl(AggressiveInlining)]
//          public static PlayerDto ToDto(scoped in PlayerData source)
//          {
//              var dest = new PlayerDto();
//              dest.Id            = source.Id;
//              dest.Name          = source.Name;
//              dest.DisplayName   = source.NickName;
//              // [LuminMapIgnore] dest.Password — skipped
//              dest.Health        = source.Health;
//              dest.Level         = global::LevelConverters.IntToString(source.LevelRaw);
//              dest.BirthDate     = global::DateConverters.ParseDate(source.BirthdayStr);
//              if (global::PlayerConditions.IsActive(in source))
//                  dest.ActiveHealth = source.Health;
//              dest.SchemaVersion = "v2";
//              return dest;
//          }
//
//          [Preserve, MethodImpl(AggressiveInlining)]
//          public static void MapIntoDto(scoped in PlayerData source, ref PlayerDto dest) { ... }
//
//          [Preserve, MethodImpl(AggressiveInlining)]
//          public static PlayerDto[] ToDtoArray(PlayerData[] sources) { ... }
//
//          [Preserve, MethodImpl(AggressiveInlining)]
//          public static List<PlayerDto> ToDtoList(List<PlayerData> sources) { ... }
//
//          // MethodSuffix = "Snapshot"，GenerateCollectionMethods = false
//          public static PlayerSnapshot ToSnapshot(scoped in PlayerData source) { ... }
//          public static void MapIntoSnapshot(scoped in PlayerData source, ref PlayerSnapshot dest) { ... }
//      }
//  }
// ─────────────────────────────────────────────────────────────────────────────

// ═══════════════════════════════════════════════════════════════════════════
//  路径②：手动路径 — 需要自定义方法名 / 多对多映射 / 非常规签名时使用
//           用户声明 partial 方法签名，生成器补全方法体
// ═══════════════════════════════════════════════════════════════════════════

[LuminMapper]   // ← 仍需手写这个 partial class，但只需声明签名，方法体由生成器产出
public static partial class GameObjectMapper
{
    // 自定义方法名（不想用 PlayerDataMapper 的默认命名）
    public static partial PlayerDto       ConvertPlayer   (PlayerData    source);
    public static partial void            FillPlayer      (PlayerData    source, ref PlayerDto     dest);

    // 跨类型的统一 Mapper（多对多）
    public static partial PlayerSnapshot  ToSnapshot      (PlayerData    source);
    public static partial EnemySnapshot   ToEnemySnapshot (EnemyData     source);
    public static partial void            FillEnemy       (EnemyData     source, ref EnemySnapshot dest);

    // 集合映射
    public static partial List<PlayerDto> BatchConvert    (List<PlayerData> sources);
    
}

// ─────────────────────────────────────────────────────────────────────────────
//  两个测试用辅助类型
// ─────────────────────────────────────────────────────────────────────────────

[LuminPackable]
public class EnemyData   { public int Id { get; set; } public string Name { get; set; } = ""; public float Damage { get; set; } }

[LuminPackable]
public class EnemySnapshot
{
    public int    Id     { get; set; }
    public string Name   { get; set; } = "";

    [LuminMapFromMember("Damage")]    // EnemyData.Damage → EnemySnapshot.Dmg
    public float  Dmg    { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  运行时调用（两条路径产出的 API 完全等价）
// ═══════════════════════════════════════════════════════════════════════════

class UsageExamples
{
    public static void Demo()
    {
        var player = new PlayerData { Id = 1, Name = "Alice", NickName = "Ace",
            Password = "secret", Health = 100f, LevelRaw = 42,
            BirthdayStr = "1990-05-15", IsActive = true };

        // ── 自动路径（路径①）生成的 API ──────────────────────────────────

        // 创建型：仅 new PlayerDto() 一次分配
        PlayerDto dto = PlayerDataMapper.ToDto(in player);

        // 就地型：完全 0 GC（对象池场景）
        PlayerDto pooled = Pool<PlayerDto>.Rent();
        PlayerDataMapper.MapIntoDto(in player, ref pooled);
        Pool<PlayerDto>.Return(pooled);

        // 集合映射
        PlayerDto[] arr  = PlayerDataMapper.ToDtoArray(new[] { player });
        List<PlayerDto> list = PlayerDataMapper.ToDtoList(new List<PlayerData> { player });

        // 多目标（第二个 [LuminMapTo]）
        PlayerSnapshot snap = PlayerDataMapper.ToSnapshot(in player);

        // ── 手动路径（路径②）生成的 API ──────────────────────────────────

        PlayerDto dto2 = GameObjectMapper.ConvertPlayer(player);
        GameObjectMapper.FillPlayer(player, ref pooled);

        // ── 动态分发（类型在运行时确定时使用）────────────────────────────
        // 两条路径生成的 Mapper 都会注册到 LuminMapper，可通过统一入口调用
        // 内部：一次静态泛型字段读取 O(1) + 一次委托调用，无反射无字典查找
        PlayerDto dtoD = LuminMapper.Map<PlayerData, PlayerDto>(in player);
        LuminMapper.MapInto(in player, ref pooled);

        bool ok = LuminMapper.IsRegistered<PlayerData, PlayerDto>(); // true
        Console.WriteLine(ok);
    }

    static class Pool<T> where T : new()
    {
        static readonly System.Collections.Concurrent.ConcurrentBag<T> _bag = new();
        public static T Rent() => _bag.TryTake(out var v) ? v : new T();
        public static void Return(T v) => _bag.Add(v);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  性能特性一览
// ═══════════════════════════════════════════════════════════════════════════
/*
  ┌─────────────────────────────────────────┬──────────┬──────────────────────┐
  │ 调用路径                                 │ GC 分配   │ 说明                 │
  ├─────────────────────────────────────────┼──────────┼──────────────────────┤
  │ PlayerDataMapper.ToDto(in src)          │ 1× new() │ 目标 class 本身      │
  │ PlayerDataMapper.ToDto(in src) [struct] │ 0        │ 值类型目标           │
  │ PlayerDataMapper.MapIntoDto(in, ref)    │ 0        │ 就地写入             │
  │ PlayerDataMapper.ToDtoArray(arr)        │ 1× arr   │ 结果数组             │
  │ LuminMapper.Map<S,D>(in src)            │ 0*       │ *委托仅注册时分配一次 │
  └─────────────────────────────────────────┴──────────┴──────────────────────┘

  生成代码特征：
  ─ [MethodImpl(AggressiveInlining)] 标记，JIT 可完全内联
  ─ .NET 8+ 使用 scoped in，防止防御性拷贝
  ─ 无反射、无表达式树、无字典查找
  ─ 集合映射用 for/foreach，无 LINQ 开销
*/
