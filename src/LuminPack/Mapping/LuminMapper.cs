using System.Runtime.CompilerServices;

namespace LuminPack.Mapping;

/// <summary>Creates a new TDest instance from a TSource value.</summary>
/// <param name="source">The input data to process.</param>
/// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
/// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
/// <returns>The result of the operation.</returns>
public delegate TDest LuminMapFunc<TSource, TDest>(in TSource source);

/// <summary>Copies TSource members into an existing TDest instance.</summary>
/// <param name="source">The input data to process.</param>
/// <param name="dest">The destination receiving the result.</param>
/// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
/// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
public delegate void LuminMapIntoFunc<TSource, TDest>(in TSource source, ref TDest dest);


/// <summary>Provides lumin mapper functionality.</summary>
public static class LuminMapper
{
    /// <summary>
    /// Maps <paramref name="source"/> into a new <typeparamref name="TDest"/> instance.
    /// Allocates one destination instance for a class; struct destinations need no heap allocation.
    /// </summary>
    /// <param name="source">The input data to process.</param>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TDest Map<TSource, TDest>(in TSource source)
    {
        var func = MapperCache<TSource, TDest>.MapFunc;

        if (func is null)
            ThrowNotRegistered<TSource, TDest>();

        return func!(in source);
    }

    /// <summary>
    /// Copies members from <paramref name="source"/> into the existing <paramref name="dest"/> instance.
    /// </summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="dest">The destination receiving the result.</param>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MapInto<TSource, TDest>(in TSource source, ref TDest dest)
    {
        var func = MapperCache<TSource, TDest>.MapIntoFunc;

        if (func is null)
            ThrowNotRegistered<TSource, TDest>();

        func!(in source, ref dest);
    }

    /// <summary>
    /// Registers creating and in-place mapping functions; generated <c>GeneratedMappersRegistry</c> invokes this at startup.
    /// Application code does not need to invoke this registration directly.
    /// </summary>
    /// <param name="mapFunc">The map func used by this operation.</param>
    /// <param name="mapIntoFunc">The map into func used by this operation.</param>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<TSource, TDest>(
        LuminMapFunc<TSource, TDest> mapFunc,
        LuminMapIntoFunc<TSource, TDest> mapIntoFunc)
    {
        MapperCache<TSource, TDest>.MapFunc = mapFunc;
        MapperCache<TSource, TDest>.MapIntoFunc = mapIntoFunc;
    }

    /// <summary>Registers a creating mapper when the destination does not support in-place updates.</summary>
    /// <param name="mapFunc">The map func used by this operation.</param>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterMapOnly<TSource, TDest>(LuminMapFunc<TSource, TDest> mapFunc)
    {
        MapperCache<TSource, TDest>.MapFunc = mapFunc;
    }

    /// <summary>Determines whether a mapping is registered for the source and destination type pair.</summary>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegistered<TSource, TDest>()
        => MapperCache<TSource, TDest>.MapFunc is not null;

    /// <summary>Raises the exception for not registered.</summary>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowNotRegistered<TSource, TDest>()
        => global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(
            $"[LuminMapper] No mapper registered for {typeof(TSource).FullName} → {typeof(TDest).FullName}. " +
            $"Make sure the [LuminMapper] partial class is in the same assembly and the source generator is enabled.");
}

/// <summary>
/// Stores mapping functions in generic static state for each source and destination type pair.
/// </summary>
/// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
/// <typeparam name="TDest">The type represented by <c>TDest</c>.</typeparam>
public static class MapperCache<TSource, TDest>
{
    /// <summary>The creating mapper, or null when no creating mapping is registered.</summary>
    public static LuminMapFunc<TSource, TDest>? MapFunc;

    /// <summary>The in-place mapper, or null when unregistered or when the destination is immutable.</summary>
    public static LuminMapIntoFunc<TSource, TDest>? MapIntoFunc;
}
