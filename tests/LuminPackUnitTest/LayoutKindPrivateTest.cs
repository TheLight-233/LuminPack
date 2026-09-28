using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  LayoutKind 镜像一致性回归测试
    //
    //  Local 镜像必须与真实类型拥有完全一致的继承链与 LayoutKind。若真实类型是 Explicit 而镜像被
    //  按 Auto 布局，槽位就会落在完全不同的偏移上，重解释后读到垃圾引用并崩溃（或静默写坏内存）。
    //  这里对 Explicit（类与结构体）与 Sequential 分别做一次往返验证。
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Explicit 布局的类：私有槽位带 [FieldOffset]，镜像必须原样复刻。</summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Explicit)]
    public class ExplicitLayoutClassRoot
    {
        [FieldOffset(0)]
        public int A;

        [FieldOffset(4)]
        [LuminPackInclude]
        private int _b;

        public int GetB() => _b;
        public void SetB(int b) => _b = b;
    }

    /// <summary>Sequential 布局的类：非默认布局同样必须被镜像。</summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Sequential)]
    public class SequentialLayoutClassRoot
    {
        public int A;

        [LuminPackInclude]
        private string _name = string.Empty;

        public string GetName() => _name;
        public void SetName(string value) => _name = value;
    }

    /// <summary>
    /// Explicit 布局的结构体。带引用类型字段是为了避开"纯非托管结构体直接整体拷贝"的快速路径，
    /// 从而真正走到 Local 镜像。
    /// </summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Explicit)]
    public struct ExplicitLayoutStructRoot
    {
        [FieldOffset(0)]
        public int A;

        [FieldOffset(8)]
        public string Name;

        [FieldOffset(16)]
        [LuminPackInclude]
        private int _b;

        public int GetB() => _b;
        public void SetB(int b) => _b = b;
    }

    /// <summary>嵌套私有槽位指向一个 Explicit 布局类型：走嵌套镜像 + 访问器路径。</summary>
    [LuminPackable]
    public class NestedExplicitHolder
    {
        public int Outer;

        [LuminPackInclude]
        private ExplicitLayoutClassRoot _inner = new();

        public ExplicitLayoutClassRoot Inner => _inner;
    }

    public static class LayoutKindPrivateTest
    {
        public static void Run(List<string> results)
        {
            TestExplicitClass(results);
            TestSequentialClass(results);
            TestExplicitStruct(results);
            TestNestedExplicit(results);
        }

        private static void TestExplicitClass(List<string> results)
        {
            try
            {
                var original = new ExplicitLayoutClassRoot { A = 1 };
                original.SetB(2);

                var restored = LuminPackSerializer.Deserialize<ExplicitLayoutClassRoot>(
                    LuminPackSerializer.Serialize(original));

                results.Add(restored.A == 1 && restored.GetB() == 2
                    ? "✓ LayoutKindPrivateTest ExplicitClass - PASSED"
                    : "✗ LayoutKindPrivateTest ExplicitClass - FAILED (A=" + restored.A + " B=" + restored.GetB() + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ LayoutKindPrivateTest ExplicitClass - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestSequentialClass(List<string> results)
        {
            try
            {
                var original = new SequentialLayoutClassRoot { A = 3 };
                original.SetName("seq");

                var restored = LuminPackSerializer.Deserialize<SequentialLayoutClassRoot>(
                    LuminPackSerializer.Serialize(original));

                results.Add(restored.A == 3 && restored.GetName() == "seq"
                    ? "✓ LayoutKindPrivateTest SequentialClass - PASSED"
                    : "✗ LayoutKindPrivateTest SequentialClass - FAILED (A=" + restored.A + " Name=" + restored.GetName() + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ LayoutKindPrivateTest SequentialClass - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestExplicitStruct(List<string> results)
        {
            try
            {
                var original = new ExplicitLayoutStructRoot { A = 4, Name = "explicit" };
                original.SetB(5);

                var restored = LuminPackSerializer.Deserialize<ExplicitLayoutStructRoot>(
                    LuminPackSerializer.Serialize(original));

                results.Add(restored.A == 4 && restored.Name == "explicit" && restored.GetB() == 5
                    ? "✓ LayoutKindPrivateTest ExplicitStruct - PASSED"
                    : "✗ LayoutKindPrivateTest ExplicitStruct - FAILED (A=" + restored.A
                        + " Name=" + restored.Name + " B=" + restored.GetB() + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ LayoutKindPrivateTest ExplicitStruct - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestNestedExplicit(List<string> results)
        {
            try
            {
                // Reachable in Minimal mode as well, so it also exercises the standalone formatter.
                _ = LuminPackSerializer.Serialize(new ExplicitLayoutClassRoot());

                var original = new NestedExplicitHolder { Outer = 6 };
                original.Inner.A = 7;
                original.Inner.SetB(8);

                var restored = LuminPackSerializer.Deserialize<NestedExplicitHolder>(
                    LuminPackSerializer.Serialize(original));

                results.Add(restored.Outer == 6 && restored.Inner.A == 7 && restored.Inner.GetB() == 8
                    ? "✓ LayoutKindPrivateTest NestedExplicit - PASSED"
                    : "✗ LayoutKindPrivateTest NestedExplicit - FAILED (Outer=" + restored.Outer
                        + " A=" + restored.Inner.A + " B=" + restored.Inner.GetB() + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ LayoutKindPrivateTest NestedExplicit - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}