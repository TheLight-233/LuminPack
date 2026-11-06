using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackJsonWriter
#else
    public unsafe ref partial struct LuminPackJsonWriter
#endif
    {
        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "propertyName">The property name used by this operation.</param>
        public void WritePropertyName(string propertyName)
        {
            WritePropertyName<LuminPackDefaultOptions>(propertyName);
        }

        /// <summary>Writes string to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(string? value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf8<LuminPackDefaultOptions>(value);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Encoded(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf8Encoded<LuminPackDefaultOptions>(value);
        }

#endif
#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 escaped to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <param name = "escapeIndex">The escape index used by this operation.</param>
        private void WriteStringContentUtf8Escaped(scoped ReadOnlySpan<char> value, int escapeIndex)
        {
            WriteStringContentUtf8Escaped<LuminPackDefaultOptions>(value, escapeIndex);
        }

#endif
#if NET8_0_OR_GREATER
        /// <summary>Writes utf8 string segment to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf8StringSegment(scoped ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
                return;
            if (Output is not null)
            {
                WriteUtf8StringSegmentToOutput(value);
                return;
            }

            // Try the actual available space first, including exact-size caller spans.
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, Buffer.Slice(Position), out _, out int bytesWritten);
            if (status != OperationStatus.Done)
                bytesWritten = WriteUtf8StringSegmentSlow(value);
            Advance(bytesWritten);
        }

        /// <summary>Keeps chunk transitions and their status locals outside the buffered UTF-8 segment path.</summary>
        /// <param name="value">The remaining unescaped characters for the external destination.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteUtf8StringSegmentToOutput(scoped ReadOnlySpan<char> value)
        {
            while (!value.IsEmpty)
            {
                var result = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, Buffer.Slice(Position), out int charsRead, out int written);
                Advance(written);
                value = value.Slice(charsRead);
                if (result == OperationStatus.Done)
                    return;
                if (result != OperationStatus.DestinationTooSmall)
                    LuminPackExceptionHelper.ThrowFailedEncoding(result);
                FlushToBufferWriter();
                EnsureCapacity(4);
            }
        }

#endif
#if NET8_0_OR_GREATER
        /// <summary>Writes utf8 string segment slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int WriteUtf8StringSegmentSlow(scoped ReadOnlySpan<char> value)
        {
            EnsureCapacity(Encoding.UTF8.GetByteCount(value));
            return Encoding.UTF8.GetBytes(value, Buffer.Slice(Position));
        }

#endif
        /// <summary>Writes escaped utf8 char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteEscapedUtf8Char(char value)
        {
            byte escaped = value switch
            {
                '"' => (byte)'"',
                '\\' => (byte)'\\',
                '\n' => (byte)'n',
                '\r' => (byte)'r',
                '\t' => (byte)'t',
                _ => (byte)'u'
            };
            int length = escaped == (byte)'u' ? 6 : 2;
            EnsureCapacity(length);
            ref byte destination = ref Current;
            if (length == 2)
            {
                ushort packed = (ushort)(Backslash | ((uint)escaped << 8));
                if (!BitConverter.IsLittleEndian)
                    packed = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(packed);
                Unsafe.WriteUnaligned(ref destination, packed);
            }
            else
            {
                uint head = 0x3030755Cu;
                ushort tail = (ushort)(HexChar((value >> 4) & 0xF) | (uint)HexChar(value & 0xF) << 8);
                if (!BitConverter.IsLittleEndian)
                {
                    head = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(head);
                    tail = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(tail);
                }
                Unsafe.WriteUnaligned(ref destination, head);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, 4), tail);
            }

            Advance(length);
        }

        /// <summary>Writes string content utf8 raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8Raw(ReadOnlySpan<byte> value)
        {
            WriteStringContentUtf8Raw<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf8 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Slow(ReadOnlySpan<byte> value)
        {
            WriteStringContentUtf8Slow<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf16 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf16<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes an already-fitting common escape pair without changing cold growth or external output.</summary>
        /// <param name="value">The original character.</param><returns>Whether the complete pair was written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16EscapePair(char value)
        {
            char escaped = value switch { '"' => '"', '\\' => '\\', '\n' => 'n', '\r' => 'r', '\t' => 't', _ => '\0' };
            if (escaped == '\0' || Output is not null || !BitConverter.IsLittleEndian)
                return false;
#if NET8_0_OR_GREATER
            if ((long)Unsafe.ByteOffset(ref Cursor, ref _end) < 4)
                return false;
#else
            if ((long)unchecked((nint)_end - (nint)Cursor) < 4)
                return false;
#endif
            Unsafe.WriteUnaligned(ref Current, (uint)'\\' | ((uint)escaped << 16));
            Advance(4);
            return true;
        }

        /// <summary>Writes string content utf16 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf16Slow(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf16Slow<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes utf16 char direct to the current output.</summary>
        /// <param name = "c">The c used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16CharDirect(char c)
        {
            EnsureCapacity(2);
            ref byte dst = ref Current;
            LuminPackWriter.Write(ref dst, c);
            Advance(2);
        }

        /// <summary>Performs the hex char utf16 step for the current operation.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char HexCharUtf16(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }

        /// <summary>Writes string content slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentSlow(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentSlow<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "propertyName">The property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(string propertyName)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
                WriteStringContentUtf8<TOptions>(propertyName.AsSpan());
            else
                WriteStringContentUtf16<TOptions>(propertyName.AsSpan());
            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>Writes string to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if (value == null)
            {
                WriteNullInternal<TOptions>();
                return;
            }


            // Reserve the quotes and content together. The transcoder only writes
            // after validating the complete value, so misses retain the normal path.
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) && (uint)(value.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
            {
                EnsureCapacity(value.Length + 2);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value.AsSpan(), Buffer.Slice(Position + 1, value.Length), escapeJson: true))
                {
                    ref byte output = ref Current;
                    output = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(value.Length + 1)) = Quote;
                    Advance(value.Length + 2);
                    return;
                }

                WriteByteRaw<TOptions>(Quote);
#if NET8_0_OR_GREATER
                WriteStringContentUtf8Encoded<TOptions>(value.AsSpan());
#else
                WriteStringContentSlow<TOptions>(value.AsSpan());
#endif
                WriteByteRaw<TOptions>(Quote);
                return;
            }


            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                WriteStringContentUtf8<TOptions>(value.AsSpan());
            }
            else
            {
                WriteStringContentUtf16<TOptions>(value.AsSpan());
            }

            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Writes a builder snapshot as a JSON string with the library default profile.</summary>
        /// <param name="value">The builder to snapshot, or null.</param>
        public void WriteStringBuilder(StringBuilder? value) => WriteStringBuilder<LuminPackDefaultOptions>(value);

        /// <summary>Writes a complete builder snapshot before an output callback can observe or change its source.</summary>
        /// <param name="value">The builder to snapshot, or null.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringBuilder<TOptions>(StringBuilder? value) where TOptions : struct, ILuminPackOptions
        {
            if (value is null)
            {
                WriteNull<TOptions>();
                return;
            }
            int length = value.Length;
            if (length == 0)
            {
                Write<TOptions>(string.Empty);
                return;
            }
            WriteStringBuilderSnapshot<TOptions>(value, length);
        }

        /// <summary>Keeps stack storage and pool cleanup outside null and empty builder writes.</summary>
        /// <param name="value">The nonempty builder to snapshot.</param>
        /// <param name="length">The captured character count.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringBuilderSnapshot<TOptions>(StringBuilder value, int length) where TOptions : struct, ILuminPackOptions
        {
            char[]? rented = null;
            Span<char> snapshot = length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(length));
            try
            {
                value.CopyTo(0, snapshot, length);
                WriteStringSnapshot<TOptions>(snapshot.Slice(0, length));
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>Writes a stable character snapshot through the existing string content kernels.</summary>
        /// <param name="text">The complete snapshot owned by the current operation.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringSnapshot<TOptions>(scoped ReadOnlySpan<char> text) where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && (uint)(text.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
            {
                EnsureCapacity(text.Length + 2);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(text, Buffer.Slice(Position + 1, text.Length), escapeJson: true))
                {
                    ref byte output = ref Current;
                    output = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(text.Length + 1)) = Quote;
                    Advance(text.Length + 2);
                    return;
                }
                WriteByteRaw<TOptions>(Quote);
#if NET8_0_OR_GREATER
                WriteStringContentUtf8Encoded<TOptions>(text);
#else
                WriteStringContentSlow<TOptions>(text);
#endif
                WriteByteRaw<TOptions>(Quote);
                return;
            }
            WriteByteRaw<TOptions>(Quote);
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
                WriteStringContentUtf8<TOptions>(text);
            else
                WriteStringContentUtf16<TOptions>(text);
            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(string? value, bool separate) => WriteElement<LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(int value, bool separate) => WriteElement<LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(long value, bool separate) => WriteElement<LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteEntryKey</c> with the library default profile.</summary>
        /// <param name = "key">The entry key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey(string? key) => WriteEntryKey<LuminPackDefaultOptions>(key);

        /// <summary>Applies <c>WriteEntryKey</c> with the library default profile.</summary>
        /// <param name = "key">The entry key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey(int key) => WriteEntryKey<LuminPackDefaultOptions>(key);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(string? value) => WriteEntryValue<LuminPackDefaultOptions>(value);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(int value) => WriteEntryValue<LuminPackDefaultOptions>(value);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(long value) => WriteEntryValue<LuminPackDefaultOptions>(value);

        /// <summary>Writes one string array element and its separator with a single capacity check.</summary>
        /// <param name = "value">The element.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <remarks>
        /// Generated collection loops track their own first element. The output and the cleared element flag are those of
        /// an optional comma followed by <see cref = "Write{TOptions}(string)"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement<TOptions>(string? value, bool separate)
            where TOptions : struct, ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && value != null &&
                (uint)(value.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
            {
                int length = value.Length;
                EnsureCapacity(length + 3);
                // The comma is stored unconditionally; without a separator the opening quote overwrites it.
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value.AsSpan(),
                    MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)(skip + 1)), length), escapeJson: true))
                {
                    Unsafe.Add(ref output, (nint)(uint)skip) = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(skip + length + 1)) = Quote;
                    Advance(skip + length + 2);
                    IsFirstElement = false;
                    return;
                }
            }


            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            Write<TOptions>(value);
        }

        /// <summary>Writes one 32-bit integer array element and its separator with a single capacity check.</summary>
        /// <param name = "value">The element.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement<TOptions>(int value, bool separate)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(12);
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 11), value));
                IsFirstElement = false;
                return;
            }

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16Element(value, separate))
                return;
            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes one 64-bit integer array element and its separator with a single capacity check.</summary>
        /// <param name = "value">The element.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement<TOptions>(long value, bool separate)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(21);
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 20), value));
                IsFirstElement = false;
                return;
            }

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16Element(value, separate))
                return;
            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Reports whether the default profile selects the isolated UTF-8 enumeration operation.</summary>
        /// <returns>Whether enumeration uses the UTF-8 integer entry.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool UsesUtf8Enumeration() => UsesUtf8Enumeration<LuminPackDefaultOptions>();

        /// <summary>Reports whether the compiled profile selects the isolated UTF-8 enumeration operation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>A profile constant which the JIT folds before executing the loop.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool UsesUtf8Enumeration<TOptions>() where TOptions : struct, ILuminPackOptions
            => default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;

        /// <summary>Writes a generated enumeration element with the default profile.</summary>
        /// <param name="value">The integer element.</param>
        /// <param name="separate">Whether a preceding element requires a comma.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEnumerationElement(int value, bool separate) => WriteEnumerationElement<LuminPackDefaultOptions>(value, separate);

        /// <summary>Writes a generated enumeration element with the default profile.</summary>
        /// <param name="value">The integer element.</param>
        /// <param name="separate">Whether a preceding element requires a comma.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEnumerationElement(long value, bool separate) => WriteEnumerationElement<LuminPackDefaultOptions>(value, separate);

        /// <summary>Combines an enumeration separator and integer in the generated loop.</summary>
        /// <param name="value">The integer element.</param>
        /// <param name="separate">Whether a preceding element requires a comma.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <remarks>Near a storage boundary, the original separator, flag and scalar operations preserve growth and failure state.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEnumerationElement<TOptions>(int value, bool separate)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && HasEnumerationCapacity(12))
            {
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 11), value));
                IsFirstElement = false;
                return;
            }
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16Element(value, separate))
                return;
            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            Write<TOptions>(value);
        }

        /// <summary>Combines an enumeration separator and long integer in the generated loop.</summary>
        /// <param name="value">The integer element.</param>
        /// <param name="separate">Whether a preceding element requires a comma.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <remarks>Near a storage boundary, the original separator, flag and scalar operations preserve growth and failure state.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEnumerationElement<TOptions>(long value, bool separate)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && HasEnumerationCapacity(21))
            {
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 20), value));
                IsFirstElement = false;
                return;
            }
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16Element(value, separate))
                return;
            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            Write<TOptions>(value);
        }

        /// <summary>Writes a complete scalar-framed integer dictionary entry with the default profile.</summary>
        /// <param name="key">The entry key.</param>
        /// <param name="value">The entry value.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntegerEntry(int key, int value) => WriteIntegerEntry<LuminPackDefaultOptions>(key, value);

        /// <summary>Combines one complete scalar-framed entry outside the enumerator's native inline budget.</summary>
        /// <param name="key">The entry key.</param>
        /// <param name="value">The entry value.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <remarks>The fast path cannot resize or call external code. Near a boundary, original array and scalar operations preserve depth, growth and failure state.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteIntegerEntry<TOptions>(int key, int value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && HasEnumerationCapacity(26))
            {
                int count = IsFirstElement ? 0 : 1;
                ref byte output = ref Current;
                output = Comma;
                Unsafe.Add(ref output, (nint)(uint)count++) = OpenBracket;
                count += LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)count), 11), key);
                Unsafe.Add(ref output, (nint)(uint)count++) = Comma;
                count += LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)count), 11), value);
                Unsafe.Add(ref output, (nint)(uint)count++) = CloseBracket;
                Advance(count);
                IsFirstElement = false;
                return;
            }
            WriteArrayStart<TOptions>();
            Write<TOptions>(key);
            Write<TOptions>(value);
            WriteArrayEnd<TOptions>();
        }

        /// <summary>Writes a complete scalar-framed integer dictionary entry with the default profile.</summary>
        /// <param name="key">The entry key.</param>
        /// <param name="value">The entry value.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntegerEntry(int key, long value) => WriteIntegerEntry<LuminPackDefaultOptions>(key, value);

        /// <summary>Combines one complete scalar-framed entry outside the enumerator's native inline budget.</summary>
        /// <param name="key">The entry key.</param>
        /// <param name="value">The entry value.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <remarks>The fast path cannot resize or call external code. Near a boundary, original array and scalar operations preserve depth, growth and failure state.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteIntegerEntry<TOptions>(int key, long value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && HasEnumerationCapacity(35))
            {
                int count = IsFirstElement ? 0 : 1;
                ref byte output = ref Current;
                output = Comma;
                Unsafe.Add(ref output, (nint)(uint)count++) = OpenBracket;
                count += LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)count), 11), key);
                Unsafe.Add(ref output, (nint)(uint)count++) = Comma;
                count += LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)count), 20), value);
                Unsafe.Add(ref output, (nint)(uint)count++) = CloseBracket;
                Advance(count);
                IsFirstElement = false;
                return;
            }
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && HasEnumerationCapacity(70))
            {
                int count = IsFirstElement ? 0 : 1;
                ref char output = ref Unsafe.As<byte, char>(ref Current);
                output = ',';
                Unsafe.Add(ref output, count++) = '[';
                key.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, count), 11), out int keyWritten);
                count += keyWritten;
                Unsafe.Add(ref output, count++) = ',';
                value.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, count), 20), out int written);
                count += written;
                Unsafe.Add(ref output, count++) = ']';
                Advance(count << 1);
                IsFirstElement = false;
                return;
            }
            WriteArrayStart<TOptions>();
            Write<TOptions>(key);
            Write<TOptions>(value);
            WriteArrayEnd<TOptions>();
        }

        /// <summary>Formats a bounded UTF-16 integer operation after its profile branch is selected.</summary>
        /// <param name="value">The integer to format with the current culture.</param>
        /// <param name="separate">Whether an outer separator is required.</param>
        /// <returns>Whether the complete current window allowed the fused write.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16Element(int value, bool separate)
        {
            if (!HasEnumerationCapacity(24))
                return false;

            int skip = separate ? 1 : 0;
            ref char output = ref Unsafe.As<byte, char>(ref Current);
            output = ',';
            value.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, skip), 11), out int written);
            Advance((skip + written) << 1);
            IsFirstElement = false;
            return true;
        }

        /// <summary>Formats a bounded UTF-16 integer operation after its profile branch is selected.</summary>
        /// <param name="value">The integer to format with the current culture.</param>
        /// <param name="separate">Whether an outer separator is required.</param>
        /// <returns>Whether the complete current window allowed the fused write.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16Element(long value, bool separate)
        {
            if (!HasEnumerationCapacity(42))
                return false;

            int skip = separate ? 1 : 0;
            ref char output = ref Unsafe.As<byte, char>(ref Current);
            output = ',';
            value.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, skip), 20), out int written);
            Advance((skip + written) << 1);
            IsFirstElement = false;
            return true;
        }

        /// <summary>Formats a bounded UTF-16 integer operation after its profile branch is selected.</summary>
        /// <param name="key">The integer to format with the current culture.</param>
        /// <returns>Whether the complete current window allowed the fused write.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16EntryKey(int key)
        {
            if (!HasEnumerationCapacity(28))
                return false;

            int count = IsFirstElement ? 0 : 1;
            ref char output = ref Unsafe.As<byte, char>(ref Current);
            output = ',';
            Unsafe.Add(ref output, count++) = '[';
            key.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, count), 11), out int written);
            count += written;
            Unsafe.Add(ref output, count++) = ',';
            Advance(count << 1);
            IsFirstElement = true;
            return true;
        }

        /// <summary>Formats a bounded UTF-16 integer operation after its profile branch is selected.</summary>
        /// <param name="value">The integer to format with the current culture.</param>
        /// <returns>Whether the complete current window allowed the fused write.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16EntryValue(int value)
        {
            if (!HasEnumerationCapacity(26))
                return false;

            int skip = IsFirstElement ? 0 : 1;
            ref char output = ref Unsafe.As<byte, char>(ref Current);
            output = ',';
            value.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, skip), 11), out int written);
            Unsafe.Add(ref output, skip + written) = ']';
            Advance((skip + written + 1) << 1);
            IsFirstElement = false;
            return true;
        }

        /// <summary>Formats a bounded UTF-16 integer operation after its profile branch is selected.</summary>
        /// <param name="value">The integer to format with the current culture.</param>
        /// <returns>Whether the complete current window allowed the fused write.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16EntryValue(long value)
        {
            if (!HasEnumerationCapacity(44))
                return false;

            int skip = IsFirstElement ? 0 : 1;
            ref char output = ref Unsafe.As<byte, char>(ref Current);
            output = ',';
            value.TryFormat(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, skip), 20), out int written);
            Unsafe.Add(ref output, skip + written) = ']';
            Advance((skip + written + 1) << 1);
            IsFirstElement = false;
            return true;
        }


        /// <summary>Tests the complete fused write range without growing or committing a storage window.</summary>
        /// <param name="count">The positive maximum fused payload width.</param>
        /// <returns>Whether the complete payload already fits in the current window.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasEnumerationCapacity(int count)
        {
#if NET8_0_OR_GREATER
            return (long)Unsafe.ByteOffset(ref Cursor, ref _end) >= count;
#else
            return (long)unchecked((nint)_end - (nint)Cursor) >= count;
#endif
        }

        /// <summary>Opens one dictionary entry array and writes its string key and the inner separator.</summary>
        /// <param name = "key">The entry key.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <remarks>
        /// Together with a following <c>WriteEntryValue</c>, the output is that of <see cref = "WriteArrayStart{TOptions}"/>,
        /// the key, the value and <see cref = "WriteArrayEnd{TOptions}"/>. The entry has no nested container, so its depth is not tracked.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey<TOptions>(string? key)
            where TOptions : struct, ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && key != null &&
                (uint)(key.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
            {
                int length = key.Length;
                EnsureCapacity(length + 5);
                int skip = IsFirstElement ? 0 : 1;
                ref byte output = ref Current;
                output = Comma;
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(key.AsSpan(),
                    MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)(skip + 2)), length), escapeJson: true))
                {
                    Unsafe.Add(ref output, (nint)(uint)skip) = OpenBracket;
                    Unsafe.Add(ref output, (nint)(uint)(skip + 1)) = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(skip + length + 2)) = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(skip + length + 3)) = Comma;
                    Advance(skip + length + 4);
                    IsFirstElement = true;
                    return;
                }
            }


            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(OpenBracket);
            IsFirstElement = true;
            Write<TOptions>(key);
            WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
        }

        /// <summary>Opens one dictionary entry array and writes its 32-bit integer key and the inner separator.</summary>
        /// <param name = "key">The entry key.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey<TOptions>(int key)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(14);
                int skip = IsFirstElement ? 0 : 1;
                ref byte output = ref Current;
                output = Comma;
                Unsafe.Add(ref output, (nint)(uint)skip) = OpenBracket;
                int written = skip + 1 + LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)(skip + 1)), 11), key);
                Unsafe.Add(ref output, (nint)(uint)written) = Comma;
                Advance(written + 1);
                IsFirstElement = true;
                return;
            }

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16EntryKey(key))
                return;
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(OpenBracket);
            IsFirstElement = true;
            WriteIntegerValue<TOptions>(key);
            WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
        }

        /// <summary>Writes a dictionary entry's string value and closes its entry array.</summary>
        /// <param name = "value">The entry value.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && value != null &&
                (uint)(value.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
            {
                int length = value.Length;
                EnsureCapacity(length + 3);
                ref byte output = ref Current;
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value.AsSpan(),
                    MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, 1), length), escapeJson: true))
                {
                    output = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(length + 1)) = Quote;
                    Unsafe.Add(ref output, (nint)(uint)(length + 2)) = CloseBracket;
                    Advance(length + 3);
                    IsFirstElement = false;
                    return;
                }
            }


            Write<TOptions>(value);
            WriteByteRaw<TOptions>(CloseBracket);
            IsFirstElement = false;
        }

        /// <summary>Writes a dictionary entry's 32-bit integer value and closes its entry array.</summary>
        /// <param name = "value">The entry value.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue<TOptions>(int value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(12);
                ref byte output = ref Current;
                int written = LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref output, 11), value);
                Unsafe.Add(ref output, (nint)(uint)written) = CloseBracket;
                Advance(written + 1);
                IsFirstElement = false;
                return;
            }

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16EntryValue(value))
                return;
            WriteIntegerValue<TOptions>(value);
            WriteByteRaw<TOptions>(CloseBracket);
            IsFirstElement = false;
        }

        /// <summary>Writes a dictionary entry's 64-bit integer value and closes its entry array.</summary>
        /// <param name = "value">The entry value.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue<TOptions>(long value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(21);
                ref byte output = ref Current;
                int written = LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref output, 20), value);
                Unsafe.Add(ref output, (nint)(uint)written) = CloseBracket;
                Advance(written + 1);
                IsFirstElement = false;
                return;
            }

            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && TryWriteUtf16EntryValue(value))
                return;
            WriteIntegerValue<TOptions>(value);
            WriteByteRaw<TOptions>(CloseBracket);
            IsFirstElement = false;
        }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, ILuminPackOptions
        {
#if NETSTANDARD2_1
            if (global::LuminPack.Core.LuminPackBurst.HasAsciiEncode)
            {
                EnsureCapacity(value.Length);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value, Buffer.Slice(Position), escapeJson: true))
                {
                    Advance(value.Length);
                    return;
                }
            }
#endif
#if NET8_0_OR_GREATER
            // Tiny ASCII values are cheaper to scan and narrow without a transcoder call.
            if (value.Length <= 32)
            {
                EnsureCapacity(value.Length);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value, Buffer.Slice(Position), escapeJson: true))
                {
                    Advance(value.Length);
                    return;
                }

                ref byte destination = ref Current;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c > 0x7F || NeedsJsonEscape(c))
                    {
                        if (c <= 0x7F)
                            WriteStringContentSlow<TOptions>(value);
                        else
                            WriteStringContentUtf8Encoded<TOptions>(value);
                        return;
                    }

                    Unsafe.Add(ref destination, (nint)(uint)(i)) = (byte)c;
                }

                Advance(value.Length);
                return;
            }

            WriteStringContentUtf8Encoded<TOptions>(value);
#else
            int length = value.Length;
            if (length >= 16 && TryWriteStringContentUtf8Complete(value))
                return;

            for (int i = 0; i < length; i++)
            {
                char c = value[i];
                if (c > 0x7F || NeedsJsonEscape(c))
                {
                    WriteStringContentSlow<TOptions>(value);
                    return;
                }
            }

            EnsureCapacity(length);
            ref byte destination = ref Current;
            for (int i = 0; i < length; i++)
                Unsafe.Add(ref destination, (nint)(uint)i) = (byte)value[i];
            Advance(length);
#endif
        }

#if NETSTANDARD2_1
        /// <summary>Encodes an ordinary complete string only when its worst-case bytes already fit.</summary>
        /// <param name="value">The complete UTF-16 string content, with no surrounding quotes.</param>
        /// <returns>Whether the existing window accepted the complete unescaped content.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringContentUtf8Complete(scoped ReadOnlySpan<char> value)
        {
            // Admission never grows or commits storage; exact-size and streaming windows keep their old path.
            if (value.Length > int.MaxValue / 3 || !HasEnumerationCapacity(value.Length * 3) || IndexOfJsonEscape(value) >= 0)
                return false;
            StringSerializer.SerializeUtf8Complete(value, Buffer.Slice(Position), out _, out int written, true);
            Advance(written);
            return true;
        }
#endif

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf8Encoded<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, ILuminPackOptions
        {
            int escapeIndex = value.IndexOfAny(StringEscapeCharacters);
            if (escapeIndex < 0)
                WriteUtf8StringSegment(value);
            else
                WriteStringContentUtf8Escaped<TOptions>(value, escapeIndex);
        }

#endif
#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 escaped to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <param name = "escapeIndex">The escape index used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf8Escaped<TOptions>(scoped ReadOnlySpan<char> value, int escapeIndex)
            where TOptions : struct, ILuminPackOptions
        {
            while (true)
            {
                if (escapeIndex <= 8 && value.Length - escapeIndex >= 3 && NeedsJsonEscape(value[escapeIndex + 1]) && NeedsJsonEscape(value[escapeIndex + 2]))
                {
                    // Keep dense escape clusters on the existing scalar path, in bounded
                    // blocks so a dense prefix does not slow a long ordinary suffix.
                    int blockLength = Math.Min(64, value.Length);
                    if (blockLength < value.Length && char.IsHighSurrogate(value[blockLength - 1]) && char.IsLowSurrogate(value[blockLength]))
                        blockLength++;
                    WriteStringContentSlow<TOptions>(value.Slice(0, blockLength));
                    value = value.Slice(blockLength);
                    escapeIndex = value.IndexOfAny(StringEscapeCharacters);
                    if (escapeIndex < 0)
                    {
                        WriteUtf8StringSegment(value);
                        return;
                    }

                    continue;
                }

                if (escapeIndex > 0)
                    WriteUtf8StringSegment(value.Slice(0, escapeIndex));
                // Consecutive escapes stay scalar; do not search or transcode empty runs.
                do
                {
                    WriteEscapedUtf8Char(value[escapeIndex++]);
                }
                while (escapeIndex < value.Length && NeedsJsonEscape(value[escapeIndex]));
                value = value.Slice(escapeIndex);
                escapeIndex = value.IndexOfAny(StringEscapeCharacters);
                if (escapeIndex < 0)
                {
                    WriteUtf8StringSegment(value);
                    return;
                }
            }
        }

#endif
        /// <summary>Writes string content utf8 raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8Raw<TOptions>(ReadOnlySpan<byte> value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            int escapeIndex = value.IndexOfAny(StringEscapeBytes);
            if (escapeIndex < 0)
            {
                WriteRaw(value);
                return;
            }

            while (true)
            {
                if (escapeIndex > 0)
                    WriteRaw(value.Slice(0, escapeIndex));
                do
                {
                    WriteEscapedUtf8Char((char)value[escapeIndex++]);
                }
                while (escapeIndex < value.Length && NeedsJsonEscape((char)value[escapeIndex]));
                value = value.Slice(escapeIndex);
                escapeIndex = value.IndexOfAny(StringEscapeBytes);
                if (escapeIndex < 0)
                {
                    WriteRaw(value);
                    return;
                }
            }
#else
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b < 0x20 || b == '"' || b == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                EnsureCapacity(value.Length);
                ref byte dst = ref Current;
                LuminPackMarshal.CopyBlockUnaligned(ref dst, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                Advance(value.Length);
            }
            else
            {
                WriteStringContentUtf8Slow<TOptions>(value);
            }
#endif
        }

        /// <summary>Writes string content utf8 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf8Slow<TOptions>(ReadOnlySpan<byte> value)
            where TOptions : struct, ILuminPackOptions
        {
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b == '"')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'"');
                }
                else if (b == '\\')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'\\');
                }
                else if (b == '\n')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'n');
                }
                else if (b == '\r')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'r');
                }
                else if (b == '\t')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'t');
                }
                else if (b < 0x20)
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteByteRaw<TOptions>((byte)'u');
                    WriteByteRaw<TOptions>((byte)'0');
                    WriteByteRaw<TOptions>((byte)'0');
                    WriteByteRaw<TOptions>(HexChar((b >> 4) & 0xF));
                    WriteByteRaw<TOptions>(HexChar(b & 0xF));
                }
                else
                {
                    WriteByteRaw<TOptions>(b);
                }
            }
        }

        /// <summary>Writes string content utf16 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, ILuminPackOptions
        {
            int escapeIndex = IndexOfJsonEscape(value);

            if (escapeIndex < 0)
            {
                if (Output is not null)
                {
                    WriteRaw(value);
                    return;
                }

                int byteCount = checked(value.Length * 2);
                EnsureCapacity(byteCount);
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value));
                ref byte dst = ref Current;
                LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
            else
            {
#if NET8_0_OR_GREATER
                if (value.Length >= 512 && value.Length - escapeIndex > 32 && Output is null &&
                    (!NeedsJsonEscape(value[escapeIndex + 1]) || !NeedsJsonEscape(value[escapeIndex + 2])) &&
                    TryWriteStringContentUtf16Sparse<TOptions>(value, escapeIndex))
                    return;
                // External output can observe and change mutable span input while the prefix is published.
                if (escapeIndex >= 16 && Output is null)
                    WriteStringContentUtf16Prefix<TOptions>(value, escapeIndex);
                else
#endif
                    WriteStringContentUtf16Slow<TOptions>(value);
            }
        }

        /// <summary>Writes string content utf16 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf16Slow<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, ILuminPackOptions
        {
            int lastWritten = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool needsEscape = false;
                if (c == '"' || c == '\\' || c == '\n' || c == '\r' || c == '\t' || c < 0x20)
                {
                    needsEscape = true;
                }

                if (needsEscape)
                {
                    if (i > lastWritten)
                    {
                        var segment = value.Slice(lastWritten, i - lastWritten);
                        if (Output is not null)
                        {
                            WriteRaw(segment);
                        }
                        else
                        {
                            int byteCount = checked(segment.Length * 2);
                            EnsureCapacity(byteCount);
                            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                            ref byte dst = ref Current;
                            LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
                            Advance(byteCount);
                        }
                    }

                    if (TryWriteUtf16EscapePair(c))
                    {
                        lastWritten = i + 1;
                        continue;
                    }

                    if (c == '"')
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('"');
                    }
                    else if (c == '\\')
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('\\');
                    }
                    else if (c == '\n')
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('n');
                    }
                    else if (c == '\r')
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('r');
                    }
                    else if (c == '\t')
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('t');
                    }
                    else if (c < 0x20)
                    {
                        WriteByteRaw<TOptions>(Backslash);
                        WriteUtf16CharDirect('u');
                        WriteUtf16CharDirect(HexCharUtf16((c >> 12) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16((c >> 8) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16((c >> 4) & 0xF));
                        WriteUtf16CharDirect(HexCharUtf16(c & 0xF));
                    }

                    lastWritten = i + 1;
                }
            }

            if (lastWritten < value.Length)
            {
                var segment = value.Slice(lastWritten);
                if (Output is not null)
                {
                    WriteRaw(segment);
                    return;
                }

                int byteCount = checked(segment.Length * 2);
                EnsureCapacity(byteCount);
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                ref byte dst = ref Current;
                LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>Copies a known ordinary UTF-16 prefix into fixed or native-owned output before the original escape loop.</summary>
        /// <param name="value">The complete content.</param>
        /// <param name="escapeIndex">The first escape after a complete ordinary prefix.</param>
        /// <typeparam name="TOptions">The stateless serialization profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf16Prefix<TOptions>(scoped ReadOnlySpan<char> value, int escapeIndex)
            where TOptions : struct, ILuminPackOptions
        {
            var prefix = value.Slice(0, escapeIndex);
            int byteCount = checked(prefix.Length * 2);
            EnsureCapacity(byteCount);
            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(prefix));
            ref byte dst = ref Current;
            LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
            Advance(byteCount);
            WriteStringContentUtf16Slow<TOptions>(value.Slice(escapeIndex));
        }
#endif

#if NET8_0_OR_GREATER
        /// <summary>Writes long UTF-16 literal runs while retaining scalar handling for escape clusters.</summary>
        /// <param name="value">The complete content, written only to owned or caller-provided contiguous storage.</param>
        /// <param name="escapeIndex">The already located first escape.</param>
        /// <typeparam name="TOptions">The stateless serialization profile.</typeparam>
        /// <returns>Whether a sparse run was found and the complete content was written.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryWriteStringContentUtf16Sparse<TOptions>(scoped ReadOnlySpan<char> value, int escapeIndex)
            where TOptions : struct, ILuminPackOptions
        {
            if (!HasUtf16LiteralRun(value, escapeIndex))
                return false;

            // Confirm that batching does not immediately enter a dense suffix before publishing any content.
            int firstRun = escapeIndex + 1;
            while (firstRun < value.Length && NeedsJsonEscape(value[firstRun]))
                firstRun++;
            var following = value.Slice(firstRun);
            int followingEscape = IndexOfJsonEscape(following);
            if (followingEscape >= 0 && following.Length - followingEscape >= 32 &&
                !HasUtf16LiteralRun(following, followingEscape, 16))
                return false;

            while (true)
            {
                if (escapeIndex > 0)
                {
                    int byteCount = checked(escapeIndex * 2);
                    EnsureCapacity(byteCount);
                    ref byte source = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value));
                    ref byte destination = ref Current;
                    LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)byteCount);
                    Advance(byteCount);
                }

                int end = escapeIndex;
                while (end < value.Length && NeedsJsonEscape(value[end]) && TryWriteUtf16EscapePair(value[end]))
                    end++;
                if (end < value.Length && NeedsJsonEscape(value[end]))
                {
                    int start = end++;
                    while (end < value.Length && NeedsJsonEscape(value[end]))
                        end++;
                    WriteStringContentUtf16Slow<TOptions>(value.Slice(start, end - start));
                }
                value = value.Slice(end);
                escapeIndex = IndexOfJsonEscape(value);
                if (escapeIndex < 0)
                {
                    WriteStringContentUtf16<TOptions>(value);
                    return true;
                }
                if (escapeIndex < 32 && !HasUtf16LiteralRun(value, escapeIndex))
                {
                    WriteStringContentUtf16Slow<TOptions>(value);
                    return true;
                }
            }
        }

        /// <summary>Bounds sparse-path admission and falls back when a later segment becomes dense.</summary>
        /// <param name="value">The remaining UTF-16 content.</param>
        /// <param name="escapeIndex">The located escape before a potential ordinary run.</param>
        /// <param name="minimumLength">The required number of ordinary code units.</param>
        /// <returns>Whether a short escape cluster is followed by the required ordinary run.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasUtf16LiteralRun(scoped ReadOnlySpan<char> value, int escapeIndex, int minimumLength = 32)
        {
            int start = escapeIndex + 1;
            for (int skipped = 0; skipped < 4 && start < value.Length && NeedsJsonEscape(value[start]); skipped++)
                start++;
            return value.Length - start >= minimumLength && IndexOfJsonEscape(value.Slice(start, minimumLength)) < 0;
        }
#endif

        /// <summary>Locates a JSON escape without changing the short scalar scan.</summary>
        /// <param name="value">The complete UTF-16 content to scan.</param>
        /// <returns>The first exceptional character, or minus one for an ordinary span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOfJsonEscape(scoped ReadOnlySpan<char> value)
        {
#if NET8_0_OR_GREATER
            if (value.Length >= 16)
                return value.IndexOfAny(StringEscapeCharacters);
#endif
            for (int index = 0; index < value.Length; index++)
                if (value[index] < 0x20 || value[index] == '"' || value[index] == '\\')
                    return index;
            return -1;
        }



        /// <summary>Writes string content slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentSlow<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, ILuminPackOptions
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (NeedsJsonEscape(c))
                {
                    WriteEscapedUtf8Char(c);
                }
                else if (c <= 0x7F)
                {
                    WriteByteRaw<TOptions>((byte)c);
                }
                else
                {
                    uint scalar = c;
                    if (char.IsSurrogate(c))
                    {
                        if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                            scalar = 0x10000u + (((uint)c - 0xD800u) << 10) + (value[++i] - 0xDC00u);
                        else
                            scalar = 0xFFFD;
                    }

                    int length = scalar <= 0x7FF ? 2 : scalar <= 0xFFFF ? 3 : 4;
                    EnsureCapacity(length);
                    ref byte destination = ref Current;
                    if (length == 2)
                    {
                        destination = (byte)(0xC0 | (scalar >> 6));
                        Unsafe.Add(ref destination, 1) = (byte)(0x80 | (scalar & 0x3F));
                    }
                    else if (length == 3)
                    {
                        destination = (byte)(0xE0 | (scalar >> 12));
                        Unsafe.Add(ref destination, 1) = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                        Unsafe.Add(ref destination, 2) = (byte)(0x80 | (scalar & 0x3F));
                    }
                    else
                    {
                        destination = (byte)(0xF0 | (scalar >> 18));
                        Unsafe.Add(ref destination, 1) = (byte)(0x80 | ((scalar >> 12) & 0x3F));
                        Unsafe.Add(ref destination, 2) = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                        Unsafe.Add(ref destination, 3) = (byte)(0x80 | (scalar & 0x3F));
                    }

                    Advance(length);
                }
            }
        }
    }
}
