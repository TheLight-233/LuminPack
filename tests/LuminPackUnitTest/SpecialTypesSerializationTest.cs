using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using LuminPack;

namespace LuminPackUnitTest
{
    public class SpecialTypesSerializationTest
    {
        // Uri Tests
        public static void TestUri(List<string> results)
        {
            try
            {
                Uri val = new Uri("https://example.com/path?query=value");
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Uri result = LuminPackSerializer.Deserialize<Uri>(buf);
                
                if (val.ToString() == result.ToString())
                    results.Add("✓ TestUri - PASSED");
                else
                    results.Add("✗ TestUri - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUri - ERROR: {ex.Message}");
            }
        }

        // Version Tests
        public static void TestVersion(List<string> results)
        {
            try
            {
                Version val = new Version(1, 2, 3, 4);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Version result = LuminPackSerializer.Deserialize<Version>(buf);
                
                if (val.Equals(result))
                    results.Add("✓ TestVersion - PASSED");
                else
                    results.Add("✗ TestVersion - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestVersion - ERROR: {ex.Message}");
            }
        }

        // BigInteger Tests
        public static void TestBigInteger(List<string> results)
        {
            try
            {
                BigInteger val = BigInteger.Parse("123456789012345678901234567890");
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                BigInteger result = LuminPackSerializer.Deserialize<BigInteger>(buf);
                
                if (val == result)
                    results.Add("✓ TestBigInteger - PASSED");
                else
                    results.Add("✗ TestBigInteger - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBigInteger - ERROR: {ex.Message}");
            }
        }

        // BitArray Tests
        public static void TestBitArray(List<string> results)
        {
            try
            {
                int[] lengths = { 0, 1, 5, 31, 32, 33, 1000, 100_000 };

                foreach (int length in lengths)
                {
                    BitArray val = new BitArray(length);
                    for (int i = 0; i < length; i += 7)
                    {
                        val[i] = true;
                    }

                    byte[] buf = LuminPackSerializer.Serialize(val);
                    BitArray result = LuminPackSerializer.Deserialize<BitArray>(buf);

                    if (result.Count != val.Count)
                    {
                        results.Add($"✗ TestBitArray - FAILED (length {length}, count mismatch)");
                        return;
                    }

                    for (int i = 0; i < val.Count; i++)
                    {
                        if (val[i] != result[i])
                        {
                            results.Add($"✗ TestBitArray - FAILED (length {length}, bit {i} mismatch)");
                            return;
                        }
                    }
                }

                results.Add("✓ TestBitArray - PASSED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBitArray - ERROR: {ex.Message}");
            }
        }

        public static void TestBitArrayCodeGen(List<string> results)
        {
            try
            {
                var bits = new BitArray(100_000);
                for (int i = 0; i < bits.Count; i += 7)
                {
                    bits[i] = true;
                }

                var value = new BitArrayCodeGenModel { Bits = bits };
                byte[] buf = LuminPackSerializer.Serialize(value);
                BitArrayCodeGenModel result = LuminPackSerializer.Deserialize<BitArrayCodeGenModel>(buf);

                if (result.Bits.Count != bits.Count)
                {
                    results.Add("✗ TestBitArrayCodeGen - FAILED (count mismatch)");
                    return;
                }

                for (int i = 0; i < bits.Count; i++)
                {
                    if (bits[i] != result.Bits[i])
                    {
                        results.Add($"✗ TestBitArrayCodeGen - FAILED (bit {i} mismatch)");
                        return;
                    }
                }

                results.Add("✓ TestBitArrayCodeGen - PASSED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBitArrayCodeGen - ERROR: {ex.Message}");
            }
        }

        // CultureInfo Tests
        public static void TestCultureInfo(List<string> results)
        {
            try
            {
                CultureInfo val = CultureInfo.GetCultureInfo("en-US");
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                CultureInfo result = LuminPackSerializer.Deserialize<CultureInfo>(buf);
                
                if (val.Name == result.Name)
                    results.Add("✓ TestCultureInfo - PASSED");
                else
                    results.Add("✗ TestCultureInfo - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestCultureInfo - ERROR: {ex.Message}");
            }
        }

        // TimeZoneInfo Tests
        public static void TestTimeZoneInfo(List<string> results)
        {
            try
            {
                TimeZoneInfo val = TimeZoneInfo.Local;
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                TimeZoneInfo result = LuminPackSerializer.Deserialize<TimeZoneInfo>(buf);
                
                if (val.Id == result.Id)
                    results.Add("✓ TestTimeZoneInfo - PASSED");
                else
                    results.Add("✗ TestTimeZoneInfo - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestTimeZoneInfo - ERROR: {ex.Message}");
            }
        }

        // Type Tests
        public static void TestType(List<string> results)
        {
            try
            {
                Type val = typeof(string);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Type result = LuminPackSerializer.Deserialize<Type>(buf);
                
                if (val == result)
                    results.Add("✓ TestType - PASSED");
                else
                    results.Add("✗ TestType - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestType - ERROR: {ex.Message}");
            }
        }

        // StringBuilder Tests
        public static void TestStringBuilder(List<string> results)
        {
            try
            {
                StringBuilder val = new StringBuilder("Hello World");
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                StringBuilder result = LuminPackSerializer.Deserialize<StringBuilder>(buf);
                
                if (val.ToString() == result.ToString())
                    results.Add("✓ TestStringBuilder - PASSED");
                else
                    results.Add("✗ TestStringBuilder - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringBuilder - ERROR: {ex.Message}");
            }
        }

        // Lazy Tests
        public static void TestLazy(List<string> results)
        {
            try
            {
                Lazy<int> val = new Lazy<int>(() => 42);
                _ = val.Value; // Force evaluation
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Lazy<int> result = LuminPackSerializer.Deserialize<Lazy<int>>(buf);
                
                if (val.Value == result.Value && result.Value == 42)
                    results.Add("✓ TestLazy - PASSED");
                else
                    results.Add("✗ TestLazy - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestLazy - ERROR: {ex.Message}");
            }
        }

        // Tuple Tests
        public static void TestTuple(List<string> results)
        {
            try
            {
                Tuple<int, string, bool> val = Tuple.Create(42, "Hello", true);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Tuple<int, string, bool> result = LuminPackSerializer.Deserialize<Tuple<int, string, bool>>(buf);
                
                if (val.Item1 == result.Item1 && val.Item2 == result.Item2 && val.Item3 == result.Item3)
                    results.Add("✓ TestTuple - PASSED");
                else
                    results.Add("✗ TestTuple - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestTuple - ERROR: {ex.Message}");
            }
        }

        // ValueTuple Tests
        public static void TestValueTuple(List<string> results)
        {
            try
            {
                ValueTuple<int, string, bool> val = (42, "Hello", true);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                ValueTuple<int, string, bool> result = LuminPackSerializer.Deserialize<ValueTuple<int, string, bool>>(buf);
                
                if (val.Item1 == result.Item1 && val.Item2 == result.Item2 && val.Item3 == result.Item3)
                    results.Add("✓ TestValueTuple - PASSED");
                else
                    results.Add("✗ TestValueTuple - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestValueTuple - ERROR: {ex.Message}");
            }
        }

        // System.Numerics Types Tests
        public static void TestComplex(List<string> results)
        {
            try
            {
                Complex val = new Complex(3.0, 4.0);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Complex result = LuminPackSerializer.Deserialize<Complex>(buf);
                
                if (val.Real == result.Real && val.Imaginary == result.Imaginary)
                    results.Add("✓ TestComplex - PASSED");
                else
                    results.Add("✗ TestComplex - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestComplex - ERROR: {ex.Message}");
            }
        }

        public static void TestVector2(List<string> results)
        {
            try
            {
                Vector2 val = new Vector2(1.5f, 2.5f);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Vector2 result = LuminPackSerializer.Deserialize<Vector2>(buf);
                
                if (val == result)
                    results.Add("✓ TestVector2 - PASSED");
                else
                    results.Add("✗ TestVector2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestVector2 - ERROR: {ex.Message}");
            }
        }

        public static void TestVector3(List<string> results)
        {
            try
            {
                Vector3 val = new Vector3(1.5f, 2.5f, 3.5f);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Vector3 result = LuminPackSerializer.Deserialize<Vector3>(buf);
                
                if (val == result)
                    results.Add("✓ TestVector3 - PASSED");
                else
                    results.Add("✗ TestVector3 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestVector3 - ERROR: {ex.Message}");
            }
        }

        public static void TestVector4(List<string> results)
        {
            try
            {
                Vector4 val = new Vector4(1.5f, 2.5f, 3.5f, 4.5f);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Vector4 result = LuminPackSerializer.Deserialize<Vector4>(buf);
                
                if (val == result)
                    results.Add("✓ TestVector4 - PASSED");
                else
                    results.Add("✗ TestVector4 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestVector4 - ERROR: {ex.Message}");
            }
        }

        public static void TestQuaternion(List<string> results)
        {
            try
            {
                Quaternion val = new Quaternion(1.0f, 2.0f, 3.0f, 4.0f);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Quaternion result = LuminPackSerializer.Deserialize<Quaternion>(buf);
                
                if (val == result)
                    results.Add("✓ TestQuaternion - PASSED");
                else
                    results.Add("✗ TestQuaternion - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestQuaternion - ERROR: {ex.Message}");
            }
        }

        public static void TestMatrix3x2(List<string> results)
        {
            try
            {
                Matrix3x2 val = new Matrix3x2(1, 2, 3, 4, 5, 6);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Matrix3x2 result = LuminPackSerializer.Deserialize<Matrix3x2>(buf);
                
                if (val == result)
                    results.Add("✓ TestMatrix3x2 - PASSED");
                else
                    results.Add("✗ TestMatrix3x2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestMatrix3x2 - ERROR: {ex.Message}");
            }
        }

        public static void TestMatrix4x4(List<string> results)
        {
            try
            {
                Matrix4x4 val = Matrix4x4.Identity;
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Matrix4x4 result = LuminPackSerializer.Deserialize<Matrix4x4>(buf);
                
                if (val == result)
                    results.Add("✓ TestMatrix4x4 - PASSED");
                else
                    results.Add("✗ TestMatrix4x4 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestMatrix4x4 - ERROR: {ex.Message}");
            }
        }

        public static void TestPlane(List<string> results)
        {
            try
            {
                Plane val = new Plane(1.0f, 2.0f, 3.0f, 4.0f);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Plane result = LuminPackSerializer.Deserialize<Plane>(buf);
                
                if (val == result)
                    results.Add("✓ TestPlane - PASSED");
                else
                    results.Add("✗ TestPlane - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestPlane - ERROR: {ex.Message}");
            }
        }

#if NET8_0_OR_GREATER
        // .NET 8+ Types
        public static void TestHalf(List<string> results)
        {
            try
            {
                Half val = (Half)3.14;
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Half result = LuminPackSerializer.Deserialize<Half>(buf);
                
                if (val == result)
                    results.Add("✓ TestHalf - PASSED");
                else
                    results.Add("✗ TestHalf - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestHalf - ERROR: {ex.Message}");
            }
        }

        public static void TestInt128(List<string> results)
        {
            try
            {
                Int128 val = Int128.MaxValue;
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                Int128 result = LuminPackSerializer.Deserialize<Int128>(buf);
                
                if (val == result)
                    results.Add("✓ TestInt128 - PASSED");
                else
                    results.Add("✗ TestInt128 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestInt128 - ERROR: {ex.Message}");
            }
        }

        public static void TestUInt128(List<string> results)
        {
            try
            {
                UInt128 val = UInt128.MaxValue;
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                UInt128 result = LuminPackSerializer.Deserialize<UInt128>(buf);
                
                if (val == result)
                    results.Add("✓ TestUInt128 - PASSED");
                else
                    results.Add("✗ TestUInt128 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUInt128 - ERROR: {ex.Message}");
            }
        }

        public static void TestDateOnly(List<string> results)
        {
            try
            {
                DateOnly val = DateOnly.FromDateTime(DateTime.Today);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                DateOnly result = LuminPackSerializer.Deserialize<DateOnly>(buf);
                
                if (val == result)
                    results.Add("✓ TestDateOnly - PASSED");
                else
                    results.Add("✗ TestDateOnly - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDateOnly - ERROR: {ex.Message}");
            }
        }

        public static void TestTimeOnly(List<string> results)
        {
            try
            {
                TimeOnly val = TimeOnly.FromDateTime(DateTime.Now);
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                TimeOnly result = LuminPackSerializer.Deserialize<TimeOnly>(buf);
                
                if (val == result)
                    results.Add("✓ TestTimeOnly - PASSED");
                else
                    results.Add("✗ TestTimeOnly - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestTimeOnly - ERROR: {ex.Message}");
            }
        }

        public static void TestRune(List<string> results)
        {
            try
            {
                System.Text.Rune val = new System.Text.Rune('A');
                
                // Binary test
                byte[] buf = LuminPackSerializer.Serialize(val);
                System.Text.Rune result = LuminPackSerializer.Deserialize<System.Text.Rune>(buf);
                
                if (val == result)
                    results.Add("✓ TestRune - PASSED");
                else
                    results.Add("✗ TestRune - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestRune - ERROR: {ex.Message}");
            }
        }
#endif
    }
}
