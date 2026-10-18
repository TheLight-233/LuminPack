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
    public unsafe ref partial struct LuminPackJsonWriter
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

        private int _depth;
        private bool _isFirstElement;

        /// <summary>Gets the option state.</summary>
        public LuminPackWriterOptionalState OptionState => _state;

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
        { WriteByteRaw<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, int value)
        { WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value); }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, uint value)
        { WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value); }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, long value)
        { WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value); }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, ulong value)
        { WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value); }

        /// <summary>Performs the reserve generated integer prefix step for the current operation.</summary>
        /// <param name="first">The first used by this operation.</param>
        /// <param name="last">The last used by this operation.</param>
        /// <param name="byteCount">The number of bytes or elements to process.</param>
        /// <param name="maxDigits">The max digits used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveGeneratedIntegerPrefix(ulong first, ulong last, int byteCount, int maxDigits)
        { return ReserveGeneratedIntegerPrefix<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, maxDigits); }

        /// <summary>Writes comma if needed to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        { WriteCommaIfNeeded<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        #endregion

        #region Object/Array

        /// <summary>Writes object start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        { WriteObjectStart<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Writes object end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        { WriteObjectEnd<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Writes array start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        { WriteArrayStart<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Writes array end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        { WriteArrayEnd<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        #endregion

        #region Property Name

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="utf8PropertyName">The utf8 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        { WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf8PropertyName); }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="utf16PropertyName">The utf16 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        { WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf16PropertyName); }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name="propertyName">The property name used by this operation.</param>
        public void WritePropertyName(string propertyName)
        { WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(propertyName); }

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
        { WriteFormattedValue<global::LuminPack.Option.LuminPackDefaultOptions>(text); }

        /// <summary>Writes a <see cref="DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteDateTime(DateTime value)
        { WriteDateTime<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes a <see cref="DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteDateTimeOffset(DateTimeOffset value)
        { WriteDateTimeOffset<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes a <see cref="Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteGuid(Guid value)
        { WriteGuid<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes a <see cref="TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteTimeSpan(TimeSpan value)
        { WriteTimeSpan<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        #endregion

        #region String

        /// <summary>Writes string to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void WriteString(string? value)
        { WriteString<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8(ReadOnlySpan<char> value)
        { WriteStringContentUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Encoded(ReadOnlySpan<char> value)
        { WriteStringContentUtf8Encoded<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes string content utf8 escaped to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="escapeIndex">The escape index used by this operation.</param>
        private void WriteStringContentUtf8Escaped(ReadOnlySpan<char> value, int escapeIndex)
        { WriteStringContentUtf8Escaped<global::LuminPack.Option.LuminPackDefaultOptions>(value, escapeIndex); }

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
        { WriteStringContentUtf8Raw<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes string content utf8 slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Slow(ReadOnlySpan<byte> value)
        { WriteStringContentUtf8Slow<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes string content utf16 to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16(ReadOnlySpan<char> value)
        { WriteStringContentUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes string content utf16 slow to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf16Slow(ReadOnlySpan<char> value)
        { WriteStringContentUtf16Slow<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

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
        { WriteStringContentSlow<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

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
        { WriteFloat<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes double to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDouble(double value)
        { WriteDouble<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        #endregion

        #region Primitive Types

        /// <summary>Writes int to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteInt(int value)
        { WriteInt<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUInt(uint value)
        { WriteUInt<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteByte(byte value)
        { WriteByte<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteSByte(sbyte value)
        { WriteSByte<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes short to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteShort(short value)
        { WriteShort<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteUShort(ushort value)
        { WriteUShort<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes long to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteLong(long value)
        { WriteLong<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteULong(ulong value)
        { WriteULong<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteDecimal(decimal value)
        { WriteDecimal<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes char to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void WriteChar(char value)
        { WriteChar<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBool(bool value)
        { WriteBool<global::LuminPack.Option.LuminPackDefaultOptions>(value); }

        #endregion

        #region Null

        /// <summary>Writes null to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        { WriteNull<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        /// <summary>Writes null internal to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        { WriteNullInternal<global::LuminPack.Option.LuminPackDefaultOptions>(); }

        #endregion
    }
}
