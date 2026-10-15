using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Regression coverage for on-demand Local generation.
    //
    //  Local mirrors are also required at VersionTolerant and CircleReference roots, which always bind
    //  mirrors for all members; LuminPackPoolRent types also use mirrors during JSON reading
    //  Populate every field; missing one can leave generated references to nonexistent Local types (CS0246).
    //
    //  These three models use only public fields, covering the easily missed shape without private fields or auto-properties.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Provides public field circle node functionality.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public class PublicFieldCircleNode
    {
        /// <summary>Stores the id.</summary>
        [LuminPackOrder(0)]
        public int Id;

        /// <summary>Stores the next.</summary>
        [LuminPackOrder(1)]
        public PublicFieldCircleNode? Next;
    }

    /// <summary>Provides public field version tolerant functionality.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class PublicFieldVersionTolerant
    {
        /// <summary>Stores the a.</summary>
        [LuminPackOrder(0)]
        public int A;

        /// <summary>Stores the name.</summary>
        [LuminPackOrder(1)]
        public string? Name;
    }

    /// <summary>Provides public field pooled functionality.</summary>
    [LuminPackable]
    public class PublicFieldPooled
    {
        /// <summary>Stores the a.</summary>
        public int A;

        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        [LuminPackPoolRent]
        public static PublicFieldPooled Rent() => new PublicFieldPooled();
    }

    /// <summary>Provides on demand local layout test functionality.</summary>
    public static class OnDemandLocalLayoutTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void Run(List<string> results)
        {
            TestCircleReference(results);
            TestVersionTolerant(results);
            TestPooled(results);
        }

        /// <summary>Verifies test circle reference.</summary>
        /// <param name="results">The results used by this operation.</param>
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

        /// <summary>Verifies test version tolerant.</summary>
        /// <param name="results">The results used by this operation.</param>
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

        /// <summary>Verifies test pooled.</summary>
        /// <param name="results">The results used by this operation.</param>
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
