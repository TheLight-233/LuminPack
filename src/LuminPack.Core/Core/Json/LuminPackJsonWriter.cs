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
#if NET8_0_OR_GREATER
    public ref struct LuminPackJsonWriter
#else
    public unsafe ref struct LuminPackJsonWriter
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
        public readonly LuminPackWriterOptionalState Options;
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
            Options = state;
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
            Options = state;
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
            Options = bufferWriter.WriterState;
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
            Options = state;
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
            get => (int)(Cursor - (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(Buffer)));
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
        public LuminPackWriterOptionalState OptionState => Options;
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
            if ((long)(_end - Cursor) >= (long)(uint)count && count >= 0)
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
            return ReserveGeneratedIntegerPrefix<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, maxDigits);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (!(default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                LuminPackExceptionHelper.ThrowInvalidOperationException("Generated integer prefixes require UTF-8");
            if ((uint)(byteCount - 8) > 8)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
            // Reserve before obtaining either reference: growth/streaming may replace the span and reset the cursor. The
            // reservation also rejects a cursor past the window. Advance only after formatting.
            EnsureCapacity(byteCount + maxDigits);
            ref byte destination = ref Current;
            global::LuminPack.Core.LuminPackWriter.Write(ref destination, first);
            global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(byteCount - 8)), last);
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
            WriteByteRaw<global::LuminPack.Option.LuminPackDefaultOptions>(value);
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
            global::LuminPack.Core.LuminPackWriter.Write(ref destination, first);
            global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, (nint)(uint)(byteCount - 8)), last);
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
            WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, uint value)
        {
            WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, long value)
        {
            WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
        /// <param name = "first">The first used by this operation.</param>
        /// <param name = "last">The last used by this operation.</param>
        /// <param name = "byteCount">The number of bytes or elements to process.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGeneratedInteger(ulong first, ulong last, int byteCount, ulong value)
        {
            WriteGeneratedInteger<global::LuminPack.Option.LuminPackDefaultOptions>(first, last, byteCount, value);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded()
        {
            WriteCommaIfNeeded<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes object start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            WriteObjectStart<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes object end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            WriteObjectEnd<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes array start to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            WriteArrayStart<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes array end to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            WriteArrayEnd<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
        {
            WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf8PropertyName);
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName(ReadOnlySpan<char> utf16PropertyName)
        {
            WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(utf16PropertyName);
        }

        /// <summary>
        /// Emits an already formatted, escape-free ASCII payload as a JSON string.
        /// The ISO-8601, GUID and TimeSpan formats used below never contain a
        /// character that requires JSON escaping, so the content writers only
        /// need to transcode or copy.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue(scoped ReadOnlySpan<char> text)
        {
            WriteFormattedValue<global::LuminPack.Option.LuminPackDefaultOptions>(text);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTime value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(DateTimeOffset value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(Guid value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(TimeSpan value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

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
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes double to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(double value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(int value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u int to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(uint value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(byte value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes s byte to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(sbyte value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(short value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u short to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ushort value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(long value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes u long to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(ulong value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes decimal to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(decimal value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes char to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        [SkipLocalsInit]
#endif
        public void Write(char value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes bool to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(bool value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes null to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WriteNull<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal()
        {
            WriteNullInternal<global::LuminPack.Option.LuminPackDefaultOptions>();
        }

        /// <summary>Writes byte raw to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteByteRaw<TOptions>(byte value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(1);
                Current = value;
                Advance(1);
            }
            else
            {
                EnsureCapacity(2);
                ref byte dst = ref Current;
                global::LuminPack.Core.LuminPackWriter.Write(ref dst, (char)value);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
            int written = LuminPackJsonNumberFormatter.WriteULong(number, value);
            Advance(byteCount + written);
        }

        /// <summary>Writes comma if needed to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteCommaIfNeeded<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteByteRaw<TOptions>(CloseBrace);
            Depth--;
            IsFirstElement = false;
        }

        /// <summary>Writes array start to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                WriteStringContentUtf8Raw<TOptions>(utf8PropertyName);
            else
                WriteStringContentUtf16<TOptions>(Encoding.UTF8.GetString(utf8PropertyName).AsSpan());
            WriteByteRaw<TOptions>(Quote);
            WriteByteRaw<TOptions>(Colon);
            IsFirstElement = true;
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(ReadOnlySpan<char> utf16PropertyName)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
        /// The ISO-8601, GUID and TimeSpan formats used below never contain a
        /// character that requires JSON escaping, so the content writers only
        /// need to transcode or copy.
        /// </summary>
        /// <param name = "text">The text used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteFormattedValue<TOptions>(scoped ReadOnlySpan<char> text)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                WriteStringContentUtf8<TOptions>(text);
            else
                WriteStringContentUtf16<TOptions>(text);
            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(DateTime value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            Span<char> buffer = stackalloc char[36];
            if (value.TryFormat(buffer, out int written))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

        /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(TimeSpan value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            // "-10675199.02:48:05.4775808" is 26 characters.
            Span<char> buffer = stackalloc char[32];
            if (value.TryFormat(buffer, out int written, "c"))
            {
                WriteFormattedValue<TOptions>(buffer.Slice(0, written));
                return;
            }

            Write<TOptions>(value.ToString());
        }

        /// <summary>Writes float to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Write<TOptions>(float value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            EnsureCapacity((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 24 : 64);
            if (float.IsNaN(value) || float.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            EnsureCapacity((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 32 : 64);
            if (double.IsNaN(value) || double.IsInfinity(value))
                LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes the complete int JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(int value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteIntegerValue<TOptions>(value);
        }

        /// <summary>Writes the complete long JSON value for scalar and fused collection callers.</summary>
        /// <param name = "value">The integer to format.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteIntegerValue<TOptions>(long value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            else if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                if (value)
                {
                    EnsureCapacity(4);
                    ref byte dst = ref Current;
                    global::LuminPack.Core.LuminPackWriter.Write(ref dst, 0x65757274u); // "true"
                    Advance(4);
                }
                else
                {
                    EnsureCapacity(5);
                    ref byte dst = ref Current;
                    global::LuminPack.Core.LuminPackWriter.Write(ref dst, 0x736C6166u); // "fals"
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteNullInternal<TOptions>();
        }

        /// <summary>Writes null internal to the current output.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteNullInternal<TOptions>()
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                EnsureCapacity(4);
                ref byte dst = ref Current;
                global::LuminPack.Core.LuminPackWriter.Write(ref dst, 0x6C6C756Eu); // "null"
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
            if ((long)(_end - Cursor) < byteCount)
#endif
                return false;
            ref byte destination = ref Current;
            global::LuminPack.Core.LuminPackWriter.Write(ref destination, first);
            if (byteCount == 10)
                global::LuminPack.Core.LuminPackWriter.Write(ref Unsafe.Add(ref destination, 8), last);
            Advance(byteCount);
            return true;
        }

#endregion

#region String
        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "propertyName">The property name used by this operation.</param>
        public void WritePropertyName(string propertyName)
        {
            WritePropertyName<global::LuminPack.Option.LuminPackDefaultOptions>(propertyName);
        }

        /// <summary>Writes string to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        public void Write(string? value)
        {
            Write<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf8<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Encoded(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf8Encoded<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

#endif
#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 escaped to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <param name = "escapeIndex">The escape index used by this operation.</param>
        private void WriteStringContentUtf8Escaped(scoped ReadOnlySpan<char> value, int escapeIndex)
        {
            WriteStringContentUtf8Escaped<global::LuminPack.Option.LuminPackDefaultOptions>(value, escapeIndex);
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
                while (!value.IsEmpty)
                {
                    var result = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(value, Buffer.Slice(Position), out int charsRead, out int written);
                    Advance(written);
                    value = value.Slice(charsRead);
                    if (result == OperationStatus.Done)
                        return;
                    if (result != OperationStatus.DestinationTooSmall)
                        LuminPackExceptionHelper.ThrowFailedEncoding(result);
                    FlushToBufferWriter();
                    EnsureCapacity(4);
                }

                return;
            }

            // Try the actual available space first, including exact-size caller spans.
            var status = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(value, Buffer.Slice(Position), out _, out int bytesWritten);
            if (status != OperationStatus.Done)
                bytesWritten = WriteUtf8StringSegmentSlow(value);
            Advance(bytesWritten);
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
                if (!BitConverter.IsLittleEndian) packed = global::System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(packed);
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
            WriteStringContentUtf8Raw<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf8 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf8Slow(ReadOnlySpan<byte> value)
        {
            WriteStringContentUtf8Slow<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes string content utf16 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf16(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf16<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes an already-fitting common escape pair without changing cold growth or external output.</summary>
        /// <param name="value">The original character.</param><returns>Whether the complete pair was written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryWriteUtf16EscapePair(char value)
        {
            char escaped = value switch { '"' => '"', '\\' => '\\', '\n' => 'n', '\r' => 'r', '\t' => 't', _ => '\0' };
            if (escaped == '\0' || Output is not null || !BitConverter.IsLittleEndian) return false;
#if NET8_0_OR_GREATER
            if ((long)Unsafe.ByteOffset(ref Cursor, ref _end) < 4) return false;
#else
            if ((long)(_end - Cursor) < 4) return false;
#endif
            Unsafe.WriteUnaligned(ref Current, (uint)'\\' | ((uint)escaped << 16));
            Advance(4);
            return true;
        }

        /// <summary>Writes string content utf16 slow to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        private void WriteStringContentUtf16Slow(scoped ReadOnlySpan<char> value)
        {
            WriteStringContentUtf16Slow<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes utf16 char direct to the current output.</summary>
        /// <param name = "c">The c used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUtf16CharDirect(char c)
        {
            EnsureCapacity(2);
            ref byte dst = ref Current;
            global::LuminPack.Core.LuminPackWriter.Write(ref dst, c);
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
            WriteStringContentSlow<global::LuminPack.Option.LuminPackDefaultOptions>(value);
        }

        /// <summary>Writes property name to the current output.</summary>
        /// <param name = "propertyName">The property name used by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WritePropertyName<TOptions>(string propertyName)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            WriteByteRaw<TOptions>(Quote);
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            WriteCommaIfNeeded<TOptions>();
            if (value == null)
            {
                WriteNullInternal<TOptions>();
                return;
            }


            // Reserve the quotes and content together. The transcoder only writes
            // after validating the complete value, so misses retain the normal path.
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) && (uint)(value.Length - 4) <= 28 && global::LuminPack.Internal.ShortAsciiTranscoder.IsSupported)
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
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                WriteStringContentUtf8<TOptions>(value.AsSpan());
            }
            else
            {
                WriteStringContentUtf16<TOptions>(value.AsSpan());
            }

            WriteByteRaw<TOptions>(Quote);
        }

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(string? value, bool separate) => WriteElement<global::LuminPack.Option.LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(int value, bool separate) => WriteElement<global::LuminPack.Option.LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteElement</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        /// <param name = "separate">Whether a preceding element requires a comma.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(long value, bool separate) => WriteElement<global::LuminPack.Option.LuminPackDefaultOptions>(value, separate);

        /// <summary>Applies <c>WriteEntryKey</c> with the library default profile.</summary>
        /// <param name = "key">The entry key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey(string? key) => WriteEntryKey<global::LuminPack.Option.LuminPackDefaultOptions>(key);

        /// <summary>Applies <c>WriteEntryKey</c> with the library default profile.</summary>
        /// <param name = "key">The entry key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryKey(int key) => WriteEntryKey<global::LuminPack.Option.LuminPackDefaultOptions>(key);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(string? value) => WriteEntryValue<global::LuminPack.Option.LuminPackDefaultOptions>(value);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(int value) => WriteEntryValue<global::LuminPack.Option.LuminPackDefaultOptions>(value);

        /// <summary>Applies <c>WriteEntryValue</c> with the library default profile.</summary>
        /// <param name = "value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue(long value) => WriteEntryValue<global::LuminPack.Option.LuminPackDefaultOptions>(value);

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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8 && value != null &&
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(12);
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 11), value));
                IsFirstElement = false;
                return;
            }

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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(21);
                int skip = separate ? 1 : 0;
                ref byte output = ref Current;
                output = Comma;
                Advance(skip + LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref output, (nint)(uint)skip), 20), value));
                IsFirstElement = false;
                return;
            }

            if (separate)
                WriteByteRaw<TOptions>(Comma);
            IsFirstElement = true;
            WriteIntegerValue<TOptions>(value);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8 && key != null &&
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {

            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8 && value != null &&
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(12);
                ref byte output = ref Current;
                int written = LuminPackJsonNumberFormatter.WriteInt32(MemoryMarshal.CreateSpan(ref output, 11), value);
                Unsafe.Add(ref output, (nint)(uint)written) = CloseBracket;
                Advance(written + 1);
                IsFirstElement = false;
                return;
            }

            WriteIntegerValue<TOptions>(value);
            WriteByteRaw<TOptions>(CloseBracket);
            IsFirstElement = false;
        }

        /// <summary>Writes a dictionary entry's 64-bit integer value and closes its entry array.</summary>
        /// <param name = "value">The entry value.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteEntryValue<TOptions>(long value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
            {
                EnsureCapacity(21);
                ref byte output = ref Current;
                int written = LuminPackJsonNumberFormatter.WriteLong(MemoryMarshal.CreateSpan(ref output, 20), value);
                Unsafe.Add(ref output, (nint)(uint)written) = CloseBracket;
                Advance(written + 1);
                IsFirstElement = false;
                return;
            }

            WriteIntegerValue<TOptions>(value);
            WriteByteRaw<TOptions>(CloseBracket);
            IsFirstElement = false;
        }

        /// <summary>Writes string content utf8 to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringContentUtf8<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
                ref byte dst = ref Current;
                for (int i = 0; i < value.Length; i++)
                {
                    Unsafe.Add(ref dst, (nint)(uint)(i)) = (byte)value[i];
                }

                Advance(value.Length);
            }
            else
            {
                WriteStringContentSlow<TOptions>(value);
            }
#endif
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes string content utf8 encoded to the current output.</summary>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteStringContentUtf8Encoded<TOptions>(scoped ReadOnlySpan<char> value)
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dst, ref MemoryMarshal.GetReference(value), (uint)value.Length);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
                Advance(byteCount);
            }
            else
            {
#if NET8_0_OR_GREATER
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
                            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
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
                global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
        {
            var prefix = value.Slice(0, escapeIndex);
            int byteCount = checked(prefix.Length * 2);
            EnsureCapacity(byteCount);
            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(prefix));
            ref byte dst = ref Current;
            global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref dst, ref src, (uint)byteCount);
            Advance(byteCount);
            WriteStringContentUtf16Slow<TOptions>(value.Slice(escapeIndex));
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
            where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
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
