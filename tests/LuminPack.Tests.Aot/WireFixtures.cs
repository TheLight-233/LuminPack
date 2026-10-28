using System.Collections;
using System.Text;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.WireCompatibility;

/// <summary>Provides wire fixtures functionality.</summary>
internal static class WireFixtures
{
    private const bool Utf8Length = false;
    private const bool Utf16Length = true;

    /// <summary>Writes  to the current output.</summary>
    /// <param name="directory">The directory used by this operation.</param>
    internal static void Write(string directory)
    {
        Put(directory, "int", -123456789);
        Put(directory, "string-utf8", "Unity \u2194 .NET 8 \uD83D\uDE00", Utf8Length);
        Put(directory, "string-utf16", "Unity \u2194 .NET 8 \uD83D\uDE00", Utf16Length);
        Put(directory, "int-array", Enumerable.Range(-32, 97).Select(x => x * 17).ToArray());
        Put(directory, "string-list", new List<string> { "alpha", "\u4E2D\u6587", "\uD83D\uDE00", string.Empty });
        Put(directory, "string-null-empty", new List<string> { null!, string.Empty, "x" });
        Put(directory, "short-ascii", Enumerable.Range(4, 13).Select(n => new string('x', n)).ToArray(), Utf8Length);
        Put(directory, "medium-ascii", Enumerable.Range(17, 16).Select(n => new string('x', n)).ToArray(), Utf8Length);
        Put(directory, "short-unicode", new[] { "\u4E2D\u6587", "\u73A9\u5BB6_00001_\uD83D\uDE00", "A\0B\u007fC", "\u00E9clair" }, Utf8Length);
        Put(directory, "dictionary", new Dictionary<string, int> { ["one"] = 1, ["\u4E2D\u6587"] = 2 });
        Put(directory, "queue", CreateQueue());
        Put(directory, "stack", CreateStack());
        Put(directory, "bit-array", new BitArray(new[] { true, false, true, true, false, false, true, true, false }));
        Put(directory, "string-builder", new StringBuilder("Unity-LuminPack-\u754C-\uD83D\uDE00"));
        Put(directory, "multi-array", CreateGrid());
        Put(directory, "generated-model", CreateModel());
    }

    /// <summary>Verifies verify.</summary>
    /// <param name="directory">The directory used by this operation.</param>
    internal static void Verify(string directory)
    {
        VerifyWrittenBytes(directory);
        Check(Get<int>(directory, "int") == -123456789, "int");
        Check(Get<string>(directory, "string-utf8", Utf8Length) == "Unity \u2194 .NET 8 \uD83D\uDE00", "string-utf8");
        Check(Get<string>(directory, "string-utf16", Utf16Length) == "Unity \u2194 .NET 8 \uD83D\uDE00", "string-utf16");
        Check(Get<int[]>(directory, "int-array").SequenceEqual(Enumerable.Range(-32, 97).Select(x => x * 17)), "int-array");
        var strings = Get<List<string>>(directory, "string-list");
        Check(strings.Count == 4 && strings[0] == "alpha" && strings[1] == "\u4E2D\u6587" && strings[2] == "\uD83D\uDE00" && strings[3] == "", "string-list");
        var nullEmpty = Get<List<string>>(directory, "string-null-empty");
        Check(nullEmpty.Count == 3 && nullEmpty[0] == null && nullEmpty[1] == "" && nullEmpty[2] == "x", "string-null-empty");
        Check(Get<string[]>(directory, "short-ascii", Utf8Length)
            .SequenceEqual(Enumerable.Range(4, 13).Select(n => new string('x', n))), "short-ascii");
        Check(Get<string[]>(directory, "medium-ascii", Utf8Length)
            .SequenceEqual(Enumerable.Range(17, 16).Select(n => new string('x', n))), "medium-ascii");
        Check(Get<string[]>(directory, "short-unicode", Utf8Length)
            .SequenceEqual(new[] { "\u4E2D\u6587", "\u73A9\u5BB6_00001_\uD83D\uDE00", "A\0B\u007fC", "\u00E9clair" }), "short-unicode");
        var dictionary = Get<Dictionary<string, int>>(directory, "dictionary");
        Check(dictionary.Count == 2 && dictionary["\u4E2D\u6587"] == 2, "dictionary");
        Check(Get<Queue<int>>(directory, "queue").SequenceEqual(CreateQueue()), "queue");
        Check(Get<Stack<int>>(directory, "stack").SequenceEqual(CreateStack()), "stack");
        Check(Get<BitArray>(directory, "bit-array").Cast<bool>().SequenceEqual(new[] { true, false, true, true, false, false, true, true, false }), "bit-array");
        Check(Get<StringBuilder>(directory, "string-builder").ToString() == "Unity-LuminPack-\u754C-\uD83D\uDE00", "string-builder");
        var grid = Get<int[,]>(directory, "multi-array");
        Check(grid.GetLength(0) == 3 && grid.GetLength(1) == 4 && grid[2, 3] == 203, "multi-array");
        var model = Get<WireModel>(directory, "generated-model");
        Check(model.Id == 42 && model.Name == "wire" && model.Stats.Health == 900 && model.Stats.Alive && model.Values.SequenceEqual(new[] { 3, 5, 8 }), "generated-model");
    }

    /// <summary>Compares newly emitted records with retained fixture bytes without modifying the baseline.</summary>
    /// <param name="directory">The retained fixture directory.</param>
    private static void VerifyWrittenBytes(string directory)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "LuminPackWire-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            Write(temporary);
            foreach (string file in Directory.EnumerateFiles(temporary, "*.bin"))
                Check(File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(directory, Path.GetFileName(file)))), Path.GetFileName(file) + " written bytes");
        }
        finally { Directory.Delete(temporary, true); }
    }
    /// <summary>Creates queue from the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    private static Queue<int> CreateQueue()
    {
        var value = new Queue<int>(8);
        for (var i = 0; i < 8; i++) value.Enqueue(i);
        for (var i = 0; i < 3; i++) value.Dequeue();
        for (var i = 8; i < 14; i++) value.Enqueue(i);
        return value;
    }

    /// <summary>Creates stack from the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    private static Stack<int> CreateStack()
    {
        var value = new Stack<int>();
        value.Push(10); value.Push(20); value.Push(30); value.Push(40);
        return value;
    }

    /// <summary>Creates grid from the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    private static int[,] CreateGrid()
    {
        var value = new int[3, 4];
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 4; y++)
                value[x, y] = x * 100 + y;
        return value;
    }

    /// <summary>Initializes semantic fields and otherwise unspecified Native padding for retained byte fixtures.</summary>
    /// <param name="value">The actual source storage consumed by the native serializer.</param>
    private static void InitializeStats(ref WireStats value)
    {
        Unsafe.InitBlockUnaligned(ref Unsafe.As<WireStats, byte>(ref value), 0, (uint)Unsafe.SizeOf<WireStats>());
        value.Health = 900;
        value.Experience = 123456789;
        value.Speed = 3.5f;
        value.Accuracy = 0.975;
        value.Alive = true;
    }

    /// <summary>Creates model from the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    private static WireModel CreateModel()
    {
        var value = new WireModel
        {
            Id = 42,
            Name = "wire",
            Values = new List<int> { 3, 5, 8 }
        };
        InitializeStats(ref value.Stats);
        return value;
    }

    /// <summary>Verifies put.</summary>
    /// <param name="directory">The directory used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="utf16">Whether this fixture selects the closed UTF-16 length profile.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    private static void Put<T>(string directory, string name, T value, bool utf16 = false)
    {
        var writerBuffer = LuminBufferWriterPool.Rent();
        try
        {
            var state = new LuminPackWriterOptionalState();
            var writer = new LuminPackWriter(writerBuffer, state);
            if (utf16) LuminPackSerializer.Serialize<T, LuminPackUtf16LengthOptions>(ref writer, in value);
            else LuminPackSerializer.Serialize<T, LuminPackUtf8LengthOptions>(ref writer, in value);
            writerBuffer.CompleteWrite(writer.CurrentIndex);
            File.WriteAllBytes(Path.Combine(directory, name + ".bin"), writerBuffer.GetSpan().ToArray());
            state.Reset();
        }
        finally
        {
            LuminBufferWriterPool.Return(writerBuffer);
        }
    }

    /// <summary>Obtains the requested value from the supplied input.</summary>
    /// <param name="directory">The directory used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="utf16">Whether this fixture selects the closed UTF-16 length profile.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    private static T Get<T>(string directory, string name, bool utf16 = false) => utf16
        ? LuminPackSerializer.Deserialize<T, LuminPackUtf16LengthOptions>(File.ReadAllBytes(Path.Combine(directory, name + ".bin")))
        : LuminPackSerializer.Deserialize<T, LuminPackUtf8LengthOptions>(File.ReadAllBytes(Path.Combine(directory, name + ".bin")));

    /// <summary>Checks  against the operation's requirements.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Wire fixture failed: " + name);
    }
}

/// <summary>Provides wire model functionality.</summary>
[LuminPackable]
public sealed class WireModel
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name = null!;
    /// <summary>Stores the stats.</summary>
    public WireStats Stats;
    /// <summary>Stores the values.</summary>
    public List<int> Values = null!;
}

/// <summary>Provides wire stats functionality.</summary>
[LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public struct WireStats
{
    /// <summary>Stores the health.</summary>
    public int Health;
    /// <summary>Stores the experience.</summary>
    public long Experience;
    /// <summary>Stores the speed.</summary>
    public float Speed;
    /// <summary>Stores the accuracy.</summary>
    public double Accuracy;
    /// <summary>Stores the alive.</summary>
    public bool Alive;
}
