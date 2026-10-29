using System.Collections.ObjectModel;
using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks complete special-value records, storage contracts and culture-independent text.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class SpecialEmitterRegressionTest
{
    /// <summary>Checks nullable presence is independent of the first underlying payload byte.</summary>
    /// <param name="number">The underlying value with a marker-like low byte.</param>
    [Theory]
    [InlineData(255)]
    [InlineData(-1)]
    [InlineData(511)]
    [InlineData(0)]
    public void NullablePayloadDoesNotCollideWithNull(int number)
    {
        int? expected = number;
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected, LuminPackSerializer.Deserialize<int?>(bytes));
        int? absent = null;
        Assert.Null(LuminPackSerializer.Deserialize<int?>(LuminPackSerializer.Serialize(absent)));
    }

    /// <summary>Checks a present Lazy wrapper is distinct from both null and a null child.</summary>
    [Fact]
    public void LazyPresenceDoesNotDependOnItsChild()
    {
        var number = new Lazy<int>(255);
        Assert.Equal(255, LuminPackSerializer.Deserialize<Lazy<int>>(LuminPackSerializer.Serialize(number)).Value);
        var text = new Lazy<string?>(() => null);
        var copy = LuminPackSerializer.Deserialize<Lazy<string?>>(LuminPackSerializer.Serialize(text));
        Assert.NotNull(copy);
        Assert.Null(copy.Value);
        Lazy<int>? absent = null;
        Assert.Null(LuminPackSerializer.Deserialize<Lazy<int>?>(LuminPackSerializer.Serialize(absent)));
    }

    /// <summary>Checks reference tuples preserve a marker-like first component.</summary>
    [Fact]
    public void ReferenceTuplePresenceDoesNotDependOnItsFirstItem()
    {
        var expected = Tuple.Create(255, "tail");
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected, LuminPackSerializer.Deserialize<Tuple<int, string>>(bytes));
    }

    /// <summary>Checks every Version constructor form consumes the entire record before a following scalar.</summary>
    [Fact]
    public void VersionConsumesItsWholeRecord()
    {
        foreach (var expected in new[] { new Version(1, 2), new Version(1, 2, 3), new Version(255, 2, 3, 4) })
        {
            byte[] record = LuminPackSerializer.Serialize(expected);
            Span<byte> input = record.Concat(BitConverter.GetBytes(42)).ToArray();
            var reader = new LuminPackReader(ref input);
            Version actual = null!;
            LuminPackSerializer.Deserialize(ref reader, ref actual);
            Assert.Equal(expected, actual);
            Assert.Equal(record.Length, reader.Position);
            Assert.Equal(42, reader.Read<int>());
        }
    }

    /// <summary>Checks a length whose first byte equals the null marker remains a nonnull URI.</summary>
    [Fact]
    public void UriLength255DoesNotMeanNull()
    {
        var expected = new Uri("https://example.com/" + new string('a', 235));
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected, LuminPackSerializer.Deserialize<Uri>(bytes));
    }

    /// <summary>Checks empty culture names preserve the nonnull invariant culture.</summary>
    [Fact]
    public void InvariantCultureRemainsPresent()
    {
        var expected = CultureInfo.InvariantCulture;
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(expected.Name, LuminPackSerializer.Deserialize<CultureInfo>(bytes)?.Name);
        Assert.Equal(expected.Name, LuminPackSerializer.DeserializeJson<CultureInfo>(LuminPackSerializer.SerializeJson(expected))?.Name);
    }

    /// <summary>Checks a custom time-zone record and a following scalar are consumed independently.</summary>
    [Fact]
    public void TimeZoneRecordKeepsItsRulesAndCursor()
    {
        var expected = TimeZoneInfo.CreateCustomTimeZone("AuditZone", TimeSpan.FromMinutes(90), "Audit", "Audit");
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Span<byte> input = bytes.Concat(BitConverter.GetBytes(42)).ToArray();
        var reader = new LuminPackReader(ref input);
        TimeZoneInfo copy = null!;
        LuminPackSerializer.Deserialize(ref reader, ref copy);
        Assert.True(expected.HasSameRules(copy));
        Assert.Equal(bytes.Length, reader.Position);
        Assert.Equal(42, reader.Read<int>());
    }

    /// <summary>Checks dynamic UTF-16 input validation precedes clearing a reusable builder.</summary>
    [Fact]
    public void TruncatedBuilderIsRejectedBeforeMutation()
    {
        // The backing array deliberately remains large: a missing span-length check must fail safely as an assertion.
        var backing = new byte[128];
        BitConverter.GetBytes(32).CopyTo(backing, 0);
        var existing = new StringBuilder("preserved");
        Assert.ThrowsAny<Exception>(() => ReadTruncatedBuilder(backing, ref existing));
        Assert.Equal("preserved", existing.ToString());
    }

    /// <summary>Checks raw-looking elements retain their explicit portable contract inside wrapper collections.</summary>
    [Fact]
    public void WrappersReadTheSamePortableContractTheyWrite()
    {
        var element = new AuditPortableElement { Tag = 17, Value = long.MinValue + 123 };
        var collection = new Collection<AuditPortableElement>(new List<AuditPortableElement> { element });
        byte[] bytes = LuminPackSerializer.Serialize(collection);
        Assert.Equal(13, bytes.Length);
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<Collection<AuditPortableElement>>(bytes)));
        var observable = new ObservableCollection<AuditPortableElement> { element };
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ObservableCollection<AuditPortableElement>>(LuminPackSerializer.Serialize(observable))));
        var readOnly = new ReadOnlyCollection<AuditPortableElement>(new[] { element });
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ReadOnlyCollection<AuditPortableElement>>(LuminPackSerializer.Serialize(readOnly))));
        var readOnlyObservable = new ReadOnlyObservableCollection<AuditPortableElement>(observable);
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ReadOnlyObservableCollection<AuditPortableElement>>(LuminPackSerializer.Serialize(readOnlyObservable))));
    }

    /// <summary>Checks reference-free Custom elements retain their two-byte formatter inside every wrapper family.</summary>
    [Fact]
    public void WrappersDoNotBypassUnmanagedCustomElements()
    {
        var element = new AuditCustomElement { Value = 1234 };
        var collection = new Collection<AuditCustomElement>(new List<AuditCustomElement> { element });
        byte[] bytes = LuminPackSerializer.Serialize(collection);
        Assert.Equal(6, bytes.Length);
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<Collection<AuditCustomElement>>(bytes)));
        var observable = new ObservableCollection<AuditCustomElement> { element };
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ObservableCollection<AuditCustomElement>>(LuminPackSerializer.Serialize(observable))));
        var readOnly = new ReadOnlyCollection<AuditCustomElement>(new[] { element });
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ReadOnlyCollection<AuditCustomElement>>(LuminPackSerializer.Serialize(readOnly))));
        var readOnlyObservable = new ReadOnlyObservableCollection<AuditCustomElement>(observable);
        Assert.Equal(element, Assert.Single(LuminPackSerializer.Deserialize<ReadOnlyObservableCollection<AuditCustomElement>>(LuminPackSerializer.Serialize(readOnlyObservable))));
    }

    /// <summary>Checks private storage mirrors retain an explicitly declared packing boundary.</summary>
    [Fact]
    public void PrivatePackedStorageKeepsItsOffsets()
    {
        var expected = AuditPackedPrivate.Create();
        var copy = LuminPackSerializer.Deserialize<AuditPackedPrivate>(LuminPackSerializer.Serialize(expected));
        Assert.Equal(expected.Tag, copy.Tag);
        Assert.Equal(expected.Value, copy.Value);
        var json = LuminPackSerializer.DeserializeJson<AuditPackedPrivate>(LuminPackSerializer.SerializeJson(expected));
        Assert.Equal(expected.Tag, json.Tag);
        Assert.Equal(expected.Value, json.Value);
    }

    /// <summary>Checks quoted 128-bit integers use invariant text despite a customized thread negative sign.</summary>
    [Fact]
    public void Int128JsonIgnoresCustomNegativeSigns()
    {
        var original = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.NegativeSign = "~";
        try
        {
            CultureInfo.CurrentCulture = custom;
            Int128 expected = Int128.MinValue + 17;
            string text = LuminPackSerializer.SerializeJson(expected);
            Assert.Equal("\"" + expected.ToString(CultureInfo.InvariantCulture) + "\"", text);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(expected, LuminPackSerializer.DeserializeJson<Int128>(text));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Checks absent qualified names fail explicitly rather than silently becoming null.</summary>
    [Fact]
    public void UnsupportedTypeShapesAreRejected()
    {
        Type parameter = typeof(List<>).GetGenericArguments()[0];
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.Serialize(parameter));
        Assert.Throws<NotSupportedException>(() => LuminPackSerializer.SerializeJson(parameter));
    }

    /// <summary>Checks Type identities round-trip or reject reads according to dynamic code support.</summary>
    [Fact]
    public void SupportedTypeShapesRoundTrip()
    {
        foreach (Type expected in new[] { typeof(int), typeof(Dictionary<string, List<int[]>>), typeof(List<>), typeof(int).MakeByRefType() })
        {
            byte[] bytes = LuminPackSerializer.Serialize(expected);
            string json = LuminPackSerializer.SerializeJson(expected);
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                Assert.Equal(expected, LuminPackSerializer.Deserialize<Type>(bytes));
                Assert.Equal(expected, LuminPackSerializer.DeserializeJson<Type>(json));
            }
            else
            {
                Assert.Throws<PlatformNotSupportedException>(() => LuminPackSerializer.Deserialize<Type>(bytes));
                Assert.Throws<PlatformNotSupportedException>(() => LuminPackSerializer.DeserializeJson<Type>(json));
            }
        }
        Assert.Null(LuminPackSerializer.Deserialize<Type>(LuminPackSerializer.Serialize((Type)null!)));
        Assert.Null(LuminPackSerializer.DeserializeJson<Type>("null"));
    }

    /// <summary>Checks an empty managed stack remains distinct from a null stack.</summary>
    [Fact]
    public void EmptyManagedStackRemainsEmpty()
    {
        var expected = new Stack<string>();
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        var copy = LuminPackSerializer.Deserialize<Stack<string>>(bytes);
        Assert.NotNull(copy);
        Assert.Empty(copy);
        Assert.Equal(0, BitConverter.ToInt32(bytes));
    }

    /// <summary>Checks the actual BCL builder mapping, raw elements, JSON, evaluation and reused capacity.</summary>
    [Fact]
    public void ReadOnlyCollectionBuilderUsesItsActualStorage()
    {
        var expected = new ReadOnlyCollectionBuilder<int> { 255, -1, 42 };
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(new[] { 255, -1, 42 }, LuminPackSerializer.Deserialize<ReadOnlyCollectionBuilder<int>>(bytes));
        var reused = new ReadOnlyCollectionBuilder<int>(32) { 9, 8, 7, 6, 5 };
        var original = reused;
        LuminPackSerializer.Deserialize(bytes, ref reused);
        Assert.Same(original, reused);
        Assert.Equal(expected, reused);
        Assert.Equal(32, reused.Capacity);
        Assert.Equal(expected, LuminPackSerializer.DeserializeJson<ReadOnlyCollectionBuilder<int>>(LuminPackSerializer.SerializeJson(expected)));
        var portable = new ReadOnlyCollectionBuilder<AuditPortableElement> { new() { Tag = 17, Value = -1 } };
        Assert.Equal(portable, LuminPackSerializer.Deserialize<ReadOnlyCollectionBuilder<AuditPortableElement>>(LuminPackSerializer.Serialize(portable)));
    }

    /// <summary>Checks thread-invariant settings do not suppress cultures supported by the actual runtime.</summary>
    [Fact]
    public void ThreadInvariantCultureDoesNotDisableCultureResolution()
    {
        CultureInfo expected;
        try { expected = CultureInfo.GetCultureInfo("en-US"); }
        catch (CultureNotFoundException) { return; }
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Assert.Equal(expected.Name, LuminPackSerializer.Deserialize<CultureInfo>(LuminPackSerializer.Serialize(expected)).Name);
            Assert.Equal(expected.Name, LuminPackSerializer.DeserializeJson<CultureInfo>(LuminPackSerializer.SerializeJson(expected)).Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    /// <summary>Checks wrapper presence and byte evaluation with each statically selected string representation.</summary>
    [Fact]
    public void WrapperFramingSupportsEveryStringProfile()
    {
        CheckWrapperProfile<LuminPackDefaultOptions>();
        CheckWrapperProfile<LuminPackUtf8TokenOptions>();
        CheckWrapperProfile<LuminPackUtf16LengthOptions>();
        CheckWrapperProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks throwing callbacks return pooled element arrays and preserve exception identity.</summary>
    [Fact]
    public void PooledCollectionFailuresReturnAndClearTheirRental()
    {
        var expected = new SortedSet<AuditPooledItem> { new() { Id = 1 }, new() { Id = 2 } };
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        var rental = ArrayPool<AuditPooledItem>.Shared.Rent(2);
        ArrayPool<AuditPooledItem>.Shared.Return(rental, clearArray: true);
        try
        {
            AuditPooledItem.FailWrite = true;
            Assert.Same(AuditPooledItem.Failure, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(expected)));
            var returned = ArrayPool<AuditPooledItem>.Shared.Rent(2);
            try { Assert.Same(rental, returned); Assert.All(returned, Assert.Null); }
            finally { ArrayPool<AuditPooledItem>.Shared.Return(returned, clearArray: true); }
            AuditPooledItem.FailWrite = false;
            AuditPooledItem.ReadsUntilFailure = 2;
            Assert.Same(AuditPooledItem.Failure, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Deserialize<SortedSet<AuditPooledItem>>(bytes)));
            returned = ArrayPool<AuditPooledItem>.Shared.Rent(2);
            try { Assert.Same(rental, returned); Assert.All(returned, Assert.Null); }
            finally { ArrayPool<AuditPooledItem>.Shared.Return(returned, clearArray: true); }
        }
        finally
        {
            AuditPooledItem.FailWrite = false;
            AuditPooledItem.ReadsUntilFailure = 0;
        }
    }

    /// <summary>Checks one option family, including empty strings whose token representation is normalized.</summary>
    /// <typeparam name="TOptions">The closed string profile.</typeparam>
    private static void CheckWrapperProfile<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        byte[] bytes = LuminPackSerializer.Serialize<CultureInfo, TOptions>(culture);
        Assert.Equal(culture.Name, LuminPackSerializer.Deserialize<CultureInfo, TOptions>(bytes).Name);
        Uri uri = new("https://example.com/" + new string('a', 235));
        bytes = LuminPackSerializer.Serialize<Uri, TOptions>(uri);
        Assert.Equal(uri, LuminPackSerializer.Deserialize<Uri, TOptions>(bytes));
        Type type = typeof(Dictionary<string, int[]>);
        bytes = LuminPackSerializer.Serialize<Type, TOptions>(type);
        if (RuntimeFeature.IsDynamicCodeSupported)
            Assert.Equal(type, LuminPackSerializer.Deserialize<Type, TOptions>(bytes));
        else
            Assert.Throws<PlatformNotSupportedException>(() => LuminPackSerializer.Deserialize<Type, TOptions>(bytes));
        Assert.Null(LuminPackSerializer.Deserialize<Type, TOptions>(LuminPackSerializer.Serialize<Type, TOptions>(null!)));
        Uri? absent = null;
        bytes = LuminPackSerializer.Serialize<Uri?, TOptions>(absent);
        Assert.Null(LuminPackSerializer.Deserialize<Uri?, TOptions>(bytes));
        Assert.Single(bytes);
    }

    /// <summary>Reads a deliberately truncated view while keeping its full backing allocation accessible.</summary>
    /// <param name="backing">The allocation containing the forged record.</param>
    /// <param name="existing">The builder whose mutation must follow validation.</param>
    private static void ReadTruncatedBuilder(byte[] backing, ref StringBuilder existing)
    {
        Span<byte> input = backing.AsSpan(0, 6);
        var reader = new LuminPackReader(ref input);
        LuminPackSerializer.Deserialize(ref reader, ref existing);
    }
}

/// <summary>Provides an element whose nine-byte portable wire differs from its native stride.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct AuditPortableElement
{
    public byte Tag;
    public long Value;
}

/// <summary>Uses a deliberately different wire width from its four-byte native storage.</summary>
[LuminPackable(GeneratorType.Custom)]
public struct AuditCustomElement
{
    public int Value;

    /// <summary>Writes only the low unsigned word through the registered Custom boundary.</summary>
    /// <param name="writer">The binary output state.</param>
    /// <param name="value">The fixture value.</param>
    public static void Serialize(ref LuminPackWriter writer, in AuditCustomElement value) => writer.Write((ushort)value.Value);

    /// <summary>Restores the unsigned word through the registered Custom boundary.</summary>
    /// <param name="reader">The binary input state.</param>
    /// <param name="value">The destination fixture storage.</param>
    public static void Deserialize(ref LuminPackReader reader, ref AuditCustomElement value) => value.Value = reader.Read<ushort>();
}

/// <summary>Exercises an inaccessible field followed by storage whose native packing is explicit.</summary>
[LuminPackable, StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial class AuditPackedPrivate
{
    [LuminPackInclude] private byte _tag;
    public long Value;
    public byte Tag => _tag;

    /// <summary>Creates recognizable storage without introducing a constructor-bound read contract.</summary>
    /// <returns>A packed instance with nonzero values.</returns>
    public static AuditPackedPrivate Create() => new() { _tag = 23, Value = long.MinValue + 123456789 };
}

/// <summary>Supplies controlled callback failures inside a pooled collection traversal.</summary>
[LuminPackable]
public partial class AuditPooledItem : IComparable<AuditPooledItem>
{
    public int Id;
    internal static readonly InvalidOperationException Failure = new("Controlled pooled traversal failure.");
    internal static bool FailWrite;
    internal static int ReadsUntilFailure;

    /// <summary>Orders fixture elements by their serialized identity.</summary>
    /// <param name="other">The other fixture item.</param>
    /// <returns>The relative numeric order.</returns>
    public int CompareTo(AuditPooledItem? other) => other is null ? 1 : Id.CompareTo(other.Id);

    /// <summary>Throws the original fixture exception when a writer failure is requested.</summary>
    [LuminPackOnSerializing]
    public void BeforeWrite()
    {
        if (FailWrite) throw Failure;
    }

    /// <summary>Fails on the selected item after an earlier pooled slot may have acquired a reference.</summary>
    [LuminPackOnDeserializing]
    public static void BeforeRead()
    {
        if (ReadsUntilFailure > 0 && --ReadsUntilFailure == 0) throw Failure;
    }
}
