using System.Runtime.CompilerServices;
using LuminPack.Option;

namespace LuminPack.Core;

public ref partial struct LuminPackEvaluator
{
    /// <summary>Measures the string representation of a generated partition-eligible Union case.</summary>
    /// <typeparam name="TOptions">The compiled string profile.</typeparam>
    /// <param name="value">The nullable string snapshot.</param>
    /// <returns>The exact record width, including its profile-specific framing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetPartitionStringLength<TOptions>(scoped ref string? value)
        where TOptions : struct, ILuminPackOptions
    {
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 &&
            default(TOptions).StringRecording == LuminPackStringRecording.Length)
            return checked(sizeof(int) + (value is null ? 0 : value.Length * sizeof(char)));
        return GetStringLength<TOptions>(ref value);
    }
}
