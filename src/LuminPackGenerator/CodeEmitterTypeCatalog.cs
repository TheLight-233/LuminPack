using System.Collections.Generic;

namespace LuminPack.SourceGenerator;

/// <summary>Provides code emitter type catalog functionality.</summary>
internal static class CodeEmitterTypeCatalog
{
    /// <summary>Stores the supported metadata names.</summary>
    public static readonly HashSet<string> SupportedMetadataNames = new HashSet<string>()
    {
        // Primitive types.
        "nint",
        "nuint",
        "System.IntPtr",
        "System.UIntPtr",
        "System.Uri",
        "System.Version",
        "System.Text.StringBuilder",
        "System.Type",
        "System.TimeZoneInfo",
        "System.Collections.BitArray",
        "System.Globalization.CultureInfo",
        "System.Numerics.BigInteger",
        "System.Guid", "System.DateTime",
        "System.DateTimeOffset", "System.TimeSpan",
        "System.Numerics.Complex",
        "System.Numerics.Matrix3x2",
        "System.Numerics.Matrix4x4",
        "System.Numerics.Plane",
        "System.Numerics.Quaternion",
        "System.Numerics.Vector2",
        "System.Numerics.Vector3",
        "System.Numerics.Vector4",

        // .Net8+
        "global::System.Half", "global::System.Int128", "global::System.UInt128",
        "global::System.DateOnly", "global::System.TimeOnly", "global::System.Text.Rune",

        // Memory and array helpers.
        "System.Memory`1",
        "System.ReadOnlyMemory`1",
        "System.ArraySegment`1",
        "System.Buffers.ReadOnlySequence`1",

        // Generic wrapper type.
        "System.Nullable`1",
        "System.Lazy`1",
        "System.Collections.Generic.KeyValuePair`2",

        // Tuples with one to seven elements.
        "System.Tuple`1",
        "System.Tuple`2",
        "System.Tuple`3",
        "System.Tuple`4",
        "System.Tuple`5",
        "System.Tuple`6",
        "System.Tuple`7",

        "System.ValueTuple`1",
        "System.ValueTuple`2",
        "System.ValueTuple`3",
        "System.ValueTuple`4",
        "System.ValueTuple`5",
        "System.ValueTuple`6",
        "System.ValueTuple`7",

        // Collection classes.
        //"System.Collections.Generic.List`1",
        "System.Collections.Generic.LinkedList`1",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.PriorityQueue`2",

        //Frozen collections.
        "System.Collections.Frozen.FrozenDictionary`2",
        "System.Collections.Frozen.FrozenSet`1",

        // Dictionary types.
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.ObjectModel.ReadOnlyDictionary`2",

        // Collection base types.
        "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.ReadOnlyCollection`1",
        "System.Collections.ObjectModel.ObservableCollection`1",
        "System.Collections.ObjectModel.ReadOnlyObservableCollection`1",

        // Collection interfaces.
        "System.Collections.Generic.IEnumerable`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.IReadOnlyList`1",
        "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2",

        // Concurrent collections.
        "System.Collections.Concurrent.ConcurrentBag`1",
        "System.Collections.Concurrent.ConcurrentQueue`1",
        "System.Collections.Concurrent.ConcurrentStack`1",
        "System.Collections.Concurrent.ConcurrentDictionary`2",
        "System.Collections.Concurrent.BlockingCollection`1",

        // Immutable collections.
        "System.Collections.Immutable.ImmutableList`1",
        "System.Collections.Immutable.ImmutableDictionary`2",
        "System.Collections.Immutable.ImmutableArray`1",
        "System.Collections.Immutable.ImmutableQueue`1",
        "System.Collections.Immutable.ImmutableStack`1",
        "System.Collections.Immutable.ImmutableHashSet`1",
        "System.Collections.Immutable.ImmutableSortedDictionary`2",
        "System.Collections.Immutable.ImmutableSortedSet`1",
        "System.Collections.Immutable.IImmutableList`1",
        "System.Collections.Immutable.IImmutableDictionary`2",
        "System.Collections.Immutable.IImmutableSet`1",
        "System.Collections.Immutable.IImmutableQueue`1",
        "System.Collections.Immutable.IImmutableStack`1",
        "System.Collections.Immutable.ReadOnlyCollectionBuilder`1",

    };
}
