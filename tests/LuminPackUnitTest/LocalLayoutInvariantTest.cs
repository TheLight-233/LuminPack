using System.Runtime.CompilerServices;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Layout invariant regression tests.
    //
    //  LuminPack accesses private and protected storage through Local mirrors; mirror fields must match the original
    //  Match every storage slot; otherwise Unsafe.As reinterprets incorrect offsets and can cause access violations.
    //
    //  Mirrors must reproduce the original inheritance hierarchy. Flattening base and derived fields into
    //  A flattened class, even with Sequential layout, differs because the runtime lays out base and derived classes
    //  Each inheritance level has its own layout; emit hierarchical mirrors instead of flattening declarations.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Provides layout base functionality.</summary>
    public class LayoutBase
    {
        /// <summary>Gets or sets the base value.</summary>
        public int BaseValue { get; set; }
    }

    /// <summary>Provides layout derived functionality.</summary>
    public class LayoutDerived : LayoutBase
    {
        /// <summary>Gets or sets the additional value.</summary>
        public int AdditionalValue { get; set; }
        /// <summary>Gets or sets the items.</summary>
        public List<int> Items { get; set; } = new();
    }

    // Preserve the inheritance hierarchy, matching the generator's mirror strategy.
    /// <summary>Provides mirror chain base functionality.</summary>
    internal class MirrorChainBase
    {
        internal int BaseValue;
    }

    /// <summary>Provides mirror chain functionality.</summary>
    internal class MirrorChain : MirrorChainBase
    {
        internal int AdditionalValue;
        internal List<int>? Items;
    }

    /// <summary>Provides local layout invariant test functionality.</summary>
    public static class LocalLayoutInvariantTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
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
