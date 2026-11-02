# 🛡️ Security

[Chinese](security.cn.md) · [Back to README](../README.md)

LuminPack is a typed serializer with performance-oriented native storage paths. Its defaults target compatible, trusted producers. Bounds checks alone do not make every schema suitable for hostile input. Application schemas, generated code, formatters and optional native kernels are trusted code.

## Runtime support and wire portability

The runtime supplies netstandard2.1 and modern .NET assets. Runtime availability and wire portability are separate guarantees. The netstandard2.1 asset uses pointer cursors for Unity/Mono/IL2CPP: low-level readers/writers over managed storage require pinning for their complete lifetime. Generated convenience entry points retain their existing pinning and cleanup.

Native primitives and raw unmanaged storage retain process byte order and representation. Raw structs can contain padding, runtime-dependent layout and pointer-sized members. Avoid `IntPtr`, `UIntPtr`, pointers, runtime-sized vectors and unanalyzable native external structs in portable persistence/network schemas. Matching type names do not establish a matching ABI across runtime versions.

Packed portable structs remove inter-field padding in declaration order. Tagged collections model supported layout differences and convert semantic field bytes using actual field addresses. Neither is a general endian conversion protocol. Cross-machine exchange requires matching schemas, framing/string options, supported representations and compatible byte order, verified with retained fixtures on the actual targets. Version-tolerant objects do not negotiate all these conditions automatically.

Native padding can contain unspecified or previously used bytes. `Native` copies the complete storage. `Tagged` may also copy complete storage, including padding, when a modeled layout matches the running layout; a layout stable across models can retain a headerless native representation. These decisions establish layout compatibility, not a no-padding or trusted-input check. `Packed` writes declaration-ordered semantic field bytes and excludes inter-field padding. Raw padded structs can disclose padding bytes or produce different byte sequences for equal field values; clearing the output buffer does not clear source padding. Public protocols should use reviewed field-oriented schemas or a canonical application wire format. Changing established native wire bytes requires a separate compatibility decision.

## Untrusted input

### Untrusted reads by default

Generated binary reads treat their input as untrusted unless a declaration says otherwise. Records keep the ordinary wire format and writers; only the generated reads change. They check the input where it is read, with limits frozen into the generated code as constants:

- **Bounds.** The Reader checks every load against the input in Debug and Release. A collection or string count can never require more bytes than remain, so allocations stay proportional to the input actually consumed.
- **Object markers.** An object's member marker must equal its declared member count or the null marker.
- **Boolean domains.** Boolean members, Boolean arrays and `List<bool>` reject every storage value except `0` and `1`. Bulk storage uses bounded vector loads on modern .NET and word groups on every target.
- **Version-tolerant records.** Prefixes, member counts, member widths and payload lengths are checked against the input and the reader's model, and every member read must end exactly at its recorded length.
- **Reference tracking.** A back reference must name an object already read, of the type expected at that position; a raw slot must record its exact width. Unknown slots are skipped by their recorded lengths.
- **Unions.** An undeclared tag is rejected. Case payloads are read with the same checks, under the enclosing root's limits.
- **Compressed collections.** The declared element count must match the compressed frame and cannot exceed what its encoded bytes can produce, before any storage is allocated.

Limits are unbounded until declared. Declare them on an assembly or a generated root type with `LuminPackOption`:

```csharp
[LuminPackable]
[LuminPackOption(MaxInputBytes = 1024 * 1024, MaxCollectionLength = 10_000, MaxStringLength = 4096, MaxDepth = 32)]
public partial class Request
{
    public int Id;
    public string Name = "";
}
```

`MaxCollectionLength` bounds each binary collection and `MaxStringLength` each binary string, in UTF-16 code units. `MaxDepth` bounds the nesting of recursive values; the generator counts depth only in recursive types, because every other graph has a finite static depth. Without it, recursive reads keep their stack guard. `MaxInputBytes` caps the input accepted by the generated facades, including stream and sequence staging. An unbounded limit is a constant the JIT removes, so undeclared limits cost nothing. Limits must be nonnegative; zero rejects any value requiring that resource. Assembly limits are inherited and explicit type limits override them. Nested types read through a root use the root's limits; a type read through a trusted parent uses its own. `MaxDecompressedBytes`, `MaxAllocationBytes`, `MaxObjectCount` and `MaxReadWork` are obsolete and have no effect. Safety declarations on individual members are rejected.

A declaration that explicitly selects hostile input, with `UntrustedData = true` or any `Max*` limit, also reads JSON with the strict grammar (`ValidateJsonSyntax`) and, when declared, the `MaxDepth` document depth, in addition to `MaxInputBytes`. Without such a declaration JSON keeps the declared profile. Collection and string limits apply to binary records; JSON values are bounded by the document size.

Declare `UntrustedData = false` on an assembly or type to read trusted data with the unchecked reads and the lenient JSON grammar. A trusted parent can contain an untrusted child; the child keeps its own checks.

Every generated shape is accepted: objects, version-tolerant and reference-tracked records, Unions, generic models, framework values and collections, compressed members and raw unmanaged structs. Custom formats and manually registered formatters are application code and stay trusted. Only input-selected types (`object`, `System.Type`, delegates, reflection types and nongeneric framework interfaces) fail closed with `NotSupportedException`; declare `UntrustedData = false` where such a model only reads trusted data.

The checks are not a schema or authenticity proof. Enum values, characters, decimal and DateTime storage, raw unmanaged structs (including their Boolean fields), raw nullable elements, Booleans inside other framework containers and string contents (lone UTF-16 surrogates) are accepted as stored; UTF-8 strings are strictly decoded as in every record. Dictionary and set reads use their framework equality; a hostile key set can still collide in a hash table, so declare `MaxCollectionLength` in proportion to the work you accept. A collection of zero-width Packed structs consumes no payload bytes per element, so only `MaxCollectionLength` bounds it. Application constructors, callbacks, comparers, custom formatters and native extensions remain trusted code.

The caller must keep the input and its backing storage stable for the entire synchronous operation, including application callbacks. Decoding is not transactional: malformed data discovered late can leave completed fields or reusable storage updated, and callbacks that already ran can have side effects. A nested object is published to its parent only after its own read succeeds. Decode into an isolated application object when failure must not change shared state.

### Existing trusted and low-level contracts

Public advancing Reader `EnsureReadable`, existing `Advance` checks, fixed preaccess checks, Union framing and dynamic collection/string payload checks remain enabled in Release. A span facade rejects short fixed input before loading data or mutating its destination. Address-only kernels cannot infer allocation bounds and retain caller-owned valid-range contracts. Fixed Writer stores require reserved output storage.

Bounded raw collection APIs reject elements containing managed references. Such types need generated/specialized formatters. Passing managed-reference storage to unchecked raw address kernels, overlapping ranges to a non-overlapping copy, stale pointers or unpinned cursor storage violates their contracts. Do not expose input-controlled pointer/type selection to these APIs.

Trusted operations (`UntrustedData = false`) do not check the object markers, Boolean domains, collection, string and depth limits described above. Valid input can still consume substantial resources. Stack sufficiency and compression plausibility checks are not application quotas. Bound encoded frame size, declared decompressed size before allocation, application collection sizes and request/process resources. Catching memory or stack exhaustion is not a resource-control strategy.

A packed empty struct occupies zero payload bytes. A valid collection of those elements can therefore contain only its count and framing, irrespective of its element count. Generated readers preserve that representation and use a zero minimum only for a proven empty packed contract. An encoded input-size cap alone cannot bound such a collection's allocations or traversal work; an independent element/work quota is required at an untrusted boundary.

Compressed raw collections require the frame's declared decoded byte width to equal the checked element count times the storage width, before allocating or writing a destination. A valid shorter compression frame is not a valid complete collection payload. Every read also rejects a declared width larger than 256 times the encoded payload, which LuminZ cannot produce, before allocation. These consistency checks do not replace an application decompression quota.

The default JSON profile has `ValidateJsonSyntax = false` and depth 1024. An explicit hostile-input declaration selects the strict grammar; otherwise select `ValidateJsonSyntax = true` and an appropriate depth for external JSON. Strict grammar still requires application resource and schema limits.

Approved collection fast paths assume producer invariants. Raw set bucket reconstruction does not promise duplicate rejection; adversarial hash collisions and application comparers can be expensive. Raw scalar bits are not domain validation for every framework type. Check uniqueness and domain invariants before using decoded data for correctness or authorization.

Constructors, callbacks, pooling/reuse handlers and custom formatters can execute application code during reading. Failed reads can preserve completed fields according to existing publication rules. Deserialization is neither transactional nor an execution sandbox. Validate a boundary object before publishing it into application state.

`System.Type` deserialization can resolve input-supplied names on dynamically enabled configurations. Exclude it from hostile-input schemas or use a closed application identifier mapping. A dynamic-code capability guard is not a type allowlist; name resolution alone also does not establish arbitrary object construction or code execution.

## Authentication and native extensions

Serialization provides no authenticity or confidentiality. The separate ChaCha20-Poly1305 helper verifies authentication before decrypting into the destination. Keep its tag comparison on the existing constant-time path. Key management, nonce uniqueness per key and authenticated protocol context remain application responsibilities. Use the standard 256-bit-key RFC 8439 construction for interoperability.

The netstandard2.1-only `LuminPackBurst` table is a trusted native extension point, never initialized from payload data. Its pointers must obey [the native hook contract](burst-hooks.md). Managed count checks cannot undo an incorrect native memory access. Enable only a backend verified on the actual runtime and CPU.

Compilation, synthetic layouts and configured CI jobs do not prove actual execution on every target. Follow [the SIMD rules](mechanism-contract.md#4-simd).
