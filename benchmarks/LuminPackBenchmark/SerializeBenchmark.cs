using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using MemoryPack;
using MessagePack;
using Nino.Core;

namespace LuminPackBenchmark;

#nullable disable


public enum CharacterClass : int { Warrior = 0, Mage = 1, Rogue = 2, Paladin = 3, Ranger = 4 }
public enum EquipSlot    : int { Head = 0, Chest = 1, Legs = 2, Boots = 3, Weapon = 4, Offhand = 5, Ring = 6, Amulet = 7 }
public enum BuffType     : int { Strength = 0, Defense = 1, Speed = 2, Regen = 3, Critical = 4 }
public enum SkillTier    : byte { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }
public enum EventCategory : int { Combat = 0, Crafting = 1, Trade = 2, Social = 3, System = 4 }


[LuminPackable]
[MemoryPackable]
[MemoryPackUnion(0, typeof(WeaponItem))]
[MemoryPackUnion(1, typeof(ArmorItem))]
[MemoryPackUnion(2, typeof(ConsumableItem))]
[MemoryPackUnion(3, typeof(MountItem))]
[MemoryPackUnion(4, typeof(QuestItem))]
[MemoryPackUnion(5, typeof(PetItem))]
[MemoryPackUnion(6, typeof(RelicItem))]
[Union(0, typeof(WeaponItem))]
[Union(1, typeof(ArmorItem))]
[Union(2, typeof(ConsumableItem))]
[Union(3, typeof(MountItem))]
[Union(4, typeof(QuestItem))]
[Union(5, typeof(PetItem))]
[Union(6, typeof(RelicItem))]
[NinoType]
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(WeaponItem),      "WeaponItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ArmorItem),       "ArmorItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ConsumableItem),  "ConsumableItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MountItem),       "MountItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(QuestItem),       "QuestItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PetItem),         "PetItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RelicItem),       "RelicItem")]
public abstract partial class ItemBase
{
    [Key(0)] public int    ItemId;
    [Key(1)] public string ItemName;
    [Key(2)] public int    Rarity;
    [Key(3)] public long   ObtainedAt;
    [Key(4)] public bool   IsLocked;
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class WeaponItem : ItemBase
{
    [Key(5)]  public float    AttackPower;
    [Key(6)]  public float    CritRate;
    [Key(7)]  public int      Durability;
    [Key(8)]  public EquipSlot Slot;
    [Key(9)]  public string   GemSocketA;
    [Key(10)] public string   GemSocketB;

    public static WeaponItem Create(Random rng, int id) => new()
    {
        ItemId = id, ItemName = $"Weapon_{id:D3}", Rarity = rng.Next(1, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 100000),
        IsLocked = id % 4 == 0,
        AttackPower = (float)Math.Round(rng.NextDouble() * 5000, 2),
        CritRate    = (float)Math.Round(rng.NextDouble() * 0.5, 4),
        Durability  = rng.Next(0, 100),
        Slot        = rng.Next(2) == 0 ? EquipSlot.Weapon : EquipSlot.Offhand,
        GemSocketA  = rng.Next(2) == 0 ? $"Gem_{rng.Next(1, 20)}" : null,
        GemSocketB  = rng.Next(3) == 0 ? $"Gem_{rng.Next(1, 20)}" : null,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class ArmorItem : ItemBase
{
    [Key(5)] public float    Defense;
    [Key(6)] public float    MagicResist;
    [Key(7)] public int      Durability;
    [Key(8)] public EquipSlot Slot;
    [Key(9)] public int      SetId;

    public static ArmorItem Create(Random rng, int id) => new()
    {
        ItemId = id + 1000, ItemName = $"Armor_{id:D3}", Rarity = rng.Next(1, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 100000),
        IsLocked = id % 5 == 0,
        Defense     = (float)Math.Round(rng.NextDouble() * 3000, 2),
        MagicResist = (float)Math.Round(rng.NextDouble() * 2000, 2),
        Durability  = rng.Next(0, 100),
        Slot        = (EquipSlot)(id % 4),
        SetId       = rng.Next(0, 10),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class ConsumableItem : ItemBase
{
    [Key(5)] public int    Count;
    [Key(6)] public int    MaxStack;
    [Key(7)] public float  HealAmount;
    [Key(8)] public float  ManaAmount;
    [Key(9)] public string Effect;

    public static ConsumableItem Create(Random rng, int id) => new()
    {
        ItemId = id + 2000, ItemName = $"Potion_{id:D3}", Rarity = rng.Next(1, 4),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 50000),
        IsLocked = false,
        Count = rng.Next(1, 99), MaxStack = 99,
        HealAmount = (float)Math.Round(rng.NextDouble() * 1000, 1),
        ManaAmount = (float)Math.Round(rng.NextDouble() * 500, 1),
        Effect = rng.Next(2) == 0 ? "Haste" : null,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class MountItem : ItemBase
{
    [Key(5)] public float  SpeedBonus;
    [Key(6)] public bool   CanFly;
    [Key(7)] public string MountSkin;
    [Key(8)] public int    StaminaPool;

    public static MountItem Create(Random rng, int id) => new()
    {
        ItemId = id + 3000, ItemName = $"Mount_{id:D3}", Rarity = rng.Next(3, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 200000),
        IsLocked = true,
        SpeedBonus  = (float)Math.Round(rng.NextDouble() * 0.6 + 0.2, 3),
        CanFly      = rng.Next(3) == 0,
        MountSkin   = $"Skin_{rng.Next(1, 30)}",
        StaminaPool = rng.Next(100, 500),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class QuestItem : ItemBase
{
    [Key(5)] public int    QuestId;
    [Key(6)] public string ObjectiveName;
    [Key(7)] public bool   IsKeyItem;
    [Key(8)] public int    RequiredCount;

    public static QuestItem Create(Random rng, int id) => new()
    {
        ItemId = id + 4000, ItemName = $"QuestItem_{id:D3}", Rarity = 1,
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 30000),
        IsLocked = true,
        QuestId       = rng.Next(1, 200),
        ObjectiveName = $"Collect_{id}_DragonScale",
        IsKeyItem     = id % 3 == 0,
        RequiredCount = rng.Next(1, 10),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class PetItem : ItemBase
{
    [Key(5)] public string PetName;
    [Key(6)] public int    Loyalty;
    [Key(7)] public float  ExpMultiplier;
    [Key(8)] public int    PetLevel;
    [Key(9)] public bool   IsAwakened;

    public static PetItem Create(Random rng, int id) => new()
    {
        ItemId = id + 5000, ItemName = $"Pet_{id:D3}", Rarity = rng.Next(2, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 500000),
        IsLocked = rng.Next(2) == 0,
        PetName       = $"Spirit_{id}",
        Loyalty       = rng.Next(0, 100),
        ExpMultiplier = (float)Math.Round(1.0 + rng.NextDouble() * 2.0, 2),
        PetLevel      = rng.Next(1, 50),
        IsAwakened    = rng.Next(4) == 0,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class RelicItem : ItemBase
{
    [Key(5)] public int    SetId;
    [Key(6)] public float  PowerLevel;
    [Key(7)] public int    SocketCount;
    [Key(8)] public string MainStat;
    [Key(9)] public string SubStatA;
    [Key(10)] public string SubStatB;

    public static RelicItem Create(Random rng, int id) => new()
    {
        ItemId = id + 6000, ItemName = $"Relic_{id:D3}", Rarity = rng.Next(3, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 300000),
        IsLocked = rng.Next(2) == 0,
        SetId       = rng.Next(0, 20),
        PowerLevel  = (float)Math.Round(rng.NextDouble() * 100, 2),
        SocketCount = rng.Next(0, 4),
        MainStat    = $"ATK+{rng.Next(100, 999)}",
        SubStatA    = $"HP+{rng.Next(50, 500)}",
        SubStatB    = rng.Next(2) == 0 ? $"CritRate+{rng.Next(1, 10)}%" : null,
    };
}

// ────────────────────────────────────────────────────────────────────────────
// 多态层2：技能（4种子类）
// ────────────────────────────────────────────────────────────────────────────

[LuminPackable]
[MemoryPackable]
[MemoryPackUnion(0, typeof(ActiveSkill))]
[MemoryPackUnion(1, typeof(PassiveSkill))]
[MemoryPackUnion(2, typeof(UltimateSkill))]
[MemoryPackUnion(3, typeof(SupportSkill))]
[Union(0, typeof(ActiveSkill))]
[Union(1, typeof(PassiveSkill))]
[Union(2, typeof(UltimateSkill))]
[Union(3, typeof(SupportSkill))]
[NinoType]
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ActiveSkill),   "ActiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PassiveSkill),  "PassiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UltimateSkill), "UltimateSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SupportSkill),  "SupportSkill")]
public abstract partial class SkillBase
{
    [Key(0)] public int       SkillId;
    [Key(1)] public string    SkillName;
    [Key(2)] public int       Level;
    [Key(3)] public SkillTier Tier;
    [Key(4)] public bool      IsUnlocked;
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class ActiveSkill : SkillBase
{
    [Key(5)] public float  CastTime;
    [Key(6)] public float  Cooldown;
    [Key(7)] public int    ManaCost;
    [Key(8)] public string TargetType;
    [Key(9)] public float  BaseDamage;

    public static ActiveSkill Create(Random rng, int id) => new()
    {
        SkillId = id, SkillName = $"Active_{id}", Level = rng.Next(1, 10),
        Tier = (SkillTier)(id % 4), IsUnlocked = rng.Next(2) == 0,
        CastTime   = (float)Math.Round(rng.NextDouble() * 3, 2),
        Cooldown   = (float)Math.Round(rng.NextDouble() * 30, 1),
        ManaCost   = rng.Next(10, 300),
        TargetType = rng.Next(2) == 0 ? "Single" : "AOE",
        BaseDamage = (float)Math.Round(rng.NextDouble() * 5000 + 100, 2),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class PassiveSkill : SkillBase
{
    [Key(5)] public float  TriggerChance;
    [Key(6)] public string TriggerCondition;
    [Key(7)] public float  StatBonus;
    [Key(8)] public string AffectedStat;

    public static PassiveSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 100, SkillName = $"Passive_{id}", Level = rng.Next(1, 5),
        Tier = (SkillTier)(id % 4), IsUnlocked = true,
        TriggerChance    = (float)Math.Round(rng.NextDouble() * 0.5, 3),
        TriggerCondition = rng.Next(2) == 0 ? "OnHit" : "OnKill",
        StatBonus        = (float)Math.Round(rng.NextDouble() * 30, 2),
        AffectedStat     = rng.Next(2) == 0 ? "ATK" : "DEF",
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class UltimateSkill : SkillBase
{
    [Key(5)] public float  ChargeRate;
    [Key(6)] public int    ChargeMax;
    [Key(7)] public float  UltimateDamage;
    [Key(8)] public int    ActiveDuration;
    [Key(9)] public bool   HasTransformation;

    public static UltimateSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 200, SkillName = $"Ultimate_{id}", Level = rng.Next(1, 3),
        Tier = SkillTier.Legendary, IsUnlocked = rng.Next(3) == 0,
        ChargeRate        = (float)Math.Round(rng.NextDouble() * 10 + 1, 2),
        ChargeMax         = rng.Next(50, 200),
        UltimateDamage    = (float)Math.Round(rng.NextDouble() * 20000 + 5000, 2),
        ActiveDuration    = rng.Next(5, 30),
        HasTransformation = rng.Next(2) == 0,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class SupportSkill : SkillBase
{
    [Key(5)] public float  HealPower;
    [Key(6)] public float  ShieldAmount;
    [Key(7)] public int    BuffDuration;
    [Key(8)] public string AppliedBuff;

    public static SupportSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 300, SkillName = $"Support_{id}", Level = rng.Next(1, 8),
        Tier = (SkillTier)(id % 3), IsUnlocked = rng.Next(2) == 0,
        HealPower    = (float)Math.Round(rng.NextDouble() * 3000, 2),
        ShieldAmount = (float)Math.Round(rng.NextDouble() * 2000, 2),
        BuffDuration = rng.Next(5, 60),
        AppliedBuff  = rng.Next(2) == 0 ? "Regeneration" : "Barrier",
    };
}

// ────────────────────────────────────────────────────────────────────────────
// 多态层3：事件日志（4种子类）
// ────────────────────────────────────────────────────────────────────────────

[LuminPackable]
[MemoryPackable]
[MemoryPackUnion(0, typeof(CombatEventLog))]
[MemoryPackUnion(1, typeof(CraftingEventLog))]
[MemoryPackUnion(2, typeof(TradeEventLog))]
[MemoryPackUnion(3, typeof(AchievementEventLog))]
[Union(0, typeof(CombatEventLog))]
[Union(1, typeof(CraftingEventLog))]
[Union(2, typeof(TradeEventLog))]
[Union(3, typeof(AchievementEventLog))]
[NinoType]
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CombatEventLog),      "CombatEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CraftingEventLog),     "CraftingEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TradeEventLog),        "TradeEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AchievementEventLog),  "AchievementEventLog")]
public abstract partial class EventLogBase
{
    [Key(0)] public long          Timestamp;
    [Key(1)] public EventCategory Category;
    [Key(2)] public string        Summary;
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class CombatEventLog : EventLogBase
{
    [Key(3)] public int    TargetId;
    [Key(4)] public int    DamageDealt;
    [Key(5)] public bool   IsCritical;
    [Key(6)] public string SkillUsed;
    [Key(7)] public bool   IsKillingBlow;

    public static CombatEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category  = EventCategory.Combat,
        Summary   = $"Combat with Enemy_{id}",
        TargetId  = rng.Next(1, 9999),
        DamageDealt    = rng.Next(100, 99999),
        IsCritical     = rng.Next(4) == 0,
        SkillUsed      = $"Skill_{rng.Next(1, 50)}",
        IsKillingBlow  = rng.Next(3) == 0,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class CraftingEventLog : EventLogBase
{
    [Key(3)] public int    RecipeId;
    [Key(4)] public string ItemName;
    [Key(5)] public bool   Success;
    [Key(6)] public int    MaterialsUsed;

    public static CraftingEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category  = EventCategory.Crafting,
        Summary   = $"Crafted item #{id}",
        RecipeId      = rng.Next(1, 500),
        ItemName      = $"CraftedItem_{id}",
        Success       = rng.Next(5) != 0,
        MaterialsUsed = rng.Next(1, 10),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class TradeEventLog : EventLogBase
{
    [Key(3)] public int    PartnerId;
    [Key(4)] public long   GoldAmount;
    [Key(5)] public int[]  ItemIds;
    [Key(6)] public bool   IsPlayerInitiated;

    public static TradeEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category  = EventCategory.Trade,
        Summary   = $"Trade with Player_{id}",
        PartnerId         = rng.Next(100000, 999999),
        GoldAmount        = rng.Next(0, 100000),
        ItemIds           = Enumerable.Range(0, rng.Next(1, 5)).Select(_ => rng.Next(1, 9999)).ToArray(),
        IsPlayerInitiated = rng.Next(2) == 0,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class AchievementEventLog : EventLogBase
{
    [Key(3)] public int    AchievementId;
    [Key(4)] public string AchievementName;
    [Key(5)] public int    RewardGold;
    [Key(6)] public int    RewardDiamond;

    public static AchievementEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category  = EventCategory.System,
        Summary   = $"Achievement unlocked #{id}",
        AchievementId   = rng.Next(1, 1000),
        AchievementName = $"Achievement_{id}_Complete",
        RewardGold      = rng.Next(0, 10000),
        RewardDiamond   = rng.Next(0, 100),
    };
}

// ────────────────────────────────────────────────────────────────────────────
// 泛型：Envelope<T>（用于包装快照数据）
// ────────────────────────────────────────────────────────────────────────────

[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class Envelope<T>
{
    [Key(0)] public long   Timestamp;
    [Key(1)] public string Tag;
    [Key(2)] public int    Version;
    [Key(3)] public T      Payload;
}

// ────────────────────────────────────────────────────────────────────────────
// 非多态辅助类型
// ────────────────────────────────────────────────────────────────────────────

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class ActiveBuff
{
    [Key(0)] public BuffType Type;
    [Key(1)] public float    Value;
    [Key(2)] public int      RemainSeconds;
    [Key(3)] public string   Source;
    [Key(4)] public bool     IsDebuff;

    public static ActiveBuff Create(Random rng, int id) => new()
    {
        Type = (BuffType)(id % 5), Value = (float)Math.Round(rng.NextDouble() * 500, 2),
        RemainSeconds = rng.Next(5, 3600),
        Source  = $"Skill_{rng.Next(1, 50)}",
        IsDebuff = id % 7 == 0,
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class CharacterStats
{
    [Key(0)] public int   Hp;
    [Key(1)] public int   MaxHp;
    [Key(2)] public int   Mp;
    [Key(3)] public int   MaxMp;
    [Key(4)] public float Attack;
    [Key(5)] public float Defense;
    [Key(6)] public float Speed;
    [Key(7)] public float CritRate;
    [Key(8)] public float CritDamage;
    [Key(9)] public int   Level;

    public static CharacterStats Create(Random rng) => new()
    {
        Hp = rng.Next(500, 50000), MaxHp = 50000,
        Mp = rng.Next(0, 10000),  MaxMp = 10000,
        Attack     = (float)Math.Round(rng.NextDouble() * 8000 + 500, 2),
        Defense    = (float)Math.Round(rng.NextDouble() * 4000 + 200, 2),
        Speed      = (float)Math.Round(rng.NextDouble() * 300 + 100, 2),
        CritRate   = (float)Math.Round(rng.NextDouble() * 0.8, 4),
        CritDamage = (float)Math.Round(rng.NextDouble() * 3.0 + 1.5, 4),
        Level      = rng.Next(1, 200),
    };
}

[LuminPackable] [MemoryPackable] [MessagePackObject] [NinoType]
public sealed partial class GuildInfo
{
    [Key(0)] public int    GuildId;
    [Key(1)] public string GuildName;
    [Key(2)] public string GuildTag;
    [Key(3)] public int    GuildLevel;
    [Key(4)] public long   GuildExp;
    [Key(5)] public int    MemberCount;
    [Key(6)] public int    MaxMemberCount;
    [Key(7)] public string GuildNotice;
    [Key(8)] public bool   IsPublic;
    [Key(9)] public long   CreatedAt;

    public static GuildInfo Create(Random rng) => new()
    {
        GuildId = rng.Next(1, 99999), GuildName = "LegendaryGuild", GuildTag = "LGD",
        GuildLevel = rng.Next(1, 30), GuildExp = rng.Next(0, 10_000_000),
        MemberCount = rng.Next(1, 100), MaxMemberCount = 100,
        GuildNotice = "Welcome to LegendaryGuild! Guild events every Friday.",
        IsPublic = true,
        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(100000, 5000000),
    };
}

// ────────────────────────────────────────────────────────────────────────────
// 顶层角色存档：三条多态集合 + 泛型快照 + 原有字段
// ────────────────────────────────────────────────────────────────────────────

[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class CharacterSaveData
{
    // 基础字段
    [Key(0)]  public int            CharacterId;
    [Key(1)]  public string         CharacterName;
    [Key(2)]  public CharacterClass Class;
    [Key(3)]  public int            ServerRegion;
    [Key(4)]  public long           TotalPlayTime;
    [Key(5)]  public long           LastSaveAt;
    [Key(6)]  public bool           IsOnline;
    [Key(7)]  public bool           IsDeleted;
    [Key(8)]  public int            VipLevel;
    [Key(9)] public long           DiamondCount;
    [Key(10)] public long           CoinCount;
    [Key(11)] public int            PvpRating;
    [Key(12)] public int            PveRating;
    [Key(13)] public string         HomeRegion;
    [Key(14)]  public string         Title;

    // 嵌套对象
    [Key(15)] public CharacterStats Stats;
    [Key(16)] public GuildInfo      Guild;

    // 泛型快照（Envelope<T>）
    [Key(17)] public Envelope<CharacterStats> StatsSnapshot;
    [Key(18)] public Envelope<GuildInfo>      GuildSnapshot;

    // 多态集合1：主背包（7种子类，40条）
    [Key(19)] public List<ItemBase> Bag;

    // 多态集合2：仓库（7种子类，25条）
    [Key(20)] public List<ItemBase> Storage;

    // 多态集合3：技能列表（4种子类，20条）
    [Key(21)] public List<SkillBase> Skills;

    // 多态集合4：近期事件日志（4种子类，30条）
    [Key(22)] public List<EventLogBase> RecentEvents;

    // 非多态集合
    [Key(23)] public ActiveBuff[]             ActiveBuffs;
    [Key(24)] public Dictionary<int, int>     SkillLevels;
    [Key(25)] public Dictionary<string, long> Records;
    [Key(26)] public List<string>             UnlockedTitles;

    public static CharacterSaveData Create()
    {
        var rng = new Random(42);

        // 主背包 40 条：7种子类
        var bag = new List<ItemBase>();
        for (int i = 1; i <= 8;  i++) bag.Add(WeaponItem.Create(rng, i));
        for (int i = 1; i <= 10; i++) bag.Add(ArmorItem.Create(rng, i));
        for (int i = 1; i <= 10; i++) bag.Add(ConsumableItem.Create(rng, i));
        for (int i = 1; i <= 4;  i++) bag.Add(MountItem.Create(rng, i));
        for (int i = 1; i <= 3;  i++) bag.Add(QuestItem.Create(rng, i));
        for (int i = 1; i <= 3;  i++) bag.Add(PetItem.Create(rng, i));
        for (int i = 1; i <= 2;  i++) bag.Add(RelicItem.Create(rng, i));

        // 仓库 25 条：以 Armor/Relic/Pet 为主
        var storage = new List<ItemBase>();
        for (int i = 1; i <= 8;  i++) storage.Add(ArmorItem.Create(rng, i + 10));
        for (int i = 1; i <= 6;  i++) storage.Add(RelicItem.Create(rng, i));
        for (int i = 1; i <= 5;  i++) storage.Add(PetItem.Create(rng, i + 3));
        for (int i = 1; i <= 4;  i++) storage.Add(WeaponItem.Create(rng, i + 10));
        for (int i = 1; i <= 2;  i++) storage.Add(QuestItem.Create(rng, i + 3));

        // 技能 20 条：4种子类
        var skills = new List<SkillBase>();
        for (int i = 1; i <= 8;  i++) skills.Add(ActiveSkill.Create(rng, i));
        for (int i = 1; i <= 6;  i++) skills.Add(PassiveSkill.Create(rng, i));
        for (int i = 1; i <= 3;  i++) skills.Add(UltimateSkill.Create(rng, i));
        for (int i = 1; i <= 3;  i++) skills.Add(SupportSkill.Create(rng, i));

        // 事件日志 30 条：4种子类
        var events = new List<EventLogBase>();
        for (int i = 1; i <= 12; i++) events.Add(CombatEventLog.Create(rng, i));
        for (int i = 1; i <= 8;  i++) events.Add(CraftingEventLog.Create(rng, i));
        for (int i = 1; i <= 6;  i++) events.Add(TradeEventLog.Create(rng, i));
        for (int i = 1; i <= 4;  i++) events.Add(AchievementEventLog.Create(rng, i));

        var stats = CharacterStats.Create(rng);
        var guild = GuildInfo.Create(rng);

        return new CharacterSaveData
        {
            CharacterId   = rng.Next(100000, 999999),
            CharacterName = "DragonSlayer_X",
            Class         = CharacterClass.Paladin,
            ServerRegion  = 3,
            TotalPlayTime = rng.Next(0, 10_000_000),
            LastSaveAt    = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IsOnline      = true,
            IsDeleted     = false,
            Title         = "Conqueror of the Abyss",
            VipLevel      = rng.Next(0, 15),
            DiamondCount  = rng.Next(0, 100000),
            CoinCount     = rng.Next(0, 10_000_000),
            PvpRating     = rng.Next(1000, 3000),
            PveRating     = rng.Next(500, 5000),
            HomeRegion    = "Asia-East-01",
            Stats         = stats,
            Guild         = guild,
            StatsSnapshot = new Envelope<CharacterStats>
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600,
                Tag       = "hourly_snapshot",
                Version   = 3,
                Payload   = CharacterStats.Create(rng),
            },
            GuildSnapshot = new Envelope<GuildInfo>
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7200,
                Tag       = "guild_snapshot",
                Version   = 1,
                Payload   = GuildInfo.Create(rng),
            },
            Bag          = bag,
            Storage      = storage,
            Skills       = skills,
            RecentEvents = events,
            ActiveBuffs  = Enumerable.Range(1, 100).Select(i => ActiveBuff.Create(rng, i)).ToArray(),
            SkillLevels  = Enumerable.Range(1, 100).ToDictionary(i => i, i => rng.Next(1, 10)),
            Records      = Enumerable.Range(1, 100).ToDictionary(
                               i => $"RECORD_{i:D3}",
                               i => (long)i),
            UnlockedTitles = Enumerable.Range(1, 16).Select(i => $"Title_{i:D2}_Master").ToList(),
        };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Benchmark
// ────────────────────────────────────────────────────────────────────────────

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer] 
public class SerializeBenchmark
{
    private CharacterSaveData _data = CharacterSaveData.Create();

    private LuminBufferWriter _luminWriter = LuminBufferWriterPool.Rent();

    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter _memoryWriter =
        MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();

    private NinoArrayBufferWriter _ninoWriter = new NinoArrayBufferWriter(1024 * 256);
    private ArrayBufferWriter<byte> _msgpackWriter = new ArrayBufferWriter<byte>(1024 * 256);

    private byte[] _luminBuffer;
    private byte[] _memoryBuffer;
    private byte[] _msgpackBuffer;
    private byte[] _ninoBuffer;

    private LuminPackSerializerOption luminPackSerializerOption = new LuminPackSerializerOption()
    {
        StringEncoding = LuminPackStringEncoding.UTF16,
        StringRecording = LuminPackStringRecording.Length
    };

    private MemoryPackSerializerOptions memoryPackSerializerOption = new MemoryPackSerializerOptions()
    {
        StringEncoding = StringEncoding.Utf16
    };

[GlobalSetup]
    public void SetUp()
    {
        _luminBuffer   = LuminPackSerializer.Serialize(_data, luminPackSerializerOption);
        _memoryBuffer  = MemoryPackSerializer.Serialize(_data, memoryPackSerializerOption);
        _msgpackBuffer = MessagePackSerializer.Serialize(_data);
        _ninoBuffer    = NinoSerializer.Serialize(_data);
    }

    [GlobalCleanup]
    public void CleanUp() => _luminWriter.Dispose();

    [Benchmark(Baseline = true)]
    public void LuminPackSerialize()
        => LuminPackSerializer.Serialize(_data, _luminWriter, luminPackSerializerOption); 

    [Benchmark]
    public void MemoryPackSerialize()
    {
        _memoryWriter.Reset();
        MemoryPackSerializer.Serialize(_memoryWriter, _data, memoryPackSerializerOption);
    }

    [Benchmark]
    public void MessagePackSerialize()
    {
        _msgpackWriter.ResetWrittenCount();
        MessagePackSerializer.Serialize(_msgpackWriter, _data);
    }

    [Benchmark]
    public void NinoSerialize()
    {
        _ninoWriter.ResetWrittenCount();
        NinoSerializer.Serialize(_data, _ninoWriter);
    }

    [Benchmark]
    public CharacterSaveData LuminPackDeserialize()
        => LuminPackSerializer.Deserialize<CharacterSaveData>(_luminBuffer, luminPackSerializerOption);

    [Benchmark]
    public CharacterSaveData MemoryPackDeserialize()
        => MemoryPackSerializer.Deserialize<CharacterSaveData>(_memoryBuffer, memoryPackSerializerOption);

    [Benchmark]
    public CharacterSaveData MessagePackDeserialize()
        => MessagePackSerializer.Deserialize<CharacterSaveData>(_msgpackBuffer);

    [Benchmark]
    public CharacterSaveData NinoDeserialize()
        => NinoDeserializer.Deserialize<CharacterSaveData>(_ninoBuffer);
}