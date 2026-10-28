using System.Runtime.CompilerServices;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Prevents temporary layout sentinels from executing user finalizers on uninitialized storage.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class LayoutProbeFinalizerTest
{
    /// <summary>Suppresses finalization only on temporary objects that the probe itself allocated.</summary>
    [Fact]
    public void InternalUninitializedObjectsNeverRunUserFinalizers()
    {
        Volatile.Write(ref FinalizableValue.UninitializedFinalizers, 0);
        Volatile.Write(ref FinalizableValue.ConstructedFinalizers, 0);
        ProbeInternalObjects();
        CollectFinalizers();
        Assert.Equal(0, Volatile.Read(ref FinalizableValue.UninitializedFinalizers));
        ProbeSuppliedObject();
        CollectFinalizers();
        Assert.Equal(1, Volatile.Read(ref FinalizableValue.ConstructedFinalizers));
    }

    /// <summary>Probes owner, reference field, managed struct and array stride with internally created objects.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeInternalObjects()
    {
        Assert.True(LuminFieldLayout.ObjectField(typeof(FinalizableValue), nameof(FinalizableValue.Number)) >= 0);
        Assert.True(LuminFieldLayout.ObjectField(typeof(Holder), nameof(Holder.Child)) >= 0);
        Assert.True(LuminFieldLayout.ValueField(typeof(ValueHolder), nameof(ValueHolder.Child)) >= 0);
        Assert.Equal(Unsafe.SizeOf<ValueHolder>(), LuminFieldLayout.ArrayStride(typeof(ValueHolder)));
    }

    /// <summary>Passes a normally constructed sentinel whose finalizer must remain enabled.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeSuppliedObject()
    {
        var value = new FinalizableValue();
        Assert.True(LuminFieldLayout.ObjectField(typeof(Holder), nameof(Holder.Child), value) >= 0);
    }

    /// <summary>Waits for the completed probe scope to release all temporary references.</summary>
    private static void CollectFinalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class FinalizableValue
    {
        public static int UninitializedFinalizers;
        public static int ConstructedFinalizers;
        public int Number;
        private readonly bool _constructed = true;

        /// <summary>Records whether the finalizer received real or uninitialized object storage.</summary>
        ~FinalizableValue()
        {
            if (_constructed) Interlocked.Increment(ref ConstructedFinalizers);
            else Interlocked.Increment(ref UninitializedFinalizers);
        }
    }

    private sealed class Holder
    {
        public FinalizableValue? Child;
    }

    private struct ValueHolder
    {
        public FinalizableValue? Child;
    }
}
