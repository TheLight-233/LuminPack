using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies named binary evolution, malformed framing and warmed graph reuse.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class NamedVersionToleranceTest
{
    /// <summary>Checks one completed receiver is retained when the caller's ref slot is also a serialized field.</summary>
    [Fact]
    public void AliasedCallerSlotKeepsOneCommitReceiver()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new CallerSlot { Number = 83 });
        var target = new CallerSlot { Number = 1 };
        target.Next = target;
        CallerSlot.Completed = null;
        LuminPackSerializer.Deserialize<CallerSlot>(bytes, ref target.Next);
        Assert.Null(target.Next);
        Assert.Equal(83, target.Number);
        Assert.Same(target, CallerSlot.Completed);
    }

    /// <summary>Checks constant primitive length tables under every compiled profile and reordered schemas.</summary>
    [Fact]
    public void FixedTablesPreserveProfilesAndEvolution()
    {
        CheckFixedTable<LuminPackDefaultOptions>();
        CheckFixedTable<LuminPackUtf8TokenOptions>();
        CheckFixedTable<LuminPackUtf16LengthOptions>();
        CheckFixedTable<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Exercises the constant-length prefix and the mapped fallback for one profile.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    private static void CheckFixedTable<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new FixedRecord { Number = 7, Stamp = 11, Flag = FixedFlag.Enabled };
        byte[] bytes = LuminPackSerializer.Serialize<FixedRecord, TOptions>(source);
        var target = LuminPackSerializer.Deserialize<FixedRecord, TOptions>(bytes);
        Assert.Equal(source.Number, target.Number);
        Assert.Equal(source.Stamp, target.Stamp);
        Assert.Equal(source.Flag, target.Flag);
        var original = target;
        LuminPackSerializer.Deserialize<FixedRecord, TOptions>(bytes, ref target);
        Assert.Same(original, target);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<FixedRecord, TOptions>(source));
        var evolved = LuminPackSerializer.Deserialize<FixedReordered, TOptions>(bytes);
        Assert.Equal(source.Stamp, evolved.Stamp);
        Assert.Equal(source.Flag, evolved.Flag);
        Assert.Equal(0, evolved.Added);
        var restored = LuminPackSerializer.Deserialize<FixedRecord, TOptions>(LuminPackSerializer.Serialize<FixedReordered, TOptions>(evolved));
        Assert.Equal(source.Stamp, restored.Stamp);
        Assert.Equal(0, restored.Number);
    }

    /// <summary>Checks arbitrary insertion, deletion, reordering and renaming in both directions.</summary>
    [Fact]
    public void FieldsEvolveWithoutOrderSlots()
    {
        var original = new Original { Id = 42, Removed = "skip", Name = "retained", Tail = 91 };
        byte[] bytes = LuminPackSerializer.Serialize(original);
        var current = LuminPackSerializer.Deserialize<Current>(bytes);
        Assert.Equal(42, current.Id);
        Assert.Equal("retained", current.DisplayName);
        Assert.Equal(91, current.Tail);
        Assert.Null(current.Added);
        current.Added = [1, 2, 3];
        bytes = LuminPackSerializer.Serialize(current);
        var restored = LuminPackSerializer.Deserialize<Original>(bytes);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Tail, restored.Tail);
        Assert.Null(restored.Removed);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(current));
        Assert.Equal(251, bytes[0]);
        Assert.Equal(1, bytes[1]);
    }

    /// <summary>Checks aliases from multiple intermediate names and stable oldest-name writes.</summary>
    [Fact]
    public void RenameHistoryIsBidirectional()
    {
        var latest = LuminPackSerializer.Deserialize<Latest>(LuminPackSerializer.Serialize(new Intermediate { B = 17 }));
        Assert.Equal(17, latest.C);
        Assert.Equal(17, LuminPackSerializer.Deserialize<Earliest>(LuminPackSerializer.Serialize(latest)).A);
        Assert.Equal(19, LuminPackSerializer.Deserialize<Latest>(LuminPackSerializer.Serialize(new Earliest { A = 19 })).C);
        var unicode = LuminPackSerializer.Deserialize<UnicodeCurrent>(LuminPackSerializer.Serialize(new UnicodeOriginal { @class = 23 }));
        Assert.Equal(23, unicode.Value);
    }

    /// <summary>Checks old names remain accepted by both JSON encodings while writes use the current name.</summary>
    [Fact]
    public void JsonAcceptsFormerNames()
    {
        Assert.Equal(41, LuminPackSerializer.DeserializeJson<Latest>("{\"A\":41}").C);
        Assert.Equal(43, LuminPackSerializer.DeserializeJson<Latest, LuminPackUtf16LengthOptions>("{\"B\":43}").C);
        Assert.Equal("{\"C\":47}", LuminPackSerializer.SerializeJson(new Latest { C = 47 }));
    }

    /// <summary>Checks known nested fields, strings and collection elements retain their storage.</summary>
    [Fact]
    public void ExistingGraphRemainsAllocationFree()
    {
        CheckReuse<LuminPackDefaultOptions>();
        CheckReuse<LuminPackUtf8TokenOptions>();
        CheckReuse<LuminPackUtf16LengthOptions>();
        CheckReuse<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks evolved metadata maps fields without allocating or replacing reusable children.</summary>
    [Fact]
    public void EvolvedGraphRemainsAllocationFree()
    {
        CheckEvolvedReuse<LuminPackDefaultOptions>();
        CheckEvolvedReuse<LuminPackUtf8TokenOptions>();
        CheckEvolvedReuse<LuminPackUtf16LengthOptions>();
        CheckEvolvedReuse<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks the uncommon mapped path under one compiled profile.</summary>
    /// <typeparam name="TOptions">The binary string profile.</typeparam>
    private static void CheckEvolvedReuse<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new GraphBefore { Child = new() { Text = "child", Number = 29 }, Items = [new() { Text = "item", Number = 31 }], Removed = "skip" };
        var target = new GraphAfter { Child = new() { Text = new string("child".ToCharArray()) }, Items = [new() { Text = new string("item".ToCharArray()) }], Added = [1] };
        var child = target.Child;
        var items = target.Items;
        var item = items[0];
        var text = child.Text;
        byte[] bytes = LuminPackSerializer.Serialize<GraphBefore, TOptions>(source);
        for (int i = 0; i < 256; i++) LuminPackSerializer.Deserialize<GraphAfter, TOptions>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<GraphAfter, TOptions>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(child, target.Child);
        Assert.Same(items, target.Items);
        Assert.Same(item, target.Items[0]);
        Assert.Same(text, target.Child.Text);
        Assert.Equal(29, child.Number);
        Assert.Equal(31, item.Number);
        Assert.Null(target.Added);
    }

    /// <summary>Measures actual allocations and identities under one complete binary profile.</summary>
    /// <typeparam name="TOptions">The binary profile.</typeparam>
    private static void CheckReuse<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new Graph { Child = new() { Text = new string('c', 300), Number = 7 }, Items = [new() { Text = new string('i', 512), Number = 8 }] };
        var target = new Graph { Child = new() { Text = new string(source.Child.Text.ToCharArray()) }, Items = [new() { Text = new string(source.Items[0].Text.ToCharArray()) }] };
        byte[] bytes = LuminPackSerializer.Serialize<Graph, TOptions>(source);
        var root = target;
        var child = target.Child;
        var list = target.Items;
        var item = list[0];
        var text = child.Text;
        for (int i = 0; i < 256; i++) LuminPackSerializer.Deserialize<Graph, TOptions>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<Graph, TOptions>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Same(list, target.Items);
        Assert.Same(item, target.Items[0]);
        Assert.Same(text, target.Child.Text);
        Assert.Equal(7, child.Number);
        Assert.Equal(8, item.Number);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<Graph, TOptions>(source));
    }

    /// <summary>Checks absent fields clear reused values and unknown fields consume exactly their payload.</summary>
    [Fact]
    public void MissingMembersResetAndUnknownMembersAreSkipped()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Original { Id = 3, Name = "a", Removed = "unknown", Tail = 4 });
        var target = new Current { Added = [9] };
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<Current>(bytes, ref target));
        Assert.Null(target.Added);
        byte[] joined = [.. bytes, .. LuminPackSerializer.Serialize(new Empty())];
        Empty? empty = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<Empty>(joined, ref empty));
        Assert.NotNull(empty);
    }

    /// <summary>Checks matched fields reject incompatible types even when their widths are equal.</summary>
    [Fact]
    public void TypeChangesAreRejected()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Earliest { A = 123 });
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<ChangedType>(bytes));
        // The default profile is the UTF-16 length profile, so a UTF-8 reader sees a different member contract.
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Earliest, LuminPackUtf8LengthOptions>(bytes));
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Latest>(LuminPackSerializer.Serialize(new TwoInts { A = 1, B = 2 })));
    }

    /// <summary>Checks private and public volatile members retain storage access semantics and aliases.</summary>
    [Fact]
    public void VolatileMembersUseTheirOriginalStorage()
    {
        var source = new VolatileMembers { Number = 59 };
        source.SetSecret(61);
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var target = new VolatileMembers();
        var original = target;
        LuminPackSerializer.Deserialize<VolatileMembers>(bytes, ref target);
        Assert.Same(original, target);
        Assert.Equal(59, target.Number);
        Assert.Equal(61, target.ReadSecret());
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(source));
        var fresh = LuminPackSerializer.Deserialize<VolatileMembers>(bytes);
        Assert.Equal(61, fresh.ReadSecret());
    }

    /// <summary>Checks named reads preserve selected constructors, pool rent and callback timing during reuse.</summary>
    [Fact]
    public void ConstructionPoolingAndCallbacksArePreserved()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Earliest { A = 67 });
        Constructed.Calls = 0;
        Constructed.Completions = 0;
        var constructed = LuminPackSerializer.Deserialize<Constructed>(bytes);
        Assert.Equal(67, constructed.Value);
        Assert.Equal(1, Constructed.Calls);
        LuminPackSerializer.Deserialize<Constructed>(bytes, ref constructed);
        Assert.Equal(1, Constructed.Calls);
        Assert.Equal(2, Constructed.Completions);

        Pooled.Instance = new Pooled();
        Pooled.Rents = 0;
        var pooled = LuminPackSerializer.Deserialize<Pooled>(bytes);
        Assert.Same(Pooled.Instance, pooled);
        Assert.Equal(67, pooled.Value);
        Assert.Equal(1, Pooled.Rents);
        LuminPackSerializer.Deserialize<Pooled>(bytes, ref pooled);
        Assert.Equal(1, Pooled.Rents);
        LuminPackSerializer.Deserialize<Pooled>(LuminPackSerializer.Serialize(new Empty()), ref pooled);
        Assert.Equal(0, pooled.Value);
        Pooled.Instance = null!;
    }

    /// <summary>Checks instance callbacks on version-tolerant structs compile and run at each lifecycle boundary.</summary>
    [Fact]
    public void ValueTypeCallbacksArePreserved()
    {
        CallbackValue.Writes = 0;
        CallbackValue.Reads = 0;
        byte[] bytes = LuminPackSerializer.Serialize(new CallbackValue { Number = 71, Text = "callback" });
        var value = LuminPackSerializer.Deserialize<CallbackValue>(bytes);
        Assert.Equal(71, value.Number);
        Assert.Equal(2, CallbackValue.Writes);
        Assert.Equal(2, CallbackValue.Reads);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(value));
    }

    /// <summary>Checks generic field contracts use closed arguments and retain nested reusable storage.</summary>
    [Fact]
    public void GenericMembersKeepTheirCanonicalRepresentation()
    {
        var source = new Generic<Node> { Value = new() { Number = 31, Text = "generic" } };
        var target = new Generic<Node> { Value = new() };
        var child = target.Value;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<Generic<Node>>(bytes, ref target);
        Assert.Same(child, target.Value);
        Assert.Equal(31, child.Number);
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof(source));
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Generic<string>>(bytes));
    }

    /// <summary>Checks new-format framing rejects the old slot marker, invalid revisions and truncation.</summary>
    [Fact]
    public void MalformedRecordsAreRejected()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Earliest { A = 5 });
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Earliest>(new byte[] { 1, 4, 5, 0, 0, 0 }));
        var revision = bytes.ToArray();
        revision[1] = 2;
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Earliest>(revision));
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes.AsSpan(0, length).ToArray();
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<Earliest>(truncated));
        }
        int payloadLengthIndex = bytes.Length - sizeof(int) - 1;
        var shortField = bytes.ToArray();
        shortField[payloadLengthIndex] = 3;
        Assert.Throws<FormatException>(() => LuminPackSerializer.Deserialize<Earliest>(shortField));
        var longField = bytes.ToArray();
        longField[payloadLengthIndex] = 5;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<Earliest>(longField));
    }

    /// <summary>Checks null and empty schemas have exact sizes and reset supplied reference storage.</summary>
    [Fact]
    public void EmptyAndNullRecordsRemainDistinct()
    {
        Assert.Equal(4, LuminPackSerializer.Serialize(new Empty()).Length);
        Assert.Equal(4, LuminPackSerializer.Sizeof(new Empty()));
        Empty? target = new();
        byte[] bytes = LuminPackSerializer.Serialize<Empty>(null);
        Assert.Equal(new byte[] { 255 }, bytes);
        Assert.Equal(1, LuminPackSerializer.Deserialize<Empty>(bytes, ref target));
        Assert.Null(target);
    }

    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Original
    {
        public int Id;
        public string? Removed;
        public string? Name;
        public long Tail;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Current
    {
        public long Tail;
        public List<int>? Added;
        [LuminPackOldName("Name")] public string? DisplayName;
        public int Id;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Earliest { public int A; }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Intermediate { public int B; }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Latest
    {
        [LuminPackOldName("A"), LuminPackOldName("B")] public int C;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class UnicodeOriginal
    {
        [LuminPackOldName("\u540D\u524D")] public int @class;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class UnicodeCurrent
    {
        [LuminPackOldName("\u540D\u524D"), LuminPackOldName("class")] public int Value;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class ChangedType { public float A; }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class TwoInts { public int A; public int B; }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class VolatileMembers
    {
        public volatile int Number;
        [LuminPackInclude, LuminPackOldName("Secret")] private volatile int _secret;

        /// <summary>Sets the private volatile member used by the storage fixture.</summary>
        /// <param name="value">The stored number.</param>
        public void SetSecret(int value) => _secret = value;

        /// <summary>Reads the private volatile member through its original declaration.</summary>
        /// <returns>The stored number.</returns>
        public int ReadSecret() => _secret;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Empty { }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Constructed
    {
        public static int Calls;
        public static int Completions;
        [LuminPackOldName("A")] public int Value { get; }

        /// <summary>Records constructor selection and initializes the readonly property.</summary>
        /// <param name="value">The supplied property value.</param>
        [LuminPackConstructor]
        public Constructed(int value) { Value = value; Calls++; }

        /// <summary>Records completed reads on constructed and reused instances.</summary>
        [LuminPackOnDeserialized]
        public void Complete() => Completions++;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Pooled
    {
        public static Pooled Instance = null!;
        public static int Rents;
        [LuminPackOldName("A")] public int Value;

        /// <summary>Returns the shared fixture instance.</summary>
        /// <returns>The selected pooled instance.</returns>
        [LuminPackPoolRent]
        public static Pooled Rent() { Rents++; return Instance; }
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public struct CallbackValue
    {
        public static int Writes;
        public static int Reads;
        public int Number;
        public string? Text;

        /// <summary>Records entry to the struct write.</summary>
        [LuminPackOnSerializing]
        public void BeforeWrite() => Writes++;

        /// <summary>Records completion of the struct write.</summary>
        [LuminPackOnSerialized]
        public void AfterWrite() => Writes++;

        /// <summary>Records entry to the struct read.</summary>
        [LuminPackOnDeserializing]
        public void BeforeRead() => Reads++;

        /// <summary>Records completion of the struct read.</summary>
        [LuminPackOnDeserialized]
        public void AfterRead() => Reads++;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Graph
    {
        public Node Child = new();
        public List<Node> Items = [];
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class GraphBefore
    {
        public Node Child = new();
        public List<Node> Items = [];
        public string? Removed;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class GraphAfter
    {
        public List<Node> Items = [];
        public List<int>? Added;
        public Node Child = new();
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Node
    {
        public string? Text;
        public int Number;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class CallerSlot
    {
        public static CallerSlot? Completed;
        public CallerSlot? Next;
        public int Number;

        /// <summary>Records the actual instance that completed the member commit.</summary>
        [LuminPackOnDeserialized]
        public void Complete() => Completed = this;
    }
    public enum FixedFlag : short { Disabled, Enabled }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class FixedRecord
    {
        public int Number;
        public long Stamp;
        public FixedFlag Flag;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class FixedReordered
    {
        public FixedFlag Flag;
        public int Added;
        public long Stamp;
    }
    [LuminPackable(GeneratorType.VersionTolerant)] public sealed class Generic<T> { public T Value = default!; }
}
