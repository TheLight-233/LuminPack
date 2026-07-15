
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Core
{
    public unsafe ref partial struct LuminPackWriter
    {
        /// <summary>
        /// Compresses <paramref name="rawBytes"/> into a rented temp buffer and writes
        /// [int32: element_count][int32: compressedLen][compressed_data] at <paramref name="dest"/>.
        /// Returns compressedLen.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteCompressedBlock(
            ref byte dest,
            ReadOnlySpan<byte> rawBytes,
            int elementCount)
        {
            int maxCompLen = LuminCompressor.GetMaxCompressedSize(rawBytes.Length);
            byte[] tempBuf  = ArrayPool<byte>.Shared.Rent(maxCompLen);
            try
            {
                int compressedLen = LuminCompressor.Compress(rawBytes, tempBuf.AsSpan(0, maxCompLen));

                Unsafe.WriteUnaligned(ref dest, elementCount);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dest, 4), compressedLen);
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 8), ref tempBuf[0], (uint)compressedLen);

                return compressedLen;
            }
            finally { ArrayPool<byte>.Shared.Return(tempBuf); }
        }

        /// <summary>
        /// Compresses <paramref name="rawBytes"/> into a rented temp buffer and writes
        /// [int32: compressedLen][compressed_data] at <paramref name="dest"/> (no element_count).
        /// Returns compressedLen.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteCompressedBlockWithoutHeader(
            ref byte dest,
            ReadOnlySpan<byte> rawBytes)
        {
            int maxCompLen = LuminCompressor.GetMaxCompressedSize(rawBytes.Length);
            byte[] tempBuf  = ArrayPool<byte>.Shared.Rent(maxCompLen);
            try
            {
                int compressedLen = LuminCompressor.Compress(rawBytes, tempBuf.AsSpan(0, maxCompLen));

                Unsafe.WriteUnaligned(ref dest, compressedLen);
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref tempBuf[0], (uint)compressedLen);

                return compressedLen;
            }
            finally { ArrayPool<byte>.Shared.Return(tempBuf); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithCompress<T>(scoped ref T[]? array)
        {
            var index = _currentIndex;

            if (array == null || array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, array.Length);
#else
            WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, array.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref T[]? array)
        {
            var index = _currentIndex;

            if (array == null || array.Length == 0)
                return;

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithCompress<T>(scoped ref int index, T[]? array)
        {
            if (array == null || array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, array.Length);
#else
            WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, array.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref int index, T[]? array)
        {
            if (array == null || array.Length == 0)
                return;

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithCompress<T>(scoped ref int index, T[]? array, out int spanOffset)
        {
            if (array == null || array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, array.Length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, array.Length);
#endif
            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref int index, T[]? array, out int spanOffset)
        {
            if (array == null || array.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithCompress<T>(scoped ref int index, T[]? array, int length, out int spanOffset)
        {
            if (array == null || array.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, length);
#endif
            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedArrayWithOutHeaderWithCompress<T>(scoped ref int index, T[]? array, int length, out int spanOffset)
        {
            if (array == null || array.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref LuminPackMarshal.GetArrayDataReference(array),
                Unsafe.SizeOf<T>() * array.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped Span<T> span)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, span.Length);
#else
            WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, span.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, span.Length);
#else
            WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, span.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped Span<T> span)
        {
            if (span.Length == 0)
                return;

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span)
        {
            if (span.Length == 0)
                return;

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, span.Length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, span.Length);
#endif
            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, span.Length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, span.Length);
#endif
            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped Span<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, length);
#endif
            spanOffset = 8 + compressedLen;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                WriteCollectionHeader(ref index, 0);
                spanOffset = 4;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes, length);
#else
            int compressedLen = WriteCompressedBlock(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes, length);
#endif
            spanOffset = 8 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped Span<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref span.GetPinnableReference()),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedSpanWithOutHeaderWithCompress<T>(scoped ref int index, scoped ReadOnlySpan<T> span, int length, out int spanOffset)
        {
            if (span.Length == 0)
            {
                spanOffset = 0;
                return;
            }

            var rawBytes = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                Unsafe.SizeOf<T>() * span.Length);

#if NET8_0_OR_GREATER
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index), rawBytes);
#else
            int compressedLen = WriteCompressedBlockWithoutHeader(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index), rawBytes);
#endif
            spanOffset = 4 + compressedLen;
        }
    }
}