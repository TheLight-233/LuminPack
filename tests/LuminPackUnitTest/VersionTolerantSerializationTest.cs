using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

/// <summary>Provides version tolerant serialization test functionality.</summary>
public class VersionTolerantSerializationTest
{
    /// <summary>Provides v1 functionality.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V1
    {
        /// <summary>Stores the instance.</summary>
        public static V1 Instance;
        /// <summary>Gets or sets the x.</summary>
        [LuminPackOrder(0)] public int X { get; set; }
        /// <summary>Gets or sets the y.</summary>
        [LuminPackOrder(1)] public int Y { get; set; }
        /// <summary>Gets or sets the z.</summary>
        [LuminPackOrder(2)] public int Z { get; set; }

        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        [LuminPackPoolRent]
        public static ref V1 Rent()
        {
            return ref Instance;
        }
    }

    /// <summary>Provides v2 functionality.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V2
    {
        /// <summary>Gets or sets the x.</summary>
        [LuminPackOrder(0)] public int X { get; set; }

        /// <summary>Gets or sets the z.</summary>
        [LuminPackOrder(2)] public int Z { get; set; }
    }

    /// <summary>Provides v3 with missing middle member functionality.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V3WithMissingMiddleMember
    {
        /// <summary>Gets or sets the x.</summary>
        [LuminPackOrder(0)] public int X { get; set; }
        /// <summary>Gets or sets the z.</summary>
        [LuminPackOrder(2)] public int Z { get; set; }
    }

    /// <summary>Provides v4 with added middle member functionality.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V4WithAddedMiddleMember
    {
        /// <summary>Gets or sets the x.</summary>
        [LuminPackOrder(0)] public int X { get; set; }
        /// <summary>Gets or sets the y.</summary>
        [LuminPackOrder(1)] public int Y { get; set; }
        /// <summary>Gets or sets the z.</summary>
        [LuminPackOrder(2)] public int Z { get; set; }
    }

    /// <summary>Verifies test version tolerant serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestVersionTolerantSerialization(List<string> output)
    {
        try
        {
            var v1 = new V1 { X = 42, Y = 43, Z = 44 };
            var buf = LuminPackSerializer.Serialize(v1);
            var v2 = LuminPackSerializer.Deserialize<V2>(buf);

            if (v1.X == v2.X && v2.Z == v1.Z)
                output.Add("✓ TestVersionTolerantSerialization - PASSED");
            else
                output.Add("✗ TestVersionTolerantSerialization - FAILED");
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestVersionTolerantSerialization - ERROR: {ex.Message}");
        }

        try
        {
            var oldValue = new V3WithMissingMiddleMember { X = 10, Z = 30 };
            var payload = LuminPackSerializer.Serialize(oldValue);
            var newValue = LuminPackSerializer.Deserialize<V4WithAddedMiddleMember>(payload);

            if (newValue.X == 10 && newValue.Y == 0 && newValue.Z == 30)
                output.Add("✓ TestVersionTolerantMissingMiddleMember - PASSED");
            else
                output.Add($"✗ TestVersionTolerantMissingMiddleMember - FAILED: X={newValue.X}, Y={newValue.Y}, Z={newValue.Z}, Payload={Convert.ToHexString(payload)}");
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestVersionTolerantMissingMiddleMember - ERROR: {ex.Message}");
        }

        try
        {
            var v1 = new V1 { X = 42, Y = 43, Z = 44 };
            var buf = LuminPackSerializer.SerializeJson(v1);
            var v2 = LuminPackSerializer.DeserializeJson<V2>(buf);

            if (v1.X == v2.X && v2.Z == v1.Z)
                output.Add("✓ TestVersionTolerantSerialization [JSON] - PASSED");
            else
                output.Add("✗ TestVersionTolerantSerialization [JSON] - FAILED");
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestVersionTolerantSerialization [JSON] - ERROR: {ex.Message}");
        }
    }
}
