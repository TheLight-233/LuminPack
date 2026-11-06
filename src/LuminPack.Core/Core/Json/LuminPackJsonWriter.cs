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

    }
}
