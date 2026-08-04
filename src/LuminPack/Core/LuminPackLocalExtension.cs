using System.Runtime.CompilerServices;

namespace LuminPack.Core;

public static class LuminPackLocalExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteValue<T>(ref this LuminPackWriter writer, scoped in T value)
    {
        LuminPackParseProvider.Cache<T>.Parser!.Serialize(ref writer, ref Unsafe.AsRef(in value)!);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadValue<T>(ref this LuminPackReader reader, scoped ref T value)
    {
        LuminPackParseProvider.Cache<T>.Parser!.Deserialize(ref reader, ref value!);
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
