/// <summary>Checks storage-boundary behavior independently of the generated integer loop.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    private const string BoundarySource = """
        public static partial class SequenceEntry
        {
            private static void BoundaryProfile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                foreach (bool wide in new[] { false, true })
                {
                    for (int capacity = 0; capacity <= 96; capacity++)
                    {
                        string expected = FixedState<TProfile>(capacity,wide,false), actual = FixedState<TProfile>(capacity,wide,true);
                        if (expected != actual) throw new Exception("Fixed integer boundary changed: " + capacity + "/" + wide + "/" + expected + "/" + actual);
                    }
                    foreach (int initial in new[] { 1, 11, 12, 20, 21, 32 })
                    for (int failure = 0; failure <= 3; failure++)
                    {
                        string expected = StreamingState<TProfile>(initial,failure,wide,false), actual = StreamingState<TProfile>(initial,failure,wide,true);
                        if (expected != actual) throw new Exception("Streaming integer boundary changed: " + initial + "/" + failure + "/" + wide);
                    }
                }
            }
            private static unsafe string FixedState<TProfile>(int capacity,bool wide,bool generated) where TProfile : struct, ILuminPackOptions
            {
                byte[] storage = Enumerable.Repeat((byte)0xA5,capacity+16).ToArray();
                fixed (byte* address = storage)
                {
                    var span = new Span<byte>(address,capacity); var writer = new LuminPackJsonWriter(ref span,new LuminPackWriterOptionalState());
                    string error = "success";
                    try { WriteBoundary<TProfile>(ref writer,wide,generated,4); }
                    catch (Exception failure) { error = failure.GetType().FullName + ":" + (failure as ArgumentException)?.ParamName; }
                    if (storage.Skip(capacity).Any(value => value != 0xA5)) throw new Exception("An integer write escaped its provided span.");
                    return State(ref writer,error);
                }
            }
            private static string StreamingState<TProfile>(int initial,int failure,bool wide,bool generated) where TProfile : struct, ILuminPackOptions
            {
                var sink = new IntegerSink(failure); var writer = new LuminPackJsonWriter(sink,new LuminPackWriterOptionalState(),initial);
                string error = "success";
                try
                {
                    try { WriteBoundary<TProfile>(ref writer,wide,generated,600); writer.FlushToBufferWriter(); }
                    catch (Exception exception)
                    {
                        if (exception is System.IO.IOException && !ReferenceEquals(exception,sink.Failure)) throw new Exception("Streaming failure identity changed.");
                        error = exception.GetType().FullName + ":" + (exception as ArgumentException)?.ParamName;
                    }
                    return State(ref writer,error) + ":" + Convert.ToBase64String(sink.Bytes.ToArray()) + ":" + string.Join(",",sink.Events);
                }
                finally { writer.Dispose(); }
            }
            private static string State(ref LuminPackJsonWriter writer,string error)
                => error + ":" + writer.Position + ":" + writer.BytesWritten + ":" + writer.Depth + ":" + writer.IsFirstElement + ":" + Convert.ToBase64String(writer.Buffer.Slice(0,writer.Position).ToArray());
            private static void WriteBoundary<TProfile>(ref LuminPackJsonWriter writer,bool wide,bool generated,int count) where TProfile : struct, ILuminPackOptions
            {
                if (wide)
                {
                    var values = new Queue<long>(Enumerable.Range(0,count).Select(i => i % 3 == 0 ? long.MinValue : i % 3 == 1 ? 1L : long.MaxValue));
                    if (generated) LuminPackSerializer.SerializeJson<Queue<long>, TProfile>(ref writer,in values);
                    else
                    {
                        writer.WriteArrayStart<TProfile>(); bool separate = false;
                        foreach (long item in values)
                        {
                            if (separate) writer.WriteByteRaw<TProfile>((byte)','); separate = true;
                            writer.SetFirstElement(true); writer.Write<TProfile>(item);
                        }
                        writer.WriteArrayEnd<TProfile>();
                    }
                }
                else
                {
                    var values = new Queue<int>(Enumerable.Range(0,count).Select(i => i % 3 == 0 ? int.MinValue : i % 3 == 1 ? 1 : int.MaxValue));
                    if (generated) LuminPackSerializer.SerializeJson<Queue<int>, TProfile>(ref writer,in values);
                    else
                    {
                        writer.WriteArrayStart<TProfile>(); bool separate = false;
                        foreach (int item in values)
                        {
                            if (separate) writer.WriteByteRaw<TProfile>((byte)','); separate = true;
                            writer.SetFirstElement(true); writer.Write<TProfile>(item);
                        }
                        writer.WriteArrayEnd<TProfile>();
                    }
                }
            }
        }
        public sealed class IntegerSink : System.Buffers.IBufferWriter<byte>
        {
            public readonly Exception Failure = new System.IO.IOException("Selected output failure.");
            public readonly List<byte> Bytes = new List<byte>(); public readonly List<string> Events = new List<string>();
            private readonly int failAt; private int requests; private byte[] current;
            public IntegerSink(int failAt) { this.failAt=failAt; }
            private byte[] Next(int sizeHint)
            {
                Events.Add("get:"+sizeHint); requests++;
                if (failAt != 0 && requests == failAt+1) throw Failure;
                return current = new byte[Math.Max(1,sizeHint)];
            }
            public Span<byte> GetSpan(int sizeHint=0) => Next(sizeHint);
            public Memory<byte> GetMemory(int sizeHint=0) => Next(sizeHint);
            public void Advance(int count) { Events.Add("advance:"+count); for(int i=0;i<count;i++)Bytes.Add(current[i]); }
        }
        """;
}
