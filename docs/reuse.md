# ♻️ Reuse & Zero Allocation

[Chinese](reuse.cn.md) · [Back to README](../README.md)

## Reading into existing objects

Pass an existing target by `ref` and a binary read fills the graph you already have:

```csharp
var target = new Message();
LuminPackSerializer.Deserialize(payload, ref target);
LuminPackSerializer.Deserialize(nextPayload, ref target);   // same objects again
```

What is kept:

| Shape | Behavior |
| --- | --- |
| Objects | The root keeps its identity. Non-null nested objects, auto-property storage and constructor-bound members are filled in place. A missing child uses its constructor or `[LuminPackPoolRent]` method. |
| Arrays | Kept when the exact array type and length match. |
| Binary `ReadOnlyCollection<T>` | Next can refill an exact wrapper over a same-length, nonempty `T[]` of supported raw scalars on probed modern CoreCLR layouts. The wrapper and its backing array both show the updated values after the collection payload is checked. Pass a null target to get a separate wrapper and backing array. |
| Binary `string[,]` | The default UTF-16 length reader (`LuminPackUtf16LengthOptions`) fills an existing matrix when both dimensions match and both lower bounds are zero. Other profiles and shapes allocate a matrix. Aliases see updates in row-major order. If a record is truncated or exceeds a string limit, only previously decoded slots have been updated. |
| `List<T>` | Keeps its capacity and existing elements; grows when needed and clears removed references when it shrinks. |
| `Dictionary<K,V>` | Keeps its comparer and reuses values by matching incoming keys; removed keys disappear after a successful read. |
| Binary `ReadOnlyDictionary<string, int>` and `ReadOnlyDictionary<string, string>` | Next can update the values of an exact wrapper over an exact default-comparer dictionary on supported 64-bit CoreCLR layouts. This applies to 1 to 4,096 keys that match in enumeration order with the built-in UTF-16 length profile. Aliases to the wrapper and backing dictionary see the values after the dictionary read succeeds. Pass a null target to read a separate dictionary. |
| `SortedList<K,V>` | Reuses eligible values at the same position when callback-free keys match. String keys use ordinal equality. |
| Binary `SortedList<string, int>` and `SortedList<string, long>` | Next can retain matching string key references in an exact default-comparer list with the same count, from 1 to 4,096 entries, under built-in string profiles on supported runtime layouts. Reuse still clears the list and publishes each complete pair as it reads it. |
| Sequence interfaces (`IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`) | Reuse an exact `List<T>` or `T[]` behind them, for string, scalar and ordinary object elements. |
| `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` | Reuse an exact `Dictionary<K,V>` behind them. |
| `Memory<T>`, `ArraySegment<T>` | Reuse array-backed storage of the same length, keeping offset and count. |
| Binary `ReadOnlyMemory<string>` and `ReadOnlySequence<string>` | With a built-in UTF-16 length profile on modern .NET, Next can reuse matching string references from a same-length array slice when the first string matches. Each result has its own backing array. |
| Immutable sets of scalars or strings | Kept when the incoming contents are identical. |
| Binary `FrozenSet<T>` of supported raw scalars | Next can retain a set after verifying that a prior reuse read on the current thread produced the exact incoming item bytes and order. This optimization applies on supported .NET 10 and 11 runtimes. |
| Strings | An unchanged string keeps its reference, in every string profile. |
| Union | Only a case of the exact same type is reused; another tag constructs the new case. |
| Version-tolerant objects | Reused, with missing members reset to `default`. |
| Reference-tracked objects | Reused while preserving incoming shared references and cycles. |

On supported modern targets, an exact binary dictionary with its default comparer can refill existing slots when its fixed-width key uses callback-free framework equality and the value formatter supports safe in-place reading. Matching keys retain the destination order. A failed read can expose the completed prefix. User-defined key behavior, custom comparers and unsupported value formatters use staged reconstruction. Empty destinations with callback-free keys can use the checked bulk rebuild for raw pairs.

Derived, covariant and unknown collection implementations are replaced, not inspected. `SortedSet<T>` of reference objects is always rebuilt, because mutating an element that user code re-inserted could corrupt the tree.

When the objects, collection storage and strings already match, repeated reads reach **0 bytes of managed allocation** after warm-up. New objects, larger storage, different array lengths, changed strings, immutable framework values and user callbacks still allocate.

### Rules to know

- Reuse is not transactional. If a read fails, or a callback throws, members already read stay updated.
- An ordinary `null` object record leaves a supplied root untouched. Specialized modes and collections follow their own null rules.
- If two members of your target share one instance, both reads fill that same instance. Use `CircleReference` when the incoming identities must be reconstructed.

## Opt-in binary object matrix reuse

With the Next generator on .NET 8 or later, set `ReuseRectangularArraySlots` on the element class of a binary `T[,]` to reuse its existing objects.

```csharp
[LuminPackable, LuminPackOption(ReuseRectangularArraySlots = true)]
public sealed partial class Cell
{
    public List<int> Samples = new();
}
```

Pass the existing matrix by `ref`, or keep it in an object passed by `ref`. The reader checks the shape and count before updating slots in row-major order. The destination must have the exact array type, the incoming dimensions and two zero lower bounds. Aliases can see the completed prefix if a later slot fails. Give each destination slot its own mutable objects, including nested collections. Multiple slots can share immutable values such as strings.

The option applies to ordinary object element types with no inherited payload storage, selected constructor, callbacks or pool rent. The original replacement path handles other array ranks, new destinations and targets that fail the shape or type checks. This option applies only to binary reads.

## JSON reads

JSON accepts the same `ref` usage:

```csharp
var target = new Message();
LuminPackSerializer.DeserializeJson(json, ref target);
LuminPackSerializer.DeserializeJson(nextJson, ref target);
```

The concrete and generic overloads accept a `string`, `ReadOnlySpan<char>`, `ReadOnlySpan<byte>` or `LuminBufferWriter`. They reuse existing objects, including private storage and constructor-bound members, nested objects, list elements, exact arrays of the same length, and dictionary values matched by incoming keys. Sequence interfaces reuse exact `List<T>` or array storage; dictionary interfaces reuse an exact `Dictionary<K,V>`. Mutable array-backed `Memory<T>` and `ArraySegment<T>` retain their slices when lengths match. An existing pooled instance is filled without another rent. Union cases reuse only an exact matching case, and reference-tracked objects reconstruct incoming shared references and cycles.

Binary and JSON reads can reuse the current framework instance for a repeated canonical culture name on validated .NET 10 and 11 CoreCLR runtimes. Each thread retains one name of at most 85 characters and at most one read-only culture. Only an ASCII name confirmed in the framework's name cache is admitted. A weak reference to that cache detects `CultureInfo.ClearCachedData()`; a miss uses `CultureInfo.GetCultureInfo`.

Unchanged strings keep their references after JSON decoding, including escaped text, in UTF-8 and UTF-16. Matching object, list, array and string graphs can be read without managed allocations after warm-up; text transcoding, changed strings, different shapes, deep grammar storage and user code may allocate. After parsing JSON into a concrete `ImmutableSortedSet<T>`, .NET 10 and later can link a balanced frozen tree from two or more strictly ascending elements. Enums and non-generic comparable CoreLib value types use their default ordering. Strings must be non-null and use an ordinary current culture. User comparisons, duplicates, unordered input and unavailable tree bindings use the builder.

JSON `null` clears the supplied reference. Missing object members reset to `default` on an existing target. Fresh reads retain their constructor selection and existing missing-member behavior. Lists remove trailing elements, and dictionaries remove old keys after reading the incoming entries. Reuse is not transactional: nested objects and storage already visited can remain changed after a failure. Encoding, syntax validation and document-end checks still apply to the whole document.

## Instance pools

A static method marked `[LuminPackPoolRent]` supplies new instances instead of `new`:

```csharp
[LuminPackable]
public partial class Bullet
{
    public float X, Y;

    [LuminPackPoolRent]
    public static Bullet Rent() => BulletPool.Rent();
}
```

For ordinary (`Object` mode) types, both binary and JSON reads rent an instance whenever a new one is needed, then enter the same reuse path used by `ref` reads. This includes the APIs without `ref`, such as `Deserialize<T>(payload)` and `DeserializeJson<T>(json)` after explicit/Custom registration, as well as missing child objects. Anything the rented object still holds (lists, children, strings) is reused, so a warm pool with matching storage can give allocation-free reads without an explicit `ref` target too.

LuminPack only rents. Returning instances is your application's job. Passing an already rented instance by `ref` reuses it without renting another one.

## Output buffers

`LuminBufferWriter` stores its data in a pooled managed array. Always dispose it, or return a rented one to its pool:

```csharp
using var buffer = new LuminBufferWriter(true);
LuminPackSerializer.Serialize(value, buffer);
ReadOnlySpan<byte> bytes = buffer.GetSpan();
```

Writes into a buffer you own append at its cursor; see [Getting started](getting-started.md#reusing-a-buffer) for the reset methods. The `byte[]`, `string`, stream and `IBufferWriter<byte>` overloads manage their own pooled buffers.
