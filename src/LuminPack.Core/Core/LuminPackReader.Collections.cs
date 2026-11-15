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
        /// <summary>Validates a generated fixed-width collection's complete dynamic payload in every build.</summary>
        /// <param name = "index">The absolute beginning of the collection header or payload.</param>
        /// <param name = "byteLength">The checked number of bytes required by the collection boundary.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCollectionPayload(int index, int byteLength) => ValidateCollectionPayload(index, byteLength);

        /// <summary>Validates a raw collection copy before accessing its complete payload in every build.</summary>
        /// <param name = "index">The first payload byte after the collection header.</param>
        /// <param name = "byteLength">The checked number of payload bytes to copy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateCollectionPayload(int index, int byteLength)
        {
            if (IsOutOfRange(index, byteLength, _buffer.Length))
                ThrowPayloadOutOfRange(index, byteLength);
        }

        /// <summary>Reports a payload that does not fit the input, without overflowing for a forged length.</summary>
        /// <param name = "index">The first payload byte.</param>
        /// <param name = "byteLength">The requested payload length.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowPayloadOutOfRange(int index, int byteLength)
        {
            long end = (long)index + byteLength;
            LuminPackExceptionHelper.ThrowSpanOutOfRange(end > int.MaxValue ? int.MaxValue : end < 0 ? 0 : (int)end);
        }

        /// <summary>Examines the current collection marker without advancing.</summary>
        /// <returns>Whether the header represents a nonnull value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekIsNullCollection()
        {
            EnsureReadable(Position, sizeof(int));
            return LuminPackReader.Read<int>(ref Current) == LuminPackCode.NullCollection;
        }

        /// <summary>
        /// Array-returning variant for generated reference-element loops. Keeping the backing
        /// array as one managed reference shortens the live range compared with Span&lt;T&gt;.
        /// </summary>
        /// <param name = "count">The number of entries involved in the operation.</param>
        /// <param name = "value">Receives the value produced by the operation.</param>
        /// <typeparam name = "T">The type represented by <c>T</c>.</typeparam>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T? [] CreateFreshListArray<T>(int count, out List<T?> value)
        {
            value = new List<T?>(count);
            return LuminPackMarshal.DangerousGetFreshListArray(value, count);
        }

        /// <summary>Bounds a presizing hint for a collection whose elements occupy at least one input byte each.</summary>
        /// <param name = "count">The nonnegative count read from the collection header.</param>
        /// <returns>The count, limited by the remaining input so a forged header cannot force a large allocation.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCapacityHint(int count)
        {
            return Math.Min(count, RemainingWindowBytes);
        }
    }
}
