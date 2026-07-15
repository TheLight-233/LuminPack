using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Interface;
using LuminPack.Internal;
using LuminPack.Option;

namespace LuminPack.Core;

[StructLayout(LayoutKind.Auto)]
public unsafe ref struct LuminPackEvaluator : IDisposable
{
    private readonly int* _valuePtr; // 直接持有目标值的指针

    private readonly LuminPackEvaluatorOptionState _optionState;
    
    public LuminPackEvaluatorOptionState OptionState => _optionState;
    public LuminPackSerializerOption Option => _optionState.Option;
    
    private readonly bool SerializeStringAsUtf8;
    private readonly bool SerializeStringRecordAsToken;
    
    /// <summary>
    /// 初始化评估器，绑定到目标整数的内存地址
    /// </summary>
    /// <param name="target">目标整数的引用</param>
    public LuminPackEvaluator(ref int target)
    {
        _valuePtr = (int*)Unsafe.AsPointer(ref target);
        
        _optionState = LuminPackEvaluatorOptionState.NullOption;
        
        SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
    }

    public LuminPackEvaluator(ref int target, LuminPackEvaluatorOptionState? option = null)
    {
        _valuePtr = (int*)Unsafe.AsPointer(ref target);
        
        _optionState = option ?? LuminPackEvaluatorOptionState.NullOption;
        
        SerializeStringAsUtf8 = _optionState.Option.StringEncoding is LuminPackStringEncoding.UTF8;
        SerializeStringRecordAsToken = _optionState.Option.StringRecording is LuminPackStringRecording.Token;
    }

    /// <summary>
    /// 直接修改内存中的值（高性能累加）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int value)
    {
        unchecked
        {
            *_valuePtr += value;
        }
        
    }

    /// <summary>
    /// 直接修改内存中的值（高性能减法）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Subtract(int value)
    {
        unchecked
        {
            *_valuePtr -= value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ILuminPackEvaluator<T> GetEvaluator<T>()
    {
        return LuminPackParseProvider.GetParserEvaluator<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReferenceOrContainsReferences<T>()
    {
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStringLength(scoped ref string? value)
    {
        return (SerializeStringAsUtf8, SerializeStringRecordAsToken) switch
        {
            (true, true) => CalculateStringUtf8(ref value) + 1,
            (true, false) => CalculateStringUtf8(ref value) + 8,
            (false, true) => CalculateStringUtf16(ref value) + 1,
            (false, false) => CalculateStringUtf16(ref value) + 4
        };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength()
    {
        return SerializeStringRecordAsToken ? 1 : SerializeStringAsUtf8 ? 8 : 4;;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf8(scoped ref string? value)
    {
        return value is null ? 0 : LuminPackMarshal.CalculateStringByteCount(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf16(scoped ref string? value)
    {
        return value is null ? 0 : Encoding.Unicode.GetByteCount(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateValue<T>(scoped in T? value)
    {
        var v = value;
        GetEvaluator<T>().CalculateOffset(ref this, ref v);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateUnionHeader(scoped in ushort tag)
    {
        if (tag < LuminPackCode.WideTag)
        {
            *_valuePtr += 1;
        }
        else
        {
            *_valuePtr += 3;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateValueWithEvaluator<TEvaluator, T>(TEvaluator evaluator, scoped in T? value) 
        where TEvaluator : ILuminPackEvaluator<T>
    {
        var v = value;
        evaluator.CalculateOffset(ref this, ref v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateArray<T>(scoped ref T?[]? array)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Add(DangerousCalculateArray(ref array));
            
            return;
        }
        
        var evaluator = LuminPackParseProvider.GetParserEvaluator<T>();

        Add(4);
        
        if (array is null)
            return;
        
        for (int i = 0; i < array.Length; i++)
        {
            evaluator.CalculateOffset(ref this, ref array[i]);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateSpan<T>(scoped ref Span<T?> span)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Add(DangerousCalculateSpan(ref span));
            
            return;
        }

        Add(4);
        
        if (span.IsEmpty)
            return;
        
        var evaluator = LuminPackParseProvider.GetParserEvaluator<T>();
        
        for (int i = 0; i < span.Length; i++)
        {
            evaluator.CalculateOffset(ref this, ref span[i]);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateSpan<T>(scoped ref ReadOnlySpan<T?> span)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Add(DangerousCalculateSpan(ref span));
            
            return;
        }
        
        var evaluator = LuminPackParseProvider.GetParserEvaluator<T>();

        Add(4);
        
        if (span.IsEmpty)
            return;
        
        for (int i = 0; i < span.Length; i++)
        {
            var v = span[i];
            evaluator.CalculateOffset(ref this, ref v);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedArray<T>(scoped ref T?[]? array) 
        where T : unmanaged
    {
        return DangerousCalculateArray(ref array);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedSpan<T>(scoped ref Span<T?> array) 
        where T : unmanaged
    {
        return DangerousCalculateSpan(ref array);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedSpan<T>(scoped ref ReadOnlySpan<T?> array) 
        where T : unmanaged
    {
        return DangerousCalculateSpan(ref array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateArray<T>(scoped ref T?[]? array)
    {
        if (array is null)
            return 4;
        
        return 4 + array.Length * Unsafe.SizeOf<T>();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateSpan<T>(scoped ref Span<T?> span)
    {
        if (span.IsEmpty)
            return 4;
        
        return 4 + span.Length * Unsafe.SizeOf<T>();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateSpan<T>(scoped ref ReadOnlySpan<T?> span)
    {
        if (span.IsEmpty)
            return 4;
        
        return 4 + span.Length * Unsafe.SizeOf<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanaged<T>()
        where T : unmanaged
    {
        return Unsafe.SizeOf<T>();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateUnmanaged<T>() 
    {
        return Unsafe.SizeOf<T>();
    }
    
    // 计算写入 byte 类型所需的字节数
    public static int CalculateVarInt(byte x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 字节
        else
            return sizeof(sbyte) + sizeof(byte); // 类型码(1) + 值(1) = 2 字节
    }

    // 计算写入 sbyte 类型所需的字节数
    public static int CalculateVarInt(sbyte x)
    {
        if (x >= VarIntCodes.MinSingleValue) // -120~127
            return sizeof(sbyte); // 1 字节
        else
            return sizeof(sbyte) + sizeof(sbyte); // 类型码(1) + 值(1) = 2 字节
    }

    // 计算写入 ushort 类型所需的字节数
    public static int CalculateVarInt(ushort x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 字节
        else
            return sizeof(sbyte) + sizeof(ushort); // 类型码(1) + 值(2) = 3 字节
    }

    // 计算写入 short 类型所需的字节数
    public static int CalculateVarInt(short x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 字节
            else
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 字节
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // 类型码(1) + 值(1) = 2 字节
            else
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
        }
    }

    // 计算写入 uint 类型所需的字节数
    public static int CalculateVarInt(uint x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 字节
        else if (x <= ushort.MaxValue) // 128~65535
            return sizeof(sbyte) + sizeof(ushort); // 类型码(1) + 值(2) = 3 字节
        else
            return sizeof(sbyte) + sizeof(uint); // 类型码(1) + 值(4) = 5 字节
    }

    // 计算写入 int 类型所需的字节数
    public static int CalculateVarInt(int x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 字节
            else if (x <= short.MaxValue) // 128~32767
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
            else
                return sizeof(sbyte) + sizeof(int); // 类型码(1) + 值(4) = 5 字节
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 字节
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // 类型码(1) + 值(1) = 2 字节
            else if (x >= short.MinValue) // -32768~-129
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
            else
                return sizeof(sbyte) + sizeof(int); // 类型码(1) + 值(4) = 5 字节
        }
    }

    // 计算写入 ulong 类型所需的字节数
    public static int CalculateVarInt(ulong x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 字节
        else if (x <= ushort.MaxValue) // 128~65535
            return sizeof(sbyte) + sizeof(ushort); // 类型码(1) + 值(2) = 3 字节
        else if (x <= uint.MaxValue) // 65536~4294967295
            return sizeof(sbyte) + sizeof(uint); // 类型码(1) + 值(4) = 5 字节
        else
            return sizeof(sbyte) + sizeof(ulong); // 类型码(1) + 值(8) = 9 字节
    }

    // 计算写入 long 类型所需的字节数
    public static int CalculateVarInt(long x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 字节
            else if (x <= short.MaxValue) // 128~32767
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
            else if (x <= int.MaxValue) // 32768~2147483647
                return sizeof(sbyte) + sizeof(int); // 类型码(1) + 值(4) = 5 字节
            else
                return sizeof(sbyte) + sizeof(long); // 类型码(1) + 值(8) = 9 字节
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 字节
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // 类型码(1) + 值(1) = 2 字节
            else if (x >= short.MinValue) // -32768~-129
                return sizeof(sbyte) + sizeof(short); // 类型码(1) + 值(2) = 3 字节
            else if (x >= int.MinValue) // -2147483648~-32769
                return sizeof(sbyte) + sizeof(int); // 类型码(1) + 值(4) = 5 字节
            else
                return sizeof(sbyte) + sizeof(long); // 类型码(1) + 值(8) = 9 字节
        }
    }
    

    /// <summary>
    /// 重载 += 运算符（原地修改）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminPackEvaluator operator +(LuminPackEvaluator a, int value)
    { 
        unchecked
        {
            *a._valuePtr += value;
            return a;
        }
        
    }

    /// <summary>
    /// 重载 -= 运算符（原地修改）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminPackEvaluator operator -(LuminPackEvaluator a, int value)
    {
        unchecked
        {
            *a._valuePtr -= value;
            return a;
        }
        
    }

    /// <summary>
    /// 获取当前值（无拷贝开销）
    /// </summary>
    public int Value => *_valuePtr;
    
    public int* ValuePtr => _valuePtr;

    public void Dispose()
    {
        
    }
}