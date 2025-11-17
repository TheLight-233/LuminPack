using LuminPack;
using LuminPack.Attribute;
using LuminPack.Enum;

namespace LuminPackUnitTest;

public class MultiDimensionalArraySerializationTest
{
    [LuminPackable]
    public class ArrayContainer
    {
        [LuminPackOrder(0)] public int[,]? TwoDimensionalArray { get; set; }
        [LuminPackOrder(1)] public int[,,]? ThreeDimensionalArray { get; set; }
        [LuminPackOrder(2)] public int[,,,]? FourDimensionalArray { get; set; }
        [LuminPackOrder(3)] public int[,,,,]? FiveDimensionalArray { get; set; }
        [LuminPackOrder(4)] public int[,,,,,]? SixDimensionalArray { get; set; }
        [LuminPackOrder(5)] public int[,,,,,,]? SevenDimensionalArray { get; set; }
    }

    public static void TestTwoDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                TwoDimensionalArray = new int[3, 4]
            };

            // 初始化二维数组
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    container.TwoDimensionalArray[i, j] = i * 10 + j;
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.TwoDimensionalArray != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (deserializedContainer.TwoDimensionalArray[i, j] != container.TwoDimensionalArray[i, j])
                        {
                            passed = false;
                            break;
                        }
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestTwoDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestTwoDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestTwoDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestThreeDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                ThreeDimensionalArray = new int[2, 3, 4]
            };

            // 初始化三维数组
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        container.ThreeDimensionalArray[i, j, k] = i * 100 + j * 10 + k;
                    }
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.ThreeDimensionalArray != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            if (deserializedContainer.ThreeDimensionalArray[i, j, k] != container.ThreeDimensionalArray[i, j, k])
                            {
                                passed = false;
                                break;
                            }
                        }
                        if (!passed) break;
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestThreeDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestThreeDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestThreeDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestFourDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                FourDimensionalArray = new int[2, 2, 3, 4]
            };

            // 初始化四维数组
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        for (int l = 0; l < 4; l++)
                        {
                            container.FourDimensionalArray[i, j, k, l] = i * 1000 + j * 100 + k * 10 + l;
                        }
                    }
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.FourDimensionalArray != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            for (int l = 0; l < 4; l++)
                            {
                                if (deserializedContainer.FourDimensionalArray[i, j, k, l] != container.FourDimensionalArray[i, j, k, l])
                                {
                                    passed = false;
                                    break;
                                }
                            }
                            if (!passed) break;
                        }
                        if (!passed) break;
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestFourDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestFourDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestFourDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestFiveDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                FiveDimensionalArray = new int[2, 2, 2, 3, 4]
            };

            // 初始化五维数组
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        for (int l = 0; l < 3; l++)
                        {
                            for (int m = 0; m < 4; m++)
                            {
                                container.FiveDimensionalArray[i, j, k, l, m] = i * 10000 + j * 1000 + k * 100 + l * 10 + m;
                            }
                        }
                    }
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.FiveDimensionalArray != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            for (int l = 0; l < 3; l++)
                            {
                                for (int m = 0; m < 4; m++)
                                {
                                    if (deserializedContainer.FiveDimensionalArray[i, j, k, l, m] != container.FiveDimensionalArray[i, j, k, l, m])
                                    {
                                        passed = false;
                                        break;
                                    }
                                }
                                if (!passed) break;
                            }
                            if (!passed) break;
                        }
                        if (!passed) break;
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestFiveDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestFiveDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestFiveDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestSixDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                SixDimensionalArray = new int[2, 2, 2, 2, 3, 4]
            };

            // 初始化六维数组
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        for (int l = 0; l < 2; l++)
                        {
                            for (int m = 0; m < 3; m++)
                            {
                                for (int n = 0; n < 4; n++)
                                {
                                    container.SixDimensionalArray[i, j, k, l, m, n] = i * 100000 + j * 10000 + k * 1000 + l * 100 + m * 10 + n;
                                }
                            }
                        }
                    }
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.SixDimensionalArray != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            for (int l = 0; l < 2; l++)
                            {
                                for (int m = 0; m < 3; m++)
                                {
                                    for (int n = 0; n < 4; n++)
                                    {
                                        if (deserializedContainer.SixDimensionalArray[i, j, k, l, m, n] != container.SixDimensionalArray[i, j, k, l, m, n])
                                        {
                                            passed = false;
                                            break;
                                        }
                                    }
                                    if (!passed) break;
                                }
                                if (!passed) break;
                            }
                            if (!passed) break;
                        }
                        if (!passed) break;
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestSixDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestSixDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSixDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestSevenDimensionalArray(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                SevenDimensionalArray = new int[2, 2, 2, 2, 2, 3, 4]
            };

            // 初始化七维数组
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        for (int l = 0; l < 2; l++)
                        {
                            for (int m = 0; m < 2; m++)
                            {
                                for (int n = 0; n < 3; n++)
                                {
                                    for (int o = 0; o < 4; o++)
                                    {
                                        container.SevenDimensionalArray[i, j, k, l, m, n, o] = i * 1000000 + j * 100000 + k * 10000 + l * 1000 + m * 100 + n * 10 + o;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = true;
            if (deserializedContainer?.SevenDimensionalArray != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            for (int l = 0; l < 2; l++)
                            {
                                for (int m = 0; m < 2; m++)
                                {
                                    for (int n = 0; n < 3; n++)
                                    {
                                        for (int o = 0; o < 4; o++)
                                        {
                                            if (deserializedContainer.SevenDimensionalArray[i, j, k, l, m, n, o] != container.SevenDimensionalArray[i, j, k, l, m, n, o])
                                            {
                                                passed = false;
                                                break;
                                            }
                                        }
                                        if (!passed) break;
                                    }
                                    if (!passed) break;
                                }
                                if (!passed) break;
                            }
                            if (!passed) break;
                        }
                        if (!passed) break;
                    }
                    if (!passed) break;
                }
            }
            else
            {
                passed = false;
            }

            if (passed)
            {
                output.Add("✓ TestSevenDimensionalArray - PASSED");
            }
            else
            {
                output.Add("✗ TestSevenDimensionalArray - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestSevenDimensionalArray - ERROR: {ex.Message}");
        }
    }

    public static void TestAllArraysInOneContainer(List<string> output)
    {
        try
        {
            var container = new ArrayContainer
            {
                TwoDimensionalArray = new int[2, 2] { { 1, 2 }, { 3, 4 } },
                ThreeDimensionalArray = new int[2, 2, 2] { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } },
                FourDimensionalArray = new int[2, 2, 2, 2] 
                { 
                    { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } }, 
                    { { { 9, 10 }, { 11, 12 } }, { { 13, 14 }, { 15, 16 } } } 
                }
            };

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedContainer = LuminPackSerializer.Deserialize<ArrayContainer>(buf);

            bool passed = deserializedContainer?.TwoDimensionalArray != null &&
                         deserializedContainer.ThreeDimensionalArray != null &&
                         deserializedContainer.FourDimensionalArray != null &&
                         deserializedContainer.TwoDimensionalArray[0, 0] == 1 &&
                         deserializedContainer.TwoDimensionalArray[1, 1] == 4 &&
                         deserializedContainer.ThreeDimensionalArray[0, 0, 0] == 1 &&
                         deserializedContainer.ThreeDimensionalArray[1, 1, 1] == 8;

            if (passed)
            {
                output.Add("✓ TestAllArraysInOneContainer - PASSED");
            }
            else
            {
                output.Add("✗ TestAllArraysInOneContainer - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestAllArraysInOneContainer - ERROR: {ex.Message}");
        }
    }
    
    public static void TestLargeTwoDimensionalArray(List<string> output)
    {
        try
        {
            // 2000个元素，超过1000阈值，触发多线程
            string[,] twoDimensionalArray = new string[100, 20];

            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    twoDimensionalArray[i, j] = i * 1000 + j.ToString();
                }
            }

            var buf = LuminPackSerializer.Serialize(twoDimensionalArray);
            var deserializedContainer = LuminPackSerializer.Deserialize<string[,]>(buf);

            bool passed = VerifyTwoDimensionalArray(deserializedContainer, twoDimensionalArray);
        
            output.Add(passed 
                ? "✓ TestLargeTwoDimensionalArray - PASSED (multithreaded)" 
                : "✗ TestLargeTwoDimensionalArray - FAILED");
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestLargeTwoDimensionalArray - ERROR: {ex.Message}");
        }
    }
    
    private static bool VerifyTwoDimensionalArray(string[,]? actual, string[,]? expected)
    {
        if (actual == null || expected == null) return false;
        if (actual.GetLength(0) != expected.GetLength(0) || actual.GetLength(1) != expected.GetLength(1)) 
            return false;
    
        for (int i = 0; i < expected.GetLength(0); i++)
        {
            for (int j = 0; j < expected.GetLength(1); j++)
            {
                if (actual[i, j] != expected[i, j]) return false;
            }
        }
        return true;
    }

    public static void RunAllTests(List<string> output)
    {
        TestTwoDimensionalArray(output);
        TestThreeDimensionalArray(output);
        TestFourDimensionalArray(output);
        TestFiveDimensionalArray(output);
        TestSixDimensionalArray(output);
        TestSevenDimensionalArray(output);
        TestAllArraysInOneContainer(output);
    }
}