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
    /// <summary>Writes JSON tokens with the configured UTF encoding to a reusable or external buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackJsonWriter
#else
    public unsafe ref partial struct LuminPackJsonWriter
#endif
    {
        private Span<byte> _buffer;
        /// <summary>The first byte after the current storage window, cached with every window change.</summary>
#if NET8_0_OR_GREATER
        private ref byte _end;
#else
        private byte* _end;
#endif

        /// <summary>Gets the current output storage window.</summary>
        /// <remarks>Only the writer replaces its window, so the cached end always describes it.</remarks>
        public Span<byte> Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get => _buffer;
            internal set
            {
                _buffer = value;
#if NET8_0_OR_GREATER
                _end = ref Unsafe.Add(ref MemoryMarshal.GetReference(value), (nint)(uint)value.Length);
#else
                _end = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(value)) + value.Length;
#endif
            }
        }
#if NET8_0_OR_GREATER
        /// <summary>The advancing JSON data cursor.</summary>
        public ref byte Cursor;
#else
        /// <summary>The advancing JSON data cursor for compatibility targets.</summary>
        public byte* Cursor;
#endif
        /// <summary>The native buffer owner, when writing to a reusable buffer.</summary>
        public LuminBufferWriter? Owner {[MethodImpl(MethodImplOptions.AggressiveInlining)] readonly get; internal set; }
        /// <summary>The external destination, when writing through a staging buffer.</summary>
        public readonly IBufferWriter<byte>? Output;
        /// <summary>The number of bytes already committed to the external destination.</summary>
        public long CommittedBytes;
        /// <summary>The operation's options and reference tracking state.</summary>
        private LuminPackWriterOptionalState? _options;
        /// <summary>Gets or sets the operation reference context.</summary>
        public LuminPackWriterOptionalState? Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _options ?? Owner?.WriterState;
            [MethodImpl(MethodImplOptions.AggressiveInlining)] set => _options = value;
        }
        /// <summary>The current JSON container nesting depth.</summary>
        public int Depth;
        /// <summary>Whether the current container has not yet written an element.</summary>
        public bool IsFirstElement;
        /// <summary>The opening object delimiter.</summary>
        public const byte OpenBrace = (byte)'{';
        /// <summary>The closing object delimiter.</summary>
        public const byte CloseBrace = (byte)'}';
        /// <summary>The opening array delimiter.</summary>
        public const byte OpenBracket = (byte)'[';
        /// <summary>The closing array delimiter.</summary>
        public const byte CloseBracket = (byte)']';
        /// <summary>The string delimiter.</summary>
        public const byte Quote = (byte)'"';
        /// <summary>The property name and value separator.</summary>
        public const byte Colon = (byte)':';
        /// <summary>The container element separator.</summary>
        public const byte Comma = (byte)',';
        /// <summary>The string escape prefix.</summary>
        public const byte Backslash = (byte)'\\';
#if NET8_0_OR_GREATER
        /// <summary>The JSON characters requiring escaping.</summary>
        public const string StringEscapeChars = "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007" + "\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" + "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017" + "\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\"\\";
#endif
#if NET8_0_OR_GREATER
        /// <summary>The character search table for string escaping.</summary>
        public static readonly SearchValues<char> StringEscapeCharacters = SearchValues.Create(StringEscapeChars);
#endif
#if NET8_0_OR_GREATER
        /// <summary>The UTF-8 byte search table for string escaping.</summary>
        public static readonly SearchValues<byte> StringEscapeBytes = SearchValues.Create(Encoding.UTF8.GetBytes(StringEscapeChars));
#endif
#region Constructors
        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <remarks>On netstandard2.1, managed span storage must remain pinned for the complete writer lifetime. Dispose an external streaming writer to release its staging lease after a failure.</remarks>
        public LuminPackJsonWriter(ref Span<byte> bufferReference, LuminPackWriterOptionalState state)
        {
            _options = state;
            Buffer = bufferReference;
            Position = 0;
            Owner = null;
            Output = null;
            CommittedBytes = 0;
            Depth = 0;
            IsFirstElement = true;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c>.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        /// <param name = "state">The reusable state associated with the operation.</param>
        public LuminPackJsonWriter(LuminBufferWriter? bufferWriter, LuminPackWriterOptionalState state)
        {
            _options = state;
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            Buffer = bufferWriter.BeginCursorWrite(out int position);
            Position = position;
            Owner = bufferWriter;
            Output = null;
            CommittedBytes = 0;
            Depth = 0;
            IsFirstElement = true;
        }

        /// <summary>Initializes a new instance of <c>LuminPackJsonWriter</c> with the buffer's own reference context.</summary>
        /// <param name = "bufferWriter">The writer receiving the output data.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackJsonWriter(LuminBufferWriter bufferWriter)
        {
            if (bufferWriter == null)
                LuminPackExceptionHelper.ThrowBufferWriterNull();
            Buffer = bufferWriter.BeginCursorWrite(out int position);
            Position = position;
            Owner = bufferWriter;
            Output = null;
            CommittedBytes = 0;
            Depth = 0;
            IsFirstElement = true;
        }

        /// <summary>Creates a sequential writer which commits JSON directly to an external buffer writer.</summary>
        /// <remarks>CurrentIndex and GetSpan refer to the current chunk. Use BytesWritten for the total and FlushToBufferWriter to commit the final chunk.</remarks>
        /// <param name = "output">The destination receiving the result.</param>
        /// <param name = "state">The reusable state associated with the operation.</param>
        /// <param name = "initialSizeHint">The initial size hint used by this operation.</param>
        /// <remarks>Dispose the writer after a failure to release its compatibility-target staging lease.</remarks>
        public LuminPackJsonWriter(IBufferWriter<byte> output, LuminPackWriterOptionalState state, int initialSizeHint)
        {
            if (output is null)
                LuminPackExceptionHelper.ThrowArgumentNullException(nameof(output));
            if (initialSizeHint <= 0)
                LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(initialSizeHint));
            _options = state;
            Output = output;
            Owner = null;
            CommittedBytes = 0;
            Depth = 0;
            IsFirstElement = true;
#if NET8_0_OR_GREATER
            Buffer = output.GetSpan(initialSizeHint);
            if (Buffer.Length < initialSizeHint)
                LuminPackExceptionHelper.ThrowInvalidOperationException("IBufferWriter returned a span smaller than sizeHint");
            Position = 0;
#else
            Owner = RentStaging(initialSizeHint);
            Buffer = Owner.GetFullSpan().Slice(0, initialSizeHint);
            Position = 0;
#endif
        }

#endregion

#region Cursor and helpers
        /// <summary>Dereferences the current cursor inside shared token implementation kernels.</summary>
        private ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref Unsafe.AsRef<byte>(Cursor);
#endif
        }

        /// <summary>Gets or restores the absolute cursor position inside the current buffer.</summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(Buffer), ref Cursor);
#else
            // Native-width byte offsets avoid Mono's element-size division and retain pointer wrap semantics.
            get => (int)unchecked((nint)Cursor - (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)value);
#else
                Cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)) + value;
#endif
            }
        }

        /// <summary>Gets the option state.</summary>
        public LuminPackWriterOptionalState OptionState => Options ?? (Options = new LuminPackWriterOptionalState());
        /// <summary>Gets the current index.</summary>
        public int CurrentIndex => Position;
        /// <summary>Gets the total JSON bytes written, including committed output chunks.</summary>
        public long BytesWritten => CommittedBytes + Position;

#if !NET8_0_OR_GREATER
        /// <summary>Acquires stationary streaming storage while retaining cleanup ownership if allocation fails.</summary>
        /// <param name = "capacity">The initial dynamic chunk capacity.</param>
        /// <returns>The owned staging lease.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminBufferWriter RentStaging(int capacity)
        {
            var owner = LuminBufferWriterPool.Rent();
            try
            {
                owner.EnsureCapacity(capacity);
                return owner;
            }
            catch (Exception exception)
            {
                LuminBufferWriterPool.Return(owner);
                LuminPackExceptionHelper.Rethrow(exception);
                return null!;
            }
        }

#endif
        /// <summary>Commits the remaining JSON bytes to the external writer without flushing its underlying stream.</summary>
        public void FlushToBufferWriter()
        {
            if (Output is null)
                LuminPackExceptionHelper.ThrowInvalidOperationException("This JSON writer has no external output");
            int count = Position;
            CommitOutput(Output, Buffer.Slice(0, count));
            CommittedBytes += count;
            Dispose();
            Buffer = default;
            Position = 0;
        }

        /// <summary>Releases compatibility-target streaming storage without publishing incomplete output.</summary>
        public void Dispose()
        {
#if !NET8_0_OR_GREATER
            if (Output is not null && Owner is not null)
            {
                LuminBufferWriterPool.Return(Owner);
                Owner = null;
            }
#endif
        }

        /// <summary>Advances the current buffer position by the supplied number of bytes.</summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
#if DEBUG
            if ((uint)count > (uint)(Buffer.Length - Position))
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
#endif
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor += count;
#endif
        }

        /// <summary>Ensures that the buffer can accommodate the requested write.</summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int count)
        {
            // Signed 64-bit distances send a cursor past the end and every negative count to the validating slow path.
#if NET8_0_OR_GREATER
            if ((long)Unsafe.ByteOffset(ref Cursor, ref _end) >= (long)(uint)count && count >= 0)
#else
            if ((long)unchecked((nint)_end - (nint)Cursor) >= (long)(uint)count && count >= 0)
#endif
                return;
            EnsureCapacitySlow(count);
        }

        /// <summary>Refreshes dynamic storage out of line, keeping growth temporaries out of every inlined write site.</summary>
        /// <remarks>Generated JSON operations always address their writer by reference, so this call exposes no register state.</remarks>
        /// <param name = "count">The complete dynamic record capacity requirement.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacitySlow(int count)
        {
            int position = Position;
            if (count < 0 || position > int.MaxValue - count)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
            Span<byte> buffer;
            if (Output is not null)
            {
#if !NET8_0_OR_GREATER
                Owner ??= LuminBufferWriterPool.Rent();
#endif
                buffer = GrowOutput(Output, Owner, Buffer.Slice(0, position), Buffer.Length != 0, count);
                CommittedBytes += position;
                position = 0;
            }
            else
                buffer = GrowNative(Owner, position + count);
            Buffer = buffer;
            Position = position;
        }

        /// <summary>Grows an owning native destination independently of the writer's cursor fields.</summary>
        /// <param name = "owner">The native destination, or null for a fixed caller span.</param>
        /// <param name = "required">The complete required byte capacity.</param>
        /// <returns>The refreshed stationary output storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Span<byte> GrowNative(LuminBufferWriter? owner, int required)
        {
            if (owner is null)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(required);
            owner.EnsureCapacity(required);
            return owner.GetFullSpan();
        }

        /// <summary>Commits one chunk and acquires the next without exposing the writer's address.</summary>
        /// <param name = "output">The external managed destination.</param>
        /// <param name = "owner">The compatibility target's stationary staging lease.</param>
        /// <param name = "payload">The completed bytes of the previous chunk.</param>
        /// <param name = "acquired">Whether the previous chunk was obtained from the external writer and not yet committed.</param>
        /// <param name = "required">The next complete dynamic write capacity.</param>
        /// <returns>The next tracked or stationary chunk.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Span<byte> GrowOutput(IBufferWriter<byte> output, LuminBufferWriter? owner, ReadOnlySpan<byte> payload, bool acquired, int required)
        {
            int sizeHint = Math.Max(4096, required);
#if NET8_0_OR_GREATER
            // An acquired chunk is committed, possibly empty, before the next one is requested.
            if (acquired)
                output.Advance(payload.Length);
            var span = output.GetSpan(sizeHint);
            if (span.Length < sizeHint)
                LuminPackExceptionHelper.ThrowInvalidOperationException("IBufferWriter returned a span smaller than sizeHint");
            return span;
#else
            CommitOutput(output, payload);
            owner!.EnsureCapacity(sizeHint);
            return owner.GetFullSpan().Slice(0, sizeHint);
#endif
        }

        /// <summary>Publishes a complete tracked or stationary chunk through the external writer contract.</summary>
        /// <param name = "output">The destination receiving the completed bytes.</param>
        /// <param name = "payload">The current completed chunk.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CommitOutput(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
        {
#if NET8_0_OR_GREATER
            if (!payload.IsEmpty)
                output.Advance(payload.Length);
#else
            while (!payload.IsEmpty)
            {
                int count = Math.Min(4096, payload.Length);
                var destination = output.GetSpan(count);
                if (destination.Length < count)
                    LuminPackExceptionHelper.ThrowInvalidOperationException("IBufferWriter returned a span smaller than sizeHint");
                payload.Slice(0, count).CopyTo(destination);
                output.Advance(count);
                payload = payload.Slice(count);
            }
#endif
        }

        /// <summary>Updates the span used by the current operation.</summary>
        /// <param name = "span">The buffer used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            int position = Position;
            Buffer = span;
            Position = position;
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = default;
            Position = 0;
            Depth = 0;
            IsFirstElement = true;
        }

        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushBuffer()
        {
            int position = Position;
            if (Owner != null)
            {
                Buffer = Owner.GetFullSpan();
                Position = position;
            }
        }

        /// <summary>Obtains the span for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return Buffer[..Position];
        }

        /// <summary>Updates the first element used by the current operation.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFirstElement(bool value)
        {
            IsFirstElement = value;
        }

        /// <summary>Performs the reserve generated integer prefix step for the current operation.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "maxDigits">The max digits used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveGeneratedIntegerPrefix(ulong first, ulong last, int byteCount, int maxDigits)
        {
            return ReserveGeneratedIntegerPrefix<LuminPackDefaultOptions>(first, last, byteCount, maxDigits);
        }

        /// <summary>Performs the needs json escape step for the current operation.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool NeedsJsonEscape(char value) => value < 0x20 || value == '"' || value == '\\';
        /// <summary>Performs the hex char step for the current operation.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte HexChar(int value)
        {
            return (byte)(value < 10 ? '0' + value : 'a' + (value - 10));
        }

        /// <summary>Performs the reserve generated integer prefix step for the current operation.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "maxDigits">The max digits used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private Span<byte> ReserveGeneratedIntegerPrefix<TOptions>(ulong first, ulong last, int byteCount, int maxDigits)
            where TOptions : struct, ILuminPackOptions
        {
            if (!(default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
                LuminPackExceptionHelper.ThrowInvalidOperationException("Generated integer prefixes require UTF-8");
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            // Reserve before obtaining either reference: growth/streaming may replace the span and reset the cursor. The
            // reservation also rejects a cursor past the window. Advance only after formatting.
            EnsureCapacity(byteCount + maxDigits);
            ref byte destination = ref Current;
            LuminPackWriter.Write(ref destination, first);
            LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(byteCount - 8)), last);
            IsFirstElement = false;
            return LuminPackMarshal.CreateSpan(ref Unsafe.Add(ref destination, (nint)(uint)byteCount), maxDigits);
        }

#endregion

#region Values
        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw(byte value)
        {
            WriteByteRaw<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<byte> value)
        {
            if (Output is not null)
            {
                WriteRawToOutput(value);
                return;
            }

            EnsureCapacity(value.Length);
            value.CopyTo(Buffer.Slice(Position, value.Length));
            Advance(value.Length);
        }

        /// <summary>Writes raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRaw(scoped ReadOnlySpan<char> value)
        {
            if (Output is not null)
            {
                WriteRawToOutput(MemoryMarshal.AsBytes(value));
                return;
            }

            var byteCount = unchecked(value.Length << 1);
            EnsureCapacity(byteCount);
            MemoryMarshal.Cast<char, byte>(value).CopyTo(Buffer.Slice(Position, byteCount));
            Advance(byteCount);
        }

        // Keep segmented-output loops out of each inlined generated JSON prefix.
        /// <summary>Writes raw to output to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteRawToOutput(scoped ReadOnlySpan<byte> value)
        {
            while (!value.IsEmpty)
            {
                if (Position == Buffer.Length)
                    EnsureCapacity(Math.Min(4096, value.Length));
                int count = Math.Min(value.Length, Buffer.Length - Position);
                value.Slice(0, count).CopyTo(Buffer.Slice(Position));
                Advance(count);
                value = value.Slice(count);
            }
        }

        /// <summary>Generator-only copy of an 8..16-byte literal packed into two overlapping native words.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedLiteral(ulong first, ulong last, int byteCount)
        {
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            if ((uint)Position > (uint)Buffer.Length)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(Position);
            EnsureCapacity(byteCount);
            ref byte destination = ref Current;
            LuminPackWriter.Write(ref destination, first);
            LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(byteCount - 8)), last);
            Advance(byteCount);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, int value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, uint value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, long value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, ulong value)
        {
            WriteGeneratedInteger<LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        {
            WriteCommaIfNeeded<LuminPackDefaultOptions>();
        }

        /// <summary>Writes object start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            WriteObjectStart<LuminPackDefaultOptions>();
        }

        /// <summary>Writes object end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            WriteObjectEnd<LuminPackDefaultOptions>();
        }

        /// <summary>Writes array start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            WriteArrayStart<LuminPackDefaultOptions>();
        }

        /// <summary>Writes array end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            WriteArrayEnd<LuminPackDefaultOptions>();
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            WritePropertyName<LuminPackDefaultOptions>(utf8PropertyName);
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        {
            WritePropertyName<LuminPackDefaultOptions>(utf16PropertyName);
        }

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// Round-trip dates/times, GUIDs, constant TimeSpan and invariant integer
        /// formats never require JSON escaping. Their bounded stack payloads can
        /// be transcoded or copied without another escape scan.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue(scoped ReadOnlySpan<char> text)
        {
            WriteFormattedValue<LuminPackDefaultOptions>(text);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTime value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTimeOffset value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(Guid value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(TimeSpan value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes a date in its round-trip JSON string form.</summary>
        /// <param name="value">The date to write.</param>
        public void Write(DateOnly value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a time in its round-trip JSON string form.</summary>
        /// <param name="value">The time to write.</param>
        public void Write(TimeOnly value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a signed 128-bit integer as an invariant decimal JSON string.</summary>
        /// <param name="value">The integer to write.</param>
        public void Write(Int128 value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes an unsigned 128-bit integer as an invariant decimal JSON string.</summary>
        /// <param name="value">The integer to write.</param>
        public void Write(UInt128 value) => Write<LuminPackDefaultOptions>(value);
#endif

        // Conversion: xjb (https://github.com/xjb714/xjb) shortest-decimal core, ported in
        // Core/Json/Xjb. The previous "try 1..9/17 fraction digits and verify the round trip"
        // loop plus its Utf8Formatter fallback is gone:
        //   1. XjbFloat32/XjbFloat64 turn the IEEE-754 bit pattern into {digits, exponent10} directly;
        //   2. LuminPackJsonFloatFormatter lays the digits out as JSON (policy documented there and
        //      kept byte compatible with the previous writer);
        //   3. NaN/Infinity still throw the existing exception and -0 keeps its sign bit.
        /// <summary>Writes float to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(float value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(double value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(int value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(uint value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(byte value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(sbyte value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(short value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ushort value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(long value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ulong value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(decimal value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(char value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(bool value)
        {
            Write<LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes null to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WriteNull<LuminPackDefaultOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        {
            WriteNullInternal<LuminPackDefaultOptions>();
        }

        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw<TOptions>(byte value)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(1);
                Current = value;
                Advance(1);
            }
            else
            {
                EnsureCapacity(2);
                ref byte dst = ref Current;
                LuminPackWriter.Write(ref dst, (char)value);
                Advance(2);
            }
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, int value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 11);
            int written = LuminPackJsonNumberFormatter.WriteInt32(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, uint value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 10);
            int written = LuminPackJsonNumberFormatter.WriteUInt32(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, long value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteLong(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, ulong value)
            where TOptions : struct, ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteULong(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (!IsFirstElement)
            {
                WriteByteRaw<TOptions>(Comma);
            }

            IsFirstElement = false;
        }

        /// <summary>Writes object start to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(OpenBrace);
            Depth++;
            IsFirstElement = true;
        }

        /// <summary>Writes object end to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteByteRaw<TOptions>(CloseBrace);
            Depth--;
            IsFirstElement = false;
        }

        /// <summary>Writes array start to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(OpenBracket);
            Depth++;
            IsFirstElement = true;
        }

        /// <summary>Writes array end to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteByteRaw<TOptions>(CloseBracket);
            Depth--;
            IsFirstElement = false;
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(ReadOnlySpan<byte> utf8PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
                WriteStringContentUtf8Raw<TOptions>(utf8PropertyName);
            else
                WriteUtf8NameAsUtf16<TOptions>(utf8PropertyName);
            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>Transcodes a UTF-8 property name for a UTF-16 document in stack or pooled storage.</summary>
        /// <param name = "utf8PropertyName">The UTF-8 name.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile.</typeparam>
        /// <remarks>A UTF-8 sequence never decodes to more UTF-16 code units than it has bytes, so no intermediate string is needed.</remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteUtf8NameAsUtf16<TOptions>(ReadOnlySpan<byte> utf8PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            char[]? rented = null;
            Span<char> chars = utf8PropertyName.Length <= 256
                ? stackalloc char[256]
                : (rented = System.Buffers.ArrayPool<char>.Shared.Rent(utf8PropertyName.Length));
            try
            {
                int length = Utf8PlatformDecoder.Decode(utf8PropertyName, chars);
                WriteStringContentUtf16<TOptions>(chars.Slice(0, length));
            }
            finally
            {
                if (rented is not null)
                    System.Buffers.ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(ReadOnlySpan<char> utf16PropertyName)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                WriteStringContentUtf8<TOptions>(utf16PropertyName);
            }
            else
            {
                WriteStringContentUtf16<TOptions>(utf16PropertyName);
            }

            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// Round-trip dates/times, GUIDs, constant TimeSpan and invariant integer
        /// formats never require JSON escaping. Their bounded stack payloads can
        /// be transcoded or copied without another escape scan.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue<TOptions>(scoped ReadOnlySpan<char> text)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
                WriteFormattedAscii(text);
            else
                WriteRaw(text);
            WriteByteRaw<TOptions>(Quote);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes the same quoted scalar framing around an already formatted UTF-8 payload.</summary>
        /// <param name="text">The bounded ASCII scalar bytes.</param>
        /// <typeparam name="TOptions">The unchanged UTF-8 document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedUtf8Value<TOptions>(scoped ReadOnlySpan<byte> text) where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            WriteFormattedUtf8Payload(text);
            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Preserves the original whole-payload reservation and publication boundary.</summary>
        /// <param name="text">The completely formatted payload.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteFormattedUtf8Payload(scoped ReadOnlySpan<byte> text)
        {
            EnsureCapacity(text.Length);
            text.CopyTo(Buffer.Slice(Position));
            Advance(text.Length);
        }
#endif

        /// <summary>Transcodes bounded, escape-free scalar text without importing encoder state into each formatter.</summary>
        /// <param name="text">The known ASCII scalar representation.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteFormattedAscii(scoped ReadOnlySpan<char> text)
        {
            EnsureCapacity(text.Length);
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(text, Buffer.Slice(Position), out _, out int written);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            Advance(written);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateTime value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[33];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            // "yyyy-MM-ddTHH:mm:ss.fffffff" is 27 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateTimeOffset value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[33];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            // Round-trip text plus the "+hh:mm" offset is 33 characters.
            Span<char> buffer = stackalloc char[33];
            if (value.TryFormat(buffer, out int written, "O"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(Guid value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                WriteGuidUtf8Destination<TOptions>(value);
                return;
            }
#endif

            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out int written))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

#if NET8_0_OR_GREATER
        /// <summary>Formats the fixed 36-byte GUID payload into the originally reserved output range.</summary>
        /// <param name="value">The immutable GUID value captured by the caller.</param>
        /// <typeparam name="TOptions">The existing UTF-8 profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteGuidUtf8Destination<TOptions>(Guid value) where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            EnsureCapacity(36);
            if (!value.TryFormat(Buffer.Slice(Position, 36), out int written))
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.DestinationTooSmall);
            Advance(written);
            WriteByteRaw<TOptions>(Quote);
        }
#endif

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(TimeSpan value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[32];
                if (value.TryFormat(utf8, out int utf8Written, "c"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString());
                return;
            }
#endif

            // "-10675199.02:48:05.4775808" is 26 characters.
            Span<char> buffer = stackalloc char[32];
            if (value.TryFormat(buffer, out int written, "c"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

        /// <summary>Writes an invariant quoted decimal integer with the library default profile.</summary>
        /// <param name="value">The arbitrary-precision integer.</param>
        public void WriteBigInteger(System.Numerics.BigInteger value) => WriteBigInteger<LuminPackDefaultOptions>(value);

        /// <summary>Writes invariant quoted digits while keeping zero outside the formatting buffer path.</summary>
        /// <param name="value">The arbitrary-precision integer.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBigInteger<TOptions>(System.Numerics.BigInteger value) where TOptions : struct, ILuminPackOptions
        {
            if (value.IsZero)
                Write<TOptions>("0");
            else
            {
                int byteCount = value.GetByteCount();
                if (byteCount <= sizeof(int))
                    WriteBigIntegerSmall<TOptions>((int)value);
                else
                    WriteBigIntegerValue<TOptions>(value, byteCount);
            }
        }

        /// <summary>Formats signed Int32 magnitudes without arbitrary-precision scratch or pool management.</summary>
        /// <param name="value">The integer proven to fit a signed Int32.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBigIntegerSmall<TOptions>(int value) where TOptions : struct, ILuminPackOptions
        {
            Span<char> text = stackalloc char[11];
            if (value.TryFormat(text, out int written, provider: System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(text.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Keeps arbitrary-precision formatting and pooled storage outside generated callers.</summary>
        /// <param name="value">The nonzero integer.</param>
        /// <param name="byteCount">Its exact signed byte width.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteBigIntegerValue<TOptions>(System.Numerics.BigInteger value, int byteCount) where TOptions : struct, ILuminPackOptions
        {
            // log10(256) < 2.5; ten spare characters also cover a padded decimal group and its sign.
            long capacity = (long)byteCount * 5 / 2 + 10;
            if (capacity > int.MaxValue)
            {
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            char[]? rented = null;
            Span<char> text = capacity <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent((int)capacity));
            try
            {
#if NET10_0_OR_GREATER
                if (value.TryFormat(text, out int written, provider: System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedValue<TOptions>(text.Slice(0, written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
#else
                int start = FormatBigIntegerMagnitude(value, byteCount, text);
                WriteFormattedValue<TOptions>(text.Slice(start));
#endif
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<char>.Shared.Return(rented);
            }
        }

#if !NET10_0_OR_GREATER
        /// <summary>Converts public magnitude bytes into decimal groups without older BCL formatter allocations.</summary>
        /// <param name="value">The nonzero integer outside the signed Int32 range.</param>
        /// <param name="byteCount">Its signed byte width.</param>
        /// <param name="text">The bounded destination, including space for one padded group and the sign.</param>
        /// <returns>The first character of the decimal suffix written to the destination.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int FormatBigIntegerMagnitude(System.Numerics.BigInteger value, int byteCount, Span<char> text)
        {
            int count = (byteCount - 1) / sizeof(uint) + 1;
            uint[]? rented = null;
            int decimalCapacity = (int)((long)count * 10 / 9 + 1);
            int workspaceLength = count + decimalCapacity;
            Span<uint> workspace = workspaceLength <= 128 ? stackalloc uint[128] : (rented = ArrayPool<uint>.Shared.Rent(workspaceLength));
            Span<uint> words = workspace.Slice(0, count);
            Span<uint> groups = workspace.Slice(count, decimalCapacity);
            try
            {
                // Only the final partial word can contain bytes not written by TryWriteBytes.
                words[count - 1] = 0;
                bool success = System.Numerics.BigInteger.Abs(value).TryWriteBytes(MemoryMarshal.AsBytes(words), out _, isUnsigned: true);
                System.Diagnostics.Debug.Assert(success);
                if (!BitConverter.IsLittleEndian)
                    for (int i = 0; i < count; i++)
                        words[i] = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(words[i]);

                while (words[count - 1] == 0)
                    count--;
                int end = text.Length;
                if (count <= 2)
                {
                    ulong magnitude = words[0];
                    if (count == 2)
                        magnitude |= (ulong)words[1] << 32;
                    end -= 20;
                    success = magnitude.TryFormat(text.Slice(end, 20), out _, "D20", System.Globalization.CultureInfo.InvariantCulture);
                    System.Diagnostics.Debug.Assert(success);
                }
                else
                {
                    const uint decimalBase = 1_000_000_000;
                    int used = 0;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        uint carry = words[i];
                        for (int j = 0; j < used; j++)
                        {
                            // Horner conversion carries the quotient, avoiding a remainder dependency between groups.
                            ulong expanded = ((ulong)groups[j] << 32) | carry;
                            uint next = (uint)(expanded / decimalBase);
                            groups[j] = (uint)(expanded - (ulong)next * decimalBase);
                            carry = next;
                        }
                        if (carry != 0)
                        {
                            uint high = carry / decimalBase;
                            groups[used++] = carry - high * decimalBase;
                            if (high != 0)
                                groups[used++] = high;
                        }
                    }
                    ReadOnlySpan<byte> pairs = LuminPackJsonFloatFormatter.DigitPairs;
                    for (int i = 0; i < used; i++)
                    {
                        end -= 9;
                        Span<char> digits = text.Slice(end, 9);
                        uint group = groups[i];
                        for (int position = 8; position > 0; position -= 2)
                        {
                            uint high = group / 100;
                            int pair = (int)(group - high * 100) * 2;
                            digits[position] = (char)pairs[pair + 1];
                            digits[position - 1] = (char)pairs[pair];
                            group = high;
                        }
                        digits[0] = (char)('0' + group);
                    }
                }
                while (text[end] == '0')
                    end++;
                if (value.Sign < 0)
                    text[--end] = '-';
                return end;
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<uint>.Shared.Return(rented);
            }
        }
#endif

        /// <summary>Writes a version string with the library default profile.</summary>
        /// <param name="value">The version, or null.</param>
        public void WriteVersion(Version? value) => WriteVersion<LuminPackDefaultOptions>(value);

        /// <summary>Writes the established component string without allocating an intermediate string.</summary>
        /// <param name="value">The version, or null.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVersion<TOptions>(Version? value) where TOptions : struct, ILuminPackOptions
        {
            if (value is null)
                WriteNull<TOptions>();
            else
                WriteVersionValue<TOptions>(value);
        }

        /// <summary>Keeps bounded component formatting outside generated callers and null writes.</summary>
        /// <param name="value">The nonnull version.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WriteVersionValue<TOptions>(Version value) where TOptions : struct, ILuminPackOptions
        {
            // Four nonnegative Int32 components and three separators require at most 43 characters.
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[43];
                if (value.TryFormat(utf8, out int written))
                {
                    WriteCommaIfNeeded<TOptions>();
                    WriteByteRaw<TOptions>(Quote);
                    WriteRaw(utf8.Slice(0, written));
                    WriteByteRaw<TOptions>(Quote);
                    return;
                }
                Write<TOptions>(value.ToString());
                return;
            }
#endif
            Span<char> text = stackalloc char[43];
            if (value.TryFormat(text, out int charsWritten))
                WriteFormattedValue<TOptions>(text.Slice(0, charsWritten));
            else
                Write<TOptions>(value.ToString());
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes a date in its round-trip JSON string form without an intermediate string.</summary>
        /// <param name="value">The date to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Write<TOptions>(DateOnly value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[10];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[10];
            if (value.TryFormat(buffer, out int written, "O"))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a time in its round-trip JSON string form without an intermediate string.</summary>
        /// <param name="value">The time to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Write<TOptions>(TimeOnly value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[16];
                if (value.TryFormat(utf8, out int utf8Written, "O"))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString("O"));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[16];
            if (value.TryFormat(buffer, out int written, "O"))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString("O"));
        }

        /// <summary>Writes a signed 128-bit integer as an invariant decimal JSON string without an intermediate string.</summary>
        /// <param name="value">The integer to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Write<TOptions>(Int128 value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[40];
                if (value.TryFormat(utf8, out int utf8Written, default, System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[40];
            if (value.TryFormat(buffer, out int written, default, System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Writes an unsigned 128-bit integer as an invariant decimal JSON string without an intermediate string.</summary>
        /// <param name="value">The integer to write.</param>
        /// <typeparam name="TOptions">The stateless document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Write<TOptions>(UInt128 value)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                Span<byte> utf8 = stackalloc byte[39];
                if (value.TryFormat(utf8, out int utf8Written, default, System.Globalization.CultureInfo.InvariantCulture))
                    WriteFormattedUtf8Value<TOptions>(utf8.Slice(0, utf8Written));
                else
                    Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
#endif

            Span<char> buffer = stackalloc char[39];
            if (value.TryFormat(buffer, out int written, default, System.Globalization.CultureInfo.InvariantCulture))
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            else
                Write<TOptions>(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
#endif

        /// <summary>Writes float to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(float value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            EnsureCapacity((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 24 : 64);
            if (float.IsNaN(value) || float.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                Advance(LuminPackJsonFloatFormatter.WriteFloatUtf8(Buffer.Slice(Position), value));
            }
            else
            {
                Advance(LuminPackJsonFloatFormatter.WriteFloatUtf16(MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position)), value) * 2);
            }
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(double value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            EnsureCapacity((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 32 : 64);
            if (double.IsNaN(value) || double.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                Advance(LuminPackJsonFloatFormatter.WriteDoubleUtf8(Buffer.Slice(Position), value));
            }
            else
            {
                Advance(LuminPackJsonFloatFormatter.WriteDoubleUtf16(MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position)), value) * 2);
            }
        }

        /// <summary>Writes int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(int value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes the complete int JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(int value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(11);
                Advance(LuminPackJsonNumberFormatter.WriteInt32(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(22);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 22));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(uint value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(10);
                Advance(LuminPackJsonNumberFormatter.WriteUInt32(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(20);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 20));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(byte value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(4); // packed store intentionally writes 4 bytes
                Advance(LuminPackJsonNumberFormatter.WriteByte(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(6);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 6));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(sbyte value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(5); // sign + packed 4-byte store
                Advance(LuminPackJsonNumberFormatter.WriteSByte(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(8);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 8));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(short value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(6);
                Advance(LuminPackJsonNumberFormatter.WriteShort(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(12);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 12));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(ushort value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(5);
                Advance(LuminPackJsonNumberFormatter.WriteUShort(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(10);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 10));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(long value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes the complete long JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(long value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(20);
                Advance(LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref Current, 20), value));
            }
            else
            {
                EnsureCapacity(40);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 40));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(ulong value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(20);
                Advance(LuminPackJsonNumberFormatter.WriteULong(MemoryMarshal.CreateSpan(ref Current, 20), value));
            }
            else
            {
                EnsureCapacity(40);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 40));
                value.TryFormat(charSpan, out int written);
                Advance(written << 1);
            }
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(decimal value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(31);
                Advance(LuminPackJsonNumberFormatter.WriteDecimal(Buffer.Slice(Position), value));
            }
            else
            {
                EnsureCapacity(62);
                var charSpan = MemoryMarshal.Cast<byte, char>(Buffer.Slice(Position, 62));
                value.TryFormat(charSpan, out int written, default, System.Globalization.NumberFormatInfo.InvariantInfo);
                Advance(written << 1);
            }
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(char value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if (value == '"' || value == '\\')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)value);
            }
            else if (value == '\b' || value == '\f' || value == '\n' || value == '\r' || value == '\t')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>(value switch
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
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'u');
                WriteByteRaw<TOptions>(HexChar((value >> 12) & 0xF));
                WriteByteRaw<TOptions>(HexChar((value >> 8) & 0xF));
                WriteByteRaw<TOptions>(HexChar((value >> 4) & 0xF));
                WriteByteRaw<TOptions>(HexChar(value & 0xF));
            }
            else if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                if (value <= 0x7F)
                {
                    WriteByteRaw<TOptions>((byte)value);
                }
                else
                {
                    Span<byte> utf8Buf = stackalloc byte[4];
                    Span<char> charBuf = stackalloc char[1]
                    {
                        value
                    };
                    int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                    WriteRaw(utf8Buf[..len]);
                }
            }
            else
            {
                WriteUtf16CharDirect(value);
            }

            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(bool value)
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                if (value)
                {
                    EnsureCapacity(4);
                    ref byte dst = ref Current;
                    LuminPackWriter.Write(ref dst, 0x65757274u); // "true"
                    Advance(4);
                }
                else
                {
                    EnsureCapacity(5);
                    ref byte dst = ref Current;
                    LuminPackWriter.Write(ref dst, 0x736C6166u); // "fals"
                    Unsafe.Add(ref dst, 4) = (byte)'e';
                    Advance(5);
                }

                return;
            }

            if (BitConverter.IsLittleEndian && TryWriteUtf16Token(
                    value ? 0x0065007500720074UL : 0x0073006C00610066UL, (ushort)'e', value ? 8 : 10))
                return;

            if (value)
            {
                WriteByteRaw<TOptions>((byte)'t');
                WriteByteRaw<TOptions>((byte)'r');
                WriteByteRaw<TOptions>((byte)'u');
                WriteByteRaw<TOptions>((byte)'e');
            }
            else
            {
                WriteByteRaw<TOptions>((byte)'f');
                WriteByteRaw<TOptions>((byte)'a');
                WriteByteRaw<TOptions>((byte)'l');
                WriteByteRaw<TOptions>((byte)'s');
                WriteByteRaw<TOptions>((byte)'e');
            }
        }

        /// <summary>Writes null to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteNull<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteNullInternal<TOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(4);
                ref byte dst = ref Current;
                LuminPackWriter.Write(ref dst, 0x6C6C756Eu); // "null"
                Advance(4);
                return;
            }

            if (BitConverter.IsLittleEndian && TryWriteUtf16Token(0x006C006C0075006EUL, 0, 8))
                return;

            WriteByteRaw<TOptions>((byte)'n');
            WriteByteRaw<TOptions>((byte)'u');
            WriteByteRaw<TOptions>((byte)'l');
            WriteByteRaw<TOptions>((byte)'l');
        }


        /// <summary>Writes a complete fixed UTF-16 token only when its existing buffer window is sufficient.</summary>
        /// <param name = "first">The first four UTF-16 characters in native little-endian storage.</param>
        /// <param name = "last">The optional fifth UTF-16 character.</param>
        /// <param name = "byteCount">The token width, eight or ten bytes at the private call sites.</param>
        /// <returns>Whether the complete token was written without acquiring or growing storage.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16Token(ulong first, ushort last, int byteCount)
        {
#if NET8_0_OR_GREATER
            if ((long)Unsafe.ByteOffset(ref Cursor, ref _end) < byteCount)
#else
            if ((long)unchecked((nint)_end - (nint)Cursor) < byteCount)
#endif
                return false;
            ref byte destination = ref Current;
            LuminPackWriter.Write(ref destination, first);
            if (byteCount == 10)
                LuminPackWriter.Write(ref Unsafe.Add(ref destination, 8), last);
            Advance(byteCount);
            return true;
        }

#endregion

#region String
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
#endregion
    }
}
