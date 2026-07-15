// Auto-generated code

using System;
using System.Runtime.CompilerServices;
using LuminPack.Utility.ViewModel;

namespace LuminPack.Core
{
    public unsafe ref partial struct LuminPackWriter
    {

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                return Unsafe.SizeOf<T1>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <typeparam name="T1">值类型 1</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                return Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>();
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
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
                // 64位优化版本
                Unsafe.WriteUnaligned(ref spanRef, value1);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>()), value2);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>()), value3);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>()), value4);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>()), value5);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref spanRef, Unsafe.SizeOf<T1>() + Unsafe.SizeOf<T2>() + Unsafe.SizeOf<T3>() + Unsafe.SizeOf<T4>() + Unsafe.SizeOf<T5>()), value6);
            }
            else
            {
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <typeparam name="T1">值类型 1</typeparam>
        /// <typeparam name="T2">值类型 2</typeparam>
        /// <typeparam name="T3">值类型 3</typeparam>
        /// <typeparam name="T4">值类型 4</typeparam>
        /// <typeparam name="T5">值类型 5</typeparam>
        /// <typeparam name="T6">值类型 6</typeparam>
        /// <typeparam name="T7">值类型 7</typeparam>
        /// <typeparam name="T8">值类型 8</typeparam>
        /// <typeparam name="T9">值类型 9</typeparam>
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)_currentIndex);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)_currentIndex);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// 序列化值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

        /// <summary>
        /// 序列化值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        /// <returns>写入的字节数</returns>
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
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
                return Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>();
            }
        }
        
        /// <summary>
        /// 序列化已知值类型（无类型约束检查）
        /// </summary>
        /// <param name="index">写入起始索引</param>
        /// <param name="value1">值 1</param>
        /// <param name="value2">值 2</param>
        /// <param name="value3">值 3</param>
        /// <param name="value4">值 4</param>
        /// <param name="value5">值 5</param>
        /// <param name="value6">值 6</param>
        /// <param name="value7">值 7</param>
        /// <param name="value8">值 8</param>
        /// <param name="value9">值 9</param>
        /// <param name="value10">值 10</param>
        /// <param name="value11">值 11</param>
        /// <param name="value12">值 12</param>
        /// <param name="value13">值 13</param>
        /// <param name="value14">值 14</param>
        /// <param name="value15">值 15</param>
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
        public void DangerousWriteUnmanagedWithoutSizeReturn<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(ref int index, in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        {
#if NET8_0_OR_GREATER
            ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
            
            if (Environment.Is64BitProcess)
            {
                // 64位优化版本
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
                // 32位兼容版本
                var view = UnmanagedViewModel.Create(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
                Unsafe.CopyBlockUnaligned(ref spanRef, ref Unsafe.As<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>, byte>(ref view), (uint)Unsafe.SizeOf<UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>>());
            }
        }

     
    }
}
