using System.Runtime.CompilerServices;

namespace LuminPack.Core;

public static class LuminPackLocalExtension
{
    public static void WriteValue<T>(ref this LuminPackWriter writer, scoped in T value)
    {
        LuminPackParseProvider.Cache<T>.Parser!.Serialize(ref writer, ref Unsafe.AsRef(in value)!);
    }
    
    public static void ReadValue<T>(ref this LuminPackReader reader, scoped ref T value)
    {
        LuminPackParseProvider.Cache<T>.Parser!.Deserialize(ref reader, ref value!);
    }
}