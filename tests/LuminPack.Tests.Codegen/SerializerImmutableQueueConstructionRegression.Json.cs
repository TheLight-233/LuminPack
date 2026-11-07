/// <summary>Exercises pooled immutable sequence construction through both JSON encodings.</summary>
internal static partial class SerializerImmutableQueueConstructionRegression
{
    private const string JsonSource = """
        [LuminPackable]
        public class SequenceGraph
        {
            public global::System.Collections.Immutable.ImmutableQueue<int> Queue;
            public global::System.Collections.Immutable.IImmutableQueue<int> QueueContract;
            public global::System.Collections.Immutable.ImmutableStack<int> Stack;
            public global::System.Collections.Immutable.IImmutableStack<int> StackContract;
        }
        public static class SequenceJsonCases
        {
            public static void Check()
            {
                CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8LengthOptions>();
            }
            private static void CheckProfile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var encoding = default(TProfile).StringEncoding == LuminPackStringEncoding.UTF8
                    ? global::System.Text.Encoding.UTF8 : global::System.Text.Encoding.Unicode;
                foreach (int length in new[] { 0, 1, 2, 17, 257 })
                {
                    var values = Enumerable.Range(0, length).ToArray();
                    var queue = global::System.Collections.Immutable.ImmutableQueue.Create(values);
                    var stack = StackOracle(values);
                    var oldQueue = global::System.Collections.Immutable.ImmutableQueue.Create(999);
                    var oldStack = global::System.Collections.Immutable.ImmutableStack.Create(999);
                    var input = encoding.GetBytes(LuminPackSerializer.SerializeJson<ImmutableQueue<int>, TProfile>(queue));
                    if (LuminPackSerializer.SerializeJson<ImmutableStack<int>, TProfile>(stack) != LuminPackSerializer.SerializeJson<ImmutableQueue<int>, TProfile>(queue))
                        throw new Exception("Stack top-first JSON order changed.");
                    var restoredQueue = oldQueue;
                    var restoredStack = oldStack;
                    global::System.Collections.Immutable.IImmutableQueue<int> queueContract = oldQueue;
                    global::System.Collections.Immutable.IImmutableStack<int> stackContract = oldStack;
                    LuminPackSerializer.DeserializeJson<ImmutableQueue<int>, TProfile>(input, ref restoredQueue);
                    LuminPackSerializer.DeserializeJson<ImmutableStack<int>, TProfile>(input, ref restoredStack);
                    LuminPackSerializer.DeserializeJson<IImmutableQueue<int>, TProfile>(input, ref queueContract);
                    LuminPackSerializer.DeserializeJson<IImmutableStack<int>, TProfile>(input, ref stackContract);
                    if (!restoredQueue.SequenceEqual(values) || !restoredStack.SequenceEqual(values) ||
                        !queueContract.SequenceEqual(values) || !stackContract.SequenceEqual(values))
                        throw new Exception("Concrete or interface JSON sequence order changed.");
                    if (oldQueue.Peek() != 999 || oldStack.Peek() != 999)
                        throw new Exception("Reading changed an existing immutable value.");
                    if (!restoredQueue.Enqueue(7).SequenceEqual(values.Append(7)) || !restoredStack.Push(7).SequenceEqual(new[] { 7 }.Concat(values)))
                        throw new Exception("The result no longer supports subsequent operations.");
                    if (length > 0 && (!restoredQueue.Dequeue().SequenceEqual(values.Skip(1)) || !restoredStack.Pop().SequenceEqual(values.Skip(1))))
                        throw new Exception("Removing the first sequence element changed.");
                    var graph = new SequenceGraph { Queue = queue, Stack = stack, QueueContract = queue, StackContract = stack };
                    var graphInput = encoding.GetBytes(LuminPackSerializer.SerializeJson<SequenceGraph, TProfile>(graph));
                    SequenceGraph restoredGraph = null;
                    LuminPackSerializer.DeserializeJson<SequenceGraph, TProfile>(graphInput, ref restoredGraph);
                    if (!restoredGraph.Queue.SequenceEqual(values) || !restoredGraph.Stack.SequenceEqual(values) ||
                        !restoredGraph.QueueContract.SequenceEqual(values) || !restoredGraph.StackContract.SequenceEqual(values))
                        throw new Exception("Nested immutable sequence construction changed.");
                    if (length == 17 && typeof(SequenceJsonCases).Assembly.GetType("System.Collections.Immutable.ImmutableQueue") == null)
                    {
                        long necessary = Allocations(() => { GC.KeepAlive(QueueOracle(values)); GC.KeepAlive(QueueOracle(values)); GC.KeepAlive(StackOracle(values)); GC.KeepAlive(StackOracle(values)); });
                        long actual = Allocations(() => {
                            LuminPackSerializer.DeserializeJson<ImmutableQueue<int>, TProfile>(input, ref restoredQueue);
                            LuminPackSerializer.DeserializeJson<ImmutableStack<int>, TProfile>(input, ref restoredStack);
                            LuminPackSerializer.DeserializeJson<IImmutableQueue<int>, TProfile>(input, ref queueContract);
                            LuminPackSerializer.DeserializeJson<IImmutableStack<int>, TProfile>(input, ref stackContract);
                        });
                        if (actual != necessary) throw new Exception("JSON immutable construction allocated temporary objects: " + actual + " vs " + necessary);
                        byte[] binary = LuminPackSerializer.Serialize<ImmutableQueue<int>, TProfile>(queue);
                        actual = Allocations(() => {
                            LuminPackSerializer.Deserialize<ImmutableQueue<int>, TProfile>(binary, ref restoredQueue);
                            LuminPackSerializer.Deserialize<ImmutableStack<int>, TProfile>(binary, ref restoredStack);
                            LuminPackSerializer.Deserialize<IImmutableQueue<int>, TProfile>(binary, ref queueContract);
                            LuminPackSerializer.Deserialize<IImmutableStack<int>, TProfile>(binary, ref stackContract);
                        });
                        if (actual != necessary) throw new Exception("Binary immutable construction allocated temporary objects: " + actual + " vs " + necessary);
                    }
                }
                foreach (string text in new[] { "[1,\"broken\"]", "[1,{}]" })
                {
                    byte[] malformed = encoding.GetBytes(text);
                    var queue = global::System.Collections.Immutable.ImmutableQueue.Create(91);
                    var stack = global::System.Collections.Immutable.ImmutableStack.Create(92);
                    var priorQueue = queue;
                    var priorStack = stack;
                    global::System.Collections.Immutable.IImmutableQueue<int> queueContract = queue;
                    global::System.Collections.Immutable.IImmutableStack<int> stackContract = stack;
                    Throws(() => LuminPackSerializer.DeserializeJson<ImmutableQueue<int>, TProfile>(malformed, ref queue));
                    Throws(() => LuminPackSerializer.DeserializeJson<ImmutableStack<int>, TProfile>(malformed, ref stack));
                    Throws(() => LuminPackSerializer.DeserializeJson<IImmutableQueue<int>, TProfile>(malformed, ref queueContract));
                    Throws(() => LuminPackSerializer.DeserializeJson<IImmutableStack<int>, TProfile>(malformed, ref stackContract));
                    if (!ReferenceEquals(queue, priorQueue) || !ReferenceEquals(stack, priorStack) || !ReferenceEquals(queueContract, priorQueue) || !ReferenceEquals(stackContract, priorStack))
                        throw new Exception("Incomplete immutable input published a replacement.");
                }
                var words = new[] { "", "alpha", "\u65e5\u672c", "\U0001F30D", null };
                var stringQueue = global::System.Collections.Immutable.ImmutableQueue.Create(words);
                var wordInput = encoding.GetBytes(LuminPackSerializer.SerializeJson<ImmutableQueue<string>, TProfile>(stringQueue));
                global::System.Collections.Immutable.ImmutableStack<string> stringStack = null;
                global::System.Collections.Immutable.IImmutableStack<string> stringContract = null;
                LuminPackSerializer.DeserializeJson<ImmutableStack<string>, TProfile>(wordInput, ref stringStack);
                LuminPackSerializer.DeserializeJson<IImmutableStack<string>, TProfile>(wordInput, ref stringContract);
                LuminPackSerializer.DeserializeJson<ImmutableQueue<string>, TProfile>(wordInput, ref stringQueue);
                if (!stringStack.SequenceEqual(words) || !stringContract.SequenceEqual(words) || !stringQueue.SequenceEqual(words))
                    throw new Exception("Reference sequence elements changed.");
                byte[] absent = encoding.GetBytes("null");
                LuminPackSerializer.DeserializeJson<ImmutableStack<string>, TProfile>(absent, ref stringStack);
                LuminPackSerializer.DeserializeJson<IImmutableStack<string>, TProfile>(absent, ref stringContract);
                LuminPackSerializer.DeserializeJson<ImmutableQueue<string>, TProfile>(absent, ref stringQueue);
                if (stringStack != null || stringContract != null || stringQueue != null) throw new Exception("Null sequence changed.");
            }
            private static void Throws(Action action)
            {
                bool failed = false;
                try { action(); } catch { failed = true; }
                if (!failed) throw new Exception("Malformed immutable input was accepted.");
            }
            private static long Allocations(Action action)
            {
                for (int i = 0; i < 128; i++) action();
                long minimum = long.MaxValue;
                for (int sample = 0; sample < 3; sample++)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 128; i++) action();
                    minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
                }
                return minimum;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static global::System.Collections.Immutable.ImmutableQueue<int> QueueOracle(int[] values)
                => global::System.Collections.Immutable.ImmutableQueue.Create(values);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static global::System.Collections.Immutable.ImmutableStack<int> StackOracle(int[] values)
            {
                var stack = global::System.Collections.Immutable.ImmutableStack<int>.Empty;
                for (int i = values.Length - 1; i >= 0; i--) stack = stack.Push(values[i]);
                return stack;
            }
        }
        """;
}
