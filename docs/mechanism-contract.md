# Mechanism contract

[Chinese](mechanism-contract.cn.md)

This document lists the hard constraints of LuminPack. It applies to the whole repository: runtime code, the generator, generated code, optional modules and conditional targets. A passing round trip, a smaller file or a faster benchmark is never a reason to replace one of these mechanisms on its own.

The history of individual approvals lives in Git. When this document, the code and the history disagree, report the discrepancy with evidence instead of silently picking a design.

## 1. Change control

1. **Mechanisms are preserved by default.** Every mechanism in section 2 stays as it is unless the owner explicitly approves a concrete replacement.
2. **Experiments come first.** A faster alternative is prepared in `experiments/` (kept out of version control), with:
   - before/after elapsed-time ratios against the same reference libraries (for example Nino, MemoryPack, System.Text.Json), measured on the same machine, fixture, runtime and options, in the same run;
   - repeated samples with their dispersion, and allocations;
   - generated source, IL or native code where it matters;
   - the semantic, API, wire, ownership and platform trade-offs.
3. **Only explicit approval of that concrete proposal authorizes the replacement.** A performance target, a refactoring, dead-code removal or a size reduction is not approval.
4. **Correctness fixes are distinct.** A reproduced bug may be fixed while keeping the intended protocol. Record it as a fix with its regression test, not as an unchanged implementation.
5. **Approved differences are recorded.** Add the approved rule to this document (not a dated narrative) and describe the change in the commit.
6. **Commit completed work by default.** After completing a feature or fix and its relevant verification, commit only the task's changes using the [commit conventions](../CONTRIBUTING.md#commit-conventions), without asking for commit permission. Exclude unrelated or unfinished changes. Ask whether to push after committing unless the owner has already authorized that push. Tags and published-history rewrites still require explicit authorization.

## 2. Preserved mechanisms

### Storage access

- **Local + As views.** Private, protected, readonly and auto-property backing storage is accessed through generated layout mirrors and `Unsafe.As`, never reflection. Mirrors replicate the inheritance chain and `LayoutKind` (including explicit offsets). Accessible writable fields are accessed directly.
- **Storage placeholders.** Mirror fields, padding members and hidden storage must not be removed because ordinary call-site searches find no use.
- **Adjacent-field bulk copies.** Consecutive unmanaged fields may be copied as one block only on 64-bit, after proving the actual field addresses match the packed offsets, excluding volatile fields, and keeping a field-wise fallback.
- **Architecture-specific fixed access.** On 32-bit, an individual unmanaged value with `Unsafe.SizeOf<T>() <= 4` may use typed `Unsafe.ReadUnaligned` / `WriteUnaligned`. Larger values and field batches use `LuminPackMarshal.CopyBlockUnaligned` and the packed storage views, because typed unaligned access of those can crash IL2CPP. All members being four bytes or smaller does not permit a typed aggregate access. On 64-bit, typed access and block copies are both allowed.
- **Raw copies.** Non-overlapping block copies, including generated ones, go through `LuminPackMarshal.CopyBlockUnaligned`; only that helper calls `Unsafe.CopyBlockUnaligned`. Overlap-safe span copies keep memmove semantics.

### Representations

- **Object mode** is positional with a one-byte member count (0 to 249, `255` = null).
- **Version tolerance** uses named members with UTF-8 metadata, a length table and byte-wise metadata comparison; `LuminPackOldName` keeps the oldest name on write.
- **Reference tracking** (`CircleReference`) keeps its slot length tables and reference ids.
- **Unmanaged structs** keep the `StructLayout` contract: `Tagged` (default), `Packed`, `Native`.
- **Strings** default to fixed-width UTF-16 length records, with the compact Latin-1 form; UTF-8 and token profiles stay available. Opt-in `VarInt` recording encodes each existing length header plus one as canonical unsigned base-128, preserving payload encodings and distinct null and empty records. UTF-8 keeps both length fields. Fixed-width defaults and their wire bytes remain unchanged; both endpoints select the same profile. Token records share the empty terminator.
- **JSON** keeps its envelopes (`$type`/`$value`, `$id`/`$ref`, `Lazy`, multidimensional arrays) and rejects non-finite numbers.
- **Byte order** is little-endian. No mode performs endian conversion.
- Changing any wire byte needs explicit approval and updated wire fixtures.

### Dispatch

- **Concrete overloads** bind generated root bodies directly and never consult registries.
- **Generic entry points** consult explicit and `Custom` registrations first, then create the generated entry of the type on first use per type and profile. No per-type id tables, type switches or registration blocks for every analyzed type are generated.
- **Union dispatch** is a generated public virtual method on the root (`EditorBrowsable(Never)`). Generic Union roots add a cached slot switch after that virtual call. No generic virtual methods, runtime type maps or replacement switches.
- **Registration** passes static method pointers keyed by method table address (`LuminPackFormatter`). No `Type` or delegate is retained, so collectible `AssemblyLoadContext`s can unload. Registration validates tags, case types and required operations before publishing.
- **`Custom`** formatters own their complete representation and are invoked through their registered pointers.

### Execution state

- **Advancing cursors.** Binary and JSON readers and writers advance a `ref byte` cursor on modern targets and a `byte*` cursor on `netstandard2.1`.
- **Cached ends.** Writer, Reader, JSON Writer and JSON Reader cache the end of their window (or the writer's growth threshold) beside the cursor. `Buffer`, `BufferCheckLimit` and `Owner` have public getters and internal setters that refresh the cache; every constructor and window change must refresh it.
- **Cursor publication.** Generated code may keep the cursor in a local between state-observing boundaries. It must publish the cursor before calling a formatter, a Union or recursive operation, a callback, or a capacity check, and resume from the resulting state afterwards. Never keep a buffer reference across a resize.
- **Fixed Union case writes.** An eligible local, narrow, non-generic class Union's ordinary virtual case slot may write its tag and callback-free fixed payload in the shared cursor-returning body, publishing the resulting position before returning. It keeps the original virtual and profile-specific slots, and excludes external-input policy cases; other cases retain their original write path.
- **Large fixed Union records.** Fixed payloads whose width plus the maximum Union header exceeds the ordinary minimum growth headroom reserve their complete payload before its unchecked stores. The returning body publishes its absolute cursor before that capacity boundary and reacquires output storage after growth. Small fixed payloads keep their threshold-based reservation contract.
- **One state body per operation.** Each retained type has one complete binary write, binary read, JSON write, JSON read and size body; convenience overloads forward to it.
- **Managed output storage.** `LuminBufferWriter` owns one `ArrayPool<byte>.Shared` array instead of native allocations, with no pinning handles. Its usable range starts at a 64-byte boundary observed at rent time; the alignment is a performance hint, not a correctness requirement. Growth keeps the 32 KiB initial capacity, 1.5x growth and 87.5% threshold, extends in place while the rented array fits, otherwise copies before returning the old array, and refreshes cursor/end caches. Every exposed byte is cleared on acquisition or written by the owner. Modern cursors are managed references; netstandard2.1 pointer cursors are taken under `fixed` and rely on the runtime not relocating the array during the operation (Unity's non-moving GC).
- **Caller-owned append sessions.** Overloads taking a `LuminBufferWriter` append at its cursor and keep reference state until the caller resets it (`ResetForReuse`, `Reset`, `ResetCore`).
- **Independent default reference contexts.** Separate default readers, writers and evaluators never share reference identities.
- **Construction, pooling and reuse.** Constructor selection, `[LuminPackPoolRent]`, `ref` reuse of objects, collections and strings, and the documented publication rules on failure are preserved. Reuse is not transactional.
- **Pooled fresh reads.** In ordinary (`Object` mode) binary reads, a reference type with `[LuminPackPoolRent]` rents its instance when the target is null, at the root and as an inline member, and then reads into it like an existing instance. Version-tolerant and reference-tracked modes keep their staged members and late commit.
- **Callbacks** run at their established points and order.

### Options and generation

- **Compile-time options.** `[LuminPackOption]` scopes (assembly, type, member) and stateless `ILuminPackOptions` profiles are compiled in. There is no runtime options object or mutable global default. JSON encoding, depth and syntax policy apply to a whole document, selected by its root.
- **`UntrustedData`** selects the bounded external-data protocol at compile time; disabled declarations generate exactly the trusted code.
- **`[LuminPackBounded]`** is a caller guarantee: the bounded record reserves once and drops inner container checks.
- **Generation modes** `Full`, `Medium`, `Light` and `Minimal` keep their reachability rules. Pruning changes which code exists, never the wire format or the selected formatter.
- **Incremental caches** keep exact semantic dependencies, settings, diagnostics and deterministic identities. No `Compilation`, `SemanticModel` or symbol may survive in a cross-compilation cache. Retained-driver output must equal fresh output.

### Optional modules

- Core references neither `LuminPack.Unity` nor `LuminPack.Burst`.
- The `LuminPackBurst` hook table exists only for `netstandard2.1` and follows the [hook ABI](burst-hooks.md). Null slots keep the Core implementation. Burst code is projected from Core sources, never maintained as a second algorithm.

## 3. Safety rules

1. **Reader checks stay on in Release.** The advancing Reader's public `EnsureReadable` and its callers (`Advance`, fixed storage batches, Union framing) are active in every configuration. Do not restore `Conditional("DEBUG")`, wrap checks in `#if DEBUG`, or bypass them for performance.
2. **Check before access.** Generated cursor arithmetic that replaces `Advance` keeps its range check before moving the reference or pointer. Scalar reads and fixed-field fallbacks validate the complete range before any unsafe load or destination write.
3. **Dynamic checks stay.** Collection, string and bulk-payload header and payload validation is mandatory. A count is validated against the remaining input before any allocation sized from it.
4. **Separate unchecked contracts.** Fixed Writer stores into reserved room and address-only raw kernels keep their existing unchecked contracts. This does not permit removing Reader checks or adding blanket Writer checks.
5. **Recursion.** Operations of recursive payloads call `EnsureSufficientStack`, and recursive binary writes check capacity on re-entry.
6. **Signed comparisons.** Range checks against cached ends use signed 64-bit distances so a cursor past the end and negative counts are rejected on every target.
7. **Exceptions** are thrown through `LuminPackExceptionHelper` (runtime) or the generator's own helper. Keep exception types, argument names, cleanup and original stacks.

## 4. SIMD

- Review scalar, 128-bit, 256-bit, 512-bit and ARM64 execution for every SIMD change.
- Guard every ISA-specific operation by its exact required features. Width preference is not hardware support.
- Keep bounded tails, a scalar or portable fallback, zero-length behavior and identical bytes. Never read or write outside a complete validated range.
- A fixed-width algorithm may keep its natural width only with a documented reason (for example the 16-byte F14 tag group).
- Hardware unavailable locally requires CI execution. Compilation or synthetic masks are not hardware validation.

## 5. Performance evidence

- The primary evidence is LuminPack's elapsed time divided by the reference library's, measured on the same machine, fixture, runtime and options, in the same run. Lower is better.
- Report before/after ratios against the same references, repeated samples with dispersion, and allocations. Keep absolute times as supporting evidence.
- Do not infer a win from one nanosecond figure, and do not assume all workloads fluctuate together.
- When machine load makes timing unreliable, compare retained IL and native code instead, and say so.
- Benchmarks of reusable buffers include the reset of each independent record.

## 6. Repository rules

- English for code, comments, XML documentation, diagnostics, tests and maintenance documents. README files and `*.cn.md` editions are the exception.
- README, AGENTS, CONTRIBUTING and every document under `docs` exist in English (`name.md`) and Chinese (`name.cn.md`); change both editions together.
- README is introduction, installation and usage. No release notes, version histories, migration notices or experiment reports, in README or in `docs`. Those records stay out of version control.
- Do not remove externally usable or generated members based only on call-site searches.
- Every method has XML documentation (checked by `tools/LuminPack.RepositoryChecks`).

## 7. Verification

Run the checks in [CONTRIBUTING](../CONTRIBUTING.md) that cover the change: repository checks, generator and codegen contracts, focused runtime tests, incremental generation, wire fixtures and, for platform-specific code, the runtime compatibility workflows. Structural assertions and round trips establish coverage of tested shapes, not proof for every model; report what was not verified.
