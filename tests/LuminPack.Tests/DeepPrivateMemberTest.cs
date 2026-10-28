using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Regression coverage for deeply nested private member chains.
    //
    //  DeepRoot ->(private) DeepLevel1 ->(private) DeepLevel2 ->(private) DeepLevel3
    //
    //  Every level includes private members through LuminPackInclude. Previously, those members used
    //  For reference types, UnsafeAccessor with an in parameter returns a reference that does not identify the actual field, causing invalid reads,
    //  Writes can be lost and eventually cause access violations; all private member access now uses
    //  Local mirror dispatch for both reference types and unmanaged types.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Provides deep root functionality.</summary>
    [LuminPackable]
    public class DeepRoot
    {
        /// <summary>Stores the root value.</summary>
        public int RootValue;

        [LuminPackInclude]
        private DeepLevel1 _level1 = new();

        /// <summary>Gets the level1.</summary>
        public DeepLevel1 Level1 => _level1;
    }

    // Each level is itself [LuminPackable]: the JSON writer dispatches a nested object through its own
    // formatter rather than inlining it, so a level without one would fail on the JSON path.
    /// <summary>Provides deep level1 functionality.</summary>
    [LuminPackable]
    public class DeepLevel1
    {
        /// <summary>Stores the value1.</summary>
        public int Value1;

        [LuminPackInclude]
        private DeepLevel2 _level2 = new();

        /// <summary>Gets the level2.</summary>
        public DeepLevel2 Level2 => _level2;
    }

    /// <summary>Provides deep level2 functionality.</summary>
    [LuminPackable]
    public class DeepLevel2
    {
        /// <summary>Stores the value2.</summary>
        public int Value2;

        [LuminPackInclude]
        private DeepLevel3 _level3 = new();

        /// <summary>Gets the level3.</summary>
        public DeepLevel3 Level3 => _level3;
    }

    /// <summary>Provides deep level3 functionality.</summary>
    [LuminPackable]
    public class DeepLevel3
    {
        /// <summary>Stores the value3.</summary>
        public int Value3;

        [LuminPackInclude]
        private int _secret = 42;

        /// <summary>Gets the secret.</summary>
        public int Secret => _secret;
        /// <summary>Updates the secret used by the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void SetSecret(int value) => _secret = value;
    }

    /// <summary>Provides deep private member test functionality.</summary>
    public static class DeepPrivateMemberTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
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
