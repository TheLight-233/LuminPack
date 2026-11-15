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
    public ref partial struct LuminPackWriter
    {
        /// <summary>Applies the established collection growth threshold to the owning buffer outside the hot comparison.</summary>
        /// <param name = "owner">The owning native output buffer.</param>
        /// <param name = "position">The current absolute output position.</param>
        /// <param name = "index">The next element's actual byte position.</param>
        /// <returns>The cursor fields over the grown storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GrownStorage GrowForThreshold(LuminBufferWriter owner, int position, int index) => Grow(owner, position, index, threshold: true);

        /// <summary>Infers the element type while calculating a contiguous record reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "value">The contiguous element storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The complete reservation width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(scoped Span<T> value, int count, bool header, bool compressed) => Size<T>(count, header, compressed);

        /// <summary>Infers the element type while calculating a contiguous record reservation.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "value">The contiguous element storage.</param>
        /// <param name = "count">The selected count.</param>
        /// <param name = "header">Whether the count is included.</param>
        /// <param name = "compressed">Whether the payload is compressed.</param>
        /// <returns>The complete reservation width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(scoped ReadOnlySpan<T> value, int count, bool header, bool compressed) => Size<T>(count, header, compressed);

        /// <summary>Calculates the complete reservation for one raw or compressed contiguous record.</summary>
        /// <typeparam name = "T">The reference-free element type.</typeparam>
        /// <param name = "count">The element count, or minus one for null.</param>
        /// <param name = "header">Whether the record includes its count.</param>
        /// <param name = "compressed">Whether the payload uses LuminZ compression.</param>
        /// <returns>The complete maximum byte width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Size<T>(int count, bool header, bool compressed)
        {
            if (count < -1)
                LuminPackExceptionHelper.ThrowInvalidCollectionLength(count);
            int prefix = header ? sizeof(int) : 0;
            if (count <= 0)
                return prefix;
            int bytes = checked(count * Unsafe.SizeOf<T>());
            return checked(prefix + (compressed ? sizeof(int) + LuminCompressor.GetMaxCompressedSize(bytes) : bytes));
        }
    }
}
