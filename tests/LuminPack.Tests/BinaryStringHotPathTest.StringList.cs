using LuminPack;
using LuminPack.Code;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public partial class BinaryStringHotPathTest
{
    /// <summary>Checks the generated list reader against the generated producer under all compiled string profiles.</summary>
    [Fact]
    public void GeneratedStringListProfilesKeepWireAndReusableStorage()
    {
        CheckStringListProfile<LuminPackDefaultOptions>();
        CheckStringListProfile<LuminPackUtf8LengthOptions>();
        CheckStringListProfile<LuminPackUtf16LengthOptions>();
        CheckStringListProfile<LuminPackUtf8TokenOptions>();
        CheckStringListProfile<LuminPackUtf16TokenOptions>();
        CheckStringListProfile<LuminPackUtf8VarIntOptions>();
        CheckStringListProfile<LuminPackUtf16VarIntOptions>();
    }

    /// <summary>Exercises fresh reads, equal and changed strings, resize and null framing.</summary>
    /// <typeparam name="TOptions">The producer and consumer's shared compiled profile.</typeparam>
    private static void CheckStringListProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        List<string?> source = [null, "", "ascii", "\u00e9\u00ff", "\u4e2d\u6587", "\ud83d\ude03", new('x', 4096)];
        var bytes = LuminPackSerializer.Serialize<TOptions>(source);
        List<string?>? expected = null, actual = null;
        expected = source.Select(text => text is null && default(TOptions).StringRecording == LuminPackStringRecording.Token ? "" : text).ToList();
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TOptions>(bytes, ref actual));
        Assert.Equal(expected, actual);
        var original = actual;
        var strings = actual!.ToArray();
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref actual);
        Assert.Same(original, actual);
        for (int i = 0; i < strings.Length; i++) Assert.Same(strings[i], actual![i]);
        foreach (int count in new[] { 1, 16, 0, 256 })
        {
            source = Enumerable.Range(0, count).Select(i => (string?) ("changed-" + i)).ToList();
            bytes = LuminPackSerializer.Serialize<TOptions>(source);
            expected = source;
            LuminPackSerializer.Deserialize<TOptions>(bytes, ref actual);
            Assert.Same(original, actual);
            Assert.Equal(expected, actual);
        }
        source = null!;
        bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref actual);
        Assert.Null(actual);
    }

    /// <summary>Matches generated list cursor and partial heap publication at every truncated input boundary.</summary>
    [Fact]
    public void GeneratedStringListFailuresKeepGeneratedPublication()
    {
        List<string?> source = [null, "", "ascii", "\u4e2d\u6587", "\ud83d\ude03"];
        var bytes = LuminPackSerializer.Serialize(source);
        for (int cut = 0; cut <= bytes.Length; cut++)
        foreach (bool reuse in new[] { false, true })
        {
            List<string?>? actual = reuse ? new(source) : null;
            Span<byte> input = bytes.AsSpan(0, cut);
            var candidate = new LuminPackReader(ref input);
            Exception? second = null;
            try { LuminPackSerializer.Deserialize(ref candidate, ref actual); } catch (Exception error) { second = error; }
            if (cut < bytes.Length)
            {
                Assert.NotNull(second);
                Assert.Equal(0, candidate.Position);
                if (reuse) Assert.Equal(source, actual);
            }
            else
            {
                Assert.Null(second);
                Assert.Equal(bytes.Length, candidate.Position);
                Assert.Equal(source, actual);
            }
        }
        List<string?> changed = ["new-first", "new-second"];
        List<string?>? partial = ["old-first", "old-second"];
        var original = partial;
        var changedBytes = LuminPackSerializer.Serialize(changed);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(changedBytes.AsSpan(0, changedBytes.Length - 1), ref partial));
        Assert.Same(original, partial);
        Assert.Equal(new[] { "new-first", "old-second" }, partial);
        foreach (int invalid in new[] { -2, int.MinValue, int.MaxValue })
        {
            var malformed = BitConverter.GetBytes(invalid);
            List<string?>? target = null;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed, ref target));
            Assert.Null(target);
        }
        // A failed facade lease must not contaminate the following independent read.
        List<string?>? recovered = null;
        LuminPackSerializer.Deserialize(bytes, ref recovered);
        Assert.Equal(source, recovered);
    }

    /// <summary>Keeps warmed stable-content reads allocation-free and reports only the leading record's width.</summary>
    [Fact]
    public void GeneratedStringListStableReadsAllocateNothingAndConsumeOneRecord()
    {
        List<string?> source = Enumerable.Range(0, 256).Select(i => (string?) ("value-" + i)).ToList();
        var bytes = LuminPackSerializer.Serialize(source);
        var appended = bytes.Concat(bytes).ToArray();
        List<string?>? target = null;
        for (int i = 0; i < 100; i++) LuminPackSerializer.Deserialize(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(appended, ref target));
        Assert.Equal(source, target);
    }

    /// <summary>Applies the same generated list rule to fields, auto-properties and nested packable objects.</summary>
    [Fact]
    public void GeneratedStringListsReuseNestedFieldsAndProperties()
    {
        var source = new StringListFieldRoot
        {
            Prefix = 17, Names = ["first", "\u4e2d\u6587"],
            Child = new() { Names = ["nested"] }, Tail = ["last"]
        };
        var bytes = LuminPackSerializer.Serialize(source);
        StringListFieldRoot target = null!;
        LuminPackSerializer.Deserialize(bytes, ref target);
        var names = target.Names;
        var child = target.Child;
        var tail = target.Tail;
        string? first = names[0], nested = child.Names[0], last = tail[0];
        for (int i = 0; i < 100; i++) LuminPackSerializer.Deserialize(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Same(names, target.Names); Assert.Same(child, target.Child); Assert.Same(tail, target.Tail);
        Assert.Same(first, target.Names[0]); Assert.Same(nested, target.Child.Names[0]); Assert.Same(last, target.Tail[0]);
        source.Names = ["changed"]; source.Child.Names = []; source.Tail = [null, "new"];
        bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(names, target.Names); Assert.Same(tail, target.Tail);
        Assert.Equal(source.Names, target.Names); Assert.Empty(target.Child.Names); Assert.Equal(source.Tail, target.Tail);
    }
}

/// <summary>Exercises ordinary fields, a nested object and auto-property storage with string lists.</summary>
[LuminPackable]
public sealed partial class StringListFieldRoot
{
    public int Prefix;
    public List<string?> Names = [];
    public StringListFieldChild Child = new();
    public List<string?> Tail { get; set; } = [];
}

/// <summary>Exercises a string list reached through a nested packable field.</summary>
[LuminPackable]
public sealed partial class StringListFieldChild
{
    public List<string?> Names = [];
}
