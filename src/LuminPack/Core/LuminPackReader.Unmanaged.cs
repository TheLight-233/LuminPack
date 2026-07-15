// Auto-generated code

using System;
using System.Runtime.CompilerServices;
using LuminPack.Utility.ViewModel;

namespace LuminPack.Core
{
    public unsafe ref partial struct LuminPackReader
    {
        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }
        
        /// <summary>
        /// 反序列化值类型并前进读取位置
        /// </summary>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>反序列化的值</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // 32位兼容版本
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
            
            Advance(size);
            return value1;
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                value1 = default!;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// 反序列化值类型
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }


        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

        /// <summary>
        /// 反序列化值类型（不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }


        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
                return Unsafe.SizeOf<T1>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
        /// <returns>读取的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }


        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
            }
            else
            {
                // 32位兼容版本
                Unsafe.SkipInit(out value1);
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<T1, byte>(ref value1), ref spanRef, (uint)Unsafe.SizeOf<T1>());
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                view.Deconstruct(out value1, out value2);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                view.Deconstruct(out value1, out value2, out value3);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                view.Deconstruct(out value1, out value2, out value3, out value4);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                value1 = Unsafe.ReadUnaligned<T1>(ref spanRef);
                value2 = Unsafe.ReadUnaligned<T2>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()));
                value3 = Unsafe.ReadUnaligned<T3>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()));
                value4 = Unsafe.ReadUnaligned<T4>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()));
                value5 = Unsafe.ReadUnaligned<T5>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()));
                value6 = Unsafe.ReadUnaligned<T6>(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()));
            }
            else
            {
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

        /// <summary>
        /// 反序列化值类型（无类型约束检查，不返回读取大小）
        /// </summary>
        /// <param name="index">读取起始索引</param>
        /// <param name="value1">输出值 1</param>
        /// <param name="value2">输出值 2</param>
        /// <param name="value3">输出值 3</param>
        /// <param name="value4">输出值 4</param>
        /// <param name="value5">输出值 5</param>
        /// <param name="value6">输出值 6</param>
        /// <param name="value7">输出值 7</param>
        /// <param name="value8">输出值 8</param>
        /// <param name="value9">输出值 9</param>
        /// <param name="value10">输出值 10</param>
        /// <param name="value11">输出值 11</param>
        /// <param name="value12">输出值 12</param>
        /// <param name="value13">输出值 13</param>
        /// <param name="value14">输出值 14</param>
        /// <param name="value15">输出值 15</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <typeparam name="T10">值类型 10</typeparam>
        /// <typeparam name="T11">值类型 11</typeparam>
        /// <typeparam name="T12">值类型 12</typeparam>
        /// <typeparam name="T13">值类型 13</typeparam>
        /// <typeparam name="T14">值类型 14</typeparam>
        /// <typeparam name="T15">值类型 15</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> view = default;
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), ref spanRef, (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                view.Deconstruct(out value1, out value2, out value3, out value4, out value5, out value6, out value7, out value8, out value9, out value10, out value11, out value12, out value13, out value14, out value15);
            }
        }

    }
}
