using System.Text;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies operation-owned reference tracking during nested and suspended convenience calls.</summary>
public class NestedReferenceStateTest
{
    /// <summary>Checks that nested binary callbacks cannot clear the outer writer's object identities.</summary>
    [Fact]
    public void NestedBinaryWritePreservesAliases() => VerifyWrite(false);

    /// <summary>Checks that nested JSON callbacks cannot clear the outer writer's object identities.</summary>
    [Fact]
    public void NestedJsonWritePreservesAliases() => VerifyWrite(true);

    /// <summary>Checks that nested binary callbacks cannot clear references needed by later members.</summary>
    [Fact]
    public void NestedBinaryReadPreservesAliases() => VerifyRead(false);

    /// <summary>Checks that nested JSON callbacks cannot clear references needed by later members.</summary>
    [Fact]
    public void NestedJsonReadPreservesAliases() => VerifyRead(true);

    /// <summary>Checks independent writer state while another asynchronous operation is waiting on its stream.</summary>
    /// <returns>Completion of the suspended writer and independent-operation checks.</returns>
    [Fact]
    public async Task SuspendedWriteDoesNotContaminateTheNextOperation()
    {
        var value = Create();
        byte[] expected = LuminPackSerializer.Serialize(value);
        using var stream = new SuspendedStream();
        var pending = LuminPackSerializer.SerializeAsync(stream, value);
        try
        {
            Assert.False(pending.IsCompleted);
            Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        }
        finally
        {
            stream.Release();
            await pending;
        }
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
    }

    /// <summary>Writes a tracked graph while a child callback starts an independent write.</summary>
    /// <param name="json">Whether to use the JSON facade.</param>
    private static void VerifyWrite(bool json)
    {
        var value = Create();
        NestedReferenceLeaf.WriteNested = true;
        byte[] payload;
        try
        {
            payload = json ? Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(value)) : LuminPackSerializer.Serialize(value);
        }
        finally { NestedReferenceLeaf.WriteNested = false; }
        var copy = json ? LuminPackSerializer.DeserializeJson<NestedReferenceRoot>(payload) : LuminPackSerializer.Deserialize<NestedReferenceRoot>(payload);
        Assert.Same(copy.First, copy.Second);
        Assert.Same(copy, copy.Self);
    }

    /// <summary>Reads a tracked graph while a child callback starts an independent read.</summary>
    /// <param name="json">Whether to use the JSON facade.</param>
    private static void VerifyRead(bool json)
    {
        var value = Create();
        byte[] payload = json ? Encoding.Unicode.GetBytes(LuminPackSerializer.SerializeJson(value)) : LuminPackSerializer.Serialize(value);
        NestedReferenceLeaf.ReadNested = true;
        NestedReferenceRoot copy;
        try
        {
            copy = json ? LuminPackSerializer.DeserializeJson<NestedReferenceRoot>(payload) : LuminPackSerializer.Deserialize<NestedReferenceRoot>(payload);
        }
        finally { NestedReferenceLeaf.ReadNested = false; }
        Assert.Same(copy.First, copy.Second);
        Assert.Same(copy, copy.Self);
    }

    /// <summary>Constructs two shared child references and a root self-cycle.</summary>
    /// <returns>The graph used by both encodings.</returns>
    private static NestedReferenceRoot Create()
    {
        NestedReferenceLeaf.WriteNested = false;
        NestedReferenceLeaf.ReadNested = false;
        var leaf = new NestedReferenceLeaf { Value = 17 };
        var root = new NestedReferenceRoot { First = leaf, Second = leaf };
        root.Self = root;
        return root;
    }

    /// <summary>Suspends the first payload write without blocking the calling thread.</summary>
    private sealed class SuspendedStream : MemoryStream
    {
        private readonly TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes the pending payload write.</summary>
        internal void Release() => gate.TrySetResult(true);

        /// <summary>Retains the stream write until another independent serialization has run.</summary>
        /// <param name="buffer">The payload retained by the asynchronous facade.</param>
        /// <param name="cancellationToken">Cancellation for the underlying stream write.</param>
        /// <returns>Completion of the delayed stream write.</returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await gate.Task.ConfigureAwait(false);
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Provides a tracked graph whose aliases survive nested facade operations.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed class NestedReferenceRoot
{
    [LuminPackOrder(0)] public NestedReferenceLeaf First = null!;
    [LuminPackOrder(1)] public NestedReferenceLeaf Second = null!;
    [LuminPackOrder(2)] public NestedReferenceRoot Self = null!;
}

/// <summary>Starts independent operations after the outer graph has registered references.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed class NestedReferenceLeaf
{
    internal static bool WriteNested;
    internal static bool ReadNested;
    private static readonly byte[] NestedPayload = LuminPackSerializer.Serialize("nested");
    [LuminPackOrder(0)] public int Value;

    /// <summary>Executes nested binary and JSON writes using their own reference contexts.</summary>
    [LuminPackOnSerialized]
    public void AfterWrite()
    {
        if (!WriteNested) return;
        WriteNested = false;
        _ = LuminPackSerializer.Serialize("nested");
        _ = LuminPackSerializer.SerializeJson("nested");
    }

    /// <summary>Executes nested binary and JSON reads without discarding the outer reference map.</summary>
    [LuminPackOnDeserialized]
    public void AfterRead()
    {
        if (!ReadNested) return;
        ReadNested = false;
        _ = LuminPackSerializer.Deserialize<string>(NestedPayload);
        _ = LuminPackSerializer.DeserializeJson<string>("\"nested\"");
    }
}
