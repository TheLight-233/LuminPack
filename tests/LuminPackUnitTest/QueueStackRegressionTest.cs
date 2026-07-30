using System.Text.Json;
using LuminPack;

namespace LuminPackUnitTest;

internal static class QueueStackRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(WrappedUnmanagedQueueKeepsFifoOrder), WrappedUnmanagedQueueKeepsFifoOrder);
        RunCase(results, nameof(WrappedReferenceQueueKeepsFifoOrder), WrappedReferenceQueueKeepsFifoOrder);
        RunCase(results, nameof(EmptyAndNullQueuesStayDistinct), EmptyAndNullQueuesStayDistinct);
        RunCase(results, nameof(JsonQueueAndStackUseLogicalOrder), JsonQueueAndStackUseLogicalOrder);
    }

    private static void WrappedUnmanagedQueueKeepsFifoOrder()
    {
        var value = new Queue<int>(8);
        for (var i = 1; i <= 6; i++) value.Enqueue(i);
        value.Dequeue();
        value.Dequeue();
        value.Dequeue();
        value.Enqueue(7);
        value.Enqueue(8);
        value.Enqueue(9);

        var expected = value.ToArray();
        var roundTrip = LuminPackSerializer.Deserialize<Queue<int>>(LuminPackSerializer.Serialize(value));

        Assert(roundTrip is not null && roundTrip.SequenceEqual(expected),
            "A wrapped unmanaged Queue lost FIFO order during binary round-trip.");
        roundTrip!.Enqueue(10);
        Assert(roundTrip.SequenceEqual(expected.Append(10)),
            "A deserialized Queue had invalid head/tail state on the next Enqueue.");
    }

    private static void WrappedReferenceQueueKeepsFifoOrder()
    {
        var value = new Queue<string>(6);
        value.Enqueue("a");
        value.Enqueue("b");
        value.Enqueue("c");
        value.Enqueue("d");
        value.Dequeue();
        value.Dequeue();
        value.Enqueue("e");
        value.Enqueue("f");

        var expected = value.ToArray();
        var roundTrip = LuminPackSerializer.Deserialize<Queue<string>>(LuminPackSerializer.Serialize(value));
        Assert(roundTrip is not null && roundTrip.SequenceEqual(expected),
            "A wrapped reference Queue lost FIFO order during binary round-trip.");
    }

    private static void EmptyAndNullQueuesStayDistinct()
    {
        var empty = LuminPackSerializer.Deserialize<Queue<int>>(
            LuminPackSerializer.Serialize(new Queue<int>()));
        Assert(empty is not null && empty.Count == 0,
            "An empty Queue was decoded as null or attempted to read missing metadata.");

        Queue<int>? value = null;
        var nullQueue = LuminPackSerializer.Deserialize<Queue<int>?>(LuminPackSerializer.Serialize(value));
        Assert(nullQueue is null, "A null Queue was decoded as an empty Queue.");
    }

    private static void JsonQueueAndStackUseLogicalOrder()
    {
        var queue = new Queue<int>(5);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Dequeue();
        queue.Enqueue(4);
        using (var queueDocument = JsonDocument.Parse(LuminPackSerializer.SerializeJson(queue)))
        {
            var values = queueDocument.RootElement.EnumerateArray().Select(static item => item.GetInt32());
            Assert(values.SequenceEqual(queue), "Queue JSON used physical ring-buffer order.");
        }

        var stack = new Stack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        using var stackDocument = JsonDocument.Parse(LuminPackSerializer.SerializeJson(stack));
        var stackValues = stackDocument.RootElement.EnumerateArray().Select(static item => item.GetInt32());
        Assert(stackValues.SequenceEqual(stack), "Stack JSON used bottom-to-top physical storage order.");
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
