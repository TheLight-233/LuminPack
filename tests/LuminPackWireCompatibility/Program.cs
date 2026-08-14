using System.Collections;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

if (args.Length != 2 || args[0] is not ("write" or "verify"))
    throw new ArgumentException("Usage: LuminPackWireCompatibility <write|verify> <directory>");

var directory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(directory);

if (args[0] == "write")
    WireFixtures.Write(directory);
else
    WireFixtures.Verify(directory);

Console.WriteLine($"WIRE {args[0].ToUpperInvariant()} OK: {directory}");

internal static class WireFixtures
{
    private static readonly LuminPackSerializerOption Utf8Length = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF8,
        StringRecording = LuminPackStringRecording.Length
    };

    private static readonly LuminPackSerializerOption Utf16Length = new()
    {
        StringEncoding = LuminPackStringEncoding.UTF16,
        StringRecording = LuminPackStringRecording.Length
    };

    internal static void Write(string directory)
    {
        Put(directory, "int", -123456789);
        Put(directory, "string-utf8", "Unity ↔ .NET 8 😀", Utf8Length);
        Put(directory, "string-utf16", "Unity ↔ .NET 8 😀", Utf16Length);
        Put(directory, "int-array", Enumerable.Range(-32, 97).Select(x => x * 17).ToArray());
        Put(directory, "string-list", new List<string> { "alpha", "中文", "😀", string.Empty });
        Put(directory, "string-null-empty", new List<string> { null!, string.Empty, "x" });
        Put(directory, "dictionary", new Dictionary<string, int> { ["one"] = 1, ["中文"] = 2 });
        Put(directory, "queue", CreateQueue());
        Put(directory, "stack", CreateStack());
        Put(directory, "bit-array", new BitArray(new[] { true, false, true, true, false, false, true, true, false }));
        Put(directory, "string-builder", new StringBuilder("Unity-LuminPack-界-😀"));
        Put(directory, "multi-array", CreateGrid());
        Put(directory, "wire-stats", CreateStats());
        Put(directory, "generated-model", CreateModel());
    }

    internal static void Verify(string directory)
    {
        Check(Get<int>(directory, "int") == -123456789, "int");
        Check(Get<string>(directory, "string-utf8", Utf8Length) == "Unity ↔ .NET 8 😀", "string-utf8");
        Check(Get<string>(directory, "string-utf16", Utf16Length) == "Unity ↔ .NET 8 😀", "string-utf16");
        Check(Get<int[]>(directory, "int-array").SequenceEqual(Enumerable.Range(-32, 97).Select(x => x * 17)), "int-array");
        var strings = Get<List<string>>(directory, "string-list");
        Check(strings.Count == 4 && strings[0] == "alpha" && strings[1] == "中文" && strings[2] == "😀" && strings[3] == "", "string-list");
        var nullEmpty = Get<List<string>>(directory, "string-null-empty");
        Check(nullEmpty.Count == 3 && nullEmpty[0] == null && nullEmpty[1] == "" && nullEmpty[2] == "x", "string-null-empty");
        var dictionary = Get<Dictionary<string, int>>(directory, "dictionary");
        Check(dictionary.Count == 2 && dictionary["中文"] == 2, "dictionary");
        Check(Get<Queue<int>>(directory, "queue").SequenceEqual(CreateQueue()), "queue");
        Check(Get<Stack<int>>(directory, "stack").SequenceEqual(CreateStack()), "stack");
        Check(Get<BitArray>(directory, "bit-array").Cast<bool>().SequenceEqual(new[] { true, false, true, true, false, false, true, true, false }), "bit-array");
        Check(Get<StringBuilder>(directory, "string-builder").ToString() == "Unity-LuminPack-界-😀", "string-builder");
        var grid = Get<int[,]>(directory, "multi-array");
        Check(grid.GetLength(0) == 3 && grid.GetLength(1) == 4 && grid[2, 3] == 203, "multi-array");
        var stats = Get<WireStats>(directory, "wire-stats");
        Check(stats.Health == 900 && stats.Experience == 123456789 && stats.Speed == 3.5f && stats.Accuracy == 0.975 && stats.Alive, "wire-stats");
        var model = Get<WireModel>(directory, "generated-model");
        Check(model.Id == 42 && model.Name == "wire" && model.Stats.Health == 900 && model.Stats.Alive && model.Values.SequenceEqual(new[] { 3, 5, 8 }), "generated-model");
    }

    private static Queue<int> CreateQueue()
    {
        var value = new Queue<int>(8);
        for (var i = 0; i < 8; i++) value.Enqueue(i);
        for (var i = 0; i < 3; i++) value.Dequeue();
        for (var i = 8; i < 14; i++) value.Enqueue(i);
        return value;
    }

    private static Stack<int> CreateStack()
    {
        var value = new Stack<int>();
        value.Push(10); value.Push(20); value.Push(30); value.Push(40);
        return value;
    }

    private static int[,] CreateGrid()
    {
        var value = new int[3, 4];
        for (var x = 0; x < 3; x++)
        for (var y = 0; y < 4; y++)
            value[x, y] = x * 100 + y;
        return value;
    }

    private static WireStats CreateStats() => new()
    {
        Health = 900,
        Experience = 123456789,
        Speed = 3.5f,
        Accuracy = 0.975,
        Alive = true
    };

    private static WireModel CreateModel() => new()
    {
        Id = 42,
        Name = "wire",
        Stats = CreateStats(),
        Values = new List<int> { 3, 5, 8 }
    };

    private static void Put<T>(string directory, string name, T value, LuminPackSerializerOption? option = null) =>
        File.WriteAllBytes(Path.Combine(directory, name + ".bin"), LuminPackSerializer.Serialize(value, option));

    private static T Get<T>(string directory, string name, LuminPackSerializerOption? option = null) =>
        LuminPackSerializer.Deserialize<T>(File.ReadAllBytes(Path.Combine(directory, name + ".bin")), option);

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Wire fixture failed: " + name);
    }
}

[LuminPackable]
public sealed class WireModel
{
    public int Id;
    public string Name = null!;
    public WireStats Stats;
    public List<int> Values = null!;
}

public struct WireStats
{
    public int Health;
    public long Experience;
    public float Speed;
    public double Accuracy;
    public bool Alive;
}
