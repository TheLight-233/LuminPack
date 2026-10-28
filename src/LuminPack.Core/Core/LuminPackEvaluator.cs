using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;

namespace LuminPack.Core;

/// <summary>Accumulates the binary payload size without materializing serialized output.</summary>
[StructLayout(LayoutKind.Auto)]
public unsafe ref partial struct LuminPackEvaluator : IDisposable
{
    private readonly int* _valuePtr; // Store a pointer to the target value.

    private LuminPackEvaluatorOptionState? _optionState;

    /// <summary>Gets the initialized reference context, creating an independent implicit scope on first access.</summary>
    /// <remarks>Initialize this context before copying an uninitialized evaluator when the copies must share references.</remarks>
    public LuminPackEvaluatorOptionState OptionState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_optionState is null) _optionState = CreateReferenceState();
            return _optionState;
        }
    }

    /// <summary>Creates reference storage outside the hot evaluation operation.</summary>
    /// <returns>An independent reference context owned by the requesting evaluator.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LuminPackEvaluatorOptionState CreateReferenceState() => new LuminPackEvaluatorOptionState();


    /// <summary>
    /// Initializes the evaluator with a reference to the target byte-count accumulator.
    /// </summary>
    /// <param name="target">A reference to the target integer.</param>
    public LuminPackEvaluator(ref int target)
    {
        _valuePtr = (int*)Unsafe.AsPointer(ref target);

        _optionState = null;


    }

    /// <summary>Initializes a new instance of <c>LuminPackEvaluator</c>.</summary>
    /// <param name="target">The target used by this operation.</param>
    /// <param name="option">An explicit shared reference context, or null for a lazily owned independent context.</param>
    public LuminPackEvaluator(ref int target, LuminPackEvaluatorOptionState? option = null)
    {
        _valuePtr = (int*)Unsafe.AsPointer(ref target);

        _optionState = option;


    }

    /// <summary>
    /// Adds directly to the target value in memory.
    /// </summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int value)
    {
        unchecked
        {
            *_valuePtr += value;
        }

    }

    /// <summary>
    /// Subtracts directly from the target value in memory.
    /// </summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Subtract(int value)
    {
        unchecked
        {
            *_valuePtr -= value;
        }
    }

    /// <summary>Determines whether the value type requires managed reference handling.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReferenceOrContainsReferences<T>()
    {
        return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    /// <summary>Obtains the string length for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStringLength(scoped ref string? value)
    { return GetStringLength<global::LuminPack.Option.LuminPackDefaultOptions>(ref value); }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength()
    { return StringRecordLength<global::LuminPack.Option.LuminPackDefaultOptions>(); }

    /// <summary>Calculates the serialized size contribution of string utf8.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf8(scoped ref string? value)
    {
        return value is null ? 0 : LuminPackMarshal.CalculateStringByteCount(value);
    }

    /// <summary>Calculates the serialized size contribution of string utf16.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <remarks>The writer copies the raw code units, so the payload is two bytes per character; a string holds at most
    /// 0x3FFFFFDF characters, so the width always fits an int.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf16(scoped ref string? value)
    {
        return value is null ? 0 : value.Length * sizeof(char);
    }

    /// <summary>Calculates the payload width of a UTF-16 length record.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The exact payload width the writer produces.</returns>
    /// <remarks>Latin-1 text is written one byte per character, other text as raw code units.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateStringUtf16Record(scoped ref string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : global::LuminPack.Utility.StringSerializer.Utf16RecordPayloadLength(value);
    }

    /// <summary>Calculates the serialized size contribution of union header.</summary>
    /// <param name="tag">The tag used by this operation.</param>
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

    /// <summary>Calculates the serialized size contribution of unmanaged array.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedArray<T>(scoped ref T?[]? array)
        where T : unmanaged
    {
        return DangerousCalculateArray(ref array);
    }

    /// <summary>Calculates the serialized size contribution of unmanaged span.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedSpan<T>(scoped ref Span<T?> array)
        where T : unmanaged
    {
        return DangerousCalculateSpan(ref array);
    }

    /// <summary>Calculates the serialized size contribution of unmanaged span.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanagedSpan<T>(scoped ref ReadOnlySpan<T?> array)
        where T : unmanaged
    {
        return DangerousCalculateSpan(ref array);
    }

    /// <summary>Performs the dangerous calculate array step for the current operation.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateArray<T>(scoped ref T?[]? array)
    {
        if (array is null)
            return 4;

        return 4 + array.Length * Unsafe.SizeOf<T>();
    }

    /// <summary>Performs the dangerous calculate span step for the current operation.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateSpan<T>(scoped ref Span<T?> span)
    {
        if (span.IsEmpty)
            return 4;

        return 4 + span.Length * Unsafe.SizeOf<T>();
    }

    /// <summary>Performs the dangerous calculate span step for the current operation.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateSpan<T>(scoped ref ReadOnlySpan<T?> span)
    {
        if (span.IsEmpty)
            return 4;

        return 4 + span.Length * Unsafe.SizeOf<T>();
    }

    /// <summary>Calculates the serialized size contribution of unmanaged.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateUnmanaged<T>()
        where T : unmanaged
    {
        return Unsafe.SizeOf<T>();
    }

    /// <summary>Performs the dangerous calculate unmanaged step for the current operation.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DangerousCalculateUnmanaged<T>()
    {
        return Unsafe.SizeOf<T>();
    }

    /// <summary>Obtains the string length for the supplied input.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringLength<TOptions>(scoped ref string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8), (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token)) switch
        {
            (true, true) => checked(CalculateStringUtf8(ref value) + 1),
            (true, false) => checked(CalculateStringUtf8(ref value) + sizeof(int) * 2),
            (false, true) => checked(CalculateStringUtf16(ref value) + sizeof(ushort)),
            (false, false) => checked(CalculateStringUtf16Record(ref value) + sizeof(int))};
    }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : sizeof(ushort)) : ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int));
    }

    // Calculates the byte payload size for a byte value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(byte x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 byte
        else
            return sizeof(sbyte) + sizeof(byte); // Type code (1) plus value (1) requires 2 bytes.
    }

    // Calculates the byte payload size for an sbyte value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(sbyte x)
    {
        if (x >= VarIntCodes.MinSingleValue) // -120~127
            return sizeof(sbyte); // 1 byte
        else
            return sizeof(sbyte) + sizeof(sbyte); // Type code (1) plus value (1) requires 2 bytes.
    }

    // Calculates the byte payload size for a ushort value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(ushort x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 byte
        else
            return sizeof(sbyte) + sizeof(ushort); // Type code (1) plus value (2) requires 3 bytes.
    }

    // Calculates the byte payload size for a short value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(short x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 byte
            else
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 byte
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // Type code (1) plus value (1) requires 2 bytes.
            else
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
        }
    }

    // Calculates the byte payload size for a uint value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(uint x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 byte
        else if (x <= ushort.MaxValue) // 128~65535
            return sizeof(sbyte) + sizeof(ushort); // Type code (1) plus value (2) requires 3 bytes.
        else
            return sizeof(sbyte) + sizeof(uint); // Type code (1) plus value (4) requires 5 bytes.
    }

    // Calculates the byte payload size for an int value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(int x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 byte
            else if (x <= short.MaxValue) // 128~32767
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
            else
                return sizeof(sbyte) + sizeof(int); // Type code (1) plus value (4) requires 5 bytes.
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 byte
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // Type code (1) plus value (1) requires 2 bytes.
            else if (x >= short.MinValue) // -32768~-129
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
            else
                return sizeof(sbyte) + sizeof(int); // Type code (1) plus value (4) requires 5 bytes.
        }
    }

    // Calculates the byte payload size for a ulong value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(ulong x)
    {
        if (x <= VarIntCodes.MaxSingleValue) // 0~127
            return sizeof(sbyte); // 1 byte
        else if (x <= ushort.MaxValue) // 128~65535
            return sizeof(sbyte) + sizeof(ushort); // Type code (1) plus value (2) requires 3 bytes.
        else if (x <= uint.MaxValue) // 65536~4294967295
            return sizeof(sbyte) + sizeof(uint); // Type code (1) plus value (4) requires 5 bytes.
        else
            return sizeof(sbyte) + sizeof(ulong); // Type code (1) plus value (8) requires 9 bytes.
    }

    // Calculates the byte payload size for a long value.
    /// <summary>Calculates the serialized size contribution of var int.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static int CalculateVarInt(long x)
    {
        if (x >= 0)
        {
            if (x <= VarIntCodes.MaxSingleValue) // 0~127
                return sizeof(sbyte); // 1 byte
            else if (x <= short.MaxValue) // 128~32767
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
            else if (x <= int.MaxValue) // 32768~2147483647
                return sizeof(sbyte) + sizeof(int); // Type code (1) plus value (4) requires 5 bytes.
            else
                return sizeof(sbyte) + sizeof(long); // Type code (1) plus value (8) requires 9 bytes.
        }
        else
        {
            if (x >= VarIntCodes.MinSingleValue) // -120~-1
                return sizeof(sbyte); // 1 byte
            else if (x >= sbyte.MinValue) // -128~-121
                return sizeof(sbyte) + sizeof(sbyte); // Type code (1) plus value (1) requires 2 bytes.
            else if (x >= short.MinValue) // -32768~-129
                return sizeof(sbyte) + sizeof(short); // Type code (1) plus value (2) requires 3 bytes.
            else if (x >= int.MinValue) // -2147483648~-32769
                return sizeof(sbyte) + sizeof(int); // Type code (1) plus value (4) requires 5 bytes.
            else
                return sizeof(sbyte) + sizeof(long); // Type code (1) plus value (8) requires 9 bytes.
        }
    }


    /// <summary>
    /// Adds to the value in place.
    /// </summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Subtracts from the value in place.
    /// </summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Returns a reference to the current value without copying it.
    /// </summary>
    public int Value => *_valuePtr;

    /// <summary>Gets the value ptr.</summary>
    public int* ValuePtr => _valuePtr;

    /// <summary>Releases the resources or pooled state owned by this instance.</summary>
    public void Dispose()
    {

    }
}
