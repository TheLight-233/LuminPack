using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Protects reference-state ownership independently of stateless compiled profiles.</summary>
internal static class BufferWriterOperationContextTest
{
    /// <summary>Runs pooled, nested and concurrent operation-context regressions.</summary>
    /// <param name="results">The collected case outcomes.</param>
    internal static void Run(List<string> results)
    {
        RunCase(results, nameof(ReferenceStateResetIsConfigurationFree), ReferenceStateResetIsConfigurationFree);
        RunCase(results, nameof(PoolReturnClearsReferences), PoolReturnClearsReferences);
        RunCase(results, nameof(WriterStateIsCallerOwned), WriterStateIsCallerOwned);
        RunCase(results, nameof(JsonProfilesShareNoMutableConfiguration), JsonProfilesShareNoMutableConfiguration);
        RunCase(results, nameof(NestedBuffersKeepIndependentContexts), NestedBuffersKeepIndependentContexts);
        RunCase(results, nameof(ConcurrentBuffersKeepIndependentContexts), ConcurrentBuffersKeepIndependentContexts);
    }

    /// <summary>Checks both reset boundaries without binding serialization configuration to reference tables.</summary>
    private static void ReferenceStateResetIsConfigurationFree()
    {
        var reader = new LuminPackReaderOptionalState();
        var writer = new LuminPackWriterOptionalState();
        var marker = new object();
        foreach (bool fullReset in new[] { false, true })
        {
            reader.AddObjectReference(7, marker);
            Assert(!writer.GetOrAddReference(marker).existsReference, "Writer retained the previous operation.");
            Assert(ReferenceEquals(reader.GetObjectReference(7), marker), "Reader lost its active reference.");
            Assert(writer.GetOrAddReference(marker).existsReference, "Writer lost its active reference.");
            if (fullReset) { reader.Reset(); writer.Reset(); }
            else { reader.ResetOperationState(); writer.ResetOperationState(); }
            AssertThrows(() => reader.GetObjectReference(7), "Reset retained reader references.");
        }
        Assert(!writer.GetOrAddReference(marker).existsReference, "Reset retained writer reference ids.");
        writer.Reset();
        Assert(typeof(LuminBufferWriter).GetProperty("Option") == null &&
            typeof(LuminPackReaderOptionalState).GetProperty("Option") == null &&
            typeof(LuminPackWriterOptionalState).GetProperty("Option") == null,
            "Operation state retained a runtime configuration object.");
    }

    /// <summary>Verifies pool return clears both maps after a nondefault compiled operation.</summary>
    private static void PoolReturnClearsReferences()
    {
        var buffer = LuminBufferWriterPool.Rent();
        Write<LuminPackUtf16TokenOptions>(buffer, "context \uD83D\uDE00");
        Assert(Read<LuminPackUtf16TokenOptions>(buffer) == "context \uD83D\uDE00", "Compiled-profile round trip failed.");
        var marker = new object();
        buffer.WriterState.GetOrAddReference(marker);
        buffer.ReaderState.AddObjectReference(999, marker);
        LuminBufferWriterPool.Return(buffer);
        var reused = LuminBufferWriterPool.Rent();
        try
        {
            Assert(!reused.WriterState.GetOrAddReference(marker).existsReference, "Return retained writer references.");
            AssertThrows(() => reused.ReaderState.GetObjectReference(999), "Return retained reader references.");
            LuminPackSerializer.Serialize("default", reused);
            Assert(LuminPackSerializer.Deserialize<string>(reused) == "default", "A reused buffer inherited a compiled profile.");
        }
        finally { LuminBufferWriterPool.Return(reused); }
    }

    /// <summary>Verifies writes retain caller state while reads retain their established cleanup boundary.</summary>
    private static void WriterStateIsCallerOwned()
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            var marker = new object();
            buffer.WriterState.GetOrAddReference(marker);
            LuminPackSerializer.Serialize(42, buffer);
            Assert(buffer.WriterState.GetOrAddReference(marker).existsReference, "Serialize cleared caller references.");
            buffer.ReaderState.AddObjectReference(123, marker);
            Assert(LuminPackSerializer.Deserialize<int>(buffer) == 42, "Buffer round trip failed.");
            AssertThrows(() => buffer.ReaderState.GetObjectReference(123), "Deserialize retained references.");
        }
        finally { LuminBufferWriterPool.Return(buffer); }
    }

    /// <summary>Checks alternating document encodings without changing the output owner's state.</summary>
    private static void JsonProfilesShareNoMutableConfiguration()
    {
        using var buffer = new LuminBufferWriter(true);
        const string expected = "JSON context \u4E2D\u6587 \uD83D\uDE00";
        var utf16 = new TestSerializationProfile { StringEncoding = LuminPackStringEncoding.UTF16 };
        LuminPackSerializer.SerializeJson(expected, buffer, utf16);
        Assert(LuminPackSerializer.DeserializeJson<string>(System.Text.Encoding.Unicode.GetString(buffer.GetSpan()), utf16) == expected,
            "UTF-16 document round trip failed.");
        buffer.ResetCore();
        LuminPackSerializer.SerializeJson(expected, buffer);
        Assert(LuminPackSerializer.DeserializeJson<string>(buffer) == expected, "The default document inherited UTF-16.");
    }

    /// <summary>Checks that an active nested operation cannot replace its parent's data or compiled policy.</summary>
    private static void NestedBuffersKeepIndependentContexts()
    {
        var outer = LuminBufferWriterPool.Rent();
        try
        {
            Write<LuminPackUtf16LengthOptions>(outer, "outer \uD83D\uDE00");
            var inner = LuminBufferWriterPool.Rent();
            try
            {
                Assert(!ReferenceEquals(outer, inner), "Nested Rent returned the active buffer.");
                Write<LuminPackUtf8TokenOptions>(inner, "inner \u4E2D\u6587");
                Assert(Read<LuminPackUtf8TokenOptions>(inner) == "inner \u4E2D\u6587", "Nested profile failed.");
            }
            finally { LuminBufferWriterPool.Return(inner); }
            Assert(Read<LuminPackUtf16LengthOptions>(outer) == "outer \uD83D\uDE00", "Nested operation corrupted the outer buffer.");
        }
        finally { LuminBufferWriterPool.Return(outer); }
    }

    /// <summary>Exercises independent closed profiles on concurrently rented buffers.</summary>
    private static void ConcurrentBuffersKeepIndependentContexts()
    {
        var errors = new ConcurrentQueue<Exception>();
        Parallel.For(0, Math.Max(4, Environment.ProcessorCount), worker =>
        {
            try
            {
                for (int iteration = 0; iteration < 1000; iteration++)
                {
                    var buffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        string expected = $"worker:{worker};iteration:{iteration};\uD83D\uDE00";
                        if ((worker & 1) == 0)
                        {
                            Write<LuminPackDefaultOptions>(buffer, expected);
                            Assert(Read<LuminPackDefaultOptions>(buffer) == expected, "Concurrent UTF-8 profile failed.");
                        }
                        else
                        {
                            Write<LuminPackUtf16TokenOptions>(buffer, expected);
                            Assert(Read<LuminPackUtf16TokenOptions>(buffer) == expected, "Concurrent UTF-16 profile failed.");
                        }
                    }
                    finally { LuminBufferWriterPool.Return(buffer); }
                }
            }
            catch (Exception error) { errors.Enqueue(error); }
            finally { LuminBufferWriterPool.ClearThreadLocalCache(); }
        });
        if (errors.TryDequeue(out var error)) throw error;
    }

    /// <summary>Writes a closed compiled profile into the caller's existing buffer.</summary>
    /// <typeparam name="TOptions">The stateless binary profile.</typeparam>
    /// <param name="buffer">The output owner.</param>
    /// <param name="value">The payload.</param>
    private static void Write<TOptions>(LuminBufferWriter buffer, string value) where TOptions : struct, ILuminPackOptions
    {
        var writer = new LuminPackWriter(buffer, buffer.WriterState);
        try { LuminPackSerializer.Serialize<string, TOptions>(ref writer, in value); buffer.CompleteWrite(writer.CurrentIndex); }
        finally { buffer.WriterState.ResetOperationState(); }
    }

    /// <summary>Reads the caller's bytes with the same statically selected profile.</summary>
    /// <typeparam name="TOptions">The stateless binary profile.</typeparam>
    /// <param name="buffer">The input owner.</param>
    /// <returns>The decoded payload.</returns>
    private static string Read<TOptions>(LuminBufferWriter buffer) where TOptions : struct, ILuminPackOptions
    {
        ReadOnlySpan<byte> bytes = buffer.GetSpan();
        var reader = new LuminPackReader(ref bytes, buffer.ReaderState);
        string value = null!;
        try { LuminPackSerializer.Deserialize<string, TOptions>(ref reader, ref value); return value; }
        finally { buffer.ReaderState.ResetOperationState(); }
    }

    /// <summary>Requires the operation to reject its missing reference.</summary>
    /// <param name="action">The failing lookup.</param>
    /// <param name="message">The assertion failure description.</param>
    private static void AssertThrows(Action action, string message)
    {
        try { action(); } catch { return; }
        throw new InvalidOperationException(message);
    }

    /// <summary>Records a standalone regression outcome.</summary>
    /// <param name="results">The collected outcomes.</param>
    /// <param name="name">The case name.</param>
    /// <param name="test">The regression.</param>
    private static void RunCase(List<string> results, string name, Action test)
    {
        try { test(); results.Add($"✓ {name} - PASSED"); }
        catch (Exception error) { results.Add($"✗ {name} - ERROR: {error.Message}"); }
    }

    /// <summary>Rejects a broken operation-context invariant.</summary>
    /// <param name="condition">The required condition.</param>
    /// <param name="message">The assertion failure description.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
