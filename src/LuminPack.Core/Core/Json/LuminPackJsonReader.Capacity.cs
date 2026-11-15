using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Option;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

#endif
namespace LuminPack.Core
{
    public ref partial struct LuminPackJsonReader
    {
        /// <summary>Gets a structural sizing hint without advancing or validating the current JSON array.</summary>
        /// <returns>The hinted count, or minus one when sizing is unavailable.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public readonly int GetArrayLengthHint() => GetArrayLengthHint<LuminPackDefaultOptions>();
        /// <summary>Gets a capacity hint only when enough input remains to justify a structural scan.</summary>
        /// <param name = "minimum">Minimum remaining input code units; negative values decline sizing.</param>
        /// <returns>A nonnegative capacity hint, or zero when sizing is unavailable.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public readonly int GetArrayCapacityHint(int minimum) => GetArrayCapacityHint<LuminPackDefaultOptions>(minimum);
        /// <summary>Gets a capacity hint using the document's compiled text encoding.</summary>
        /// <typeparam name = "TOptions">The compiled document profile.</typeparam>
        /// <param name = "minimum">Minimum remaining input code units; negative values decline sizing.</param>
        /// <returns>A nonnegative capacity hint, or zero when sizing is unavailable.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetArrayCapacityHint<TOptions>(int minimum)
            where TOptions : struct, ILuminPackOptions
            => HasArrayCapacityInput<TOptions>(minimum) ? Math.Max(0, GetArrayLengthHint<TOptions>()) : 0;

        /// <summary>Checks for array capacity input.</summary>
        /// <typeparam name="TOptions">The compiled serialization options.</typeparam>
        /// <param name="minimum">The minimum supplied to <c>HasArrayCapacityInput</c>.</param>
        /// <returns>Whether the input has array capacity input.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool HasArrayCapacityInput<TOptions>(int minimum)
            where TOptions : struct, ILuminPackOptions
        {
            int position = Position;
            if ((uint)position > (uint)Buffer.Length)
                return false;
            int step = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
            return (uint)minimum <= (uint)(Buffer.Length - position) / (uint)step;
        }

        /// <summary>Counts outer array separators while ignoring quoted and nested contents.</summary>
        /// <typeparam name = "TOptions">The compiled document profile.</typeparam>
        /// <returns>The structural count, or minus one when no complete array is available.</returns>
        /// <remarks>This is a read-only sizing hint, not syntax validation. Consumers must retain their ordinary
        /// parsing, bounds checks and growth fallback. The reader must be positioned immediately after ArrayStart.</remarks>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public readonly int GetArrayLengthHint<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.ArrayStart)
                return -1;
            int position = Position;
            if ((uint)position > (uint)Buffer.Length)
                return -1;
            var tail = Buffer.Slice(position);
#if NETSTANDARD2_1
            if (LuminPackBurst.TryJsonArrayHint(tail, default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8, false, out int nativeCount))
                return nativeCount;
#endif
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
                return ScanHintByte(tail);
            if ((tail.Length & 1) != 0)
                return -1;
            return ScanHintChar(MemoryMarshal.Cast<byte, char>(tail));
        }

        /// <summary>Gets a sizing hint for an array of flat scalar or string key/value pairs.</summary>
        /// <returns>A nonnegative hint, or zero when sizing is unavailable.</returns>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public readonly int GetEntryArrayCapacityHint() => GetEntryArrayCapacityHint<LuminPackDefaultOptions>();
        /// <summary>Counts flat pair entries without traversing each pair delimiter separately.</summary>
        /// <typeparam name = "TOptions">The compiled document profile.</typeparam>
        /// <returns>A nonnegative hint, or zero when sizing is unavailable.</returns>
        /// <remarks>Only scalar, string and null pair members qualify. Nested container members require the
        /// general array hint. This operation does not validate syntax or change reader state.</remarks>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public readonly int GetEntryArrayCapacityHint<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            int position = Position;
            int step = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 1 : 2;
            if (CurrentTokenType != JsonTokenType.ArrayStart || (uint)position > (uint)Buffer.Length || Buffer.Length - position < 24 * step)
                return 0;
            var tail = Buffer.Slice(position);
            if (step == 2 && (tail.Length & 1) != 0)
                return 0;
#if NETSTANDARD2_1
            if (LuminPackBurst.TryJsonArrayHint(tail, step == 1, true, out int nativeCount))
                return Math.Max(0, nativeCount);
#endif
            return Math.Max(0, step == 1 ? ScanEntryByte(tail) : ScanEntryChar(MemoryMarshal.Cast<byte, char>(tail)));
        }

        /// <summary>Selects flat-pair structural scanning for UTF-8.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanEntryByte(ReadOnlySpan<byte> input) => ScanHintByte(input, true);
        /// <summary>Scans complete UTF-8 blocks with cross-block quote and escape state.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <param name = "entries">Whether members are flat scalar/string pairs.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ScanHintByte(ReadOnlySpan<byte> input, bool entries = false)
        {
            int offset = 0;
            while (offset < input.Length && (input[offset] == ' ' || input[offset] == '\t' || input[offset] == '\n' || input[offset] == '\r'))
                offset++;
            if (offset == input.Length)
                return -1;
            if (input[offset] == ']')
                return 0;
            int count = entries ? 0 : 1, depth = 0;
            ulong pairDepth = 0;
            bool quoted = false, slashCarry = false;
            while (offset < input.Length)
            {
                int remaining = input.Length - offset;
                int start = offset, width;
                ulong opens, closes, commas, quotes, slashes;
#if NET8_0_OR_GREATER
                if ((Vector512.IsHardwareAccelerated && Avx512F.IsSupported && Avx512BW.IsSupported) && remaining >= 64)
                {
                    var v = Vector512.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector512.Create((byte)32);
                    quotes = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((byte)'"')));
                    slashes = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((byte)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 64;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector512.ExtractMostSignificantBits(Vector512.Equals(folded, Vector512.Create((byte)'{')));
                    closes = Vector512.ExtractMostSignificantBits(Vector512.Equals(folded, Vector512.Create((byte)'}')));
                    commas = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((byte)',')));
                    width = 64;
                }
                else if ((Avx2.IsSupported) && remaining >= 32)
                {
                    var v = Vector256.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector256.Create((byte)32);
                    quotes = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((byte)'"')));
                    slashes = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((byte)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 32;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector256.ExtractMostSignificantBits(Vector256.Equals(folded, Vector256.Create((byte)'{')));
                    closes = Vector256.ExtractMostSignificantBits(Vector256.Equals(folded, Vector256.Create((byte)'}')));
                    commas = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((byte)',')));
                    width = 32;
                }
                else if ((Sse2.IsSupported || AdvSimd.IsSupported) && remaining >= 16)
                {
                    var v = Vector128.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector128.Create((byte)32);
                    quotes = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((byte)'"')));
                    slashes = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((byte)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 16;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector128.ExtractMostSignificantBits(Vector128.Equals(folded, Vector128.Create((byte)'{')));
                    closes = Vector128.ExtractMostSignificantBits(Vector128.Equals(folded, Vector128.Create((byte)'}')));
                    commas = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((byte)',')));
                    width = 16;
                }
                else
#else
                if (Vector.IsHardwareAccelerated && remaining >= Vector<byte>.Count)
                {
                    var v = new Vector<byte>(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(input.Slice(offset)), Vector<byte>.Count));
                    var folded = v | new Vector<byte>((byte)32);
                    quotes = Packbyte(Vector.Equals(v, new Vector<byte>((byte)'"')));
                    slashes = Packbyte(Vector.Equals(v, new Vector<byte>((byte)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += Vector<byte>.Count;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Packbyte(Vector.Equals(folded, new Vector<byte>((byte)'{')));
                    closes = Packbyte(Vector.Equals(folded, new Vector<byte>((byte)'}')));
                    commas = Packbyte(Vector.Equals(v, new Vector<byte>((byte)',')));
                    width = Vector<byte>.Count;
                }
                else
#endif
                if (IntPtr.Size == 8 && remaining >= 8)
                {
                    var v = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input.Slice(offset, 8))));
                    var folded = v | 0x2020202020202020UL;
                    quotes = PackByte64(ExactZero64(v ^ 0x2222222222222222UL, 0x8080808080808080UL));
                    slashes = PackByte64(ExactZero64(v ^ 0x5C5C5C5C5C5C5C5CUL, 0x8080808080808080UL));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 8;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = PackByte64(ExactZero64(folded ^ 0x7B7B7B7B7B7B7B7BUL, 0x8080808080808080UL));
                    closes = PackByte64(ExactZero64(folded ^ 0x7D7D7D7D7D7D7D7DUL, 0x8080808080808080UL));
                    commas = PackByte64(ExactZero64(v ^ 0x2C2C2C2C2C2C2C2CUL, 0x8080808080808080UL));
                    width = 8;
                }
                else if (remaining >= 4)
                {
                    var v = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input.Slice(offset, 4))));
                    var folded = v | 0x20202020U;
                    quotes = PackByte32(ExactZero32(v ^ 0x22222222U, 0x80808080U));
                    slashes = PackByte32(ExactZero32(v ^ 0x5C5C5C5CU, 0x80808080U));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 4;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintByte(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = PackByte32(ExactZero32(folded ^ 0x7B7B7B7BU, 0x80808080U));
                    closes = PackByte32(ExactZero32(folded ^ 0x7D7D7D7DU, 0x80808080U));
                    commas = PackByte32(ExactZero32(v ^ 0x2C2C2C2CU, 0x80808080U));
                    width = 4;
                }
                else
                {
                    var ch = input[offset];
                    quotes = ch == '"' ? 1UL : 0;
                    slashes = ch == '\\' ? 1UL : 0;
                    opens = ch == '[' || ch == '{' ? 1UL : 0;
                    closes = ch == ']' || ch == '}' ? 1UL : 0;
                    commas = ch == ',' ? 1UL : 0;
                    width = 1;
                }

                offset += width;
                if (slashes != 0 || slashCarry)
                    quotes = RemoveEscapedQuotes(quotes, slashes, width, ref slashCarry);
                if (quotes != 0 || quoted)
                {
                    ulong inside = GetQuotePrefix(quotes, width);
                    if (quoted)
                        inside = ~inside;
                    quoted = ((inside >> (width - 1)) & 1) != 0;
                    opens &= ~inside;
                    closes &= ~inside;
                    commas &= ~inside;
                }

                if (entries)
                {
                    ulong unmatched = closes & (closes - opens - pairDepth);
                    if (unmatched != 0)
                    {
                        ulong end = unmatched & (0UL - unmatched);
                        return count + CountHintBits(opens & (end - 1));
                    }

                    count += CountHintBits(opens);
                    pairDepth = closes < opens + pairDepth ? 1UL : 0UL;
                    continue;
                }

                ulong boundaries = opens | closes;
                while (boundaries != 0)
                {
                    ulong bit = boundaries & (0UL - boundaries);
                    boundaries &= boundaries - 1;
                    if (depth == 0)
                        count += CountHintBits(commas & (bit - 1));
                    commas &= ~(bit - 1);
                    if ((opens & bit) != 0)
                        depth++;
                    else if (depth == 0)
                        return input[start + HintTrailingZero(bit)] == ']' ? count : -1;
                    else
                        depth--;
                }

                if (depth == 0)
                    count += CountHintBits(commas);
            }

            return -1;
        }

        /// <summary>Selects flat-pair structural scanning for UTF-16.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanEntryChar(ReadOnlySpan<char> input) => ScanHintChar(input, true);
        /// <summary>Scans complete UTF-16 blocks with cross-block quote and escape state.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <param name = "entries">Whether members are flat scalar/string pairs.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ScanHintChar(ReadOnlySpan<char> input, bool entries = false)
        {
            int offset = 0;
            while (offset < input.Length && (input[offset] == ' ' || input[offset] == '\t' || input[offset] == '\n' || input[offset] == '\r'))
                offset++;
            if (offset == input.Length)
                return -1;
            if (input[offset] == ']')
                return 0;
            int count = entries ? 0 : 1, depth = 0;
            ulong pairDepth = 0;
            bool quoted = false, slashCarry = false;
            while (offset < input.Length)
            {
                int remaining = input.Length - offset;
                int start = offset, width;
                ulong opens, closes, commas, quotes, slashes;
#if NET8_0_OR_GREATER
                if ((Vector512.IsHardwareAccelerated && Avx512F.IsSupported && Avx512BW.IsSupported) && remaining >= 32)
                {
                    var v = Vector512.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector512.Create((ushort)32);
                    quotes = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((ushort)'"')));
                    slashes = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((ushort)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 32;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector512.ExtractMostSignificantBits(Vector512.Equals(folded, Vector512.Create((ushort)'{')));
                    closes = Vector512.ExtractMostSignificantBits(Vector512.Equals(folded, Vector512.Create((ushort)'}')));
                    commas = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((ushort)',')));
                    width = 32;
                }
                else if ((Avx2.IsSupported) && remaining >= 16)
                {
                    var v = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector256.Create((ushort)32);
                    quotes = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((ushort)'"')));
                    slashes = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((ushort)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 16;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector256.ExtractMostSignificantBits(Vector256.Equals(folded, Vector256.Create((ushort)'{')));
                    closes = Vector256.ExtractMostSignificantBits(Vector256.Equals(folded, Vector256.Create((ushort)'}')));
                    commas = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((ushort)',')));
                    width = 16;
                }
                else if ((Sse2.IsSupported || AdvSimd.IsSupported) && remaining >= 8)
                {
                    var v = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)offset);
                    var folded = v | Vector128.Create((ushort)32);
                    quotes = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((ushort)'"')));
                    slashes = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((ushort)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 8;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Vector128.ExtractMostSignificantBits(Vector128.Equals(folded, Vector128.Create((ushort)'{')));
                    closes = Vector128.ExtractMostSignificantBits(Vector128.Equals(folded, Vector128.Create((ushort)'}')));
                    commas = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((ushort)',')));
                    width = 8;
                }
                else
#else
                if (Vector.IsHardwareAccelerated && remaining >= Vector<ushort>.Count)
                {
                    var v = new Vector<ushort>(MemoryMarshal.CreateSpan(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input.Slice(offset))), Vector<ushort>.Count));
                    var folded = v | new Vector<ushort>((ushort)32);
                    quotes = Packushort(Vector.Equals(v, new Vector<ushort>((ushort)'"')));
                    slashes = Packushort(Vector.Equals(v, new Vector<ushort>((ushort)'\\')));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += Vector<ushort>.Count;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = Packushort(Vector.Equals(folded, new Vector<ushort>((ushort)'{')));
                    closes = Packushort(Vector.Equals(folded, new Vector<ushort>((ushort)'}')));
                    commas = Packushort(Vector.Equals(v, new Vector<ushort>((ushort)',')));
                    width = Vector<ushort>.Count;
                }
                else
#endif
                if (IntPtr.Size == 8 && remaining >= 4)
                {
                    var v = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(input.Slice(offset, 4))));
                    var folded = v | 0x20002000200020UL;
                    quotes = PackChar64(ExactZero64(v ^ 0x22002200220022UL, 0x8000800080008000UL));
                    slashes = PackChar64(ExactZero64(v ^ 0x5C005C005C005CUL, 0x8000800080008000UL));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 4;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = PackChar64(ExactZero64(folded ^ 0x7B007B007B007BUL, 0x8000800080008000UL));
                    closes = PackChar64(ExactZero64(folded ^ 0x7D007D007D007DUL, 0x8000800080008000UL));
                    commas = PackChar64(ExactZero64(v ^ 0x2C002C002C002CUL, 0x8000800080008000UL));
                    width = 4;
                }
                else if (remaining >= 2)
                {
                    var v = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(input.Slice(offset, 2))));
                    var folded = v | 0x200020U;
                    quotes = PackChar32(ExactZero32(v ^ 0x220022U, 0x80008000U));
                    slashes = PackChar32(ExactZero32(v ^ 0x5C005CU, 0x80008000U));
                    if (quoted && (quotes | slashes) == 0)
                    {
                        offset += 2;
                        slashCarry = false;
                        if (input.Length - offset >= 128)
                        {
                            int next = FindHintChar(input.Slice(offset));
                            if (next < 0)
                                return -1;
                            offset += next;
                        }

                        continue;
                    }

                    opens = PackChar32(ExactZero32(folded ^ 0x7B007BU, 0x80008000U));
                    closes = PackChar32(ExactZero32(folded ^ 0x7D007DU, 0x80008000U));
                    commas = PackChar32(ExactZero32(v ^ 0x2C002CU, 0x80008000U));
                    width = 2;
                }
                else
                {
                    var ch = input[offset];
                    quotes = ch == '"' ? 1UL : 0;
                    slashes = ch == '\\' ? 1UL : 0;
                    opens = ch == '[' || ch == '{' ? 1UL : 0;
                    closes = ch == ']' || ch == '}' ? 1UL : 0;
                    commas = ch == ',' ? 1UL : 0;
                    width = 1;
                }

                offset += width;
                if (slashes != 0 || slashCarry)
                    quotes = RemoveEscapedQuotes(quotes, slashes, width, ref slashCarry);
                if (quotes != 0 || quoted)
                {
                    ulong inside = GetQuotePrefix(quotes, width);
                    if (quoted)
                        inside = ~inside;
                    quoted = ((inside >> (width - 1)) & 1) != 0;
                    opens &= ~inside;
                    closes &= ~inside;
                    commas &= ~inside;
                }

                if (entries)
                {
                    ulong unmatched = closes & (closes - opens - pairDepth);
                    if (unmatched != 0)
                    {
                        ulong end = unmatched & (0UL - unmatched);
                        return count + CountHintBits(opens & (end - 1));
                    }

                    count += CountHintBits(opens);
                    pairDepth = closes < opens + pairDepth ? 1UL : 0UL;
                    continue;
                }

                ulong boundaries = opens | closes;
                while (boundaries != 0)
                {
                    ulong bit = boundaries & (0UL - boundaries);
                    boundaries &= boundaries - 1;
                    if (depth == 0)
                        count += CountHintBits(commas & (bit - 1));
                    commas &= ~(bit - 1);
                    if ((opens & bit) != 0)
                        depth++;
                    else if (depth == 0)
                        return input[start + HintTrailingZero(bit)] == ']' ? count : -1;
                    else
                        depth--;
                }

                if (depth == 0)
                    count += CountHintBits(commas);
            }

            return -1;
        }

        /// <summary>Computes inclusive quote parity for the valid lanes in this block.</summary>
        /// <param name="quotes">Positions of unescaped quotes.</param>
        /// <param name="width">The number of valid lanes.</param>
        /// <returns>The parity in the low <paramref name="width"/> bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetQuotePrefix(ulong quotes, int width)
        {
#if NET8_0_OR_GREATER
            if (Pclmulqdq.X64.IsSupported)
            {
                // A polynomial product by all ones xors each quote into every subsequent lane.
                return Pclmulqdq.CarrylessMultiply(Vector128.CreateScalarUnsafe(quotes), Vector128.Create(ulong.MaxValue), 0).ToScalar();
            }
#endif
            quotes ^= quotes << 1;
            quotes ^= quotes << 2;
            if (width > 4)
            {
                quotes ^= quotes << 4;
                quotes ^= quotes << 8;
                if (width > 16)
                {
                    quotes ^= quotes << 16;
                    if (width > 32)
                        quotes ^= quotes << 32;
                }
            }
            return quotes;
        }

        /// <summary>Removes quotes preceded by odd-length backslash runs across block boundaries.</summary>
        /// <param name = "quotes">Quote positions.</param>
        /// <param name = "slashes">Backslash positions.</param>
        /// <param name = "width">Valid lanes in this block.</param>
        /// <param name = "carry">Odd trailing backslash state.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ulong RemoveEscapedQuotes(ulong quotes, ulong slashes, int width, ref bool carry)
        {
            // Subtraction propagates each run's parity without visiting its individual backslashes.
            // The incoming escape consumes lane zero, including when that lane is another backslash.
            // This is the escape/terminal mask identity used by simdjson's stage-one escape scanner.
            const ulong odd = 0xAAAAAAAAAAAAAAAAUL;
            ulong incoming = carry ? 1UL : 0UL;
            ulong active = slashes & ~incoming;
            ulong terminals = unchecked(((active << 1) | odd) - active) ^ odd;
            ulong escaped = terminals ^ (slashes | incoming);
            carry = ((terminals & slashes) >> (width - 1) & 1) != 0;
            return quotes & ~escaped;
        }

        /// <summary>Identifies zero byte or word lanes without cross-lane borrow artifacts.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <param name = "high">The high bit of each lane.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExactZero64(ulong value, ulong high)
        {
            ulong low = ~high;
            return ~(((value & low) + low) | value | low);
        }

        /// <summary>Identifies zero lanes in a bounded four-byte word.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <param name = "high">The high bit of each lane.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ExactZero32(uint value, uint high)
        {
            uint low = ~high;
            return ~(((value & low) + low) | value | low);
        }

        /// <summary>Packs eight byte sign bits in increasing address order.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong PackByte64(ulong value) => ((value & 0x8080808080808080UL) * 0x0002040810204081UL) >> 56;
        /// <summary>Packs four UTF-16 sign bits in increasing address order.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong PackChar64(ulong value) => ((value & 0x8000800080008000UL) * 0x0000200040008001UL) >> 60;
        /// <summary>Packs four byte sign bits in increasing address order.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackByte32(uint value) => ((value & 0x80808080U) * 0x00204081U) >> 28;
        /// <summary>Packs two UTF-16 sign bits in increasing address order.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackChar32(uint value) => ((value & 0x80008000U) * 0x00008001U) >> 30;
        /// <summary>Counts structural mask bits using native or portable arithmetic.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountHintBits(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
#endif
        }

        /// <summary>Finds the next mask bit, returning sixty-four for an empty mask.</summary>
        /// <param name = "value">The input word or mask.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HintTrailingZero(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.TrailingZeroCount(value);
#else
            if (value == 0)
                return 64;
            int n = 0;
            if ((uint)value == 0)
            {
                n = 32;
                value >>= 32;
            }

            uint low = (uint)value;
            if ((low & 65535) == 0)
            {
                n += 16;
                low >>= 16;
            }

            if ((low & 255) == 0)
            {
                n += 8;
                low >>= 8;
            }

            if ((low & 15) == 0)
            {
                n += 4;
                low >>= 4;
            }

            if ((low & 3) == 0)
            {
                n += 2;
                low >>= 2;
            }

            return n + (1 - (int)(low & 1));
#endif
        }

#if !NET8_0_OR_GREATER
        /// <summary>Packs portable vector byte comparisons without per-lane extraction loops on 64-bit.</summary>
        /// <param name = "v">The comparison vector.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Packbyte(Vector<byte> v)
        {
            if (IntPtr.Size == 8)
            {
                var w = Vector.AsVectorUInt64(v);
                ulong m = PackByte64(w[0]) | (PackByte64(w[1]) << 8);
                if (Vector<ulong>.Count > 2)
                    m |= (PackByte64(w[2]) << 16) | (PackByte64(w[3]) << 24);
                return m;
            }

            var words = Vector.AsVectorUInt32(v);
            ulong result = 0;
            for (int k = 0; k < Vector<uint>.Count; k++)
                result |= (ulong)(((words[k] & 0x80808080U) * 0x00204081U) >> 28) << (k * 4);
            return result;
        }

        /// <summary>Packs portable vector word comparisons without per-lane extraction loops on 64-bit.</summary>
        /// <param name = "v">The comparison vector.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Packushort(Vector<ushort> v)
        {
            if (IntPtr.Size == 8)
            {
                var w = Vector.AsVectorUInt64(v);
                ulong m = PackChar64(w[0]) | (PackChar64(w[1]) << 4);
                if (Vector<ulong>.Count > 2)
                    m |= (PackChar64(w[2]) << 8) | (PackChar64(w[3]) << 12);
                return m;
            }

            var words = Vector.AsVectorUInt32(v);
            ulong result = 0;
            for (int k = 0; k < Vector<uint>.Count; k++)
                result |= (ulong)(((words[k] & 0x80008000U) * 0x00008001U) >> 30) << (k * 2);
            return result;
        }

#endif
        /// <summary>Skips a plain UTF-8 string run within its complete input window.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int FindHintByte(ReadOnlySpan<byte> input)
        {
            int i = 0;
            while (i < input.Length)
            {
                int remaining = input.Length - i, width;
                ulong mask;
#if NET8_0_OR_GREATER
                if ((Vector512.IsHardwareAccelerated && Avx512F.IsSupported && Avx512BW.IsSupported) && remaining >= 64)
                {
                    var v = Vector512.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((byte)'"')) | Vector512.Equals(v, Vector512.Create((byte)'\\')));
                    width = 64;
                }
                else if ((Avx2.IsSupported) && remaining >= 32)
                {
                    var v = Vector256.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((byte)'"')) | Vector256.Equals(v, Vector256.Create((byte)'\\')));
                    width = 32;
                }
                else if ((Sse2.IsSupported || AdvSimd.IsSupported) && remaining >= 16)
                {
                    var v = Vector128.LoadUnsafe(ref Unsafe.As<byte, byte>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((byte)'"')) | Vector128.Equals(v, Vector128.Create((byte)'\\')));
                    width = 16;
                }
                else
#else
                return input.Slice(i).IndexOfAny((byte)'"', (byte)'\\')is int found && found >= 0 ? i + found : -1;
#endif
                {
                    var ch = input[i];
                    mask = ch == '"' || ch == '\\' ? 1UL : 0;
                    width = 1;
                }

                if (mask != 0)
                    return i + HintTrailingZero(mask);
                i += width;
            }

            return -1;
        }

        /// <summary>Skips a plain UTF-16 string run within its complete input window.</summary>
        /// <param name = "input">The complete input window.</param>
        /// <returns>The computed count, position or mask.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int FindHintChar(ReadOnlySpan<char> input)
        {
            int i = 0;
            while (i < input.Length)
            {
                int remaining = input.Length - i, width;
                ulong mask;
#if NET8_0_OR_GREATER
                if ((Vector512.IsHardwareAccelerated && Avx512F.IsSupported && Avx512BW.IsSupported) && remaining >= 32)
                {
                    var v = Vector512.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector512.ExtractMostSignificantBits(Vector512.Equals(v, Vector512.Create((ushort)'"')) | Vector512.Equals(v, Vector512.Create((ushort)'\\')));
                    width = 32;
                }
                else if ((Avx2.IsSupported) && remaining >= 16)
                {
                    var v = Vector256.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector256.ExtractMostSignificantBits(Vector256.Equals(v, Vector256.Create((ushort)'"')) | Vector256.Equals(v, Vector256.Create((ushort)'\\')));
                    width = 16;
                }
                else if ((Sse2.IsSupported || AdvSimd.IsSupported) && remaining >= 8)
                {
                    var v = Vector128.LoadUnsafe(ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(input)), (nuint)i);
                    mask = Vector128.ExtractMostSignificantBits(Vector128.Equals(v, Vector128.Create((ushort)'"')) | Vector128.Equals(v, Vector128.Create((ushort)'\\')));
                    width = 8;
                }
                else
#else
                return input.Slice(i).IndexOfAny((char)'"', (char)'\\')is int found && found >= 0 ? i + found : -1;
#endif
                {
                    var ch = input[i];
                    mask = ch == '"' || ch == '\\' ? 1UL : 0;
                    width = 1;
                }

                if (mask != 0)
                    return i + HintTrailingZero(mask);
                i += width;
            }

            return -1;
        }
    }
}
