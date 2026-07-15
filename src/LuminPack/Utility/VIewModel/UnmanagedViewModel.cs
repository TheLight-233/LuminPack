using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Utility.ViewModel;

#pragma warning disable CS0168

internal static class UnmanagedViewModel
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1> Create<T1>(in T1 value1)
        => new UnmanagedViewModel<T1>(value1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2> Create<T1, T2>(in T1 value1, in T2 value2)
        => new UnmanagedViewModel<T1, T2>(value1, value2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3> Create<T1, T2, T3>(in T1 value1, in T2 value2, in T3 value3)
        => new UnmanagedViewModel<T1, T2, T3>(value1, value2, value3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4> Create<T1, T2, T3, T4>(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
        => new UnmanagedViewModel<T1, T2, T3, T4>(value1, value2, value3, value4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5>(value1, value2, value3, value4, value5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6>(value1, value2, value3, value4, value5, value6);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>(value1, value2, value3, value4, value5, value6, value7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>(value1, value2, value3, value4, value5, value6, value7, value8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>(value1, value2, value3, value4, value5, value6, value7, value8, value9);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> Create<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
        => new UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(value1, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1>
{
    private readonly T1 Value1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1) => Value1 = value1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1) => item1 = Value1;
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UnmanagedViewModel<T1, T2>
{
    private readonly T1 Value1;
    private readonly T2 Value2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2)
    {
        Value1 = value1;
        Value2 = value2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2)
    {
        item1 = Value1;
        item2 = Value2;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;
    private readonly T11 Value11;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
        Value11 = value11;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10, out T11 item11)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
        item11 = Value11;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;
    private readonly T11 Value11;
    private readonly T12 Value12;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
        Value11 = value11;
        Value12 = value12;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10, out T11 item11, out T12 item12)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
        item11 = Value11;
        item12 = Value12;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;
    private readonly T11 Value11;
    private readonly T12 Value12;
    private readonly T13 Value13;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
        Value11 = value11;
        Value12 = value12;
        Value13 = value13;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10, out T11 item11, out T12 item12, out T13 item13)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
        item11 = Value11;
        item12 = Value12;
        item13 = Value13;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;
    private readonly T11 Value11;
    private readonly T12 Value12;
    private readonly T13 Value13;
    private readonly T14 Value14;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
        Value11 = value11;
        Value12 = value12;
        Value13 = value13;
        Value14 = value14;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10, out T11 item11, out T12 item12, out T13 item13, out T14 item14)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
        item11 = Value11;
        item12 = Value12;
        item13 = Value13;
        item14 = Value14;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;
    private readonly T9 Value9;
    private readonly T10 Value10;
    private readonly T11 Value11;
    private readonly T12 Value12;
    private readonly T13 Value13;
    private readonly T14 Value14;
    private readonly T15 Value15;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UnmanagedViewModel(in T1 value1, in T2 value2, in T3 value3, in T4 value4, in T5 value5, in T6 value6, in T7 value7, in T8 value8, in T9 value9, in T10 value10, in T11 value11, in T12 value12, in T13 value13, in T14 value14, in T15 value15)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
        Value8 = value8;
        Value9 = value9;
        Value10 = value10;
        Value11 = value11;
        Value12 = value12;
        Value13 = value13;
        Value14 = value14;
        Value15 = value15;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8, out T9 item9, out T10 item10, out T11 item11, out T12 item12, out T13 item13, out T14 item14, out T15 item15)
    {
        item1 = Value1;
        item2 = Value2;
        item3 = Value3;
        item4 = Value4;
        item5 = Value5;
        item6 = Value6;
        item7 = Value7;
        item8 = Value8;
        item9 = Value9;
        item10 = Value10;
        item11 = Value11;
        item12 = Value12;
        item13 = Value13;
        item14 = Value14;
        item15 = Value15;
    }
}
