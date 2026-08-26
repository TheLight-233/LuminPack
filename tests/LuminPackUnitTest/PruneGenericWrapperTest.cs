using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    /// <summary>
    /// Verifies prune-mode reachability handles the interprocedural generic wrapper pattern
    /// <c>void Save&lt;T&gt;(T x) =&gt; Serialize(x)</c>.  The UnitTest project runs in Prune mode, so a
    /// type that is serialized only through such a wrapper is reachable only because the generator
    /// propagates the concrete instantiation of T from the wrapper's call sites.  If propagation were
    /// missing, deserialization would throw "no source generated formatter" at runtime.
    /// </summary>
    public static class PruneGenericWrapperTest
    {
        // Only reachable through the generic wrapper methods below (never passed to
        // LuminPackSerializer.Serialize directly elsewhere).
        [LuminPackable]
        public sealed class WrapperPayload
        {
            public int Id;
            public string Name = "";
            public List<int> Numbers = new();
        }

        private static void Save<T>(T value)
        {
            _ = LuminPackSerializer.Serialize(value);
        }

        private static byte[] SaveReturn<T>(T value)
        {
            return LuminPackSerializer.Serialize(value);
        }

        private static T Restore<T>(byte[] bytes)
        {
            return LuminPackSerializer.Deserialize<T>(bytes);
        }

        public static void Run(List<string> results)
        {
            var original = new WrapperPayload
            {
                Id = 42,
                Name = "prune-wrapper",
                Numbers = new List<int> { 1, 2, 3 }
            };

            // Void wrapper: type inference resolves T = WrapperPayload.
            Save(original);

            // Round-trip through the wrapper that returns the payload.
            var restored = Restore<WrapperPayload>(SaveReturn(original));

            if (original.Id == restored.Id &&
                original.Name == restored.Name &&
                original.Numbers.Count == restored.Numbers.Count &&
                original.Numbers[0] == restored.Numbers[0] &&
                original.Numbers[1] == restored.Numbers[1] &&
                original.Numbers[2] == restored.Numbers[2])
            {
                results.Add("✓ PruneGenericWrapperTest - PASSED");
            }
            else
            {
                results.Add("✗ PruneGenericWrapperTest - FAILED (data mismatch)");
            }
        }
    }
}
