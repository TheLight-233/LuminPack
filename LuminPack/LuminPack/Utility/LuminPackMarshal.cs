using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;
using LuminPack.Attribute;

namespace LuminPack.Code;

public static class LuminPackMarshal
{
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetListSpan<T>(scoped ref List<T?>? list)
    {
        if (list is null)
        {
            return Span<T?>.Empty;
        }

#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, list.Count);
        return CollectionsMarshal.AsSpan(list);
#else
        SetListSize(ref list, list.Count);
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        return local._items.AsSpan(0, list.Count);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetListSpan<T>(scoped ref List<T?>? list, int length)
    {
        if (list is null)
        {
            return Span<T?>.Empty;
        }

        if (list.Capacity < length)
        {
            length = list.Capacity;
        }
        
        
#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, length);
        return CollectionsMarshal.AsSpan(list).Slice(0, length);
#else
        SetListSize(ref list, length);
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        return local._items.AsSpan(0, length);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetListSize<T>(scoped ref List<T?>? list, int size)
    {
        if (list is null) return;
        
        if (list.Capacity < size)
            list.Capacity = size;
        
#if NET8_0_OR_GREATER
        CollectionsMarshal.SetCount(list, size);
        return;
#else
        ref ListView<T?> local = ref As<List<T?>, ListView<T?>>(ref list);
        local._size = size;
#endif
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetStackSpan<T>(scoped ref Stack<T?>? stack)
    {
        if (stack is null)
        {
            return Span<T?>.Empty;
        }
        
        SetStackSize(ref stack, stack.Count);
        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        return view._items.AsSpan(0, stack.Count);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetStackSpan<T>(scoped ref Stack<T?>? stack, int length)
    {
        if (stack is null)
        {
            return Span<T?>.Empty;
        }
        
        SetStackSize(ref stack, length);
        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        return view._items.AsSpan(0, length);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetStackSize<T>(scoped ref Stack<T?>? stack, int size)
    {
        if (stack is null) return;
        
        ref var view = ref As<Stack<T?>, StackView<T?>>(ref stack);
        view._size = size;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetQueueSpan<T>(scoped ref Queue<T?>? queue)
    {
        if (queue is null)
        {
            return Span<T?>.Empty;
        }
        
        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);
        return local._items.AsSpan();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T?> GetQueueSpan<T>(scoped ref Queue<T?>? queue, int size)
    {
        if (queue is null)
        {
            return Span<T?>.Empty;
        }
        
        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);
        return local._items.AsSpan(0, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetQueueSize<T>(scoped ref Queue<T?>? queue, out int tail, out int head, out int size)
    {
        if (queue is null)
        {
            tail = 0;
            head = 0;
            size = 0;
            
            return;
        }
        
        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);

        head = local._head;
        tail = local._tail;
        size = local._size;
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetQueueSize<T>(scoped ref Queue<T?>? queue, int tail, int head, int size)
    {
        if (queue is null) return;
        
        ref QueueView<T?> local = ref As<Queue<T?>, QueueView<T?>>(ref queue);

        if (local._items.Length < size)
        {
            local._items = new T[size];
        }
        

        local._head = head;
        local._tail = tail;
        local._size = size;
        
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetNonNullPinnableReference<T>(Span<T> span)
    {
        return ref span.Length == 0 
            ? ref Unsafe.AsRef<T>((void*) new IntPtr(1)) 
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T GetNonNullPinnableReference<T>(ReadOnlySpan<T> span)
    {
        return ref span.Length == 0 
            ? ref Unsafe.AsRef<T>((void*) new IntPtr(1)) 
            : ref Unsafe.AsRef(in span.GetPinnableReference());
    }
    
    /// <summary>
    /// 将Span As SpanByte
    /// </summary>
    /// <param name="span"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<byte> AsRawBytes<T>(this Span<T> span) where T : unmanaged
    {
        if (span.IsEmpty)
            return Span<byte>.Empty;

        fixed (T* ptr = &MemoryMarshal.GetReference(span))
        {
            return new Span<byte>(ptr, span.Length * sizeof(T));
        }
    }

    /// <summary>
    /// 类型安全的内存重新解释
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<TOut> ReinterpretCast<TIn, TOut>(this Span<TIn> span)
        where TIn : unmanaged
        where TOut : unmanaged
    {
        if (span.IsEmpty)
            return Span<TOut>.Empty;

        fixed (TIn* srcPtr = &MemoryMarshal.GetReference(span))
        {
            byte* bytePtr = (byte*)srcPtr;
            int newLength = (span.Length * sizeof(TIn)) / sizeof(TOut);
            return new Span<TOut>(bytePtr, newLength);
        }
    }

    /// <summary>
    /// 直接获取Span的底层指针
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T* GetUnderlyingPointer<T>(this Span<T> span) where T : unmanaged
    {
        fixed (T* ptr = &span.GetPinnableReference())
        {
            return ptr;
        }
    }

    /// <summary>
    /// 从引用创建Span（无安全检查）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpanFromRef<T>(ref T data, int length)
    {
        return new Span<T>(Unsafe.AsPointer(ref data), length);
    }

    /// <summary>
    /// 结构体到字节数组的直接转换
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe byte[] StructureToByteArray<T>(ref T value) where T : unmanaged
    {
        byte[] result = new byte[sizeof(T)];
        fixed (byte* dest = result)
        {
            Unsafe.Write(dest, value);
        }
        return result;
    }

    /// <summary>
    /// 内存块比较（基于指针操作）
    /// </summary>
    public static unsafe bool MemoryCompare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        if (a.Length == 0) return true;

        fixed (byte* aPtr = a, bPtr = b)
        {
            long* longA = (long*)aPtr;
            long* longB = (long*)bPtr;
            int longCount = a.Length / sizeof(long);

            // 先比较long块
            for (int i = 0; i < longCount; i++)
            {
                if (longA[i] != longB[i]) return false;
            }

            // 处理剩余字节
            byte* remA = (byte*)&longA[longCount];
            byte* remB = (byte*)&longB[longCount];
            for (int i = 0; i < a.Length % sizeof(long); i++)
            {
                if (remA[i] != remB[i]) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 非托管内存复制
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void RawCopy<T>(void* destination, void* source, int count) where T : unmanaged
    {
        Buffer.MemoryCopy(source, destination, count * sizeof(T), count * sizeof(T));
    }

    /// <summary>
    /// 获取数组底层数据指针
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T* GetArrayPointer<T>(T[] array) where T : unmanaged
    {
        fixed (T* ptr = array)
        {
            return ptr;
        }
    }

    /// <summary>
    /// 从指针创建Span（带长度校验）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpan<T>(T* ptr, int length) where T : unmanaged
    {
        if (ptr == null && length != 0)
            throw new ArgumentException("Null pointer with non-zero length");
        
        return new Span<T>(ptr, length);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Span<T> CreateSpan<T>(scoped ref T reference, int length)
    {
        var ptr = Unsafe.AsPointer(ref reference);
        return new Span<T>(ptr, length);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(scoped ref T reference, int length)
    {
        var ptr = Unsafe.AsPointer(ref reference);
        return new ReadOnlySpan<T>(ptr, length);
    }

    /// <summary>
    /// 内存初始化（非托管类型）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void InitMemory<T>(T* ptr, T value, int count) where T : unmanaged
    {
        for (int i = 0; i < count; i++)
        {
            ptr[i] = value;
        }
    }

    /// <summary>
    /// 安全的内存地址转换
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref T AsRef<T>(IntPtr ptr) where T : unmanaged
    {
        if (ptr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(ptr));

        return ref Unsafe.AsRef<T>(ptr.ToPointer());
    }
    
    /// <summary>
    /// 内存分块处理（支持SIMD优化）
    /// </summary>
    public static unsafe void ProcessMemoryBlocks(
        void* source,
        void* destination,
        int byteLength,
        int blockSize,
        Action<IntPtr, IntPtr, int> blockProcessor)
    {
        byte* src = (byte*)source;
        byte* dest = (byte*)destination;
        int remaining = byteLength;

        while (remaining >= blockSize)
        {
            blockProcessor((IntPtr)src, (IntPtr)dest, blockSize);
            src += blockSize;
            dest += blockSize;
            remaining -= blockSize;
        }

        if (remaining > 0)
        {
            blockProcessor((IntPtr)src, (IntPtr)dest, remaining);
        }
    }

    /// <summary>
    /// 内存交换（避免临时缓冲区）
    /// </summary>
    public static unsafe void SwapMemory(void* a, void* b, int byteLength)
    {
        byte* pa = (byte*)a;
        byte* pb = (byte*)b;
        long* la = (long*)a;
        long* lb = (long*)b;
        int longCount = byteLength / sizeof(long);
        int rem = byteLength % sizeof(long);

        // 使用long交换提升性能
        for (int i = 0; i < longCount; i++)
        {
            (la[i],  lb[i]) = (lb[i], la[i]);
        }

        // 处理剩余字节
        if (rem > 0)
        {
            pa += longCount * sizeof(long);
            pb += longCount * sizeof(long);
            for (int i = 0; i < rem; i++)
            {
                (pa[i], pb[i]) = (pb[i], pb[i]);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TTo As<TForm, TTo>(ref TForm form)
    { 
        return ref Unsafe.As<TForm, TTo>(ref form);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<T> AsMemory<T>(ReadOnlyMemory<T> memory)
    {
        return Unsafe.As<ReadOnlyMemory<T>, Memory<T>>(ref memory);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetArrayReference<T>(T[] array)
    {
        ref var src = ref array.AsSpan().GetPinnableReference();
        
        return ref src;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetArrayReference(Array array)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        // 使用指针操作确保类型安全
        return ref Unsafe.AsRef<byte>(GetArrayDataPointer(array));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe byte* GetArrayDataPointer(Array array)
    {
        fixed (void* ptr = &Unsafe.As<LuminRawArrayData>(array).Data)
        {
            // 根据数组维度计算数据偏移量
            int offset = array.Rank is 1 ? 0 : array.Rank * sizeof(UIntPtr);
            return (byte*)ptr + offset;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetArrayDataReference<T>(T[] array)
    {
        ref var elementRef = ref GetArrayReference(array);
        return ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref elementRef));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetArrayDataReference(Array array)
    {
        ref var elementRef = ref GetArrayReference(array);
        return ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref elementRef));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref byte GetReference<T>(ref T value)
    {
        return ref Unsafe.AsRef<byte>(Unsafe.AsPointer(ref value));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte DangerousGetArrayDataReference<T>(T[] array)
    {
        ref var src = ref Unsafe.As<T, byte>(ref Unsafe.As<byte, T>(ref Unsafe.As<LuminRawArrayData>(array).Data));

#if NETSTANDARD2_1
        return ref Unsafe.Add(ref src, Unsafe.SizeOf<UIntPtr>());
#else
        return ref src;
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte DangerousGetArrayDataReference<T>(Array array)
    {
        ref var src = ref Unsafe.As<T, byte>(ref Unsafe.As<byte, T>(ref Unsafe.As<LuminRawArrayData>(array).Data));
        
#if NETSTANDARD2_1
        return ref Unsafe.Add(ref src, Unsafe.SizeOf<UIntPtr>());
#else
        return ref Unsafe.Add(ref src, array.Rank is 1 ? 0 : array.Rank * Unsafe.SizeOf<UIntPtr>());
#endif
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int CalculateStringByteCount(string s)
    {
        if (s.Length > 5) 
            return Encoding.UTF8.GetByteCount(s);
        
        int total = 0;
        fixed (char* ptr = s)
        {
            char* p = ptr;
            int remaining = s.Length;
            while (remaining > 0)
            {
                char c = *p++;
                remaining--;

                if (c <= 0x7F) total += 1;
                else if (c <= 0x7FF) total += 2;
                else if (char.IsHighSurrogate(c))
                {
                    if (remaining > 0 && char.IsLowSurrogate(*p))
                    {
                        p++;
                        remaining--;
                        total += 4;
                    }
                    else total += 3; // 无效代理对处理
                }
                else total += 3;
            }
        }
        return total;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AllocateUninitializedObject<T>()
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] AllocateUninitializedArray<T>(int length)
    {
#if NET8_0_OR_GREATER
        return GC.AllocateUninitializedArray<T>(length);
#else
        return new T[length];
#endif
    }
    
    /// <summary>
    /// 将字符串编码为 UTF-8 字节并写入 Span
    /// </summary>
    /// <param name="source">输入的字符串</param>
    /// <param name="destination">目标 Span</param>
    /// <returns>实际写入的字节数（若空间不足返回 -1）</returns>
    public static unsafe int GetBytes(ReadOnlySpan<char> source, Span<byte> destination)
    {
        fixed (char* srcPtr = &source.GetPinnableReference())
        fixed (byte* destPtr = &destination.GetPinnableReference())
        {
            return EncodeCore(srcPtr, source.Length, destPtr, destination.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int EncodeCore(char* src, int srcLength, byte* dest, int destMaxLength)
    {
        byte* destStart = dest;
        char* srcEnd = src + srcLength;
        char* srcCurrent = src;

        // 快速处理 ASCII 前缀
        ProcessAsciiPrefix(ref srcCurrent, srcEnd, ref dest, destMaxLength);

        // 处理剩余字符
        while (srcCurrent < srcEnd)
        {
            if (dest >= destStart + destMaxLength) return -1; // 空间不足

            uint c = *srcCurrent++;

            // ASCII 字符 (0x00-0x7F)
            if (c <= 0x7F)
            {
                *dest++ = (byte)c;
                continue;
            }

            // 两字节编码 (0x80-0x7FF)
            if (c <= 0x7FF)
            {
                if (dest + 1 >= destStart + destMaxLength) return -1;
                *dest++ = (byte)(0xC0 | (c >> 6));
                *dest++ = (byte)(0x80 | (c & 0x3F));
                continue;
            }

            // 代理对处理 (0x10000-0x10FFFF)
            if (char.IsHighSurrogate((char)c))
            {
                if (srcCurrent >= srcEnd || dest + 3 >= destStart + destMaxLength) 
                    return WriteReplacementChar(ref dest, destStart, destMaxLength);

                uint lowSurrogate = *srcCurrent++;
                if (!char.IsLowSurrogate((char)lowSurrogate))
                    return WriteReplacementChar(ref dest, destStart, destMaxLength);

                uint codePoint = ((c - 0xD800U) << 10) + (lowSurrogate - 0xDC00U) + 0x10000U;
                *dest++ = (byte)(0xF0 | (codePoint >> 18));
                *dest++ = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                *dest++ = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                *dest++ = (byte)(0x80 | (codePoint & 0x3F));
                continue;
            }

            // 三字节编码 (0x800-0xFFFF)
            if (dest + 2 >= destStart + destMaxLength) return -1;
            *dest++ = (byte)(0xE0 | (c >> 12));
            *dest++ = (byte)(0x80 | ((c >> 6) & 0x3F));
            *dest++ = (byte)(0x80 | (c & 0x3F));
        }

        return (int)(dest - destStart);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessAsciiPrefix(ref char* src, char* srcEnd, ref byte* dest, int destMaxLength)
    {
        byte* destEnd = dest + destMaxLength;
        
        // 批量处理连续 ASCII
        while (src < srcEnd && dest < destEnd)
        {
            if (*src > 0x7F) break;
            *dest++ = (byte)*src++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int WriteReplacementChar(ref byte* dest, byte* destStart, int destMaxLength)
    {
        if (dest + 2 >= destStart + destMaxLength) return -1;
        *dest++ = 0xEF; // U+FFFD 替换字符
        *dest++ = 0xBF;
        *dest++ = 0xBD;
        return (int)(dest - destStart);
    }
    
    private const int MediumDataThreshold = 16 * 1024; // 16 KB
    private const int LargeDataThreshold = 64 * 1024; // 64 KB

#if NET8_0_OR_GREATER
    private static readonly bool UseAvx2 = System.Runtime.Intrinsics.X86.Avx2.IsSupported;
    private static readonly bool UseSse2 = System.Runtime.Intrinsics.X86.Sse2.IsSupported;
    private static readonly bool UseAdvSimd = System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported;

    #region NET8_0_OR_GREATER SIMD
    
    private static unsafe void Avx2CopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;
        uint head = (uint)((nuint)d & 31);
        if (head != 0)
        {
            uint fix = Math.Min(32 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0 = Avx.LoadVector256(s);
            var v1 = Avx.LoadVector256(s + 32);
            var v2 = Avx.LoadVector256(s + 64);
            var v3 = Avx.LoadVector256(s + 96);
            var v4 = Avx.LoadVector256(s + 128);
            var v5 = Avx.LoadVector256(s + 160);
            var v6 = Avx.LoadVector256(s + 192);
            var v7 = Avx.LoadVector256(s + 224);

            
            Avx.StoreAligned(d, v0);
            Avx.StoreAligned(d + 32, v1);
            Avx.StoreAligned(d + 64, v2);
            Avx.StoreAligned(d + 96, v3);
            Avx.StoreAligned(d + 128, v4);
            Avx.StoreAligned(d + 160, v5);
            Avx.StoreAligned(d + 192, v6);
            Avx.StoreAligned(d + 224, v7);

            // 预取下一行
            if (i + 1 < blocks)
                Sse.Prefetch0(s + 256);
            
            s += 256; d += 256;
            
        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }
    
    private static unsafe void Sse2CopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;

        uint head = (uint)((nuint)d & 15);
        if (head != 0)
        {
            uint fix = Math.Min(16 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0  = Sse2.LoadVector128(s);
            var v1  = Sse2.LoadVector128(s + 16);
            var v2  = Sse2.LoadVector128(s + 32);
            var v3  = Sse2.LoadVector128(s + 48);
            var v4  = Sse2.LoadVector128(s + 64);
            var v5  = Sse2.LoadVector128(s + 80);
            var v6  = Sse2.LoadVector128(s + 96);
            var v7  = Sse2.LoadVector128(s + 112);
            var v8  = Sse2.LoadVector128(s + 128);
            var v9  = Sse2.LoadVector128(s + 144);
            var v10 = Sse2.LoadVector128(s + 160);
            var v11 = Sse2.LoadVector128(s + 176);
            var v12 = Sse2.LoadVector128(s + 192);
            var v13 = Sse2.LoadVector128(s + 208);
            var v14 = Sse2.LoadVector128(s + 224);
            var v15 = Sse2.LoadVector128(s + 240);

            Sse2.StoreAligned(d, v0);
            Sse2.StoreAligned(d + 16, v1);
            Sse2.StoreAligned(d + 32, v2);
            Sse2.StoreAligned(d + 48, v3);
            Sse2.StoreAligned(d + 64, v4);
            Sse2.StoreAligned(d + 80, v5);
            Sse2.StoreAligned(d + 96, v6);
            Sse2.StoreAligned(d + 112, v7);
            Sse2.StoreAligned(d + 128, v8);
            Sse2.StoreAligned(d + 144, v9);
            Sse2.StoreAligned(d + 160, v10);
            Sse2.StoreAligned(d + 176, v11);
            Sse2.StoreAligned(d + 192, v12);
            Sse2.StoreAligned(d + 208, v13);
            Sse2.StoreAligned(d + 224, v14);
            Sse2.StoreAligned(d + 240, v15);

            // 预取下一行
            if (i + 1 < blocks)
                Sse.Prefetch0(s + 256);
            
            s += 256; d += 256;
        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }
    
    private static unsafe void AdvCopyBlock(void* dest, void* src, uint byteCount)
    {
        byte* d = (byte*)dest;
        byte* s = (byte*)src;

        uint head = (uint)((nuint)d & 15);
        if (head != 0)
        {
            uint fix = Math.Min(16 - head, byteCount);
            Unsafe.CopyBlockUnaligned(d, s, fix);
            d += fix; s += fix; byteCount -= fix;
        }

        uint blocks = byteCount / 256;
        for (uint i = 0; i < blocks; i++)
        {
            var v0  = AdvSimd.LoadVector128(s);
            var v1  = AdvSimd.LoadVector128(s + 16);
            var v2  = AdvSimd.LoadVector128(s + 32);
            var v3  = AdvSimd.LoadVector128(s + 48);
            var v4  = AdvSimd.LoadVector128(s + 64);
            var v5  = AdvSimd.LoadVector128(s + 80);
            var v6  = AdvSimd.LoadVector128(s + 96);
            var v7  = AdvSimd.LoadVector128(s + 112);
            var v8  = AdvSimd.LoadVector128(s + 128);
            var v9  = AdvSimd.LoadVector128(s + 144);
            var v10 = AdvSimd.LoadVector128(s + 160);
            var v11 = AdvSimd.LoadVector128(s + 176);
            var v12 = AdvSimd.LoadVector128(s + 192);
            var v13 = AdvSimd.LoadVector128(s + 208);
            var v14 = AdvSimd.LoadVector128(s + 224);
            var v15 = AdvSimd.LoadVector128(s + 240);

            AdvSimd.Store(d, v0);
            AdvSimd.Store(d + 16, v1);
            AdvSimd.Store(d + 32, v2);
            AdvSimd.Store(d + 48, v3);
            AdvSimd.Store(d + 64, v4);
            AdvSimd.Store(d + 80, v5);
            AdvSimd.Store(d + 96, v6);
            AdvSimd.Store(d + 112, v7);
            AdvSimd.Store(d + 128, v8);
            AdvSimd.Store(d + 144, v9);
            AdvSimd.Store(d + 160, v10);
            AdvSimd.Store(d + 176, v11);
            AdvSimd.Store(d + 192, v12);
            AdvSimd.Store(d + 208, v13);
            AdvSimd.Store(d + 224, v14);
            AdvSimd.Store(d + 240, v15);

            s += 256; d += 256;
        }

        uint tail = byteCount % 256;
        if (tail != 0) Unsafe.CopyBlockUnaligned(d, s, tail);
    }

    #endregion
    
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyBlockUnaligned(ref byte destination, ref byte src, uint byteCount)
    {
        CopyBlockUnaligned(Unsafe.AsPointer(ref destination), Unsafe.AsPointer(ref src), byteCount);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyBlockUnaligned(
        void* destination, 
        void* source, 
        uint byteCount)
    {
        
#if NET8_0_OR_GREATER
        if (byteCount < MediumDataThreshold)
        {
            Unsafe.CopyBlockUnaligned(destination, source, byteCount);
            return;
        }
        else
        {
            
            switch (UseAvx2, UseSse2, UseAdvSimd)
            {
                case (true, _, _):
                    Avx2CopyBlock(destination, source, byteCount);
                    return;
                case (_, true, _):
                    Sse2CopyBlock(destination, source, byteCount);
                    return;
                case (_, _, true):
                    AdvCopyBlock(destination, source, byteCount);
                    return;
                default:
                    Unsafe.CopyBlockUnaligned(destination, source, byteCount);
                    return;
            }
        }
#else
        
        Unsafe.CopyBlockUnaligned(destination, source, byteCount);
        return;
#endif
        
    }
    
    [Preserve]
    internal sealed class ListView<T>
    {
        public T[]? _items;
        public int _size;
        public int _version;
    }

    [Preserve]
    internal sealed class StackView<T>
    {
        public T[]? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        public int _size;
        public int _version;
    }
    
    [Preserve]
    internal sealed class QueueView<T>
    {
        public T[]? _items;
#if NETSTANDARD2_1
        private IntPtr _ptr;
#endif
        public int _head;
        public int _tail;
        public int _size;
        public int _version;

        
    }
    
}