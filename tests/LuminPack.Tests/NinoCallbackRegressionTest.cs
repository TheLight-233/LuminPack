#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks callback order and the receiver selected by the binary record modes.</summary>
public class NinoCallbackRegressionTest
{
    [ThreadStatic]
    private static List<string>? log;

    /// <summary>Runs static hooks before instance hooks in base-to-derived order and observes the chosen receiver.</summary>
    [Fact]
    public void HierarchyUsesBaseFirstWithStaticCallbacksBeforeInstanceCallbacks()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new CallbackDerived { Number = 12, Text = "encoded" });
        Assert.Equal(new[] { "Base static write", "Derived static write", "Base write", "Derived write" }, TakeLog());

        CallbackDerived fresh = LuminPackSerializer.Deserialize<CallbackDerived>(bytes);
        Assert.Equal("encoded", fresh.Text);
        Assert.Equal(new[] { "Base static read", "Derived static read", "Base read 3", "Derived read initial", "Base read done 12", "Derived read done encoded" }, TakeLog());

        var existing = new CallbackDerived { Number = 99, Text = "old" };
        CallbackDerived target = existing;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(existing, target);
        Assert.Equal(new[] { "Base static read", "Derived static read", "Base read 99", "Derived read old", "Base read done 12", "Derived read done encoded" }, TakeLog());
    }

    /// <summary>Calls an attributed virtual hook once even when an override carries the same attribute.</summary>
    [Fact]
    public void ARepeatedVirtualCallbackAttributeKeepsOneVirtualSlot()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new CallbackVirtualDerived { Number = 12 });
        Assert.Equal(new[] { "Virtual derived" }, TakeLog());
        Assert.Equal(12, LuminPackSerializer.Deserialize<CallbackVirtualDerived>(bytes).Number);
    }

    /// <summary>A tracked cycle invokes each callback only for the complete first occurrence of an instance.</summary>
    [Fact]
    public void TrackedCallbacksSkipBackReferences()
    {
        var first = new CallbackNode { Text = "first" };
        var second = new CallbackNode { Text = "second", Next = first };
        first.Next = second;

        byte[] bytes = LuminPackSerializer.Serialize(first);
        Assert.Equal(new[] { "Write first", "Write second", "Written second", "Written first" }, TakeLog());
        CallbackNode result = LuminPackSerializer.Deserialize<CallbackNode>(bytes);
        Assert.Same(result, result.Next!.Next);
        Assert.Equal(new[] { "Read initial", "Read initial", "Read done second", "Read done first" }, TakeLog());
    }

    /// <summary>Runs an instance hook when default construction or rental can choose the receiver before the payload.</summary>
    [Fact]
    public void VersionedAndPooledUnionRunFreshInstanceCallbacks()
    {
        byte[] versioned = LuminPackSerializer.Serialize(new CallbackVersioned { Number = 19, Label = "label" });
        CallbackVersioned versionedCopy = LuminPackSerializer.Deserialize<CallbackVersioned>(versioned);
        Assert.Equal(19, versionedCopy.Number);
        Assert.Equal("label", versionedCopy.Label);
        Assert.Equal(new[] { "Versioned read 3", "Versioned read done 19" }, TakeLog());

        CallbackUnion source = new CallbackPooled { Number = 23 };
        byte[] union = LuminPackSerializer.Serialize(source);
        CallbackPooled rented = Assert.IsType<CallbackPooled>(LuminPackSerializer.Deserialize<CallbackUnion>(union));
        Assert.Equal(23, rented.Number);
        Assert.Equal(new[] { "Rented", "Pooled read 17" }, TakeLog());
    }

    /// <summary>Runs only static before-read hooks when constructor arguments require the serialized fields.</summary>
    [Fact]
    public void ConstructorBoundRecordRunsInstanceBeforeReadOnlyForAnExistingReceiver()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new CallbackConstructed("encoded"));
        Assert.Equal("encoded", LuminPackSerializer.Deserialize<CallbackConstructed>(bytes).Text);
        Assert.Equal(new[] { "Constructed static read", "Constructed read done encoded" }, TakeLog());

        var existing = new CallbackConstructed("old");
        CallbackConstructed target = existing;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(existing, target);
        Assert.Equal(new[] { "Constructed static read", "Constructed read old", "Constructed read done encoded" }, TakeLog());
    }

    /// <summary>Records a callback on the test thread.</summary>
    /// <param name="entry">The observed callback and value.</param>
    private static void AddLog(string entry) => (log ??= new()).Add(entry);

    /// <summary>Returns and clears the observed callbacks.</summary>
    /// <returns>The observed callbacks in execution order.</returns>
    private static string[] TakeLog()
    {
        List<string> entries = log ??= new();
        string[] result = entries.ToArray();
        entries.Clear();
        return result;
    }

    [LuminPackable]
    public partial class CallbackBase
    {
        public int Number = 3;

        [LuminPackOnSerializing]
        public void BeforeWrite() => AddLog("Base write");

        [LuminPackOnSerializing]
        public static void BeforeWriteStatic() => AddLog("Base static write");

        [LuminPackOnDeserializing]
        public void BeforeRead() => AddLog("Base read " + Number);

        [LuminPackOnDeserializing]
        public static void BeforeReadStatic() => AddLog("Base static read");

        [LuminPackOnDeserialized]
        public void AfterRead() => AddLog("Base read done " + Number);
    }

    [LuminPackable]
    public partial class CallbackDerived : CallbackBase
    {
        public string Text = "initial";

        [LuminPackOnSerializing]
        public new void BeforeWrite() => AddLog("Derived write");

        [LuminPackOnSerializing]
        public new static void BeforeWriteStatic() => AddLog("Derived static write");

        [LuminPackOnDeserializing]
        public new void BeforeRead() => AddLog("Derived read " + Text);

        [LuminPackOnDeserializing]
        public new static void BeforeReadStatic() => AddLog("Derived static read");

        [LuminPackOnDeserialized]
        public new void AfterRead() => AddLog("Derived read done " + Text);
    }

    [LuminPackable]
    public partial class CallbackVirtualBase
    {
        public int Number;

        [LuminPackOnSerializing]
        public virtual void BeforeWrite() => AddLog("Virtual base");
    }

    [LuminPackable]
    public partial class CallbackVirtualDerived : CallbackVirtualBase
    {
        [LuminPackOnSerializing]
        public override void BeforeWrite() => AddLog("Virtual derived");
    }

    [LuminPackable, LuminPackOption(TrackReferences = true)]
    public partial class CallbackNode
    {
        public string Text = "initial";
        public CallbackNode? Next;

        [LuminPackOnSerializing]
        public void BeforeWrite() => AddLog("Write " + Text);

        [LuminPackOnSerialized]
        public void AfterWrite() => AddLog("Written " + Text);

        [LuminPackOnDeserializing]
        public void BeforeRead() => AddLog("Read " + Text);

        [LuminPackOnDeserialized]
        public void AfterRead() => AddLog("Read done " + Text);
    }

    [LuminPackable(GeneratorType.VersionTolerant)]
    public partial class CallbackVersioned
    {
        public int Number = 3;
        public required string Label;

        [LuminPackOnDeserializing]
        public void BeforeRead() => AddLog("Versioned read " + Number);

        [LuminPackOnDeserialized]
        public void AfterRead() => AddLog("Versioned read done " + Number);
    }

    [LuminPackable, LuminPackUnion(11, typeof(CallbackPooled))]
    public abstract partial class CallbackUnion { }

    [LuminPackable]
    public sealed partial class CallbackPooled : CallbackUnion
    {
        public int Number;

        [LuminPackPoolRent]
        public static CallbackPooled Rent()
        {
            AddLog("Rented");
            return new CallbackPooled { Number = 17 };
        }

        [LuminPackOnDeserializing]
        public void BeforeRead() => AddLog("Pooled read " + Number);
    }

    [LuminPackable]
    public partial record CallbackConstructed(string Text)
    {
        [LuminPackOnDeserializing]
        public static void BeforeReadStatic() => AddLog("Constructed static read");

        [LuminPackOnDeserializing]
        public void BeforeRead() => AddLog("Constructed read " + Text);

        [LuminPackOnDeserialized]
        public void AfterRead() => AddLog("Constructed read done " + Text);
    }
}
#endif
