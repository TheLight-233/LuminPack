using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest.FixedUnionCursors;
/// <summary>Checks the approved cursor-returning protocol against original virtual operations.</summary>
public class FixedUnionCursorTest
{
    /// <summary>Preserves old overrides, callbacks, nulls, buffer growth and sparse enumeration.</summary>
    [Fact]
    public void CursorBridgePreservesOriginalVirtualBoundaries()
    {
        var source = new Dictionary<int, CursorRoot?>
        {
            [1] = new FixedCase
            {
                Number = 11
            },
            [2] = new LegacyCase
            {
                Number = 22
            },
            [3] = new CallbackCase
            {
                Number = 33
            },
            [4] = new VariableCase
            {
                Text = new string ('a', 40_000)
            },
            [5] = null
        };
        LegacyCase.Calls = CallbackCase.Calls = 0;
        using var actualDestination = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(in source, actualDestination);
        byte[] actual = actualDestination.GetSpan().ToArray();
        Assert.True(actualDestination.TotalLength > 32 * 1024);
        Assert.Equal(1, LegacyCase.Calls);
        Assert.Equal(17, LegacyCase.Position);
        Assert.Equal(1, CallbackCase.Calls);
        using var destination = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(destination);
        int index = 0;
        writer.WriteCollectionHeader(source.Count);
        foreach (var pair in source)
        {
            index = writer.Position;
            writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), pair.Key));
            if (pair.Value is null)
            {
                writer.Cursor = 255;
                writer.Advance(1);
            }
            else
                pair.Value.__LuminPackUnionSerialize_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref writer);
        }

        Assert.Equal(actual, destination.DangerousGetBuffer()[..writer.Position].ToArray());
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>>(actual);
        Assert.Equal(11, Assert.IsType<FixedCase>(copy[1]).Number);
        Assert.Equal(22, Assert.IsType<FixedCase>(copy[2]).Number);
        Assert.Equal(33, Assert.IsType<CallbackCase>(copy[3]).Number);
        Assert.Equal(new string ('a', 40_000), Assert.IsType<VariableCase>(copy[4]).Text);
        Assert.Null(copy[5]);
        source.Remove(1);
        copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>>(LuminPackSerializer.Serialize(source));
        Assert.Equal(source.Keys, copy.Keys);
        Assert.Equal(22, Assert.IsType<FixedCase>(copy[2]).Number);
    }

    /// <summary>Publishes the pending cursor on legacy failure and allows an explicit caller reset before retry.</summary>
    [Fact]
    public void CursorFallbackRetainsExceptionCleanupAndReuse()
    {
        var source = new Dictionary<int, CursorRoot?>
        {
            [1] = new FixedCase
            {
                Number = 11
            },
            [2] = new LegacyCase
            {
                Number = 22
            }
        };
        using var destination = new LuminBufferWriter(true);
        try
        {
            LegacyCase.Throw = true;
            var error = Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(in source, destination));
            Assert.Equal("Controlled legacy-slot failure.", error.Message);
            Assert.Equal(0, destination.CurrentIndex);
        }
        finally
        {
            LegacyCase.Throw = false;
        }

        destination.ResetForReuse();
        LuminPackSerializer.Serialize(in source, destination);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>>(destination.GetSpan());
        Assert.Equal(22, Assert.IsType<FixedCase>(copy[2]).Number);
    }

    /// <summary>Publishes the pending fixed-case cursor before a manually registered formatter.</summary>
    [Fact]
    public unsafe void RegisteredRuntimeCaseRetainsItsOriginalFormatter()
    {
        CursorRoot.Register<RegisteredCase>(42, &WriteRegistered, &ReadRegistered, &WriteRegisteredJson, &ReadRegisteredJson);
        var source = new Dictionary<int, CursorRoot?>
        {
            [1] = new FixedCase
            {
                Number = 11
            },
            [2] = new RegisteredCase
            {
                Number = 71
            }
        };
        byte[] payload = LuminPackSerializer.Serialize(source);
        Assert.Equal(17, RegisteredCase.Position);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>>(payload);
        Assert.Equal(71, Assert.IsType<RegisteredCase>(copy[2]).Number);
        Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
    }

    /// <summary>Retains the last complete root boundary when an unknown runtime case fails.</summary>
    [Fact]
    public void UnknownRuntimeCaseStillFailsAndReleasesTheOperation()
    {
        var source = new Dictionary<int, CursorRoot?>
        {
            [1] = new FixedCase
            {
                Number = 11
            },
            [2] = new UnknownCase()
        };
        using var destination = new LuminBufferWriter(true);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize(in source, destination));
        Assert.Equal(0, destination.CurrentIndex);
        source[2] = new FixedCase
        {
            Number = 73
        };
        destination.ResetForReuse();
        LuminPackSerializer.Serialize(in source, destination);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>>(destination.GetSpan());
        Assert.Equal(73, Assert.IsType<FixedCase>(copy[2]).Number);
    }

    /// <summary>Writes the registered case using the original state-owning callback convention.</summary>
    /// <param name = "writer">The published caller operation.</param>
    /// <param name = "value">The registered runtime case.</param>
    private static void WriteRegistered(ref LuminPackWriter writer, ref CursorRoot value)
    {
        RegisteredCase.Position = writer.Position;
        writer.WriteUnionHeader(42);
        int index = writer.Position;
        writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), ((RegisteredCase)value).Number));
    }

    /// <summary>Reads the registered payload after its tag has been consumed by the original reader.</summary>
    /// <param name = "reader">The active caller input.</param>
    /// <param name = "value">Receives the registered runtime object.</param>
    private static void ReadRegistered(ref LuminPackReader reader, ref CursorRoot value)
    {
        int index = reader.Position;
        reader.Advance(global::LuminPack.Core.LuminPackReader.Read(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(reader.Buffer), (nint)(uint)index), out int number));
        value = new RegisteredCase
        {
            Number = number
        };
    }

    /// <summary>Completes the required JSON operation pair; this binary cursor test never writes JSON.</summary>
    /// <param name = "writer">The JSON operation state.</param>
    /// <param name = "value">The registered runtime case.</param>
    private static void WriteRegisteredJson(ref LuminPackJsonWriter writer, ref CursorRoot value)
        => throw new NotSupportedException("The cursor fixture registers JSON only to satisfy the complete Union registration.");

    /// <summary>Completes the required JSON operation pair; this binary cursor test never reads JSON.</summary>
    /// <param name = "reader">The JSON operation state.</param>
    /// <param name = "value">Receives the registered runtime object.</param>
    private static void ReadRegisteredJson(ref LuminPackJsonReader reader, ref CursorRoot value)
        => throw new NotSupportedException("The cursor fixture registers JSON only to satisfy the complete Union registration.");
}

/// <summary>Provides ordinary non-generic Union cases for cursor and fallback coverage.</summary>
[LuminPackable, LuminPackUnion(5, typeof(FixedCase)), LuminPackUnion(6, typeof(CallbackCase)), LuminPackUnion(7, typeof(VariableCase)), LuminPackUnion(8, typeof(DerivedCase))]
public abstract partial class CursorRoot
{
}

/// <summary>Provides a callback-free fixed case with an inherited old-slot override.</summary>
[LuminPackable]
public partial class FixedCase : CursorRoot
{
    public int Number;
}

/// <summary>Simulates a previously compiled subtype overriding only the original slot.</summary>
public sealed class LegacyCase : FixedCase
{
    public static int Calls, Position;
    public static bool Throw;
    /// <summary>Observes published state before invoking the inherited original case body.</summary>
    /// <param name = "writer">The caller's current operation.</param>
    public override void __LuminPackUnionSerialize_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref LuminPackWriter writer)
    {
        Calls++;
        Position = writer.Position;
        if (Throw)
            throw new InvalidOperationException("Controlled legacy-slot failure.");
        base.__LuminPackUnionSerialize_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref writer);
    }
}

/// <summary>Retains user callbacks on the original cursor-observing path.</summary>
[LuminPackable]
public sealed partial class CallbackCase : CursorRoot
{
    public int Number;
    public static int Calls;
    /// <summary>Checks callback selection and managed entry roots during a compacting collection.</summary>
    [LuminPackOnSerializing]
    public void BeforeWrite()
    {
        Calls++;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}

/// <summary>Forces the original variable-length growth path to refresh the operation's destination.</summary>
[LuminPackable]
public sealed partial class VariableCase : CursorRoot
{
    public string? Text;
}

/// <summary>Keeps the registered base case slot open under the established sealing policy.</summary>
[LuminPackable]
public sealed partial class DerivedCase : FixedCase
{
    public int Extra;
}

/// <summary>Uses explicit registration rather than generated case fields.</summary>
public sealed class RegisteredCase : CursorRoot
{
    public int Number;
    public static int Position;
}

/// <summary>Has no generated or registered operation and must retain the original missing-case failure.</summary>
public sealed class UnknownCase : CursorRoot
{
}
