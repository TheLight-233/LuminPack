#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the typed concurrent-stack range push against the framework range push.</summary>
public sealed class ConcurrentStackRangePushTest
{
    /// <summary>Pushes staged ranges onto empty and populated stacks and compares contents and order.</summary>
    [Fact]
    public void RangePushMatchesFrameworkPushRange()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 16, 17, 257 })
        {
            Check(count, index => "s" + index);
            Check(count, index => (object)("o" + index));
            Check(count, index => index);
        }
    }

    /// <summary>Publishes complete ranges while other threads push and pop, so contended publication reaches the framework retry path.</summary>
    [Fact]
    public void ContendedRangePushesKeepEveryItem()
    {
        var stack = new ConcurrentStack<string>();
        const int Batches = 4000, Batch = 7;
        long popped = 0;
        var pushers = Enumerable.Range(0, 4).Select(thread => Task.Run(() =>
        {
            var items = new string[Batch];
            for (int batch = 0; batch < Batches; batch++)
            {
                for (int index = 0; index < Batch; index++)
                    items[index] = thread + ":" + batch + ":" + index;
                if ((batch & 1) == 0)
                    LuminPackCollectionAccess.PushConcurrentStackRange(stack, items, Batch);
                else
                    stack.PushRange(items, 0, Batch);
            }
        })).ToArray();
        var popper = Task.Run(() =>
        {
            var buffer = new string[5];
            while (!pushers.All(task => task.IsCompleted))
                Interlocked.Add(ref popped, stack.TryPopRange(buffer));
        });
        Task.WaitAll(pushers);
        popper.Wait();
        Assert.Equal(4L * Batches * Batch, popped + stack.Count);
    }

    /// <summary>Round-trips string stacks through binary and JSON reads into fresh and reused targets.</summary>
    [Fact]
    public void StringStackReadsKeepEnumerationOrder()
    {
        foreach (int count in new[] { 1, 8, 256 })
        {
            var source = new ConcurrentStack<string>(Enumerable.Range(0, count).Select(index => "value-" + index));
            var reused = new ConcurrentStack<string>(new[] { "old" });
            Assert.Equal(source.ToArray(), ConcreteReads.Read<ConcurrentStack<string>>(LuminPackSerializer.Serialize(source),
                static (ReadOnlySpan<byte> input, ref ConcurrentStack<string> target) => LuminPackSerializer.Deserialize(input, ref target))!.ToArray());
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref reused);
            Assert.Equal(source.ToArray(), reused.ToArray());
            var json = new ConcurrentStack<string>(new[] { "old" });
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref json);
            Assert.Equal(source.ToArray(), json.ToArray());
        }
    }

    /// <summary>Compares one helper publication with the framework range push.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The staged item count.</param>
    /// <param name="create">Creates the item at an index.</param>
    private static void Check<T>(int count, Func<int, T> create)
    {
        var staged = new T[count + 5];
        for (int index = 0; index < count; index++)
            staged[index] = create(index);
        foreach (bool populated in new[] { false, true })
        {
            var expected = new ConcurrentStack<T>();
            var actual = new ConcurrentStack<T>();
            if (populated)
            {
                expected.Push(create(-1));
                actual.Push(create(-1));
            }
            expected.PushRange(staged, 0, count);
            LuminPackCollectionAccess.PushConcurrentStackRange(actual, staged, count);
            Assert.Equal(expected.ToArray(), actual.ToArray());
        }
    }
}
#endif
