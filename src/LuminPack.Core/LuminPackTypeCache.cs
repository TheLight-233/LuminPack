namespace LuminPack;

/// <summary>
/// Per-closed-type cache of the compact formatter id assigned by a generated static constructor.
/// Ids are 0,1,2,3... (dense, jump-table friendly). <see cref="uint.MaxValue"/> means "no generated
/// formatter for T", so the generic dispatch falls back to the manual registration registry
/// (<see cref="LuminPack.Code.LuminPackFormatterRegistry"/>).
/// </summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
public static class LuminPackTypeCache<T>
{
    /// <summary>Stores the type id.</summary>
    public static uint TypeId = uint.MaxValue;
}
