using System.Runtime.CompilerServices;

namespace LuminPack.Core;

public static class LuminPackLocalExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue<T>(ref this LuminPackWriter writer, scoped in T value)
    {
        LuminPackFormatterCache.Cache<T>.Serialize(ref writer, in value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadValue<T>(ref this LuminPackReader reader, scoped ref T value)
    {
        LuminPackFormatterCache.Cache<T>.Deserialize(ref reader, ref value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue<T>(ref this LuminPackJsonWriter writer, scoped in T value)
    {
        LuminPackFormatterCache.Cache<T>.SerializeJson(ref writer, in value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadValue<T>(ref this LuminPackJsonReader reader, scoped ref T value)
    {
        LuminPackFormatterCache.Cache<T>.DeserializeJson(ref reader, ref value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CalculateOffset<T>(ref this LuminPackEvaluator evaluator, scoped ref T value)
    {
        LuminPackFormatterCache.Cache<T>.CalculateOffset(ref evaluator, ref value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WritePolymorphismValue<T>(ref this LuminPackWriter writer, scoped in T value)
    {
        WriteValue(ref writer, in value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadPolymorphismValue<T>(ref this LuminPackReader reader, ref T value)
    {
        ReadValue(ref reader, ref value);
    }
}
