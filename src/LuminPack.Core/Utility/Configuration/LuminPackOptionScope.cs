using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Option;

namespace LuminPack.Code;

/// <summary>Supplies compiler-bound identities used only while a generated member operation is expanded.</summary>
/// <remarks>The generator erases these identities after capturing their profile; they do not change storage or framing.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackOptionScope
{
    /// <summary>Identifies a member's complete write scope without copying its value.</summary>
    /// <typeparam name="TOptions">The stateless member profile.</typeparam>
    /// <typeparam name="T">The original member type.</typeparam>
    /// <param name="value">The original value storage.</param>
    /// <returns>A readonly alias of the original storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T Write<TOptions, T>(in T value) where TOptions : struct, ILuminPackOptions => ref value;

    /// <summary>Identifies a member's read or size scope while preserving its original writable alias.</summary>
    /// <typeparam name="TOptions">The stateless member profile.</typeparam>
    /// <typeparam name="T">The original member type.</typeparam>
    /// <param name="value">The original value storage.</param>
    /// <returns>A writable alias of the original storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Read<TOptions, T>(ref T value) where TOptions : struct, ILuminPackOptions => ref value;
}
