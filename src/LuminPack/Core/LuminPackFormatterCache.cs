using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPack.Core;

public delegate void LuminPackBinarySerializeDelegate<T>(ref LuminPackWriter writer, scoped in T value);
public delegate void LuminPackBinaryDeserializeDelegate<T>(ref LuminPackReader reader, scoped ref T value);
public delegate void LuminPackBinaryCalculateDelegate<T>(ref LuminPackEvaluator evaluator, scoped ref T value);
public delegate void LuminPackJsonSerializeDelegate<T>(ref LuminPackJsonWriter writer, scoped in T value);
public delegate void LuminPackJsonDeserializeDelegate<T>(ref LuminPackJsonReader reader, scoped ref T value);

/// <summary>
/// Closed generic formatter delegates populated only by source-generated registration code.
/// </summary>
public static class LuminPackFormatterCache
{
    public static class Cache<T>
    {
        public static LuminPackBinarySerializeDelegate<T> Serialize = ErrorCache<T>.Serialize;
        public static LuminPackBinaryDeserializeDelegate<T> Deserialize = ErrorCache<T>.Deserialize;
        public static LuminPackBinaryCalculateDelegate<T> CalculateOffset = ErrorCache<T>.CalculateOffset;
        public static LuminPackJsonSerializeDelegate<T> SerializeJson = ErrorCache<T>.SerializeJson;
        public static LuminPackJsonDeserializeDelegate<T> DeserializeJson = ErrorCache<T>.DeserializeJson;
    }

    public static class ErrorCache<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize(ref LuminPackWriter writer, scoped in T value)
            => LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Deserialize(ref LuminPackReader reader, scoped ref T value)
            => LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T value)
            => LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeJson(ref LuminPackJsonWriter writer, scoped in T value)
            => LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T value)
            => LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
    }
}
