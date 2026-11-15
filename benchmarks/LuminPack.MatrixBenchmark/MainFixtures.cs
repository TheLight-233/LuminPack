using LuminPack;

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Buffers;
using System.Runtime.CompilerServices;
using LuminPackBenchmark;

internal static partial class Fixtures
{
    /// <summary>Builds the fixtures for the main benchmark scene.</summary>
    /// <returns>The resulting <c>Fixture[]</c>.</returns>
    internal static Fixture[] MainCases() =>
    [
        Fixture.Create<int>("Int32", CreateInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<string>("String", CreateString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref string value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref string value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Guid>("Guid", CreateGuid,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Guid value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Guid value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<DateTime>("DateTime", CreateDateTime,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref DateTime value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref DateTime value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<DateTimeOffset>("DateTimeOffset", CreateDateTimeOffset,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref DateTimeOffset value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref DateTimeOffset value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<TimeSpan>("TimeSpan", CreateTimeSpan,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref TimeSpan value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref TimeSpan value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<decimal>("Decimal", CreateDecimal,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref decimal value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref decimal value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Half>("Half", CreateHalf,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Half value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Half value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Int128>("Int128", CreateInt128,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Int128 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Int128 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<UInt128>("UInt128", CreateUInt128,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref UInt128 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref UInt128 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<DateOnly>("DateOnly", CreateDateOnly,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref DateOnly value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref DateOnly value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<TimeOnly>("TimeOnly", CreateTimeOnly,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref TimeOnly value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref TimeOnly value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Rune>("Rune", CreateRune,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Rune value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Rune value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BigInteger>("BigInteger", CreateBigInteger,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BigInteger value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BigInteger value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BitArray>("BitArray", CreateBitArray,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BitArray value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BitArray value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<StringBuilder>("StringBuilder", CreateStringBuilder,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref StringBuilder value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref StringBuilder value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Uri>("Uri", CreateUri,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Uri value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Uri value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Version>("Version", CreateVersion,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Version value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Version value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<CultureInfo>("CultureInfo", CreateCultureInfo,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref CultureInfo value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref CultureInfo value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<TimeZoneInfo>("TimeZoneInfo", CreateTimeZoneInfo,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref TimeZoneInfo value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref TimeZoneInfo value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Type>("SystemType", CreateSystemType,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Type value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Type value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Memory<int>>("MemoryInt32", CreateMemoryInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Memory<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Memory<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlyMemory<int>>("ReadOnlyMemoryInt32", CreateReadOnlyMemoryInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyMemory<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyMemory<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ArraySegment<int>>("ArraySegmentInt32", CreateArraySegmentInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ArraySegment<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ArraySegment<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<KeyValuePair<int, string>>("KeyValuePairInt32String", CreateKeyValuePairInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref KeyValuePair<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref KeyValuePair<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Tuple<int, string, bool>>("TupleInt32StringBoolean", CreateTupleInt32StringBoolean,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Tuple<int, string, bool> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Tuple<int, string, bool> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<(int, string, bool)>("ValueTupleInt32StringBoolean", CreateValueTupleInt32StringBoolean,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref (int, string, bool) value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref (int, string, bool) value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<int?>("NullableInt32", CreateNullableInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int? value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int? value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<int[]>("Int32Array", CreateInt32Array,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int[] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int[] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<int[,]>("Int32Matrix", CreateInt32Matrix,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int[,] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int[,] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Complex>("Complex", CreateComplex,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Complex value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Complex value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Vector2>("Vector2", CreateVector2,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Vector2 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Vector2 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Vector3>("Vector3", CreateVector3,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Vector3 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Vector3 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Vector4>("Vector4", CreateVector4,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Vector4 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Vector4 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Quaternion>("Quaternion", CreateQuaternion,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Quaternion value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Quaternion value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Matrix4x4>("Matrix4x4", CreateMatrix4x4,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Matrix4x4 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Matrix4x4 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Plane>("Plane", CreatePlane,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Plane value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Plane value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<int>>("ListInt32", CreateListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<string>>("ListString", CreateListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<LinkedList<int>>("LinkedListInt32", CreateLinkedListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref LinkedList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref LinkedList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Queue<int>>("QueueInt32", CreateQueueInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Queue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Queue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Stack<int>>("StackInt32", CreateStackInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Stack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Stack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<HashSet<int>>("HashSetInt32", CreateHashSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedSet<int>>("SortedSetInt32", CreateSortedSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedList<int, string>>("SortedListInt32String", CreateSortedListInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedDictionary<int, string>>("SortedDictionaryInt32String", CreateSortedDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Dictionary<int, string>>("DictionaryInt32String", CreateDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Dictionary<string, int>>("DictionaryStringInt32", CreateDictionaryStringInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string, int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string, int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentBag<int>>("ConcurrentBagInt32", CreateConcurrentBagInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentBag<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentBag<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentDictionary<int, string>>("ConcurrentDictionaryInt32String", CreateConcurrentDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentQueue<int>>("ConcurrentQueueInt32", CreateConcurrentQueueInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentStack<int>>("ConcurrentStackInt32", CreateConcurrentStackInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BlockingCollection<int>>("BlockingCollectionInt32", CreateBlockingCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BlockingCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BlockingCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<int>>("ImmutableArrayInt32", CreateImmutableArrayInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<int>>("ImmutableListInt32", CreateImmutableListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableDictionary<int, string>>("ImmutableDictionaryInt32String", CreateImmutableDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableHashSet<int>>("ImmutableHashSetInt32", CreateImmutableHashSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableQueue<int>>("ImmutableQueueInt32", CreateImmutableQueueInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedDictionary<int, string>>("ImmutableSortedDictionaryInt32String", CreateImmutableSortedDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedSet<int>>("ImmutableSortedSetInt32", CreateImmutableSortedSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<int>>("ImmutableStackInt32", CreateImmutableStackInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<int>>("ICollectionInt32", CreateICollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<int>>("IListInt32", CreateIListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<int>>("IReadOnlyCollectionInt32", CreateIReadOnlyCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<int>>("IReadOnlyListInt32", CreateIReadOnlyListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ISet<int>>("ISetInt32", CreateISetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlySet<int>>("IReadOnlySetInt32", CreateIReadOnlySetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IDictionary<int, string>>("IDictionaryInt32String", CreateIDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyDictionary<int, string>>("IReadOnlyDictionaryInt32String", CreateIReadOnlyDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableList<int>>("IImmutableListInt32", CreateIImmutableListInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableDictionary<int, string>>("IImmutableDictionaryInt32String", CreateIImmutableDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableQueue<int>>("IImmutableQueueInt32", CreateIImmutableQueueInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableSet<int>>("IImmutableSetInt32", CreateIImmutableSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableStack<int>>("IImmutableStackInt32", CreateIImmutableStackInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollection<int>>("ReadOnlyCollectionInt32", CreateReadOnlyCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyDictionary<int, string>>("ReadOnlyDictionaryInt32String", CreateReadOnlyDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyObservableCollection<int>>("ReadOnlyObservableCollectionInt32", CreateReadOnlyObservableCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyObservableCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyObservableCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ObservableCollection<int>>("ObservableCollectionInt32", CreateObservableCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ObservableCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ObservableCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Collection<int>>("CollectionInt32", CreateCollectionInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Collection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Collection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<FrozenDictionary<int, string>>("FrozenDictionaryInt32String", CreateFrozenDictionaryInt32String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenDictionary<int, string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenDictionary<int, string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<FrozenSet<int>>("FrozenSetInt32", CreateFrozenSetInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<PriorityQueue<int, int>>("PriorityQueueInt32Int32", CreatePriorityQueueInt32Int32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<int, int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<int, int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Lazy<int>>("LazyInt32", CreateLazyInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Lazy<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Lazy<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Value)),
        Fixture.Create<IEnumerable<int>>("IEnumerableInt32", CreateIEnumerableInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<bool>("Boolean", CreateBoolean,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref bool value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref bool value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<byte>("Byte", CreateByte,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref byte value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref byte value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<sbyte>("SByte", CreateSByte,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref sbyte value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref sbyte value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<short>("Int16", CreateInt16,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref short value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref short value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ushort>("UInt16", CreateUInt16,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ushort value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ushort value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<uint>("UInt32", CreateUInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref uint value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref uint value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<long>("Int64", CreateInt64,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref long value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref long value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ulong>("UInt64", CreateUInt64,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ulong value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ulong value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<float>("Single", CreateSingle,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref float value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref float value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<double>("Double", CreateDouble,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref double value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref double value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<char>("Char", CreateChar,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref char value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref char value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<nint>("IntPtr", CreateIntPtr,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref nint value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref nint value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<nuint>("UIntPtr", CreateUIntPtr,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref nuint value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref nuint value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SampleEnum>("Enum", CreateEnum,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SampleEnum value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SampleEnum value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<int?>("NullableEmpty", CreateNullableEmpty,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int? value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int? value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<string>("StringUnicode", CreateStringUnicode,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref string value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref string value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<System.Buffers.ReadOnlySequence<int>>("ReadOnlySequenceInt32", CreateReadOnlySequenceInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref System.Buffers.ReadOnlySequence<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref System.Buffers.ReadOnlySequence<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<List<string>>("Reference_ListString", CreateReference_ListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<Record>>("Reference_ListClass", CreateReference_ListClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<PriorityQueue<string,int>>("Reference_PriorityQueueString", CreateReference_PriorityQueueString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<PriorityQueue<Record,int>>("Reference_PriorityQueueClass", CreateReference_PriorityQueueClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<Record,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<Record,int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<HashSet<string>>("Reference_HashSetString", CreateReference_HashSetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<HashSet<Record>>("Reference_HashSetClass", CreateReference_HashSetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedSet<string>>("Reference_SortedSetString", CreateReference_SortedSetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedSet<Record>>("Reference_SortedSetClass", CreateReference_SortedSetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<string>>("Reference_ImmutableArrayString", CreateReference_ImmutableArrayString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<Record>>("Reference_ImmutableArrayClass", CreateReference_ImmutableArrayClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<string>>("Reference_ImmutableListString", CreateReference_ImmutableListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<Record>>("Reference_ImmutableListClass", CreateReference_ImmutableListClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableHashSet<string>>("Reference_ImmutableHashSetString", CreateReference_ImmutableHashSetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableHashSet<Record>>("Reference_ImmutableHashSetClass", CreateReference_ImmutableHashSetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedSet<string>>("Reference_ImmutableSortedSetString", CreateReference_ImmutableSortedSetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedSet<Record>>("Reference_ImmutableSortedSetClass", CreateReference_ImmutableSortedSetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableQueue<string>>("Reference_ImmutableQueueString", CreateReference_ImmutableQueueString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableQueue<Record>>("Reference_ImmutableQueueClass", CreateReference_ImmutableQueueClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<string>>("Reference_ImmutableStackString", CreateReference_ImmutableStackString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<Record>>("Reference_ImmutableStackClass", CreateReference_ImmutableStackClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IEnumerable<string>>("Reference_IEnumerableString", CreateReference_IEnumerableString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IEnumerable<Record>>("Reference_IEnumerableClass", CreateReference_IEnumerableClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<string>>("Reference_ICollectionString", CreateReference_ICollectionString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<Record>>("Reference_ICollectionClass", CreateReference_ICollectionClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<string>>("Reference_IListString", CreateReference_IListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<Record>>("Reference_IListClass", CreateReference_IListClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<string>>("Reference_IReadOnlyListString", CreateReference_IReadOnlyListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<Record>>("Reference_IReadOnlyListClass", CreateReference_IReadOnlyListClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<string>>("Reference_IReadOnlyCollectionString", CreateReference_IReadOnlyCollectionString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<Record>>("Reference_IReadOnlyCollectionClass", CreateReference_IReadOnlyCollectionClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ISet<string>>("Reference_ISetString", CreateReference_ISetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ISet<Record>>("Reference_ISetClass", CreateReference_ISetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlySet<string>>("Reference_IReadOnlySetString", CreateReference_IReadOnlySetString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlySet<Record>>("Reference_IReadOnlySetClass", CreateReference_IReadOnlySetClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Dictionary<string,string>>("Reference_DictionaryString", CreateReference_DictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Dictionary<string,Record>>("Reference_DictionaryClass", CreateReference_DictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedDictionary<string,string>>("Reference_SortedDictionaryString", CreateReference_SortedDictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedDictionary<string,Record>>("Reference_SortedDictionaryClass", CreateReference_SortedDictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedList<string,string>>("Reference_SortedListString", CreateReference_SortedListString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SortedList<string,Record>>("Reference_SortedListClass", CreateReference_SortedListClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableDictionary<string,string>>("Reference_ImmutableDictionaryString", CreateReference_ImmutableDictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableDictionary<string,Record>>("Reference_ImmutableDictionaryClass", CreateReference_ImmutableDictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedDictionary<string,string>>("Reference_ImmutableSortedDictionaryString", CreateReference_ImmutableSortedDictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableSortedDictionary<string,Record>>("Reference_ImmutableSortedDictionaryClass", CreateReference_ImmutableSortedDictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IDictionary<string,string>>("Reference_IDictionaryString", CreateReference_IDictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IDictionary<string,Record>>("Reference_IDictionaryClass", CreateReference_IDictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyDictionary<string,string>>("Reference_IReadOnlyDictionaryString", CreateReference_IReadOnlyDictionaryString,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyDictionary<string,Record>>("Reference_IReadOnlyDictionaryClass", CreateReference_IReadOnlyDictionaryClass,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<string,Record> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<string,Record> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<SimpleClassBase>("SimpleBenchmark", CreateSimpleBenchmark,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SimpleClassBase value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SimpleClassBase value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<CharacterSaveData>("SerializeBenchmark", CreateSerializeBenchmark,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref CharacterSaveData value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref CharacterSaveData value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Matrix3x2>("Matrix3x2", CreateMatrix3x2,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Matrix3x2 value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Matrix3x2 value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollectionBuilder<int>>("ReadOnlyCollectionBuilderInt32", CreateReadOnlyCollectionBuilderInt32,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollectionBuilder<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollectionBuilder<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<CharacterSaveData>("JsonBenchmark", CreateJsonBenchmark,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref CharacterSaveData value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref CharacterSaveData value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<PolymorphismClassBase>("PolymorphismBenchmark", CreatePolymorphismBenchmark,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PolymorphismClassBase value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PolymorphismClassBase value) => LuminPackSerializer.DeserializeJson(data, ref value)),
    ];

    /// <summary>Creates the benchmark input for <c>Int32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int CreateInt32(int offset) => 42;
    /// <summary>Creates the benchmark input for <c>String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static string CreateString(int offset) => new string('x', 256);
    /// <summary>Creates the benchmark input for <c>Guid</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Guid CreateGuid(int offset) => Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b");
    /// <summary>Creates the benchmark input for <c>DateTime</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static DateTime CreateDateTime(int offset) => new DateTime(2026, 10, 22, 12, 34, 56, DateTimeKind.Utc);
    /// <summary>Creates the benchmark input for <c>DateTimeOffset</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static DateTimeOffset CreateDateTimeOffset(int offset) => new DateTimeOffset(2026, 10, 22, 12, 34, 56, TimeSpan.FromHours(9));
    /// <summary>Creates the benchmark input for <c>TimeSpan</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static TimeSpan CreateTimeSpan(int offset) => TimeSpan.FromMinutes(123.456);
    /// <summary>Creates the benchmark input for <c>Decimal</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static decimal CreateDecimal(int offset) => 123456.789m;
    /// <summary>Creates the benchmark input for <c>Half</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Half CreateHalf(int offset) => (Half)3.14;
    /// <summary>Creates the benchmark input for <c>Int128</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Int128 CreateInt128(int offset) => Int128.MaxValue;
    /// <summary>Creates the benchmark input for <c>UInt128</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static UInt128 CreateUInt128(int offset) => UInt128.MaxValue;
    /// <summary>Creates the benchmark input for <c>DateOnly</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static DateOnly CreateDateOnly(int offset) => new DateOnly(2026, 10, 22);
    /// <summary>Creates the benchmark input for <c>TimeOnly</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static TimeOnly CreateTimeOnly(int offset) => new TimeOnly(12, 34, 56, 789);
    /// <summary>Creates the benchmark input for <c>Rune</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Rune CreateRune(int offset) => new Rune('A');
    /// <summary>Creates the benchmark input for <c>BigInteger</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BigInteger CreateBigInteger(int offset) => BigInteger.Parse("123456789012345678901234567890");
    /// <summary>Creates the benchmark input for <c>BitArray</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BitArray CreateBitArray(int offset) => new BitArray(Enumerable.Range(0, 256).Select(i => i % 3 == 0).ToArray());
    /// <summary>Creates the benchmark input for <c>StringBuilder</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static StringBuilder CreateStringBuilder(int offset) => new StringBuilder("Hello World").Append('!', 128);
    /// <summary>Creates the benchmark input for <c>Uri</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Uri CreateUri(int offset) => new Uri("https://example.com/path?query=value");
    /// <summary>Creates the benchmark input for <c>Version</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Version CreateVersion(int offset) => new Version(1, 2, 3, 4);
    /// <summary>Creates the benchmark input for <c>CultureInfo</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static CultureInfo CreateCultureInfo(int offset) => CultureInfo.GetCultureInfo("en-US");
    /// <summary>Creates the benchmark input for <c>TimeZoneInfo</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static TimeZoneInfo CreateTimeZoneInfo(int offset) => TimeZoneInfo.Local;
    /// <summary>Creates the benchmark input for <c>SystemType</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Type CreateSystemType(int offset) => typeof(string);
    /// <summary>Creates the benchmark input for <c>MemoryInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Memory<int> CreateMemoryInt32(int offset) => Enumerable.Range(0, 1000).ToArray();
    /// <summary>Creates the benchmark input for <c>ReadOnlyMemoryInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyMemory<int> CreateReadOnlyMemoryInt32(int offset) => Enumerable.Range(0, 1000).ToArray();
    /// <summary>Creates the benchmark input for <c>ArraySegmentInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ArraySegment<int> CreateArraySegmentInt32(int offset) => new ArraySegment<int>(Enumerable.Range(0, 1000).ToArray());
    /// <summary>Creates the benchmark input for <c>KeyValuePairInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static KeyValuePair<int, string> CreateKeyValuePairInt32String(int offset) => new KeyValuePair<int, string>(7, "seven");
    /// <summary>Creates the benchmark input for <c>TupleInt32StringBoolean</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Tuple<int, string, bool> CreateTupleInt32StringBoolean(int offset) => Tuple.Create(42, "Hello", true);
    /// <summary>Creates the benchmark input for <c>ValueTupleInt32StringBoolean</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static (int, string, bool) CreateValueTupleInt32StringBoolean(int offset) => (42, "Hello", true);
    /// <summary>Creates the benchmark input for <c>NullableInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int? CreateNullableInt32(int offset) => 42;
    /// <summary>Creates the benchmark input for <c>Int32Array</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int[] CreateInt32Array(int offset) => Enumerable.Range(0, 1000).ToArray();
    /// <summary>Creates the benchmark input for <c>Int32Matrix</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int[,] CreateInt32Matrix(int offset)
    {
        var matrix = new int[32, 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                matrix[y, x] = y * 32 + x;
        return matrix;
    }
    /// <summary>Creates the benchmark input for <c>Complex</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Complex CreateComplex(int offset) => new Complex(3.0, 4.0);
    /// <summary>Creates the benchmark input for <c>Vector2</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Vector2 CreateVector2(int offset) => new Vector2(1.5f, 2.5f);
    /// <summary>Creates the benchmark input for <c>Vector3</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Vector3 CreateVector3(int offset) => new Vector3(1.5f, 2.5f, 3.5f);
    /// <summary>Creates the benchmark input for <c>Vector4</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Vector4 CreateVector4(int offset) => new Vector4(1.5f, 2.5f, 3.5f, 4.5f);
    /// <summary>Creates the benchmark input for <c>Quaternion</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Quaternion CreateQuaternion(int offset) => new Quaternion(1f, 2f, 3f, 4f);
    /// <summary>Creates the benchmark input for <c>Matrix4x4</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Matrix4x4 CreateMatrix4x4(int offset) => Matrix4x4.Identity;
    /// <summary>Creates the benchmark input for <c>Plane</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Plane CreatePlane(int offset) => new Plane(1f, 2f, 3f, 4f);
    /// <summary>Creates the benchmark input for <c>ListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<int> CreateListInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>ListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<string> CreateListString(int offset) => Enumerable.Range(0, 100).Select(i => "item_" + i).ToList();
    /// <summary>Creates the benchmark input for <c>LinkedListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static LinkedList<int> CreateLinkedListInt32(int offset) => new LinkedList<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>QueueInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Queue<int> CreateQueueInt32(int offset) => new Queue<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>StackInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Stack<int> CreateStackInt32(int offset) => new Stack<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>HashSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<int> CreateHashSetInt32(int offset) => Enumerable.Range(0, 1000).ToHashSet();
    /// <summary>Creates the benchmark input for <c>SortedSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<int> CreateSortedSetInt32(int offset) => new SortedSet<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>SortedListInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<int, string> CreateSortedListInt32String(int offset) =>
        new SortedList<int, string>(Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i));
    /// <summary>Creates the benchmark input for <c>SortedDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<int, string> CreateSortedDictionaryInt32String(int offset) =>
        new SortedDictionary<int, string>(Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i));
    /// <summary>Creates the benchmark input for <c>DictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<int, string> CreateDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>DictionaryStringInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, int> CreateDictionaryStringInt32(int offset) =>
        Enumerable.Range(0, 500).ToDictionary(i => "k" + i, i => i);
    /// <summary>Creates the benchmark input for <c>ConcurrentBagInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentBag<int> CreateConcurrentBagInt32(int offset) => new ConcurrentBag<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>ConcurrentDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentDictionary<int, string> CreateConcurrentDictionaryInt32String(int offset) =>
        new ConcurrentDictionary<int, string>(Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i));
    /// <summary>Creates the benchmark input for <c>ConcurrentQueueInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentQueue<int> CreateConcurrentQueueInt32(int offset) => new ConcurrentQueue<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>ConcurrentStackInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentStack<int> CreateConcurrentStackInt32(int offset) => new ConcurrentStack<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>BlockingCollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BlockingCollection<int> CreateBlockingCollectionInt32(int offset)
    {
        var collection = new BlockingCollection<int>();
        foreach (int value in Enumerable.Range(0, 1000))
            collection.Add(value);
        return collection;
    }
    /// <summary>Creates the benchmark input for <c>ImmutableArrayInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<int> CreateImmutableArrayInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableArray();
    /// <summary>Creates the benchmark input for <c>ImmutableListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<int> CreateImmutableListInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableList();
    /// <summary>Creates the benchmark input for <c>ImmutableDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<int, string> CreateImmutableDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToImmutableDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>ImmutableHashSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<int> CreateImmutableHashSetInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableHashSet();
    /// <summary>Creates the benchmark input for <c>ImmutableQueueInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<int> CreateImmutableQueueInt32(int offset) => ImmutableQueue.CreateRange(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>ImmutableSortedDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<int, string> CreateImmutableSortedDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToImmutableSortedDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>ImmutableSortedSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<int> CreateImmutableSortedSetInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableSortedSet();
    /// <summary>Creates the benchmark input for <c>ImmutableStackInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<int> CreateImmutableStackInt32(int offset) => ImmutableStack.CreateRange(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>ICollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<int> CreateICollectionInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>IListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<int> CreateIListInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>IReadOnlyCollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<int> CreateIReadOnlyCollectionInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>IReadOnlyListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<int> CreateIReadOnlyListInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>ISetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<int> CreateISetInt32(int offset) => Enumerable.Range(0, 1000).ToHashSet();
    /// <summary>Creates the benchmark input for <c>IReadOnlySetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<int> CreateIReadOnlySetInt32(int offset) => Enumerable.Range(0, 1000).ToHashSet();
    /// <summary>Creates the benchmark input for <c>IDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<int, string> CreateIDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>IReadOnlyDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<int, string> CreateIReadOnlyDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>IImmutableListInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableList<int> CreateIImmutableListInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableList();
    /// <summary>Creates the benchmark input for <c>IImmutableDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableDictionary<int, string> CreateIImmutableDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToImmutableDictionary(i => i, i => "v" + i);
    /// <summary>Creates the benchmark input for <c>IImmutableQueueInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableQueue<int> CreateIImmutableQueueInt32(int offset) => ImmutableQueue.CreateRange(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>IImmutableSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableSet<int> CreateIImmutableSetInt32(int offset) => Enumerable.Range(0, 1000).ToImmutableHashSet();
    /// <summary>Creates the benchmark input for <c>IImmutableStackInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableStack<int> CreateIImmutableStackInt32(int offset) => ImmutableStack.CreateRange(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>ReadOnlyCollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollection<int> CreateReadOnlyCollectionInt32(int offset) => new ReadOnlyCollection<int>(Enumerable.Range(0, 1000).ToList());
    /// <summary>Creates the benchmark input for <c>ReadOnlyDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyDictionary<int, string> CreateReadOnlyDictionaryInt32String(int offset) =>
        new ReadOnlyDictionary<int, string>(Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i));
    /// <summary>Creates the benchmark input for <c>ReadOnlyObservableCollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyObservableCollection<int> CreateReadOnlyObservableCollectionInt32(int offset) =>
        new ReadOnlyObservableCollection<int>(new ObservableCollection<int>(Enumerable.Range(0, 1000)));
    /// <summary>Creates the benchmark input for <c>ObservableCollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ObservableCollection<int> CreateObservableCollectionInt32(int offset) => new ObservableCollection<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>CollectionInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Collection<int> CreateCollectionInt32(int offset) => new Collection<int>(Enumerable.Range(0, 1000).ToList());
    /// <summary>Creates the benchmark input for <c>FrozenDictionaryInt32String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenDictionary<int, string> CreateFrozenDictionaryInt32String(int offset) =>
        Enumerable.Range(0, 500).ToDictionary(i => i, i => "v" + i).ToFrozenDictionary();
    /// <summary>Creates the benchmark input for <c>FrozenSetInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenSet<int> CreateFrozenSetInt32(int offset) => Enumerable.Range(0, 1000).ToFrozenSet();
    /// <summary>Creates the benchmark input for <c>PriorityQueueInt32Int32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<int, int> CreatePriorityQueueInt32Int32(int offset)
    {
        var queue = new PriorityQueue<int, int>();
        for (int i = 0; i < 1000; i++)
            queue.Enqueue(i, i);
        return queue;
    }
    /// <summary>Creates the benchmark input for <c>LazyInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Lazy<int> CreateLazyInt32(int offset) => new Lazy<int>(() => 42);
    /// <summary>Creates the benchmark input for <c>IEnumerableInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<int> CreateIEnumerableInt32(int offset) => Enumerable.Range(0, 1000).ToList();
    /// <summary>Creates the benchmark input for <c>Boolean</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static bool CreateBoolean(int offset) => true;
    /// <summary>Creates the benchmark input for <c>Byte</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static byte CreateByte(int offset) => 123;
    /// <summary>Creates the benchmark input for <c>SByte</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static sbyte CreateSByte(int offset) => -42;
    /// <summary>Creates the benchmark input for <c>Int16</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static short CreateInt16(int offset) => -1234;
    /// <summary>Creates the benchmark input for <c>UInt16</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ushort CreateUInt16(int offset) => 54321;
    /// <summary>Creates the benchmark input for <c>UInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static uint CreateUInt32(int offset) => 123456789u;
    /// <summary>Creates the benchmark input for <c>Int64</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static long CreateInt64(int offset) => -12345678901234L;
    /// <summary>Creates the benchmark input for <c>UInt64</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ulong CreateUInt64(int offset) => 12345678901234UL;
    /// <summary>Creates the benchmark input for <c>Single</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static float CreateSingle(int offset) => 123.25f;
    /// <summary>Creates the benchmark input for <c>Double</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static double CreateDouble(int offset) => 123456.125;
    /// <summary>Creates the benchmark input for <c>Char</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static char CreateChar(int offset) => 'Z';
    /// <summary>Creates the benchmark input for <c>IntPtr</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static nint CreateIntPtr(int offset) => (nint)123456;
    /// <summary>Creates the benchmark input for <c>UIntPtr</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static nuint CreateUIntPtr(int offset) => (nuint)123456;
    /// <summary>Creates the benchmark input for <c>Enum</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SampleEnum CreateEnum(int offset) => SampleEnum.Second;
    /// <summary>Creates the benchmark input for <c>NullableEmpty</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int? CreateNullableEmpty(int offset) => null;
    /// <summary>Creates the benchmark input for <c>StringUnicode</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static string CreateStringUnicode(int offset) => "sample-\u4e2d\u6587-\ud83d\ude03";
    /// <summary>Creates the benchmark input for <c>ReadOnlySequenceInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static System.Buffers.ReadOnlySequence<int> CreateReadOnlySequenceInt32(int offset) => new System.Buffers.ReadOnlySequence<int>(Enumerable.Range(0, 1000).ToArray());
    /// <summary>Creates the benchmark input for <c>Reference_ListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<string> CreateReference_ListString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_ListClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<Record> CreateReference_ListClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_PriorityQueueString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<string, int> CreateReference_PriorityQueueString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new PriorityQueue<string, int>(texts.Select((v, i) => (v, i))); }
    /// <summary>Creates the benchmark input for <c>Reference_PriorityQueueClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<Record, int> CreateReference_PriorityQueueClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new PriorityQueue<Record, int>(records.Select((v, i) => (v, i))); }
    /// <summary>Creates the benchmark input for <c>Reference_HashSetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<string> CreateReference_HashSetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_HashSetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<Record> CreateReference_HashSetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedSetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<string> CreateReference_SortedSetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedSet<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedSetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<Record> CreateReference_SortedSetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedSet<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableArrayString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<string> CreateReference_ImmutableArrayString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.ToImmutableArray(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableArrayClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<Record> CreateReference_ImmutableArrayClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.ToImmutableArray(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<string> CreateReference_ImmutableListString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.ToImmutableList(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableListClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<Record> CreateReference_ImmutableListClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.ToImmutableList(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableHashSetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<string> CreateReference_ImmutableHashSetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.ToImmutableHashSet(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableHashSetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<Record> CreateReference_ImmutableHashSetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.ToImmutableHashSet(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableSortedSetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<string> CreateReference_ImmutableSortedSetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.ToImmutableSortedSet(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableSortedSetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<Record> CreateReference_ImmutableSortedSetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.ToImmutableSortedSet(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableQueueString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<string> CreateReference_ImmutableQueueString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return ImmutableQueue.CreateRange(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableQueueClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<Record> CreateReference_ImmutableQueueClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return ImmutableQueue.CreateRange(records); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableStackString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<string> CreateReference_ImmutableStackString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return ImmutableStack.CreateRange(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableStackClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<Record> CreateReference_ImmutableStackClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return ImmutableStack.CreateRange(records); }
    /// <summary>Creates the benchmark input for <c>Reference_IEnumerableString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<string> CreateReference_IEnumerableString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_IEnumerableClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<Record> CreateReference_IEnumerableClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_ICollectionString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<string> CreateReference_ICollectionString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_ICollectionClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<Record> CreateReference_ICollectionClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_IListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<string> CreateReference_IListString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_IListClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<Record> CreateReference_IListClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<string> CreateReference_IReadOnlyListString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyListClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<Record> CreateReference_IReadOnlyListClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyCollectionString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<string> CreateReference_IReadOnlyCollectionString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyCollectionClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<Record> CreateReference_IReadOnlyCollectionClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new List<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_ISetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<string> CreateReference_ISetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_ISetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<Record> CreateReference_ISetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlySetString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<string> CreateReference_IReadOnlySetString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<string>(texts); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlySetClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<Record> CreateReference_IReadOnlySetClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new HashSet<Record>(records); }
    /// <summary>Creates the benchmark input for <c>Reference_DictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, string> CreateReference_DictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, string>(texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_DictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, Record> CreateReference_DictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, Record>(records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedDictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<string, string> CreateReference_SortedDictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedDictionary<string, string>(texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedDictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<string, Record> CreateReference_SortedDictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedDictionary<string, Record>(records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedListString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<string, string> CreateReference_SortedListString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedList<string, string>(texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_SortedListClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<string, Record> CreateReference_SortedListClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new SortedList<string, Record>(records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableDictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<string, string> CreateReference_ImmutableDictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value).ToImmutableDictionary(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableDictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<string, Record> CreateReference_ImmutableDictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value).ToImmutableDictionary(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableSortedDictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<string, string> CreateReference_ImmutableSortedDictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value).ToImmutableSortedDictionary(); }
    /// <summary>Creates the benchmark input for <c>Reference_ImmutableSortedDictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<string, Record> CreateReference_ImmutableSortedDictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value).ToImmutableSortedDictionary(); }
    /// <summary>Creates the benchmark input for <c>Reference_IDictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<string, string> CreateReference_IDictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, string>(texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_IDictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<string, Record> CreateReference_IDictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, Record>(records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyDictionaryString</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<string, string> CreateReference_IReadOnlyDictionaryString(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, string>(texts.Select((v, i) => new KeyValuePair<string, string>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>Reference_IReadOnlyDictionaryClass</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<string, Record> CreateReference_IReadOnlyDictionaryClass(int offset) { var texts = Enumerable.Range(0, 256).Select(i => "value-" + i).ToArray(); var records = Enumerable.Range(0, 256).Select(i => new Record { Id = i, Name = "record-" + i, Values = new List<int> { i, i + 1, i + 2 } }).ToArray(); return new Dictionary<string, Record>(records.Select((v, i) => new KeyValuePair<string, Record>("key" + i, v)).ToDictionary(p => p.Key, p => p.Value)); }
    /// <summary>Creates the benchmark input for <c>SimpleBenchmark</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SimpleClassBase CreateSimpleBenchmark(int offset) => SimpleClass.Create();
    /// <summary>Creates the benchmark input for <c>SerializeBenchmark</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static CharacterSaveData CreateSerializeBenchmark(int offset) => CharacterSaveData.Create();
    /// <summary>Creates the benchmark input for <c>Matrix3x2</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Matrix3x2 CreateMatrix3x2(int offset) => new Matrix3x2(1, 2, 3, 4, 5, 6);
    /// <summary>Creates the benchmark input for <c>ReadOnlyCollectionBuilderInt32</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollectionBuilder<int> CreateReadOnlyCollectionBuilderInt32(int offset) => new ReadOnlyCollectionBuilder<int>(Enumerable.Range(0, 1000));
    /// <summary>Creates the benchmark input for <c>JsonBenchmark</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static CharacterSaveData CreateJsonBenchmark(int offset) => CharacterSaveData.Create();
    /// <summary>Creates the benchmark input for <c>PolymorphismBenchmark</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PolymorphismClassBase CreatePolymorphismBenchmark(int offset) => CreatePolymorphicFixture();
}
