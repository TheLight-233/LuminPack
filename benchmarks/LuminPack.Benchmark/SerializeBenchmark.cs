using System.Buffers;
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


/// <summary>Provides character class functionality.</summary>
public enum CharacterClass : int {
/// <summary>Specifies warrior.</summary>
Warrior = 0,
/// <summary>Specifies mage.</summary>
Mage = 1,
/// <summary>Specifies rogue.</summary>
Rogue = 2,
/// <summary>Specifies paladin.</summary>
Paladin = 3,
/// <summary>Specifies ranger.</summary>
Ranger = 4 }
/// <summary>Provides equip slot functionality.</summary>
public enum EquipSlot : int {
/// <summary>Specifies head.</summary>
Head = 0,
/// <summary>Specifies chest.</summary>
Chest = 1,
/// <summary>Specifies legs.</summary>
Legs = 2,
/// <summary>Specifies boots.</summary>
Boots = 3,
/// <summary>Specifies weapon.</summary>
Weapon = 4,
/// <summary>Specifies offhand.</summary>
Offhand = 5,
/// <summary>Specifies ring.</summary>
Ring = 6,
/// <summary>Specifies amulet.</summary>
Amulet = 7 }
/// <summary>Provides buff type functionality.</summary>
public enum BuffType : int {
/// <summary>Specifies strength.</summary>
Strength = 0,
/// <summary>Specifies defense.</summary>
Defense = 1,
/// <summary>Specifies speed.</summary>
Speed = 2,
/// <summary>Specifies regen.</summary>
Regen = 3,
/// <summary>Specifies critical.</summary>
Critical = 4 }
/// <summary>Provides skill tier functionality.</summary>
public enum SkillTier : byte {
/// <summary>Specifies common.</summary>
Common = 0,
/// <summary>Specifies rare.</summary>
Rare = 1,
/// <summary>Specifies epic.</summary>
Epic = 2,
/// <summary>Specifies legendary.</summary>
Legendary = 3 }
/// <summary>Provides event category functionality.</summary>
public enum EventCategory : int {
/// <summary>Specifies combat.</summary>
Combat = 0,
/// <summary>Specifies crafting.</summary>
Crafting = 1,
/// <summary>Specifies trade.</summary>
Trade = 2,
/// <summary>Specifies social.</summary>
Social = 3,
/// <summary>Specifies system.</summary>
System = 4 }


/// <summary>Provides item base functionality.</summary>
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
[System.Text.Json.Serialization.JsonDerivedType(typeof(WeaponItem), "WeaponItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ArmorItem), "ArmorItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ConsumableItem), "ConsumableItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MountItem), "MountItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(QuestItem), "QuestItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PetItem), "PetItem")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(RelicItem), "RelicItem")]
public abstract partial class ItemBase
{
    /// <summary>Stores the item id.</summary>
    [Key(0)] public int ItemId;
    /// <summary>Stores the item name.</summary>
    [Key(1)] public string ItemName;
    /// <summary>Stores the rarity.</summary>
    [Key(2)] public int Rarity;
    /// <summary>Stores the obtained at.</summary>
    [Key(3)] public long ObtainedAt;
    /// <summary>Stores the is locked.</summary>
    [Key(4)] public bool IsLocked;
}

/// <summary>Provides weapon item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class WeaponItem : ItemBase
{
    /// <summary>Stores the attack power.</summary>
    [Key(5)] public float AttackPower;
    /// <summary>Stores the crit rate.</summary>
    [Key(6)] public float CritRate;
    /// <summary>Stores the durability.</summary>
    [Key(7)] public int Durability;
    /// <summary>Stores the slot.</summary>
    [Key(8)] public EquipSlot Slot;
    /// <summary>Stores the gem socket a.</summary>
    [Key(9)] public string GemSocketA;
    /// <summary>Stores the gem socket b.</summary>
    [Key(10)] public string GemSocketB;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static WeaponItem Create(Random rng, int id) => new()
    {
        ItemId = id,
        ItemName = $"Weapon_{id:D3}",
        Rarity = rng.Next(1, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 100000),
        IsLocked = id % 4 == 0,
        AttackPower = (float)Math.Round(rng.NextDouble() * 5000, 2),
        CritRate = (float)Math.Round(rng.NextDouble() * 0.5, 4),
        Durability = rng.Next(0, 100),
        Slot = rng.Next(2) == 0 ? EquipSlot.Weapon : EquipSlot.Offhand,
        GemSocketA = rng.Next(2) == 0 ? $"Gem_{rng.Next(1, 20)}" : null,
        GemSocketB = rng.Next(3) == 0 ? $"Gem_{rng.Next(1, 20)}" : null,
    };
}

/// <summary>Provides armor item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class ArmorItem : ItemBase
{
    /// <summary>Stores the defense.</summary>
    [Key(5)] public float Defense;
    /// <summary>Stores the magic resist.</summary>
    [Key(6)] public float MagicResist;
    /// <summary>Stores the durability.</summary>
    [Key(7)] public int Durability;
    /// <summary>Stores the slot.</summary>
    [Key(8)] public EquipSlot Slot;
    /// <summary>Stores the set id.</summary>
    [Key(9)] public int SetId;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ArmorItem Create(Random rng, int id) => new()
    {
        ItemId = id + 1000,
        ItemName = $"Armor_{id:D3}",
        Rarity = rng.Next(1, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 100000),
        IsLocked = id % 5 == 0,
        Defense = (float)Math.Round(rng.NextDouble() * 3000, 2),
        MagicResist = (float)Math.Round(rng.NextDouble() * 2000, 2),
        Durability = rng.Next(0, 100),
        Slot = (EquipSlot)(id % 4),
        SetId = rng.Next(0, 10),
    };
}

/// <summary>Provides consumable item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class ConsumableItem : ItemBase
{
    /// <summary>Stores the count.</summary>
    [Key(5)] public int Count;
    /// <summary>Stores the max stack.</summary>
    [Key(6)] public int MaxStack;
    /// <summary>Stores the heal amount.</summary>
    [Key(7)] public float HealAmount;
    /// <summary>Stores the mana amount.</summary>
    [Key(8)] public float ManaAmount;
    /// <summary>Stores the effect.</summary>
    [Key(9)] public string Effect;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ConsumableItem Create(Random rng, int id) => new()
    {
        ItemId = id + 2000,
        ItemName = $"Potion_{id:D3}",
        Rarity = rng.Next(1, 4),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 50000),
        IsLocked = false,
        Count = rng.Next(1, 99),
        MaxStack = 99,
        HealAmount = (float)Math.Round(rng.NextDouble() * 1000, 1),
        ManaAmount = (float)Math.Round(rng.NextDouble() * 500, 1),
        Effect = rng.Next(2) == 0 ? "Haste" : null,
    };
}

/// <summary>Provides mount item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class MountItem : ItemBase
{
    /// <summary>Stores the speed bonus.</summary>
    [Key(5)] public float SpeedBonus;
    /// <summary>Stores the can fly.</summary>
    [Key(6)] public bool CanFly;
    /// <summary>Stores the mount skin.</summary>
    [Key(7)] public string MountSkin;
    /// <summary>Stores the stamina pool.</summary>
    [Key(8)] public int StaminaPool;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MountItem Create(Random rng, int id) => new()
    {
        ItemId = id + 3000,
        ItemName = $"Mount_{id:D3}",
        Rarity = rng.Next(3, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 200000),
        IsLocked = true,
        SpeedBonus = (float)Math.Round(rng.NextDouble() * 0.6 + 0.2, 3),
        CanFly = rng.Next(3) == 0,
        MountSkin = $"Skin_{rng.Next(1, 30)}",
        StaminaPool = rng.Next(100, 500),
    };
}

/// <summary>Provides quest item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class QuestItem : ItemBase
{
    /// <summary>Stores the quest id.</summary>
    [Key(5)] public int QuestId;
    /// <summary>Stores the objective name.</summary>
    [Key(6)] public string ObjectiveName;
    /// <summary>Stores the is key item.</summary>
    [Key(7)] public bool IsKeyItem;
    /// <summary>Stores the required count.</summary>
    [Key(8)] public int RequiredCount;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static QuestItem Create(Random rng, int id) => new()
    {
        ItemId = id + 4000,
        ItemName = $"QuestItem_{id:D3}",
        Rarity = 1,
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 30000),
        IsLocked = true,
        QuestId = rng.Next(1, 200),
        ObjectiveName = $"Collect_{id}_DragonScale",
        IsKeyItem = id % 3 == 0,
        RequiredCount = rng.Next(1, 10),
    };
}

/// <summary>Provides pet item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class PetItem : ItemBase
{
    /// <summary>Stores the pet name.</summary>
    [Key(5)] public string PetName;
    /// <summary>Stores the loyalty.</summary>
    [Key(6)] public int Loyalty;
    /// <summary>Stores the exp multiplier.</summary>
    [Key(7)] public float ExpMultiplier;
    /// <summary>Stores the pet level.</summary>
    [Key(8)] public int PetLevel;
    /// <summary>Stores the is awakened.</summary>
    [Key(9)] public bool IsAwakened;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static PetItem Create(Random rng, int id) => new()
    {
        ItemId = id + 5000,
        ItemName = $"Pet_{id:D3}",
        Rarity = rng.Next(2, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 500000),
        IsLocked = rng.Next(2) == 0,
        PetName = $"Spirit_{id}",
        Loyalty = rng.Next(0, 100),
        ExpMultiplier = (float)Math.Round(1.0 + rng.NextDouble() * 2.0, 2),
        PetLevel = rng.Next(1, 50),
        IsAwakened = rng.Next(4) == 0,
    };
}

/// <summary>Provides relic item functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class RelicItem : ItemBase
{
    /// <summary>Stores the set id.</summary>
    [Key(5)] public int SetId;
    /// <summary>Stores the power level.</summary>
    [Key(6)] public float PowerLevel;
    /// <summary>Stores the socket count.</summary>
    [Key(7)] public int SocketCount;
    /// <summary>Stores the main stat.</summary>
    [Key(8)] public string MainStat;
    /// <summary>Stores the sub stat a.</summary>
    [Key(9)] public string SubStatA;
    /// <summary>Stores the sub stat b.</summary>
    [Key(10)] public string SubStatB;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static RelicItem Create(Random rng, int id) => new()
    {
        ItemId = id + 6000,
        ItemName = $"Relic_{id:D3}",
        Rarity = rng.Next(3, 6),
        ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 300000),
        IsLocked = rng.Next(2) == 0,
        SetId = rng.Next(0, 20),
        PowerLevel = (float)Math.Round(rng.NextDouble() * 100, 2),
        SocketCount = rng.Next(0, 4),
        MainStat = $"ATK+{rng.Next(100, 999)}",
        SubStatA = $"HP+{rng.Next(50, 500)}",
        SubStatB = rng.Next(2) == 0 ? $"CritRate+{rng.Next(1, 10)}%" : null,
    };
}

// ────────────────────────────────────────────────────────────────────────────
// Polymorphism level 2: skills with four derived types.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides skill base functionality.</summary>
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
[System.Text.Json.Serialization.JsonDerivedType(typeof(ActiveSkill), "ActiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PassiveSkill), "PassiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UltimateSkill), "UltimateSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SupportSkill), "SupportSkill")]
public abstract partial class SkillBase
{
    /// <summary>Stores the skill id.</summary>
    [Key(0)] public int SkillId;
    /// <summary>Stores the skill name.</summary>
    [Key(1)] public string SkillName;
    /// <summary>Stores the level.</summary>
    [Key(2)] public int Level;
    /// <summary>Stores the tier.</summary>
    [Key(3)] public SkillTier Tier;
    /// <summary>Stores the is unlocked.</summary>
    [Key(4)] public bool IsUnlocked;
}

/// <summary>Provides active skill functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class ActiveSkill : SkillBase
{
    /// <summary>Stores the cast time.</summary>
    [Key(5)] public float CastTime;
    /// <summary>Stores the cooldown.</summary>
    [Key(6)] public float Cooldown;
    /// <summary>Stores the mana cost.</summary>
    [Key(7)] public int ManaCost;
    /// <summary>Stores the target type.</summary>
    [Key(8)] public string TargetType;
    /// <summary>Stores the base damage.</summary>
    [Key(9)] public float BaseDamage;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ActiveSkill Create(Random rng, int id) => new()
    {
        SkillId = id,
        SkillName = $"Active_{id}",
        Level = rng.Next(1, 10),
        Tier = (SkillTier)(id % 4),
        IsUnlocked = rng.Next(2) == 0,
        CastTime = (float)Math.Round(rng.NextDouble() * 3, 2),
        Cooldown = (float)Math.Round(rng.NextDouble() * 30, 1),
        ManaCost = rng.Next(10, 300),
        TargetType = rng.Next(2) == 0 ? "Single" : "AOE",
        BaseDamage = (float)Math.Round(rng.NextDouble() * 5000 + 100, 2),
    };
}

/// <summary>Provides passive skill functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class PassiveSkill : SkillBase
{
    /// <summary>Stores the trigger chance.</summary>
    [Key(5)] public float TriggerChance;
    /// <summary>Stores the trigger condition.</summary>
    [Key(6)] public string TriggerCondition;
    /// <summary>Stores the stat bonus.</summary>
    [Key(7)] public float StatBonus;
    /// <summary>Stores the affected stat.</summary>
    [Key(8)] public string AffectedStat;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static PassiveSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 100,
        SkillName = $"Passive_{id}",
        Level = rng.Next(1, 5),
        Tier = (SkillTier)(id % 4),
        IsUnlocked = true,
        TriggerChance = (float)Math.Round(rng.NextDouble() * 0.5, 3),
        TriggerCondition = rng.Next(2) == 0 ? "OnHit" : "OnKill",
        StatBonus = (float)Math.Round(rng.NextDouble() * 30, 2),
        AffectedStat = rng.Next(2) == 0 ? "ATK" : "DEF",
    };
}

/// <summary>Provides ultimate skill functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class UltimateSkill : SkillBase
{
    /// <summary>Stores the charge rate.</summary>
    [Key(5)] public float ChargeRate;
    /// <summary>Stores the charge max.</summary>
    [Key(6)] public int ChargeMax;
    /// <summary>Stores the ultimate damage.</summary>
    [Key(7)] public float UltimateDamage;
    /// <summary>Stores the active duration.</summary>
    [Key(8)] public int ActiveDuration;
    /// <summary>Stores the has transformation.</summary>
    [Key(9)] public bool HasTransformation;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static UltimateSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 200,
        SkillName = $"Ultimate_{id}",
        Level = rng.Next(1, 3),
        Tier = SkillTier.Legendary,
        IsUnlocked = rng.Next(3) == 0,
        ChargeRate = (float)Math.Round(rng.NextDouble() * 10 + 1, 2),
        ChargeMax = rng.Next(50, 200),
        UltimateDamage = (float)Math.Round(rng.NextDouble() * 20000 + 5000, 2),
        ActiveDuration = rng.Next(5, 30),
        HasTransformation = rng.Next(2) == 0,
    };
}

/// <summary>Provides support skill functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class SupportSkill : SkillBase
{
    /// <summary>Stores the heal power.</summary>
    [Key(5)] public float HealPower;
    /// <summary>Stores the shield amount.</summary>
    [Key(6)] public float ShieldAmount;
    /// <summary>Stores the buff duration.</summary>
    [Key(7)] public int BuffDuration;
    /// <summary>Stores the applied buff.</summary>
    [Key(8)] public string AppliedBuff;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static SupportSkill Create(Random rng, int id) => new()
    {
        SkillId = id + 300,
        SkillName = $"Support_{id}",
        Level = rng.Next(1, 8),
        Tier = (SkillTier)(id % 3),
        IsUnlocked = rng.Next(2) == 0,
        HealPower = (float)Math.Round(rng.NextDouble() * 3000, 2),
        ShieldAmount = (float)Math.Round(rng.NextDouble() * 2000, 2),
        BuffDuration = rng.Next(5, 60),
        AppliedBuff = rng.Next(2) == 0 ? "Regeneration" : "Barrier",
    };
}

// ────────────────────────────────────────────────────────────────────────────
// Polymorphism level 3: event logs with four derived types.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides event log base functionality.</summary>
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
[System.Text.Json.Serialization.JsonDerivedType(typeof(CombatEventLog), "CombatEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CraftingEventLog), "CraftingEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TradeEventLog), "TradeEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AchievementEventLog), "AchievementEventLog")]
public abstract partial class EventLogBase
{
    /// <summary>Stores the timestamp.</summary>
    [Key(0)] public long Timestamp;
    /// <summary>Stores the category.</summary>
    [Key(1)] public EventCategory Category;
    /// <summary>Stores the summary.</summary>
    [Key(2)] public string Summary;
}

/// <summary>Provides combat event log functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class CombatEventLog : EventLogBase
{
    /// <summary>Stores the target id.</summary>
    [Key(3)] public int TargetId;
    /// <summary>Stores the damage dealt.</summary>
    [Key(4)] public int DamageDealt;
    /// <summary>Stores the is critical.</summary>
    [Key(5)] public bool IsCritical;
    /// <summary>Stores the skill used.</summary>
    [Key(6)] public string SkillUsed;
    /// <summary>Stores the is killing blow.</summary>
    [Key(7)] public bool IsKillingBlow;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CombatEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category = EventCategory.Combat,
        Summary = $"Combat with Enemy_{id}",
        TargetId = rng.Next(1, 9999),
        DamageDealt = rng.Next(100, 99999),
        IsCritical = rng.Next(4) == 0,
        SkillUsed = $"Skill_{rng.Next(1, 50)}",
        IsKillingBlow = rng.Next(3) == 0,
    };
}

/// <summary>Provides crafting event log functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class CraftingEventLog : EventLogBase
{
    /// <summary>Stores the recipe id.</summary>
    [Key(3)] public int RecipeId;
    /// <summary>Stores the item name.</summary>
    [Key(4)] public string ItemName;
    /// <summary>Stores the success.</summary>
    [Key(5)] public bool Success;
    /// <summary>Stores the materials used.</summary>
    [Key(6)] public int MaterialsUsed;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CraftingEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category = EventCategory.Crafting,
        Summary = $"Crafted item #{id}",
        RecipeId = rng.Next(1, 500),
        ItemName = $"CraftedItem_{id}",
        Success = rng.Next(5) != 0,
        MaterialsUsed = rng.Next(1, 10),
    };
}

/// <summary>Provides trade event log functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class TradeEventLog : EventLogBase
{
    /// <summary>Stores the partner id.</summary>
    [Key(3)] public int PartnerId;
    /// <summary>Stores the gold amount.</summary>
    [Key(4)] public long GoldAmount;
    /// <summary>Stores the item ids.</summary>
    [Key(5)] public int[] ItemIds;
    /// <summary>Stores the is player initiated.</summary>
    [Key(6)] public bool IsPlayerInitiated;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TradeEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category = EventCategory.Trade,
        Summary = $"Trade with Player_{id}",
        PartnerId = rng.Next(100000, 999999),
        GoldAmount = rng.Next(0, 100000),
        ItemIds = Enumerable.Range(0, rng.Next(1, 5)).Select(_ => rng.Next(1, 9999)).ToArray(),
        IsPlayerInitiated = rng.Next(2) == 0,
    };
}

/// <summary>Provides achievement event log functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class AchievementEventLog : EventLogBase
{
    /// <summary>Stores the achievement id.</summary>
    [Key(3)] public int AchievementId;
    /// <summary>Stores the achievement name.</summary>
    [Key(4)] public string AchievementName;
    /// <summary>Stores the reward gold.</summary>
    [Key(5)] public int RewardGold;
    /// <summary>Stores the reward diamond.</summary>
    [Key(6)] public int RewardDiamond;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static AchievementEventLog Create(Random rng, int id) => new()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(0, 86400),
        Category = EventCategory.System,
        Summary = $"Achievement unlocked #{id}",
        AchievementId = rng.Next(1, 1000),
        AchievementName = $"Achievement_{id}_Complete",
        RewardGold = rng.Next(0, 10000),
        RewardDiamond = rng.Next(0, 100),
    };
}

// ────────────────────────────────────────────────────────────────────────────
// Envelope<T> wraps snapshot data.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides envelope functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class Envelope<T>
{
    /// <summary>Stores the timestamp.</summary>
    [Key(0)] public long Timestamp;
    /// <summary>Stores the tag.</summary>
    [Key(1)] public string Tag;
    /// <summary>Stores the version.</summary>
    [Key(2)] public int Version;
    /// <summary>Stores the payload.</summary>
    [Key(3)] public T Payload;
}

// ────────────────────────────────────────────────────────────────────────────
// Non-polymorphic helper types.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides active buff functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class ActiveBuff
{
    /// <summary>Stores the type.</summary>
    [Key(0)] public BuffType Type;
    /// <summary>Stores the value.</summary>
    [Key(1)] public float Value;
    /// <summary>Stores the remain seconds.</summary>
    [Key(2)] public int RemainSeconds;
    /// <summary>Stores the source.</summary>
    [Key(3)] public string Source;
    /// <summary>Stores the is debuff.</summary>
    [Key(4)] public bool IsDebuff;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ActiveBuff Create(Random rng, int id) => new()
    {
        Type = (BuffType)(id % 5),
        Value = (float)Math.Round(rng.NextDouble() * 500, 2),
        RemainSeconds = rng.Next(5, 3600),
        Source = $"Skill_{rng.Next(1, 50)}",
        IsDebuff = id % 7 == 0,
    };
}

/// <summary>Provides character stats functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class CharacterStats
{
    /// <summary>Stores the hp.</summary>
    [Key(0)] public int Hp;
    /// <summary>Stores the max hp.</summary>
    [Key(1)] public int MaxHp;
    /// <summary>Stores the mp.</summary>
    [Key(2)] public int Mp;
    /// <summary>Stores the max mp.</summary>
    [Key(3)] public int MaxMp;
    /// <summary>Stores the attack.</summary>
    [Key(4)] public float Attack;
    /// <summary>Stores the defense.</summary>
    [Key(5)] public float Defense;
    /// <summary>Stores the speed.</summary>
    [Key(6)] public float Speed;
    /// <summary>Stores the crit rate.</summary>
    [Key(7)] public float CritRate;
    /// <summary>Stores the crit damage.</summary>
    [Key(8)] public float CritDamage;
    /// <summary>Stores the level.</summary>
    [Key(9)] public int Level;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CharacterStats Create(Random rng) => new()
    {
        Hp = rng.Next(500, 50000),
        MaxHp = 50000,
        Mp = rng.Next(0, 10000),
        MaxMp = 10000,
        Attack = (float)Math.Round(rng.NextDouble() * 8000 + 500, 2),
        Defense = (float)Math.Round(rng.NextDouble() * 4000 + 200, 2),
        Speed = (float)Math.Round(rng.NextDouble() * 300 + 100, 2),
        CritRate = (float)Math.Round(rng.NextDouble() * 0.8, 4),
        CritDamage = (float)Math.Round(rng.NextDouble() * 3.0 + 1.5, 4),
        Level = rng.Next(1, 200),
    };
}

/// <summary>Provides guild info functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class GuildInfo
{
    /// <summary>Stores the guild id.</summary>
    [Key(0)] public int GuildId;
    /// <summary>Stores the guild name.</summary>
    [Key(1)] public string GuildName;
    /// <summary>Stores the guild tag.</summary>
    [Key(2)] public string GuildTag;
    /// <summary>Stores the guild level.</summary>
    [Key(3)] public int GuildLevel;
    /// <summary>Stores the guild exp.</summary>
    [Key(4)] public long GuildExp;
    /// <summary>Stores the member count.</summary>
    [Key(5)] public int MemberCount;
    /// <summary>Stores the max member count.</summary>
    [Key(6)] public int MaxMemberCount;
    /// <summary>Stores the guild notice.</summary>
    [Key(7)] public string GuildNotice;
    /// <summary>Stores the is public.</summary>
    [Key(8)] public bool IsPublic;
    /// <summary>Stores the created at.</summary>
    [Key(9)] public long CreatedAt;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static GuildInfo Create(Random rng) => new()
    {
        GuildId = rng.Next(1, 99999),
        GuildName = "LegendaryGuild",
        GuildTag = "LGD",
        GuildLevel = rng.Next(1, 30),
        GuildExp = rng.Next(0, 10_000_000),
        MemberCount = rng.Next(1, 100),
        MaxMemberCount = 100,
        GuildNotice = "Welcome to LegendaryGuild! Guild events every Friday.",
        IsPublic = true,
        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - rng.Next(100000, 5000000),
    };
}

// ────────────────────────────────────────────────────────────────────────────
// Root character save: polymorphic collections, a generic snapshot, and scalar members.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides character save data functionality.</summary>
[LuminPackable]
[MemoryPackable]
[MessagePackObject]
[NinoType]
public sealed partial class CharacterSaveData
{
    // Scalar fields.
    /// <summary>Stores the character id.</summary>
    [Key(0)] public int CharacterId;
    /// <summary>Stores the character name.</summary>
    [Key(1)] public string CharacterName;
    /// <summary>Stores the class.</summary>
    [Key(2)] public CharacterClass Class;
    /// <summary>Stores the server region.</summary>
    [Key(3)] public int ServerRegion;
    /// <summary>Stores the total play time.</summary>
    [Key(4)] public long TotalPlayTime;
    /// <summary>Stores the last save at.</summary>
    [Key(5)] public long LastSaveAt;
    /// <summary>Stores the is online.</summary>
    [Key(6)] public bool IsOnline;
    /// <summary>Stores the is deleted.</summary>
    [Key(7)] public bool IsDeleted;
    /// <summary>Stores the vip level.</summary>
    [Key(8)] public int VipLevel;
    /// <summary>Stores the diamond count.</summary>
    [Key(9)] public long DiamondCount;
    /// <summary>Stores the coin count.</summary>
    [Key(10)] public long CoinCount;
    /// <summary>Stores the pvp rating.</summary>
    [Key(11)] public int PvpRating;
    /// <summary>Stores the pve rating.</summary>
    [Key(12)] public int PveRating;
    /// <summary>Stores the home region.</summary>
    [Key(13)] public string HomeRegion;
    /// <summary>Stores the title.</summary>
    [Key(14)] public string Title;

    // Nested objects.
    /// <summary>Stores the stats.</summary>
    [Key(15)] public CharacterStats Stats;
    /// <summary>Stores the guild.</summary>
    [Key(16)] public GuildInfo Guild;

    // Generic snapshot: Envelope<T>.
    /// <summary>Stores the stats snapshot.</summary>
    [Key(17)] public Envelope<CharacterStats> StatsSnapshot;
    /// <summary>Stores the guild snapshot.</summary>
    [Key(18)] public Envelope<GuildInfo> GuildSnapshot;

    // Polymorphic collection 1: main inventory, seven derived types and 40 entries.
    /// <summary>Stores the bag.</summary>
    [Key(19)] public List<ItemBase> Bag;

    // Polymorphic collection 2: storage, seven derived types and 25 entries.
    /// <summary>Stores the storage.</summary>
    [Key(20)] public List<ItemBase> Storage;

    // Polymorphic collection 3: skills, four derived types and 20 entries.
    /// <summary>Stores the skills.</summary>
    [Key(21)] public List<SkillBase> Skills;

    // Polymorphic collection 4: recent events, four derived types and 30 entries.
    /// <summary>Stores the recent events.</summary>
    [Key(22)] public List<EventLogBase> RecentEvents;

    // Non-polymorphic collection.
    /// <summary>Stores the active buffs.</summary>
    [Key(23)] public ActiveBuff[] ActiveBuffs;
    /// <summary>Stores the skill levels.</summary>
    [Key(24)] public Dictionary<int, int> SkillLevels;
    /// <summary>Stores the records.</summary>
    [Key(25)] public Dictionary<string, long> Records;
    /// <summary>Stores the unlocked titles.</summary>
    [Key(26)] public List<string> UnlockedTitles;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static CharacterSaveData Create()
    {
        var rng = new Random(42);

        // Forty main inventory entries across seven derived types.
        var bag = new List<ItemBase>();
        for (int i = 1; i <= 8; i++) bag.Add(WeaponItem.Create(rng, i));
        for (int i = 1; i <= 10; i++) bag.Add(ArmorItem.Create(rng, i));
        for (int i = 1; i <= 10; i++) bag.Add(ConsumableItem.Create(rng, i));
        for (int i = 1; i <= 4; i++) bag.Add(MountItem.Create(rng, i));
        for (int i = 1; i <= 3; i++) bag.Add(QuestItem.Create(rng, i));
        for (int i = 1; i <= 3; i++) bag.Add(PetItem.Create(rng, i));
        for (int i = 1; i <= 2; i++) bag.Add(RelicItem.Create(rng, i));

        // Storage contains 25 entries, primarily Armor, Relic, and Pet.
        var storage = new List<ItemBase>();
        for (int i = 1; i <= 8; i++) storage.Add(ArmorItem.Create(rng, i + 10));
        for (int i = 1; i <= 6; i++) storage.Add(RelicItem.Create(rng, i));
        for (int i = 1; i <= 5; i++) storage.Add(PetItem.Create(rng, i + 3));
        for (int i = 1; i <= 4; i++) storage.Add(WeaponItem.Create(rng, i + 10));
        for (int i = 1; i <= 2; i++) storage.Add(QuestItem.Create(rng, i + 3));

        // Twenty skills across four derived types.
        var skills = new List<SkillBase>();
        for (int i = 1; i <= 8; i++) skills.Add(ActiveSkill.Create(rng, i));
        for (int i = 1; i <= 6; i++) skills.Add(PassiveSkill.Create(rng, i));
        for (int i = 1; i <= 3; i++) skills.Add(UltimateSkill.Create(rng, i));
        for (int i = 1; i <= 3; i++) skills.Add(SupportSkill.Create(rng, i));

        // Thirty event log entries across four derived types.
        var events = new List<EventLogBase>();
        for (int i = 1; i <= 12; i++) events.Add(CombatEventLog.Create(rng, i));
        for (int i = 1; i <= 8; i++) events.Add(CraftingEventLog.Create(rng, i));
        for (int i = 1; i <= 6; i++) events.Add(TradeEventLog.Create(rng, i));
        for (int i = 1; i <= 4; i++) events.Add(AchievementEventLog.Create(rng, i));

        var stats = CharacterStats.Create(rng);
        var guild = GuildInfo.Create(rng);

        return new CharacterSaveData
        {
            CharacterId = rng.Next(100000, 999999),
            CharacterName = "DragonSlayer_X",
            Class = CharacterClass.Paladin,
            ServerRegion = 3,
            TotalPlayTime = rng.Next(0, 10_000_000),
            LastSaveAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IsOnline = true,
            IsDeleted = false,
            Title = "Conqueror of the Abyss",
            VipLevel = rng.Next(0, 15),
            DiamondCount = rng.Next(0, 100000),
            CoinCount = rng.Next(0, 10_000_000),
            PvpRating = rng.Next(1000, 3000),
            PveRating = rng.Next(500, 5000),
            HomeRegion = "Asia-East-01",
            Stats = stats,
            Guild = guild,
            StatsSnapshot = new Envelope<CharacterStats>
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600,
                Tag = "hourly_snapshot",
                Version = 3,
                Payload = CharacterStats.Create(rng),
            },
            GuildSnapshot = new Envelope<GuildInfo>
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7200,
                Tag = "guild_snapshot",
                Version = 1,
                Payload = GuildInfo.Create(rng),
            },
            Bag = bag,
            Storage = storage,
            Skills = skills,
            RecentEvents = events,
            ActiveBuffs = Enumerable.Range(1, 100).Select(i => ActiveBuff.Create(rng, i)).ToArray(),
            SkillLevels = Enumerable.Range(1, 100).ToDictionary(i => i, i => rng.Next(1, 10)),
            Records = Enumerable.Range(1, 100).ToDictionary(
                               i => $"RECORD_{i:D3}",
                               i => (long)i),
            UnlockedTitles = Enumerable.Range(1, 16).Select(i => $"Title_{i:D2}_Master").ToList(),
        };
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Benchmark
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides serialize benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 3, warmupCount: 6, iterationCount: 15)]
[MemoryDiagnoser]
[GcServer(true)]
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

private MemoryPackSerializerOptions memoryPackSerializerOption = new MemoryPackSerializerOptions()
    {
        StringEncoding = StringEncoding.Utf16
    };

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void SetUp()
    {
        _luminBuffer = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(_data);
        _memoryBuffer = MemoryPackSerializer.Serialize(_data, memoryPackSerializerOption);
        _msgpackBuffer = MessagePackSerializer.Serialize(_data);
        _ninoBuffer = NinoSerializer.Serialize(_data);
    }

    /// <summary>Measures clean up for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void CleanUp() => _luminWriter.Dispose();

    /// <summary>Measures lumin pack serialize for the configured benchmark input.</summary>
    [Benchmark(Baseline = true)]
    public void LuminPackSerialize()
    {
        _luminWriter.ResetCore();
        _luminWriter.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(_data, _luminWriter);
    }

    /// <summary>Measures memory pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackSerialize()
    {
        _memoryWriter.Reset();
        MemoryPackSerializer.Serialize(_memoryWriter, _data, memoryPackSerializerOption);
    }

    /// <summary>Measures message pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackSerialize()
    {
        _msgpackWriter.ResetWrittenCount();
        MessagePackSerializer.Serialize(_msgpackWriter, _data);
    }

    /// <summary>Measures nino serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void NinoSerialize()
    {
        _ninoWriter.ResetWrittenCount();
        NinoSerializer.Serialize(_data, _ninoWriter);
    }

    /// <summary>Measures lumin pack deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public CharacterSaveData LuminPackDeserialize()
        => LuminPackSerializer.Deserialize<CharacterSaveData, LuminPackUtf16LengthOptions>(_luminBuffer);

    /// <summary>Measures memory pack deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public CharacterSaveData MemoryPackDeserialize()
        => MemoryPackSerializer.Deserialize<CharacterSaveData>(_memoryBuffer, memoryPackSerializerOption);

    /// <summary>Measures message pack deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public CharacterSaveData MessagePackDeserialize()
        => MessagePackSerializer.Deserialize<CharacterSaveData>(_msgpackBuffer);

    /// <summary>Measures nino deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public CharacterSaveData NinoDeserialize()
        => NinoDeserializer.Deserialize<CharacterSaveData>(_ninoBuffer);
}
