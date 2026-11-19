#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    /// <summary>Reads a standalone date and checks its Gregorian day number.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <returns>The complete date.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateOnly ReadDateOnly(ref LuminPackReader reader) => CheckDate(reader.Read<DateOnly>());

    /// <summary>Loads a standalone date from a validated fixed input range.</summary>
    /// <param name="source">The first byte of the complete date storage.</param>
    /// <param name="value">Receives the date after validation.</param>
    /// <returns>The consumed width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadDateOnly(ref byte source, out DateOnly value)
    {
        value = CheckDate(LuminPackReader.Read<DateOnly>(ref source));
        return Unsafe.SizeOf<DateOnly>();
    }

    /// <summary>Reads a standalone time and checks its ticks within the day.</summary>
    /// <param name="reader">The checked reader.</param>
    /// <returns>The complete time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeOnly ReadTimeOnly(ref LuminPackReader reader) => CheckTime(reader.Read<TimeOnly>());

    /// <summary>Loads a standalone time from a validated fixed input range.</summary>
    /// <param name="source">The first byte of the complete time storage.</param>
    /// <param name="value">Receives the time after validation.</param>
    /// <returns>The consumed width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadTimeOnly(ref byte source, out TimeOnly value)
    {
        value = CheckTime(LuminPackReader.Read<TimeOnly>(ref source));
        return Unsafe.SizeOf<TimeOnly>();
    }

    /// <summary>Checks the full day number range with a single unsigned comparison.</summary>
    /// <param name="value">The decoded date.</param>
    /// <returns>The validated date.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateOnly CheckDate(DateOnly value)
    {
        if (unchecked((uint)value.DayNumber) > (uint)DateOnly.MaxValue.DayNumber)
            InvalidTemporalValue(nameof(DateOnly));
        return value;
    }

    /// <summary>Checks the full tick range with a single unsigned comparison.</summary>
    /// <param name="value">The decoded time.</param>
    /// <returns>The validated time.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TimeOnly CheckTime(TimeOnly value)
    {
        if (unchecked((ulong)value.Ticks) >= (ulong)TimeSpan.TicksPerDay)
            InvalidTemporalValue(nameof(TimeOnly));
        return value;
    }

    /// <summary>Rejects temporal storage outside the type's supported range.</summary>
    /// <param name="type">The temporal type receiving the value.</param>
    [DoesNotReturn]
    private static void InvalidTemporalValue(string type) => LuminPackExceptionHelper.ThrowInvalidDataException("The " + type + " storage is outside its valid range.");
}
#endif
