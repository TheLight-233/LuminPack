# 🛡️ Security

[Chinese](security.cn.md) · [Back to README](../README.md)

LuminPack is a typed serializer with performance-oriented native storage paths. Its defaults target compatible, trusted producers. Bounds checks alone do not make every schema suitable for hostile input. Application schemas, generated code, formatters and optional native kernels are trusted code.

## Runtime support and wire portability

The runtime supplies netstandard2.1 and modern .NET assets. Runtime availability and wire portability are separate guarantees. The netstandard2.1 asset uses pointer cursors for Unity/Mono/IL2CPP: low-level readers/writers over managed storage require pinning for their complete lifetime. Generated convenience entry points retain their existing pinning and cleanup.

Native primitives and raw unmanaged storage retain process byte order and representation. Raw structs can contain padding, runtime-dependent layout and pointer-sized members. Avoid `IntPtr`, `UIntPtr`, pointers, runtime-sized vectors and unanalyzable native external structs in portable persistence/network schemas. Matching type names do not establish a matching ABI across runtime versions.

Packed portable structs remove inter-field padding in declaration order. Tagged collections model supported layout differences and convert semantic field bytes using actual field addresses. Neither is a general endian conversion protocol. Cross-machine exchange requires matching schemas, framing/string options, supported representations and compatible byte order, verified with retained fixtures on the actual targets. Version-tolerant objects do not negotiate all these conditions automatically.

Native padding can contain unspecified or previously used bytes. `Native` copies the complete storage. `Tagged` may also copy complete storage, including padding, when a modeled layout matches the running layout; a layout stable across models can retain a headerless native representation. These decisions establish layout compatibility, not a no-padding or trusted-input check. `Packed` writes declaration-ordered semantic field bytes and excludes inter-field padding. Raw padded structs can disclose padding bytes or produce different byte sequences for equal field values; clearing the output buffer does not clear source padding. Public protocols should use reviewed field-oriented schemas or a canonical application wire format. Changing established native wire bytes requires a separate compatibility decision.

## Untrusted input

### Declaration-selected external contracts

Use the existing `LuminPackOption` attribute on an assembly or a generated root type. Existing serialization entry points select this policy at compilation; there is no additional serializer facade.

```csharp
[LuminPackable]
[LuminPackOption(UntrustedData = true, MaxInputBytes = 1024 * 1024,
    MaxCollectionLength = 10_000, MaxDepth = 32)]
public partial class Request
{
    public int Id;
    public string Name = "";
}
```

Defaults: 8 MiB encoded input, 16 MiB decompressed payload, 64 MiB conservative cumulative library allocation estimates, 100,000 elements per collection, 1,048,576 UTF-16 code units per string, 100,000 reference values, depth 64 and 32 Mi work units. Configure these with `MaxInputBytes`, `MaxDecompressedBytes`, `MaxAllocationBytes`, `MaxCollectionLength`, `MaxStringLength`, `MaxObjectCount`, `MaxDepth` and `MaxReadWork`. Limits must be nonnegative; depth must be between 1 and 64. Zero rejects any operation requiring that resource. `MaxJsonDepth` also applies, bounded by `MaxDepth` for these documents. Assembly limits are inherited and explicit type limits override them. Safety declarations on individual members are rejected.

Enabled contracts use a separately identified binary or JSON envelope containing a generated schema/profile digest. Both endpoints must agree on the contract and string/profile options. Limits are receiver policy and do not change the digest. Enabled contracts are incompatible with unframed trusted records; this is an opt-in protocol, not automatic negotiation or authentication. JSON uses a canonical `$luminpack`, `$schema`, `$value` envelope. Binary envelopes validate declared compression sizes before decompression. Convenience facades also derive a conservative byte cap from the allocation budget, so their effective input cap can be smaller than `MaxInputBytes`, including for span overloads. Individual limits are upper bounds, not a guarantee that every record below them fits the aggregate budgets.

The boundary checks framing and the schema/profile digest before decoding. It then checks actual counts, strings, scalar domains, known JSON members, collection invariants and cumulative budgets while the original formatter runs. Count-based allocations and unsafe accesses retain their preceding range and resource checks. JSON uses the original strict tokenizer and escape decoder; dictionary entries must contain exactly one key and one value. Duplicate names, keys and set elements are rejected. Collection invariants can be checked after bounded construction and before successful return. There is no complete input copy or schema prepass for an uncompressed span. Compression owns its separately bounded output buffer; stream, sequence and character facades retain their necessary bounded ingestion storage.

The caller must keep the input and its backing storage stable for the entire synchronous operation, including application callbacks. Concurrent mutation or returning pooled input while decoding violates this contract. Decoding is not transactional: malformed data discovered late can leave completed fields or reusable storage updated, and callbacks that already ran can have side effects. Decode into an isolated application object when failure must not change shared state. Writers frame the original operation directly, enforce output byte limits and patch frame lengths without revalidating the full output. Read quotas apply at reception; a produced record can exceed a receiver's collection, depth or other decoding limits.

Reviewed shapes include ordinary nongeneric generated objects, supported framework scalars, nullable scalar values, supported standard collections, and explicitly Packed unmanaged structs. Unknown/custom formats, input-selected types, pointers, native-sized storage, Union/reference/version-tolerant mechanisms, compressed members, legacy member-compressed operations, raw nullable collection aggregates and other unreviewed shapes fail closed under this option. Their trusted implementations remain available without it. Bounded binary envelope compression is separate from legacy member compression. Dictionary/set keys require reviewed framework equality; culture-sensitive sorted string keys are excluded. JSON graphs containing specialized Guid, DateTime, TimeSpan or multidimensional-array contracts fail closed even where their binary representation is accepted. JSON objects with more than 256 members are outside the reviewed member-tracking bound.

Packed storage removes padding; it does not implement general byte-order conversion. The external policy currently accepts little-endian runtimes only and rejects other byte orders. Interoperability still requires actual target fixture execution. Application constructors, callbacks, comparers, manually registered formatters and native extensions remain trusted code: their private allocations, side effects and execution are outside the library's budget. Manual replacement of generated dispatch is outside this generated-policy guarantee. Conservative allocation/work accounting may reject inputs below individual size limits, especially collision-prone containers and large zero-wire element storage.

`UntrustedData = false` is the default. Graphs without selected external contracts retain their original generated code and wire representation, without runtime policy dispatch. A trusted parent can contain a separately selected child contract; that child retains its own envelope and limits. Disabling a parent's policy does not disable an independently selected child. Such mixed graphs can contain inactive guard adapters around their original trusted operations. Enabled validation, budgets and envelopes have runtime costs; compression, ingestion and some collection checks can allocate. Compilation removes configuration selection overhead, not the work required to validate hostile input. Compatibility and performance of an updated implementation require focused execution on its actual targets.

### Existing trusted and low-level contracts

Public advancing Reader `EnsureReadable`, existing `Advance` checks, fixed preaccess checks, Union framing and dynamic collection/string payload checks remain enabled in Release. A span facade rejects short fixed input before loading data or mutating its destination. Address-only kernels cannot infer allocation bounds and retain caller-owned valid-range contracts. Fixed Writer stores require reserved output storage.

Bounded raw collection APIs reject elements containing managed references. Such types need generated/specialized formatters. Passing managed-reference storage to unchecked raw address kernels, overlapping ranges to a non-overlapping copy, stale pointers or unpinned cursor storage violates their contracts. Do not expose input-controlled pointer/type selection to these APIs.

Default trusted operations do not provide the declaration-selected allocation, object-count, depth or work budgets described above. Valid input can still consume substantial resources. Stack sufficiency and compression plausibility checks are not application quotas. Bound encoded frame size, declared decompressed size before allocation, application collection sizes and request/process resources. Catching memory or stack exhaustion is not a resource-control strategy.

A packed empty struct occupies zero payload bytes. A valid collection of those elements can therefore contain only its count and framing, irrespective of its element count. Generated readers preserve that representation and use a zero minimum only for a proven empty packed contract. An encoded input-size cap alone cannot bound such a collection's allocations or traversal work; an independent element/work quota is required at an untrusted boundary.

Compressed raw collections require the frame's declared decoded byte width to equal the checked element count times the storage width, before allocating or writing a destination. A valid shorter compression frame is not a valid complete collection payload. This consistency check does not replace an application decompression quota.

The default JSON profile has `ValidateJsonSyntax = false` and depth 1024. It is a trusted-JSON profile. For external JSON, select a stateless profile with `ValidateJsonSyntax = true` and an appropriate depth. Strict grammar still requires application resource and schema limits.

Approved collection fast paths assume producer invariants. Raw set bucket reconstruction does not promise duplicate rejection; adversarial hash collisions and application comparers can be expensive. Raw scalar bits are not domain validation for every framework type. Check uniqueness and domain invariants before using decoded data for correctness or authorization.

Constructors, callbacks, pooling/reuse handlers and custom formatters can execute application code during reading. Failed reads can preserve completed fields according to existing publication rules. Deserialization is neither transactional nor an execution sandbox. Validate a boundary object before publishing it into application state.

`System.Type` deserialization can resolve input-supplied names on dynamically enabled configurations. Exclude it from hostile-input schemas or use a closed application identifier mapping. A dynamic-code capability guard is not a type allowlist; name resolution alone also does not establish arbitrary object construction or code execution.

## Authentication and native extensions

Serialization provides no authenticity or confidentiality. The separate ChaCha20-Poly1305 helper verifies authentication before decrypting into the destination. Keep its tag comparison on the existing constant-time path. Key management, nonce uniqueness per key and authenticated protocol context remain application responsibilities. Use the standard 256-bit-key RFC 8439 construction for interoperability.

The netstandard2.1-only `LuminPackBurst` table is a trusted native extension point, never initialized from payload data. Its pointers must obey [the native hook contract](burst-hooks.md). Managed count checks cannot undo an incorrect native memory access. Enable only a backend verified on the actual runtime and CPU.

Compilation, synthetic layouts and configured CI jobs do not prove actual execution on every target. Follow [the SIMD rules](mechanism-contract.md#4-simd).
