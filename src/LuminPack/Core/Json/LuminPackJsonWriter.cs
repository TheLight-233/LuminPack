using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
    /// <summary>Writes JSON tokens with the configured UTF encoding to a reusable or external buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
    public unsafe ref struct LuminPackJsonWriter
    {
        private Span<byte> _bufferReference;
#if NET8_0_OR_GREATER
        internal ref byte _bufferStart;
#else
        internal byte* _bufferStart;
#endif
        internal int _currentIndex;
        private LuminBufferWriter? _writerBuffer;
        private readonly IBufferWriter<byte>? _output;
        private long _committedBytes;
        private readonly LuminPackWriterOptionalState _state;
        private readonly bool SerializeStringAsUtf8;

        private int _depth;
        private bool _isFirstElement;

        /// <summary>Gets the option state.</summary>
        public LuminPackWriterOptionalState OptionState => _state;
        /// <summary>Gets the option.</summary>
        public LuminPackSerializerOption Option => _state.Option;

        private const byte OpenBrace = (byte)'{';
        private const byte CloseBrace = (byte)'}';
        private const byte OpenBracket = (byte)'[';
        private const byte CloseBracket = (byte)']';
        private const byte Quote = (byte)'"';
        private const byte Colon = (byte)':';
        private const byte Comma = (byte)',';
        private const byte Backslash = (byte)'\\';

#if NET8_0_OR_GREATER
        private const string StringEscapeChars =
            "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007" +
            "\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" +
            "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017" +
            "\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\"\\";
        private static readonly SearchValues<char> s_stringEscapeChars = SearchValues.Create(StringEscapeChars);
        private static readonly SearchValues<byte> s_stringEscapeBytes =
            SearchValues.Create(Encoding.UTF8.GetBytes(StringEscapeChars));
#endif

        /// <summary>Gets the current index.</summary>
        public ref int CurrentIndex => ref _currentIndex;
        /// <summary>Gets the depth.</summary>
        public int Depth => _depth;
        /// <summary>Gets the total JSON bytes written, including committed output chunks.</summary>
        public long BytesWritten => _committedBytes + _currentIndex;

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c>.</summary>
        /// <param name="bufferReference">The buffer reference used by this operation.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        public LuminPackJsonWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState state)
        {
            _state = state;
            _bufferReference = bufferReference;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _currentIndex = 0;
            _writerBuffer = null;
            _output = null;
            _committedBytes = 0;
            _depth = 0;
            _isFirstElement = true;
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        public LuminPackJsonWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState state)
        {
            _state = state;

            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();

            _bufferReference = bufferWriter.BeginWrite(out _currentIndex);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _output = null;
            _committedBytes = 0;
            _depth = 0;
            _isFirstElement = true;
            SerializeStringAsUtf8 = _state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c>.</summary>
        /// <param name="bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LuminPackJsonWriter(LuminBufferWriter bufferWriter)
        {
            _state = bufferWriter.WriterState;
            _bufferReference = bufferWriter.BeginWrite(out _currentIndex);
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            _writerBuffer = bufferWriter;
            _output = null;
            _committedBytes = 0;
            _depth = 0;
            _isFirstElement = true;
            SerializeStringAsUtf8 = bufferWriter.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        }

        /// <summary>Creates a sequential writer which commits JSON directly to an external buffer writer.</summary>
        /// <remarks>CurrentIndex and GetSpan refer to the current chunk. Use BytesWritten for the total and FlushToBufferWriter to commit the final chunk.</remarks>
        /// <param name="output">The destination receiving the result.</param>
        /// <param name="state">The reusable state associated with the operation.</param>
        /// <param name="initialSizeHint">The initial size hint used by this operation.</param>
        public LuminPackJsonWriter(IBufferWriter<byte> output, LuminPackWriterOptionalState state, int initialSizeHint)
        {
            if (output is null) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(output));
            if (initialSizeHint <= 0) LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(initialSizeHint));
            _state = state;
            _output = output;
            _writerBuffer = null;
            _committedBytes = 0;
            _currentIndex = 0;
            _depth = 0;
            _isFirstElement = true;
            SerializeStringAsUtf8 = state.Option.StringEncoding is LuminPackStringEncoding.UTF8;
            _bufferReference = output.GetSpan(initialSizeHint);
            if (_bufferReference.Length < initialSizeHint)
                LuminPackExceptionHelper.ThrowInvalidOperationException("IBufferWriter returned a span smaller than sizeHint");
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
        }

        /// <summary>Commits the remaining JSON bytes to the external writer without flushing its underlying stream.</summary>
        public void FlushToBufferWriter()
        {
            if (_output is null)
                LuminPackExceptionHelper.ThrowInvalidOperationException("This JSON writer has no external output");
            if (_currentIndex != 0)
            {
                _output.Advance(_currentIndex);
                _committedBytes += _currentIndex;
                _currentIndex = 0;
            }
            _bufferReference = default;
#if NET8_0_OR_GREATER
            _bufferStart = ref Unsafe.NullRef<byte>();
#else
            _bufferStart = null;
#endif
        }

        #endregion

        #region Core Methods

        /// <summary>Obtains the span reference for the supplied input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetSpanReference(int index) =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
#if DEBUG
            if ((uint)count > (uint)(_bufferReference.Length - _currentIndex))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
            _currentIndex += count;
        }

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int count)
        {
            if ((uint)count <= (uint)(_bufferReference.Length - _currentIndex))
                return;

            EnsureCapacitySlow(count);
        }

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name="count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacitySlow(int count)
        {
            if (count < 0 || _currentIndex > int.MaxValue - count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);

            if (_output is not null)
            {
                FlushToBufferWriter();
                int sizeHint = Math.Max(4096, count);
                _bufferReference = _output.GetSpan(sizeHint);
                if (_bufferReference.Length < sizeHint)
                    LuminPackExceptionHelper.ThrowInvalidOperationException("IBufferWriter returned a span smaller than sizeHint");
#if NET8_0_OR_GREATER
                _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
                _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
                return;
            }

            if (_writerBuffer is null)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(_currentIndex + count);

            _writerBuffer.EnsureCapacity(_currentIndex + count);
            FlushBuffer();
        }

        /// <summary>Updates the span used by the current operation.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            _bufferReference = span;
#if NET8_0_OR_GREATER
            _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
            _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            _bufferReference = default;
#if NET8_0_OR_GREATER
            _bufferStart = ref Unsafe.NullRef<byte>();
#else
            _bufferStart = null;
#endif
            _currentIndex = 0;
            _depth = 0;
            _isFirstElement = true;
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            if (_writerBuffer != null)
            {
                _bufferReference = _writerBuffer.GetFullSpan();
#if NET8_0_OR_GREATER
                _bufferStart = ref MemoryMarshal.GetReference(_bufferReference);
#else
                _bufferStart = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_bufferReference));
#endif
            }
        }

        /// <summary>Obtains the current span reference for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte GetCurrentSpanReference() =>
#if NET8_0_OR_GREATER
            ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

        /// <summary>Obtains the span for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _bufferReference[.._currentIndex];
        }

        /// <summary>Obtains the current span offset for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref int GetCurrentSpanOffset() => ref _currentIndex;

        #endregion

        #region JSON Basic Methods

        /// <summary>Updates the first element used by the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFirstElement(bool value)
        {
            _isFirstElement = value;
        }

        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw(byte value)
        {
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(1);
                GetCurrentSpanReference() = value;
                Advance(1);
            }
            else
            {
                EnsureCapacity(2);
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.WriteUnaligned(ref dst, (char)value);
                Advance(2);
            }
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<byte> value)
        {
            if (_output is not null)
            {
                WriteRawToOutput(value);
                return;
            }
            EnsureCapacity(value.Length);
            value.CopyTo(_bufferReference.Slice(_currentIndex, value.Length));
            Advance(value.Length);
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<char> value)
        {
            if (_output is not null)
            {
                WriteRawToOutput(MemoryMarshal.AsBytes(value));
                return;
            }
            var byteCount = checked(value.Length << 1);
            EnsureCapacity(byteCount);
            MemoryMarshal.Cast<char, byte>(value).CopyTo(_bufferReference.Slice(_currentIndex, byteCount));
            Advance(byteCount);
        }

        // Keep segmented-output loops out of each inlined generated JSON prefix.
        /// <summary>Writes raw to output to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteRawToOutput(scoped ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty)
            {
                if (_currentIndex == _bufferReference.Length)
                    EnsureCapacity(Math.Min(4096, value.Length));
                int count = Math.Min(value.Length, _bufferReference.Length - _currentIndex);
                value.Slice(0, count).CopyTo(_bufferReference.Slice(_currentIndex));
                Advance(count);
                value = value.Slice(count);
            }
        }

        /// <summary>Generator-only copy of an 8..16-byte literal packed into two overlapping native words.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedLiteral(ulong first, ulong last, int byteCount)
        {
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            if ((uint)_currentIndex > (uint)_bufferReference.Length)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(_currentIndex);
            EnsureCapacity(byteCount);
            ref byte destination = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref destination, first);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, byteCount - 8), last);
            Advance(byteCount);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, int value)
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix(first, last, byteCount, 11);
            int written = LuminPackJsonNumberFormatter.WriteInt32(number, value);
            _currentIndex += byteCount + written;
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, uint value)
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix(first, last, byteCount, 10);
            int written = LuminPackJsonNumberFormatter.WriteUInt32(number, value);
            _currentIndex += byteCount + written;
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, long value)
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteLong(number, value);
            _currentIndex += byteCount + written;
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, ulong value)
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteULong(number, value);
            _currentIndex += byteCount + written;
        }

        /// <summary>Performs the reserve generated integer prefix step for the current operation.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="maxDigits">The max digits used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveGeneratedIntegerPrefix(ulong first, ulong last, int byteCount, int maxDigits)
        {
            if (!SerializeStringAsUtf8)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Generated integer prefixes require UTF-8");
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            if ((uint)_currentIndex > (uint)_bufferReference.Length)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(_currentIndex);
            // Reserve before obtaining either reference: growth/streaming may
            // replace the span and reset the cursor. Advance only after formatting.
            EnsureCapacity(byteCount + maxDigits);
            ref byte destination = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref destination, first);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, byteCount - 8), last);
            _isFirstElement = false;
            return _bufferReference.Slice(_currentIndex + byteCount);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        {
            if (!_isFirstElement)
            {
                WriteByteRaw(Comma);
            }
            _isFirstElement = false;
        }

        #endregion

        #region Object/Array

        /// <summary>Writes object start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            WriteCommaIfNeeded();
            WriteByteRaw(OpenBrace);
            _depth++;
            _isFirstElement = true;
        }

        /// <summary>Writes object end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            WriteByteRaw(CloseBrace);
            _depth--;
            _isFirstElement = false;
        }

        /// <summary>Writes array start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            WriteCommaIfNeeded();
            WriteByteRaw(OpenBracket);
            _depth++;
            _isFirstElement = true;
        }

        /// <summary>Writes array end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            WriteByteRaw(CloseBracket);
            _depth--;
            _isFirstElement = false;
        }

        #endregion

        #region Property Name

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="utf8PropertyName">The utf8 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);

            if (SerializeStringAsUtf8)
                WriteStringContentUtf8Raw(utf8PropertyName);
            else
                WriteStringContentUtf16(Encoding.UTF8.GetString(utf8PropertyName).AsSpan());

            WriteByteRaw(Quote);
            WriteByteRaw(Colon);

            _isFirstElement = true;
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="utf16PropertyName">The utf16 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);

            if (SerializeStringAsUtf8)
            {
                WriteStringContentUtf8(utf16PropertyName);
            }
            else
            {
                WriteStringContentUtf16(utf16PropertyName);
            }

            WriteByteRaw(Quote);
            WriteByteRaw(Colon);

            _isFirstElement = true;
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="propertyName">The property name used by this operation.</param>
        public void WritePropertyName(string propertyName)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);

            if (SerializeStringAsUtf8)
                WriteStringContentUtf8(propertyName.AsSpan());
            else
                WriteStringContentUtf16(propertyName.AsSpan());

            WriteByteRaw(Quote);
            WriteByteRaw(Colon);

            _isFirstElement = true;
        }

        #endregion

        #region Formatted Values

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// The ISO-8601, GUID and TimeSpan formats used below never contain a
        /// character that requires JSON escaping, so the content writers only
        /// need to transcode or copy.
        /// </summary>
        /// <param name="text">The text used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue(ReadOnlySpan<char> text)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);

            if (SerializeStringAsUtf8)
                WriteStringContentUtf8(text);
            else
                WriteStringContentUtf16(text);

            WriteByteRaw(Quote);
        }

        /// <summary>Writes a <see cref="DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteDateTime(DateTime value)
        {
            // "yyyy-MM-ddTHH:mm:ss.fffffff" is 27 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue(buffer.Slice(0, written));
                return;
            }

            WriteString(value.ToString("O"));
        }

        /// <summary>Writes a <see cref="DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteDateTimeOffset(DateTimeOffset value)
        {
            // Round-trip text plus the "+hh:mm" offset is 33 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue(buffer.Slice(0, written));
                return;
            }

            WriteString(value.ToString("O"));
        }

        /// <summary>Writes a <see cref="Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteGuid(Guid value)
        {
            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out int written))
            {
                WriteFormattedValue(buffer.Slice(0, written));
                return;
            }

            WriteString(value.ToString());
        }

        /// <summary>Writes a <see cref="TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteTimeSpan(TimeSpan value)
        {
            // "-10675199.02:48:05.4775808" is 26 characters.
            Span<char> buffer = stackalloc char[32];
            if (value.TryFormat(buffer, out int written, "c"))
            {
                WriteFormattedValue(buffer.Slice(0, written));
                return;
            }

            WriteString(value.ToString());
        }

        #endregion

        #region String

        /// <summary>Writes string to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteString(string? value)
        {
            WriteCommaIfNeeded();

            if (value == null)
            {
                WriteNullInternal();
                return;
            }

#if NET8_0_OR_GREATER
            // Reserve the quotes and content together. The transcoder only writes
            // after validating the complete value, so misses retain the normal path.
            if (SerializeStringAsUtf8 && (uint)(value.Length - 4) <= 28 &&
                global::System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated)
            {
                EnsureCapacity(value.Length + 2);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(
                    value.AsSpan(), _bufferReference.Slice(_currentIndex + 1, value.Length), escapeJson: true))
                {
                    ref byte output = ref GetCurrentSpanReference();
                    output = Quote;
                    Unsafe.Add(ref output, value.Length + 1) = Quote;
                    Advance(value.Length + 2);
                    return;
                }
                WriteByteRaw(Quote);
                WriteStringContentUtf8Encoded(value.AsSpan());
                WriteByteRaw(Quote);
                return;
            }
#endif

            WriteByteRaw(Quote);

            if (SerializeStringAsUtf8)
            {
                WriteStringContentUtf8(value.AsSpan());
            }
            else
            {
                WriteStringContentUtf16(value.AsSpan());
            }

            WriteByteRaw(Quote);
        }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8(ReadOnlySpan<char> value)
        {
#if NET8_0_OR_GREATER
            // Tiny ASCII values are cheaper to scan and narrow without a transcoder call.
            if (value.Length <= 32)
            {
                EnsureCapacity(value.Length);
                if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(
                    value, _bufferReference.Slice(_currentIndex), escapeJson: true))
                {
                    Advance(value.Length);
                    return;
                }
                ref byte destination = ref GetCurrentSpanReference();
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c > 0x7F || NeedsJsonEscape(c))
                    {
                        if (c <= 0x7F)
                            WriteStringContentSlow(value);
                        else
                            WriteStringContentUtf8Encoded(value);
                        return;
                    }
                    Unsafe.Add(ref destination, i) = (byte)c;
                }
                Advance(value.Length);
                return;
            }
            WriteStringContentUtf8Encoded(value);
#else
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 0x7F || c < 0x20 || c == '"' || c == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                EnsureCapacity(value.Length);
                ref byte dst = ref GetCurrentSpanReference();
                for (int i = 0; i < value.Length; i++)
                {
                    Unsafe.Add(ref dst, i) = (byte)value[i];
                }
                Advance(value.Length);
            }
            else
            {
                WriteStringContentSlow(value);
            }
#endif
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Encoded(ReadOnlySpan<char> value)
        {
            int escapeIndex = value.IndexOfAny(s_stringEscapeChars);
            if (escapeIndex < 0)
                WriteUtf8StringSegment(value);
            else
                WriteStringContentUtf8Escaped(value, escapeIndex);
        }

        /// <summary>Writes string content utf8 escaped to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="escapeIndex">The escape index used by this operation.</param>
        private void WriteStringContentUtf8Escaped(ReadOnlySpan<char> value, int escapeIndex)
        {
            while (true)
            {
                if (escapeIndex <= 8 && value.Length - escapeIndex >= 3 &&
                    NeedsJsonEscape(value[escapeIndex + 1]) && NeedsJsonEscape(value[escapeIndex + 2]))
                {
                    // Keep dense escape clusters on the existing scalar path, in bounded
                    // blocks so a dense prefix does not slow a long ordinary suffix.
                    int blockLength = Math.Min(64, value.Length);
                    if (blockLength < value.Length && char.IsHighSurrogate(value[blockLength - 1]) &&
                        char.IsLowSurrogate(value[blockLength]))
                        blockLength++;
                    WriteStringContentSlow(value.Slice(0, blockLength));
                    value = value.Slice(blockLength);
                    escapeIndex = value.IndexOfAny(s_stringEscapeChars);
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
                escapeIndex = value.IndexOfAny(s_stringEscapeChars);
                if (escapeIndex < 0)
                {
                    WriteUtf8StringSegment(value);
                    return;
                }
            }
        }

        /// <summary>Writes utf8 string segment to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf8StringSegment(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return;
            if (_output is not null)
            {
                while (!value.IsEmpty)
                {
                    var result = StringSerializer.Serialize(value, _bufferReference.Slice(_currentIndex),
                        out int charsRead, out int written);
                    Advance(written);
                    value = value.Slice(charsRead);
                    if (result == OperationStatus.Done) return;
                    if (result != OperationStatus.DestinationTooSmall)
                        LuminPackExceptionHelper.ThrowFailedEncoding(result);
                    FlushToBufferWriter();
                    EnsureCapacity(4);
                }
                return;
            }

            // Try the actual available space first, including exact-size caller spans.
            var status = StringSerializer.Serialize(value, _bufferReference.Slice(_currentIndex),
                out _, out int bytesWritten);
            if (status != OperationStatus.Done)
                bytesWritten = WriteUtf8StringSegmentSlow(value);
            Advance(bytesWritten);
        }

        /// <summary>Writes utf8 string segment slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int WriteUtf8StringSegmentSlow(ReadOnlySpan<char> value)
        {
            EnsureCapacity(Encoding.UTF8.GetByteCount(value));
            return Encoding.UTF8.GetBytes(value, _bufferReference.Slice(_currentIndex));
        }
#endif

        /// <summary>Performs the needs json escape step for the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsJsonEscape(char value) => value < 0x20 || value == '"' || value == '\\';

        /// <summary>Writes escaped utf8 char to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
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
            ref byte destination = ref GetCurrentSpanReference();
            destination = Backslash;
            Unsafe.Add(ref destination, 1) = escaped;
            if (length == 6)
            {
                Unsafe.Add(ref destination, 2) = (byte)'0';
                Unsafe.Add(ref destination, 3) = (byte)'0';
                Unsafe.Add(ref destination, 4) = HexChar((value >> 4) & 0xF);
                Unsafe.Add(ref destination, 5) = HexChar(value & 0xF);
            }
            Advance(length);
        }

        /// <summary>Writes string content utf8 raw to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8Raw(ReadOnlySpan<byte> value)
        {
#if NET8_0_OR_GREATER
            int escapeIndex = value.IndexOfAny(s_stringEscapeBytes);
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
                escapeIndex = value.IndexOfAny(s_stringEscapeBytes);
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
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlockUnaligned(ref dst, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                Advance(value.Length);
            }
            else
            {
                WriteStringContentUtf8Slow(value);
            }
#endif
        }

        /// <summary>Writes string content utf8 slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Slow(ReadOnlySpan<byte> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];

                if (b == '"')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'"');
                }
                else if (b == '\\')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'\\');
                }
                else if (b == '\n')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'n');
                }
                else if (b == '\r')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'r');
                }
                else if (b == '\t')
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'t');
                }
                else if (b < 0x20)
                {
                    WriteByteRaw(Backslash);
                    WriteByteRaw((byte)'u');
                    WriteByteRaw((byte)'0');
                    WriteByteRaw((byte)'0');
                    WriteByteRaw(HexChar((b >> 4) & 0xF));
                    WriteByteRaw(HexChar(b & 0xF));
                }
                else
                {
                    WriteByteRaw(b);
                }
            }
        }

        /// <summary>Writes string content utf16 to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16(ReadOnlySpan<char> value)
        {
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 0x20 || c == '"' || c == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                if (_output is not null)
                {
                    WriteRaw(value);
                    return;
                }
                int byteCount = checked(value.Length * 2);
                EnsureCapacity(byteCount);
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value));
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
            else
            {
                WriteStringContentUtf16Slow(value);
            }
        }

        /// <summary>Writes string content utf16 slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf16Slow(ReadOnlySpan<char> value)
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
                        if (_output is not null)
                        {
                            WriteRaw(segment);
                        }
                        else
                        {
                            int byteCount = checked(segment.Length * 2);
                            EnsureCapacity(byteCount);
                            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                            ref byte dst = ref GetCurrentSpanReference();
                            Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                            Advance(byteCount);
                        }
                    }

                    if (c == '"')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('"');
                    }
                    else if (c == '\\')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('\\');
                    }
                    else if (c == '\n')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('n');
                    }
                    else if (c == '\r')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('r');
                    }
                    else if (c == '\t')
                    {
                        WriteByteRaw(Backslash);
                        WriteUtf16CharDirect('t');
                    }
                    else if (c < 0x20)
                    {
                        WriteByteRaw(Backslash);
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
                if (_output is not null)
                {
                    WriteRaw(segment);
                    return;
                }
                int byteCount = checked(segment.Length * 2);
                EnsureCapacity(byteCount);
                ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
        }

        /// <summary>Writes utf16 char direct to the current output.</summary>
        /// <param name="c">The c used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16CharDirect(char c)
        {
            EnsureCapacity(2);
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref dst, c);
            Advance(2);
        }

        /// <summary>Performs the hex char utf16 step for the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char HexCharUtf16(int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }

        /// <summary>Writes string content slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentSlow(ReadOnlySpan<char> value)
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
                    WriteByteRaw((byte)c);
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
                    ref byte destination = ref GetCurrentSpanReference();
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

        /// <summary>Performs the hex char step for the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte HexChar(int value)
        {
            return (byte)(value < 10 ? '0' + value : 'a' + (value - 10));
        }

        #endregion

        #region Fast float/double serialization

        // Conversion: xjb (https://github.com/xjb714/xjb) shortest-decimal core, ported in
        // Core/Json/Xjb. The previous "try 1..9/17 fraction digits and verify the round trip"
        // loop plus its Utf8Formatter fallback is gone:
        //   1. XjbFloat32/XjbFloat64 turn the IEEE-754 bit pattern into {digits, exponent10} directly;
        //   2. LuminPackJsonFloatFormatter lays the digits out as JSON (policy documented there and
        //      kept byte compatible with the previous writer);
        //   3. NaN/Infinity still throw the existing exception and -0 keeps its sign bit.
        /// <summary>Writes float to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteFloat(float value)
        {
            WriteCommaIfNeeded();
            EnsureCapacity(SerializeStringAsUtf8 ? 24 : 64);
            if (float.IsNaN(value) || float.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");

            if (SerializeStringAsUtf8)
            {
                _currentIndex += LuminPackJsonFloatFormatter.WriteFloatUtf8(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                _currentIndex += LuminPackJsonFloatFormatter.WriteFloatUtf16(
                    MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex)), value) * 2;
            }
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDouble(double value)
        {
            WriteCommaIfNeeded();
            EnsureCapacity(SerializeStringAsUtf8 ? 32 : 64);
            if (double.IsNaN(value) || double.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");

            if (SerializeStringAsUtf8)
            {
                _currentIndex += LuminPackJsonFloatFormatter.WriteDoubleUtf8(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                _currentIndex += LuminPackJsonFloatFormatter.WriteDoubleUtf16(
                    MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex)), value) * 2;
            }
        }

        #endregion

        #region Primitive Types

        /// <summary>Writes int to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteInt(int value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(11);
                _currentIndex += LuminPackJsonNumberFormatter.WriteInt32(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(22);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 22));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUInt(uint value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(10);
                _currentIndex += LuminPackJsonNumberFormatter.WriteUInt32(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(20);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 20));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteByte(byte value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(4); // packed store intentionally writes 4 bytes
                _currentIndex += LuminPackJsonNumberFormatter.WriteByte(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(6);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 6));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteSByte(sbyte value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(5); // sign + packed 4-byte store
                _currentIndex += LuminPackJsonNumberFormatter.WriteSByte(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(8);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 8));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteShort(short value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(6);
                _currentIndex += LuminPackJsonNumberFormatter.WriteShort(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(12);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 12));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUShort(ushort value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(5);
                _currentIndex += LuminPackJsonNumberFormatter.WriteUShort(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(10);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 10));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteLong(long value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(20);
                _currentIndex += LuminPackJsonNumberFormatter.WriteLong(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(40);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteULong(ulong value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(20);
                _currentIndex += LuminPackJsonNumberFormatter.WriteULong(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(40);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
                value.TryFormat(charSpan, out int written);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDecimal(decimal value)
        {
            WriteCommaIfNeeded();
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(31);
                _currentIndex += LuminPackJsonNumberFormatter.WriteDecimal(
                    _bufferReference.Slice(_currentIndex), value);
            }
            else
            {
                EnsureCapacity(62);
                var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 62));
                value.TryFormat(charSpan, out int written, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                _currentIndex += written << 1;
            }
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteChar(char value)
        {
            WriteCommaIfNeeded();
            WriteByteRaw(Quote);

            if (value == '"' || value == '\\')
            {
                WriteByteRaw(Backslash);
                WriteByteRaw((byte)value);
            }
            else if (value == '\b' || value == '\f' || value == '\n' || value == '\r' || value == '\t')
            {
                WriteByteRaw(Backslash);
                WriteByteRaw(value switch
                {
                    '\b' => (byte)'b',
                    '\f' => (byte)'f',
                    '\n' => (byte)'n',
                    '\r' => (byte)'r',
                    _ => (byte)'t'
                });
            }
            else if (value < 0x20 || char.IsSurrogate(value))
            {
                WriteByteRaw(Backslash);
                WriteByteRaw((byte)'u');
                WriteByteRaw(HexChar((value >> 12) & 0xF));
                WriteByteRaw(HexChar((value >> 8) & 0xF));
                WriteByteRaw(HexChar((value >> 4) & 0xF));
                WriteByteRaw(HexChar(value & 0xF));
            }
            else if (SerializeStringAsUtf8)
            {
                if (value <= 0x7F)
                {
                    WriteByteRaw((byte)value);
                }
                else
                {
                    Span<byte> utf8Buf = stackalloc byte[4];
                    Span<char> charBuf = stackalloc char[1] { value };
                    int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                    WriteRaw(utf8Buf[..len]);
                }
            }
            else
            {
                WriteUtf16CharDirect(value);
            }

            WriteByteRaw(Quote);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        {
            WriteCommaIfNeeded();

            if (SerializeStringAsUtf8)
            {
                if (value)
                {
                    EnsureCapacity(4);
                    ref byte dst = ref GetCurrentSpanReference();
                    Unsafe.WriteUnaligned(ref dst, 0x65757274u); // "true"
                    _currentIndex += 4;
                }
                else
                {
                    EnsureCapacity(5);
                    ref byte dst = ref GetCurrentSpanReference();
                    Unsafe.WriteUnaligned(ref dst, 0x736C6166u); // "fals"
                    Unsafe.Add(ref dst, 4) = (byte)'e';
                    _currentIndex += 5;
                }
                return;
            }

            // UTF-16 path was not benchmarked.
            if (value)
            {
                WriteByteRaw((byte)'t');
                WriteByteRaw((byte)'r');
                WriteByteRaw((byte)'u');
                WriteByteRaw((byte)'e');
            }
            else
            {
                WriteByteRaw((byte)'f');
                WriteByteRaw((byte)'a');
                WriteByteRaw((byte)'l');
                WriteByteRaw((byte)'s');
                WriteByteRaw((byte)'e');
            }
        }

        #endregion

        #region Null

        /// <summary>Writes null to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WriteCommaIfNeeded();
            WriteNullInternal();
        }

        /// <summary>Writes null internal to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        {
            if (SerializeStringAsUtf8)
            {
                EnsureCapacity(4);
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.WriteUnaligned(ref dst, 0x6C6C756Eu); // "null"
                _currentIndex += 4;
                return;
            }

            WriteByteRaw((byte)'n');
            WriteByteRaw((byte)'u');
            WriteByteRaw((byte)'l');
            WriteByteRaw((byte)'l');
        }

        #endregion
    }
}
