using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LuminPack.Core;
using LuminPack.Option;

// A feasibility experiment, not a full serializer benchmark or a production patch.
// Independently implements the run-based strategy seen in RE:Dox JsonTextEncoder.
internal static class Program
{
    private static readonly SearchValues<char> EscapeChars = SearchValues.Create(
        new string(Enumerable.Range(0, 32).Select(x => (char)x).ToArray()) + "\"\\");
    private static readonly LuminPackWriterOptionalState State = new();
    private static int _sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Current(string value, byte[] buffer)
    {
        Span<byte> destination = buffer;
        var writer = new LuminPackJsonWriter(ref destination, State);
        writer.WriteString(value);
        return writer.CurrentIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Segmented(string value, byte[] buffer)
    {
        Span<byte> destination = buffer;
        var writer = new LuminPackJsonWriter(ref destination, State);
        writer.WriteByteRaw((byte)'"');
        int written = writer.CurrentIndex;
        ReadOnlySpan<char> remaining = value;
        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOfAny(EscapeChars);
            if (index < 0)
            {
                written += Encoding.UTF8.GetBytes(remaining, destination[written..]);
                break;
            }

            written += Encoding.UTF8.GetBytes(remaining[..index], destination[written..]);
            char escape = remaining[index];
            destination[written++] = (byte)'\\';
            switch (escape)
            {
                case '"': destination[written++] = (byte)'"'; break;
                case '\\': destination[written++] = (byte)'\\'; break;
                case '\n': destination[written++] = (byte)'n'; break;
                case '\r': destination[written++] = (byte)'r'; break;
                case '\t': destination[written++] = (byte)'t'; break;
                default:
                    // Preserve current LuminPack spelling, including backspace/formfeed.
                    destination[written++] = (byte)'u';
                    destination[written++] = (byte)'0';
                    destination[written++] = (byte)'0';
                    destination[written++] = Hex(escape >> 4);
                    destination[written++] = Hex(escape & 15);
                    break;
            }
            remaining = remaining[(index + 1)..];
        }
        writer.Advance(written - writer.CurrentIndex);
        writer.WriteByteRaw((byte)'"');
        return writer.CurrentIndex;
    }

    private static byte Hex(int digit) => (byte)(digit < 10 ? '0' + digit : 'a' + digit - 10);

    private static void Check(string value)
    {
        int capacity = checked(value.Length * 6 + 2);
        byte[] expected = new byte[capacity];
        byte[] actual = Enumerable.Repeat((byte)0xCC, capacity + 16).ToArray();
        int a = Current(value, expected);
        int b = Segmented(value, actual);
        if (a != b || !expected.AsSpan(0, a).SequenceEqual(actual.AsSpan(0, b)))
            throw new InvalidOperationException("Output differs for " + Convert.ToHexString(MemoryMarshal.AsBytes(value.AsSpan())));
        if (actual.AsSpan(b).ContainsAnyExcept((byte)0xCC))
            throw new InvalidOperationException("Canary modified");
        byte[] exact = new byte[a];
        if (Segmented(value, exact) != a || !expected.AsSpan(0, a).SequenceEqual(exact))
            throw new InvalidOperationException("Exact-size destination differs");
        string normalized = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value));
        if (JsonSerializer.Deserialize<string>(actual.AsSpan(0, b)) != normalized)
            throw new InvalidOperationException("JSON roundtrip differs");
    }

    private static void CheckCorrectness()
    {
        foreach (string value in new[] { "", "a", "\0", "\b\f\n\r\t\"\\", "中文", "🚀",
            "\uD800", "\uDC00", "\uD800X", "X\uDC00Y", "\uD800\n\uDC00", "\uDBFF\uDFFF" }) Check(value);
        var random = new Random(42);
        for (int test = 0; test < 2000; test++)
        {
            char[] chars = new char[random.Next(0, 257)];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = random.Next(4) == 0 ? (char)random.Next(0, 128) : (char)random.Next(0, 65536);
            Check(new string(chars));
        }
        Console.WriteLine("Correctness: 2012 edge/random cases passed (byte equality, JSON roundtrip, canary, exact-size destination).\n");
    }

    private static string Build(string pattern, int length)
    {
        var builder = new StringBuilder(length + pattern.Length);
        while (builder.Length < length) builder.Append(pattern);
        return builder.ToString(0, length); // Deliberate surrogate truncation is covered too.
    }

    private static double Measure(Func<string, byte[], int> encode, string input, byte[] output, int count)
    {
        int total = 0;
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) total = unchecked(total + encode(input, output));
        long end = Stopwatch.GetTimestamp();
        Volatile.Write(ref _sink, total);
        return (end - start) * (1_000_000_000.0 / Stopwatch.Frequency) / count;
    }

    private static double Median(List<double> samples) => samples.Order().ElementAt(samples.Count / 2);

    public static void Main()
    {
        CheckCorrectness();
        Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; CPU count {Environment.ProcessorCount}");
        Console.WriteLine($"DOTNET_TieredCompilation={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"}");
        Console.WriteLine("Preallocated buffers, reused optional state, 7 alternating rounds; median ns/op.\n");
        Console.WriteLine("| Data | UTF16 units | Current ns | Segmented ns | Speedup | Current B/op | Segmented B/op |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        var patterns = new (string Name, string Text)[] {
            ("Ascii", "LuminPack-Serialization0123456789"),
            ("Chinese", "极致性能序列化字符串编码中文数据游戏网络"),
            ("Mixed", "LuminPack极致性能Serialization序列化123"),
            ("Emoji", "LuminPack🚀🔥🎮性能🙂数据🌍"),
            ("SparseEscape", new string('中', 63) + "\n"),
            ("DenseEscape", "\n\t\"\\\0中🚀")
        };
        foreach (var pattern in patterns)
        foreach (int length in new[] { 8, 32, 128, 4096 })
        {
            string input = Build(pattern.Text, length);
            byte[] output = new byte[6 * input.Length + 2];
            Check(input);
            for (int warmup = 0; warmup < 15000; warmup++) { Current(input, output); Segmented(input, output); }
            double pilot = Math.Max(Measure(Current, input, output, 1000), Measure(Segmented, input, output, 1000));
            int iterations = Math.Clamp((int)(20_000_000 / Math.Max(1, pilot)), 1000, 2_000_000);
            var current = new List<double>();
            var segmented = new List<double>();
            for (int round = 0; round < 7; round++)
            {
                if (round % 2 == 0)
                {
                    current.Add(Measure(Current, input, output, iterations));
                    segmented.Add(Measure(Segmented, input, output, iterations));
                }
                else
                {
                    segmented.Add(Measure(Segmented, input, output, iterations));
                    current.Add(Measure(Current, input, output, iterations));
                }
            }
            long start = GC.GetAllocatedBytesForCurrentThread();
            Measure(Current, input, output, 1000);
            double allocationCurrent = (GC.GetAllocatedBytesForCurrentThread() - start) / 1000.0;
            start = GC.GetAllocatedBytesForCurrentThread();
            Measure(Segmented, input, output, 1000);
            double allocationSegmented = (GC.GetAllocatedBytesForCurrentThread() - start) / 1000.0;
            double c = Median(current), s = Median(segmented);
            Console.WriteLine(FormattableString.Invariant($"| {pattern.Name} | {length} | {c:F1} | {s:F1} | {c/s:F2}x | {allocationCurrent:F3} | {allocationSegmented:F3} |"));
        }
    }
}
