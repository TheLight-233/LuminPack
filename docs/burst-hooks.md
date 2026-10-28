# Optional Core native kernels

[Chinese](burst-hooks.cn.md)

`LuminPack.Core.LuminPackBurst` is compiled only under `NETSTANDARD2_1`. Modern .NET assets contain neither the table nor its call-site checks and retain native SIMD dispatch. Core has no Unity/Burst dependency or automatically installed backend. The optional `LuminPack.Burst` extension registers its available backend before scene loading when imported into Unity.

Set `KernelTable.Version = LuminPackBurst.AbiVersion`, assign supported Cdecl pointers, leave other slots null and call `Register`. ABI version three appends compact Latin-1 and planned-layout slots to the managed table; existing version-two registrations remain supported with the appended slots disabled. Do not pass this managed structure as an unversioned native memory block. Platforms without a compatible backend leave the table unregistered. Missing slots retain established scalar/platform paths. Layout transfer returning zero leaves work to existing field-wise code.

Registration copies and atomically publishes an immutable table snapshot. Later mutation of the caller's table has no effect. Each invocation captures its own snapshot. `Clear()` stops subsequent publication; it does not drain active calls or unload native code. Keep native code and any delegate/compiler owners alive until captured calls finish. Use an application-controlled quiescent boundary for unloading/replacement. Kernels must not retain pointers, call managed callbacks, throw across the ABI or recursively invoke their own dispatcher.

Planned layout backends share `LuminKernelPolicy.UseNativeLayout`: batches below 512 elements retain Core's existing byte-run or generated field-wise path before native descriptor creation. This is native-boundary eligibility; modern .NET's direct vector path retains its established complete-access reserve. Both `LuminLayout` converters and `LuminPortable` pack/unpack entry points cache their direction-specific plans. The portable factory retains its original four-run wide-element eligibility and conservative complete-vector reserve; the general converter retains its own original three-run eligibility. The original offset-based slot remains available for version-two registrations.

All slots use `delegate* unmanaged[Cdecl]`. `int` and `uint` are signed/unsigned 32-bit integers, `char` is a UTF-16 code unit, and `byte` is 8 bits. Boolean results are zero/one. Parameters are ordered as follows:

| Slot | Parameters and result |
| --- | --- |
| `Copy` | `byte* destination, byte* source, uint bytes -> int`. ABI version 2 returns one on completion, or zero without writes to decline into Core's existing `Unsafe.CopyBlockUnaligned` fallback. Core only dispatches 16..256 bytes on 64-bit; 32-bit fixed storage and larger ranges retain cpblk. `LuminKernelPolicy` shares the original 128/256/512-bit short-copy eligibility. |
| `Equal` | `byte* left, byte* right, uint bytes -> int`. Ordinary complete byte equality; never used for authentication tags. |
| `Utf8Encode` | `char* source, int characters, byte* destination, int capacity, int* consumed, int* written, uint flags -> int`. Flag 1 permits malformed-input replacement; flag 2 marks final input. Counts are input characters/output bytes. |
| `Utf8Decode` | `byte* source, int bytes, char* destination, int capacity, int* consumed, int* written, uint flags -> int`. Same flags; counts are input bytes/output characters. |
| `Utf8Equal` | `byte* source, int bytes, char* candidate, int characters -> int`. True only for valid canonical UTF-8 decoding to exactly the candidate. Reject overlong sequences, surrogate scalars, incomplete sequences and out-of-range scalars. |
| `AsciiEncode` | `char* source, int characters, byte* destination, uint flags -> int`. Capacity is at least `characters`. Validate all input before writing. Reject non-ASCII; flag 1 additionally rejects controls below 0x20, quotes and backslashes. False writes nothing. Add no quotes/terminators. |
| `Layout` | `byte* source, byte* destination, int count, int sourceStride, int destinationStride, int* sourceOffsets, int* destinationOffsets, int semanticBytes -> int`. Offset pairs describe semantic bytes in declaration order. Return a complete element prefix in `[0,count]`; leave later elements untouched. Later overlapping fields win. Access no bytes beyond the complete source/destination elements. |
| `CountMatch` | `byte* left, byte* right, uint maximum -> uint`. Exact equal prefix in `[0,maximum]`. Inputs can overlap and remain read-only. |
| `CopyMatch` | `byte* destination, uint bytes, uint backwardDistance -> void`. Positive backward reference and complete output are validated by the parser. Expand in increasing logical byte order: short periods need repeated overlap semantics, not memcpy/memmove. |
| `ChaCha20` | `uint* state, byte* output, int blocks -> void`. Sixteen state words remain unchanged. Blocks is 1/2/4, output has `64 * blocks` bytes. Consecutive blocks increment word 12 with existing 32-bit wrap semantics; retain RFC byte order. |
| `Xor` | `byte* output, byte* input, byte* keystream, uint bytes -> void`. Complete ranges; exact input/output aliasing is supported. |
| `IsLatin1` | `char* source, int characters -> int`. Check the complete range for code units at most U+00FF without writes. |
| `NarrowLatin1` | `char* source, byte* destination, int characters -> int`. Output has room for every character. One means every code unit narrowed. Zero means unrepresentable and may leave a narrowed prefix, matching Core's existing contract; the record writer overwrites the prefix with raw UTF-16. |
| `WidenLatin1` | `byte* source, char* destination, int characters -> void`. Complete input and unpublished output ranges; widen every byte exactly. |
| `Latin1Equal` | `byte* source, char* candidate, int characters -> int`. Complete same-length ranges; compare payload bytes with exact UTF-16 code units. No UTF-8 interpretation. |
| `LayoutPlanned` | `byte* source, byte* destination, int count, LuminLayoutPlan* plan -> int`. The pinned immutable process-local descriptor contains Core's precomputed strides, masks and complete-access reserve. Return a complete prefix in `[0,count]`. Core uses its original byte-run tail. Version-two registrations disable this appended slot. |

UTF codecs return `OperationStatus` integers: Done 0, DestinationTooSmall 1, NeedMoreData 2, InvalidData 3. Return -1 only before consuming/writing anything, with both counts zero, to decline into the existing codec. Streaming, malformed-sequence consumption, replacement and output-capacity semantics must match the platform codec. Done consumes the whole source. Core validates counts/status before managed cursor publication; validation cannot protect against a bad native memory access.

Managed inputs, outputs and layout descriptions are pinned for the entire native invocation. ChaCha/XOR already receive caller-pinned, native or stack storage. Empty windows may have null pointers; never dereference them. Assume no alignment. Core retains framing, checked ranges, allocation/reuse, reference state, construction/callbacks and publication. Layout descriptors initialize lazily only when a layout slot exists; invalid geometry declines into existing code.

Boundaries cover raw copies, UTF-16 copy/equality, UTF-8 encoding/decoding/reuse, compact Latin-1 records, short ASCII JSON, tagged/packed struct collections, LZ match comparison/expansion and cipher/XOR. Modern BMP/512-bit text and F14 vector helpers stay private to the native .NET path. The legacy F14 representation has no new tag-group algorithm.

A future backend selects exact CPU features itself: nonnull pointers are not feature checks. Review scalar, 128/256/512-bit and ARM64 behavior, bounded tails, unaligned access, true overlap and lifetime on each runtime. Fixed-width algorithms need a block-based reason. Compilation and scalar ABI probes do not validate Burst code generation, IL2CPP or unavailable hardware. Follow [the SIMD rules](mechanism-contract.md#4-simd) before enabling a backend.

The `Hooks` area of `tests/LuminPack.Tests` (`-p:LuminPackTestArea=Hooks`) executes the actual netstandard2.1 asset using scalar unmanaged ABI probes in a modern host; it is neither a shipped backend nor a performance test. Modern unit coverage separately checks that hook types are absent.
