# Compression

[Chinese](compression.cn.md)

`LuminPack.Utility.LuminCompressor` compresses byte spans. `[LuminPackCompress]` uses the same codec for unmanaged array members. Compression is independent of the binary and JSON serializers.

```csharp
byte[] compressed = LuminCompressor.Compress(input);
byte[] restored = LuminCompressor.Decompress(compressed);

byte[] storage = new byte[LuminCompressor.GetMaxCompressedSize(input.Length)];
int written = LuminCompressor.Compress(input, storage);
int originalLength = LuminCompressor.GetDecompressedSize(storage.AsSpan(0, written));
byte[] output = new byte[originalLength];
LuminCompressor.Decompress(storage.AsSpan(0, written), output);
```

The span overloads use caller-owned, non-overlapping input and output. Compression requires the full advertised maximum capacity, even when the result is smaller. Decompression requires the declared output capacity and returns the exact number of bytes written. The `LuminBufferWriter` overloads support using the same writer for input and output by staging the result before replacing its contents.

`GetMaxCompressedSize(0)` is 8. For nonempty input the bound is `8 + length + (length >> 7) + 16`; negative lengths and lengths above 2,130,836,464 throw `ArgumentOutOfRangeException`. This bounds literal extensions and sequence framing, including incompressible data.

C++ exposes the same frames through `<luminpack/compression.h>` and `luminpack::compression`. Use `max_compressed_size(input.size())` to allocate a destination, then use the byte count returned by `compress(input, destination)` to select the written frame. `decompressed_size(frame)` gives the destination size for `decompress(frame, destination)`. Both operations use non-overlapping caller-owned spans. A malformed frame can leave a decoded prefix in the destination.

## Frame format

All multibyte fixed integers are little-endian. A frame starts with ASCII `LUM2` and a nonnegative `Int32` original length. Empty input consists of this eight-byte header alone. The decoder accepts only the `LUM2` signature.

Each nonempty frame contains sequences:

| Field | Encoding |
| --- | --- |
| Token | High nibble: literal length, saturated at 15. Low nibble: match length minus 4, saturated at 15. |
| Literal extension | Present when the high nibble is 15; added to 15. |
| Literals | Exactly the decoded literal length in bytes. |
| Match distance | Nonzero `UInt16`, at most the number of output bytes already produced. |
| Match extension | Present when the low nibble is 15; added to 19. |

An extension below 255 occupies one byte. Otherwise, the byte `FF` is followed by the extension minus 255 as a canonical unsigned base-128 integer: seven payload bits per byte, least significant group first, with bit 7 indicating another byte. A saturated nibble always has an extension, including zero. Values must fit in a nonnegative `Int32`; redundant final zero groups, unterminated values and overflow are rejected. For example, extensions 128, 254, 255 and 383 are `80`, `FE`, `FF 00` and `FF 80 01`.

The final sequence has a zero match nibble and contains only literals and their optional extension. No match distance follows it. The decoder requires the final sequence and an exact match between produced bytes and the declared length.

## Matching and execution

The encoder uses a 64 KiB window, an uncleared per-thread table of low 16-bit positions, skip acceleration, backward extension and one-step lazy matching. A candidate's modular distance is validated before reading it. The table retains at most 32,768 entries (64 KiB) per thread; warm span operations need no managed allocation.

Lazy matching first checks whether a candidate can exceed the current match and whether the word at that boundary agrees. SIMD extension compares four complete 128-, 256- or 512-bit vectors per group and locates a mismatch in the already loaded differences. Every load is inside the validated range. Hardware guards select the available width; scalar and portable paths retain bounded tails and the 32-bit block-copy contract.

Long periodic matches expand an initialized prefix with non-overlapping copies. Each completed copy increases the readable prefix, allowing the shared copy kernel to use SIMD even when the original match distance is smaller than a vector. Short matches retain bounded scalar or vector copies. Native Burst match hooks remain available on `netstandard2.1`.

## Validation

The decoder checks literal input and output ranges, match distances, canonical extensions, output lengths and final framing before each access. `GetDecompressedSize` validates the header and, when the declared expansion exceeds 255 times the body length, checks the entire sequence structure before allocating or growing a buffer. This permits compact long matches without trusting a large header on its own. It is not a full validation pass for ordinary frames; `Decompress` performs their sequence validation. A failed span decode can leave a valid decoded prefix in the destination.
