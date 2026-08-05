using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackBenchmark;

[LuminPackable]
public sealed partial class JsonInitializerJitControl
{
    public int Number = 42;
    public string? Text = "initializer";
    public List<int>? Values = new();
}

internal static class CharacterSaveDataJsonJitProbe
{
    private const int Iterations = 20_000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        var data = CharacterSaveData.Create();
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Length
        };
        var json = LuminPackSerializer.SerializeJson(data, option);

        CharacterSaveData? result = null;
        for (var i = 0; i < Iterations; i++)
            result = LuminPackSerializer.DeserializeJson<CharacterSaveData>(json, option);

        const string initializerControlJson = "{\"Number\":17}";
        JsonInitializerJitControl? initializerControl = null;
        for (var i = 0; i < Iterations; i++)
            initializerControl = LuminPackSerializer.DeserializeJson<JsonInitializerJitControl>(
                initializerControlJson,
                option);

        var expectedWeapon = data.Bag.OfType<WeaponItem>().First();
        var actualWeapon = result?.Bag.OfType<WeaponItem>().FirstOrDefault();
        var expectedRelic = data.Storage.OfType<RelicItem>().First();
        var actualRelic = result?.Storage.OfType<RelicItem>().FirstOrDefault();
        if (result is null ||
            result.CharacterId != data.CharacterId ||
            actualWeapon is null ||
            actualWeapon.ItemId != expectedWeapon.ItemId ||
            actualWeapon.GemSocketA != expectedWeapon.GemSocketA ||
            actualWeapon.GemSocketB != expectedWeapon.GemSocketB ||
            actualRelic is null ||
            actualRelic.ItemId != expectedRelic.ItemId ||
            actualRelic.MainStat != expectedRelic.MainStat ||
            actualRelic.SubStatA != expectedRelic.SubStatA ||
            actualRelic.SubStatB != expectedRelic.SubStatB ||
            initializerControl is null ||
            initializerControl.Number != 17 ||
            initializerControl.Text is not null ||
            initializerControl.Values is not null)
            throw new InvalidOperationException("Character/Weapon/Relic JSON direct-result round-trip failed.");

        Console.WriteLine($"Character JSON JIT probe complete: {result.CharacterId}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RunInitializerControl()
    {
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Length
        };
        JsonInitializerJitControl? result = null;
        for (var i = 0; i < 100_000; i++)
            result = LuminPackSerializer.DeserializeJson<JsonInitializerJitControl>("{\"Number\":17}", option);

        if (result is null || result.Number != 17 || result.Text is not null || result.Values is not null)
            throw new InvalidOperationException("JSON initializer control changed temp-first semantics.");

        Console.WriteLine($"JSON initializer JIT control complete: {result.Number}");
    }
}
