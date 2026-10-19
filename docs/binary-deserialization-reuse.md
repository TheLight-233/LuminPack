# Reusing binary deserialization targets

Pass an existing target by reference to populate its existing object graph:

```csharp
var target = new Message();
LuminPackSerializer.Deserialize<Message>(payload, ref target);
```

Ordinary objects retain their root identity. Non-null nested objects, automatic-property storage and constructor-bound members are populated through their established storage paths. A missing instance uses the selected constructor or pool. An already rented instance can be passed with `ref`; reading it does not rent a replacement. The fresh API retains its existing constructor and pool timing.

Arrays retain their storage when the exact array type and length match. Lists retain capacity and existing prefix elements, grow when required, and clear removed references when shrinking. A dictionary retains its comparer and reuses values by matching incoming keys; removed keys disappear after a successful read. Mutable reference keys still use their formatter's normal construction rules. Temporary dictionary records use cleared pooled storage.

On modern .NET, a dictionary with the default int comparer can refill existing non-null leaf values through their entry slots. The generator requires a local sealed non-generic class with field-only primitive/string payload and no callbacks or specialized user operations. Ordinary and named version-tolerant leaves qualify; Unity, custom comparers and other shapes keep staged reconstruction. Missing or null slots return to staging, preserving the parsed prefix before constructors or pools can mutate the dictionary. Existing slot references never survive those user operations.

Slot refill can retain the old enumeration order and existing enumerators. Partial slot changes can remain visible after failure; duplicate incoming keys still fail. Changed-shape cleanup, including a different incoming key order, may allocate even when the warmed path with matching key order does not. Empty records retain their original Clear behavior. The fresh dictionary bucket-building path is unchanged.

An unchanged string retains its existing reference in each binary string profile, including non-ASCII text. Changed strings require normal immutable-string allocation. Length profiles preserve distinct null and empty values; token profiles retain their established empty-token normalization.

Union cases reuse only an exact matching reference type. A different tag constructs the selected case. Version-tolerant reads preserve the length table and reset absent slots to defaults. Reference-tracked reads register a reused object before reading its children, preserve incoming shared references and cycles, and construct a distinct object when an old target alias would collapse distinct incoming records.

Ordinary mode retains aliases already present in the target graph. If two target members share one instance, both reads populate that instance. Use reference-tracked records when incoming object identities must be reconstructed.

Generic members use the existing compact type dispatch and each known formatter's null framing. An opaque registered reference formatter retains its original fresh-member boundary. Null elements still enter their formatter so static callback counts are preserved.

Repeated reads can allocate **0 managed bytes** after initialization when objects and collection storage are available and strings are unchanged. New objects, larger storage, different array lengths, changed strings, immutable framework values, opaque generic value storage, boxed Union value cases and user formatters or callbacks may allocate. Reuse does not make those allocations disappear.

The existing root null rules are unchanged: an ordinary null-object record leaves a supplied root untouched; specialized mode and collection null records use their own default/null behavior. A successful read returns the original consumed-byte count. Ordinary object members are read directly into their existing storage, so a failed parse or a callback can leave already-read root members as well as reused child objects and collections partially updated. This is not a transactional object-graph update.

Ordinary reuse avoids storing an identical non-volatile nested reference back into its existing field and collection slot. String records still undergo their complete comparison and validation. A changed or newly constructed reference is published where it is read; ordinary root member storage is written directly, so user code that replaces a member during parsing keeps its replacement. Named version framing keeps its staged member locals and its late commit. Volatile accesses retain their original stores.

Version-tolerant completion retains one receiver throughout member assignment and its completion callbacks, including when the caller's reference slot is itself a serialized member of that receiver.

This behavior applies to binary reads. JSON retains its separate reader and string-decoding contract.
