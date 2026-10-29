using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace LuminPackUnitTest.FixedUnionCursors;
/// <summary>Checks the approved cursor-returning protocol against original virtual operations.</summary>
public class FixedUnionCursorTest
{
    /// <summary>Reserves complete large fixed cases in lists and both dictionary traversal paths.</summary>
    [Fact]
    public void LargeFixedCasesRetainCapacityAcrossCursorCalls()
    {
        var first = new CapacityCase { Value = new CapacityPayload { Marker = 31 }, Tail = 32 };
        var second = new CapacityCase { Value = new CapacityPayload { Marker = 41 }, Tail = 42 };
        var list = new List<CapacityRoot> { first, second };
        using var listDestination = new LuminBufferWriter(true);
        int initial = listDestination.TotalLength;
        LuminPackSerializer.Serialize(in list, listDestination);
        byte[] listBytes = listDestination.GetSpan().ToArray();
        Assert.True(listDestination.TotalLength > initial);
        Assert.Equal(40014, listBytes.Length);
        var copy = LuminPackSerializer.Deserialize<List<CapacityRoot>>(listBytes);
        Assert.Equal(31, Assert.IsType<CapacityCase>(copy[0]).Value.Marker);
        Assert.Equal(42, Assert.IsType<CapacityCase>(copy[1]).Tail);
        Assert.Equal(listBytes, LuminPackSerializer.Serialize(copy));

        var dense = new Dictionary<int, CapacityRoot> { [1] = first, [2] = second };
        var sparse = new Dictionary<int, CapacityRoot>(dense) { [3] = first };
        sparse.Remove(3);
        using var denseDestination = new LuminBufferWriter(true);
        using var sparseDestination = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(in dense, denseDestination);
        LuminPackSerializer.Serialize(in sparse, sparseDestination);
        byte[] denseBytes = denseDestination.GetSpan().ToArray();
        Assert.Equal(40022, denseBytes.Length);
        Assert.Equal(denseBytes, sparseDestination.GetSpan().ToArray());
        var dictionaryCopy = LuminPackSerializer.Deserialize<Dictionary<int, CapacityRoot>>(denseBytes);
        Assert.Equal(31, Assert.IsType<CapacityCase>(dictionaryCopy[1]).Value.Marker);
        Assert.Equal(42, Assert.IsType<CapacityCase>(dictionaryCopy[2]).Tail);
    }

    /// <summary>Includes the widest Union header when a fixed payload crosses the minimum growth headroom by one byte.</summary>
    [Fact]
    public void WideFixedCaseReservesAtTheExactThreshold()
    {
        Assert.Equal(28665, Unsafe.SizeOf<CapacityPrefix>());
        Assert.Equal(4094, Unsafe.SizeOf<CapacityTail>());
        var values = new List<WideCapacityRoot>
        {
            new WideCapacityPrefix { Value = new CapacityPrefix { Marker = 51 } },
            new WideCapacityTail { Value = new CapacityTail { Marker = 61 } }
        };
        using var destination = new LuminBufferWriter(true);
        int initial = destination.TotalLength;
        LuminPackSerializer.Serialize(in values, destination);
        byte[] bytes = destination.GetSpan().ToArray();
        Assert.Equal(32769, bytes.Length);
        Assert.True(destination.TotalLength > initial);
        var copy = LuminPackSerializer.Deserialize<List<WideCapacityRoot>>(bytes);
        Assert.Equal(51, Assert.IsType<WideCapacityPrefix>(copy[0]).Value.Marker);
        Assert.Equal(61, Assert.IsType<WideCapacityTail>(copy[1]).Value.Marker);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
    }

    /// <summary>Preserves the original Union virtual slot, callbacks, nulls and growth in list writes.</summary>
    [Fact]
    public void ListWritesRetainOriginalVirtualBoundaries()
    {
        var source = new List<CursorRoot?>
        {
            new FixedCase { Number = 11 },
            new LegacyCase { Number = 22 },
            new CursorOverrideCase { Number = 44 },
            new CallbackCase { Number = 33 },
            new VariableCase { Text = new string('a', 40_000) },
            null
        };
        LegacyCase.Calls = CursorOverrideCase.Calls = CallbackCase.Calls = 0;
        using var destination = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(in source, destination);
        byte[] actual = destination.GetSpan().ToArray();
        Assert.Equal(1, LegacyCase.Calls);
        Assert.Equal(9, LegacyCase.Position);
        Assert.Equal(0, CursorOverrideCase.Calls);
        Assert.Equal(1, CallbackCase.Calls);

        using var reference = new LuminBufferWriter(true);
        var writer = new LuminPackWriter(reference);
        writer.WriteCollectionHeader(source.Count);
        foreach (var value in source)
        {
            writer.CheckBuffer();
            if (value is null) writer.WriteNullUnionHeader();
            else value.__LuminPackUnionSerialize_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref writer);
        }
        Assert.Equal(actual, reference.DangerousGetBuffer()[..writer.Position].ToArray());
        var copy = LuminPackSerializer.Deserialize<List<CursorRoot?>>(actual);
        Assert.Equal(11, Assert.IsType<FixedCase>(copy[0]).Number);
        Assert.Equal(22, Assert.IsType<FixedCase>(copy[1]).Number);
        Assert.Equal(44, Assert.IsType<FixedCase>(copy[2]).Number);
        Assert.Equal(33, Assert.IsType<CallbackCase>(copy[3]).Number);
        Assert.Equal(new string('a', 40_000), Assert.IsType<VariableCase>(copy[4]).Text);
        Assert.Null(copy[5]);
    }

    /// <summary>Preserves each compiled string profile across dense and sparse Union dictionary paths.</summary>
    [Fact]
    public void CursorBridgePreservesCompiledStringProfiles()
    {
        CheckStringProfile<LuminPackUtf16LengthOptions>();
        CheckStringProfile<LuminPackUtf16TokenOptions>();
        CheckStringProfile<LuminPackUtf8LengthOptions>();
        CheckStringProfile<LuminPackUtf8TokenOptions>();
    }

    /// <summary>Respects declared root and member profiles before selecting a cursor fallback.</summary>
    [Fact]
    public void CursorBridgePreservesDeclaredProfiles()
    {
        CheckDeclaredProfile<LuminPackUtf16LengthOptions>();
        CheckDeclaredProfile<LuminPackUtf16TokenOptions>();
        CheckDeclaredProfile<LuminPackUtf8LengthOptions>();
        CheckDeclaredProfile<LuminPackUtf8TokenOptions>();
        CheckMemberProfile<LuminPackUtf16LengthOptions>();
        CheckMemberProfile<LuminPackUtf8LengthOptions>();
    }

    /// <summary>Compares dictionary paths when the Union root declares a non-default wire profile.</summary>
    /// <typeparam name="TProfile">The caller's compiled binary string profile.</typeparam>
    private static void CheckDeclaredProfile<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        const string text = "\u4e16\u754c-\u03c0";
        var dense = new Dictionary<int, Utf8CursorRoot?>
        {
            [1] = new Utf8FixedCase { Number = 11 },
            [2] = new Utf8VariableCase { Text = text },
            [3] = null
        };
        var sparse = new Dictionary<int, Utf8CursorRoot?>(dense) { [4] = new Utf8FixedCase { Number = 99 } };
        sparse.Remove(4);
        byte[] actual = LuminPackSerializer.Serialize<TProfile>(dense);
        Assert.Equal(LuminPackSerializer.Serialize<TProfile>(sparse), actual);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, Utf8CursorRoot?>, TProfile>(actual);
        Assert.Equal(11, Assert.IsType<Utf8FixedCase>(copy[1]).Number);
        Assert.Equal(text, Assert.IsType<Utf8VariableCase>(copy[2]).Text);
        Assert.Null(copy[3]);
    }

    /// <summary>Compares dictionary paths when a containing member fixes a different profile.</summary>
    /// <typeparam name="TProfile">The caller's compiled binary string profile.</typeparam>
    private static void CheckMemberProfile<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        const string text = "\u4e16\u754c-\u03c0";
        var dense = new Dictionary<int, CursorRoot?> { [1] = new FixedCase { Number = 11 }, [2] = new VariableCase { Text = text } };
        var sparse = new Dictionary<int, CursorRoot?>(dense) { [3] = new FixedCase { Number = 99 } };
        sparse.Remove(3);
        byte[] actual = LuminPackSerializer.Serialize<TProfile>(new CursorProfileContainer { Values = dense });
        Assert.Equal(LuminPackSerializer.Serialize<TProfile>(new CursorProfileContainer { Values = sparse }), actual);
        var copy = LuminPackSerializer.Deserialize<CursorProfileContainer, TProfile>(actual);
        Assert.Equal(11, Assert.IsType<FixedCase>(copy.Values[1]).Number);
        Assert.Equal(text, Assert.IsType<VariableCase>(copy.Values[2]).Text);
    }

    /// <summary>Compares dense and sparse dictionary wire bytes and reads the variable Union payload.</summary>
    /// <typeparam name="TProfile">The compiled binary string profile.</typeparam>
    private static void CheckStringProfile<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        const string text = "\u4e16\u754c-\u03c0";
        var dense = new Dictionary<int, CursorRoot?>
        {
            [1] = new FixedCase { Number = 11 },
            [2] = new VariableCase { Text = text },
            [3] = null
        };
        var sparse = new Dictionary<int, CursorRoot?>(dense) { [4] = new FixedCase { Number = 99 } };
        sparse.Remove(4);

        byte[] expected = LuminPackSerializer.Serialize<TProfile>(sparse);
        byte[] actual = LuminPackSerializer.Serialize<TProfile>(dense);
        Assert.Equal(expected, actual);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>, TProfile>(actual);
        Assert.Equal(11, Assert.IsType<FixedCase>(copy[1]).Number);
        Assert.Equal(text, Assert.IsType<VariableCase>(copy[2]).Text);
        Assert.Null(copy[3]);
    }

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
        CheckRegisteredProfile<LuminPackUtf8LengthOptions>();
        CheckRegisteredProfile<LuminPackUtf8TokenOptions>();
    }

    /// <summary>Retains registered runtime dispatch and its published cursor under a non-default profile.</summary>
    /// <typeparam name="TProfile">The caller's compiled binary string profile.</typeparam>
    private static void CheckRegisteredProfile<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        var dense = new Dictionary<int, CursorRoot?> { [1] = new FixedCase { Number = 11 }, [2] = new RegisteredCase { Number = 71 } };
        var sparse = new Dictionary<int, CursorRoot?>(dense) { [3] = new FixedCase { Number = 99 } };
        sparse.Remove(3);
        byte[] actual = LuminPackSerializer.Serialize<TProfile>(dense);
        Assert.Equal(17, RegisteredCase.Position);
        Assert.Equal(LuminPackSerializer.Serialize<TProfile>(sparse), actual);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, CursorRoot?>, TProfile>(actual);
        Assert.Equal(71, Assert.IsType<RegisteredCase>(copy[2]).Number);
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

/// <summary>Provides large fixed cases that exceed one element's ordinary growth headroom.</summary>
[LuminPackable, LuminPackUnion(1, typeof(CapacityCase))]
public abstract partial class CapacityRoot { }

/// <summary>Retains the complete raw large payload and its following fixed field.</summary>
[LuminPackable]
public sealed partial class CapacityCase : CapacityRoot { public CapacityPayload Value; public int Tail; }

/// <summary>Retains a large native storage size independent of its one populated field.</summary>
[LuminPackable, StructLayout(LayoutKind.Sequential, Size = 20000)]
public struct CapacityPayload { public int Marker; }

/// <summary>Exercises the maximum three-byte Union header at the growth threshold.</summary>
[LuminPackable, LuminPackWideTag, LuminPackUnion(60000, typeof(WideCapacityPrefix)), LuminPackUnion(60001, typeof(WideCapacityTail))]
public abstract partial class WideCapacityRoot { }

/// <summary>Positions the following record exactly at the initial buffer's check threshold.</summary>
[LuminPackable] public sealed partial class WideCapacityPrefix : WideCapacityRoot { public CapacityPrefix Value; }

/// <summary>Exceeds the remaining room by one byte when its wide header is included.</summary>
[LuminPackable] public sealed partial class WideCapacityTail : WideCapacityRoot { public CapacityTail Value; }

/// <summary>Sets the first wide record's exact raw width without alignment padding.</summary>
[LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28665)]
public struct CapacityPrefix { public int Marker; }

/// <summary>Sets the second wide record's exact raw width without alignment padding.</summary>
[LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4094)]
public struct CapacityTail { public int Marker; }

/// <summary>Defines a Union whose declared binary wire profile differs from the assembly default.</summary>
[LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token),
 LuminPackUnion(1, typeof(Utf8FixedCase)), LuminPackUnion(2, typeof(Utf8VariableCase))]
public abstract partial class Utf8CursorRoot { }

/// <summary>Provides a fixed payload for the declared UTF-8 Union.</summary>
[LuminPackable] public sealed partial class Utf8FixedCase : Utf8CursorRoot { public int Number; }

/// <summary>Provides a string payload for the declared UTF-8 Union.</summary>
[LuminPackable] public sealed partial class Utf8VariableCase : Utf8CursorRoot { public string? Text; }

/// <summary>Overrides the Union dictionary's string profile at a containing member.</summary>
[LuminPackable]
public partial class CursorProfileContainer
{
    /// <summary>Stores dictionary values under the declared member profile.</summary>
    [LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.Token)]
    public Dictionary<int, CursorRoot?> Values = new();
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

/// <summary>Observes whether list writes invoke the separate public cursor slot.</summary>
public sealed class CursorOverrideCase : FixedCase
{
    /// <summary>Counts invocations of the cursor slot.</summary>
    public static int Calls;

    /// <summary>Counts a cursor-slot invocation before forwarding to the original implementation.</summary>
    /// <param name="writer">The caller's active binary writer.</param>
    /// <param name="initialIndex">The incoming absolute cursor.</param>
    /// <returns>The cursor returned by the inherited implementation.</returns>
    public override int __LuminPackUnionSerializeCursor_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref LuminPackWriter writer, int initialIndex)
    {
        Calls++;
        return base.__LuminPackUnionSerializeCursor_globalLuminPackUnitTestFixedUnionCursorsCursorRoot_90246b56(ref writer, initialIndex);
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
