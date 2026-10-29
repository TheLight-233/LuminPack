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
| `List<T>` | Keeps its capacity and existing elements; grows when needed and clears removed references when it shrinks. |
| `Dictionary<K,V>` | Keeps its comparer and reuses values by matching incoming keys; removed keys disappear after a successful read. |
| `SortedList<string,T>` | Reuses a value when the incoming key at the same position is ordinally equal. |
| Sequence interfaces (`IEnumerable<T>`, `ICollection<T>`, `IList<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`) | Reuse an exact `List<T>` or `T[]` behind them, for string, scalar and ordinary object elements. |
| `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` | Reuse an exact `Dictionary<K,V>` behind them. |
| `Memory<T>`, `ArraySegment<T>` | Reuse array-backed storage of the same length, keeping offset and count. |
| Immutable sets of scalars or strings | Kept when the incoming contents are identical. |
| Strings | An unchanged string keeps its reference, in every string profile. |
| Union | Only a case of the exact same type is reused; another tag constructs the new case. |
| Version-tolerant objects | Reused, with missing members reset to `default`. |
| Reference-tracked objects | Reused while preserving incoming shared references and cycles. |

Derived, covariant and unknown collection implementations are replaced, not inspected. `SortedSet<T>` of reference objects is always rebuilt, because mutating an element that user code re-inserted could corrupt the tree.

When the objects, collection storage and strings already match, repeated reads reach **0 bytes of managed allocation** after warm-up. New objects, larger storage, different array lengths, changed strings, immutable framework values and user callbacks still allocate.

### Rules to know

- Reuse is not transactional. If a read fails, or a callback throws, members already read stay updated.
- An ordinary `null` object record leaves a supplied root untouched. Specialized modes and collections follow their own null rules.
- If two members of your target share one instance, both reads fill that same instance. Use `CircleReference` when the incoming identities must be reconstructed.

## JSON reads

JSON accepts the same `ref` usage:

```csharp
var target = new Message();
LuminPackSerializer.DeserializeJson(json, ref target);
LuminPackSerializer.DeserializeJson(nextJson, ref target);
```

The concrete and generic overloads accept a `string`, `ReadOnlySpan<char>`, `ReadOnlySpan<byte>` or `LuminBufferWriter`. They reuse existing objects, including private storage and constructor-bound members, nested objects, list elements, exact arrays of the same length, and dictionary values matched by incoming keys. Sequence interfaces reuse exact `List<T>` or array storage; dictionary interfaces reuse an exact `Dictionary<K,V>`. Mutable array-backed `Memory<T>` and `ArraySegment<T>` retain their slices when lengths match. An existing pooled instance is filled without another rent. Union cases reuse only an exact matching case, and reference-tracked objects reconstruct incoming shared references and cycles.

Unchanged strings keep their references after JSON decoding, including escaped text, in UTF-8 and UTF-16. Matching object, list, array and string graphs can be read without managed allocations after warm-up; text transcoding, changed strings, different shapes, deep grammar storage and user code may allocate. Immutable collection formatters keep their existing construction behavior.

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

For ordinary (`Object` mode) types, both binary and JSON reads rent an instance whenever a new one is needed, then enter the same reuse path used by `ref` reads. This includes the APIs without `ref`, such as `Deserialize<T>(payload)` and `DeserializeJson<T>(json)`, as well as missing child objects. Anything the rented object still holds (lists, children, strings) is reused, so a warm pool with matching storage can give allocation-free reads without an explicit `ref` target too.

LuminPack only rents. Returning instances is your application's job. Passing an already rented instance by `ref` reuses it without renting another one.

## Output buffers

`LuminBufferWriter` stores its data in a pooled managed array. Always dispose it, or return a rented one to its pool:

```csharp
using var buffer = new LuminBufferWriter(true);
LuminPackSerializer.Serialize(value, buffer);
ReadOnlySpan<byte> bytes = buffer.GetSpan();
```

Writes into a buffer you own append at its cursor; see [Getting started](getting-started.md#reusing-a-buffer) for the reset methods. The `byte[]`, `string`, stream and `IBufferWriter<byte>` overloads manage their own pooled buffers.
