using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Utility;

using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core
{
    public unsafe ref partial struct LuminPackReader
    {

        /// <summary>
        /// Reads [int32: element_count] and, when element_count > 0,
        /// [int32: compressedLen] from <paramref name="index"/>.
        /// Returns <c>false</c> when the collection is null
        /// (<see cref="LuminPackCode.NullCollection"/>); <paramref name="elementCount"/>
        /// is set to 0 and <paramref name="compressedLen"/> to 0 in that case.
        /// When <paramref name="elementCount"/> == 0 the collection is empty and
        /// <paramref name="compressedLen"/> is 0 — caller should short-circuit to
        /// <c>Array.Empty&lt;T&gt;()</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCompressedCollectionHead(int index, out int elementCount, out int compressedLen)
        {
            elementCount = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            if (elementCount == LuminPackCode.NullCollection)
            {
                elementCount  = 0;
                compressedLen = 0;
                return false; // null
            }

            if (elementCount == 0)
            {
                compressedLen = 0;
                return true; // empty
            }

            compressedLen = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index + 4));
            return true;
        }

        /// <summary>
        /// Decompresses <paramref name="compressedLen"/> bytes from the internal buffer
        /// starting at <paramref name="compressedDataOffset"/> directly into
        /// <paramref name="destRef"/> / <paramref name="decompressedByteLen"/> bytes.
        /// No intermediate allocation — LuminZ decompresses in a single pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecompressFromBuffer(int compressedDataOffset, int compressedLen, ref byte destRef, int decompressedByteLen)
        {
            var src = MemoryMarshal.CreateReadOnlySpan(ref GetSpanReference(compressedDataOffset), compressedLen);
            var dst = MemoryMarshal.CreateSpan(ref destRef, decompressedByteLen);
            LuminCompressor.Decompress(src, dst);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArrayWithCompress<T>(scoped ref T[]? array)
        {
            var index = _currentIndex;

            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                array = null;
                return;
            }

            if (elementCount == 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != elementCount)
                array = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref LuminPackMarshal.GetArrayDataReference(array),
                elementCount * Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArrayWithCompress<T>(scoped ref int index, ref T[]? array)
        {
            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                array = null;
                return;
            }

            if (elementCount == 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != elementCount)
                array = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref LuminPackMarshal.GetArrayDataReference(array),
                elementCount * Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArrayWithCompress<T>(scoped ref int index, ref T[]? array, out int spanOffset)
        {
            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                array     = null;
                spanOffset = 4;
                return;
            }

            if (elementCount == 0)
            {
                array      = Array.Empty<T>();
                spanOffset = 4;
                return;
            }

            if (array is null || array.Length != elementCount)
                array = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref LuminPackMarshal.GetArrayDataReference(array),
                elementCount * Unsafe.SizeOf<T>());

            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref int index, ref T[]? array, int length)
        {
            if (length == LuminPackCode.NullCollection)
            {
                array = null;
                return;
            }

            if (length == 0)
            {
                array = Array.Empty<T>();
                return;
            }

            if (array is null || array.Length != length)
                array = AllocateUninitializedArray<T>(length);

            var compressedLen = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            DecompressFromBuffer(
                index + 4,
                compressedLen,
                ref LuminPackMarshal.GetArrayDataReference(array),
                length * Unsafe.SizeOf<T>());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref int index, ref T[]? array, int length, out int spanOffset)
        {
            if (length == LuminPackCode.NullCollection)
            {
                array      = null;
                spanOffset = 0;
                return;
            }

            if (length == 0)
            {
                array      = Array.Empty<T>();
                spanOffset = 0;
                return;
            }

            if (array is null || array.Length != length)
                array = AllocateUninitializedArray<T>(length);

            var compressedLen = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            DecompressFromBuffer(
                index + 4,
                compressedLen,
                ref LuminPackMarshal.GetArrayDataReference(array),
                length * Unsafe.SizeOf<T>());

            spanOffset = 4 + compressedLen;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpanWithCompress<T>(scoped ref Span<T> span)
        {
            var index = _currentIndex;

            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                span = default;
                return;
            }

            if (elementCount == 0)
            {
                span = Span<T>.Empty;
                return;
            }

            if (span.Length != elementCount)
                span = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                elementCount * Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpanWithCompress<T>(scoped ref int index, scoped ref Span<T> span)
        {
            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                span = default;
                return;
            }

            if (elementCount == 0)
            {
                span = Span<T>.Empty;
                return;
            }

            if (span.Length != elementCount)
                span = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                elementCount * Unsafe.SizeOf<T>());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpanWithCompress<T>(scoped ref int index, scoped ref Span<T> span, out int spanOffset)
        {
            if (!TryReadCompressedCollectionHead(index, out var elementCount, out var compressedLen))
            {
                span       = default;
                spanOffset = 4;
                return;
            }

            if (elementCount == 0)
            {
                span       = Span<T>.Empty;
                spanOffset = 4;
                return;
            }

            if (span.Length != elementCount)
                span = AllocateUninitializedArray<T>(elementCount);

            DecompressFromBuffer(
                index + 8,
                compressedLen,
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                elementCount * Unsafe.SizeOf<T>());

            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped ref Span<T> span, int length)
        {
            if (length == LuminPackCode.NullCollection)
            {
                span = default;
                return;
            }

            if (length == 0)
            {
                span = Span<T>.Empty;
                return;
            }

            if (span.Length != length)
                span = AllocateUninitializedArray<T>(length);

            var compressedLen = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            DecompressFromBuffer(
                index + 4,
                compressedLen,
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                length * Unsafe.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped ref Span<T> span, int length, out int spanOffset)
        {
            if (length == LuminPackCode.NullCollection)
            {
                span       = default;
                spanOffset = 0;
                return;
            }

            if (length == 0)
            {
                span       = Span<T>.Empty;
                spanOffset = 0;
                return;
            }

            if (span.Length != length)
                span = AllocateUninitializedArray<T>(length);

            var compressedLen = Unsafe.ReadUnaligned<int>(ref GetSpanReference(index));

            DecompressFromBuffer(
                index + 4,
                compressedLen,
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                length * Unsafe.SizeOf<T>());

            spanOffset = 4 + compressedLen;
        }
    }
}