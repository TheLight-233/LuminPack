using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  布局不变量回归测试
    //
    //  LuminPack 通过"Local 镜像类"访问 private/protected 槽位：镜像的字段序列必须与真实类型的
    //  内存布局逐槽对齐，否则 Unsafe.As 重解释后会读到错误的槽位（表现为内存访问违规）。
    //
    //  这里固化一条实测结论：镜像必须复刻真实类型的**继承层级**。把基类字段与派生类字段平铺到
    //  同一个类里（即使显式标注 LayoutKind.Sequential）与真实布局并不等价——运行时会为基类与派生类
    //  分别计算布局，平铺声明对不上。生成器因此始终按层级生成镜像。
    // ─────────────────────────────────────────────────────────────────────────

    public class LayoutBase
    {
        public int BaseValue { get; set; }
    }

    public class LayoutDerived : LayoutBase
    {
        public int AdditionalValue { get; set; }
        public List<int> Items { get; set; } = new();
    }

    // 正确形态：复刻继承层级（生成器实际采用的形态）
    internal class MirrorChainBase
    {
        internal int BaseValue;
    }

    internal class MirrorChain : MirrorChainBase
    {
        internal int AdditionalValue;
        internal List<int>? Items;
    }

    public static class LocalLayoutInvariantTest
    {
        public static void Run(List<string> results)
        {
            try
            {
                var d = new LayoutDerived { BaseValue = 11, AdditionalValue = 22 };
                d.Items.Add(7);

                ref var chain = ref Unsafe.As<LayoutDerived, MirrorChain>(ref d);

                bool ok = chain.BaseValue == 11
                    && chain.AdditionalValue == 22
                    && ReferenceEquals(chain.Items, d.Items);

                results.Add(ok
                    ? "✓ LocalLayoutInvariantTest - PASSED"
                    : "✗ LocalLayoutInvariantTest - FAILED (hierarchy-faithful mirror does not match the real layout)");
            }
            catch (Exception ex)
            {
                results.Add("✗ LocalLayoutInvariantTest - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}