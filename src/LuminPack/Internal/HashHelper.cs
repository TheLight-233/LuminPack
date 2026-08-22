namespace LuminPack.Internal;

// Portions of this file (prime table, GetFastModMultiplier, FastMod, ExpandPrime, IsPrime)
// are derived from the .NET Runtime (MIT License).
// Copyright (c) .NET Foundation and Contributors
// https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

using System.Runtime.CompilerServices;

internal static class HashHelpers
{
    private static readonly int[] Primes = {
        3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPrime(int min)
    {
        foreach (int prime in Primes.AsSpan())
        {
            if (prime >= min)
                return prime;
        }
            
        // Fallback for large primes
        for (int i = min | 1; i < int.MaxValue; i += 2)
        {
            if (IsPrime(i))
                return i;
        }
            
        return min;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ExpandPrime(int oldSize)
    {
        int newSize = 2 * oldSize;
        return (uint)newSize > 0x7FEFFFFD && 0x7FEFFFFD > oldSize ? 0x7FEFFFFD : GetPrime(newSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetFastModMultiplier(uint divisor) => divisor is 0 ? 0 : ulong.MaxValue / divisor + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FastMod(uint value, uint divisor, ulong multiplier)
    {
        // Corrected formula from .NET Runtime: the +1 prevents the rare case
        // where the result equals `divisor` instead of 0.
        return (uint)(((((multiplier * value) >> 32) + 1) * divisor) >> 32);
    }

    private static bool IsPrime(int candidate)
    {
        if ((candidate & 1) == 0)
            return candidate == 2;
            
        int limit = (int)Math.Sqrt(candidate);
        for (int divisor = 3; divisor <= limit; divisor += 2)
        {
            if (candidate % divisor == 0)
                return false;
        }
            
        return true;
    }
}

internal static class ThrowHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowKeyNotFoundException<TKey>(TKey key)
    {
        throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowInvalidOperationException(string message)
    {
        throw new InvalidOperationException(message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowArgumentException(string message)
    {
        throw new ArgumentException(message);
    }
}