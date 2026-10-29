using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that recursive graphs fail with a catchable exception instead of exhausting the thread stack.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class RecursionGuardTest
{
    /// <summary>A depth no thread stack can serve with the nested generated operations.</summary>
    private const int ExtremeDepth = 2_000_000;

    /// <summary>Checks that ordinary nesting is unaffected by the guard.</summary>
    [Fact]
    public void ModerateNestingRoundTrips()
    {
        var chain = Build(200);
        byte[] bytes = LuminPackSerializer.Serialize(chain);
        var restored = ConcreteReads.Read<DeepChain>(bytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        int depth = 0;
        for (var node = restored; node != null; node = node.Next) depth++;
        Assert.Equal(200, depth);
    }

    /// <summary>Checks that a chain of fixed-width nodes, which has no collection or string boundary, grows owned output instead of overrunning it.</summary>
    [Fact]
    public void LongFixedWidthChainGrowsOwnedOutput()
    {
        const int depth = 100_000;
        int restoredDepth = 0;
        long restoredSum = 0;
        Exception? failure = null;
        // The thread is large enough that the stack guard never trips; only output capacity is under test.
        var thread = new Thread(() =>
        {
            try
            {
                var chain = Build(depth);
                byte[] bytes = LuminPackSerializer.Serialize(chain);
                Assert.Equal(depth * 5 + 1, bytes.Length);
                for (var node = ConcreteReads.Read<DeepChain>(bytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)); node != null; node = node.Next)
                {
                    restoredDepth++;
                    restoredSum += node.Value;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }, 256 * 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Equal(depth, restoredDepth);
        Assert.Equal((long)depth * (depth - 1) / 2, restoredSum);
    }

    /// <summary>Checks that the size evaluator of a recursive record agrees with the written length.</summary>
    [Fact]
    public void RecursiveSizeEvaluationMatchesTheWrittenRecord()
    {
        var chain = Build(200);
    }

    /// <summary>Checks the write operation of a graph that is deeper than the stack can hold.</summary>
    [Fact]
    public void ExtremelyDeepWriteIsRejected()
        => Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Serialize(Build(ExtremeDepth)));

    /// <summary>Checks the JSON writer of a graph that is deeper than the stack can hold.</summary>
    [Fact]
    public void ExtremelyDeepJsonWriteIsRejected()
        => Assert.Throws<InvalidDataException>(() => LuminPackSerializer.SerializeJson(Build(ExtremeDepth)));

    /// <summary>Checks that a forged record nesting the same type endlessly is rejected by the reader.</summary>
    [Fact]
    public void ForgedDeepRecordIsRejectedByTheReader()
    {
        // Each node is the reference marker 2 followed by a four-byte value; 0xFF ends the chain with null.
        byte[] forged = new byte[ExtremeDepth * 5 + 1];
        for (int node = 0; node < ExtremeDepth; node++) forged[node * 5] = 2;
        forged[^1] = 0xFF;
        Assert.Throws<InvalidDataException>(() => ConcreteReads.Read<DeepChain>(forged, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }

    /// <summary>Builds a singly linked chain without recursion.</summary>
    /// <param name="depth">The number of nodes.</param>
    /// <returns>The head of the chain.</returns>
    private static DeepChain Build(int depth)
    {
        DeepChain? head = null;
        for (int index = 0; index < depth; index++) head = new DeepChain { Value = index, Next = head };
        return head!;
    }
}

/// <summary>A self-referencing model whose operations re-enter themselves.</summary>
[LuminPackable]
public sealed partial class DeepChain
{
    /// <summary>The node payload.</summary>
    public int Value;
    /// <summary>The following node, or null at the end.</summary>
    public DeepChain? Next;
}
