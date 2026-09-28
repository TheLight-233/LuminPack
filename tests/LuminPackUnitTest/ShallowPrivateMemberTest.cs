using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  二层嵌套 private 回归测试：根 ->(private) ShallowLevel1 ->(private) int
    //  覆盖"嵌套类型自身私有成员"这一最基本形态，防止深层改造回归浅层行为。
    // ─────────────────────────────────────────────────────────────────────────

    [LuminPackable]
    public class ShallowRoot
    {
        public int RootValue;

        [LuminPackInclude]
        private ShallowLevel1 _level1 = new();

        public ShallowLevel1 Level1 => _level1;
    }

    public class ShallowLevel1
    {
        public int Value1;

        [LuminPackInclude]
        private int _secret = 42;

        public int Secret => _secret;
        public void SetSecret(int value) => _secret = value;
    }

    public static class ShallowPrivateMemberTest
    {
        public static void Run(List<string> results)
        {
            try
            {
                var original = new ShallowRoot { RootValue = 1 };
                original.Level1.Value1 = 2;
                original.Level1.SetSecret(5);

                var restored = LuminPackSerializer.Deserialize<ShallowRoot>(LuminPackSerializer.Serialize(original));

                bool ok = restored.RootValue == 1
                    && restored.Level1.Value1 == 2
                    && restored.Level1.Secret == 5;

                results.Add(ok
                    ? "✓ ShallowPrivateMemberTest - PASSED"
                    : "✗ ShallowPrivateMemberTest - FAILED (data mismatch)");
            }
            catch (Exception ex)
            {
                results.Add("✗ ShallowPrivateMemberTest - ERROR: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}