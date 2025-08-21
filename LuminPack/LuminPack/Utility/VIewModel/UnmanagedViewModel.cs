using System.Runtime.InteropServices;

namespace LuminPack.Utility.VIewModel;

#pragma warning disable CS0168 

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1>
    where T1 : unmanaged
{
    private readonly T1 Value1;

    public UnmanagedViewModel(T1 value1)
    {
        Value1 = value1;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2>
    where T1 : unmanaged
    where T2 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;

    public UnmanagedViewModel(T1 value1, T2 value2)
    {
        Value1 = value1;
        Value2 = value2;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
        Value6 = value6;
        Value7 = value7;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
{
    private readonly T1 Value1;
    private readonly T2 Value2;
    private readonly T3 Value3;
    private readonly T4 Value4;
    private readonly T5 Value5;
    private readonly T6 Value6;
    private readonly T7 Value7;
    private readonly T8 Value8;

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
    where T11 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
    where T11 : unmanaged
    where T12 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
    where T11 : unmanaged
    where T12 : unmanaged
    where T13 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
    where T11 : unmanaged
    where T12 : unmanaged
    where T13 : unmanaged
    where T14 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14)
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly record struct UnmanagedViewModel<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>
    where T1 : unmanaged
    where T2 : unmanaged 
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
    where T7 : unmanaged
    where T8 : unmanaged
    where T9 : unmanaged
    where T10 : unmanaged
    where T11 : unmanaged
    where T12 : unmanaged
    where T13 : unmanaged
    where T14 : unmanaged
    where T15 : unmanaged
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

    public UnmanagedViewModel(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8, T9 value9, T10 value10, T11 value11, T12 value12, T13 value13, T14 value14, T15 value15)
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
}