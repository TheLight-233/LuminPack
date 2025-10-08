using System;
using System.Collections.Generic;
using System.Linq;

// 测试用的类型定义
public class TypeA { }
public class TypeB { }
public class TypeC { }
public class TypeD { }
public class TypeE { }
public class TypeF { } // 用于测试不存在的类型

unsafe class Program
{
    static void Main()
    {
        TestHashAlgorithmWithRealTypes();
    }

    static unsafe void TestHashAlgorithmWithRealTypes()
    {
        Console.WriteLine("=== 使用真实类型的哈希算法测试 ===");
        
        // 创建测试类型和对应的标签/索引
        var testTypes = new (Type Type, ushort Tag, ushort Index)[]
        {
            (typeof(TypeA), 1, 0),
            (typeof(TypeB), 2, 1),
            (typeof(TypeC), 3, 2),
            (typeof(TypeD), 4, 3),
            (typeof(TypeE), 5, 4)
        };

        // 创建哈希表（使用相同的算法计算大小）
        int tableSize = testTypes.Length;
        tableSize--;
        tableSize |= tableSize >> 1;
        tableSize |= tableSize >> 2;
        tableSize |= tableSize >> 4;
        tableSize |= tableSize >> 8;
        tableSize |= tableSize >> 16;
        tableSize++;
        tableSize = tableSize * 8;

        Console.WriteLine($"哈希表大小: {tableSize}");
        Console.WriteLine($"HASH_MASK: {tableSize - 1}");
        
        var hashTable = new HashEntry[tableSize];
        const uint GOLDEN_RATIO = 2654435769u;
        uint HASH_MASK = (uint)(tableSize - 1);

        // 初始化哈希表
        Console.WriteLine("\n=== 初始化哈希表 ===");
        for (int i = 0; i < testTypes.Length; i++)
        {
            var testCase = testTypes[i];
            
            // 使用实际的 GetMethodTable 方法
            void* methodTable = LuminPackMarshal.GetMethodTable(testCase.Type);
            ulong ptr = (ulong)methodTable;
            
            // 计算主哈希
            uint hash = (uint)((ptr * GOLDEN_RATIO) >> 32) & HASH_MASK;
            
            Console.WriteLine($"条目 {i}: Type={testCase.Type.Name}, MethodTable=0x{ptr:X16}, 初始哈希={hash}");

            // 处理冲突
            if (hashTable[hash].MethodTable != null && 
                hashTable[hash].MethodTable != methodTable)
            {
                Console.WriteLine($"  检测到冲突，进行二次哈希...");
                var hash2 = ((uint)(((ptr >> 32) * GOLDEN_RATIO) >> 32) & HASH_MASK) | 1u;
                int probeCount = 0;
                
                do
                {
                    hash = (hash + hash2) & HASH_MASK;
                    probeCount++;
                    Console.WriteLine($"    探测 {probeCount}: 新哈希={hash}");
                } while (hashTable[hash].MethodTable != null && 
                        hashTable[hash].MethodTable != methodTable && 
                        probeCount < 10); // 防止无限循环
            }

            // 插入条目
            if (hashTable[hash].MethodTable != methodTable)
            {
                hashTable[hash] = new HashEntry
                {
                    MethodTable = methodTable,
                    Tag = testCase.Tag,
                    Index = testCase.Index
                };
                Console.WriteLine($"  插入位置: {hash}, Tag={testCase.Tag}, Index={testCase.Index}");
            }
        }

        // 测试查找功能
        Console.WriteLine("\n=== 测试查找功能 ===");
        int successCount = 0;
        int failureCount = 0;

        foreach (var testCase in testTypes)
        {
            void* methodTable = LuminPackMarshal.GetMethodTable(testCase.Type);
            bool found = TryGetEntry(methodTable, hashTable, HASH_MASK, out ushort tag, out ushort index);
            
            if (found && tag == testCase.Tag && index == testCase.Index)
            {
                Console.WriteLine($"✓ {testCase.Type.Name}: 找到 Tag={tag}, Index={index}");
                successCount++;
            }
            else
            {
                Console.WriteLine($"✗ {testCase.Type.Name}: 期望 Tag={testCase.Tag}, Index={testCase.Index}, 实际 Tag={tag}, Index={index}");
                failureCount++;
            }
        }

        // 测试不存在的类型
        Console.WriteLine("\n=== 测试不存在的类型 ===");
        void* nonExistentMethodTable = LuminPackMarshal.GetMethodTable(typeof(TypeF));
        bool shouldNotFind = TryGetEntry(nonExistentMethodTable, hashTable, HASH_MASK, out ushort dummyTag, out ushort dummyIndex);
        
        if (!shouldNotFind)
        {
            Console.WriteLine($"✓ 不存在的类型 TypeF 正确返回未找到");
            successCount++;
        }
        else
        {
            Console.WriteLine($"✗ 不存在的类型 TypeF 错误地找到了 Tag={dummyTag}, Index={dummyIndex}");
            failureCount++;
        }

        // 输出统计信息
        Console.WriteLine("\n=== 测试结果 ===");
        Console.WriteLine($"成功: {successCount}");
        Console.WriteLine($"失败: {failureCount}");
        Console.WriteLine($"成功率: {(double)successCount / (successCount + failureCount) * 100:F2}%");

        // 分析哈希表分布
        AnalyzeHashDistribution(hashTable);

        // 性能测试
        Console.WriteLine("\n=== 性能测试 ===");
        PerformanceTest(hashTable, HASH_MASK, testTypes);
    }

    // 查找方法的实现（与源代码中相同）
    static unsafe bool TryGetEntry(void* mt, HashEntry[] hashTable, uint HASH_MASK, out ushort tag, out ushort index)
    {
        const uint GOLDEN_RATIO = 2654435769u;
        ulong ptr = (ulong)mt;

        // 主哈希
        var hash = (uint)((ptr * GOLDEN_RATIO) >> 32) & HASH_MASK;

        // 第一次探测
        var entry = hashTable[hash];
        if (entry.MethodTable == mt)
        {
            tag = entry.Tag;
            index = entry.Index;
            return true;
        }

        // 冲突处理
        if (entry.MethodTable != null)
        {
            // 二次哈希
            var hash2 = ((uint)(((ptr >> 32) * GOLDEN_RATIO) >> 32) & HASH_MASK) | 1u;

            // 前3次探测（展开循环）
            hash = (hash + hash2) & HASH_MASK;
            entry = hashTable[hash];
            if (entry.MethodTable == mt) { tag = entry.Tag; index = entry.Index; return true; }
            if (entry.MethodTable == null) { tag = 0; index = 0; return false; }

            hash = (hash + hash2) & HASH_MASK;
            entry = hashTable[hash];
            if (entry.MethodTable == mt) { tag = entry.Tag; index = entry.Index; return true; }
            if (entry.MethodTable == null) { tag = 0; index = 0; return false; }

            hash = (hash + hash2) & HASH_MASK;
            entry = hashTable[hash];
            if (entry.MethodTable == mt) { tag = entry.Tag; index = entry.Index; return true; }
            if (entry.MethodTable == null) { tag = 0; index = 0; return false; }

            // 后续探测
            for (int i = 3; i < 8; i++)
            {
                hash = (hash + hash2) & HASH_MASK;
                entry = hashTable[hash];
                if (entry.MethodTable == mt)
                {
                    tag = entry.Tag;
                    index = entry.Index;
                    return true;
                }
                if (entry.MethodTable == null)
                    break;
            }
        }

        tag = 0;
        index = 0;
        return false;
    }

    // 分析哈希分布
    static void AnalyzeHashDistribution(HashEntry[] hashTable)
    {
        Console.WriteLine("\n=== 哈希分布分析 ===");
        
        int totalEntries = hashTable.Length;
        int usedEntries = 0;
        
        // 手动计算已使用的条目
        foreach (var entry in hashTable)
        {
            if (entry.MethodTable != null)
                usedEntries++;
        }
        
        int emptyEntries = totalEntries - usedEntries;
        
        Console.WriteLine($"总槽位: {totalEntries}");
        Console.WriteLine($"已使用: {usedEntries}");
        Console.WriteLine($"空槽位: {emptyEntries}");
        Console.WriteLine($"负载因子: {(double)usedEntries / totalEntries * 100:F2}%");

        // 计算冲突统计 - 不使用 GroupBy
        var methodTableCounts = new Dictionary<IntPtr, int>();
        
        foreach (var entry in hashTable)
        {
            if (entry.MethodTable != null)
            {
                IntPtr key = new IntPtr(entry.MethodTable);
                if (methodTableCounts.ContainsKey(key))
                    methodTableCounts[key]++;
                else
                    methodTableCounts[key] = 1;
            }
        }
        
        var collisionGroups = methodTableCounts.Where(kvp => kvp.Value > 1).ToList();

        if (collisionGroups.Any())
        {
            Console.WriteLine($"检测到 {collisionGroups.Count} 个冲突组");
            foreach (var group in collisionGroups)
            {
                Console.WriteLine($"  MethodTable: 0x{group.Key:X16} 有 {group.Value} 个条目");
            }
        }
        else
        {
            Console.WriteLine("无冲突检测");
        }
    }

    // 性能测试
    static unsafe void PerformanceTest(HashEntry[] hashTable, uint HASH_MASK, (Type Type, ushort Tag, ushort Index)[] testTypes)
    {
        Console.WriteLine("开始性能测试...");
        
        int iterations = 1000000;
        var random = new Random();
        
        // 准备测试数据 - 不使用指针列表
        var testData = new List<IntPtr>();
        for (int i = 0; i < iterations; i++)
        {
            var testCase = testTypes[random.Next(testTypes.Length)];
            testData.Add(new IntPtr(LuminPackMarshal.GetMethodTable(testCase.Type)));
        }

        // 预热
        for (int i = 0; i < 1000; i++)
        {
            TryGetEntry(testData[i].ToPointer(), hashTable, HASH_MASK, out _, out _);
        }

        // 正式测试
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        int foundCount = 0;
        foreach (var methodTablePtr in testData)
        {
            if (TryGetEntry(methodTablePtr.ToPointer(), hashTable, HASH_MASK, out _, out _))
            {
                foundCount++;
            }
        }
        
        stopwatch.Stop();
        
        Console.WriteLine($"性能测试结果:");
        Console.WriteLine($"  总查找次数: {iterations}");
        Console.WriteLine($"  成功查找: {foundCount}");
        Console.WriteLine($"  总耗时: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  平均每次查找: {(double)stopwatch.ElapsedMilliseconds / iterations:F2} ms");
        Console.WriteLine($"  每秒查找次数: {iterations / stopwatch.Elapsed.TotalSeconds:F0} ops/s");
    }
}

// 模拟 LuminPackMarshal 类（根据您的实际实现调整）
public static class LuminPackMarshal
{
    private static readonly Dictionary<Type, IntPtr> _methodTableCache = new Dictionary<Type, IntPtr>();
    private static long _nextFakeMethodTable = 0x1000;

    public static unsafe void* GetMethodTable(Type type)
    {
        if (!_methodTableCache.TryGetValue(type, out var methodTable))
        {
            // 模拟方法表指针分配（实际项目中这里会返回真实的方法表指针）
            methodTable = new IntPtr(_nextFakeMethodTable);
            _nextFakeMethodTable += 0x1000;
            _methodTableCache[type] = methodTable;
        }
        return methodTable.ToPointer();
    }

    public static T GetArrayReference<T>(T[] array)
    {
        return array[0];
    }
}

// 模拟 HashEntry 结构
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
unsafe struct HashEntry
{
    public void* MethodTable;
    public ushort Tag;
    public ushort Index;
}