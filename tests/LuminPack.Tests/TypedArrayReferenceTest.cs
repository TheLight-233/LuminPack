using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks typed array data references independently of the runtime's object header.</summary>
public sealed class TypedArrayReferenceTest
{
    /// <summary>Returns the actual first element rather than a pointer-width-shifted element.</summary>
    [Fact]
    public void PrimitiveReferencesStartAtElementZero()
    {
        int[] values = [111, 222, 333, 444];
        Assert.True(Unsafe.AreSame(ref values[0], ref LuminPackMarshal.DangerousGetArrayDataReference(values)));
        Assert.True(Unsafe.AreSame(ref values[0], ref LuminPackMarshal.GetArrayReference(values)));
        Assert.True(Unsafe.AreSame(ref values[0], ref LuminPackMarshal.GetNotNullArrayReference(values)));
        Assert.True(Unsafe.AreSame(ref Unsafe.As<int, byte>(ref values[0]), ref LuminPackMarshal.GetArrayDataReference(values)));
    }

    /// <summary>Keeps the data address for an empty non-null vector without dereferencing it.</summary>
    [Fact]
    public void EmptyArraysKeepTheirDataReference()
    {
        int[] values = [];
        Assert.True(Unsafe.AreSame(ref MemoryMarshal.GetArrayDataReference(values), ref LuminPackMarshal.GetArrayReference(values)));
    }

    /// <summary>Preserves a live reference into a covariant array without adding a writable-Span type check.</summary>
    [Fact]
    public void ReferenceArraysKeepIdentityAndCovariance()
    {
        string[] strings = [new string('a', 3), new string('b', 3)];
        object[] values = strings;
        ref object first = ref LuminPackMarshal.DangerousGetArrayDataReference(values);
        Assert.Same(strings[0], first);
        strings[0] = new string('c', 3);
        Assert.Same(strings[0], first);
    }

    /// <summary>Supports the already validated fake array type used by hash-entry views.</summary>
    [Fact]
    public void HashEntryViewsStartAtTheirFirstRealEntry()
    {
        var set = new HashSet<int> { 113, 229, 347 };
        var view = LuminPackMarshal.GetHashSetView(set);
        ref var first = ref LuminPackMarshal.GetArrayReference(view._entries);
        Assert.Equal(113, first.Value);
    }

    /// <summary>Returns a null reference without an eager validation load for a null typed array.</summary>
    [Fact]
    public void NullBehaviorMatchesTheExistingTargetContract()
    {
        Assert.True(Unsafe.IsNullRef(ref LuminPackMarshal.GetArrayReference<int>(null)));
        Assert.True(Unsafe.IsNullRef(ref LuminPackMarshal.DangerousGetArrayDataReference<int>(null!)));
    }
}
