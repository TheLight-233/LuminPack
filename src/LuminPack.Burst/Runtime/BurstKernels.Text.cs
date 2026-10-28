#if LUMINPACK_BURST
using System;
using System.Buffers;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
namespace LuminPack.Burst
{
    public static unsafe partial class BurstKernels
    {
        /// <summary>Compares complete ordinary byte ranges using the platform comparison primitive; never used for authentication tags.</summary><param name="left">First range.</param><param name="right">Second range.</param><param name="count">Byte count.</param><returns>One for equality.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.EqualFunction))]
        public static int Equal(byte* left, byte* right, uint count) => KernelMemory.SequenceEqual<byte>(new ReadOnlySpan<byte>(left, (int)count), new ReadOnlySpan<byte>(right, (int)count)) ? 1 : 0;
        /// <summary>Adapts Core's UTF-8 encoder without changing replacement, streaming or bounded-tail semantics.</summary><param name="source">Input.</param><param name="length">Character count.</param><param name="destination">Output.</param><param name="capacity">Byte capacity.</param><param name="consumed">Consumed characters.</param><param name="written">Produced bytes.</param><param name="flags">Replacement bit zero and final-block bit one.</param><returns>The operation status.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.Utf8EncodeFunction))]
        public static int Utf8Encode(ushort* source, int length, byte* destination, int capacity, int* consumed, int* written, uint flags)
        {
            var status = CoreTextAlgorithms.SerializeAsUtf8(new ReadOnlySpan<char>((char*)source, length), new Span<byte>(destination, capacity), out int read, out int produced, (flags & 1) != 0, (flags & 2) != 0);
            *consumed = read; *written = produced; return (int)status;
        }
        /// <summary>Adapts Core's UTF-8 decoder and its complete ASCII widening fast path.</summary><param name="source">Input.</param><param name="length">Byte count.</param><param name="destination">Output.</param><param name="capacity">Character capacity.</param><param name="consumed">Consumed bytes.</param><param name="written">Produced characters.</param><param name="flags">Replacement bit zero and final-block bit one.</param><returns>The operation status.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.Utf8DecodeFunction))]
        public static int Utf8Decode(byte* source, int length, ushort* destination, int capacity, int* consumed, int* written, uint flags)
        {
            var status = PortableUtf8.ToUtf16(new ReadOnlySpan<byte>(source, length), new Span<char>((char*)destination, capacity), out int read, out int produced, (flags & 1) != 0, (flags & 2) != 0);
            *consumed = read; *written = produced; return (int)status;
        }
        /// <summary>Compares complete UTF-8 input to a candidate using Core's strict comparison paths.</summary><param name="source">Input.</param><param name="length">Byte count.</param><param name="candidate">Candidate.</param><param name="characters">Character count.</param><returns>One only for valid equal text.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.Utf8EqualFunction))]
        public static int Utf8Equal(byte* source, int length, ushort* candidate, int characters) => CoreTextAlgorithms.MatchesUtf8(new ReadOnlySpan<byte>(source, length), new ReadOnlySpan<char>((char*)candidate, characters)) ? 1 : 0;
        /// <summary>Uses Core's existing short ASCII eligibility, full validation and exact stores.</summary><param name="source">Input.</param><param name="length">Character count.</param><param name="destination">Output with complete input-sized capacity.</param><param name="flags">JSON escaping bit zero.</param><returns>One on success; zero without writes.</returns>
        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(BurstBackend.AsciiEncodeFunction))]
        public static int AsciiEncode(ushort* source, int length, byte* destination, uint flags) => CoreShortAscii.TryEncode(new ReadOnlySpan<char>((char*)source, length), new Span<byte>(destination, length), (flags & 1) != 0) ? 1 : 0;
    }
}
#endif

