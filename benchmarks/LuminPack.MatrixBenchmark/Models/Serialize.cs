using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Utility;
using MemoryPack;

namespace LuminPackBenchmark;

#nullable disable


/// <summary>Provides character class functionality.</summary>
public enum CharacterClass : int
{
    /// <summary>Specifies warrior.</summary>
    Warrior = 0,
    /// <summary>Specifies mage.</summary>
    Mage = 1,
    /// <summary>Specifies rogue.</summary>
    Rogue = 2,
    /// <summary>Specifies paladin.</summary>
    Paladin = 3,
    /// <summary>Specifies ranger.</summary>
    Ranger = 4
}
/// <summary>Provides equip slot functionality.</summary>
public enum EquipSlot : int
{
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
    Amulet = 7
}
/// <summary>Provides buff type functionality.</summary>
public enum BuffType : int
{
    /// <summary>Specifies strength.</summary>
    Strength = 0,
    /// <summary>Specifies defense.</summary>
    Defense = 1,
    /// <summary>Specifies speed.</summary>
    Speed = 2,
    /// <summary>Specifies regen.</summary>
    Regen = 3,
    /// <summary>Specifies critical.</summary>
    Critical = 4
}
/// <summary>Provides skill tier functionality.</summary>
public enum SkillTier : byte
{
    /// <summary>Specifies common.</summary>
    Common = 0,
    /// <summary>Specifies rare.</summary>
    Rare = 1,
    /// <summary>Specifies epic.</summary>
    Epic = 2,
    /// <summary>Specifies legendary.</summary>
    Legendary = 3
}
/// <summary>Provides event category functionality.</summary>
public enum EventCategory : int
{
    /// <summary>Specifies combat.</summary>
    Combat = 0,
    /// <summary>Specifies crafting.</summary>
    Crafting = 1,
    /// <summary>Specifies trade.</summary>
    Trade = 2,
    /// <summary>Specifies social.</summary>
    Social = 3,
    /// <summary>Specifies system.</summary>
    System = 4
}


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
    public int ItemId;
    /// <summary>Stores the item name.</summary>
    public string ItemName;
    /// <summary>Stores the rarity.</summary>
    public int Rarity;
    /// <summary>Stores the obtained at.</summary>
    public long ObtainedAt;
    /// <summary>Stores the is locked.</summary>
    public bool IsLocked;
}

/// <summary>Provides weapon item functionality.</summary>
[LuminPackable]
[MemoryPackable]


public sealed partial class WeaponItem : ItemBase
{
    /// <summary>Stores the attack power.</summary>
    public float AttackPower;
    /// <summary>Stores the crit rate.</summary>
    public float CritRate;
    /// <summary>Stores the durability.</summary>
    public int Durability;
    /// <summary>Stores the slot.</summary>
    public EquipSlot Slot;
    /// <summary>Stores the gem socket a.</summary>
    public string GemSocketA;
    /// <summary>Stores the gem socket b.</summary>
    public string GemSocketB;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static WeaponItem Create(Random rng, int id) => new()
    {
        ItemId = id,
        ItemName = $"Weapon_{id:D3}",
        Rarity = rng.Next(1, 6),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 100000),
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


public sealed partial class ArmorItem : ItemBase
{
    /// <summary>Stores the defense.</summary>
    public float Defense;
    /// <summary>Stores the magic resist.</summary>
    public float MagicResist;
    /// <summary>Stores the durability.</summary>
    public int Durability;
    /// <summary>Stores the slot.</summary>
    public EquipSlot Slot;
    /// <summary>Stores the set id.</summary>
    public int SetId;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ArmorItem Create(Random rng, int id) => new()
    {
        ItemId = id + 1000,
        ItemName = $"Armor_{id:D3}",
        Rarity = rng.Next(1, 6),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 100000),
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


public sealed partial class ConsumableItem : ItemBase
{
    /// <summary>Stores the count.</summary>
    public int Count;
    /// <summary>Stores the max stack.</summary>
    public int MaxStack;
    /// <summary>Stores the heal amount.</summary>
    public float HealAmount;
    /// <summary>Stores the mana amount.</summary>
    public float ManaAmount;
    /// <summary>Stores the effect.</summary>
    public string Effect;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ConsumableItem Create(Random rng, int id) => new()
    {
        ItemId = id + 2000,
        ItemName = $"Potion_{id:D3}",
        Rarity = rng.Next(1, 4),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 50000),
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


public sealed partial class MountItem : ItemBase
{
    /// <summary>Stores the speed bonus.</summary>
    public float SpeedBonus;
    /// <summary>Stores the can fly.</summary>
    public bool CanFly;
    /// <summary>Stores the mount skin.</summary>
    public string MountSkin;
    /// <summary>Stores the stamina pool.</summary>
    public int StaminaPool;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static MountItem Create(Random rng, int id) => new()
    {
        ItemId = id + 3000,
        ItemName = $"Mount_{id:D3}",
        Rarity = rng.Next(3, 6),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 200000),
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


public sealed partial class QuestItem : ItemBase
{
    /// <summary>Stores the quest id.</summary>
    public int QuestId;
    /// <summary>Stores the objective name.</summary>
    public string ObjectiveName;
    /// <summary>Stores the is key item.</summary>
    public bool IsKeyItem;
    /// <summary>Stores the required count.</summary>
    public int RequiredCount;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static QuestItem Create(Random rng, int id) => new()
    {
        ItemId = id + 4000,
        ItemName = $"QuestItem_{id:D3}",
        Rarity = 1,
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 30000),
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


public sealed partial class PetItem : ItemBase
{
    /// <summary>Stores the pet name.</summary>
    public string PetName;
    /// <summary>Stores the loyalty.</summary>
    public int Loyalty;
    /// <summary>Stores the exp multiplier.</summary>
    public float ExpMultiplier;
    /// <summary>Stores the pet level.</summary>
    public int PetLevel;
    /// <summary>Stores the is awakened.</summary>
    public bool IsAwakened;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static PetItem Create(Random rng, int id) => new()
    {
        ItemId = id + 5000,
        ItemName = $"Pet_{id:D3}",
        Rarity = rng.Next(2, 6),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 500000),
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


public sealed partial class RelicItem : ItemBase
{
    /// <summary>Stores the set id.</summary>
    public int SetId;
    /// <summary>Stores the power level.</summary>
    public float PowerLevel;
    /// <summary>Stores the socket count.</summary>
    public int SocketCount;
    /// <summary>Stores the main stat.</summary>
    public string MainStat;
    /// <summary>Stores the sub stat a.</summary>
    public string SubStatA;
    /// <summary>Stores the sub stat b.</summary>
    public string SubStatB;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static RelicItem Create(Random rng, int id) => new()
    {
        ItemId = id + 6000,
        ItemName = $"Relic_{id:D3}",
        Rarity = rng.Next(3, 6),
        ObtainedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 300000),
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





[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(ActiveSkill), "ActiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PassiveSkill), "PassiveSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(UltimateSkill), "UltimateSkill")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(SupportSkill), "SupportSkill")]
public abstract partial class SkillBase
{
    /// <summary>Stores the skill id.</summary>
    public int SkillId;
    /// <summary>Stores the skill name.</summary>
    public string SkillName;
    /// <summary>Stores the level.</summary>
    public int Level;
    /// <summary>Stores the tier.</summary>
    public SkillTier Tier;
    /// <summary>Stores the is unlocked.</summary>
    public bool IsUnlocked;
}

/// <summary>Provides active skill functionality.</summary>
[LuminPackable]
[MemoryPackable]


public sealed partial class ActiveSkill : SkillBase
{
    /// <summary>Stores the cast time.</summary>
    public float CastTime;
    /// <summary>Stores the cooldown.</summary>
    public float Cooldown;
    /// <summary>Stores the mana cost.</summary>
    public int ManaCost;
    /// <summary>Stores the target type.</summary>
    public string TargetType;
    /// <summary>Stores the base damage.</summary>
    public float BaseDamage;

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


public sealed partial class PassiveSkill : SkillBase
{
    /// <summary>Stores the trigger chance.</summary>
    public float TriggerChance;
    /// <summary>Stores the trigger condition.</summary>
    public string TriggerCondition;
    /// <summary>Stores the stat bonus.</summary>
    public float StatBonus;
    /// <summary>Stores the affected stat.</summary>
    public string AffectedStat;

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


public sealed partial class UltimateSkill : SkillBase
{
    /// <summary>Stores the charge rate.</summary>
    public float ChargeRate;
    /// <summary>Stores the charge max.</summary>
    public int ChargeMax;
    /// <summary>Stores the ultimate damage.</summary>
    public float UltimateDamage;
    /// <summary>Stores the active duration.</summary>
    public int ActiveDuration;
    /// <summary>Stores the has transformation.</summary>
    public bool HasTransformation;

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


public sealed partial class SupportSkill : SkillBase
{
    /// <summary>Stores the heal power.</summary>
    public float HealPower;
    /// <summary>Stores the shield amount.</summary>
    public float ShieldAmount;
    /// <summary>Stores the buff duration.</summary>
    public int BuffDuration;
    /// <summary>Stores the applied buff.</summary>
    public string AppliedBuff;

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





[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CombatEventLog), "CombatEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CraftingEventLog), "CraftingEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(TradeEventLog), "TradeEventLog")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AchievementEventLog), "AchievementEventLog")]
public abstract partial class EventLogBase
{
    /// <summary>Stores the timestamp.</summary>
    public long Timestamp;
    /// <summary>Stores the category.</summary>
    public EventCategory Category;
    /// <summary>Stores the summary.</summary>
    public string Summary;
}

/// <summary>Provides combat event log functionality.</summary>
[LuminPackable]
[MemoryPackable]


public sealed partial class CombatEventLog : EventLogBase
{
    /// <summary>Stores the target id.</summary>
    public int TargetId;
    /// <summary>Stores the damage dealt.</summary>
    public int DamageDealt;
    /// <summary>Stores the is critical.</summary>
    public bool IsCritical;
    /// <summary>Stores the skill used.</summary>
    public string SkillUsed;
    /// <summary>Stores the is killing blow.</summary>
    public bool IsKillingBlow;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CombatEventLog Create(Random rng, int id) => new()
    {
        Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 86400),
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


public sealed partial class CraftingEventLog : EventLogBase
{
    /// <summary>Stores the recipe id.</summary>
    public int RecipeId;
    /// <summary>Stores the item name.</summary>
    public string ItemName;
    /// <summary>Stores the success.</summary>
    public bool Success;
    /// <summary>Stores the materials used.</summary>
    public int MaterialsUsed;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CraftingEventLog Create(Random rng, int id) => new()
    {
        Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 86400),
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


public sealed partial class TradeEventLog : EventLogBase
{
    /// <summary>Stores the partner id.</summary>
    public int PartnerId;
    /// <summary>Stores the gold amount.</summary>
    public long GoldAmount;
    /// <summary>Stores the item ids.</summary>
    public int[] ItemIds;
    /// <summary>Stores the is player initiated.</summary>
    public bool IsPlayerInitiated;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static TradeEventLog Create(Random rng, int id) => new()
    {
        Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 86400),
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


public sealed partial class AchievementEventLog : EventLogBase
{
    /// <summary>Stores the achievement id.</summary>
    public int AchievementId;
    /// <summary>Stores the achievement name.</summary>
    public string AchievementName;
    /// <summary>Stores the reward gold.</summary>
    public int RewardGold;
    /// <summary>Stores the reward diamond.</summary>
    public int RewardDiamond;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="id">The id used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static AchievementEventLog Create(Random rng, int id) => new()
    {
        Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(0, 86400),
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


public sealed partial class Envelope<T>
{
    /// <summary>Stores the timestamp.</summary>
    public long Timestamp;
    /// <summary>Stores the tag.</summary>
    public string Tag;
    /// <summary>Stores the version.</summary>
    public int Version;
    /// <summary>Stores the payload.</summary>
    public T Payload;
}

// ────────────────────────────────────────────────────────────────────────────
// Non-polymorphic helper types.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides active buff functionality.</summary>
[LuminPackable]
[MemoryPackable]


public sealed partial class ActiveBuff
{
    /// <summary>Stores the type.</summary>
    public BuffType Type;
    /// <summary>Stores the value.</summary>
    public float Value;
    /// <summary>Stores the remain seconds.</summary>
    public int RemainSeconds;
    /// <summary>Stores the source.</summary>
    public string Source;
    /// <summary>Stores the is debuff.</summary>
    public bool IsDebuff;

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


public sealed partial class CharacterStats
{
    /// <summary>Stores the hp.</summary>
    public int Hp;
    /// <summary>Stores the max hp.</summary>
    public int MaxHp;
    /// <summary>Stores the mp.</summary>
    public int Mp;
    /// <summary>Stores the max mp.</summary>
    public int MaxMp;
    /// <summary>Stores the attack.</summary>
    public float Attack;
    /// <summary>Stores the defense.</summary>
    public float Defense;
    /// <summary>Stores the speed.</summary>
    public float Speed;
    /// <summary>Stores the crit rate.</summary>
    public float CritRate;
    /// <summary>Stores the crit damage.</summary>
    public float CritDamage;
    /// <summary>Stores the level.</summary>
    public int Level;

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


public sealed partial class GuildInfo
{
    /// <summary>Stores the guild id.</summary>
    public int GuildId;
    /// <summary>Stores the guild name.</summary>
    public string GuildName;
    /// <summary>Stores the guild tag.</summary>
    public string GuildTag;
    /// <summary>Stores the guild level.</summary>
    public int GuildLevel;
    /// <summary>Stores the guild exp.</summary>
    public long GuildExp;
    /// <summary>Stores the member count.</summary>
    public int MemberCount;
    /// <summary>Stores the max member count.</summary>
    public int MaxMemberCount;
    /// <summary>Stores the guild notice.</summary>
    public string GuildNotice;
    /// <summary>Stores the is public.</summary>
    public bool IsPublic;
    /// <summary>Stores the created at.</summary>
    public long CreatedAt;

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
        CreatedAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - rng.Next(100000, 5000000),
    };
}

// ────────────────────────────────────────────────────────────────────────────
// Root character save: polymorphic collections, a generic snapshot, and scalar members.
// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides character save data functionality.</summary>
[LuminPackable]
[MemoryPackable]


public sealed partial class CharacterSaveData
{
    // Scalar fields.
    /// <summary>Stores the character id.</summary>
    public int CharacterId;
    /// <summary>Stores the character name.</summary>
    public string CharacterName;
    /// <summary>Stores the class.</summary>
    public CharacterClass Class;
    /// <summary>Stores the server region.</summary>
    public int ServerRegion;
    /// <summary>Stores the total play time.</summary>
    public long TotalPlayTime;
    /// <summary>Stores the last save at.</summary>
    public long LastSaveAt;
    /// <summary>Stores the is online.</summary>
    public bool IsOnline;
    /// <summary>Stores the is deleted.</summary>
    public bool IsDeleted;
    /// <summary>Stores the vip level.</summary>
    public int VipLevel;
    /// <summary>Stores the diamond count.</summary>
    public long DiamondCount;
    /// <summary>Stores the coin count.</summary>
    public long CoinCount;
    /// <summary>Stores the pvp rating.</summary>
    public int PvpRating;
    /// <summary>Stores the pve rating.</summary>
    public int PveRating;
    /// <summary>Stores the home region.</summary>
    public string HomeRegion;
    /// <summary>Stores the title.</summary>
    public string Title;

    // Nested objects.
    /// <summary>Stores the stats.</summary>
    public CharacterStats Stats;
    /// <summary>Stores the guild.</summary>
    public GuildInfo Guild;

    // Generic snapshot: Envelope<T>.
    /// <summary>Stores the stats snapshot.</summary>
    public Envelope<CharacterStats> StatsSnapshot;
    /// <summary>Stores the guild snapshot.</summary>
    public Envelope<GuildInfo> GuildSnapshot;

    // Polymorphic collection 1: main inventory, seven derived types and 40 entries.
    /// <summary>Stores the bag.</summary>
    public List<ItemBase> Bag;

    // Polymorphic collection 2: storage, seven derived types and 25 entries.
    /// <summary>Stores the storage.</summary>
    public List<ItemBase> Storage;

    // Polymorphic collection 3: skills, four derived types and 20 entries.
    /// <summary>Stores the skills.</summary>
    public List<SkillBase> Skills;

    // Polymorphic collection 4: recent events, four derived types and 30 entries.
    /// <summary>Stores the recent events.</summary>
    public List<EventLogBase> RecentEvents;

    // Non-polymorphic collection.
    /// <summary>Stores the active buffs.</summary>
    public ActiveBuff[] ActiveBuffs;
    /// <summary>Stores the skill levels.</summary>
    public Dictionary<int, int> SkillLevels;
    /// <summary>Stores the records.</summary>
    public Dictionary<string, long> Records;
    /// <summary>Stores the unlocked titles.</summary>
    public List<string> UnlockedTitles;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static CharacterSaveData Create()
    {
        var rng = new Random(42);

        // Forty main inventory entries across seven derived types.
        var bag = new List<ItemBase>();
        for (int i = 1; i <= 8; i++)
            bag.Add(WeaponItem.Create(rng, i));
        for (int i = 1; i <= 10; i++)
            bag.Add(ArmorItem.Create(rng, i));
        for (int i = 1; i <= 10; i++)
            bag.Add(ConsumableItem.Create(rng, i));
        for (int i = 1; i <= 4; i++)
            bag.Add(MountItem.Create(rng, i));
        for (int i = 1; i <= 3; i++)
            bag.Add(QuestItem.Create(rng, i));
        for (int i = 1; i <= 3; i++)
            bag.Add(PetItem.Create(rng, i));
        for (int i = 1; i <= 2; i++)
            bag.Add(RelicItem.Create(rng, i));

        // Storage contains 25 entries, primarily Armor, Relic, and Pet.
        var storage = new List<ItemBase>();
        for (int i = 1; i <= 8; i++)
            storage.Add(ArmorItem.Create(rng, i + 10));
        for (int i = 1; i <= 6; i++)
            storage.Add(RelicItem.Create(rng, i));
        for (int i = 1; i <= 5; i++)
            storage.Add(PetItem.Create(rng, i + 3));
        for (int i = 1; i <= 4; i++)
            storage.Add(WeaponItem.Create(rng, i + 10));
        for (int i = 1; i <= 2; i++)
            storage.Add(QuestItem.Create(rng, i + 3));

        // Twenty skills across four derived types.
        var skills = new List<SkillBase>();
        for (int i = 1; i <= 8; i++)
            skills.Add(ActiveSkill.Create(rng, i));
        for (int i = 1; i <= 6; i++)
            skills.Add(PassiveSkill.Create(rng, i));
        for (int i = 1; i <= 3; i++)
            skills.Add(UltimateSkill.Create(rng, i));
        for (int i = 1; i <= 3; i++)
            skills.Add(SupportSkill.Create(rng, i));

        // Thirty event log entries across four derived types.
        var events = new List<EventLogBase>();
        for (int i = 1; i <= 12; i++)
            events.Add(CombatEventLog.Create(rng, i));
        for (int i = 1; i <= 8; i++)
            events.Add(CraftingEventLog.Create(rng, i));
        for (int i = 1; i <= 6; i++)
            events.Add(TradeEventLog.Create(rng, i));
        for (int i = 1; i <= 4; i++)
            events.Add(AchievementEventLog.Create(rng, i));

        var stats = CharacterStats.Create(rng);
        var guild = GuildInfo.Create(rng);

        return new CharacterSaveData
        {
            CharacterId = rng.Next(100000, 999999),
            CharacterName = "DragonSlayer_X",
            Class = CharacterClass.Paladin,
            ServerRegion = 3,
            TotalPlayTime = rng.Next(0, 10_000_000),
            LastSaveAt = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
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
                Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - 3600,
                Tag = "hourly_snapshot",
                Version = 3,
                Payload = CharacterStats.Create(rng),
            },
            GuildSnapshot = new Envelope<GuildInfo>
            {
                Timestamp = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds() - 7200,
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
