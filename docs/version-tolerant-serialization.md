# Version-tolerant binary contracts

Mark each evolving model with `LuminPackable(GeneratorType.VersionTolerant)`.
Members are identified by their exact, case-sensitive names. They may be added,
removed, or reordered without reserving numeric slots. `LuminPackOrder` remains
optional and controls write order only. Unknown members are skipped by their
recorded payload lengths; missing members receive `default`, including when
deserializing into an existing instance.

```csharp
[LuminPackable(GeneratorType.VersionTolerant)]
public class Player
{
    public int Id;

    [LuminPackOldName("Name")]
    public string? DisplayName;

    public List<int>? Scores;
}
```

`DisplayName` reads records containing either `Name` or `DisplayName`. Binary
writes retain `Name`, allowing an earlier model whose member was called `Name`
to read newly written records. After another rename, keep the history in
chronological attribute order:

```csharp
[LuminPackOldName("Name")]
[LuminPackOldName("DisplayName")]
public string? Label;
```

All names and aliases must be nonempty, valid Unicode and unique across the
complete filtered member contract, including inherited members. Reusing a
removed member's name for a different meaning creates the same binary identity
and must be avoided. `[LuminPackOldName]` requires VersionTolerant mode. JSON
reads also accept these aliases; JSON writes use the current CLR member name.

Nested types that evolve must also use VersionTolerant mode. Apply the same rule
to models used in arrays, collections and generic members. A nested ordinary
Object or raw unmanaged struct retains its own existing representation and does
not acquire field-level evolution from its parent. Keep declared field types,
type namespaces and binary options stable. Matching names with incompatible
type or profile metadata are rejected; automatic numeric conversions and
arbitrary type migrations are not provided.

The `ref` deserialize API retains existing root objects, present child objects,
compatible collection storage and unchanged strings through the same reuse
paths as ordinary binary deserialization. Missing members are cleared instead
of retaining previous values. Warmed reads allocate no managed memory when the
destination graph has sufficient reusable storage and its formatters support
reuse. Construction, pool-rent selection and serialization callbacks retain
their normal behavior. Skipping an unknown field does not execute that field's
formatter; reference ids defined only inside a skipped field cannot establish
objects for later shared references.

## Binary framing

A null reference is the single byte `255`. A non-null record starts with four
bytes: marker `251`, format revision `1`, binary string profile, member count
(0 through 249). Profile bits contain string encoding in bit 1 and string
recording in bit 0. The input profile must match the selected read profile.

Each metadata entry contains a UTF-8 name and a UTF-8 type/representation contract.
The complete metadata block is followed by all payload lengths in entry order,
then all payloads in that same order. Metadata byte strings have unsigned base-128
length prefixes; payload lengths retain LuminPack's signed type-code VarInt encoding.
Comparisons use complete
metadata bytes, so hash collisions cannot alias fields. Duplicate known
identities, incompatible contracts, invalid framing and mismatched completed
field lengths are rejected.

For a matching schema, generated reads keep variable payload lengths in local
values. Fixed primitive schemas can compare their complete constant length table
together with the metadata. Both paths retain the same written length table and
validate the aggregate input boundary before reading fixed payloads.

Generated caches own metadata once per model or closed generic construction.
Reading a matching table compares cached metadata directly against the input;
it does not decode names into strings or discover fields through reflection.
Type identity construction uses type metadata only during cache initialization.
All-unmanaged writes retain fixed field batches and architecture-specific raw
access. Variable-length writes retain the pooled staging buffer and shared
operation state. The size evaluator accounts for the complete table and actual
payloads. Dynamic framing and payload boundaries are validated without adding
Release checks to the existing fixed-value kernels.
