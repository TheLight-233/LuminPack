using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    public class ComplexSerializationTest
    {

        public static void TestComplexDataSerialization(List<string> results)
        {
            try
            {
                Console.WriteLine("Testing ComplexData serialization...");
                
                // Create complex test data
                var original = new ComplexData
                {
                    A = new int[][] 
                    {
                        new int[] { 1, 2, 3 },
                        new int[] { 4, 5, 6 },
                        new int[] { 7, 8, 9 }
                    },
                    B = new List<int[]> 
                    {
                        new int[] { 10, 11 },
                        new int[] { 12, 13, 14 },
                        new int[] { 15 }
                    },
                    C = new List<int>[] 
                    {
                        new List<int> { 16, 17 },
                        new List<int> { 18, 19, 20 },
                        new List<int> { 21 }
                    },
                    D = new Dictionary<string, Dictionary<string, int>>
                    {
                        {
                            "dict1", 
                            new Dictionary<string, int>
                            {
                                { "key1", 100 },
                                { "key2", 200 },
                                { "key3", 300 }
                            }
                        },
                        {
                            "dict2", 
                            new Dictionary<string, int>
                            {
                                { "keyA", 400 },
                                { "keyB", 500 }
                            }
                        }
                    },
                    E = new Dictionary<string, Dictionary<string, int[][]>>[]
                    {
                        new Dictionary<string, Dictionary<string, int[][]>>
                        {
                            {
                                "outer1",
                                new Dictionary<string, int[][]>
                                {
                                    {
                                        "inner1",
                                        new int[][]
                                        {
                                            new int[] { 1000, 1001 },
                                            new int[] { 1002, 1003, 1004 }
                                        }
                                    }
                                }
                            }
                        },
                        new Dictionary<string, Dictionary<string, int[][]>>
                        {
                            {
                                "outer2",
                                new Dictionary<string, int[][]>
                                {
                                    {
                                        "inner2",
                                        new int[][]
                                        {
                                            new int[] { 2000 },
                                            new int[] { 2001, 2002 }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    F = new Data[][]
                    {
                        new Data[]
                        {
                            new Data { X = 1, Y = 2, Z = 3, F = 4, D = 1.1f, Db = 2.2, Bo = true, Name = "F1" },
                            new Data { X = 5, Y = 6, Z = 7, F = 8, D = 3.3f, Db = 4.4, Bo = false, Name = "F2" }
                        },
                        new Data[]
                        {
                            new Data { X = 9, Y = 10, Z = 11, F = 12, D = 5.5f, Db = 6.6, Bo = true, Name = "F3" }
                        }
                    },
                    G = new List<Data[]>
                    {
                        new Data[]
                        {
                            new Data { X = 13, Y = 14, Z = 15, F = 16, D = 7.7f, Db = 8.8, Bo = false, Name = "G1" }
                        },
                        new Data[]
                        {
                            new Data { X = 17, Y = 18, Z = 19, F = 20, D = 9.9f, Db = 10.10, Bo = true, Name = "G2" },
                            new Data { X = 21, Y = 22, Z = 23, F = 24, D = 11.11f, Db = 12.12, Bo = false, Name = "G3" }
                        }
                    },
                    H = new Data[][][]
                    {
                        new Data[][]
                        {
                            new Data[]
                            {
                                new Data { X = 25, Y = 26, Z = 27, F = 28, D = 13.13f, Db = 14.14, Bo = true, Name = "H1" }
                            }
                        },
                        new Data[][]
                        {
                            new Data[]
                            {
                                new Data { X = 29, Y = 30, Z = 31, F = 32, D = 15.15f, Db = 16.16, Bo = false, Name = "H2" }
                            },
                            new Data[]
                            {
                                new Data { X = 33, Y = 34, Z = 35, F = 36, D = 17.17f, Db = 18.18, Bo = true, Name = "H3" }
                            }
                        }
                    },
                    I = new List<Data>[]
                    {
                        new List<Data>
                        {
                            new Data { X = 37, Y = 38, Z = 39, F = 40, D = 19.19f, Db = 20.20, Bo = false, Name = "I1" },
                            new Data { X = 41, Y = 42, Z = 43, F = 44, D = 21.21f, Db = 22.22, Bo = true, Name = "I2" }
                        },
                        new List<Data>
                        {
                            new Data { X = 45, Y = 46, Z = 47, F = 48, D = 23.23f, Db = 24.24, Bo = false, Name = "I3" }
                        }
                    },
                    J = new List<Data[]>[]
                    {
                        new List<Data[]>
                        {
                            new Data[]
                            {
                                new Data { X = 49, Y = 50, Z = 51, F = 52, D = 25.25f, Db = 26.26, Bo = true, Name = "J1" }
                            }
                        },
                        new List<Data[]>
                        {
                            new Data[]
                            {
                                new Data { X = 53, Y = 54, Z = 55, F = 56, D = 27.27f, Db = 28.28, Bo = false, Name = "J2" },
                                new Data { X = 57, Y = 58, Z = 59, F = 60, D = 29.29f, Db = 30.30, Bo = true, Name = "J3" }
                            }
                        }
                    }
                };

                // Serialize and deserialize
                byte[] bytes = LuminPackSerializer.Serialize(original);
                ComplexData deserialized = LuminPackSerializer.Deserialize<ComplexData>(bytes);

                // Verify all data
                bool success = CompareComplexData(original, deserialized);
                
                if (success)
                    results.Add("✓ TestComplexDataSerialization - PASSED");
                else
                    results.Add("✗ TestComplexDataSerialization - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestComplexDataSerialization - ERROR: {ex.Message}");
            }
        }

        public static void TestNestedDataSerialization(List<string> results)
        {
            try
            {
                Console.WriteLine("Testing NestedData serialization...");
                
                var original = new NestedData
                {
                    Name = "TestNested",
                    Ps = new Data[]
                    {
                        new Data { X = 100, Y = 200, Z = 300, F = 400, D = 1.5f, Db = 2.5, Bo = true, Name = "Nested1" },
                        new Data { X = 500, Y = 600, Z = 700, F = 800, D = 3.5f, Db = 4.5, Bo = false, Name = "Nested2" },
                        new Data { X = 900, Y = 1000, Z = 1100, F = 1200, D = 5.5f, Db = 6.5, Bo = true, Name = "Nested3" }
                    }
                };

                // Serialize and deserialize
                byte[] bytes = LuminPackSerializer.Serialize(original);
                NestedData deserialized = LuminPackSerializer.Deserialize<NestedData>(bytes);

                // Verify data
                bool success = original.Name == deserialized.Name &&
                              original.Ps.Length == deserialized.Ps.Length;
                
                if (success)
                {
                    for (int i = 0; i < original.Ps.Length; i++)
                    {
                        if (!CompareData(original.Ps[i], deserialized.Ps[i]))
                        {
                            success = false;
                            break;
                        }
                    }
                }

                if (success)
                    results.Add("✓ TestNestedDataSerialization - PASSED");
                else
                    results.Add("✗ TestNestedDataSerialization - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestNestedDataSerialization - ERROR: {ex.Message}");
            }
        }

        public static void TestComplexDataWithNullValues(List<string> results)
        {
            try
            {
                Console.WriteLine("Testing ComplexData with null values...");
                
                var original = new ComplexData
                {
                    A = null,
                    B = null,
                    C = null,
                    D = null,
                    E = null,
                    F = null,
                    G = null,
                    H = null,
                    I = null,
                    J = null
                };

                // Serialize and deserialize
                byte[] bytes = LuminPackSerializer.Serialize(original);
                ComplexData deserialized = LuminPackSerializer.Deserialize<ComplexData>(bytes);

                // Verify all null values are preserved
                bool success = deserialized.A == null &&
                              deserialized.B == null &&
                              deserialized.C == null &&
                              deserialized.D == null &&
                              deserialized.E == null &&
                              deserialized.F == null &&
                              deserialized.G == null &&
                              deserialized.H == null &&
                              deserialized.I == null &&
                              deserialized.J == null;

                if (success)
                    results.Add("✓ TestComplexDataWithNullValues - PASSED");
                else
                    results.Add("✗ TestComplexDataWithNullValues - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestComplexDataWithNullValues - ERROR: {ex.Message}");
            }
        }

        public static void TestComplexDataWithEmptyCollections(List<string> results)
        {
            try
            {
                Console.WriteLine("Testing ComplexData with empty collections...");
                
                var original = new ComplexData
                {
                    A = new int[0][],
                    B = new List<int[]>(),
                    C = new List<int>[0],
                    D = new Dictionary<string, Dictionary<string, int>>(),
                    E = new Dictionary<string, Dictionary<string, int[][]>>[0],
                    F = new Data[0][],
                    G = new List<Data[]>(),
                    H = new Data[0][][],
                    I = new List<Data>[0],
                    J = new List<Data[]>[0]
                };

                // Serialize and deserialize
                byte[] bytes = LuminPackSerializer.Serialize(original);
                ComplexData deserialized = LuminPackSerializer.Deserialize<ComplexData>(bytes);

                // Verify empty collections are preserved
                bool success = deserialized.A != null && deserialized.A.Length == 0 &&
                              deserialized.B != null && deserialized.B.Count == 0 &&
                              deserialized.C != null && deserialized.C.Length == 0 &&
                              deserialized.D != null && deserialized.D.Count == 0 &&
                              deserialized.E != null && deserialized.E.Length == 0 &&
                              deserialized.F != null && deserialized.F.Length == 0 &&
                              deserialized.G != null && deserialized.G.Count == 0 &&
                              deserialized.H != null && deserialized.H.Length == 0 &&
                              deserialized.I != null && deserialized.I.Length == 0 &&
                              deserialized.J != null && deserialized.J.Length == 0;

                if (success)
                    results.Add("✓ TestComplexDataWithEmptyCollections - PASSED");
                else
                    results.Add("✗ TestComplexDataWithEmptyCollections - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestComplexDataWithEmptyCollections - ERROR: {ex.Message}");
            }
        }

        // Helper methods for comparison
        static bool CompareComplexData(ComplexData a, ComplexData b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return CompareJaggedArray(a.A, b.A) &&
                   CompareListOfArrays(a.B, b.B) &&
                   CompareArrayOfLists(a.C, b.C) &&
                   CompareNestedDictionary(a.D, b.D) &&
                   CompareComplexArray(a.E, b.E) &&
                   CompareJaggedDataArray(a.F, b.F) &&
                   CompareListOfDataArrays(a.G, b.G) &&
                   Compare3DDataArray(a.H, b.H) &&
                   CompareArrayOfDataLists(a.I, b.I) &&
                   CompareArrayOfDataArrayLists(a.J, b.J);
        }

        static bool CompareJaggedArray(int[][] a, int[][] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!ArraysEqual(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareListOfArrays(List<int[]> a, List<int[]> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!ArraysEqual(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareArrayOfLists(List<int>[] a, List<int>[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!ListsEqual(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareNestedDictionary(Dictionary<string, Dictionary<string, int>> a, Dictionary<string, Dictionary<string, int>> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var key in a.Keys)
            {
                if (!b.ContainsKey(key)) return false;
                if (!DictionariesEqual(a[key], b[key])) return false;
            }
            return true;
        }

        static bool CompareComplexArray(Dictionary<string, Dictionary<string, int[][]>>[] a, Dictionary<string, Dictionary<string, int[][]>>[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareNestedDictionaryWithArrays(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareNestedDictionaryWithArrays(Dictionary<string, Dictionary<string, int[][]>> a, Dictionary<string, Dictionary<string, int[][]>> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var outerKey in a.Keys)
            {
                if (!b.ContainsKey(outerKey)) return false;
                
                var innerDictA = a[outerKey];
                var innerDictB = b[outerKey];
                
                if (innerDictA.Count != innerDictB.Count) return false;
                
                foreach (var innerKey in innerDictA.Keys)
                {
                    if (!innerDictB.ContainsKey(innerKey)) return false;
                    if (!CompareJaggedArray(innerDictA[innerKey], innerDictB[innerKey])) return false;
                }
            }
            return true;
        }

        static bool CompareJaggedDataArray(Data[][] a, Data[][] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareDataArray(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareListOfDataArrays(List<Data[]> a, List<Data[]> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!CompareDataArray(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool Compare3DDataArray(Data[][][] a, Data[][][] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareJaggedDataArray(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareArrayOfDataLists(List<Data>[] a, List<Data>[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareDataList(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareArrayOfDataArrayLists(List<Data[]>[] a, List<Data[]>[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareListOfDataArrays(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareDataArray(Data[] a, Data[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (!CompareData(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareDataList(List<Data> a, List<Data> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!CompareData(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool CompareData(Data a, Data b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.X == b.X &&
                   a.Y == b.Y &&
                   a.Z == b.Z &&
                   a.F == b.F &&
                   Math.Abs(a.D - b.D) < 0.0001f &&
                   Math.Abs(a.Db - b.Db) < 0.0001 &&
                   a.Bo == b.Bo &&
                   a.Name == b.Name;
        }

        // Generic collection comparison helpers
        static bool ArraysEqual<T>(T[] a, T[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            
            for (int i = 0; i < a.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool ListsEqual<T>(List<T> a, List<T> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            
            for (int i = 0; i < a.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        static bool DictionariesEqual<TKey, TValue>(Dictionary<TKey, TValue> a, Dictionary<TKey, TValue> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach (var kvp in a)
            {
                if (!b.ContainsKey(kvp.Key)) return false;
                if (!EqualityComparer<TValue>.Default.Equals(kvp.Value, b[kvp.Key])) return false;
            }
            return true;
        }
    }

    [LuminPackable]
    public class ComplexData
    {
        [LuminPackOrder(0)] public int[][]? A;
        [LuminPackOrder(1)] public List<int[]>? B;
        [LuminPackOrder(2)] public List<int>[]? C;
        [LuminPackOrder(3)] public Dictionary<string, Dictionary<string, int>>? D;
        [LuminPackOrder(4)] public Dictionary<string, Dictionary<string, int[][]>>[]? E;
        [LuminPackOrder(5)] public Data[][]? F;
        [LuminPackOrder(6)] public List<Data[]>? G;
        [LuminPackOrder(7)] public Data[][][]? H;
        [LuminPackOrder(8)] public List<Data>[]? I;
        [LuminPackOrder(9)] public List<Data[]>[]? J;

        public override string ToString()
        {
            return $"{string.Join(",", A?.SelectMany(x => x).ToArray() ?? Array.Empty<int>())},\n" +
                   $"{string.Join(",", B?.SelectMany(x => x).ToArray() ?? Array.Empty<int>())},\n" +
                   $"{string.Join(",", C?.SelectMany(x => x).ToArray() ?? Array.Empty<int>())},\n" +
                   $"{GetDictString(D)},\n" +
                   $"{string.Join(",\n", E?.Select(GetDictString).ToArray() ?? Array.Empty<string>())}\n" +
                   $"{string.Join(",\n", F?.SelectMany(x => x).Select(x => x) ?? Array.Empty<Data>())}\n" +
                   $"{string.Join(",\n", G?.SelectMany(x => x).Select(x => x) ?? Array.Empty<Data>())}\n" +
                   $"{string.Join(",\n", H?.SelectMany(x => x).SelectMany(x => x)?.Select(x => x) ?? Array.Empty<Data>())}\n" +
                   $"{string.Join(",\n", I?.SelectMany(x => x).Select(x => x) ?? Array.Empty<Data>())}\n" +
                   $"{string.Join(",\n", J?.SelectMany(x => x).Select(x => x)?.SelectMany(x => x)?.Select(x => x) ?? Array.Empty<Data>())}\n";
        }

        private string GetDictString<Tk, Tv>(Dictionary<Tk, Dictionary<Tk, Tv>>? ddd)
        {
            if (ddd == null) return "null";
            return $"{string.Join(",", ddd.Keys.ToList())},\n" +
                   $"   {string.Join(",", ddd.Values.ToList().SelectMany(k => k?.Keys ?? Enumerable.Empty<Tk>()))},\n" +
                   $"   {string.Join(",", ddd.Values.ToList().SelectMany(k => k?.Values ?? Enumerable.Empty<Tv>()))}";
        }
    }
    
    [LuminPackable]
    public class Data
    {
        [LuminPackOrder(1)] public int X;
        [LuminPackOrder(2)] public short Y;
        [LuminPackOrder(3)] public long Z;
        [LuminPackOrder(4)] public ulong F;
        [LuminPackOrder(5)] public float D;
        [LuminPackOrder(6)] public double Db;
        [LuminPackOrder(7)] public bool Bo;
        [LuminPackOrder(8)] public string Name;

        public override string ToString()
        {
            return $"{X},{Y},{Z},{F},{D},{Db},{Bo},{Name}";
        }
    }

    [LuminPackable]
    public class NestedData
    {
        public string Name;
        public Data[] Ps;

        public override string ToString()
        {
            return $"{Name},{Ps[0]}";
        }
    }
}