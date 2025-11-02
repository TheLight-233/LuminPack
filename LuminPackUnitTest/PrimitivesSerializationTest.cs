

using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackUnitTest
{
    public enum TestEnumVal : short
    {
        A = Int16.MinValue,
        B = Int16.MaxValue,
    }

    [LuminPackable]
    public struct EmptyStruct
    {
    }

    public class PrimitivesSerializationTest
    {

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

        public static void TestByte(List<string> results)
        {
            try
            {
                byte val = 10;
                byte[] buf = LuminPackSerializer.Serialize(val);
                byte result = LuminPackSerializer.Deserialize<byte>(buf);
                
                if (val == result && result == 10)
                    results.Add("✓ TestByte - PASSED");
                else
                    results.Add("✗ TestByte - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestByte - ERROR: {ex.Message}");
            }
        }

        public static void TestSByte(List<string> results)
        {
            try
            {
                sbyte val = -95;
                byte[] buf = LuminPackSerializer.Serialize(val);
                sbyte result = LuminPackSerializer.Deserialize<sbyte>(buf);
                
                if (val == result && result == -95)
                    results.Add("✓ TestSByte - PASSED");
                else
                    results.Add("✗ TestSByte - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestSByte - ERROR: {ex.Message}");
            }
        }

        public static void TestShort(List<string> results)
        {
            try
            {
                short val = short.MinValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                short result = LuminPackSerializer.Deserialize<short>(buf);
                
                if (val == result && result == short.MinValue)
                    results.Add("✓ TestShort - PASSED");
                else
                    results.Add("✗ TestShort - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestShort - ERROR: {ex.Message}");
            }
        }

        public static void TestUShort(List<string> results)
        {
            try
            {
                ushort val = ushort.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                ushort result = LuminPackSerializer.Deserialize<ushort>(buf);
                
                if (val == result && result == ushort.MaxValue)
                    results.Add("✓ TestUShort - PASSED");
                else
                    results.Add("✗ TestUShort - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUShort - ERROR: {ex.Message}");
            }
        }

        public static void TestInt(List<string> results)
        {
            try
            {
                int val = int.MinValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                int result = LuminPackSerializer.Deserialize<int>(buf);
                
                if (val == result && result == int.MinValue)
                    results.Add("✓ TestInt - PASSED");
                else
                    results.Add("✗ TestInt - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestInt - ERROR: {ex.Message}");
            }
        }

        public static void TestUInt(List<string> results)
        {
            try
            {
                uint val = uint.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                uint result = LuminPackSerializer.Deserialize<uint>(buf);
                
                if (val == result && result == uint.MaxValue)
                    results.Add("✓ TestUInt - PASSED");
                else
                    results.Add("✗ TestUInt - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestUInt - ERROR: {ex.Message}");
            }
        }

        public static void TestLong(List<string> results)
        {
            try
            {
                long val = long.MinValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                long result = LuminPackSerializer.Deserialize<long>(buf);
                
                if (val == result && result == long.MinValue)
                    results.Add("✓ TestLong - PASSED");
                else
                    results.Add("✗ TestLong - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestLong - ERROR: {ex.Message}");
            }
        }

        public static void TestULong(List<string> results)
        {
            try
            {
                ulong val = ulong.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                ulong result = LuminPackSerializer.Deserialize<ulong>(buf);
                
                if (val == result && result == ulong.MaxValue)
                    results.Add("✓ TestULong - PASSED");
                else
                    results.Add("✗ TestULong - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestULong - ERROR: {ex.Message}");
            }
        }

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
        
        public static void TestStringUtf8Token(List<string> results)
        {
            try
            {
                string val = "嗨嗨Test！";
                byte[] buf = LuminPackSerializer.Serialize(val, new LuminPackSerializerOption()
                {
                    StringEncoding = LuminPackStringEncoding.UTF8,
                    StringRecording = LuminPackStringRecording.Token
                });
                string result = LuminPackSerializer.Deserialize<string>(buf, new LuminPackSerializerOption()
                {
                    StringEncoding = LuminPackStringEncoding.UTF8,
                    StringRecording = LuminPackStringRecording.Token
                });
                
                if (val == result && result == "嗨嗨Test！")
                    results.Add("✓ TestStringUtf8Token - PASSED");
                else
                    results.Add("✗ TestStringUtf8Token - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringUtf8Token - ERROR: {ex.Message}");
            }
        }
        
        public static void TestStringUtf16(List<string> results)
        {
            try
            {
                string val = "嗨嗨Test！";
                byte[] buf = LuminPackSerializer.Serialize(val, new LuminPackSerializerOption()
                {
                    StringEncoding = LuminPackStringEncoding.UTF16,
                    StringRecording = LuminPackStringRecording.Length
                });
                string result = LuminPackSerializer.Deserialize<string>(buf, new LuminPackSerializerOption()
                {
                    StringEncoding = LuminPackStringEncoding.UTF16,
                    StringRecording = LuminPackStringRecording.Length
                });
                
                if (val == result && result == "嗨嗨Test！")
                    results.Add("✓ TestStringUtf16 - PASSED");
                else
                    results.Add("✗ TestStringUtf16 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestStringUtf16 - ERROR: {ex.Message}");
            }
        }

        public static void TestBool(List<string> results)
        {
            try
            {
                bool val = true;
                byte[] buf = LuminPackSerializer.Serialize(val);
                bool result = LuminPackSerializer.Deserialize<bool>(buf);
                
                if (val == result && result == true)
                    results.Add("✓ TestBool - PASSED");
                else
                    results.Add("✗ TestBool - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestBool - ERROR: {ex.Message}");
            }
        }

        public static void TestDecimal(List<string> results)
        {
            try
            {
                decimal val = decimal.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                decimal result = LuminPackSerializer.Deserialize<decimal>(buf);
                
                if (val == result && result == decimal.MaxValue)
                    results.Add("✓ TestDecimal - PASSED");
                else
                    results.Add("✗ TestDecimal - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDecimal - ERROR: {ex.Message}");
            }
        }

        public static void TestDouble(List<string> results)
        {
            try
            {
                double val = double.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                double result = LuminPackSerializer.Deserialize<double>(buf);
                
                if (val == result && result == double.MaxValue)
                    results.Add("✓ TestDouble - PASSED");
                else
                    results.Add("✗ TestDouble - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDouble - ERROR: {ex.Message}");
            }
        }

        public static void TestFloat(List<string> results)
        {
            try
            {
                float val = float.MaxValue;
                byte[] buf = LuminPackSerializer.Serialize(val);
                float result = LuminPackSerializer.Deserialize<float>(buf);
                
                if (val == result && result == float.MaxValue)
                    results.Add("✓ TestFloat - PASSED");
                else
                    results.Add("✗ TestFloat - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestFloat - ERROR: {ex.Message}");
            }
        }

        public static void TestChar(List<string> results)
        {
            try
            {
                char val = 'a';
                byte[] buf = LuminPackSerializer.Serialize(val);
                char result = LuminPackSerializer.Deserialize<char>(buf);
                
                if (val == result && result == 'a')
                    results.Add("✓ TestChar - PASSED");
                else
                    results.Add("✗ TestChar - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestChar - ERROR: {ex.Message}");
            }
        }

        public static void TestDateTime(List<string> results)
        {
            try
            {
                DateTime val = new DateTime(2000, 1, 1);
                byte[] buf = LuminPackSerializer.Serialize(val);
                DateTime result = LuminPackSerializer.Deserialize<DateTime>(buf);
                
                if (val == result && result == new DateTime(2000, 1, 1))
                    results.Add("✓ TestDateTime - PASSED");
                else
                    results.Add("✗ TestDateTime - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ TestDateTime - ERROR: {ex.Message}");
            }
        }

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
    }
}