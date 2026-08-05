using System.Runtime.CompilerServices;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Utility;


namespace LuminPackBenchmark;

/// <summary>
/// Gives every concrete union payload a unique JIT method name. The generated
/// extension overloads otherwise all appear as LuminPackExtensions:(byref,byref)
/// in diffable disassembly, which makes recursive call-chain attribution

/// ambiguous.
/// </summary>
internal static class CharacterSaveDataConcreteJitProbe
{
    private const int Iterations = 100_000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        var data = CharacterSaveData.Create();
        var buffer = LuminBufferWriterPool.Rent();

        try
        {
            WeaponItem weapon = (WeaponItem)data.Bag[0];
            ArmorItem armor = (ArmorItem)data.Bag[8];
            ConsumableItem consumable = (ConsumableItem)data.Bag[18];
            MountItem mount = (MountItem)data.Bag[28];
            QuestItem quest = (QuestItem)data.Bag[32];
            PetItem pet = (PetItem)data.Bag[35];
            RelicItem relic = (RelicItem)data.Bag[38];

            ActiveSkill active = (ActiveSkill)data.Skills[0];
            PassiveSkill passive = (PassiveSkill)data.Skills[8];
            UltimateSkill ultimate = (UltimateSkill)data.Skills[14];
            SupportSkill support = (SupportSkill)data.Skills[17];

            CombatEventLog combat = (CombatEventLog)data.RecentEvents[0];
            CraftingEventLog crafting = (CraftingEventLog)data.RecentEvents[12];
            TradeEventLog trade = (TradeEventLog)data.RecentEvents[20];
            AchievementEventLog achievement = (AchievementEventLog)data.RecentEvents[26];

            for (var i = 0; i < Iterations; i++)
            {
                weapon = RoundTripWeapon(buffer, weapon);
                armor = RoundTripArmor(buffer, armor);
                consumable = RoundTripConsumable(buffer, consumable);
                mount = RoundTripMount(buffer, mount);
                quest = RoundTripQuest(buffer, quest);
                pet = RoundTripPet(buffer, pet);
                relic = RoundTripRelic(buffer, relic);

                active = RoundTripActiveSkill(buffer, active);
                passive = RoundTripPassiveSkill(buffer, passive);
                ultimate = RoundTripUltimateSkill(buffer, ultimate);
                support = RoundTripSupportSkill(buffer, support);

                combat = RoundTripCombatEvent(buffer, combat);
                crafting = RoundTripCraftingEvent(buffer, crafting);
                trade = RoundTripTradeEvent(buffer, trade);
                achievement = RoundTripAchievementEvent(buffer, achievement);

                SerializeItemList(buffer, data.Bag);
                SerializeSkillList(buffer, data.Skills);
                SerializeEventList(buffer, data.RecentEvents);
                SerializeIntDictionary(buffer, data.SkillLevels);
                SerializeStringDictionary(buffer, data.Records);
            }

            Console.WriteLine($"Concrete JIT probe complete: {weapon.ItemId}/{active.SkillId}/{combat.TargetId}");
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }

    private static WeaponItem RoundTripWeapon(LuminBufferWriter buffer, WeaponItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeWeapon(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        WeaponItem result = null!;
        DeserializeWeapon(ref reader, ref result);
        return result;
    }

    private static ArmorItem RoundTripArmor(LuminBufferWriter buffer, ArmorItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeArmor(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        ArmorItem result = null!;
        DeserializeArmor(ref reader, ref result);
        return result;
    }

    private static ConsumableItem RoundTripConsumable(LuminBufferWriter buffer, ConsumableItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeConsumable(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        ConsumableItem result = null!;
        DeserializeConsumable(ref reader, ref result);
        return result;
    }

    private static MountItem RoundTripMount(LuminBufferWriter buffer, MountItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeMount(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        MountItem result = null!;
        DeserializeMount(ref reader, ref result);
        return result;
    }

    private static QuestItem RoundTripQuest(LuminBufferWriter buffer, QuestItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeQuest(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        QuestItem result = null!;
        DeserializeQuest(ref reader, ref result);
        return result;
    }

    private static PetItem RoundTripPet(LuminBufferWriter buffer, PetItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializePet(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        PetItem result = null!;
        DeserializePet(ref reader, ref result);
        return result;
    }

    private static RelicItem RoundTripRelic(LuminBufferWriter buffer, RelicItem value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeRelic(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        RelicItem result = null!;
        DeserializeRelic(ref reader, ref result);
        return result;
    }

    private static ActiveSkill RoundTripActiveSkill(LuminBufferWriter buffer, ActiveSkill value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeActiveSkill(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        ActiveSkill result = null!;
        DeserializeActiveSkill(ref reader, ref result);
        return result;
    }

    private static PassiveSkill RoundTripPassiveSkill(LuminBufferWriter buffer, PassiveSkill value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializePassiveSkill(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        PassiveSkill result = null!;
        DeserializePassiveSkill(ref reader, ref result);
        return result;
    }

    private static UltimateSkill RoundTripUltimateSkill(LuminBufferWriter buffer, UltimateSkill value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeUltimateSkill(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        UltimateSkill result = null!;
        DeserializeUltimateSkill(ref reader, ref result);
        return result;
    }

    private static SupportSkill RoundTripSupportSkill(LuminBufferWriter buffer, SupportSkill value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeSupportSkill(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        SupportSkill result = null!;
        DeserializeSupportSkill(ref reader, ref result);
        return result;
    }

    private static CombatEventLog RoundTripCombatEvent(LuminBufferWriter buffer, CombatEventLog value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeCombatEvent(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        CombatEventLog result = null!;
        DeserializeCombatEvent(ref reader, ref result);
        return result;
    }

    private static CraftingEventLog RoundTripCraftingEvent(LuminBufferWriter buffer, CraftingEventLog value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeCraftingEvent(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        CraftingEventLog result = null!;
        DeserializeCraftingEvent(ref reader, ref result);
        return result;
    }

    private static TradeEventLog RoundTripTradeEvent(LuminBufferWriter buffer, TradeEventLog value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeTradeEvent(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        TradeEventLog result = null!;
        DeserializeTradeEvent(ref reader, ref result);
        return result;
    }

    private static AchievementEventLog RoundTripAchievementEvent(LuminBufferWriter buffer, AchievementEventLog value)
    {
        var writer = new LuminPackWriter(buffer);
        SerializeAchievementEvent(ref writer, value);
        ReadOnlySpan<byte> payload = writer.GetSpan();
        var reader = new LuminPackReader(ref payload);
        AchievementEventLog result = null!;
        DeserializeAchievementEvent(ref reader, ref result);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeWeapon(ref LuminPackWriter writer, WeaponItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeArmor(ref LuminPackWriter writer, ArmorItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeConsumable(ref LuminPackWriter writer, ConsumableItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeMount(ref LuminPackWriter writer, MountItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeQuest(ref LuminPackWriter writer, QuestItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializePet(ref LuminPackWriter writer, PetItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeRelic(ref LuminPackWriter writer, RelicItem value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeActiveSkill(ref LuminPackWriter writer, ActiveSkill value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializePassiveSkill(ref LuminPackWriter writer, PassiveSkill value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeUltimateSkill(ref LuminPackWriter writer, UltimateSkill value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeSupportSkill(ref LuminPackWriter writer, SupportSkill value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeCombatEvent(ref LuminPackWriter writer, CombatEventLog value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeCraftingEvent(ref LuminPackWriter writer, CraftingEventLog value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeTradeEvent(ref LuminPackWriter writer, TradeEventLog value) => writer.WritePolymorphismValue(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void SerializeAchievementEvent(ref LuminPackWriter writer, AchievementEventLog value) => writer.WritePolymorphismValue(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SerializeItemList(LuminBufferWriter buffer, List<ItemBase> value)
    {
        var writer = new LuminPackWriter(buffer);
        writer.WriteValue(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SerializeSkillList(LuminBufferWriter buffer, List<SkillBase> value)
    {
        var writer = new LuminPackWriter(buffer);
        writer.WriteValue(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SerializeEventList(LuminBufferWriter buffer, List<EventLogBase> value)
    {
        var writer = new LuminPackWriter(buffer);
        writer.WriteValue(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SerializeIntDictionary(LuminBufferWriter buffer, Dictionary<int, int> value)
    {
        var writer = new LuminPackWriter(buffer);
        writer.WriteValue(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SerializeStringDictionary(LuminBufferWriter buffer, Dictionary<string, long> value)
    {
        var writer = new LuminPackWriter(buffer);
        writer.WriteValue(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeWeapon(ref LuminPackReader reader, ref WeaponItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeArmor(ref LuminPackReader reader, ref ArmorItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeConsumable(ref LuminPackReader reader, ref ConsumableItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeMount(ref LuminPackReader reader, ref MountItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeQuest(ref LuminPackReader reader, ref QuestItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializePet(ref LuminPackReader reader, ref PetItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeRelic(ref LuminPackReader reader, ref RelicItem value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeActiveSkill(ref LuminPackReader reader, ref ActiveSkill value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializePassiveSkill(ref LuminPackReader reader, ref PassiveSkill value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeUltimateSkill(ref LuminPackReader reader, ref UltimateSkill value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeSupportSkill(ref LuminPackReader reader, ref SupportSkill value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeCombatEvent(ref LuminPackReader reader, ref CombatEventLog value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeCraftingEvent(ref LuminPackReader reader, ref CraftingEventLog value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeTradeEvent(ref LuminPackReader reader, ref TradeEventLog value) => reader.ReadPolymorphismValue(ref value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static void DeserializeAchievementEvent(ref LuminPackReader reader, ref AchievementEventLog value) => reader.ReadPolymorphismValue(ref value);
}
