using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Local 按需生成的覆盖性回归测试
    //
    //  Local 镜像不是"有 private 才需要"：VersionTolerant 与 CircleReference 布局在根上无条件绑定
    //  镜像（它们经镜像访问所有成员），带 [LuminPackPoolRent] 的类型在 JSON 读取路径上也会经镜像
    //  回填所有字段。只要漏掉一处，生成代码就会引用到不存在的 Local 类型（CS0246）。
    //
    //  下面三个模型刻意只使用 public 字段——既没有 private 成员也没有自动属性，正是最容易漏判的形态。
    // ─────────────────────────────────────────────────────────────────────────

    [LuminPackable(GeneratorType.CircleReference)]
    public class PublicFieldCircleNode
    {
        [LuminPackOrder(0)]
        public int Id;

        [LuminPackOrder(1)]
        public PublicFieldCircleNode? Next;
    }

    [LuminPackable(GeneratorType.VersionTolerant)]
    public class PublicFieldVersionTolerant
    {
        [LuminPackOrder(0)]
        public int A;

        [LuminPackOrder(1)]
        public string? Name;
    }

    [LuminPackable]
    public class PublicFieldPooled
    {
        public int A;

        [LuminPackPoolRent]
        public static PublicFieldPooled Rent() => new PublicFieldPooled();
    }

    public static class OnDemandLocalLayoutTest
    {
        public static void Run(List<string> results)
        {
            TestCircleReference(results);
            TestVersionTolerant(results);
            TestPooled(results);
        }

        private static void TestCircleReference(List<string> results)
        {
            try
            {
                var head = new PublicFieldCircleNode { Id = 1 };
                head.Next = new PublicFieldCircleNode { Id = 2, Next = head };

                var restored = LuminPackSerializer.Deserialize<PublicFieldCircleNode>(
                    LuminPackSerializer.Serialize(head));

                var json = LuminPackSerializer.SerializeJson(head);
                var fromJson = LuminPackSerializer.DeserializeJson<PublicFieldCircleNode>(json);

                bool ok = restored.Id == 1
                    && restored.Next != null
                    && restored.Next.Id == 2
                    && ReferenceEquals(restored.Next.Next, restored)
                    && fromJson.Id == 1
                    && fromJson.Next != null
                    && fromJson.Next.Id == 2
                    && ReferenceEquals(fromJson.Next.Next, fromJson);

                results.Add(ok
                    ? "✓ OnDemandLocalLayoutTest CircleReference - PASSED"
                    : "✗ OnDemandLocalLayoutTest CircleReference - FAILED");
            }
            catch (Exception ex)
            {
                results.Add("✗ OnDemandLocalLayoutTest CircleReference - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestVersionTolerant(List<string> results)
        {
            try
            {
                var original = new PublicFieldVersionTolerant { A = 7, Name = "vt" };

                var restored = LuminPackSerializer.Deserialize<PublicFieldVersionTolerant>(
                    LuminPackSerializer.Serialize(original));

                var fromJson = LuminPackSerializer.DeserializeJson<PublicFieldVersionTolerant>(
                    LuminPackSerializer.SerializeJson(original));

                bool ok = restored.A == 7 && restored.Name == "vt"
                    && fromJson.A == 7 && fromJson.Name == "vt";

                results.Add(ok
                    ? "✓ OnDemandLocalLayoutTest VersionTolerant - PASSED"
                    : "✗ OnDemandLocalLayoutTest VersionTolerant - FAILED (A=" + restored.A
                        + " Name=" + restored.Name + " jsonA=" + fromJson.A + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ OnDemandLocalLayoutTest VersionTolerant - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestPooled(List<string> results)
        {
            try
            {
                var original = new PublicFieldPooled { A = 9 };

                var restored = LuminPackSerializer.Deserialize<PublicFieldPooled>(
                    LuminPackSerializer.Serialize(original));

                var fromJson = LuminPackSerializer.DeserializeJson<PublicFieldPooled>(
                    LuminPackSerializer.SerializeJson(original));

                bool ok = restored.A == 9 && fromJson.A == 9;

                results.Add(ok
                    ? "✓ OnDemandLocalLayoutTest PoolRent - PASSED"
                    : "✗ OnDemandLocalLayoutTest PoolRent - FAILED (A=" + restored.A + " jsonA=" + fromJson.A + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ OnDemandLocalLayoutTest PoolRent - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}