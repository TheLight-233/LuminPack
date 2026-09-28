using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  多层嵌套 private 成员链回归测试
    //
    //  DeepRoot ->(private) DeepLevel1 ->(private) DeepLevel2 ->(private) DeepLevel3
    //
    //  每一层都通过 [LuminPackInclude] 要求序列化 private 成员。改动前私有成员由
    //  [UnsafeAccessor] + in 参数访问：对引用类型它返回的 ref 不指向真实字段，表现为读到垃圾值、
    //  写入丢失，反复写入最终触发 AccessViolation（并非层级上限所致）；改动后所有私有访问都经
    //  Local 镜像派发，不再区分引用类型与值类型。
    // ─────────────────────────────────────────────────────────────────────────

    [LuminPackable]
    public class DeepRoot
    {
        public int RootValue;

        [LuminPackInclude]
        private DeepLevel1 _level1 = new();

        public DeepLevel1 Level1 => _level1;
    }

    // Each level is itself [LuminPackable]: the JSON writer dispatches a nested object through its own
    // formatter rather than inlining it, so a level without one would fail on the JSON path.
    [LuminPackable]
    public class DeepLevel1
    {
        public int Value1;

        [LuminPackInclude]
        private DeepLevel2 _level2 = new();

        public DeepLevel2 Level2 => _level2;
    }

    [LuminPackable]
    public class DeepLevel2
    {
        public int Value2;

        [LuminPackInclude]
        private DeepLevel3 _level3 = new();

        public DeepLevel3 Level3 => _level3;
    }

    [LuminPackable]
    public class DeepLevel3
    {
        public int Value3;

        [LuminPackInclude]
        private int _secret = 42;

        public int Secret => _secret;
        public void SetSecret(int value) => _secret = value;
    }

    public static class DeepPrivateMemberTest
    {
        public static void Run(List<string> results)
        {
            try
            {
                var original = new DeepRoot { RootValue = 1 };
                original.Level1.Value1 = 2;
                original.Level1.Level2.Value2 = 3;
                original.Level1.Level2.Level3.Value3 = 4;
                original.Level1.Level2.Level3.SetSecret(5);

                // Each level is also serializable on its own. Besides being a realistic usage, this is
                // what makes the nested types reachable in Minimal generation mode, which the JSON path
                // needs because it dispatches nested objects through their own formatter.
                _ = LuminPackSerializer.Serialize(original.Level1);
                _ = LuminPackSerializer.Serialize(original.Level1.Level2);
                _ = LuminPackSerializer.Serialize(original.Level1.Level2.Level3);

                var buffer = LuminPackSerializer.Serialize(original);
                var restored = LuminPackSerializer.Deserialize<DeepRoot>(buffer);

                bool binaryOk = restored.RootValue == 1
                    && restored.Level1.Value1 == 2
                    && restored.Level1.Level2.Value2 == 3
                    && restored.Level1.Level2.Level3.Value3 == 4
                    && restored.Level1.Level2.Level3.Secret == 5;

                string json = LuminPackSerializer.SerializeJson(original);
                var fromJson = LuminPackSerializer.DeserializeJson<DeepRoot>(json);

                bool jsonOk = fromJson.RootValue == 1
                    && fromJson.Level1.Value1 == 2
                    && fromJson.Level1.Level2.Value2 == 3
                    && fromJson.Level1.Level2.Level3.Value3 == 4
                    && fromJson.Level1.Level2.Level3.Secret == 5;

                results.Add(binaryOk && jsonOk
                    ? "✓ DeepPrivateMemberTest - PASSED"
                    : "✗ DeepPrivateMemberTest - FAILED (binary=" + binaryOk + " json=" + jsonOk + ")");
            }
            catch (Exception ex)
            {
                results.Add("✗ DeepPrivateMemberTest - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}