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
    /// <summary>Builds the collection fixtures shared by the size and changing-input scenes.</summary>
    /// <returns>The ion cases.</returns>
    internal static Fixture[] CollectionCases() =>
    [
        Fixture.Create<int[]>("Array_Value", CreateArray_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int[] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int[] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<string[]>("Array_String", CreateArray_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref string[] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref string[] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Element[]>("Array_Object", CreateArray_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Element[] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Element[] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<int>>("List_Value", CreateList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<string>>("List_String", CreateList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<List<Element>>("List_Object", CreateList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref List<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref List<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<LinkedList<int>>("LinkedList_Value", CreateLinkedList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref LinkedList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref LinkedList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<LinkedList<string>>("LinkedList_String", CreateLinkedList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref LinkedList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref LinkedList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<LinkedList<Element>>("LinkedList_Object", CreateLinkedList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref LinkedList<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref LinkedList<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Queue<int>>("Queue_Value", CreateQueue_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Queue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Queue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Queue<string>>("Queue_String", CreateQueue_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Queue<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Queue<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Queue<Element>>("Queue_Object", CreateQueue_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Queue<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Queue<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Stack<int>>("Stack_Value", CreateStack_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Stack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Stack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Stack<string>>("Stack_String", CreateStack_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Stack<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Stack<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Stack<Element>>("Stack_Object", CreateStack_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Stack<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Stack<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<HashSet<int>>("HashSet_Value", CreateHashSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<HashSet<string>>("HashSet_String", CreateHashSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<HashSet<Element>>("HashSet_Object", CreateHashSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref HashSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref HashSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<SortedSet<int>>("SortedSet_Value", CreateSortedSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<SortedSet<string>>("SortedSet_String", CreateSortedSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<SortedSet<Element>>("SortedSet_Object", CreateSortedSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ConcurrentBag<int>>("ConcurrentBag_Value", CreateConcurrentBag_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentBag<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentBag<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ConcurrentBag<string>>("ConcurrentBag_String", CreateConcurrentBag_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentBag<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentBag<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ConcurrentBag<Element>>("ConcurrentBag_Object", CreateConcurrentBag_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentBag<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentBag<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ConcurrentQueue<int>>("ConcurrentQueue_Value", CreateConcurrentQueue_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentQueue<string>>("ConcurrentQueue_String", CreateConcurrentQueue_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentQueue<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentQueue<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentQueue<Element>>("ConcurrentQueue_Object", CreateConcurrentQueue_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentQueue<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentQueue<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentStack<int>>("ConcurrentStack_Value", CreateConcurrentStack_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentStack<string>>("ConcurrentStack_String", CreateConcurrentStack_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentStack<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentStack<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ConcurrentStack<Element>>("ConcurrentStack_Object", CreateConcurrentStack_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentStack<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentStack<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BlockingCollection<int>>("BlockingCollection_Value", CreateBlockingCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BlockingCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BlockingCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BlockingCollection<string>>("BlockingCollection_String", CreateBlockingCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BlockingCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BlockingCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<BlockingCollection<Element>>("BlockingCollection_Object", CreateBlockingCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref BlockingCollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref BlockingCollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<int>>("ImmutableArray_Value", CreateImmutableArray_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<string>>("ImmutableArray_String", CreateImmutableArray_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableArray<Element>>("ImmutableArray_Object", CreateImmutableArray_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableArray<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableArray<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<int>>("ImmutableList_Value", CreateImmutableList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<string>>("ImmutableList_String", CreateImmutableList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableList<Element>>("ImmutableList_Object", CreateImmutableList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableList<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableList<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableHashSet<int>>("ImmutableHashSet_Value", CreateImmutableHashSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableHashSet<string>>("ImmutableHashSet_String", CreateImmutableHashSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableHashSet<Element>>("ImmutableHashSet_Object", CreateImmutableHashSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableHashSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableHashSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedSet<int>>("ImmutableSortedSet_Value", CreateImmutableSortedSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedSet<string>>("ImmutableSortedSet_String", CreateImmutableSortedSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedSet<Element>>("ImmutableSortedSet_Object", CreateImmutableSortedSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ImmutableQueue<int>>("ImmutableQueue_Value", CreateImmutableQueue_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableQueue<string>>("ImmutableQueue_String", CreateImmutableQueue_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableQueue<Element>>("ImmutableQueue_Object", CreateImmutableQueue_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableQueue<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableQueue<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<int>>("ImmutableStack_Value", CreateImmutableStack_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<string>>("ImmutableStack_String", CreateImmutableStack_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ImmutableStack<Element>>("ImmutableStack_Object", CreateImmutableStack_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableStack<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableStack<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IEnumerable<int>>("IEnumerable_Value", CreateIEnumerable_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IEnumerable<string>>("IEnumerable_String", CreateIEnumerable_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IEnumerable<Element>>("IEnumerable_Object", CreateIEnumerable_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IEnumerable<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IEnumerable<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<int>>("ICollection_Value", CreateICollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<string>>("ICollection_String", CreateICollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ICollection<Element>>("ICollection_Object", CreateICollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ICollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ICollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<int>>("IList_Value", CreateIList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<string>>("IList_String", CreateIList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IList<Element>>("IList_Object", CreateIList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IList<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IList<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<int>>("IReadOnlyCollection_Value", CreateIReadOnlyCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<string>>("IReadOnlyCollection_String", CreateIReadOnlyCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyCollection<Element>>("IReadOnlyCollection_Object", CreateIReadOnlyCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyCollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyCollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<int>>("IReadOnlyList_Value", CreateIReadOnlyList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<string>>("IReadOnlyList_String", CreateIReadOnlyList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IReadOnlyList<Element>>("IReadOnlyList_Object", CreateIReadOnlyList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyList<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyList<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ISet<int>>("ISet_Value", CreateISet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ISet<string>>("ISet_String", CreateISet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<ISet<Element>>("ISet_Object", CreateISet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ISet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ISet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IReadOnlySet<int>>("IReadOnlySet_Value", CreateIReadOnlySet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IReadOnlySet<string>>("IReadOnlySet_String", CreateIReadOnlySet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IReadOnlySet<Element>>("IReadOnlySet_Object", CreateIReadOnlySet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlySet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlySet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IImmutableList<int>>("IImmutableList_Value", CreateIImmutableList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableList<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableList<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableList<string>>("IImmutableList_String", CreateIImmutableList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableList<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableList<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableList<Element>>("IImmutableList_Object", CreateIImmutableList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableList<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableList<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableSet<int>>("IImmutableSet_Value", CreateIImmutableSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IImmutableSet<string>>("IImmutableSet_String", CreateIImmutableSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IImmutableSet<Element>>("IImmutableSet_Object", CreateIImmutableSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<IImmutableQueue<int>>("IImmutableQueue_Value", CreateIImmutableQueue_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableQueue<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableQueue<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableQueue<string>>("IImmutableQueue_String", CreateIImmutableQueue_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableQueue<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableQueue<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableQueue<Element>>("IImmutableQueue_Object", CreateIImmutableQueue_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableQueue<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableQueue<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableStack<int>>("IImmutableStack_Value", CreateIImmutableStack_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableStack<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableStack<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableStack<string>>("IImmutableStack_String", CreateIImmutableStack_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableStack<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableStack<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<IImmutableStack<Element>>("IImmutableStack_Object", CreateIImmutableStack_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableStack<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableStack<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollection<int>>("ReadOnlyCollection_Value", CreateReadOnlyCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollection<string>>("ReadOnlyCollection_String", CreateReadOnlyCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollection<Element>>("ReadOnlyCollection_Object", CreateReadOnlyCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Collection<int>>("Collection_Value", CreateCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Collection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Collection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Collection<string>>("Collection_String", CreateCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Collection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Collection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Collection<Element>>("Collection_Object", CreateCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Collection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Collection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ObservableCollection<int>>("ObservableCollection_Value", CreateObservableCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ObservableCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ObservableCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ObservableCollection<string>>("ObservableCollection_String", CreateObservableCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ObservableCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ObservableCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ObservableCollection<Element>>("ObservableCollection_Object", CreateObservableCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ObservableCollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ObservableCollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyObservableCollection<int>>("ReadOnlyObservableCollection_Value", CreateReadOnlyObservableCollection_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyObservableCollection<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyObservableCollection<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyObservableCollection<string>>("ReadOnlyObservableCollection_String", CreateReadOnlyObservableCollection_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyObservableCollection<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyObservableCollection<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyObservableCollection<Element>>("ReadOnlyObservableCollection_Object", CreateReadOnlyObservableCollection_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyObservableCollection<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyObservableCollection<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollectionBuilder<int>>("ReadOnlyCollectionBuilder_Value", CreateReadOnlyCollectionBuilder_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollectionBuilder<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollectionBuilder<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollectionBuilder<string>>("ReadOnlyCollectionBuilder_String", CreateReadOnlyCollectionBuilder_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollectionBuilder<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollectionBuilder<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlyCollectionBuilder<Element>>("ReadOnlyCollectionBuilder_Object", CreateReadOnlyCollectionBuilder_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyCollectionBuilder<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyCollectionBuilder<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<FrozenSet<int>>("FrozenSet_Value", CreateFrozenSet_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenSet<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenSet<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<FrozenSet<string>>("FrozenSet_String", CreateFrozenSet_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenSet<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenSet<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<FrozenSet<Element>>("FrozenSet_Object", CreateFrozenSet_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenSet<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenSet<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.Select(v=>Canonical.Of(v)).Order(StringComparer.Ordinal))),
        Fixture.Create<Memory<int>>("Memory_Value", CreateMemory_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Memory<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Memory<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<Memory<string>>("Memory_String", CreateMemory_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Memory<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Memory<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<Memory<Element>>("Memory_Object", CreateMemory_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Memory<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Memory<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlyMemory<int>>("ReadOnlyMemory_Value", CreateReadOnlyMemory_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyMemory<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyMemory<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlyMemory<string>>("ReadOnlyMemory_String", CreateReadOnlyMemory_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyMemory<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyMemory<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlyMemory<Element>>("ReadOnlyMemory_Object", CreateReadOnlyMemory_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyMemory<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyMemory<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ArraySegment<int>>("ArraySegment_Value", CreateArraySegment_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ArraySegment<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ArraySegment<int> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ArraySegment<string>>("ArraySegment_String", CreateArraySegment_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ArraySegment<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ArraySegment<string> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ArraySegment<Element>>("ArraySegment_Object", CreateArraySegment_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ArraySegment<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ArraySegment<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<ReadOnlySequence<int>>("ReadOnlySequence_Value", CreateReadOnlySequence_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlySequence<int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlySequence<int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlySequence<string>>("ReadOnlySequence_String", CreateReadOnlySequence_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlySequence<string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlySequence<string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<ReadOnlySequence<Element>>("ReadOnlySequence_Object", CreateReadOnlySequence_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlySequence<Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlySequence<Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.ToArray())),
        Fixture.Create<int[,]>("Array2D_Value", CreateArray2D_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref int[,] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref int[,] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<string[,]>("Array2D_String", CreateArray2D_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref string[,] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref string[,] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Element[,]>("Array2D_Object", CreateArray2D_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Element[,] value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Element[,] value) => LuminPackSerializer.DeserializeJson(data, ref value)),
        Fixture.Create<Dictionary<string,int>>("Dictionary_Value", CreateDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<Dictionary<string,string>>("Dictionary_String", CreateDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<Dictionary<string,Element>>("Dictionary_Object", CreateDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref Dictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref Dictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedDictionary<string,int>>("SortedDictionary_Value", CreateSortedDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedDictionary<string,string>>("SortedDictionary_String", CreateSortedDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedDictionary<string,Element>>("SortedDictionary_Object", CreateSortedDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedList<string,int>>("SortedList_Value", CreateSortedList_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedList<string,string>>("SortedList_String", CreateSortedList_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<SortedList<string,Element>>("SortedList_Object", CreateSortedList_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref SortedList<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref SortedList<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ConcurrentDictionary<string,int>>("ConcurrentDictionary_Value", CreateConcurrentDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ConcurrentDictionary<string,string>>("ConcurrentDictionary_String", CreateConcurrentDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ConcurrentDictionary<string,Element>>("ConcurrentDictionary_Object", CreateConcurrentDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ConcurrentDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ConcurrentDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableDictionary<string,int>>("ImmutableDictionary_Value", CreateImmutableDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableDictionary<string,string>>("ImmutableDictionary_String", CreateImmutableDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableDictionary<string,Element>>("ImmutableDictionary_Object", CreateImmutableDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedDictionary<string,int>>("ImmutableSortedDictionary_Value", CreateImmutableSortedDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedDictionary<string,string>>("ImmutableSortedDictionary_String", CreateImmutableSortedDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ImmutableSortedDictionary<string,Element>>("ImmutableSortedDictionary_Object", CreateImmutableSortedDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ImmutableSortedDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ImmutableSortedDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IDictionary<string,int>>("IDictionary_Value", CreateIDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IDictionary<string,string>>("IDictionary_String", CreateIDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IDictionary<string,Element>>("IDictionary_Object", CreateIDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IReadOnlyDictionary<string,int>>("IReadOnlyDictionary_Value", CreateIReadOnlyDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IReadOnlyDictionary<string,string>>("IReadOnlyDictionary_String", CreateIReadOnlyDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IReadOnlyDictionary<string,Element>>("IReadOnlyDictionary_Object", CreateIReadOnlyDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IReadOnlyDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IReadOnlyDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IImmutableDictionary<string,int>>("IImmutableDictionary_Value", CreateIImmutableDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IImmutableDictionary<string,string>>("IImmutableDictionary_String", CreateIImmutableDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<IImmutableDictionary<string,Element>>("IImmutableDictionary_Object", CreateIImmutableDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref IImmutableDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref IImmutableDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ReadOnlyDictionary<string,int>>("ReadOnlyDictionary_Value", CreateReadOnlyDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ReadOnlyDictionary<string,string>>("ReadOnlyDictionary_String", CreateReadOnlyDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<ReadOnlyDictionary<string,Element>>("ReadOnlyDictionary_Object", CreateReadOnlyDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref ReadOnlyDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref ReadOnlyDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<FrozenDictionary<string,int>>("FrozenDictionary_Value", CreateFrozenDictionary_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenDictionary<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenDictionary<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<FrozenDictionary<string,string>>("FrozenDictionary_String", CreateFrozenDictionary_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenDictionary<string,string> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenDictionary<string,string> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<FrozenDictionary<string,Element>>("FrozenDictionary_Object", CreateFrozenDictionary_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref FrozenDictionary<string,Element> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref FrozenDictionary<string,Element> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.OrderBy(p=>p.Key,StringComparer.Ordinal))),
        Fixture.Create<PriorityQueue<int,int>>("PriorityQueue_Value", CreatePriorityQueue_Value,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<int,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<int,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.UnorderedItems.OrderBy(p=>p.Priority).Select(p=>p.Element))),
        Fixture.Create<PriorityQueue<string,int>>("PriorityQueue_String", CreatePriorityQueue_String,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<string,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<string,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.UnorderedItems.OrderBy(p=>p.Priority).Select(p=>p.Element))),
        Fixture.Create<PriorityQueue<Element,int>>("PriorityQueue_Object", CreatePriorityQueue_Object,
            static value => LuminPackSerializer.Serialize(value),
            static (byte[] data, ref PriorityQueue<Element,int> value) => LuminPackSerializer.Deserialize(data, ref value),
            static value => LuminPackSerializer.SerializeJson(value),
            static (string data, ref PriorityQueue<Element,int> value) => LuminPackSerializer.DeserializeJson(data, ref value),
            static value => Canonical.Of(value.UnorderedItems.OrderBy(p=>p.Priority).Select(p=>p.Element))),
    ];

    /// <summary>Creates the benchmark input for <c>Array_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int[] CreateArray_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values;
    }

    /// <summary>Creates the benchmark input for <c>Array_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static string[] CreateArray_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values;
    }

    /// <summary>Creates the benchmark input for <c>Array_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Element[] CreateArray_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values;
    }

    /// <summary>Creates the benchmark input for <c>List_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<int> CreateList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>List_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<string> CreateList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>List_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static List<Element> CreateList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>LinkedList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static LinkedList<int> CreateLinkedList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>LinkedList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static LinkedList<string> CreateLinkedList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>LinkedList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static LinkedList<Element> CreateLinkedList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Queue_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Queue<int> CreateQueue_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Queue_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Queue<string> CreateQueue_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Queue_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Queue<Element> CreateQueue_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Stack_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Stack<int> CreateStack_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Stack_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Stack<string> CreateStack_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Stack_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Stack<Element> CreateStack_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>HashSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<int> CreateHashSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>HashSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<string> CreateHashSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>HashSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static HashSet<Element> CreateHashSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>SortedSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<int> CreateSortedSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>SortedSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<string> CreateSortedSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>SortedSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedSet<Element> CreateSortedSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentBag_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentBag<int> CreateConcurrentBag_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentBag_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentBag<string> CreateConcurrentBag_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentBag_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentBag<Element> CreateConcurrentBag_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentQueue_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentQueue<int> CreateConcurrentQueue_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentQueue_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentQueue<string> CreateConcurrentQueue_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentQueue_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentQueue<Element> CreateConcurrentQueue_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentStack_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentStack<int> CreateConcurrentStack_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentStack_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentStack<string> CreateConcurrentStack_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentStack_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentStack<Element> CreateConcurrentStack_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>BlockingCollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BlockingCollection<int> CreateBlockingCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(new ConcurrentQueue<int>(values));
    }

    /// <summary>Creates the benchmark input for <c>BlockingCollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BlockingCollection<string> CreateBlockingCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(new ConcurrentQueue<string>(values));
    }

    /// <summary>Creates the benchmark input for <c>BlockingCollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static BlockingCollection<Element> CreateBlockingCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(new ConcurrentQueue<Element>(values));
    }

    /// <summary>Creates the benchmark input for <c>ImmutableArray_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<int> CreateImmutableArray_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableArray();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableArray_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<string> CreateImmutableArray_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableArray();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableArray_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableArray<Element> CreateImmutableArray_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableArray();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<int> CreateImmutableList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<string> CreateImmutableList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableList<Element> CreateImmutableList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableHashSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<int> CreateImmutableHashSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableHashSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<string> CreateImmutableHashSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableHashSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableHashSet<Element> CreateImmutableHashSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<int> CreateImmutableSortedSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableSortedSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<string> CreateImmutableSortedSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableSortedSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedSet<Element> CreateImmutableSortedSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableSortedSet();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableQueue_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<int> CreateImmutableQueue_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableQueue_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<string> CreateImmutableQueue_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableQueue_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableQueue<Element> CreateImmutableQueue_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableStack_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<int> CreateImmutableStack_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableStack_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<string> CreateImmutableStack_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableStack_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableStack<Element> CreateImmutableStack_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IEnumerable_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<int> CreateIEnumerable_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IEnumerable_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<string> CreateIEnumerable_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IEnumerable_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IEnumerable<Element> CreateIEnumerable_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>ICollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<int> CreateICollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>ICollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<string> CreateICollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>ICollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ICollection<Element> CreateICollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<int> CreateIList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<string> CreateIList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IList<Element> CreateIList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyCollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<int> CreateIReadOnlyCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyCollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<string> CreateIReadOnlyCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyCollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyCollection<Element> CreateIReadOnlyCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<int> CreateIReadOnlyList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<string> CreateIReadOnlyList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyList<Element> CreateIReadOnlyList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToList();
    }

    /// <summary>Creates the benchmark input for <c>ISet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<int> CreateISet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>ISet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<string> CreateISet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>ISet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ISet<Element> CreateISet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlySet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<int> CreateIReadOnlySet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlySet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<string> CreateIReadOnlySet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlySet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlySet<Element> CreateIReadOnlySet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableList<int> CreateIImmutableList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableList<string> CreateIImmutableList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableList<Element> CreateIImmutableList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableList();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableSet<int> CreateIImmutableSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableSet<string> CreateIImmutableSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableSet<Element> CreateIImmutableSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToImmutableHashSet();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableQueue_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableQueue<int> CreateIImmutableQueue_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableQueue_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableQueue<string> CreateIImmutableQueue_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableQueue_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableQueue<Element> CreateIImmutableQueue_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return ImmutableQueue.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableStack_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableStack<int> CreateIImmutableStack_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableStack_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableStack<string> CreateIImmutableStack_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableStack_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableStack<Element> CreateIImmutableStack_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return ImmutableStack.CreateRange(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollection<int> CreateReadOnlyCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollection<string> CreateReadOnlyCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollection<Element> CreateReadOnlyCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>Collection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Collection<int> CreateCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values.ToList());
    }

    /// <summary>Creates the benchmark input for <c>Collection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Collection<string> CreateCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values.ToList());
    }

    /// <summary>Creates the benchmark input for <c>Collection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Collection<Element> CreateCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values.ToList());
    }

    /// <summary>Creates the benchmark input for <c>ObservableCollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ObservableCollection<int> CreateObservableCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ObservableCollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ObservableCollection<string> CreateObservableCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ObservableCollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ObservableCollection<Element> CreateObservableCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyObservableCollection_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyObservableCollection<int> CreateReadOnlyObservableCollection_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(new ObservableCollection<int>(values));
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyObservableCollection_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyObservableCollection<string> CreateReadOnlyObservableCollection_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(new ObservableCollection<string>(values));
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyObservableCollection_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyObservableCollection<Element> CreateReadOnlyObservableCollection_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(new ObservableCollection<Element>(values));
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollectionBuilder_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollectionBuilder<int> CreateReadOnlyCollectionBuilder_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollectionBuilder_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollectionBuilder<string> CreateReadOnlyCollectionBuilder_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyCollectionBuilder_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyCollectionBuilder<Element> CreateReadOnlyCollectionBuilder_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values);
    }

    /// <summary>Creates the benchmark input for <c>FrozenSet_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenSet<int> CreateFrozenSet_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return values.ToFrozenSet();
    }

    /// <summary>Creates the benchmark input for <c>FrozenSet_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenSet<string> CreateFrozenSet_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return values.ToFrozenSet();
    }

    /// <summary>Creates the benchmark input for <c>FrozenSet_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenSet<Element> CreateFrozenSet_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return values.ToFrozenSet();
    }

    /// <summary>Creates the benchmark input for <c>Memory_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Memory<int> CreateMemory_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => i * 17 + offset).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>Memory_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Memory<string> CreateMemory_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>Memory_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Memory<Element> CreateMemory_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyMemory_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyMemory<int> CreateReadOnlyMemory_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => i * 17 + offset).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyMemory_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyMemory<string> CreateReadOnlyMemory_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyMemory_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyMemory<Element> CreateReadOnlyMemory_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ArraySegment_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ArraySegment<int> CreateArraySegment_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => i * 17 + offset).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ArraySegment_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ArraySegment<string> CreateArraySegment_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ArraySegment_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ArraySegment<Element> CreateArraySegment_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count + 2).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values, 1, Config.Count);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlySequence_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlySequence<int> CreateReadOnlySequence_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return MakeSequence(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlySequence_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlySequence<string> CreateReadOnlySequence_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return MakeSequence(values);
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlySequence_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlySequence<Element> CreateReadOnlySequence_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return MakeSequence(values);
    }

    /// <summary>Creates the benchmark input for <c>Array2D_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static int[,] CreateArray2D_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return MakeMatrix(values);
    }

    /// <summary>Creates the benchmark input for <c>Array2D_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static string[,] CreateArray2D_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return MakeMatrix(values);
    }

    /// <summary>Creates the benchmark input for <c>Array2D_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Element[,] CreateArray2D_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return MakeMatrix(values);
    }

    /// <summary>Creates the benchmark input for <c>Dictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, int> CreateDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>Dictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, string> CreateDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>Dictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static Dictionary<string, Element> CreateDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<string, int> CreateSortedDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<string, string> CreateSortedDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedDictionary<string, Element> CreateSortedDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedList_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<string, int> CreateSortedList_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedList_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<string, string> CreateSortedList_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>SortedList_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static SortedList<string, Element> CreateSortedList_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentDictionary<string, int> CreateConcurrentDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentDictionary<string, string> CreateConcurrentDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>ConcurrentDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ConcurrentDictionary<string, Element> CreateConcurrentDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(pairs);
    }

    /// <summary>Creates the benchmark input for <c>ImmutableDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<string, int> CreateImmutableDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<string, string> CreateImmutableDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableDictionary<string, Element> CreateImmutableDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<string, int> CreateImmutableSortedDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableSortedDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<string, string> CreateImmutableSortedDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableSortedDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ImmutableSortedDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ImmutableSortedDictionary<string, Element> CreateImmutableSortedDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableSortedDictionary();
    }

    /// <summary>Creates the benchmark input for <c>IDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<string, int> CreateIDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, int>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<string, string> CreateIDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, string>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IDictionary<string, Element> CreateIDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, Element>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<string, int> CreateIReadOnlyDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, int>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<string, string> CreateIReadOnlyDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, string>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IReadOnlyDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IReadOnlyDictionary<string, Element> CreateIReadOnlyDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new Dictionary<string, Element>(pairs);
    }

    /// <summary>Creates the benchmark input for <c>IImmutableDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableDictionary<string, int> CreateIImmutableDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableDictionary<string, string> CreateIImmutableDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>IImmutableDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static IImmutableDictionary<string, Element> CreateIImmutableDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToImmutableDictionary();
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyDictionary<string, int> CreateReadOnlyDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(new Dictionary<string, int>(pairs));
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyDictionary<string, string> CreateReadOnlyDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(new Dictionary<string, string>(pairs));
    }

    /// <summary>Creates the benchmark input for <c>ReadOnlyDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static ReadOnlyDictionary<string, Element> CreateReadOnlyDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return new(new Dictionary<string, Element>(pairs));
    }

    /// <summary>Creates the benchmark input for <c>FrozenDictionary_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenDictionary<string, int> CreateFrozenDictionary_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, int>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToFrozenDictionary();
    }

    /// <summary>Creates the benchmark input for <c>FrozenDictionary_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenDictionary<string, string> CreateFrozenDictionary_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, string>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToFrozenDictionary();
    }

    /// <summary>Creates the benchmark input for <c>FrozenDictionary_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static FrozenDictionary<string, Element> CreateFrozenDictionary_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        var pairs = values.Select((v, i) => new KeyValuePair<string, Element>("key-" + i.ToString("D6"), v)).ToDictionary(p => p.Key, p => p.Value);
        return pairs.ToFrozenDictionary();
    }

    /// <summary>Creates the benchmark input for <c>PriorityQueue_Value</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<int, int> CreatePriorityQueue_Value(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => i * 17 + offset).ToArray();
        return new(values.Select((v, i) => (v, i)));
    }

    /// <summary>Creates the benchmark input for <c>PriorityQueue_String</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<string, int> CreatePriorityQueue_String(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => "value-" + (i + offset).ToString("D6") + (i % 5 == 0 ? "-\u4e2d\u6587-\ud83d\ude03" : "-sample")).ToArray();
        return new(values.Select((v, i) => (v, i)));
    }

    /// <summary>Creates the benchmark input for <c>PriorityQueue_Object</c>.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>A deterministic value for this fixture.</returns>
    private static PriorityQueue<Element, int> CreatePriorityQueue_Object(int offset)
    {
        var values = Enumerable.Range(0, Config.Count).Select(i => new Element { Id = i + offset, Name = "record-" + (i + offset) + (i % 5 == 0 ? "-\u4e2d\u6587" : ""), Values = new() { i + offset, i + 1 + offset, i + 2 + offset } }).ToArray();
        return new(values.Select((v, i) => (v, i)));
    }

}
