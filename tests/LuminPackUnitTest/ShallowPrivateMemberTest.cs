using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Regression: root to private ShallowLevel1 to private integer.
    //  Cover private members on the nested type itself to protect shallow access during deeper layout changes.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Provides shallow root functionality.</summary>
    [LuminPackable]
    public class ShallowRoot
    {
        /// <summary>Stores the root value.</summary>
        public int RootValue;

        [LuminPackInclude]
        private ShallowLevel1 _level1 = new();

        /// <summary>Gets the level1.</summary>
        public ShallowLevel1 Level1 => _level1;
    }

    /// <summary>Provides shallow level1 functionality.</summary>
    public class ShallowLevel1
    {
        /// <summary>Stores the value1.</summary>
        public int Value1;

        [LuminPackInclude]
        private int _secret = 42;

        /// <summary>Gets the secret.</summary>
        public int Secret => _secret;
        /// <summary>Updates the secret used by the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void SetSecret(int value) => _secret = value;
    }

    /// <summary>Provides shallow private member test functionality.</summary>
    public static class ShallowPrivateMemberTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
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
