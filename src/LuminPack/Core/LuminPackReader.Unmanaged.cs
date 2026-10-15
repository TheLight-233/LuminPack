// Auto-generated code

using System.Runtime.CompilerServices;
using LuminPack.Utility.ViewModel;

namespace LuminPack.Core
{
    /// <summary>Reads LuminPack binary values while tracking the current input position and operation state.</summary>
    public unsafe ref partial struct LuminPackReader
    {
        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1>(out T1 value1)
            where T1 : unmanaged
        {
            var index = _currentIndex;

#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// Reads an unmanaged value and advances the input position.
        /// </summary>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The deserialized value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T1 ReadUnmanaged<T1>()
            where T1 : unmanaged
        {
            var size = Unsafe.SizeOf<T1>();
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            T1 value1;
            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }

            Advance(size);
            return value1;
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1>(ref int index, out T1 value1)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2>(out T1 value1, out T2 value2)
            where T1 : unmanaged where T2 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2>(ref int index, out T1 value1, out T2 value2)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3>(out T1 value1, out T2 value2, out T3 value3)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4>(out T1 value1, out T2 value2, out T3 value3, out T4 value4)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
            where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged where T7 : unmanaged where T8 : unmanaged where T9 : unmanaged where T10 : unmanaged where T11 : unmanaged where T12 : unmanaged where T13 : unmanaged where T14 : unmanaged where T15 : unmanaged
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Reads unmanaged values.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
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
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }


        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1>(out T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1>(ref int index, out T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2>(out T1 value1, out T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2>(ref int index, out T1 value1, out T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3>(out T1 value1, out T2 value2, out T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

        /// <summary>
        /// Reads unmanaged values without returning the byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        public void ReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }


        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1>(out T1 value1)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1>(ref int index, out T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2>(out T1 value1, out T2 value2)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2>(ref int index, out T1 value1, out T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3>(out T1 value1, out T2 value2, out T3 value3)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4>(out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        /// <typeparam name="T9">Unmanaged type 9</typeparam>
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
            var index = _currentIndex;
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        /// <returns>The number of bytes read.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousReadUnmanaged<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>() + Unsafe.SizeOf<T15>();
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }


        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1>(out T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1>(ref int index, out T1 value1)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // Compatibility path for 32-bit processes.
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2>(out T1 value1, out T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2>(ref int index, out T1 value1, out T2 value2)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3>(out T1 value1, out T2 value2, out T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3>(ref int index, out T1 value1, out T2 value2, out T3 value3)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <typeparam name="T1">Unmanaged type 1</typeparam>
        /// <typeparam name="T2">Unmanaged type 2</typeparam>
        /// <typeparam name="T3">Unmanaged type 3</typeparam>
        /// <typeparam name="T4">Unmanaged type 4</typeparam>
        /// <typeparam name="T5">Unmanaged type 5</typeparam>
        /// <typeparam name="T6">Unmanaged type 6</typeparam>
        /// <typeparam name="T7">Unmanaged type 7</typeparam>
        /// <typeparam name="T8">Unmanaged type 8</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

        /// <summary>
        /// Reads values without an unmanaged type constraint or a returned byte count.
        /// </summary>
        /// <param name="index">The initial read offset.</param>
        /// <param name="value1">Output value 1</param>
        /// <param name="value2">Output value 2</param>
        /// <param name="value3">Output value 3</param>
        /// <param name="value4">Output value 4</param>
        /// <param name="value5">Output value 5</param>
        /// <param name="value6">Output value 6</param>
        /// <param name="value7">Output value 7</param>
        /// <param name="value8">Output value 8</param>
        /// <param name="value9">Output value 9</param>
        /// <param name="value10">Output value 10</param>
        /// <param name="value11">Output value 11</param>
        /// <param name="value12">Output value 12</param>
        /// <param name="value13">Output value 13</param>
        /// <param name="value14">Output value 14</param>
        /// <param name="value15">Output value 15</param>
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
        public void DangerousReadUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, out T1 value1, out T2 value2, out T3 value3, out T4 value4, out T5 value5, out T6 value6, out T7 value7, out T8 value8, out T9 value9, out T10 value10, out T11 value11, out T12 value12, out T13 value13, out T14 value14, out T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif

            if (Environment.Is64BitProcess)
            {
                // Optimized path for 64-bit processes.
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
                value7 = Unsafe.ReadUnaligned<T7>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>()));
                value8 = Unsafe.ReadUnaligned<T8>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>()));
                value9 = Unsafe.ReadUnaligned<T9>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>()));
                value10 = Unsafe.ReadUnaligned<T10>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>()));
                value11 = Unsafe.ReadUnaligned<T11>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>()));
                value12 = Unsafe.ReadUnaligned<T12>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>()));
                value13 = Unsafe.ReadUnaligned<T13>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>()));
                value14 = Unsafe.ReadUnaligned<T14>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>()));
                value15 = Unsafe.ReadUnaligned<T15>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>() + Unsafe.SizeOf<T6>() + Unsafe.SizeOf<T7>() + Unsafe.SizeOf<T8>() + Unsafe.SizeOf<T9>() + Unsafe.SizeOf<T10>() + Unsafe.SizeOf<T11>() + Unsafe.SizeOf<T12>() + Unsafe.SizeOf<T13>() + Unsafe.SizeOf<T14>()));
            }
            else
            {
                // Compatibility path for 32-bit processes.
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

    }
}
