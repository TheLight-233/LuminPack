using System;
using System.Runtime.CompilerServices;

namespace LuminPack.Mapping;

/// <summary>创建型映射委托：从 TSource 创建新的 TDest</summary>
public delegate TDest LuminMapFunc<TSource, TDest>(in TSource source);

/// <summary>就地映射委托：将 TSource 的字段写入已有 TDest 实例</summary>
public delegate void LuminMapIntoFunc<TSource, TDest>(in TSource source, ref TDest dest);


public static class LuminMapper
{
    /// <summary>
    /// 将 <paramref name="source"/> 映射为新的 <typeparamref name="TDest"/> 实例。
    /// 若 TDest 是 class，会产生一次 new() 分配；若 TDest 是 struct 则零分配。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TDest Map<TSource, TDest>(in TSource source)
    {
        var func = MapperCache<TSource, TDest>.MapFunc;

        if (func is null)
            ThrowNotRegistered<TSource, TDest>();

        return func!(in source);
    }

    /// <summary>
    /// 将 <paramref name="source"/> 的字段写入已有的 <paramref name="dest"/> 实例。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MapInto<TSource, TDest>(in TSource source, ref TDest dest)
    {
        var func = MapperCache<TSource, TDest>.MapIntoFunc;
        
        if (func is null)
            ThrowNotRegistered<TSource, TDest>();
        
        func!(in source, ref dest);
    }

    /// <summary>
    /// 注册一对映射函数。由生成的 <c>GeneratedMappersRegistry</c> 在启动时调用，
    /// 用户代码无需手动调用。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<TSource, TDest>(
        LuminMapFunc<TSource, TDest>    mapFunc,
        LuminMapIntoFunc<TSource, TDest> mapIntoFunc)
    {
        MapperCache<TSource, TDest>.MapFunc     = mapFunc;
        MapperCache<TSource, TDest>.MapIntoFunc = mapIntoFunc;
    }

    /// <summary>仅注册创建型映射（当目标类型不支持就地写入时）</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterMapOnly<TSource, TDest>(LuminMapFunc<TSource, TDest> mapFunc)
    {
        MapperCache<TSource, TDest>.MapFunc = mapFunc;
    }

    /// <summary>检查某对类型映射是否已注册</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegistered<TSource, TDest>()
        => MapperCache<TSource, TDest>.MapFunc is not null;

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowNotRegistered<TSource, TDest>()
        => throw new InvalidOperationException(
            $"[LuminMapper] No mapper registered for {typeof(TSource).FullName} → {typeof(TDest).FullName}. " +
            $"Make sure the [LuminMapper] partial class is in the same assembly and the source generator is enabled.");
}

/// <summary>
/// 每对 (TSource, TDest) 类型对的静态映射函数缓存。
/// </summary>
public static class MapperCache<TSource, TDest>
{
    /// <summary>创建型映射函数（可为 null 表示未注册）</summary>
    public static LuminMapFunc<TSource, TDest>?     MapFunc;

    /// <summary>就地映射函数（可为 null 表示未注册或目标不可变）</summary>
    public static LuminMapIntoFunc<TSource, TDest>? MapIntoFunc;
}
