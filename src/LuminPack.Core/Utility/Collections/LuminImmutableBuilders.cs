using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Utility;

/// <summary>Reuses empty framework builders while preserving public insertion and freezing behavior.</summary>
/// <remarks>Rental and cleanup stay shared to keep thread-local lookup, cold allocation and exception-finally
/// expansion out of every generated reader. Callers own each rental exclusively until its matching return.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminImmutableBuilders
{
    /// <summary>Obtains an exclusive empty default-comparer builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <returns>The builder owned by the caller until its matching return.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ImmutableHashSet<T>.Builder RentHashSet<T>()
        => Cache<ImmutableHashSet<T>.Builder>.Take() ?? ImmutableHashSet.CreateBuilder<T>();

    /// <summary>Clears input references and restores defaults before retaining an exclusively owned builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <param name="builder">The builder being returned, including after an insertion failure.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnHashSet<T>(ImmutableHashSet<T>.Builder builder)
    {
        builder.Clear();
        if (!ReferenceEquals(builder.KeyComparer, EqualityComparer<T>.Default)) builder.KeyComparer = EqualityComparer<T>.Default;
        Cache<ImmutableHashSet<T>.Builder>.Store(builder);
    }

    /// <summary>Obtains an exclusive empty default-comparer builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <returns>The builder owned by the caller until its matching return.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ImmutableSortedSet<T>.Builder RentSortedSet<T>()
        => Cache<ImmutableSortedSet<T>.Builder>.Take() ?? ImmutableSortedSet.CreateBuilder<T>();

    /// <summary>Clears input references and restores defaults before retaining an exclusively owned builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <param name="builder">The builder being returned, including after an insertion failure.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnSortedSet<T>(ImmutableSortedSet<T>.Builder builder)
    {
        builder.Clear();
        if (!ReferenceEquals(builder.KeyComparer, Comparer<T>.Default)) builder.KeyComparer = Comparer<T>.Default;
        Cache<ImmutableSortedSet<T>.Builder>.Store(builder);
    }

    /// <summary>Obtains an exclusive empty default-comparer builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <returns>The builder owned by the caller until its matching return.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ImmutableList<T>.Builder RentList<T>()
        => Cache<ImmutableList<T>.Builder>.Take() ?? ImmutableList.CreateBuilder<T>();

    /// <summary>Clears input references and restores defaults before retaining an exclusively owned builder.</summary>
    /// <typeparam name="T">The builder element contract.</typeparam>
    /// <param name="builder">The builder being returned, including after an insertion failure.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnList<T>(ImmutableList<T>.Builder builder)
    {
        builder.Clear();
        Cache<ImmutableList<T>.Builder>.Store(builder);
    }

    /// <summary>Obtains an exclusive empty default-comparer builder.</summary>
    /// <typeparam name="TKey">The builder element contract.</typeparam>
    /// <typeparam name="TValue">The builder element contract.</typeparam>
    /// <returns>The builder owned by the caller until its matching return.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ImmutableDictionary<TKey, TValue>.Builder RentDictionary<TKey, TValue>() where TKey : notnull
        => Cache<ImmutableDictionary<TKey, TValue>.Builder>.Take() ?? ImmutableDictionary.CreateBuilder<TKey, TValue>();

    /// <summary>Clears input references and restores defaults before retaining an exclusively owned builder.</summary>
    /// <typeparam name="TKey">The builder element contract.</typeparam>
    /// <typeparam name="TValue">The builder element contract.</typeparam>
    /// <param name="builder">The builder being returned, including after an insertion failure.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnDictionary<TKey, TValue>(ImmutableDictionary<TKey, TValue>.Builder builder) where TKey : notnull
    {
        builder.Clear();
        if (!ReferenceEquals(builder.KeyComparer, EqualityComparer<TKey>.Default)) builder.KeyComparer = EqualityComparer<TKey>.Default;
        if (!ReferenceEquals(builder.ValueComparer, EqualityComparer<TValue>.Default)) builder.ValueComparer = EqualityComparer<TValue>.Default;
        Cache<ImmutableDictionary<TKey, TValue>.Builder>.Store(builder);
    }

    /// <summary>Obtains an exclusive empty default-comparer builder.</summary>
    /// <typeparam name="TKey">The builder element contract.</typeparam>
    /// <typeparam name="TValue">The builder element contract.</typeparam>
    /// <returns>The builder owned by the caller until its matching return.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ImmutableSortedDictionary<TKey, TValue>.Builder RentSortedDictionary<TKey, TValue>() where TKey : notnull
        => Cache<ImmutableSortedDictionary<TKey, TValue>.Builder>.Take() ?? ImmutableSortedDictionary.CreateBuilder<TKey, TValue>();

    /// <summary>Clears input references and restores defaults before retaining an exclusively owned builder.</summary>
    /// <typeparam name="TKey">The builder element contract.</typeparam>
    /// <typeparam name="TValue">The builder element contract.</typeparam>
    /// <param name="builder">The builder being returned, including after an insertion failure.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturnSortedDictionary<TKey, TValue>(ImmutableSortedDictionary<TKey, TValue>.Builder builder) where TKey : notnull
    {
        builder.Clear();
        if (!ReferenceEquals(builder.KeyComparer, Comparer<TKey>.Default)) builder.KeyComparer = Comparer<TKey>.Default;
        if (!ReferenceEquals(builder.ValueComparer, EqualityComparer<TValue>.Default)) builder.ValueComparer = EqualityComparer<TValue>.Default;
        Cache<ImmutableSortedDictionary<TKey, TValue>.Builder>.Store(builder);
    }

    /// <summary>Uses existing bounded atomic storage with one thread-local fast slot.</summary>
    /// <typeparam name="TBuilder">The exact closed framework builder type.</typeparam>
    private static class Cache<TBuilder> where TBuilder : class
    {
        [ThreadStatic] private static TBuilder? t_item;
        private static readonly AtomicSlotPool<TBuilder> s_overflow = new(8);

        /// <summary>Takes exclusive ownership before user code can reenter.</summary>
        /// <returns>A retained empty builder or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TBuilder? Take()
        {
            var item = t_item;
            if (item is null) return s_overflow.TryTake();
            t_item = null;
            return item;
        }

        /// <summary>Retains only cleared builders; overflow is bounded and can be collected.</summary>
        /// <param name="item">The exclusively owned empty builder.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Store(TBuilder item)
        {
            if (t_item is null) t_item = item;
            else s_overflow.TryStore(item);
        }
    }
}
