using System.Buffers;
using System.Buffers.Binary;
using LuminPack.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;

#endif
namespace LuminPack.Core
{
    public ref partial struct LuminPackReader
    {
        /// <summary>Validates string length before the operation continues.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void ValidateStringLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, ILuminPackOptions
        {
            if (length < 0 && length != LuminPackCode.NullCollection)
                LuminPackExceptionHelper.ThrowInvalidStringLength(length);
            int headerSize = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int);
            if ((uint)index > (uint)buffer.Length || (uint)headerSize > (uint)(buffer.Length - index) || (length >= 0 && (uint)length > (uint)(buffer.Length - index - headerSize)))
                LuminPackExceptionHelper.ThrowInSufficientBuffer(length);
        }

        /// <summary>
        /// Calculates the binary string encoding length.
        /// </summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index"></param>
        /// <param name = "length"></param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void ReadStringLength<TOptions>(Span<byte> buffer, ref int index, out int length)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringRecording == LuminPackStringRecording.Token))
            {
                var remaining = buffer.Slice(index);
                if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
                {
                    length = remaining.IndexOf((byte)0);
                }
                else
                {
                    var characters = MemoryMarshal.Cast<byte, ushort>(remaining.Slice(0, remaining.Length & ~1));
                    var characterLength = characters.IndexOf((ushort)0);
                    length = characterLength < 0 ? -1 : checked(characterLength * sizeof(ushort));
                }

                if (length < 0)
                    LuminPackExceptionHelper.ThrowInvalidDataException("String token terminator was not found.");
                return;
            }

            EnsureReadable(buffer, index, sizeof(int));
#if NET8_0_OR_GREATER
            length = LuminPackReader.Read<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index));
#else
            length = LuminPackReader.Read<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index));
#endif
            ValidateStringLength<TOptions>(buffer, index, length);
        }

        /// <summary>Returns the fixed framing width or the variable-length header upper bound of the current mode.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int StringRecordLength<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
                return default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 10 : 5;
            return (recording == LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 8 : 4;
        }

        /// <summary>
        /// Reads one string record and advances the supplied absolute offset.
        /// Intended for source-generated parsers so the JIT sees a single call and
        /// does not have to keep the offset live across four string helpers.
        /// </summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string? ReadStringAndAdvance<TOptions>(Span<byte> buffer, ref int index)
            where TOptions : struct, ILuminPackOptions
        {
            bool isUtf8 = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8);
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.Token)
            {
                return ReadTokenStringAndAdvance<TOptions>(buffer, ref index, isUtf8);
            }
            if (recording == LuminPackStringRecording.VarInt)
            {
                string? result = ReadVarIntRecord<TOptions>(buffer.Slice(index), null, out int consumed);
                index += consumed;
                return result;
            }

            int recordLength = isUtf8 ? sizeof(int) * 2 : sizeof(int);
            int bufferLength = buffer.Length;
            if ((uint)index > (uint)bufferLength || (uint)recordLength > (uint)(bufferLength - index))
            {
                LuminPackExceptionHelper.ThrowInSufficientBuffer(recordLength);
            }

#if NET8_0_OR_GREATER
            int length = LuminPackReader.Read<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index));
#else
            int length = LuminPackReader.Read<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index));
#endif
            if (length < 0)
            {
                if (length != LuminPackCode.NullCollection)
                    LuminPackExceptionHelper.ThrowInvalidStringLength(length);
                index += recordLength;
                return null;
            }

            // A UTF-16 header is the raw byte count or the compact Latin-1 form; both carry their payload width.
            int payload = isUtf8 ? length : StringSerializer.Utf16RecordPayload(length);
            if ((uint)payload > (uint)(bufferLength - index - recordLength))
            {
                LuminPackExceptionHelper.ThrowInSufficientBuffer(payload);
            }

            var value = payload == 0 ? string.Empty : isUtf8 ? ReadUtf8StringWithLength<TOptions>(buffer, index, length) : ReadUtf16StringWithLength<TOptions>(buffer, index, length);
            index += payload + recordLength;
            return value;
        }

        /// <summary>Reads token string and advance from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "isUtf8">The is utf8 used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string? ReadTokenStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, bool isUtf8)
            where TOptions : struct, ILuminPackOptions
        {
            ReadStringLength<TOptions>(buffer, ref index, out var length);
            // Token mode has no null marker: null and empty are both encoded as an empty
            // token and must therefore deserialize to string.Empty.
            var value = isUtf8 ? ReadUtf8StringWithToken<TOptions>(buffer, index, length) : ReadUtf16StringWithToken<TOptions>(buffer, index, length);
            index += length + (isUtf8 ? 1 : 2);
            return value;
        }

        /// <summary>Reads utf8 string with token from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf8StringWithToken<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index);
            if ((uint)(length - 1) <= 2)
            {
                var tinyAscii = StringSerializer.ReadTinyAsciiString(ref spanRef, length);
                if (tinyAscii is not null)
                    return tinyAscii;
            }
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index);
#endif
            return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
        }

        /// <summary>Reads utf16 string with token from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf16StringWithToken<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index);
#endif
            return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
        }

        /// <summary>Reads utf8 string with length from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf8StringWithLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, ILuminPackOptions
        {
            if (length < 0)
                return null;
            if (length == 0)
                return string.Empty;
            ref byte count = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(index + sizeof(int)));
            return StringSerializer.DeserializeUtf8(ref Unsafe.Add(ref count, sizeof(int)), length, LuminPackReader.Read<int>(ref count));
        }

        /// <summary>Reads utf16 string with length from the current input.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static string? ReadUtf16StringWithLength<TOptions>(Span<byte> buffer, int index, int length)
            where TOptions : struct, ILuminPackOptions
        {
            if (length < 0)
                return null;
            int payload = StringSerializer.Utf16RecordPayload(length);
            if (payload == 0)
                return string.Empty;
            int index1 = index + 4;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(buffer)), (nint)(uint)index1);
#endif
            if ((length & 1) != 0)
                return StringSerializer.CreateLatin1String(ref spanRef, payload);
            return StringSerializer.DeserializeUtf16(MemoryMarshal.CreateReadOnlySpan(ref spanRef, length));
        }

        /// <summary>Reads a compiled-profile string record without allocating an unchanged string.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The absolute input cursor, advanced only after a successful read.</param>
        /// <param name = "current">The string available for reuse.</param>
        /// <returns>The existing string when equal, or the normally decoded value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string? ReadStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, string? current)
            where TOptions : struct, ILuminPackOptions
        {
            if (current is null)
                return ReadStringAndAdvance<TOptions>(buffer, ref index);
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                // The shared record reader validates the complete record, compares either payload form once and
                // publishes the cursor only after success.
                int start = index;
                if ((uint)start <= (uint)buffer.Length)
                {
                    string? text = StringSerializer.ReadUtf16Record(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)start), buffer.Length - start, current, out int consumed);
                    index = start + consumed;
                    return text;
                }
            }
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                int start = index;
                int available = buffer.Length;
                if ((uint)start <= (uint)available && available - start >= sizeof(int) * 2)
                {
                    ref byte destination = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(start));
                    int length = 0;
                    length += LuminPackReader.Read(ref Unsafe.Add(ref destination, (nint)(uint)length), out int bytes);
                    length += LuminPackReader.Read(ref Unsafe.Add(ref destination, (nint)(uint)length), out int characters);
                    if (bytes > 0 && bytes <= available - start - sizeof(int) * 2)
                    {
                        if (characters > 0 && characters != current.Length)
                            return ReadStringAndAdvance<TOptions>(buffer, ref index);
                        ref byte payloadStart = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)start + sizeof(int) * 2);
                        bool matches;
                        if (bytes == current.Length)
                        {
                            // Short ASCII records dominate reuse traffic. The framework helper's fixed cost
                            // (span construction plus a cross-assembly vector setup) outweighs its work at these
                            // lengths, so compare short records in-assembly and keep the helper for longer ones.
                            matches = bytes <= 16 ? StringSerializer.MatchesAsciiRecord(ref payloadStart, current) : Ascii.Equals(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current.AsSpan());
                        }
                        else
                            matches = StringSerializer.MatchesUtf8(MemoryMarshal.CreateReadOnlySpan(ref payloadStart, bytes), current);
                        if (matches)
                        {
                            // Publish only a validated match; failed reads retain their original cursor.
                            index = start + sizeof(int) * 2 + bytes;
                            return current;
                        }

                        // The validated payload was compared once; a mismatch needs the original decoder only.
                        return ReadStringAndAdvance<TOptions>(buffer, ref index);
                    }
                }
            }
#endif
            return ReadExistingStringAndAdvance<TOptions>(buffer, ref index, current);
        }

        /// <summary>Isolates fallback comparison work from the original fresh-string reader.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The absolute record cursor.</param>
        /// <param name = "current">The non-null candidate string.</param>
        /// <returns>The reused or normally decoded string.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string? ReadExistingStringAndAdvance<TOptions>(Span<byte> buffer, ref int index, string current)
            where TOptions : struct, ILuminPackOptions
        {
            if (MatchesStringRecord<TOptions>(buffer, index, current, out int end))
            {
                index = end;
                return current;
            }

            return ReadStringAndAdvance<TOptions>(buffer, ref index);
        }

        /// <summary>Compares validated string payload bytes without publishing a cursor or creating text.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "index">The original record cursor.</param>
        /// <param name = "current">The candidate immutable string.</param>
        /// <param name = "end">Receives the complete record end after a match.</param>
        /// <returns>Whether the ordinary reader would produce the candidate text.</returns>
        private static bool MatchesStringRecord<TOptions>(Span<byte> buffer, int index, string current, out int end)
            where TOptions : struct, ILuminPackOptions
        {
            end = index;
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            var recording = default(TOptions).StringRecording;
            bool token = recording == LuminPackStringRecording.Token;
            if (recording == LuminPackStringRecording.VarInt)
            {
                if ((uint)index > (uint)buffer.Length)
                    return false;
                ReadVarIntRecordHeader<TOptions>(buffer.Slice(index), out int prefix, out int bytes, out int characters, out bool latin1);
                if (bytes < 0 || characters != current.Length)
                    return false;
                var variablePayload = buffer.Slice(index + prefix, bytes);
                bool variableMatch = utf8 ? StringSerializer.MatchesUtf8(variablePayload, current) : latin1
                    ? StringSerializer.MatchesLatin1(ref MemoryMarshal.GetReference(variablePayload), current)
                    : variablePayload.SequenceEqual(MemoryMarshal.AsBytes(current.AsSpan()));
                if (variableMatch)
                    end = index + prefix + bytes;
                return variableMatch;
            }
            int header = token ? 0 : utf8 ? 8 : 4;
            int available = buffer.Length;
            if ((uint)index > (uint)available || (uint)header > (uint)(available - index))
                return false;
            ReadStringLength<TOptions>(buffer, ref index, out int length);
            // Null, empty and malformed records retain the original reader's normalization and failures.
            if (length <= 0)
                return false;
            if ((uint)length > (uint)(available - index - header))
                return false;
            ReadOnlySpan<byte> payload = buffer.Slice(index + header, length);
            bool matches;
            if (utf8)
            {
                if (!token)
                {
                    int characterIndex = index + 4;
                    LuminPackReader.Read(ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)(uint)(characterIndex)), out int characters);
                    if (characters > 0 && characters != current.Length)
                        return false;
                }

                matches = StringSerializer.MatchesUtf8(payload, current);
            }
            else
            {
                matches = payload.SequenceEqual(MemoryMarshal.AsBytes(current.AsSpan()));
                // Token decoding uses Encoding.Unicode, which replaces malformed surrogate sequences.
                if (matches && token)
                {
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (!char.IsSurrogate(current[i]))
                            continue;
                        if (!char.IsHighSurrogate(current[i]) || ++i == current.Length || !char.IsLowSurrogate(current[i]))
                            return false;
                    }
                }
            }

            if (matches)
                end = index + header + length + (token ? utf8 ? 1 : 2 : 0);
            return matches;
        }

        /// <summary>Validates that the requested bytes remain in the input window in every build.</summary>
        /// <param name = "buffer">The complete retained input window.</param>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "count">The number of bytes required.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureReadable(Span<byte> buffer, int index, int count)
        {
            if (IsOutOfRange(index, count, buffer.Length))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(checked(index + count));
        }

        /// <summary>Checks whether a compiled string key can reuse its enumerated dictionary value.</summary>
        /// <typeparam name="TOptions">The profile used to decode the key.</typeparam>
        /// <param name="comparer">The destination dictionary's comparer.</param>
        /// <returns>Whether the profile is a known framework profile and the dictionary uses the default string comparer.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanReuseEnumeratedStringKey<TOptions>(IEqualityComparer<string> comparer)
            where TOptions : struct, ILuminPackOptions =>
            ConstantStringProfile<TOptions>() && ReferenceEquals(comparer, EqualityComparer<string>.Default);

        /// <summary>Gets the reusable row-major slots for a validated default-profile string matrix.</summary>
        /// <param name="matrix">The existing target.</param>
        /// <param name="rows">The validated incoming row count.</param>
        /// <param name="columns">The validated incoming column count.</param>
        /// <param name="slots">The matching slots, including an empty span for matching empty shapes.</param>
        /// <returns>Whether the existing matrix can be filled in place.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStringMatrixSlots(string?[,]? matrix, int rows, int columns, out Span<string?> slots) =>
            TryGetStringMatrixSlots<LuminPackUtf16LengthOptions>(matrix, rows, columns, out slots);

        /// <summary>Gets the reusable slots for the framework's default binary UTF-16 length profile.</summary>
        /// <typeparam name="TOptions">The effective compiled profile.</typeparam>
        /// <param name="matrix">The existing target.</param>
        /// <param name="rows">The validated incoming row count.</param>
        /// <param name="columns">The validated incoming column count.</param>
        /// <param name="slots">The matching row-major string slots.</param>
        /// <returns>Whether this profile and shape permit reuse.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStringMatrixSlots<TOptions>(string?[,]? matrix, int rows, int columns, out Span<string?> slots)
            where TOptions : struct, ILuminPackOptions
        {
            slots = default;
            if (typeof(TOptions) != typeof(LuminPackUtf16LengthOptions) || matrix is null ||
                matrix.GetLength(0) != rows || matrix.GetLength(1) != columns ||
                matrix.GetLowerBound(0) != 0 || matrix.GetLowerBound(1) != 0)
                return false;
            if (matrix.Length != 0)
                slots = MemoryMarshal.CreateSpan(ref matrix[0, 0], matrix.Length);
            return true;
        }

        /// <summary>Reads a string with the default compiled profile, retaining equal immutable storage.</summary>
        /// <param name = "value">The current string and destination for the parsed string.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref string? value) => Read<LuminPackDefaultOptions>(ref value);

        /// <summary>Reads a complete string record and owns its successful cursor advancement.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "value">The current string and destination for the parsed string.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref string? value)
            where TOptions : struct, ILuminPackOptions
        {
            string? decoded = Read<TOptions>(value);
            // An equal record returns the current reference, which needs no store.
            if (!ReferenceEquals(value, decoded))
                value = decoded;
        }

        /// <summary>Reads a complete default-profile record without exposing an integer cursor.</summary>
        /// <param name = "current">The immutable string available for reuse.</param>
        /// <returns>The parsed string, retaining equal storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? Read(string? current = null) => Read<LuminPackDefaultOptions>(current);

        /// <summary>Reads validated string slots with the default profile, retaining equal immutable values.</summary>
        /// <param name="strings">The destination slots after their collection framing has been validated.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadStrings(Span<string?> strings) => ReadStrings<LuminPackDefaultOptions>(strings);

        /// <summary>Reads validated string slots while retaining per-record checks and unchanged heap references.</summary>
        /// <typeparam name="TOptions">The stateless binary string profile.</typeparam>
        /// <param name="strings">The destination slots after their collection framing has been validated.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ReadStrings<TOptions>(Span<string?> strings) where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 &&
                default(TOptions).StringRecording == LuminPackStringRecording.Length &&
                Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 &&
                !strings.IsEmpty && strings[0] is null && RemainingWindowBytes >= sizeof(int))
            {
                int header = Read<int>(ref Current);
                if (IsShortLatin1Header(header))
                    strings = strings.Slice(ReadFreshLatin1Prefix(strings, header));
            }
#endif
            ref string? first = ref MemoryMarshal.GetReference(strings);
            for (int i = 0; i < strings.Length; i++)
            {
                ref string? slot = ref Unsafe.Add(ref first, (nint)(uint)i);
                string? current = slot;
                string? decoded = Read<TOptions>(current);
                if (!ReferenceEquals(current, decoded))
                    slot = decoded;
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>Tests for a compact length header carrying four to sixteen Latin-1 bytes.</summary>
        /// <param name="header">The complete length header.</param>
        /// <returns>Whether the short decoder can consume the payload.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsShortLatin1Header(int header) =>
            (header & 1) != 0 && (uint)((header >> 1) - 4) <= 12;

        /// <summary>Fills leading empty slots from short Latin-1 length records.</summary>
        /// <param name="slots">The destination slots, starting with an empty slot.</param>
        /// <param name="header">The first short Latin-1 header, already read at the cursor.</param>
        /// <returns>The number of complete string records read.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadFreshLatin1Prefix(Span<string?> slots, int header)
        {
            int i = 0;
            ref string? first = ref MemoryMarshal.GetReference(slots);
            do
            {
                int length = header >> 1;
                ref byte payload = ref Unsafe.Add(ref Current, sizeof(int));
                Advance(sizeof(int) + length);
                Unsafe.Add(ref first, i++) = StringSerializer.CreateShortLatin1String(ref payload, length);
                if (i == slots.Length || Unsafe.Add(ref first, i) is not null || RemainingWindowBytes < sizeof(int))
                    break;
                header = Read<int>(ref Current);
            } while (IsShortLatin1Header(header));
            return i;
        }
#endif

        /// <summary>Gets the same relative readable width directly from the cached window end.</summary>
        private int RemainingWindowBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
                (int)Unsafe.ByteOffset(ref Cursor, ref _end);
#else
                (int)Unsafe.ByteOffset(ref Current, ref MemoryMarshal.GetReference(_end));
#endif
        }

        /// <summary>Reads a complete compiled-profile record without exposing an integer cursor.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "current">The immutable string available for reuse.</param>
        /// <returns>The parsed string, retaining equal storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? Read<TOptions>(string? current)
            where TOptions : struct, ILuminPackOptions
        {
            bool utf16Length = false;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16)
            {
                var recording = default(TOptions).StringRecording;
                if (recording == LuminPackStringRecording.VarInt)
                    return ReadVarIntString<TOptions>(current);
                utf16Length = recording == LuminPackStringRecording.Length;
            }
            if (utf16Length)
            {
#if NET8_0_OR_GREATER
                // Complete nonempty records validate before comparison or allocation. Equal candidates retain
                // their immutable storage; null, empty and malformed records keep the shared reader's failures.
                nint available = Unsafe.ByteOffset(ref Cursor, ref _end);
                if (unchecked((nuint)(available - sizeof(int))) <= int.MaxValue - sizeof(int))
                {
                    int header = LuminPackReader.Read<int>(ref Current);
                    if (header > 1)
                    {
                        ref byte text = ref Unsafe.Add(ref Current, sizeof(int));
                        if ((header & 1) == 0)
                        {
                            AdvanceInStringWindow(unchecked(sizeof(int) + header), (nuint)available);
                            return StringSerializer.ReadUtf16Payload(ref text, header, current);
                        }

                        int length = header >> 1;
                        AdvanceInStringWindow(sizeof(int) + length, (nuint)available);
                        return StringSerializer.ReadLatin1Payload(ref text, length, current);
                    }
                }
#endif
                string? value = StringSerializer.ReadUtf16Record(ref Current, RemainingWindowBytes, current, out int bytes);
                Advance(bytes);
                return value;
            }

#if NET8_0_OR_GREATER
            // Fresh short ASCII length records widen inline, saving the shared decoder's call per string; every other
            // record, including non-ASCII payloads with an equal count, keeps the validating decoder.
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && default(TOptions).StringRecording == LuminPackStringRecording.Length &&
                current is null && Vector128.IsHardwareAccelerated)
            {
                nint available = Unsafe.ByteOffset(ref Cursor, ref _end);
                if (unchecked((nuint)(available - sizeof(int) * 2)) <= int.MaxValue - sizeof(int) * 2)
                {
                    long header = LuminPackReader.Read<long>(ref Current);
                    int payload = (int)header;
                    if (payload == (int)(header >> 32) && (uint)(payload - 4) <= 12 && payload <= available - sizeof(int) * 2)
                    {
                        string? ascii = StringSerializer.ReadShortAsciiString(ref Unsafe.Add(ref Current, sizeof(int) * 2), payload);
                        if (ascii is not null)
                        {
                            AdvanceValidated(sizeof(int) * 2 + payload);
                            return ascii;
                        }
                    }
                }
            }
#endif
            int start = Position;
            int index = start;
            // The decoders are static, so a fresh or retried read never takes this state's address.
            string? result = ReadStringAndAdvance<TOptions>(_buffer, ref index, current);
            Advance(index - start);
            return result;
        }

#if NET8_0_OR_GREATER
        /// <summary>Consumes a length record after checking its full width against the captured input window.</summary>
        /// <param name="byteCount">The complete record width, with a negative value indicating overflow.</param>
        /// <param name="available">The previously measured readable window, at most <see cref="int.MaxValue"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceInStringWindow(int byteCount, nuint available)
        {
            if (unchecked((nuint)byteCount) > available)
                LuminPackExceptionHelper.ThrowReaderSpanOutOfRange(
                    Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Cursor), byteCount);
            AdvanceValidated(byteCount);
        }
#endif

        /// <summary>Validates a variable-length record before decoding and publishing its successful cursor.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <param name="current">The immutable candidate available for reuse.</param>
        /// <returns>The decoded or reused string, including null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string? ReadVarIntString<TOptions>(string? current) where TOptions : struct, ILuminPackOptions
        {
            string? result = ReadVarIntRecord<TOptions>(_buffer.Slice(Position), current, out int consumed);
            Advance(consumed);
            return result;
        }

        /// <summary>Reads a canonical bounded header without publishing a partial cursor on failure.</summary>
        /// <param name="source">The complete available input.</param>
        /// <param name="offset">The header position, advanced only after a complete valid header.</param>
        /// <returns>The original length header, or minus one for null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ReadVarIntHeader(ReadOnlySpan<byte> source, ref int offset)
        {
            int position = offset;
            if ((uint)position >= (uint)source.Length)
                LuminPackExceptionHelper.ThrowInSufficientBuffer(1);
            uint first = source[position++];
            if (first < 128)
            {
                offset = position;
                return unchecked((int)(first - 1));
            }
            if ((uint)position >= (uint)source.Length)
                LuminPackExceptionHelper.ThrowInSufficientBuffer(1);
            uint second = source[position++];
            uint value = (first & 127) | ((second & 127) << 7);
            if (second < 128)
            {
                if (second == 0)
                    LuminPackExceptionHelper.ThrowInvalidStringLength(-2);
                offset = position;
                return unchecked((int)(value - 1));
            }
            return ReadVarIntHeaderTail(source, position, value, ref offset);
        }

        /// <summary>Decodes the checked remainder of a canonical multi-byte string header.</summary>
        /// <param name="source">The complete input window.</param>
        /// <param name="position">The position after two continuation bytes.</param>
        /// <param name="value">The low fourteen payload bits.</param>
        /// <param name="offset">Published only after the complete valid header.</param>
        /// <returns>The original length header.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadVarIntHeaderTail(ReadOnlySpan<byte> source, int position, uint value, ref int offset)
        {
            for (int shift = 14; shift <= 28; shift += 7)
            {
                if ((uint)position >= (uint)source.Length)
                    LuminPackExceptionHelper.ThrowInSufficientBuffer(1);
                uint next = source[position++];
                if (shift == 28 && next > 8)
                    LuminPackExceptionHelper.ThrowInvalidStringLength(-2);
                value |= (next & 127) << shift;
                if (next < 128)
                {
                    if (shift != 0 && next == 0 || value > 0x80000000u)
                        LuminPackExceptionHelper.ThrowInvalidStringLength(-2);
                    offset = position;
                    return unchecked((int)(value - 1));
                }
            }
            LuminPackExceptionHelper.ThrowInvalidStringLength(-2);
            return 0;
        }

        /// <summary>Validates both variable headers and the complete payload before allocation.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="source">The complete available record window.</param>
        /// <param name="prefix">Receives the complete framing width.</param>
        /// <param name="bytes">Receives the payload byte count, or minus one for null.</param>
        /// <param name="characters">Receives the decoded character count.</param>
        /// <param name="latin1">Receives whether the payload is compact Latin-1.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ReadVarIntRecordHeader<TOptions>(ReadOnlySpan<byte> source, out int prefix, out int bytes, out int characters, out bool latin1) where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            prefix = 0;
            int header = ReadVarIntHeader(source, ref prefix);
            characters = utf8 ? ReadVarIntHeader(source, ref prefix) : header >> 1;
            latin1 = !utf8 && header >= 0 && (header & 1) != 0;
            bytes = header == -1 ? -1 : utf8 ? header : StringSerializer.Utf16RecordPayload(header);
            if (utf8 && (header == -1 ? characters != -1 : characters < 0 || characters > bytes || bytes != 0 && characters == 0))
                LuminPackExceptionHelper.ThrowInvalidStringLength(characters);
            if (bytes >= 0 && (uint)bytes > (uint)(source.Length - prefix))
                LuminPackExceptionHelper.ThrowInSufficientBuffer(bytes);
        }

        /// <summary>Decodes a validated opt-in record while preserving equal immutable string storage.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="source">The complete available input.</param>
        /// <param name="current">The immutable candidate available for reuse.</param>
        /// <param name="consumed">Receives the complete validated record width.</param>
        /// <returns>The decoded or reused string, including null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string? ReadVarIntRecord<TOptions>(ReadOnlySpan<byte> source, string? current, out int consumed) where TOptions : struct, ILuminPackOptions
        {
            ReadVarIntRecordHeader<TOptions>(source, out int prefix, out int bytes, out int characters, out bool latin1);
            consumed = prefix + Math.Max(bytes, 0);
            if (bytes < 0)
                return null;
            if (bytes == 0)
                return string.Empty;
            var payload = source.Slice(prefix, bytes);
            ref byte text = ref MemoryMarshal.GetReference(payload);
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
                return current is not null && current.Length == characters && StringSerializer.MatchesUtf8(payload, current) ? current : StringSerializer.DeserializeUtf8(ref text, bytes, characters);
            return latin1 ? StringSerializer.ReadLatin1Payload(ref text, bytes, current) : StringSerializer.ReadUtf16Payload(ref text, bytes, current);
        }
    }
}
