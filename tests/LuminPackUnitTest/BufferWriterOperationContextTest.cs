using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

internal static class BufferWriterOperationContextTest
{
    internal static void Run(List<string> results)
    {
        RunCase(results, nameof(DefaultAndCustomOptionsResetOnReturn), DefaultAndCustomOptionsResetOnReturn);
        RunCase(results, nameof(WriterAndReaderStateResetPerOperation), WriterAndReaderStateResetPerOperation);
        RunCase(results, nameof(JsonUsesBufferOwnedOption), JsonUsesBufferOwnedOption);
        RunCase(results, nameof(NestedBuffersKeepIndependentContexts), NestedBuffersKeepIndependentContexts);
        RunCase(results, nameof(ConcurrentBuffersKeepIndependentContexts), ConcurrentBuffersKeepIndependentContexts);
    }

    private static void DefaultAndCustomOptionsResetOnReturn()
    {
        var writer = LuminBufferWriterPool.Rent();
        AssertDefault(writer.Option);
        Assert(ReferenceEquals(writer.Option, writer.WriterState.Option) &&
               ReferenceEquals(writer.Option, writer.ReaderState.Option),
            "WriterState and ReaderState do not share the BufferWriter-owned option instance.");

        writer.Option.StringEncoding = LuminPackStringEncoding.UTF16;
        writer.Option.StringRecording = LuminPackStringRecording.Token;
        LuminPackSerializer.Serialize("context 😀", writer);
        Assert(LuminPackSerializer.Deserialize<string>(writer) == "context 😀",
            "The BufferWriter option was not shared by serialize and deserialize.");
        Assert(writer.Option.StringEncoding == LuminPackStringEncoding.UTF16 &&
               writer.Option.StringRecording == LuminPackStringRecording.Token,
            "A high-performance operation reset the option before Return.");

        var marker = new object();
        writer.WriterState.GetOrAddReference(marker);
        writer.ReaderState.AddObjectReference(999, marker);

        LuminBufferWriterPool.Return(writer);
        var reused = LuminBufferWriterPool.Rent();
        try
        {
            AssertDefault(reused.Option);
            Assert(!reused.WriterState.GetOrAddReference(marker).existsReference,
                "Writer state was not cleared by Return.");
            AssertThrows(() => reused.ReaderState.GetObjectReference(999),
                "Reader state was not cleared by Return.");
        }
        finally
        {
            LuminBufferWriterPool.Return(reused);
        }
    }

    private static void JsonUsesBufferOwnedOption()
    {
        var writer = LuminBufferWriterPool.Rent();
        try
        {
            writer.Option.StringEncoding = LuminPackStringEncoding.UTF16;
            const string expected = "JSON context 中文 😀";
            LuminPackSerializer.SerializeJson(expected, writer);
            Assert(LuminPackSerializer.DeserializeJson<string>(writer) == expected,
                "JSON BufferWriter APIs did not share the BufferWriter-owned UTF-16 option.");
            Assert(writer.Option.StringEncoding == LuminPackStringEncoding.UTF16,
                "JSON operation reset the BufferWriter option before Return.");
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }
    }

    private static void WriterAndReaderStateResetPerOperation()
    {
        var writer = LuminBufferWriterPool.Rent();
        try
        {
            var marker = new object();
            Assert(!writer.WriterState.GetOrAddReference(marker).existsReference,
                "Writer state unexpectedly contained a reference before the operation.");

            LuminPackSerializer.Serialize(42, writer);
            Assert(!writer.WriterState.GetOrAddReference(marker).existsReference,
                "Writer reference state leaked across serialize operations.");

            writer.ReaderState.AddObjectReference(123, marker);
            Assert(LuminPackSerializer.Deserialize<int>(writer) == 42,
                "The binary BufferWriter round-trip failed.");
            AssertThrows(() => writer.ReaderState.GetObjectReference(123),
                "Reader reference state leaked across deserialize operations.");
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }
    }

    private static void NestedBuffersKeepIndependentContexts()
    {
        var outer = LuminBufferWriterPool.Rent();
        try
        {
            outer.Option.StringEncoding = LuminPackStringEncoding.UTF16;
            LuminPackSerializer.Serialize("outer 😀", outer);

            var inner = LuminBufferWriterPool.Rent();
            try
            {
                Assert(!ReferenceEquals(outer, inner), "Nested Rent returned the active outer BufferWriter.");
                inner.Option.StringEncoding = LuminPackStringEncoding.UTF8;
                inner.Option.StringRecording = LuminPackStringRecording.Token;
                LuminPackSerializer.Serialize("inner 中文", inner);
                Assert(LuminPackSerializer.Deserialize<string>(inner) == "inner 中文",
                    "The nested BufferWriter failed to round-trip with its own option.");
            }
            finally
            {
                LuminBufferWriterPool.Return(inner);
            }

            Assert(outer.Option.StringEncoding == LuminPackStringEncoding.UTF16,
                "The nested operation changed the outer BufferWriter option.");
            Assert(LuminPackSerializer.Deserialize<string>(outer) == "outer 😀",
                "The nested operation corrupted the outer BufferWriter state or payload.");
        }
        finally
        {
            LuminBufferWriterPool.Return(outer);
        }
    }

    private static void ConcurrentBuffersKeepIndependentContexts()
    {
        var errors = new ConcurrentQueue<Exception>();
        Parallel.For(0, Math.Max(4, Environment.ProcessorCount), worker =>
        {
            try
            {
                for (var i = 0; i < 1_000; i++)
                {
                    var writer = LuminBufferWriterPool.Rent();
                    try
                    {
                        writer.Option.StringEncoding = (worker & 1) == 0
                            ? LuminPackStringEncoding.UTF8
                            : LuminPackStringEncoding.UTF16;
                        writer.Option.StringRecording = (i & 1) == 0
                            ? LuminPackStringRecording.Length
                            : LuminPackStringRecording.Token;

                        var expected = $"worker:{worker};iteration:{i};😀";
                        LuminPackSerializer.Serialize(expected, writer);
                        if (LuminPackSerializer.Deserialize<string>(writer) != expected)
                            throw new InvalidOperationException("Concurrent BufferWriter context was corrupted.");
                    }
                    finally
                    {
                        LuminBufferWriterPool.Return(writer);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
            finally
            {
                LuminBufferWriterPool.ClearThreadLocalCache();
            }
        });

        if (errors.TryDequeue(out var error))
            throw error;
    }

    private static void AssertDefault(LuminPackSerializerOption option)
    {
        Assert(option.StringEncoding == LuminPackStringEncoding.UTF8,
            "A rented BufferWriter did not use the default UTF-8 encoding.");
        Assert(option.StringRecording == LuminPackStringRecording.Length,
            "A rented BufferWriter did not use length-prefixed strings by default.");
        Assert(option.StandardFormat.Symbol == 'G',
            "A rented BufferWriter did not use the default standard format.");
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
