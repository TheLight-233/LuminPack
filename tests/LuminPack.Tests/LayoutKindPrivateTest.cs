using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Regression coverage for matching mirror LayoutKind.
    //
    //  Local mirrors must match the original inheritance chain and LayoutKind. If an Explicit type instead receives
    //  Auto layout can place slots at different offsets, causing invalid references or memory corruption when reinterpreted.
    //  Round-trip Explicit classes and structs, plus Sequential layouts.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>An Explicit-layout class whose private FieldOffset slots must be reproduced by its mirror.</summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Explicit)]
    public class ExplicitLayoutClassRoot
    {
        /// <summary>Stores the a.</summary>
        [FieldOffset(0)]
        public int A;

        [FieldOffset(4)]
        [LuminPackInclude]
        private int _b;

        /// <summary>Obtains the b for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public int GetB() => _b;
        /// <summary>Updates the b used by the current operation.</summary>
        /// <param name="b">The b used by this operation.</param>
        public void SetB(int b) => _b = b;
    }

    /// <summary>A Sequential-layout class whose non-default layout must be preserved by its mirror.</summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Sequential)]
    public class SequentialLayoutClassRoot
    {
        /// <summary>Stores the a.</summary>
        public int A;

        [LuminPackInclude]
        private string _name = string.Empty;

        /// <summary>Obtains the name for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public string GetName() => _name;
        /// <summary>Updates the name used by the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void SetName(string value) => _name = value;
    }

    /// <summary>
    /// An Explicit-layout struct with a reference field so it exercises member reading rather than the unmanaged whole-storage copy path,
    /// so the test exercises the Local mirror path.
    /// </summary>
    [LuminPackable]
    [StructLayout(LayoutKind.Explicit)]
    public struct ExplicitLayoutStructRoot
    {
        /// <summary>Stores the a.</summary>
        [FieldOffset(0)]
        public int A;

        /// <summary>Stores the name.</summary>
        [FieldOffset(8)]
        public string Name;

        [FieldOffset(16)]
        [LuminPackInclude]
        private int _b;

        /// <summary>Obtains the b for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public int GetB() => _b;
        /// <summary>Updates the b used by the current operation.</summary>
        /// <param name="b">The b used by this operation.</param>
        public void SetB(int b) => _b = b;
    }

    /// <summary>A private nested slot references an Explicit-layout type, exercising nested mirrors and accessors.</summary>
    [LuminPackable]
    public class NestedExplicitHolder
    {
        /// <summary>Stores the outer.</summary>
        public int Outer;

        [LuminPackInclude]
        private ExplicitLayoutClassRoot _inner = new();

        /// <summary>Gets the inner.</summary>
        public ExplicitLayoutClassRoot Inner => _inner;
    }

    /// <summary>Provides layout kind private test functionality.</summary>
    public static class LayoutKindPrivateTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void Run(List<string> results)
        {
            TestExplicitClass(results);
            TestSequentialClass(results);
            TestExplicitStruct(results);
            TestNestedExplicit(results);
        }

        /// <summary>Verifies test explicit class.</summary>
        /// <param name="results">The results used by this operation.</param>
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

        /// <summary>Verifies test sequential class.</summary>
        /// <param name="results">The results used by this operation.</param>
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

        /// <summary>Verifies test explicit struct.</summary>
        /// <param name="results">The results used by this operation.</param>
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

        /// <summary>Verifies test nested explicit.</summary>
        /// <param name="results">The results used by this operation.</param>
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
