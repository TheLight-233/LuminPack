using LuminPack;
using LuminPack.Attribute;
using LuminPack.Enum;

namespace LuminPackUnitTest;

public class VersionTolerantSerializationTest
{
    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V1
    {
        public static V1 Instance;
        [LuminPackOrder(0)] public int X { get; set; }
        [LuminPackOrder(1)] public int Y { get; set; }
        [LuminPackOrder(2)] public int Z { get; set; }

        [LuminPackPoolRent]
        public static ref V1 Rent()
        {
            return ref Instance;
        }
    }

    [LuminPackable(GeneratorType.VersionTolerant)]
    public class V2
    {
        [LuminPackOrder(0)] public int X { get; set; }
        
        [LuminPackOrder(2)] public int Z { get; set; }
    }

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