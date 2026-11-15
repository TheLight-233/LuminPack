# JSON property-name lookup

[Chinese](json-name-lookup.cn.md)

LuminPack generates property-name lookup tables for each model. Ordered properties first use the existing ordinal matcher. Unordered names, escaped names after decoding, and former names use the generated lookup for the document's encoding.

Small tables use a length switch and exact comparisons. Larger tables also use this path when every length bucket contains at most four candidates. The generator decides independently for UTF-8 byte lengths and UTF-16 code-unit lengths, including aliases and reference metadata. The decision is made during compilation; there is no runtime dictionary or strategy selection.

Dense length buckets use a seed-zero XXH3 64-bit switch. Equal hash constants share one case, and every candidate still requires an exact name comparison. A hash collision cannot select a different property. XXH3 is a noncryptographic grouping function, not an equality test or a security boundary.

Core and both generators compile the same `XxHash3Algorithm.cs`. The algorithm matches standard XXH3 with seed zero. UTF-8 hashes cover the encoded bytes; UTF-16 hashes cover the code units' in-memory bytes, with no transcoding or Unicode normalization. Hashes for different encodings are not expected to agree. Compile-time constants and runtime hashes for the same encoding must agree.

Generated hash lookups call `XxHash3.Hash64(ref byte source, uint byteLength)`. The caller provides a validated readable range; a null reference is accepted only for zero bytes. Scalar and vector kernels use tracked references and `Unsafe`, with no pointers or pinning. The span convenience entries `Hash64Utf8` and `Hash64Utf16` forward to the same entry. These are internal dispatch utilities, not persisted identifiers or a replacement for serializer APIs.

Inputs up to 240 bytes use scalar mixing with constant secrets. Longer inputs use AVX2 or SSE2/ARM64 AdvSimd on supported 64-bit modern targets. AVX-512 machines use the AVX2 implementation. Generators and portable targets use the scalar implementation; 32-bit runtime word loads retain the Marshal block-copy rule. All paths read only within the supplied span. No native library or runtime hashing package is required.

Name hashes are not serialized into JSON or binary records. Changing this internal algorithm requires rebuilding generated consumers with the matching Core; already compiled hash constants are not compatible across algorithm changes.

The implementation adapts .NET 10 `System.IO.Hashing` and xxHash. Their [third-party licenses](xxhash-LICENSE.txt) are retained under `docs` and included in the runtime and generator packages.

The focused generator check is `--verify-json-name-hash`. It compares both generators with Core and executes generated sparse and dense lookups, aliases and reference metadata against the portable Core asset. Build the Release `netstandard2.1` Core first. The runtime `MarshalCopyBlockTest` includes independent standard vectors, a reference digest, constructed-collision regressions and guarded memory ranges; it is also part of the SIMD and architecture CI coverage.
