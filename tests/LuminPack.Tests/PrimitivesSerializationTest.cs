using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackUnitTest
{
    /// <summary>Provides test enum val functionality.</summary>
    public enum TestEnumVal : short
    {
        /// <summary>Specifies a.</summary>
        A = Int16.MinValue,
        /// <summary>Specifies b.</summary>
        B = Int16.MaxValue,
    }

    /// <summary>Provides empty struct functionality.</summary>
    [LuminPackable]
    public struct EmptyStruct
    {
    }

    /// <summary>Provides primitives serialization test functionality.</summary>
    public class PrimitivesSerializationTest
    {

        /// <summary>Verifies test empty struct.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestEmptyStruct(List<string> results)
        {
            try
            {
                var val = new EmptyStruct();
                byte[] buf = LuminPackSerializer.Serialize(val);
                EmptyStruct val2 = LuminPackSerializer.Deserialize<EmptyStruct>(buf);

                // For empty structs, we just check if deserialization succeeds
                results.Add("✓ TestEmptyStruct - PASSED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestEmptyStruct - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test enum.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestEnum(List<string> results)
        {
            try
            {
                TestEnumVal val = TestEnumVal.B;
                byte[] buf = LuminPackSerializer.Serialize(val);
                TestEnumVal val2 = LuminPackSerializer.Deserialize<TestEnumVal>(buf);

                if (val == val2 && val2 == TestEnumVal.B)
                    results.Add("✓ TestEnum - PASSED");
                else
                    results.Add("✗ TestEnum - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestEnum - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestString(List<string> results)
        {
            try
            {
                string val = "test";
                byte[] buf = LuminPackSerializer.Serialize(val);
                string result = LuminPackSerializer.Deserialize<string>(buf);

                if (val == result && result == "test")
                    results.Add("✓ TestString - PASSED");
                else
                    results.Add("✗ TestString - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestString - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string null and empty.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringNullAndEmpty(List<string> results)
        {
            try
            {
                string? empty = "";
                string? nullValue = null;

                foreach (var option in new[]
                         {
                             new TestSerializationProfile { StringRecording = LuminPackStringRecording.Length },
                             new TestSerializationProfile { StringRecording = LuminPackStringRecording.Length, StringEncoding = LuminPackStringEncoding.UTF16 },
                         })
                {
                    string? emptyBack = LuminPackSerializer.Deserialize<string>(LuminPackSerializer.Serialize(empty, option), option);
                    string? nullBack = LuminPackSerializer.Deserialize<string>(LuminPackSerializer.Serialize(nullValue, option), option);
                    if (emptyBack != "" || nullBack != null)
                    {
                        results.Add($"✗ TestStringNullAndEmpty - FAILED: empty='{emptyBack}' null={nullBack}");
                        return;
                    }
                }

                results.Add("✓ TestStringNullAndEmpty - PASSED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringNullAndEmpty - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string utf8 token.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringUtf8Token(List<string> results)
        {
            try
            {
                string val = "\u55E8\u55E8Test\uFF01";
                byte[] buf = LuminPackSerializer.Serialize(val, new TestSerializationProfile()
                {
                    StringEncoding = LuminPackStringEncoding.UTF8,
                    StringRecording = LuminPackStringRecording.Token
                });
                string result = LuminPackSerializer.Deserialize<string>(buf, new TestSerializationProfile()
                {
                    StringEncoding = LuminPackStringEncoding.UTF8,
                    StringRecording = LuminPackStringRecording.Token
                });

                if (val == result && result == "\u55E8\u55E8Test\uFF01")
                    results.Add("✓ TestStringUtf8Token - PASSED");
                else
                    results.Add("✗ TestStringUtf8Token - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringUtf8Token - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string utf16.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringUtf16(List<string> results)
        {
            try
            {
                string val = "\u55E8\u55E8Test\uFF01";
                byte[] buf = LuminPackSerializer.Serialize(val, new TestSerializationProfile()
                {
                    StringEncoding = LuminPackStringEncoding.UTF16,
                    StringRecording = LuminPackStringRecording.Length
                });
                string result = LuminPackSerializer.Deserialize<string>(buf, new TestSerializationProfile()
                {
                    StringEncoding = LuminPackStringEncoding.UTF16,
                    StringRecording = LuminPackStringRecording.Length
                });

                if (val == result && result == "\u55E8\u55E8Test\uFF01")
                    results.Add("✓ TestStringUtf16 - PASSED");
                else
                    results.Add("✗ TestStringUtf16 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringUtf16 - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test enum arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestEnumArr(List<string> results)
        {
            try
            {
                TestEnumVal[] val = new[] { TestEnumVal.A, TestEnumVal.B };
                byte[] buf = LuminPackSerializer.Serialize(val);
                TestEnumVal[] result = LuminPackSerializer.Deserialize<TestEnumVal[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { TestEnumVal.A, TestEnumVal.B }))
                    results.Add("✓ TestEnumArr - PASSED");
                else
                    results.Add("✗ TestEnumArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestEnumArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test byte arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestByteArr(List<string> results)
        {
            try
            {
                byte[] val = new byte[] { 1, 2, 3, 4, 5 };
                byte[] buf = LuminPackSerializer.Serialize(val);
                byte[] result = LuminPackSerializer.Deserialize<byte[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }))
                    results.Add("✓ TestByteArr - PASSED");
                else
                    results.Add("✗ TestByteArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestByteArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test s byte arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestSByteArr(List<string> results)
        {
            try
            {
                sbyte[] val = new sbyte[] { -1, -2, -3, -4, -5 };
                byte[] buf = LuminPackSerializer.Serialize(val);
                sbyte[] result = LuminPackSerializer.Deserialize<sbyte[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new sbyte[] { -1, -2, -3, -4, -5 }))
                    results.Add("✓ TestSByteArr - PASSED");
                else
                    results.Add("✗ TestSByteArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSByteArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test short arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestShortArr(List<string> results)
        {
            try
            {
                short[] val = new[] { short.MinValue, short.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                short[] result = LuminPackSerializer.Deserialize<short[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { short.MinValue, short.MaxValue }))
                    results.Add("✓ TestShortArr - PASSED");
                else
                    results.Add("✗ TestShortArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestShortArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u short arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestUShortArr(List<string> results)
        {
            try
            {
                ushort[] val = new[] { ushort.MinValue, ushort.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                ushort[] result = LuminPackSerializer.Deserialize<ushort[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { ushort.MinValue, ushort.MaxValue }))
                    results.Add("✓ TestUShortArr - PASSED");
                else
                    results.Add("✗ TestUShortArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUShortArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test int arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestIntArr(List<string> results)
        {
            try
            {
                int[] val = new[] { int.MinValue, int.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                int[] result = LuminPackSerializer.Deserialize<int[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { int.MinValue, int.MaxValue }))
                    results.Add("✓ TestIntArr - PASSED");
                else
                    results.Add("✗ TestIntArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIntArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u int arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestUIntArr(List<string> results)
        {
            try
            {
                uint[] val = new[] { uint.MinValue, uint.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                uint[] result = LuminPackSerializer.Deserialize<uint[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { uint.MinValue, uint.MaxValue }))
                    results.Add("✓ TestUIntArr - PASSED");
                else
                    results.Add("✗ TestUIntArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUIntArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test long arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestLongArr(List<string> results)
        {
            try
            {
                long[] val = new[] { long.MinValue, long.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                long[] result = LuminPackSerializer.Deserialize<long[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { long.MinValue, long.MaxValue }))
                    results.Add("✓ TestLongArr - PASSED");
                else
                    results.Add("✗ TestLongArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestLongArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u long arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestULongArr(List<string> results)
        {
            try
            {
                ulong[] val = new[] { ulong.MinValue, ulong.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                ulong[] result = LuminPackSerializer.Deserialize<ulong[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { ulong.MinValue, ulong.MaxValue }))
                    results.Add("✓ TestULongArr - PASSED");
                else
                    results.Add("✗ TestULongArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestULongArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringArr(List<string> results)
        {
            try
            {
                string[] val = new[] { "a", "b", "c", "d", "e" };
                byte[] buf = LuminPackSerializer.Serialize(val);
                string[] result = LuminPackSerializer.Deserialize<string[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { "a", "b", "c", "d", "e" }))
                    results.Add("✓ TestStringArr - PASSED");
                else
                    results.Add("✗ TestStringArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test bool arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestBoolArr(List<string> results)
        {
            try
            {
                bool[] val = new[] { true, false };
                byte[] buf = LuminPackSerializer.Serialize(val);
                bool[] result = LuminPackSerializer.Deserialize<bool[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { true, false }))
                    results.Add("✓ TestBoolArr - PASSED");
                else
                    results.Add("✗ TestBoolArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBoolArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test float arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestFloatArr(List<string> results)
        {
            try
            {
                float[] val = new[] { float.MinValue, float.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                float[] result = LuminPackSerializer.Deserialize<float[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { float.MinValue, float.MaxValue }))
                    results.Add("✓ TestFloatArr - PASSED");
                else
                    results.Add("✗ TestFloatArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestFloatArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test double arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestDoubleArr(List<string> results)
        {
            try
            {
                double[] val = new[] { double.MinValue, double.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                double[] result = LuminPackSerializer.Deserialize<double[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { double.MinValue, double.MaxValue }))
                    results.Add("✓ TestDoubleArr - PASSED");
                else
                    results.Add("✗ TestDoubleArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDoubleArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test char arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestCharArr(List<string> results)
        {
            try
            {
                char[] val = new[] { 'a', 'b', 'c', 'd', 'e' };
                byte[] buf = LuminPackSerializer.Serialize(val);
                char[] result = LuminPackSerializer.Deserialize<char[]>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new[] { 'a', 'b', 'c', 'd', 'e' }))
                    results.Add("✓ TestCharArr - PASSED");
                else
                    results.Add("✗ TestCharArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestCharArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test date time arr.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestDateTimeArr(List<string> results)
        {
            try
            {
                DateTime[] val = new[] { DateTime.Today, DateTime.Today.AddDays(-1234) };
                byte[] buf = LuminPackSerializer.Serialize(val);
                DateTime[] result = LuminPackSerializer.Deserialize<DateTime[]>(buf);

                if (val.SequenceEqual(result))
                    results.Add("✓ TestDateTimeArr - PASSED");
                else
                    results.Add("✗ TestDateTimeArr - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDateTimeArr - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test enum list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestEnumList(List<string> results)
        {
            try
            {
                List<TestEnumVal> val = new List<TestEnumVal>() { TestEnumVal.A, TestEnumVal.B };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<TestEnumVal> result = LuminPackSerializer.Deserialize<List<TestEnumVal>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<TestEnumVal>() { TestEnumVal.A, TestEnumVal.B }))
                    results.Add("✓ TestEnumList - PASSED");
                else
                    results.Add("✗ TestEnumList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestEnumList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test byte list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestByteList(List<string> results)
        {
            try
            {
                List<byte> val = new List<byte> { byte.MinValue, byte.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<byte> result = LuminPackSerializer.Deserialize<List<byte>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<byte> { byte.MinValue, byte.MaxValue }))
                    results.Add("✓ TestByteList - PASSED");
                else
                    results.Add("✗ TestByteList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestByteList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test s byte list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestSByteList(List<string> results)
        {
            try
            {
                List<sbyte> val = new List<sbyte> { sbyte.MinValue, sbyte.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<sbyte> result = LuminPackSerializer.Deserialize<List<sbyte>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<sbyte> { sbyte.MinValue, sbyte.MaxValue }))
                    results.Add("✓ TestSByteList - PASSED");
                else
                    results.Add("✗ TestSByteList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSByteList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test short list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestShortList(List<string> results)
        {
            try
            {
                List<short> val = new List<short> { short.MinValue, short.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<short> result = LuminPackSerializer.Deserialize<List<short>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<short> { short.MinValue, short.MaxValue }))
                    results.Add("✓ TestShortList - PASSED");
                else
                    results.Add("✗ TestShortList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestShortList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u short list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestUShortList(List<string> results)
        {
            try
            {
                List<ushort> val = new List<ushort> { ushort.MinValue, ushort.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<ushort> result = LuminPackSerializer.Deserialize<List<ushort>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<ushort> { ushort.MinValue, ushort.MaxValue }))
                    results.Add("✓ TestUShortList - PASSED");
                else
                    results.Add("✗ TestUShortList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUShortList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test int list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestIntList(List<string> results)
        {
            try
            {
                List<int> val = new List<int> { int.MinValue, int.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<int> result = LuminPackSerializer.Deserialize<List<int>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<int> { int.MinValue, int.MaxValue }))
                    results.Add("✓ TestIntList - PASSED");
                else
                    results.Add("✗ TestIntList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestIntList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u int list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestUIntList(List<string> results)
        {
            try
            {
                List<uint> val = new List<uint> { uint.MinValue, uint.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<uint> result = LuminPackSerializer.Deserialize<List<uint>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<uint> { uint.MinValue, uint.MaxValue }))
                    results.Add("✓ TestUIntList - PASSED");
                else
                    results.Add("✗ TestUIntList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUIntList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test long list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestLongList(List<string> results)
        {
            try
            {
                List<long> val = new List<long> { long.MinValue, long.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<long> result = LuminPackSerializer.Deserialize<List<long>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<long> { long.MinValue, long.MaxValue }))
                    results.Add("✓ TestLongList - PASSED");
                else
                    results.Add("✗ TestLongList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestLongList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test u long list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestULongList(List<string> results)
        {
            try
            {
                List<ulong> val = new List<ulong> { ulong.MinValue, ulong.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<ulong> result = LuminPackSerializer.Deserialize<List<ulong>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<ulong> { ulong.MinValue, ulong.MaxValue }))
                    results.Add("✓ TestULongList - PASSED");
                else
                    results.Add("✗ TestULongList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestULongList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test float list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestFloatList(List<string> results)
        {
            try
            {
                List<float> val = new List<float> { float.MinValue, float.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<float> result = LuminPackSerializer.Deserialize<List<float>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<float> { float.MinValue, float.MaxValue }))
                    results.Add("✓ TestFloatList - PASSED");
                else
                    results.Add("✗ TestFloatList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestFloatList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test double list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestDoubleList(List<string> results)
        {
            try
            {
                List<double> val = new List<double> { double.MinValue, double.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<double> result = LuminPackSerializer.Deserialize<List<double>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<double> { double.MinValue, double.MaxValue }))
                    results.Add("✓ TestDoubleList - PASSED");
                else
                    results.Add("✗ TestDoubleList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDoubleList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test decimal list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestDecimalList(List<string> results)
        {
            try
            {
                List<decimal> val = new List<decimal> { decimal.MinValue, decimal.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<decimal> result = LuminPackSerializer.Deserialize<List<decimal>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<decimal> { decimal.MinValue, decimal.MaxValue }))
                    results.Add("✓ TestDecimalList - PASSED");
                else
                    results.Add("✗ TestDecimalList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDecimalList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test char list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestCharList(List<string> results)
        {
            try
            {
                List<char> val = new List<char> { char.MinValue, char.MaxValue };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<char> result = LuminPackSerializer.Deserialize<List<char>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<char> { char.MinValue, char.MaxValue }))
                    results.Add("✓ TestCharList - PASSED");
                else
                    results.Add("✗ TestCharList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestCharList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringList(List<string> results)
        {
            try
            {
                List<string> val = new List<string> { "Hello", "World" };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<string> result = LuminPackSerializer.Deserialize<List<string>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<string> { "Hello", "World" }))
                    results.Add("✓ TestStringList - PASSED");
                else
                    results.Add("✗ TestStringList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test bool list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestBoolList(List<string> results)
        {
            try
            {
                List<bool> val = new List<bool> { true, false };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<bool> result = LuminPackSerializer.Deserialize<List<bool>>(buf);

                if (val.SequenceEqual(result) && result.SequenceEqual(new List<bool> { true, false }))
                    results.Add("✓ TestBoolList - PASSED");
                else
                    results.Add("✗ TestBoolList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBoolList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test date time list.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestDateTimeList(List<string> results)
        {
            try
            {
                List<DateTime> val = new List<DateTime> { DateTime.Today, DateTime.Today.AddDays(-1234) };
                byte[] buf = LuminPackSerializer.Serialize(val);
                List<DateTime> result = LuminPackSerializer.Deserialize<List<DateTime>>(buf);

                if (val.SequenceEqual(result))
                    results.Add("✓ TestDateTimeList - PASSED");
                else
                    results.Add("✗ TestDateTimeList - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDateTimeList - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test byte int dict.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestByteIntDict(List<string> results)
        {
            try
            {
                Dictionary<byte, int> val = new Dictionary<byte, int>
                    { { byte.MinValue, int.MinValue }, { byte.MaxValue, int.MaxValue } };
                byte[] buf = LuminPackSerializer.Serialize(val);
                Dictionary<byte, int> result = LuminPackSerializer.Deserialize<Dictionary<byte, int>>(buf);

                if (val[byte.MinValue] == result[byte.MinValue] && val[byte.MaxValue] == result[byte.MaxValue])
                    results.Add("✓ TestByteIntDict - PASSED");
                else
                    results.Add("✗ TestByteIntDict - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestByteIntDict - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string short dict.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringShortDict(List<string> results)
        {
            try
            {
                Dictionary<string, short> val = new Dictionary<string, short>
                    { { "Hello", short.MinValue }, { "World", short.MaxValue } };
                byte[] buf = LuminPackSerializer.Serialize(val);
                Dictionary<string, short> result = LuminPackSerializer.Deserialize<Dictionary<string, short>>(buf);

                if (val.SequenceEqual(result))
                    results.Add("✓ TestStringShortDict - PASSED");
                else
                    results.Add("✗ TestStringShortDict - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringShortDict - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test string string dict.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestStringStringDict(List<string> results)
        {
            try
            {
                Dictionary<string, string> val = new Dictionary<string, string>
                    { { "Hello", "World" }, { "World", "Hello" } };
                byte[] buf = LuminPackSerializer.Serialize(val);
                Dictionary<string, string> result = LuminPackSerializer.Deserialize<Dictionary<string, string>>(buf);

                if (val.SequenceEqual(result))
                    results.Add("✓ TestStringStringDict - PASSED");
                else
                    results.Add("✗ TestStringStringDict - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringStringDict - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test nullable.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestNullable(List<string> results)
        {
            try
            {
                int? val = 123;
                byte[] buf = LuminPackSerializer.Serialize(val);
                int? result = LuminPackSerializer.Deserialize<int?>(buf);

                if (val == result)
                {
                    val = null;
                    buf = LuminPackSerializer.Serialize(val);
                    result = LuminPackSerializer.Deserialize<int?>(buf);

                    if (val == result)
                        results.Add("✓ TestNullable - PASSED");
                    else
                        results.Add("✗ TestNullable - FAILED (null case)");
                }
                else
                {
                    results.Add("✗ TestNullable - FAILED (value case)");
                }
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestNullable - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies test hash set.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void TestHashSet(List<string> results)
        {
            try
            {
                HashSet<int> val = new HashSet<int> { 1, 2, 3, 4, 5 };
                byte[] buf = LuminPackSerializer.Serialize(val);
                HashSet<int> result = LuminPackSerializer.Deserialize<HashSet<int>>(buf);

                if (val.SetEquals(result) && result.SetEquals(new HashSet<int> { 1, 2, 3, 4, 5 }))
                    results.Add("✓ TestHashSet - PASSED");
                else
                    results.Add("✗ TestHashSet - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestHashSet - ERROR: {ex.Message}");
            }
        }
/// <summary>Checks scalar boundaries through statically closed generated entries without repeated runner boilerplate.</summary>
        /// <param name="results">Receives the single logical scalar contract result.</param>
        public static void TestScalarRoundTrips(List<string> results)
        {
            try
            {
                CheckScalars(new byte[] { 0, 10, byte.MaxValue }, static (byte value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<byte>(bytes));
                CheckScalars(new sbyte[] { sbyte.MinValue, -95, sbyte.MaxValue }, static (sbyte value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<sbyte>(bytes));
                CheckScalars(new short[] { short.MinValue, -137, short.MaxValue }, static (short value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<short>(bytes));
                CheckScalars(new ushort[] { 0, 137, ushort.MaxValue }, static (ushort value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<ushort>(bytes));
                CheckScalars(new int[] { int.MinValue, -137, int.MaxValue }, static (int value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<int>(bytes));
                CheckScalars(new uint[] { 0, 137, uint.MaxValue }, static (uint value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<uint>(bytes));
                CheckScalars(new long[] { long.MinValue, -137, long.MaxValue }, static (long value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<long>(bytes));
                CheckScalars(new ulong[] { 0, 137, ulong.MaxValue }, static (ulong value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<ulong>(bytes));
                CheckScalars(new[] { false, true }, static (bool value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<bool>(bytes));
                CheckScalars(new[] { double.MinValue, -0d, double.NaN, double.PositiveInfinity, double.MaxValue }, static (double value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<double>(bytes));
                CheckScalars(new[] { float.MinValue, -0f, float.NaN, float.PositiveInfinity, float.MaxValue }, static (float value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<float>(bytes));
                CheckScalars(new[] { char.MinValue, 'A', '\uD800', char.MaxValue }, static (char value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<char>(bytes));
                CheckScalars(new[] { decimal.MinValue, -1.25m, decimal.MaxValue }, static (decimal value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<decimal>(bytes));
                CheckScalars(new[] { DateTime.MinValue, new DateTime(137, DateTimeKind.Utc), DateTime.MaxValue }, static (DateTime value) => LuminPackSerializer.Serialize(value), static (byte[] bytes) => LuminPackSerializer.Deserialize<DateTime>(bytes));
                results.Add("✓ Scalar boundaries - PASSED");
            }
            catch (Exception error) { results.Add("✗ Scalar boundaries - ERROR: " + error.Message); }
        }

        /// <summary>Executes a typed scalar matrix, preserving every payload bit as well as logical equality.</summary>
        /// <typeparam name="T">The reference-free scalar.</typeparam><param name="values">Boundary samples.</param><param name="write">Statically closed generated writer.</param><param name="read">Statically closed generated reader.</param>
        private static void CheckScalars<T>(T[] values, Func<T, byte[]> write, Func<byte[], T> read) where T : unmanaged
        {
            foreach (T value in values)
            {
                byte[] bytes = write(value);
                T copy = read(bytes);
                if (!EqualityComparer<T>.Default.Equals(value, copy) || !bytes.AsSpan().SequenceEqual(write(copy)))
                    throw new InvalidOperationException("Scalar round trip changed " + typeof(T) + ".");
            }
        }
    }
}
