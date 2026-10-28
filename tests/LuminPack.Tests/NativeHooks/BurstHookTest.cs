using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Cryptography;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Executes the actual netstandard2.1 asset with scalar native ABI probes; these tests make no SIMD hardware claim.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class BurstHookTest : IDisposable
{
    private static int _copyCalls, _encodeCalls, _decodeCalls, _matchCalls, _expandCalls, _xorCalls, _descriptions;
    private static bool _collect;
    private static int _invalidResult;

    /// <summary>Starts every case without an installed native backend.</summary>
    public BurstHookTest() => LuminPackBurst.Clear();

    /// <summary>Releases the process-wide registration after every case.</summary>
    public void Dispose() => LuminPackBurst.Clear();

    /// <summary>Checks ABI validation, copied ownership, null slots, clearing, and the selected runtime asset.</summary>
    [Fact]
    public void NullSlotsAndRegistrationRetainFallback()
    {
        Assert.Contains(".NETStandard,Version=v2.1", typeof(LuminPackBurst).Assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName);
        Assert.False(LuminPackBurst.IsRegistered);
        Assert.Throws<ArgumentException>(() => LuminPackBurst.Register(default));
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion });
        Assert.True(LuminPackBurst.IsRegistered);
        byte[] input = Enumerable.Range(0, 67).Select(i => (byte)i).ToArray(), output = new byte[67];
        LuminPackMarshal.CopyBlockUnaligned(ref output[0], ref input[0], 67);
        Assert.Equal(input, output);
        _descriptions = 0;
        var value = new Padded { Tag = 9, Value = 123 };
        Assert.Equal(0, LuminPortable<Padded, PaddedLayout>.Pack(ref value, 1, ref output[0]));
        Assert.Equal(0, _descriptions);
        var table = new LuminPackBurst.KernelTable { Version = LuminPackBurst.AbiVersion, Copy = &Copy };
        LuminPackBurst.Register(table);
        table.Copy = null;
        int before = _copyCalls;
        LuminPackMarshal.CopyBlockUnaligned(ref output[0], ref input[0], 67);
        Assert.Equal(before + (IntPtr.Size == 8 ? 1 : 0), _copyCalls);
        LuminPackBurst.Clear();
        Assert.False(LuminPackBurst.IsRegistered);
        LuminPackMarshal.CopyBlockUnaligned(ref output[0], ref input[0], 67);
        Assert.Equal(before + (IntPtr.Size == 8 ? 1 : 0), _copyCalls);
    }

    /// <summary>Checks exact copy windows, empty inputs and pinning across a compacting managed collection.</summary>
    [Fact]
    public void CopiesKeepBoundedPinnedWindows()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Copy = &Copy });
        _collect = true;
        try
        {
            foreach (int length in new[] { 0, 1, 15, 16, 17, 31, 32, 63, 64, 65, 257 })
            {
                byte[] input = Enumerable.Range(0, length + 8).Select(i => (byte)(i * 3)).ToArray();
                byte[] output = Enumerable.Repeat((byte)0xA5, length + 8).ToArray();
                int before = _copyCalls;
                LuminPackMarshal.CopyBlockUnaligned(ref output[3], ref input[2], (uint)length);
                Assert.Equal(input.AsSpan(2, length).ToArray(), output.AsSpan(3, length).ToArray());
                Assert.All(output.Take(3).Concat(output.Skip(3 + length)), b => Assert.Equal(0xA5, b));
                Assert.Equal(before + (IntPtr.Size == 8 && length >= 16 && length <= LuminKernelPolicy.ShortCopyLimit ? 1 : 0), _copyCalls);
            }
        }
        finally { _collect = false; }
    }

    /// <summary>Preserves native cpblk when a backend declines without writes or the byte count exceeds Core's short-copy range.</summary>
    [Fact]
    public void DeclinedCopiesRetainCoreNativeFallback()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Copy = &DeclineCopy });
        foreach (int length in new[] { 16, 17, 127, 128, 129, 255, 256, 257, 1024 })
        {
            byte[] input = Enumerable.Range(0, length).Select(i => (byte)i).ToArray(), output = new byte[length];
            int before = _copyCalls;
            LuminPackMarshal.CopyBlockUnaligned(ref output[0], ref input[0], (uint)length);
            Assert.Equal(input, output);
            Assert.Equal(before + (IntPtr.Size == 8 && length <= LuminKernelPolicy.ShortCopyLimit ? 1 : 0), _copyCalls);
        }
        Assert.Throws<ArgumentException>(() => LuminPackBurst.Register(new() { Version = 1 }));
    }

    /// <summary>Checks Unicode, strict failures, replacement, streaming tails and all destination capacities against the platform codec.</summary>
    [Fact]
    public void TextStatusCountsAndBytesMatchPlatform()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Utf8Encode = &Encode, Utf8Decode = &Decode, Utf8Equal = &Utf8Equal });
        foreach (string text in new[] { "", "ASCII", "\u00e9\u4e2d\ud83d\ude00", "\ud800", "A\udc00B" })
        foreach (bool replace in new[] { false, true })
        foreach (bool final in new[] { false, true })
        for (int capacity = 0; capacity <= 20; capacity++)
        {
            byte[] expected = Enumerable.Repeat((byte)0xA5, capacity + 2).ToArray(), actual = (byte[])expected.Clone();
            var status = Utf8.FromUtf16(text, expected.AsSpan(1, capacity), out int read, out int written, replace, final);
            var result = StringSerializer.SerializeAsUtf8(text, actual.AsSpan(1, capacity), out int gotRead, out int gotWritten, replace, final);
            Assert.Equal((status, read, written), (result, gotRead, gotWritten));
            Assert.Equal(expected, actual);
        }
        foreach (byte[] encoded in new[] { Array.Empty<byte>(), Encoding.UTF8.GetBytes("\u00e9\u4e2d\ud83d\ude00"), new byte[] { 0xF0, 0x9F }, new byte[] { 0xC0, 0x80 }, new byte[] { 65, 0xFF, 66 } })
        foreach (bool replace in new[] { false, true })
        foreach (bool final in new[] { false, true })
        for (int capacity = 0; capacity <= 10; capacity++)
        {
            char[] expected = Enumerable.Repeat('\u55aa', capacity + 2).ToArray(), actual = (char[])expected.Clone();
            var status = Utf8.ToUtf16(encoded, expected.AsSpan(1, capacity), out int read, out int written, replace, final);
            var result = StringSerializer.DeserializeFromUtf8(encoded, actual.AsSpan(1, capacity), out int gotRead, out int gotWritten, replace, final);
            Assert.Equal((status, read, written), (result, gotRead, gotWritten));
            Assert.Equal(expected, actual);
        }
        string valid = "ASCII\u4e2d\ud83d\ude00";
        byte[] bytes = Encoding.UTF8.GetBytes(valid);
        int before = _decodeCalls;
        Assert.Equal(valid, StringSerializer.DeserializeUtf8(ref bytes[0], bytes.Length, valid.Length));
        Assert.Equal(before + 1, _decodeCalls);
        Assert.True(StringSerializer.MatchesUtf8(bytes, valid));
        Assert.False(StringSerializer.MatchesUtf8(new byte[] { 0xC0, 0x80 }, "\0"));
        Assert.True(_encodeCalls > 0);
    }

    /// <summary>Rejects native cursor results outside validated ranges and retains fallback on an unsupported operation.</summary>
    [Fact]
    public void InvalidNativeResultsCannotAdvanceManagedCursors()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Utf8Encode = &InvalidEncode });
        for (_invalidResult = 0; _invalidResult < 6; _invalidResult++)
            Assert.Throws<InvalidOperationException>(() => StringSerializer.SerializeAsUtf8("abc", new byte[8], out _, out _));
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Utf8Encode = &UnsupportedEncode });
        byte[] output = new byte[8];
        Assert.Equal(OperationStatus.Done, StringSerializer.SerializeAsUtf8("abc", output, out int read, out int written));
        Assert.Equal((3, 3), (read, written));
        Assert.Equal("abc", Encoding.UTF8.GetString(output, 0, written));
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Layout = &InvalidLayout });
        Assert.Throws<InvalidOperationException>(() => ConvertLayout(4));
    }

    /// <summary>Checks ASCII refusal without writes, JSON escaping, and UTF-16 equality through optional native slots.</summary>
    [Fact]
    public void AsciiAndEqualityKeepCompleteValidation()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, AsciiEncode = &Ascii, Equal = &Equal });
        foreach (string text in new[] { "", "ASCII", "a\"bc", "a\\bc", "a\nbc", "\u4e2dabc" })
        {
            byte[] output = Enumerable.Repeat((byte)0xA5, text.Length + 3).ToArray();
            bool valid = text.All(c => c <= 0x7F && c >= 0x20 && c != '"' && c != '\\');
            Assert.Equal(valid, LuminPackBurst.TryAsciiEncode(text, output.AsSpan(1, text.Length), true));
            if (!valid) Assert.All(output, b => Assert.Equal(0xA5, b));
            else Assert.Equal(Encoding.ASCII.GetBytes(text), output.AsSpan(1, text.Length).ToArray());
            byte[] buffer = new byte[256];
            fixed (byte* pinned = buffer)
            {
                Span<byte> window = new(pinned, buffer.Length);
                using var state = new LuminPackWriterOptionalState();
                var writer = new LuminPackJsonWriter(ref window, state);
                writer.Write<LuminPackUtf8LengthOptions>(text);
                string json = Encoding.UTF8.GetString(buffer, 0, writer.Position);
                Assert.Equal(text, System.Text.Json.JsonSerializer.Deserialize<string>(json));
            }
        }
        string current = "UTF16\ud800";
        byte[] record = BitConverter.GetBytes(current.Length * 2).Concat(MemoryMarshal.AsBytes(current.AsSpan()).ToArray()).ToArray();
        Assert.Same(current, StringSerializer.ReadUtf16Record(ref record[0], record.Length, current, out int consumed));
        Assert.Equal(record.Length, consumed);
        record[^1] ^= 1;
        Assert.NotEqual(current, StringSerializer.ReadUtf16Record(ref record[0], record.Length, current, out _));
    }

    /// <summary>Checks that a captured native call can complete after registration is cleared.</summary>
    [Fact]
    public void ClearDoesNotInvalidateCapturedCall()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Copy = &ClearAndCopy });
        byte[] input = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray(), output = new byte[64];
        if (IntPtr.Size == 8)
            LuminPackMarshal.CopyBlockUnaligned(ref output[0], ref input[0], 64);
        else
            Assert.True(LuminPackBurst.TryCopy(ref output[0], ref input[0], 64));
        Assert.Equal(input, output);
        Assert.False(LuminPackBurst.IsRegistered);
    }

    /// <summary>Checks scalar completion of a native prefix, padding, immutable descriptors and legacy forwarding names.</summary>
    [Fact]
    public void LayoutPrefixKeepsFieldOrderAndScalarTail()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, Layout = &Layout });
        for (int count = 0; count <= 69; count++) ConvertLayout(count);
        var values = Enumerable.Range(0, 7).Select(i => new Padded { Tag = (byte)(i + 1), Value = i * 103 }).ToArray();
        byte[] first = Enumerable.Repeat((byte)0xA5, 40).ToArray(), second = (byte[])first.Clone();
        int done = LuminPortable<Padded, PaddedLayout>.Pack(ref values[0], values.Length, ref first[0]);
        Assert.Equal(3, done);
        Assert.Equal(done, LuminPortableTransfer<Padded, PaddedLayout>.Pack(ref values[0], values.Length, ref second[0]));
        Assert.Equal(first, second);
        var restored = new Padded[values.Length];
        Assert.Equal(done, LuminPortableTransfer<Padded, PaddedLayout>.Unpack(ref first[0], done, ref restored[0]));
        Assert.Equal(values.Take(done), restored.Take(done));
        Assert.All(first.Skip(done * 5), b => Assert.Equal(0xA5, b));
    }

    /// <summary>Compares compressed bytes and overlapping expansions with the unchanged scalar implementation.</summary>
    [Fact]
    public void CompressionPreservesBytesAndPeriodicOverlap()
    {
        foreach (int period in new[] { 1, 2, 3, 7, 8, 16, 31, 64, 257 })
        {
            byte[] input = Enumerable.Range(0, 8193).Select(i => (byte)(i % period)).ToArray();
            byte[] baseline = LuminCompressor.Compress(input);
            LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, CountMatch = &CountMatch, CopyMatch = &CopyMatch });
            byte[] actual = LuminCompressor.Compress(input);
            Assert.Equal(baseline, actual);
            Assert.Equal(input, LuminCompressor.Decompress(actual));
            LuminPackBurst.Clear();
        }
        Assert.True(_matchCalls > 0);
        Assert.True(_expandCalls > 0);
    }

    /// <summary>Checks registered crypto kernels against the platform AEAD and verifies authentication precedes decryption.</summary>
    [Fact]
    public void CryptoKeepsAuthenticationAndExactInPlaceSemantics()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, ChaCha20 = &Blocks, Xor = &Xor });
        byte[] key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), nonce = new byte[12], aad = { 1, 2, 3 };
        foreach (int length in new[] { 0, 1, 63, 64, 65, 127, 128, 255, 256, 1025 })
        {
            byte[] input = Enumerable.Range(0, length).Select(i => (byte)i).ToArray(), expected = new byte[length], expectedTag = new byte[16];
            if (System.Security.Cryptography.ChaCha20Poly1305.IsSupported)
            {
                using var reference = new System.Security.Cryptography.ChaCha20Poly1305(key);
                reference.Encrypt(nonce, input, expected, expectedTag, aad);
            }
            else
            {
                LuminPackBurst.Clear();
                LuminChaCha20Poly1305.Encrypt(key, nonce, aad, input, expected, expectedTag);
                LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, ChaCha20 = &Blocks, Xor = &Xor });
            }
            byte[] actual = (byte[])input.Clone(), tag = new byte[16];
            LuminChaCha20Poly1305.Encrypt(key, nonce, aad, actual, actual, tag);
            Assert.Equal(expected, actual);
            Assert.Equal(expectedTag, tag);
            Assert.True(LuminChaCha20Poly1305.Decrypt(key, nonce, aad, actual, tag, actual));
            Assert.Equal(input, actual);
            tag[0] ^= 1;
            byte[] rejected = Enumerable.Repeat((byte)0xA5, length).ToArray();
            int before = _xorCalls;
            Assert.False(LuminChaCha20Poly1305.Decrypt(key, nonce, aad, expected, tag, rejected));
            Assert.All(rejected, b => Assert.Equal(0xA5, b));
            Assert.Equal(before, _xorCalls);
        }
    }

    /// <summary>Compares complete conversion output with a scalar byte-run reference and sentinel padding.</summary>
    /// <param name="count">The element count.</param>
    private static void ConvertLayout(int count)
    {
        byte[] source = Enumerable.Range(0, Math.Max(1, count * 8)).Select(i => (byte)(i * 17)).ToArray();
        byte[] actual = Enumerable.Repeat((byte)0xA5, count * 6 + 9).ToArray(), expected = (byte[])actual.Clone();
        int[] from = { 0, 4, 5, 6, 7 }, to = { 0, 2, 3, 4, 5 };
        var accelerated = new LuminLayout(from, 8, to, 6);
        var scalar = new LuminLayout(from, 8, to, 6, allowVectors: false);
        from[0] = 7; to[0] = 5;
        scalar.Convert(ref source[0], ref expected[0], count);
        accelerated.Convert(ref source[0], ref actual[0], count);
        Assert.Equal(expected, actual);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Padded { public byte Tag; public int Value; }

    private readonly struct PaddedLayout : ILuminPortableLayout
    {
        public int WireSize => 5;
        /// <summary>Describes actual field addresses rather than assumed platform padding.</summary>
        /// <param name="storageOffsets">The five wire-byte offsets.</param>
        public void Describe(Span<int> storageOffsets)
        {
            _descriptions++;
            Padded value = default;
            storageOffsets[0] = 0;
            int offset = (int)Unsafe.ByteOffset(ref value.Tag, ref Unsafe.As<int, byte>(ref value.Value));
            for (int i = 0; i < 4; i++) storageOffsets[i + 1] = offset + i;
        }
    }

    /// <summary>Copies exact bytes after optionally forcing a moving collection inside the native call.</summary>
    /// <param name="output">The pinned output.</param><param name="input">The pinned input.</param><param name="count">The byte count.</param>
    /// <returns>One for complete success.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Copy(byte* output, byte* input, uint count)
    {
        _copyCalls++;
        if (_collect) GC.Collect(2, GCCollectionMode.Forced, true, true);
        for (uint i = 0; i < count; i++) output[i] = input[i];
        return 1;
    }

    /// <summary>Declines a copy before touching either range.</summary>
    /// <param name="output">Untouched output.</param><param name="input">Unread input.</param><param name="count">Unused byte count.</param>
    /// <returns>Zero to select the original Core native copy.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int DeclineCopy(byte* output, byte* input, uint count) { _copyCalls++; return 0; }

    /// <summary>Clears publication while the current native invocation still owns its snapshot.</summary>
    /// <param name="output">Output.</param><param name="input">Input.</param><param name="count">Bytes.</param>
    /// <returns>One for complete success.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int ClearAndCopy(byte* output, byte* input, uint count)
    {
        LuminPackBurst.Clear();
        for (uint i = 0; i < count; i++) output[i] = input[i];
        return 1;
    }

    /// <summary>Validates every code unit before producing ASCII bytes.</summary>
    /// <param name="input">Input.</param><param name="length">Characters.</param><param name="output">Output.</param><param name="flags">JSON escape rejection.</param>
    /// <returns>One for complete success, zero without writes on rejection.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Ascii(char* input, int length, byte* output, uint flags)
    {
        for (int i = 0; i < length; i++)
            if (input[i] > 127 || ((flags & 1) != 0 && (input[i] < 32 || input[i] == '"' || input[i] == '\\'))) return 0;
        for (int i = 0; i < length; i++) output[i] = (byte)input[i];
        return 1;
    }

    /// <summary>Compares ordinary bytes; authentication never calls this slot.</summary>
    /// <param name="left">First range.</param><param name="right">Second range.</param><param name="count">Bytes.</param>
    /// <returns>One for equality.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Equal(byte* left, byte* right, uint count)
    {
        for (uint i = 0; i < count; i++) if (left[i] != right[i]) return 0;
        return 1;
    }

    /// <summary>Uses the platform codec as a scalar ABI correctness oracle.</summary>
    /// <param name="input">Input.</param><param name="length">Input count.</param><param name="output">Output.</param><param name="capacity">Output count.</param><param name="read">Consumed.</param><param name="written">Produced.</param><param name="flags">Replacement and final-block flags.</param>
    /// <returns>The platform status.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Encode(char* input, int length, byte* output, int capacity, int* read, int* written, uint flags)
    {
        _encodeCalls++;
        return (int)Utf8.FromUtf16(new(input, length), new(output, capacity), out *read, out *written, (flags & 1) != 0, (flags & 2) != 0);
    }

    /// <summary>Uses the platform decoder as the ABI oracle.</summary>
    /// <param name="input">Input.</param><param name="length">Input count.</param><param name="output">Output.</param><param name="capacity">Output count.</param><param name="read">Consumed.</param><param name="written">Produced.</param><param name="flags">Codec flags.</param>
    /// <returns>The platform status.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Decode(byte* input, int length, char* output, int capacity, int* read, int* written, uint flags)
    {
        _decodeCalls++;
        return (int)Utf8.ToUtf16(new(input, length), new(output, capacity), out *read, out *written, (flags & 1) != 0, (flags & 2) != 0);
    }

    /// <summary>Rejects malformed text during equality.</summary>
    /// <param name="input">Encoded bytes.</param><param name="length">Byte count.</param><param name="text">Candidate.</param><param name="characters">Candidate length.</param>
    /// <returns>One for valid exact equality.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Utf8Equal(byte* input, int length, char* text, int characters)
    {
        try { return new UTF8Encoding(false, true).GetString(new ReadOnlySpan<byte>(input, length)).AsSpan().SequenceEqual(new ReadOnlySpan<char>(text, characters)) ? 1 : 0; }
        catch (DecoderFallbackException) { return 0; }
    }

    /// <summary>Returns invalid counts or status without accessing payload memory.</summary>
    /// <param name="input">Unused.</param><param name="length">Input count.</param><param name="output">Unused.</param><param name="capacity">Unused.</param><param name="read">Invalid count.</param><param name="written">Zero.</param><param name="flags">Unused.</param>
    /// <returns>An invalid count/status combination selected by the test.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int InvalidEncode(char* input, int length, byte* output, int capacity, int* read, int* written, uint flags)
    {
        *read = length; *written = 0;
        switch (_invalidResult)
        {
            case 0: *read = length + 1; return 0;
            case 1: *read = 0; return 0;
            case 2: *written = capacity + 1; return 0;
            case 3: return 4;
            case 4: *read = -1; return 0;
            default: *read = 0; *written = 1; return -1;
        }
    }

    /// <summary>Declines without writes or consumed input.</summary>
    /// <param name="input">Unused.</param><param name="length">Unused.</param><param name="output">Unused.</param><param name="capacity">Unused.</param><param name="read">Zero.</param><param name="written">Zero.</param><param name="flags">Unused.</param>
    /// <returns>Unsupported.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int UnsupportedEncode(char* input, int length, byte* output, int capacity, int* read, int* written, uint flags) { *read = *written = 0; return -1; }

    /// <summary>Copies only complete leading elements in ordered byte descriptors.</summary>
    /// <param name="input">Input.</param><param name="output">Output.</param><param name="count">Elements.</param><param name="sourceStride">Input stride.</param><param name="destinationStride">Output stride.</param><param name="from">Input offsets.</param><param name="to">Output offsets.</param><param name="bytes">Semantic byte count.</param>
    /// <returns>At most three completed elements.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Layout(byte* input, byte* output, int count, int sourceStride, int destinationStride, int* from, int* to, int bytes)
    {
        int done = Math.Min(3, count);
        for (int i = 0; i < done; i++) for (int j = 0; j < bytes; j++) output[i * destinationStride + to[j]] = input[i * sourceStride + from[j]];
        return done;
    }

    /// <summary>Returns an invalid prefix without memory access.</summary>
    /// <param name="input">Unused.</param><param name="output">Unused.</param><param name="count">Element count.</param><param name="sourceStride">Unused.</param><param name="destinationStride">Unused.</param><param name="from">Unused.</param><param name="to">Unused.</param><param name="bytes">Unused.</param>
    /// <returns>An out-of-range count.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int InvalidLayout(byte* input, byte* output, int count, int sourceStride, int destinationStride, int* from, int* to, int bytes) => count + 1;

    /// <summary>Counts bytes without reading beyond the supplied windows.</summary>
    /// <param name="left">First input.</param><param name="right">Second input.</param><param name="count">Maximum bytes.</param>
    /// <returns>The equal prefix.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static uint CountMatch(byte* left, byte* right, uint count) { _matchCalls++; uint i = 0; while (i < count && left[i] == right[i]) i++; return i; }

    /// <summary>Expands overlapping matches in increasing byte order.</summary>
    /// <param name="output">Output.</param><param name="count">Bytes.</param><param name="distance">Backward offset.</param>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CopyMatch(byte* output, uint count, uint distance) { _expandCalls++; for (uint i = 0; i < count; i++) output[i] = *(output + i - distance); }

    /// <summary>Computes consecutive blocks using the established scalar reference without changing caller state.</summary>
    /// <param name="state">Sixteen words.</param><param name="output">Complete output.</param><param name="blocks">Block count.</param>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Blocks(uint* state, byte* output, int blocks)
    {
        uint* copy = stackalloc uint[16];
        for (int i = 0; i < 16; i++) copy[i] = state[i];
        for (int i = 0; i < blocks; i++) { copy[12] = state[12] + (uint)i; ChaCha20.BlockScalar(copy, output + i * 64); }
    }

    /// <summary>Supports exact in-place input/output aliasing.</summary>
    /// <param name="output">Output.</param><param name="input">Input.</param><param name="stream">Keystream.</param><param name="count">Bytes.</param>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Xor(byte* output, byte* input, byte* stream, uint count) { _xorCalls++; for (uint i = 0; i < count; i++) output[i] = (byte)(input[i] ^ stream[i]); }
}
