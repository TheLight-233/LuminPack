// ═══════════════════════════════════════════════════════════════════════════
//  Complete LuminMapper usage example.
//  Demonstrates automatic mapping and custom manual declarations.
// ═══════════════════════════════════════════════════════════════════════════

using LuminPack.Attribute;
using LuminPack.Mapping;

// ─────────────────────────────────────────────────────────────────────────────
//  Conversion and condition methods referenced by attributes.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Provides level converters functionality.</summary>
public static class LevelConverters
{
    /// <summary>Measures int to string for the configured benchmark input.</summary>
    /// <param name="level">The level used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string IntToString(int level) => $"Lv.{level}";
}

/// <summary>Provides date converters functionality.</summary>
public static class DateConverters
{
    /// <summary>Measures parse date for the configured benchmark input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static System.DateTime ParseDate(string value)
        => System.DateTime.TryParse(value, out var dt) ? dt : System.DateTime.MinValue;
}

/// <summary>Provides player conditions functionality.</summary>
public static class PlayerConditions
{
    // [LuminMapCondition] requires static bool Method(in TSource source).
    /// <summary>Determines whether the supplied value has the active classification.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsActive(in PlayerData source) => source.IsActive;
}

// ═══════════════════════════════════════════════════════════════════════════
//  Automatic mapping: annotate the source with LuminMapTo; no mapper declaration is required.
//           The generator emits the complete mapper in PlayerDataMapper.g.cs.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Provides player data functionality.</summary>
[LuminPackable]
[LuminMapTo(typeof(PlayerDto), MethodSuffix = "Dto", GenerateCollectionMethods = true)]    // ← The only required handwritten declaration.
[LuminMapTo(typeof(PlayerSnapshot), MethodSuffix = "Snapshot", GenerateCollectionMethods = false)]
public class PlayerData
{
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the nick name.</summary>
    public string NickName { get; set; } = "";
    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = "";
    /// <summary>Gets or sets the health.</summary>
    public float Health { get; set; }
    /// <summary>Gets or sets the level raw.</summary>
    public int LevelRaw { get; set; }
    /// <summary>Gets or sets the birthday str.</summary>
    public string BirthdayStr { get; set; } = "";
    /// <summary>Gets or sets the is active.</summary>
    public bool IsActive { get; set; }
}

// Configure destination member mapping through attributes.
/// <summary>Provides player dto functionality.</summary>
[LuminPackable]
public class PlayerDto
{
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the display name.</summary>
    [LuminMapFromMember("NickName")]              // Rename the source member.
    public string DisplayName { get; set; } = "";

    /// <summary>Gets or sets the password.</summary>
    [LuminMapIgnore]                              // Do not map from the source.
    public string Password { get; set; } = "";

    /// <summary>Gets or sets the health.</summary>
    public float Health { get; set; }

    /// <summary>Gets or sets the level.</summary>
    [LuminMapFromMember("LevelRaw")]
    [LuminMapUsing(typeof(LevelConverters), nameof(LevelConverters.IntToString))]
    public string Level { get; set; } = "";   // Convert int to string.

    /// <summary>Gets or sets the birth date.</summary>
    [LuminMapFromMember("BirthdayStr")]
    [LuminMapUsing(typeof(DateConverters), nameof(DateConverters.ParseDate))]
    public System.DateTime BirthDate { get; set; }   // Rename and convert the member.

    /// <summary>Gets or sets the active health.</summary>
    [LuminMapFromMember("Health")]
    [LuminMapCondition(typeof(PlayerConditions), nameof(PlayerConditions.IsActive))]
    public float ActiveHealth { get; set; }          // Conditional mapping.

    /// <summary>Gets or sets the schema version.</summary>
    [LuminMapConstant("v2")]                      // Assign a constant value.
    public string SchemaVersion { get; set; } = "";
}

/// <summary>Provides player snapshot functionality.</summary>
[LuminPackable]
public class PlayerSnapshot
{
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the health.</summary>
    public float Health { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Excerpt from generated PlayerDataMapper.g.cs:
//
//  namespace MyGame           // Same namespace as PlayerData.
//  {
//      public static class PlayerDataMapper   // Entirely generated; no handwritten implementation is required.
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
//  Manual mapping for custom names, multiple type pairs, or specialized signatures.
//           The user declares partial signatures and the generator supplies their bodies.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Provides game object mapper functionality.</summary>
[LuminMapper]   // ← Declare the partial class and method signatures; the generator supplies method bodies.
public static partial class GameObjectMapper
{
    // Custom method names instead of PlayerDataMapper defaults.
    /// <summary>Converts the supplied input to the player representation.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>The result of the operation.</returns>
    public static partial PlayerDto ConvertPlayer(PlayerData source);
    /// <summary>Measures fill player for the configured benchmark input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="dest">The destination receiving the result.</param>
    public static partial void FillPlayer(PlayerData source, ref PlayerDto dest);

    // A shared mapper for multiple source and destination types.
    /// <summary>Measures to snapshot for the configured benchmark input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>The result of the operation.</returns>
    public static partial PlayerSnapshot ToSnapshot(PlayerData source);
    /// <summary>Measures to enemy snapshot for the configured benchmark input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <returns>The result of the operation.</returns>
    public static partial EnemySnapshot ToEnemySnapshot(EnemyData source);
    /// <summary>Measures fill enemy for the configured benchmark input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="dest">The destination receiving the result.</param>
    public static partial void FillEnemy(EnemyData source, ref EnemySnapshot dest);

    // Collection mapping.
    /// <summary>Measures batch convert for the configured benchmark input.</summary>
    /// <param name="sources">The sources used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static partial List<PlayerDto> BatchConvert(List<PlayerData> sources);

}

// ─────────────────────────────────────────────────────────────────────────────
//  Two helper types used by the tests.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Provides enemy data functionality.</summary>
[LuminPackable]
public class EnemyData {
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the damage.</summary>
    public float Damage { get; set; } }

/// <summary>Provides enemy snapshot functionality.</summary>
[LuminPackable]
public class EnemySnapshot
{
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the dmg.</summary>
    [LuminMapFromMember("Damage")]    // EnemyData.Damage → EnemySnapshot.Dmg
    public float Dmg { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  Runtime calls use equivalent APIs for both mapping paths.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Provides usage examples functionality.</summary>
class UsageExamples
{
    /// <summary>Measures demo for the configured benchmark input.</summary>
    public static void Demo()
    {
        var player = new PlayerData
        {
            Id = 1,
            Name = "Alice",
            NickName = "Ace",
            Password = "secret",
            Health = 100f,
            LevelRaw = 42,
            BirthdayStr = "1990-05-15",
            IsActive = true
        };

        // ── API generated by automatic mapping declarations. ──────────────────────────────────

        // Creating mapping: one PlayerDto allocation.
        PlayerDto dto = PlayerDataMapper.ToDto(in player);

        // In-place mapping: no allocations when reusing a pooled instance.
        PlayerDto pooled = Pool<PlayerDto>.Rent();
        PlayerDataMapper.MapIntoDto(in player, ref pooled);
        Pool<PlayerDto>.Return(pooled);

        // Collection mapping.
        PlayerDto[] arr = PlayerDataMapper.ToDtoArray(new[] { player });
        List<PlayerDto> list = PlayerDataMapper.ToDtoList(new List<PlayerData> { player });

        // A second LuminMapTo destination.
        PlayerSnapshot snap = PlayerDataMapper.ToSnapshot(in player);

        // ── API generated by manual mapping declarations. ──────────────────────────────────

        PlayerDto dto2 = GameObjectMapper.ConvertPlayer(player);
        GameObjectMapper.FillPlayer(player, ref pooled);

        // ── Dynamic dispatch when the type is determined at runtime.────────────────────────────
        // Both mapping paths register with LuminMapper and share the same entry point.
        // One O(1) generic static field read and one delegate call; no reflection or dictionary lookup.
        PlayerDto dtoD = LuminMapper.Map<PlayerData, PlayerDto>(in player);
        LuminMapper.MapInto(in player, ref pooled);

        bool ok = LuminMapper.IsRegistered<PlayerData, PlayerDto>(); // true
        Console.WriteLine(ok);
    }

    /// <summary>Provides pool functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    static class Pool<T> where T : new()
    {
        static readonly System.Collections.Concurrent.ConcurrentBag<T> _bag = new();
        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        public static T Rent() => _bag.TryTake(out var v) ? v : new T();
        /// <summary>Returns an instance to the pool for reuse.</summary>
        /// <param name="v">The v used by this operation.</param>
        public static void Return(T v) => _bag.Add(v);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  Performance characteristics.
// ═══════════════════════════════════════════════════════════════════════════
/*
  ┌─────────────────────────────────────────┬──────────┬──────────────────────┐
  │ Call path                                 │ GC allocations   │ Description                 │
  ├─────────────────────────────────────────┼──────────┼──────────────────────┤
  │ PlayerDataMapper.ToDto(in src)          │ 1× new() │ Destination class      │
  │ PlayerDataMapper.ToDto(in src) [struct] │ 0        │ Unmanaged destination           │
  │ PlayerDataMapper.MapIntoDto(in, ref)    │ 0        │ In-place write             │
  │ PlayerDataMapper.ToDtoArray(arr)        │ 1× arr   │ Result array             │
  │ LuminMapper.Map<S,D>(in src)            │ 0*       │ *Delegate allocated once during registration │
  └─────────────────────────────────────────┴──────────┴──────────────────────┘

  Generated code characteristics:
  - AggressiveInlining allows the JIT to inline generated methods.
  - .NET 8 and later use scoped in for compatible declarations.
  - Generated mappings avoid reflection, expression trees, and dictionary lookup.
  - Collection mappings use for or foreach loops without LINQ.
*/
