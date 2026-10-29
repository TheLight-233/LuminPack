# 🎯 Defining Types

[Chinese](defining-types.cn.md) · [Back to README](../README.md)

## `[LuminPackable]`

Mark a `class`, `abstract class`, `struct`, `record`, `record struct` or `interface` with `[LuminPackable]`. The source generator then emits its binary and JSON operations at compile time.

```csharp
using LuminPack.Attribute;

[LuminPackable]
public partial class Player
{
    public int Id;
    public string Name { get; set; } = "";
    public List<int> Scores = new();
}
```

Union roots and their cases, together with every containing type, must be `partial` so that virtual dispatch can be generated (diagnostic `LuminPack035`). Declaring other types `partial` is recommended.

A struct or record struct that contains no references (a C# unmanaged type) is copied directly from memory. See [32/64-bit struct layouts](options.md#struct-layout) for the storage choices of such structs.

`GeneratorType` selects the representation:

| Mode | Use |
| --- | --- |
| `Object` (default) | Positional members. The fastest and most compact form; use it for stable models. |
| `VersionTolerant` | Members identified by name; fields can be added, removed, reordered and renamed. See [Version tolerance](versioning.md). |
| `CircleReference` | Reference tracking with shared objects and cycles. See [Reference tracking](versioning.md#reference-tracking). |
| `Custom` | Your own static read/write methods. See [Custom formatters](custom-formatters.md). |
| `NonGenerator` | No generated formatter for the type. |

## Members

By default, public instance fields and public auto-properties (including those with a private setter) are serialized. Computed properties have no storage and are never serialized.

| Attribute | Effect |
| --- | --- |
| `[LuminPackIgnore]` | Excludes a public member. |
| `[LuminPackInclude]` | Includes a private or protected field or property. |
| `[LuminPackOrder(n)]` | Sets the write order. It never changes the CLR layout. |
| `[LuminPackableObject]` | Calls the member type's own complete formatter instead of inlining its members. Use it for `Custom` and other specialized members. |
| `[LuminPackCompress]` | Compresses a supported unmanaged array or span member with LuminPack LZ. |
| `[LuminPackDelegate]`, `[LuminPackStaticDelegate]`, `[LuminPackSingletonDelegate]` | Routes one member through your own method on an instance, a static type, or a singleton. |

```csharp
[LuminPackable]
public partial class Inventory
{
    [LuminPackInclude] private int _version;
    [LuminPackIgnore] public int CachedTotal;

    [LuminPackOrder(1)] public string Owner = "";
    [LuminPackOrder(0)] public long Id;
}
```

The `Object` mode writes members by position, parents before children, in declaration order or `LuminPackOrder` order. A reader must use the same members in the same order. An object can have 0 to 249 members.

`[LuminPackInclude]` works at every nesting level: private members of a nested type marked `[LuminPackInclude]` are picked up the same way. Private members are accessed by generated code, never by reflection, on .NET Standard 2.1 and modern .NET alike.

### Construction

LuminPack does not require a parameterless constructor. A constructor marked `[LuminPackConstructor]`, or otherwise the selected constructor, must name its parameters after serialized members; those members are passed to it. Deserialization into an existing object (see [Reuse](reuse.md)) does not construct the root at all.

A static method marked `[LuminPackPoolRent]` replaces `new` whenever an instance is needed. In binary reads of `Object`-mode types the rented object is then filled in place, exactly like a read into an existing object, so anything it already holds (lists, child objects, strings) is reused:

```csharp
[LuminPackPoolRent]
public static Player Rent() => PlayerPool.Rent();
```

LuminPack never returns instances to your pool; your application owns the pool and its return policy.

### Callbacks

Instance or static methods marked with these attributes run around each operation:

```csharp
[LuminPackOnSerializing]   public void BeforeWrite() { }
[LuminPackOnSerialized]    public void AfterWrite() { }
[LuminPackOnDeserializing] public static void BeforeRead() { }
[LuminPackOnDeserialized]  public void AfterRead() { }
```

## Size guarantees

When the complete binary size of a type has a known upper bound, `[LuminPackBounded(maxBytes)]` lets the generated code reserve that bound once and drop every inner capacity check. A collection of such elements reserves `count * maxBytes` once.

```csharp
[LuminPackable, LuminPackBounded(128)]
public sealed partial class SmallRecord
{
    public int Id;
    public string Label = "";      // at most 10 characters in this application
    public int[] Values = [];      // at most 16 elements in this application
}
```

The bound covers headers and all nested data under every string option the type is used with. It is your guarantee: a wrong bound can write past the buffer. The cursor still advances by the actual size, and the wire format is unchanged. Do not use it for types without a reliable limit.

## Supported types

Besides `[LuminPackable]` types, these are built in, for binary and JSON:

| Category | Types |
| --- | --- |
| Primitives | `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`, `float`, `double`, `decimal`, `bool`, `char`, enums |
| .NET 8+ scalars | `Half`, `Int128`, `UInt128`, `Rune`, `DateOnly`, `TimeOnly` |
| Time and identity | `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `TimeZoneInfo` |
| Numerics | `BigInteger`, `Complex`, `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Plane`, `Matrix3x2`, `Matrix4x4` |
| Text and system | `string`, `StringBuilder`, `Uri`, `Version`, `Type`, `BitArray`, `CultureInfo` |
| Wrappers | `Nullable<T>`, `Lazy<T>`, `KeyValuePair<K,V>`, `Tuple<...>` and `ValueTuple<...>` (1 to 7 items) |
| Memory | `T[]`, multidimensional arrays, `Memory<T>`, `ReadOnlyMemory<T>`, `ArraySegment<T>`, `ReadOnlySequence<T>` |
| Collections | `List<T>`, `LinkedList<T>`, `Queue<T>`, `Stack<T>`, `HashSet<T>`, `SortedSet<T>`, `PriorityQueue<T,P>`, `Dictionary<K,V>`, `SortedDictionary<K,V>`, `SortedList<K,V>`, `ReadOnlyDictionary<K,V>` |
| Concurrent | `ConcurrentBag<T>`, `ConcurrentQueue<T>`, `ConcurrentStack<T>`, `ConcurrentDictionary<K,V>`, `BlockingCollection<T>` |
| Object model | `Collection<T>`, `ReadOnlyCollection<T>`, `ObservableCollection<T>`, `ReadOnlyObservableCollection<T>` |
| Immutable and frozen | `ImmutableArray`, `ImmutableList`, `ImmutableQueue`, `ImmutableStack`, `ImmutableDictionary`, `ImmutableHashSet`, `ImmutableSortedDictionary`, `ImmutableSortedSet`, `ReadOnlyCollectionBuilder`, `FrozenDictionary`, `FrozenSet` (.NET 8+) |
| Interfaces | `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyCollection<T>`, `IList<T>`, `IReadOnlyList<T>`, `ISet<T>`, `IReadOnlySet<T>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, `ILookup<K,V>`, `IGrouping<K,V>` and the `IImmutable*` interfaces |

Engine and UI value types (Unity, MonoGame, WPF, Avalonia, MAUI, SkiaSharp, System.Drawing, ASP.NET Core) are provided by the optional platform packages listed in the [README](../README.md#-platform-packages).
