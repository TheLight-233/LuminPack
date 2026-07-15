using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using LuminPack;

namespace LuminPackUnitTest
{
    public class CollectionTypesSerializationTest
    {
        // ConcurrentDictionary Tests
        public static void TestConcurrentDictionary(List<string> results)
        {
            try
            {
                var val = new ConcurrentDictionary<int, string>
                {
                    [1] = "One",
                    [2] = "Two",
                    [3] = "Three"
                };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ConcurrentDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.TryGetValue(kv.Key, out var v) && v == kv.Value))
                    results.Add("✓ TestConcurrentDictionary - PASSED");
                else
                    results.Add("✗ TestConcurrentDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestConcurrentDictionary - ERROR: {ex.Message}");
            }
        }

        // SortedDictionary Tests
        public static void TestSortedDictionary(List<string> results)
        {
            try
            {
                var val = new SortedDictionary<int, string>
                {
                    { 3, "Three" },
                    { 1, "One" },
                    { 2, "Two" }
                };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<SortedDictionary<int, string>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestSortedDictionary - PASSED");
                else
                    results.Add("✗ TestSortedDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSortedDictionary - ERROR: {ex.Message}");
            }
        }

        // Stack Tests
        public static void TestStack(List<string> results)
        {
            try
            {
                var val = new Stack<int>(new[] { 1, 2, 3, 4, 5 });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<Stack<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestStack - PASSED");
                else
                    results.Add("✗ TestStack - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStack - ERROR: {ex.Message}");
            }
        }

        // Queue Tests
        public static void TestQueue(List<string> results)
        {
            try
            {
                var val = new Queue<int>(new[] { 1, 2, 3, 4, 5 });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<Queue<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestQueue - PASSED");
                else
                    results.Add("✗ TestQueue - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestQueue - ERROR: {ex.Message}");
            }
        }

        // LinkedList Tests
        public static void TestLinkedList(List<string> results)
        {
            try
            {
                var val = new LinkedList<int>(new[] { 1, 2, 3, 4, 5 });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<LinkedList<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestLinkedList - PASSED");
                else
                    results.Add("✗ TestLinkedList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestLinkedList - ERROR: {ex.Message}");
            }
        }

        // HashSet Tests
        public static void TestHashSet(List<string> results)
        {
            try
            {
                var val = new HashSet<int> { 1, 2, 3, 4, 5, 3, 2 }; // 重复项应被忽略
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<HashSet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestHashSet - PASSED");
                else
                    results.Add("✗ TestHashSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestHashSet - ERROR: {ex.Message}");
            }
        }

        // SortedSet Tests
        public static void TestSortedSet(List<string> results)
        {
            try
            {
                var val = new SortedSet<int> { 5, 1, 3, 2, 4 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<SortedSet<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestSortedSet - PASSED");
                else
                    results.Add("✗ TestSortedSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSortedSet - ERROR: {ex.Message}");
            }
        }

        // SortedList Tests
        public static void TestSortedList(List<string> results)
        {
            try
            {
                var val = new SortedList<int, string>
                {
                    { 3, "Three" },
                    { 1, "One" },
                    { 2, "Two" }
                };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<SortedList<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.ContainsKey(kv.Key) && result[kv.Key] == kv.Value))
                    results.Add("✓ TestSortedList - PASSED");
                else
                    results.Add("✗ TestSortedList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSortedList - ERROR: {ex.Message}");
            }
        }

        // BlockingCollection Tests
        public static void TestBlockingCollection(List<string> results)
        {
            try
            {
                var val = new BlockingCollection<int>();
                val.Add(1);
                val.Add(2);
                val.Add(3);
                val.CompleteAdding();
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<BlockingCollection<int>>(buf);
                
                var valArray = val.ToArray();
                var resultArray = result.ToArray();
                
                if (valArray.SequenceEqual(resultArray))
                    results.Add("✓ TestBlockingCollection - PASSED");
                else
                    results.Add("✗ TestBlockingCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBlockingCollection - ERROR: {ex.Message}");
            }
        }

        // ConcurrentBag Tests
        public static void TestConcurrentBag(List<string> results)
        {
            try
            {
                var val = new ConcurrentBag<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ConcurrentBag<int>>(buf);
                
                // ConcurrentBag 没有保证顺序，所以比较集合内容而不考虑顺序
                var valSorted = val.OrderBy(x => x).ToArray();
                var resultSorted = result.OrderBy(x => x).ToArray();
                
                if (valSorted.SequenceEqual(resultSorted))
                    results.Add("✓ TestConcurrentBag - PASSED");
                else
                    results.Add("✗ TestConcurrentBag - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestConcurrentBag - ERROR: {ex.Message}");
            }
        }

        // ConcurrentQueue Tests
        public static void TestConcurrentQueue(List<string> results)
        {
            try
            {
                var val = new ConcurrentQueue<int>(new[] { 1, 2, 3, 4, 5 });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ConcurrentQueue<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestConcurrentQueue - PASSED");
                else
                    results.Add("✗ TestConcurrentQueue - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestConcurrentQueue - ERROR: {ex.Message}");
            }
        }

        // ConcurrentStack Tests
        public static void TestConcurrentStack(List<string> results)
        {
            try
            {
                var val = new ConcurrentStack<int>(new[] { 1, 2, 3, 4, 5 });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ConcurrentStack<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestConcurrentStack - PASSED");
                else
                    results.Add("✗ TestConcurrentStack - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestConcurrentStack - ERROR: {ex.Message}");
            }
        }

        // Collection Tests
        public static void TestCollection(List<string> results)
        {
            try
            {
                var val = new Collection<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<Collection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestCollection - PASSED");
                else
                    results.Add("✗ TestCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestCollection - ERROR: {ex.Message}");
            }
        }

        // ObservableCollection Tests
        public static void TestObservableCollection(List<string> results)
        {
            try
            {
                var val = new ObservableCollection<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ObservableCollection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestObservableCollection - PASSED");
                else
                    results.Add("✗ TestObservableCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestObservableCollection - ERROR: {ex.Message}");
            }
        }

        // ReadOnlyCollection Tests
        public static void TestReadOnlyCollection(List<string> results)
        {
            try
            {
                var innerList = new List<int> { 1, 2, 3, 4, 5 };
                var val = new ReadOnlyCollection<int>(innerList);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ReadOnlyCollection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestReadOnlyCollection - PASSED");
                else
                    results.Add("✗ TestReadOnlyCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestReadOnlyCollection - ERROR: {ex.Message}");
            }
        }

        // ReadOnlyObservableCollection Tests
        public static void TestReadOnlyObservableCollection(List<string> results)
        {
            try
            {
                var innerCollection = new ObservableCollection<int> { 1, 2, 3, 4, 5 };
                var val = new ReadOnlyObservableCollection<int>(innerCollection);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ReadOnlyObservableCollection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestReadOnlyObservableCollection - PASSED");
                else
                    results.Add("✗ TestReadOnlyObservableCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestReadOnlyObservableCollection - ERROR: {ex.Message}");
            }
        }

        // PriorityQueue Tests
        public static void TestPriorityQueue(List<string> results)
        {
            try
            {
                var val = new PriorityQueue<string, int>();
                val.Enqueue("One", 3);
                val.Enqueue("Two", 1);
                val.Enqueue("Three", 2);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<PriorityQueue<string, int>>(buf);
                
                bool equal = true;
                while (val.Count > 0 && result.Count > 0)
                {
                    val.TryDequeue(out var valItem, out var valPriority);
                    result.TryDequeue(out var resultItem, out var resultPriority);
                    
                    if (valItem != resultItem || valPriority != resultPriority)
                    {
                        equal = false;
                        break;
                    }
                }
                
                if (equal && val.Count == 0 && result.Count == 0)
                    results.Add("✓ TestPriorityQueue - PASSED");
                else
                    results.Add("✗ TestPriorityQueue - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestPriorityQueue - ERROR: {ex.Message}");
            }
        }

        // ImmutableArray Tests
        public static void TestImmutableArray(List<string> results)
        {
            try
            {
                var val = ImmutableArray.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableArray<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableArray - PASSED");
                else
                    results.Add("✗ TestImmutableArray - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableArray - ERROR: {ex.Message}");
            }
        }

        // ImmutableList Tests
        public static void TestImmutableList(List<string> results)
        {
            try
            {
                var val = ImmutableList.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableList<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableList - PASSED");
                else
                    results.Add("✗ TestImmutableList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableList - ERROR: {ex.Message}");
            }
        }

        // ImmutableQueue Tests
        public static void TestImmutableQueue(List<string> results)
        {
            try
            {
                var val = ImmutableQueue.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableQueue<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableQueue - PASSED");
                else
                    results.Add("✗ TestImmutableQueue - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableQueue - ERROR: {ex.Message}");
            }
        }

        // ImmutableStack Tests
        public static void TestImmutableStack(List<string> results)
        {
            try
            {
                var val = ImmutableStack.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableStack<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableStack - PASSED");
                else
                    results.Add("✗ TestImmutableStack - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableStack - ERROR: {ex.Message}");
            }
        }

        // ImmutableDictionary Tests
        public static void TestImmutableDictionary(List<string> results)
        {
            try
            {
                var val = ImmutableDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create(1, "One"),
                    KeyValuePair.Create(2, "Two"),
                    KeyValuePair.Create(3, "Three")
                });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.TryGetValue(kv.Key, out var v) && v == kv.Value))
                    results.Add("✓ TestImmutableDictionary - PASSED");
                else
                    results.Add("✗ TestImmutableDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableDictionary - ERROR: {ex.Message}");
            }
        }

        // ImmutableHashSet Tests
        public static void TestImmutableHashSet(List<string> results)
        {
            try
            {
                var val = ImmutableHashSet.Create(1, 2, 3, 4, 5, 3, 2); // 重复项应被忽略
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableHashSet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestImmutableHashSet - PASSED");
                else
                    results.Add("✗ TestImmutableHashSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableHashSet - ERROR: {ex.Message}");
            }
        }

        // ImmutableSortedDictionary Tests
        public static void TestImmutableSortedDictionary(List<string> results)
        {
            try
            {
                var val = ImmutableSortedDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create(3, "Three"),
                    KeyValuePair.Create(1, "One"),
                    KeyValuePair.Create(2, "Two")
                });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableSortedDictionary<int, string>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableSortedDictionary - PASSED");
                else
                    results.Add("✗ TestImmutableSortedDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableSortedDictionary - ERROR: {ex.Message}");
            }
        }

        // ImmutableSortedSet Tests
        public static void TestImmutableSortedSet(List<string> results)
        {
            try
            {
                var val = ImmutableSortedSet.Create(5, 1, 3, 2, 4);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ImmutableSortedSet<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestImmutableSortedSet - PASSED");
                else
                    results.Add("✗ TestImmutableSortedSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestImmutableSortedSet - ERROR: {ex.Message}");
            }
        }

#if NET8_0_OR_GREATER
        // FrozenDictionary Tests (.NET 8+)
        public static void TestFrozenDictionary(List<string> results)
        {
            try
            {
                var val = new Dictionary<int, string>
                {
                    [1] = "One",
                    [2] = "Two",
                    [3] = "Three"
                }.ToFrozenDictionary();
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<FrozenDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.TryGetValue(kv.Key, out var v) && v == kv.Value))
                    results.Add("✓ TestFrozenDictionary - PASSED");
                else
                    results.Add("✗ TestFrozenDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestFrozenDictionary - ERROR: {ex.Message}");
            }
        }

        // FrozenSet Tests (.NET 8+)
        public static void TestFrozenSet(List<string> results)
        {
            try
            {
                var val = new HashSet<int> { 1, 2, 3, 4, 5 }.ToFrozenSet();
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<FrozenSet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestFrozenSet - PASSED");
                else
                    results.Add("✗ TestFrozenSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestFrozenSet - ERROR: {ex.Message}");
            }
        }
#endif

        // 接口类型测试
        // 使用具体实现进行测试

        // IEnumerable Tests
        public static void TestIEnumerable(List<string> results)
        {
            try
            {
                IEnumerable<int> val = new List<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IEnumerable<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIEnumerable - PASSED");
                else
                    results.Add("✗ TestIEnumerable - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIEnumerable - ERROR: {ex.Message}");
            }
        }

        // ICollection Tests
        public static void TestICollection(List<string> results)
        {
            try
            {
                ICollection<int> val = new List<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ICollection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestICollection - PASSED");
                else
                    results.Add("✗ TestICollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestICollection - ERROR: {ex.Message}");
            }
        }

        // IReadOnlyCollection Tests
        public static void TestIReadOnlyCollection(List<string> results)
        {
            try
            {
                IReadOnlyCollection<int> val = new List<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IReadOnlyCollection<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIReadOnlyCollection - PASSED");
                else
                    results.Add("✗ TestIReadOnlyCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIReadOnlyCollection - ERROR: {ex.Message}");
            }
        }

        // IList Tests
        public static void TestIList(List<string> results)
        {
            try
            {
                IList<int> val = new List<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IList<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIList - PASSED");
                else
                    results.Add("✗ TestIList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIList - ERROR: {ex.Message}");
            }
        }

        // IReadOnlyList Tests
        public static void TestIReadOnlyList(List<string> results)
        {
            try
            {
                IReadOnlyList<int> val = new List<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IReadOnlyList<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIReadOnlyList - PASSED");
                else
                    results.Add("✗ TestIReadOnlyList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIReadOnlyList - ERROR: {ex.Message}");
            }
        }

        // IDictionary Tests
        public static void TestIDictionary(List<string> results)
        {
            try
            {
                IDictionary<int, string> val = new Dictionary<int, string>
                {
                    [1] = "One",
                    [2] = "Two",
                    [3] = "Three"
                };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.ContainsKey(kv.Key) && result[kv.Key] == kv.Value))
                    results.Add("✓ TestIDictionary - PASSED");
                else
                    results.Add("✗ TestIDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIDictionary - ERROR: {ex.Message}");
            }
        }

        // IReadOnlyDictionary Tests
        public static void TestIReadOnlyDictionary(List<string> results)
        {
            try
            {
                IReadOnlyDictionary<int, string> val = new Dictionary<int, string>
                {
                    [1] = "One",
                    [2] = "Two",
                    [3] = "Three"
                };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IReadOnlyDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.ContainsKey(kv.Key) && result[kv.Key] == kv.Value))
                    results.Add("✓ TestIReadOnlyDictionary - PASSED");
                else
                    results.Add("✗ TestIReadOnlyDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIReadOnlyDictionary - ERROR: {ex.Message}");
            }
        }

        // ILookup Tests
        public static void TestILookup(List<string> results)
        {
            try
            {
                var data = new[]
                {
                    new { Key = "A", Value = 1 },
                    new { Key = "B", Value = 2 },
                    new { Key = "A", Value = 3 },
                    new { Key = "C", Value = 4 },
                    new { Key = "B", Value = 5 }
                };
                
                ILookup<string, int> val = data.ToLookup(x => x.Key, x => x.Value);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ILookup<string, int>>(buf);
                
                bool equal = true;
                foreach (var group in val)
                {
                    if (!result.Contains(group.Key))
                    {
                        equal = false;
                        break;
                    }
                    
                    if (!group.SequenceEqual(result[group.Key]))
                    {
                        equal = false;
                        break;
                    }
                }
                
                if (equal && val.Count == result.Count)
                    results.Add("✓ TestILookup - PASSED");
                else
                    results.Add("✗ TestILookup - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestILookup - ERROR: {ex.Message}");
            }
        }

        // IGrouping Tests
        public static void TestIGrouping(List<string> results)
        {
            try
            {
                var data = new[]
                {
                    new { Key = "A", Value = 1 },
                    new { Key = "A", Value = 2 },
                    new { Key = "A", Value = 3 }
                };
                
                var lookup = data.ToLookup(x => x.Key, x => x.Value);
                IGrouping<string, int> val = lookup.First();
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IGrouping<string, int>>(buf);
                
                if (val.Key == result.Key && val.SequenceEqual(result))
                    results.Add("✓ TestIGrouping - PASSED");
                else
                    results.Add("✗ TestIGrouping - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIGrouping - ERROR: {ex.Message}");
            }
        }

        // ISet Tests
        public static void TestISet(List<string> results)
        {
            try
            {
                ISet<int> val = new HashSet<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<ISet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestISet - PASSED");
                else
                    results.Add("✗ TestISet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestISet - ERROR: {ex.Message}");
            }
        }

        // IReadOnlySet Tests
        public static void TestIReadOnlySet(List<string> results)
        {
            try
            {
                IReadOnlySet<int> val = new HashSet<int> { 1, 2, 3, 4, 5 };
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IReadOnlySet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestIReadOnlySet - PASSED");
                else
                    results.Add("✗ TestIReadOnlySet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIReadOnlySet - ERROR: {ex.Message}");
            }
        }

        // 不可变集合接口测试

        // IImmutableList Tests
        public static void TestIImmutableList(List<string> results)
        {
            try
            {
                IImmutableList<int> val = ImmutableList.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IImmutableList<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIImmutableList - PASSED");
                else
                    results.Add("✗ TestIImmutableList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIImmutableList - ERROR: {ex.Message}");
            }
        }

        // IImmutableQueue Tests
        public static void TestIImmutableQueue(List<string> results)
        {
            try
            {
                IImmutableQueue<int> val = ImmutableQueue.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IImmutableQueue<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIImmutableQueue - PASSED");
                else
                    results.Add("✗ TestIImmutableQueue - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIImmutableQueue - ERROR: {ex.Message}");
            }
        }

        // IImmutableStack Tests
        public static void TestIImmutableStack(List<string> results)
        {
            try
            {
                IImmutableStack<int> val = ImmutableStack.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IImmutableStack<int>>(buf);
                
                if (val.SequenceEqual(result))
                    results.Add("✓ TestIImmutableStack - PASSED");
                else
                    results.Add("✗ TestIImmutableStack - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIImmutableStack - ERROR: {ex.Message}");
            }
        }

        // IImmutableDictionary Tests
        public static void TestIImmutableDictionary(List<string> results)
        {
            try
            {
                IImmutableDictionary<int, string> val = ImmutableDictionary.CreateRange(new[]
                {
                    KeyValuePair.Create(1, "One"),
                    KeyValuePair.Create(2, "Two"),
                    KeyValuePair.Create(3, "Three")
                });
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IImmutableDictionary<int, string>>(buf);
                
                if (val.Count == result.Count && val.All(kv => result.TryGetValue(kv.Key, out var v) && v == kv.Value))
                    results.Add("✓ TestIImmutableDictionary - PASSED");
                else
                    results.Add("✗ TestIImmutableDictionary - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIImmutableDictionary - ERROR: {ex.Message}");
            }
        }

        // IImmutableSet Tests
        public static void TestIImmutableSet(List<string> results)
        {
            try
            {
                IImmutableSet<int> val = ImmutableHashSet.Create(1, 2, 3, 4, 5);
                
                byte[] buf = LuminPackSerializer.Serialize(val);
                var result = LuminPackSerializer.Deserialize<IImmutableSet<int>>(buf);
                
                if (val.SetEquals(result))
                    results.Add("✓ TestIImmutableSet - PASSED");
                else
                    results.Add("✗ TestIImmutableSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIImmutableSet - ERROR: {ex.Message}");
            }
        }
    }
}