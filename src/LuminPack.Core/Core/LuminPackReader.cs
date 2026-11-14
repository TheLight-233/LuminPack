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
    /// <summary>Reads complete binary records with an advancing input cursor and reusable storage.</summary>
    [StructLayout(LayoutKind.Sequential)]
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackReader
#else
    public unsafe ref partial struct LuminPackReader
#endif
    {
#if NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public ref byte Cursor;
#endif
#if !NET8_0_OR_GREATER
        /// <summary>The advancing data cursor; caller-owned managed storage must remain pinned on netstandard2.1.</summary>
        public byte* Cursor;
#endif
        private Span<byte> _buffer;
        /// <summary>The first byte after the readable window, cached with every window change.</summary>
#if NET8_0_OR_GREATER
        private ref byte _end;
#else
        private byte* _end;
#endif

        /// <summary>Gets the complete retained storage window used for dynamic bounds and framing patches.</summary>
        /// <remarks>Only the reader replaces its window, so the cached end always describes it.</remarks>
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
        /// <summary>The explicit or materialized reference context, or null before an implicit scope requests state.</summary>
        /// <remarks>Use <see cref="OptionState"/> when reference tracking requires an initialized context.</remarks>
        private LuminPackReaderOptionalState? _options;
        /// <summary>The recursive values currently entered by generated untrusted-input reads.</summary>
        /// <remarks>Each reader starts at zero; only recursive types of untrusted graphs change it.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int InputDepth;
        /// <summary>Gets or sets the operation reference context.</summary>
        public LuminPackReaderOptionalState? Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _options;
            [MethodImpl(MethodImplOptions.AggressiveInlining)] set => _options = value;
        }

        #region Constructors

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        public LuminPackReader(LuminPackReaderOptionalState? option = null)
        {
            _options = option;
            Buffer = default;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        /// <remarks>On netstandard2.1, caller-owned managed span storage must remain pinned for the complete operation lifetime.</remarks>
        public LuminPackReader(ref Span<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _options = option;
            Buffer = bufferReference;
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c>.</summary>
        /// <param name = "bufferReference">The buffer reference used by this operation.</param>
        /// <param name = "option">An explicit shared reference context, or null for a lazily owned independent context.</param>
        /// <remarks>On netstandard2.1, caller-owned managed span storage must remain pinned for the complete operation lifetime.</remarks>
        public LuminPackReader(ref ReadOnlySpan<byte> bufferReference, LuminPackReaderOptionalState? option = null)
        {
            _options = option;
            Buffer = LuminPackMarshal.CreateSpan(ref GetNonNullPinnableReference(bufferReference), bufferReference.Length);
            Position = 0;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReader</c> using the buffer's input and reference context.</summary>
        /// <param name = "bufferWriter">The buffer containing the input data and reader state.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LuminPackReader(LuminBufferWriter bufferWriter)
        {
            _options = bufferWriter.ReaderState;
            Buffer = bufferWriter.GetSpan();
            Position = 0;
        }

        #endregion

        #region Cursor and helpers

        /// <summary>Dereferences the architecture-specific advancing cursor inside shared implementation kernels.</summary>
        private ref byte Current {[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
#if NET8_0_OR_GREATER
            ref Cursor;
#else
            ref Unsafe.AsRef<byte>(Cursor);
#endif
        }

        /// <summary>Copies individual cursor fields into an isolated operation state without taking this state's address.</summary>
        /// <param name = "destination">The state used by an outlined compound operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ref LuminPackReader destination)
        {
            destination._options = _options;
            destination._buffer = _buffer;
#if NET8_0_OR_GREATER
            destination.Cursor = ref Cursor;
            destination._end = ref _end;
#else
            destination.Cursor = Cursor;
            destination._end = _end;
#endif
        }

        /// <summary>Gets the initialized reference context, creating an independent implicit scope on first access.</summary>
        /// <remarks>Initialize this context before copying an uninitialized reader when the copies must share references.</remarks>
        public LuminPackReaderOptionalState OptionState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Options is null)
                    Options = CreateReferenceState();
                return Options;
            }
        }

        /// <summary>Creates reference storage outside the hot cursor operation.</summary>
        /// <returns>An independent reference context owned by the requesting reader.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminPackReaderOptionalState CreateReferenceState() => new LuminPackReaderOptionalState();

        /// <summary>Validates in every build that the requested bytes remain in the input buffer from the cursor.</summary>
        /// <param name = "count">The number of bytes required by the operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureReadable(int count)
        {
            // Signed 64-bit distances reject a cursor past the end and every negative or oversized count on all targets.
#if NET8_0_OR_GREATER
            long room = (long)Unsafe.ByteOffset(ref Cursor, ref _end);
#else
            // Native-width byte offsets avoid Mono's element-size division and retain pointer wrap semantics.
            long room = (long)unchecked((nint)_end - (nint)Cursor);
#endif
            if (room < (long)(uint)count || count < 0)
#if NET8_0_OR_GREATER
                LuminPackExceptionHelper.ThrowReaderSpanOutOfRange(Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Cursor), count);
#else
                LuminPackExceptionHelper.ThrowSpanOutOfRange((long)Position + count);
#endif
        }

        /// <summary>Validates that a range of the readable window exists.</summary>
        /// <param name = "index">The position at which the range starts.</param>
        /// <param name = "count">The number of bytes in the range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureReadable(int index, int count)
        {
            if (IsOutOfRange(index, count, _buffer.Length))
                LuminPackExceptionHelper.ThrowSpanOutOfRange((long)index + count);
        }

        /// <summary>Returns whether <paramref name="count"/> bytes from <paramref name="index"/> leave a buffer of <paramref name="length"/> bytes.</summary>
        /// <param name="index">The starting position, possibly negative or past the end.</param>
        /// <param name="count">The requested byte count, possibly negative.</param>
        /// <param name="length">The nonnegative buffer length.</param>
        /// <returns>Whether the range is invalid.</returns>
        /// <remarks>On 64-bit targets the two unsigned operands cannot overflow their 64-bit sum, so one comparison
        /// rejects exactly the ranges the two-comparison form rejects: a negative or past-the-end index, a negative
        /// count, and any range ending after the buffer.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsOutOfRange(int index, int count, int length) =>
            Unsafe.SizeOf<IntPtr>() == 8
                ? (ulong)(uint)index + (uint)count > (uint)length
                : (uint)index > (uint)length || (uint)count > (uint)(length - index);

        /// <summary>
        /// Advances the target pointer.
        /// </summary>
        /// <param name = "count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            EnsureReadable(count);
#if NET8_0_OR_GREATER
            Cursor = ref Unsafe.Add(ref Cursor, (nint)(uint)count);
#else
            Cursor += count;
#endif
        }

        /// <summary>
        /// Replaces the current buffer span.
        /// </summary>
        /// <param name = "span"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSpan(ref Span<byte> span)
        {
            int position = Position;
            Buffer = span;
            Position = position;
        }

        /// <summary>
        /// Releases the input span and resets its cursor without modifying caller memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Flush()
        {
            Buffer = null;
            Position = 0;
        }

        /// <summary>
        /// Returns the span currently held by the reader.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan()
        {
            return _buffer;
        }

        /// <summary>Examines the current object marker without advancing.</summary>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullObject()
        {
            // At the end of the input no marker byte may be loaded; the following checked read rejects the missing record.
            return (uint)Position < (uint)_buffer.Length && Current == LuminPackCode.NullObject;
        }

        /// <summary>Gets or replaces the absolute position for framing and random-access implementation kernels.</summary>
        public int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => (int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Cursor);
#else
            get => (int)unchecked((nint)Cursor - (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_buffer)));
#endif
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if NET8_0_OR_GREATER
                Cursor = ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)value);
#else
                Cursor = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(_buffer)) + value;
#endif
            }
        }

        #endregion

}
}
