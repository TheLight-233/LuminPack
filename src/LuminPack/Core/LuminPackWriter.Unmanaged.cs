// Auto-generated code

using System.Runtime.CompilerServices;
using LuminPack.Utility.ViewModel;

namespace LuminPack.Core
{
    /// <summary>Writes LuminPack binary values to a caller-supplied or reusable buffer.</summary>
    public unsafe ref partial struct LuminPackWriter
    {

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1>(in T1 value1)
            where T1 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1>(in T1 value1)
            where T1 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1>(in T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1>(in T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1>(ref int index, in T1 value1)
            where T1 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1>(ref int index, in T1 value1)
            where T1 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1>(ref int index, in T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1>(ref int index, in T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2>(in T1 value1, in T2 value2)
            where T1 : unmanaged where T2 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2>(in T1 value1, in T2 value2)
            where T1 : unmanaged where T2 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2>(in T1 value1, in T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2>(in T1 value1, in T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2>(ref int index, in T1 value1, in T2 value2)
            where T1 : unmanaged where T2 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2>(ref int index, in T1 value1, in T2 value2)
            where T1 : unmanaged where T2 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2>(ref int index, in T1 value1, in T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2>(ref int index, in T1 value1, in T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3>(in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3>(in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3>(in T1 value1, in T2 value2, in T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3>(in T1 value1, in T2 value2, in T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, in T1 value1, in T2 value2, in T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4>(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4>(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged where T15 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged where T15 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// SerializeUnmanaged type
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged where T15 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Writes unmanaged values.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged where T15 : unmanaged
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// Writes a value without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        /// <returns>The number of bytes written.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousWriteUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Writes values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial write offset.</param>
        /// <param name="value1">Value 1</param>
        /// <param name="value2">Value 2</param>
        /// <param name="value3">Value 3</param>
        /// <param name="value4">Value 4</param>
        /// <param name="value5">Value 5</param>
        /// <param name="value6">Value 6</param>
        /// <param name="value7">Value 7</param>
        /// <param name="value8">Value 8</param>
        /// <param name="value9">Value 9</param>
        /// <param name="value10">Value 10</param>
        /// <param name="value11">Value 11</param>
        /// <param name="value12">Value 12</param>
        /// <param name="value13">Value 13</param>
        /// <param name="value14">Value 14</param>
        /// <param name="value15">Value 15</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <typeparam name="T10">Unmanaged type 10</typeparam>
        /// <typeparam name="T11">Unmanaged type 11</typeparam>
        /// <typeparam name="T12">Unmanaged type 12</typeparam>
        /// <typeparam name="T13">Unmanaged type 13</typeparam>
        /// <typeparam name="T14">Unmanaged type 14</typeparam>
        /// <typeparam name="T15">Unmanaged type 15</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()), value7);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()), value8);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()), value9);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()), value10);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()), value11);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()), value12);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()), value13);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()), value14);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()), value15);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }


    }
}
