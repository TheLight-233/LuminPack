# Next generator formatter contracts

[Chinese](next-formatters.cn.md)

The Next generator analyzes a closed type into a value-equal `FormatterModel`. Its arguments recursively describe the element operations. The graph exists during generation and publishes concrete static owners. Generated code calls each child owner's `Write` or `Read` operation with the caller's compiled profile and operation state.

## Element contracts

`Raw` describes a standalone raw value operation. `RawCollection` describes the existing contiguous storage representation. These facts can differ for `Nullable<T>` and `KeyValuePair<TKey, TValue>`. `Layout` carries the recursive ABI fields, explicit offsets, packed width and native layout models. Container implementations select their established scalar or contiguous contract.

`GenericIdentity` retains balanced nested argument lists. Tuple, pair, nullable, lazy, grouping and memory owners use the formatter contract of each argument. `Tuple` and `ValueTuple` preserve their eighth `Rest` argument. Framework scalar owners retain their existing JSON forms.

Specialized scalar, string and packable templates remain available. Every registered family has a general element-owner route. Examples include `ConcurrentQueue<List<int[]>>`, `Dictionary<string, Tuple<MyClass, List<MyStruct>>>` and `Memory<Nullable<MyStruct>>`, subject to the referenced payload body being modeled.

## Unmanaged storage

Unmanaged analysis keeps the declared `StructLayout` strategy and resolves closed generic arguments before choosing raw storage. A Custom contract anywhere in the storage graph excludes the raw path.

- `Native` retains the complete native representation.
- `Packed` transfers the analyzed scalar fields through ABI-preserving storage mirrors.
- `Tagged` retains the existing native layout models and the verified `LuminStorageTag` transfer for contiguous payloads. Standalone portable values retain their packed field representation.

Tuple components and nested aggregates retain their physical fields. Empty aggregates retain their zero-width packed contract. Portable blocks use the existing vectorized transfer and field-wise tail. Reader validation precedes allocation, reuse mutation and cursor advancement. Scalar kernels and `LuminPackMarshal.CopyBlockUnaligned` retain the 32-bit access contracts.

## Adding a family

1. Register the open definition and the default renderer in `FormatterRegistry`.
2. Implement the container traversal, construction and reuse behavior in its formatter directory. Use the element and entry context methods for child operations.
3. Describe contiguous storage explicitly when its representation differs from standalone element operations.
4. Capture optional runtime APIs in analysis using their complete public signatures. Carry the resulting capability names into the model and retain the original fallback.
5. Retain pool cleanup, failed-read prefixes, callbacks, comparer behavior, null replacement and cursor publication from the original implementation.
6. Publish operations through `FormatterOwnerFile` or `SpecialOwnerFile`, which honor `GeneratedOperations`.

The default route recursively publishes its dependencies. An extra closed element combination uses the existing container implementation. The registry uses open type identities and capability names, allowing additional families without a type-family bit limit.

## Payload and pipeline boundaries

A payload owner is referenced when its modeled body has an implementation. `CanPublish` checks that boundary before a collection publishes a child call. External and generic-Union contracts currently use the explicit typed registration boundary. Registration-disabled operations retain their throwing behavior.

Serializer facade generation, standalone-call discovery, static entry binding, generic Union publication, compiled input policies and the remaining payload-body protocols still require generator integration. The final compatibility review covers generated consumers, legacy cross reads, existing targets and conditional runtime assets.
