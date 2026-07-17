using System.Collections.Concurrent;
using System.Text;
using LuminPack;
using LuminPack.Generated;
using LuminPack.Internal;
using static LuminPackUnitTest.PrimitivesSerializationTest;
using static LuminPackUnitTest.MultiThreadSerializationTest;
using static LuminPackUnitTest.ComplexSerializationTest;
using static LuminPackUnitTest.VersionTolerantSerializationTest;
using static LuminPackUnitTest.CircleReferenceSerializationTest;
using static LuminPackUnitTest.GenericSerializationTest;
using static LuminPackUnitTest.MultiDimensionalArraySerializationTest;
using static LuminPackUnitTest.SpecialTypesSerializationTest;
using static LuminPackUnitTest.CollectionTypesSerializationTest;

namespace LuminPackUnitTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting LuminPack Serialization Tests...");
            Console.WriteLine("=========================================\n");
            
            var testResults = new List<string>();
            // Run all tests
            TestEmptyStruct(testResults);
            TestEnum(testResults);
            TestByte(testResults);
            TestSByte(testResults);
            TestShort(testResults);
            TestUShort(testResults);
            TestInt(testResults);
            TestUInt(testResults);
            TestLong(testResults);
            TestULong(testResults);
            TestString(testResults);
            TestStringUtf8Token(testResults);
            TestStringUtf16(testResults);
            TestBool(testResults);
            TestDecimal(testResults);
            TestDouble(testResults);
            TestFloat(testResults);
            TestChar(testResults);
            TestDateTime(testResults);
            TestEnumArr(testResults);
            TestByteArr(testResults);
            TestSByteArr(testResults);
            TestShortArr(testResults);
            TestUShortArr(testResults);
            TestIntArr(testResults);
            TestUIntArr(testResults);
            TestLongArr(testResults);
            TestULongArr(testResults);
            TestStringArr(testResults);
            TestBoolArr(testResults);
            TestFloatArr(testResults);
            TestDoubleArr(testResults);
            TestCharArr(testResults);
            TestDateTimeArr(testResults);
            TestEnumList(testResults);
            TestByteList(testResults);
            TestSByteList(testResults);
            TestShortList(testResults);
            TestUShortList(testResults);
            TestIntList(testResults);
            TestUIntList(testResults);
            TestLongList(testResults);
            TestULongList(testResults);
            TestFloatList(testResults);
            TestDoubleList(testResults);
            TestDecimalList(testResults);
            TestCharList(testResults);
            TestStringList(testResults);
            TestBoolList(testResults);
            TestDateTimeList(testResults);
            TestByteIntDict(testResults);
            TestStringShortDict(testResults);
            TestStringStringDict(testResults);
            TestNullable(testResults);
            Test_HierarchicalBase(testResults);
            Test_HierarchicalSub1(testResults);
            Test_HierarchicalSub2(testResults);
            Test_SomeNestedPrivateEnum(testResults);
            Test_Move(testResults);
            Test_TestMethodCtor(testResults);
            Test_TestA(testResults);
            Test_TestB(testResults);
            Test_CursedGeneric(testResults);
            Test_PrivateNestedCollection(testResults);
            Test_ListElementClass(testResults);
            Test_ListElementClass2Renamed(testResults);
            Test_ProtectedShouldInclude(testResults);
            Test_ShouldIgnorePrivate(testResults);
            Test_Bindable(testResults);
            Test_TestPrivateMemberClass(testResults);
            Test_RecordWithPrivateMember(testResults);
            Test_RecordWithPrivateMember2(testResults);
            Test_StructWithPrivateMember(testResults);
            Test_ClassWithPrivateMember(testResults);
            Test_Struct1(testResults);
            Test_Class1(testResults);
            Test_Struct2(testResults);
            Test_StringData(testResults);
            Test_StringData2(testResults);
            Test_SaveData(testResults);
            Test_GenericStruct(testResults);
            Test_Generic(testResults);
            Test_ComplexGeneric(testResults);
            Test_ComplexGeneric2(testResults);
            Test_Sub1(testResults);
            Test_Sub3(testResults);
            Test_TestClass(testResults);
            Test_TestClass2(testResults);
            Test_TestClass3(testResults);
            Test_TestStruct(testResults);
            Test_TestStruct2(testResults);
            Test_SimpleClass(testResults);
            Test_SimpleRecordStruct(testResults);
            Test_SimpleRecordStruct2(testResults);
            Test_SimpleRecordStruct2_Generic(testResults);
            Test_SimpleRecord(testResults);
            Test_SimpleRecord2(testResults);
            Test_SimpleRecord3(testResults);
            Test_SimpleRecord4(testResults);
            Test_SimpleRecord5(testResults);
            Test_SimpleRecord6(testResults);
            Test_SimpleStruct(testResults);
            Test_SimpleClassWithConstructor(testResults);
            TestMultiThreadCodeGenSerialize(testResults);
            TestMultiThreadCodeGenDeserialize(testResults);
            TestMixedMultiThreadOperations(testResults);
            TestComplexDataSerialization(testResults);
            TestNestedDataSerialization(testResults);
            TestComplexDataWithNullValues(testResults);
            TestComplexDataWithEmptyCollections(testResults);
            TestVersionTolerantSerialization(testResults);
            TestSimpleReference(testResults);
            TestSimpleCircleReference(testResults);
            TestSimpleTree(testResults);
            TestComplexCircleReference(testResults);
            TestRingCircleReference(testResults);
            TestGenericIntSerialization(testResults);
            TestGenericClassSerialization(testResults);
            TestGenericDoubleSerialization(testResults);
            TestNestedGenericSerialization(testResults);
            TestGenericClass2Serialization(testResults);
            TestGenericPolymorphismWithString(testResults);
            TestGenericPolymorphismWithInt(testResults);
            TestNumericGenericPolymorphism(testResults);
            TestComplexGenericPolymorphism(testResults);
            TestTwoDimensionalArray(testResults);
            TestThreeDimensionalArray(testResults);
            TestFourDimensionalArray(testResults);
            TestFiveDimensionalArray(testResults);
            TestSixDimensionalArray(testResults);
            TestSevenDimensionalArray(testResults);
            TestAllArraysInOneContainer(testResults);
            TestLargeTwoDimensionalArray(testResults);
            TestUri(testResults);
            TestVersion(testResults);
            TestBigInteger(testResults);
            TestBitArray(testResults);
            TestBitArrayCodeGen(testResults);
            //TestCultureInfo(testResults);
            TestTimeZoneInfo(testResults);
            TestType(testResults);
            TestStringBuilder(testResults);
            TestLazy(testResults);
            TestTuple(testResults);
            TestValueTuple(testResults);
            TestComplex(testResults);
            TestVector2(testResults);
            TestVector3(testResults);
            TestVector4(testResults);
            TestQuaternion(testResults);
            TestMatrix3x2(testResults);
            TestMatrix4x4(testResults);
            TestPlane(testResults);
#if NET8_0_OR_GREATER
            TestHalf(testResults);
            TestInt128(testResults);
            TestUInt128(testResults);
            TestDateOnly(testResults);
            TestTimeOnly(testResults);
            TestRune(testResults);
#endif
            TestConcurrentDictionary(testResults);
            TestSortedDictionary(testResults);
            TestStack(testResults);
            TestQueue(testResults);
            TestLinkedList(testResults);
            CollectionTypesSerializationTest.TestHashSet(testResults);
            TestSortedSet(testResults);
            TestSortedList(testResults);
            TestBlockingCollection(testResults);
            TestConcurrentBag(testResults);
            TestConcurrentQueue(testResults);
            TestConcurrentStack(testResults);
            TestCollection(testResults);
            TestObservableCollection(testResults);
            TestReadOnlyCollection(testResults);
            TestReadOnlyObservableCollection(testResults);
            TestPriorityQueue(testResults);
            TestImmutableArray(testResults);
            TestImmutableList(testResults);
            TestImmutableQueue(testResults);
            TestImmutableStack(testResults);
            TestImmutableDictionary(testResults);
            TestImmutableHashSet(testResults);
            TestImmutableSortedDictionary(testResults);
            TestImmutableSortedSet(testResults);
#if NET8_0_OR_GREATER
            TestFrozenDictionary(testResults);
            TestFrozenSet(testResults);
#endif
            TestIEnumerable(testResults);
            TestICollection(testResults);
            TestIReadOnlyCollection(testResults);
            TestIList(testResults);
            TestIReadOnlyList(testResults);
            TestIDictionary(testResults);
            TestIReadOnlyDictionary(testResults);
            TestILookup(testResults);
            TestIGrouping(testResults);
            TestISet(testResults);
            TestIReadOnlySet(testResults);
            TestIImmutableList(testResults);
            TestIImmutableQueue(testResults);
            TestIImmutableStack(testResults);
            TestIImmutableDictionary(testResults);
            TestIImmutableSet(testResults);
            ObjectPoolTests.RunAll(testResults);
            
            // Print summary
            Console.WriteLine("\n=========================================");
            Console.WriteLine("Test Summary:");
            foreach (var result in testResults)
            {
                Console.WriteLine(result);
            }
            var passedCount = 0;
            var failedCount = 0;
            var skippedCount = 0;
            
            foreach (var result in testResults)
            {
                if (result.StartsWith("✓")) passedCount++;
                else if (result.StartsWith("✗")) failedCount++;
                else if (result.StartsWith("?")) skippedCount++;
            }

            Console.WriteLine($"\nTotal Tests: {testResults.Count}");
            Console.WriteLine($"Passed: {passedCount}");
            Console.WriteLine($"Failed: {failedCount}");
            Console.WriteLine($"Skipped: {skippedCount}");
        }

        static void Test_HierarchicalBase(List<string> results)
        {
            try
            {
                var original = new HierarchicalBase();
                original.B = "Test";
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<HierarchicalBase>(bytes);
                
                if (original.B == deserialized.B)
                    results.Add("✓ Test_HierarchicalBase - PASSED");
                else
                    results.Add("✗ Test_HierarchicalBase - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalBase - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new HierarchicalBase();
                original.B = "Test";
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<HierarchicalBase>(json);
                
                if (original.B == deserialized.B)
                    results.Add("✓ Test_HierarchicalBase [JSON] - PASSED");
                else
                    results.Add("✗ Test_HierarchicalBase [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalBase [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_HierarchicalSub1(List<string> results)
        {
            try
            {
                var original = new HierarchicalSub1();
                original.B = "Test";
                original.D = 1.23f;
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<HierarchicalSub1>(bytes);
                
                if (original.B == deserialized.B && original.D == deserialized.D)
                    results.Add("✓ Test_HierarchicalSub1 - PASSED");
                else
                    results.Add("✗ Test_HierarchicalSub1 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalSub1 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new HierarchicalSub1();
                original.B = "Test";
                original.D = 1.23f;
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<HierarchicalSub1>(json);
                
                if (original.B == deserialized.B && original.D == deserialized.D)
                    results.Add("✓ Test_HierarchicalSub1 [JSON] - PASSED");
                else
                    results.Add("✗ Test_HierarchicalSub1 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalSub1 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_HierarchicalSub2(List<string> results)
        {
            try
            {
                var original = new HierarchicalSub2();
                original.B = "Test";
                original.D = 1.23f;
                original.F = new List<int> { 1, 2, 3 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<HierarchicalSub2>(bytes);
                
                if (original.B == deserialized.B && original.D == deserialized.D && 
                    ListsEqual(original.F, deserialized.F))
                    results.Add("✓ Test_HierarchicalSub2 - PASSED");
                else
                    results.Add("✗ Test_HierarchicalSub2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalSub2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new HierarchicalSub2();
                original.B = "Test";
                original.D = 1.23f;
                original.F = new List<int> { 1, 2, 3 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<HierarchicalSub2>(json);
                
                if (original.B == deserialized.B && original.D == deserialized.D && 
                    ListsEqual(original.F, deserialized.F))
                    results.Add("✓ Test_HierarchicalSub2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_HierarchicalSub2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_HierarchicalSub2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SomeNestedPrivateEnum(List<string> results)
        {
            try
            {
                var original = new SomeNestedPrivateEnum();
                original.Id = 123;
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SomeNestedPrivateEnum>(bytes);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_SomeNestedPrivateEnum - PASSED");
                else
                    results.Add("✗ Test_SomeNestedPrivateEnum - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SomeNestedPrivateEnum - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SomeNestedPrivateEnum();
                original.Id = 123;
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SomeNestedPrivateEnum>(json);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_SomeNestedPrivateEnum [JSON] - PASSED");
                else
                    results.Add("✗ Test_SomeNestedPrivateEnum [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SomeNestedPrivateEnum [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Move(List<string> results)
        {
            try
            {
                var original = Move.Create(100, 200);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Move>(bytes);
                
                if (original.ClientX == deserialized.ClientX && original.ClientY == deserialized.ClientY)
                    results.Add("✓ Test_Move - PASSED");
                else
                    results.Add("✗ Test_Move - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Move - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = Move.Create(100, 200);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Move>(json);
                
                if (original.ClientX == deserialized.ClientX && original.ClientY == deserialized.ClientY)
                    results.Add("✓ Test_Move [JSON] - PASSED");
                else
                    results.Add("✗ Test_Move [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Move [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestMethodCtor(List<string> results)
        {
            try
            {
                var original = TestMethodCtor.Create(123, "test");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestMethodCtor>(bytes);
                
                if (original.A == deserialized.A)
                    results.Add("✓ Test_TestMethodCtor - PASSED");
                else
                    results.Add("✗ Test_TestMethodCtor - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestMethodCtor - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = TestMethodCtor.Create(123, "test");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestMethodCtor>(json);
                
                if (original.A == deserialized.A)
                    results.Add("✓ Test_TestMethodCtor [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestMethodCtor [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestMethodCtor [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestA(List<string> results)
        {
            try
            {
                var original = new TestA<int>(42);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestA<int>>(bytes);
                
                if (original.MValue == deserialized.MValue) 
                    results.Add("✓ Test_TestA - PASSED");
                else
                    results.Add("✗ Test_TestA - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestA - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestA<int>(42);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestA<int>>(json);
                
                if (original.MValue == deserialized.MValue) 
                    results.Add("✓ Test_TestA [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestA [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestA [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestB(List<string> results)
        {
            try
            {
                var original = new TestB<string>("hello");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestB<string>>(bytes);
                
                if (original.MValue == deserialized.MValue) 
                    results.Add("✓ Test_TestB - PASSED");
                else
                    results.Add("✗ Test_TestB - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestB - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestB<string>("hello");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestB<string>>(json);
                
                if (original.MValue == deserialized.MValue) 
                    results.Add("✓ Test_TestB [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestB [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestB [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_CursedGeneric(List<string> results)
        {
            try
            {
                var original = new CursedGeneric<int>();
                original.field = new ConcurrentDictionary<string, int[]>();
                original.field.TryAdd("test", new[] { 1, 2, 3 });
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<CursedGeneric<int>>(bytes);
                
                if (ArraysEqual(original.field["test"], deserialized.field["test"]))
                    results.Add("✓ Test_CursedGeneric - PASSED");
                else
                    results.Add("✗ Test_CursedGeneric - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_CursedGeneric - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new CursedGeneric<int>();
                original.field = new ConcurrentDictionary<string, int[]>();
                original.field.TryAdd("test", new[] { 1, 2, 3 });
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<CursedGeneric<int>>(json);
                
                if (ArraysEqual(original.field["test"], deserialized.field["test"]))
                    results.Add("✓ Test_CursedGeneric [JSON] - PASSED");
                else
                    results.Add("✗ Test_CursedGeneric [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_CursedGeneric [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_PrivateNestedCollection(List<string> results)
        {
            try
            {
                var original = new PrivateNestedCollection();
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<PrivateNestedCollection>(bytes);
                
                if (deserialized != null)
                    results.Add("✓ Test_PrivateNestedCollection - PASSED");
                else
                    results.Add("✗ Test_PrivateNestedCollection - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_PrivateNestedCollection - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new PrivateNestedCollection();
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<PrivateNestedCollection>(json);
                
                if (deserialized != null)
                    results.Add("✓ Test_PrivateNestedCollection [JSON] - PASSED");
                else
                    results.Add("✗ Test_PrivateNestedCollection [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_PrivateNestedCollection [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ListElementClass(List<string> results)
        {
            try
            {
                var original = new ListElementClass
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now,
                    Extra = true
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ListElementClass>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Extra == deserialized.Extra)
                    results.Add("✓ Test_ListElementClass - PASSED");
                else
                    results.Add("✗ Test_ListElementClass - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ListElementClass - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ListElementClass
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now,
                    Extra = true
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ListElementClass>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Extra == deserialized.Extra)
                    results.Add("✓ Test_ListElementClass [JSON] - PASSED");
                else
                    results.Add("✗ Test_ListElementClass [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ListElementClass [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ListElementClass2Renamed(List<string> results)
        {
            try
            {
                var original = new ListElementClass2Renamed
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now,
                    Extra = "extra"
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ListElementClass2Renamed>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Extra == deserialized.Extra)
                    results.Add("✓ Test_ListElementClass2Renamed - PASSED");
                else
                    results.Add("✗ Test_ListElementClass2Renamed - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ListElementClass2Renamed - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ListElementClass2Renamed
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now,
                    Extra = "extra"
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ListElementClass2Renamed>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Extra == deserialized.Extra)
                    results.Add("✓ Test_ListElementClass2Renamed [JSON] - PASSED");
                else
                    results.Add("✗ Test_ListElementClass2Renamed [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ListElementClass2Renamed [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ProtectedShouldInclude(List<string> results)
        {
            try
            {
                var original = new ProtectedShouldInclude();
                original.Id = 123;
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ProtectedShouldInclude>(bytes);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_ProtectedShouldInclude - PASSED");
                else
                    results.Add("✗ Test_ProtectedShouldInclude - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ProtectedShouldInclude - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ProtectedShouldInclude();
                original.Id = 123;
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ProtectedShouldInclude>(json);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_ProtectedShouldInclude [JSON] - PASSED");
                else
                    results.Add("✗ Test_ProtectedShouldInclude [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ProtectedShouldInclude [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ShouldIgnorePrivate(List<string> results)
        {
            try
            {
                var original = new ShouldIgnorePrivate();
                original.Id = 123;
                original.Name = "Test";
                original.CreateTime = DateTime.Now;
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ShouldIgnorePrivate>(bytes);
                
                if (original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_ShouldIgnorePrivate - PASSED");
                else
                    results.Add("✗ Test_ShouldIgnorePrivate - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ShouldIgnorePrivate - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ShouldIgnorePrivate();
                original.Id = 123;
                original.Name = "Test";
                original.CreateTime = DateTime.Now;
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ShouldIgnorePrivate>(json);
                
                if (original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_ShouldIgnorePrivate [JSON] - PASSED");
                else
                    results.Add("✗ Test_ShouldIgnorePrivate [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ShouldIgnorePrivate [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Bindable(List<string> results)
        {
            try
            {
                var original = new Bindable<int>(42);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Bindable<int>>(bytes);
                
                if (original.Value == deserialized.Value)
                    results.Add("✓ Test_Bindable - PASSED");
                else
                    results.Add("✗ Test_Bindable - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Bindable - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Bindable<int>(42);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Bindable<int>>(json);
                
                if (original.Value == deserialized.Value)
                    results.Add("✓ Test_Bindable [JSON] - PASSED");
                else
                    results.Add("✗ Test_Bindable [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Bindable [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestPrivateMemberClass(List<string> results)
        {
            try
            {
                var original = new TestPrivateMemberClass();
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestPrivateMemberClass>(bytes);
                
                if (deserialized != null)
                    results.Add("✓ Test_TestPrivateMemberClass - PASSED");
                else
                    results.Add("✗ Test_TestPrivateMemberClass - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestPrivateMemberClass - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestPrivateMemberClass();
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestPrivateMemberClass>(json);
                
                if (deserialized != null)
                    results.Add("✓ Test_TestPrivateMemberClass [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestPrivateMemberClass [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestPrivateMemberClass [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_RecordWithPrivateMember(List<string> results)
        {
            try
            {
                var original = new RecordWithPrivateMember("Test");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<RecordWithPrivateMember>(bytes);
                
                if (original.Name == deserialized.Name)
                    results.Add("✓ Test_RecordWithPrivateMember - PASSED");
                else
                    results.Add("✗ Test_RecordWithPrivateMember - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_RecordWithPrivateMember - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new RecordWithPrivateMember("Test");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<RecordWithPrivateMember>(json);
                
                if (original.Name == deserialized.Name)
                    results.Add("✓ Test_RecordWithPrivateMember [JSON] - PASSED");
                else
                    results.Add("✗ Test_RecordWithPrivateMember [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_RecordWithPrivateMember [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_RecordWithPrivateMember2(List<string> results)
        {
            try
            {
                var original = new RecordWithPrivateMember2("Test");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<RecordWithPrivateMember2>(bytes);
                
                if (original.Name == deserialized.Name)
                    results.Add("✓ Test_RecordWithPrivateMember2 - PASSED");
                else
                    results.Add("✗ Test_RecordWithPrivateMember2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_RecordWithPrivateMember2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new RecordWithPrivateMember2("Test");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<RecordWithPrivateMember2>(json);
                
                if (original.Name == deserialized.Name)
                    results.Add("✓ Test_RecordWithPrivateMember2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_RecordWithPrivateMember2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_RecordWithPrivateMember2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_StructWithPrivateMember(List<string> results)
        {
            try
            {
                var original = new StructWithPrivateMember();
                original.Id = 123;
                original.SetName("Test");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<StructWithPrivateMember>(bytes);
                
                if (original.Id == deserialized.Id && original.GetName() == deserialized.GetName())
                    results.Add("✓ Test_StructWithPrivateMember - PASSED");
                else
                    results.Add("✗ Test_StructWithPrivateMember - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StructWithPrivateMember - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new StructWithPrivateMember();
                original.Id = 123;
                original.SetName("Test");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<StructWithPrivateMember>(json);
                
                if (original.Id == deserialized.Id && original.GetName() == deserialized.GetName())
                    results.Add("✓ Test_StructWithPrivateMember [JSON] - PASSED");
                else
                    results.Add("✗ Test_StructWithPrivateMember [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StructWithPrivateMember [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ClassWithPrivateMember(List<string> results)
        {
            try
            {
                var original = new ClassWithPrivateMember<int>();
                original.Id = 123;
                original.List = new List<int> { 1, 2, 3 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ClassWithPrivateMember<int>>(bytes);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_ClassWithPrivateMember - PASSED");
                else
                    results.Add("✗ Test_ClassWithPrivateMember - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ClassWithPrivateMember - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ClassWithPrivateMember<int>();
                original.Id = 123;
                original.List = new List<int> { 1, 2, 3 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ClassWithPrivateMember<int>>(json);
                
                if (original.Id == deserialized.Id)
                    results.Add("✓ Test_ClassWithPrivateMember [JSON] - PASSED");
                else
                    results.Add("✗ Test_ClassWithPrivateMember [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ClassWithPrivateMember [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Struct1(List<string> results)
        {
            try
            {
                var original = new Struct1
                {
                    A = 1,
                    B = DateTime.Now,
                    C = Guid.NewGuid()
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Struct1>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Struct1 - PASSED");
                else
                    results.Add("✗ Test_Struct1 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Struct1 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Struct1
                {
                    A = 1,
                    B = DateTime.Now,
                    C = Guid.NewGuid()
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Struct1>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Struct1 [JSON] - PASSED");
                else
                    results.Add("✗ Test_Struct1 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Struct1 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Class1(List<string> results)
        {
            try
            {
                var original = new Class1
                {
                    A = 1,
                    B = DateTime.Now,
                    C = Guid.NewGuid(),
                    D = new Struct1 { A = 2, B = DateTime.Now, C = Guid.NewGuid() }
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Class1>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Class1 - PASSED");
                else
                    results.Add("✗ Test_Class1 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Class1 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Class1
                {
                    A = 1,
                    B = DateTime.Now,
                    C = Guid.NewGuid(),
                    D = new Struct1 { A = 2, B = DateTime.Now, C = Guid.NewGuid() }
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Class1>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Class1 [JSON] - PASSED");
                else
                    results.Add("✗ Test_Class1 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Class1 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Struct2(List<string> results)
        {
            try
            {
                var original = new Struct2
                {
                    A = 1,
                    B = DateTime.Now,
                    C = "Test",
                    D = new Class1 { A = 2, B = DateTime.Now, C = Guid.NewGuid() }
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Struct2>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Struct2 - PASSED");
                else
                    results.Add("✗ Test_Struct2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Struct2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Struct2
                {
                    A = 1,
                    B = DateTime.Now,
                    C = "Test",
                    D = new Class1 { A = 2, B = DateTime.Now, C = Guid.NewGuid() }
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Struct2>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_Struct2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_Struct2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Struct2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_StringData(List<string> results)
        {
            try
            {
                var original = new StringData
                {
                    Str = "Hello World",
                    ShouldHaveNoEffect = true
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<StringData>(bytes);
                
                if (original.Str == deserialized.Str && original.ShouldHaveNoEffect == deserialized.ShouldHaveNoEffect)
                    results.Add("✓ Test_StringData - PASSED");
                else
                    results.Add("✗ Test_StringData - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StringData - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new StringData
                {
                    Str = "Hello World",
                    ShouldHaveNoEffect = true
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<StringData>(json);
                
                if (original.Str == deserialized.Str && original.ShouldHaveNoEffect == deserialized.ShouldHaveNoEffect)
                    results.Add("✓ Test_StringData [JSON] - PASSED");
                else
                    results.Add("✗ Test_StringData [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StringData [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_StringData2(List<string> results)
        {
            try
            {
                var original = new StringData2
                {
                    Str = "Hello World",
                    ShouldHaveNoEffect = true
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<StringData2>(bytes);
                
                if (original.Str == deserialized.Str && original.ShouldHaveNoEffect == deserialized.ShouldHaveNoEffect)
                    results.Add("✓ Test_StringData2 - PASSED");
                else
                    results.Add("✗ Test_StringData2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StringData2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new StringData2
                {
                    Str = "Hello World",
                    ShouldHaveNoEffect = true
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<StringData2>(json);
                
                if (original.Str == deserialized.Str && original.ShouldHaveNoEffect == deserialized.ShouldHaveNoEffect)
                    results.Add("✓ Test_StringData2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_StringData2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_StringData2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SaveData(List<string> results)
        {
            try
            {
                var original = new SaveData
                {
                    Id = 1,
                    Name = "Test",
                    NewField1 = DateTime.Now,
                    NewField2 = new Generic<int> { Val = 42 }
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SaveData>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.NewField1 == deserialized.NewField1 && 
                    original.NewField2.Val == deserialized.NewField2.Val)
                    results.Add("✓ Test_SaveData - PASSED");
                else
                    results.Add("✗ Test_SaveData - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SaveData - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SaveData
                {
                    Id = 1,
                    Name = "Test",
                    NewField1 = DateTime.Now,
                    NewField2 = new Generic<int> { Val = 42 }
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SaveData>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.NewField1 == deserialized.NewField1 && 
                    original.NewField2.Val == deserialized.NewField2.Val)
                    results.Add("✓ Test_SaveData [JSON] - PASSED");
                else
                    results.Add("✗ Test_SaveData [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SaveData [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_GenericStruct(List<string> results)
        {
            try
            {
                var original = new GenericStruct<int> { Val = 42 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<GenericStruct<int>>(bytes);
                
                if (original.Val == deserialized.Val)
                    results.Add("✓ Test_GenericStruct - PASSED");
                else
                    results.Add("✗ Test_GenericStruct - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_GenericStruct - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new GenericStruct<int> { Val = 42 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<GenericStruct<int>>(json);
                
                if (original.Val == deserialized.Val)
                    results.Add("✓ Test_GenericStruct [JSON] - PASSED");
                else
                    results.Add("✗ Test_GenericStruct [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_GenericStruct [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Generic(List<string> results)
        {
            try
            {
                var original = new Generic<string> { Val = "test" };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Generic<string>>(bytes);
                
                if (original.Val == deserialized.Val)
                    results.Add("✓ Test_Generic - PASSED");
                else
                    results.Add("✗ Test_Generic - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Generic - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Generic<string> { Val = "test" };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Generic<string>>(json);
                
                if (original.Val == deserialized.Val)
                    results.Add("✓ Test_Generic [JSON] - PASSED");
                else
                    results.Add("✗ Test_Generic [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Generic [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ComplexGeneric(List<string> results)
        {
            try
            {
                var original = new ComplexGeneric<List<int>> { Val = new List<int> { 1, 2, 3 } };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ComplexGeneric<List<int>>>(bytes);
                
                if (ListsEqual(original.Val, deserialized.Val))
                    results.Add("✓ Test_ComplexGeneric - PASSED");
                else
                    results.Add("✗ Test_ComplexGeneric - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ComplexGeneric - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ComplexGeneric<List<int>> { Val = new List<int> { 1, 2, 3 } };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ComplexGeneric<List<int>>>(json);
                
                if (ListsEqual(original.Val, deserialized.Val))
                    results.Add("✓ Test_ComplexGeneric [JSON] - PASSED");
                else
                    results.Add("✗ Test_ComplexGeneric [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ComplexGeneric [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_ComplexGeneric2(List<string> results)
        {
            try
            {
                var original = new ComplexGeneric2<int> { Val = new Generic<int> { Val = 42 } };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<ComplexGeneric2<int>>(bytes);
                
                if (original.Val.Val == deserialized.Val.Val)
                    results.Add("✓ Test_ComplexGeneric2 - PASSED");
                else
                    results.Add("✗ Test_ComplexGeneric2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ComplexGeneric2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new ComplexGeneric2<int> { Val = new Generic<int> { Val = 42 } };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<ComplexGeneric2<int>>(json);
                
                if (original.Val.Val == deserialized.Val.Val)
                    results.Add("✓ Test_ComplexGeneric2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_ComplexGeneric2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_ComplexGeneric2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Sub1(List<string> results)
        {
            try
            {
                var original = new Sub1 { A = 1, B = 2 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Sub1>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_Sub1 - PASSED");
                else
                    results.Add("✗ Test_Sub1 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Sub1 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Sub1 { A = 1, B = 2 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Sub1>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_Sub1 [JSON] - PASSED");
                else
                    results.Add("✗ Test_Sub1 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Sub1 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_Sub3(List<string> results)
        {
            try
            {
                var original = new Sub3 { A = 1, C = 3, E = 5 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<Sub3>(bytes);
                
                if (original.A == deserialized.A && original.C == deserialized.C && original.E == deserialized.E)
                    results.Add("✓ Test_Sub3 - PASSED");
                else
                    results.Add("✗ Test_Sub3 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Sub3 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new Sub3 { A = 1, C = 3, E = 5 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<Sub3>(json);
                
                if (original.A == deserialized.A && original.C == deserialized.C && original.E == deserialized.E)
                    results.Add("✓ Test_Sub3 [JSON] - PASSED");
                else
                    results.Add("✗ Test_Sub3 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_Sub3 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestClass(List<string> results)
        {
            try
            {
                var original = new TestClass { A = 1, B = "test" };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestClass>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestClass - PASSED");
                else
                    results.Add("✗ Test_TestClass - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestClass { A = 1, B = "test" };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestClass>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestClass [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestClass [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestClass2(List<string> results)
        {
            try
            {
                var original = new TestClass2 { A = 1, B = "test", C = 3 };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestClass2>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_TestClass2 - PASSED");
                else
                    results.Add("✗ Test_TestClass2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestClass2 { A = 1, B = "test", C = 3 };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestClass2>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B && original.C == deserialized.C)
                    results.Add("✓ Test_TestClass2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestClass2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestClass3(List<string> results)
        {
            try
            {
                var original = new TestClass3
                {
                    A = 1,
                    B = "test",
                    C = 3,
                    D = true,
                    E = new TestClass { A = 2, B = "inner" },
                    F = new TestStruct { A = 4, B = "struct" },
                    G = new TestStruct { A = 5, B = "nullable" },
                    H = new List<TestStruct2> { new TestStruct2 { A = 6, B = true } },
                    I = new List<TestStruct2?> { new TestStruct2 { A = 7, B = false }, null },
                    J = new TestStruct3[] { new TestStruct3 { A = 8, B = 1.5f } },
                    K = new Dictionary<int, int> { { 1, 100 } },
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestClass3>(bytes);
                
                bool success = original.A == deserialized.A && 
                              original.B == deserialized.B && 
                              original.C == deserialized.C && 
                              original.D == deserialized.D &&
                              original.E.A == deserialized.E.A &&
                              original.E.B == deserialized.E.B &&
                              original.F.A == deserialized.F.A &&
                              original.F.B == deserialized.F.B &&
                              original.G.Value.A == deserialized.G.Value.A &&
                              original.H.Count == deserialized.H.Count &&
                              original.I[0].Value.A == deserialized.I[0].Value.A &&
                              original.J.Length == deserialized.J.Length &&
                              original.K[1] == deserialized.K[1];
                
                if (success)
                    results.Add("✓ Test_TestClass3 - PASSED");
                else
                    results.Add("✗ Test_TestClass3 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass3 - ERROR: {ex}");
            }

            // JSON Test
            try
            {
                var original = new TestClass3
                {
                    A = 1,
                    B = "test",
                    C = 3,
                    D = true,
                    E = new TestClass { A = 2, B = "inner" },
                    F = new TestStruct { A = 4, B = "struct" },
                    G = new TestStruct { A = 5, B = "nullable" },
                    H = new List<TestStruct2> { new TestStruct2 { A = 6, B = true } },
                    I = new List<TestStruct2?> { new TestStruct2 { A = 7, B = false }, null },
                    J = new TestStruct3[] { new TestStruct3 { A = 8, B = 1.5f } },
                    K = new Dictionary<int, int> { { 1, 100 } },
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestClass3>(json);
                
                bool success = original.A == deserialized.A && 
                              original.B == deserialized.B && 
                              original.C == deserialized.C && 
                              original.D == deserialized.D &&
                              original.E.A == deserialized.E.A &&
                              original.E.B == deserialized.E.B &&
                              original.F.A == deserialized.F.A &&
                              original.F.B == deserialized.F.B &&
                              original.G.Value.A == deserialized.G.Value.A &&
                              original.H.Count == deserialized.H.Count &&
                              original.I[0].Value.A == deserialized.I[0].Value.A &&
                              original.J.Length == deserialized.J.Length &&
                              original.K[1] == deserialized.K[1];
                
                if (success)
                    results.Add("✓ Test_TestClass3 [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestClass3 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestClass3 [JSON] - ERROR: {ex}");
            }
        }

        static void Test_TestStruct(List<string> results)
        {
            try
            {
                var original = new TestStruct { A = 1, B = "test" };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestStruct>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestStruct - PASSED");
                else
                    results.Add("✗ Test_TestStruct - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestStruct - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestStruct { A = 1, B = "test" };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestStruct>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestStruct [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestStruct [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestStruct [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_TestStruct2(List<string> results)
        {
            try
            {
                var original = new TestStruct2
                {
                    A = 1,
                    B = true,
                    C = new TestStruct3 { A = 2, B = 1.5f }
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<TestStruct2>(bytes);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestStruct2 - PASSED");
                else
                    results.Add("✗ Test_TestStruct2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestStruct2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new TestStruct2
                {
                    A = 1,
                    B = true,
                    C = new TestStruct3 { A = 2, B = 1.5f }
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<TestStruct2>(json);
                
                if (original.A == deserialized.A && original.B == deserialized.B)
                    results.Add("✓ Test_TestStruct2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_TestStruct2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_TestStruct2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleClass(List<string> results)
        {
            try
            {
                var original = new SimpleClass
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleClass>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleClass - PASSED");
                else
                    results.Add("✗ Test_SimpleClass - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleClass - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleClass
                {
                    Id = 1,
                    Name = "Test",
                    CreateTime = DateTime.Now
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleClass>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleClass [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleClass [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleClass [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecordStruct(List<string> results)
        {
            try
            {
                var original = new SimpleRecordStruct(1, "Test", DateTime.Now);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecordStruct>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecordStruct - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecordStruct(1, "Test", DateTime.Now);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecordStruct>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecordStruct [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecordStruct2(List<string> results)
        {
            try
            {
                var original = new SimpleRecordStruct2(1, DateTime.Now);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecordStruct2>(bytes);
                
                if (original.Id == deserialized.Id && original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecordStruct2 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecordStruct2(1, DateTime.Now);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecordStruct2>(json);
                
                if (original.Id == deserialized.Id && original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecordStruct2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecordStruct2_Generic(List<string> results)
        {
            try
            {
                var original = new SimpleRecordStruct2<string>(1, "test data");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecordStruct2<string>>(bytes);
                
                if (original.Id == deserialized.Id && original.Data == deserialized.Data)
                    results.Add("✓ Test_SimpleRecordStruct2_Generic - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct2_Generic - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct2_Generic - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecordStruct2<string>(1, "test data");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecordStruct2<string>>(json);
                
                if (original.Id == deserialized.Id && original.Data == deserialized.Data)
                    results.Add("✓ Test_SimpleRecordStruct2_Generic [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecordStruct2_Generic [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecordStruct2_Generic [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord(List<string> results)
        {
            try
            {
                var original = new SimpleRecord(1, "Test");
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name)
                    results.Add("✓ Test_SimpleRecord - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord(1, "Test");
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name)
                    results.Add("✓ Test_SimpleRecord [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord2(List<string> results)
        {
            try
            {
                var original = new SimpleRecord2(1, "Test", DateTime.Now);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord2>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecord2 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord2 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord2 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord2(1, "Test", DateTime.Now);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord2>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleRecord2 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord2 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord2 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord3(List<string> results)
        {
            try
            {
                var original = new SimpleRecord3(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    Ignored = 999
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord3>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Flag == deserialized.Flag)
                    results.Add("✓ Test_SimpleRecord3 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord3 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord3 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord3(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    Ignored = 999
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord3>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.Flag == deserialized.Flag)
                    results.Add("✓ Test_SimpleRecord3 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord3 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord3 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord4(List<string> results)
        {
            try
            {
                var original = new SimpleRecord4(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    ShouldNotIgnore = 42
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord4>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.ShouldNotIgnore == deserialized.ShouldNotIgnore)
                    results.Add("✓ Test_SimpleRecord4 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord4 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord4 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord4(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    ShouldNotIgnore = 42
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord4>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime && original.ShouldNotIgnore == deserialized.ShouldNotIgnore)
                    results.Add("✓ Test_SimpleRecord4 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord4 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord4 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord5(List<string> results)
        {
            try
            {
                var original = new SimpleRecord5(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    ShouldNotIgnore = 42
                };
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord5>(bytes);
                
                if (original.ShouldNotIgnore == deserialized.ShouldNotIgnore)
                    results.Add("✓ Test_SimpleRecord5 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord5 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord5 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord5(1, "Test", DateTime.Now)
                {
                    Flag = true,
                    ShouldNotIgnore = 42
                };
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord5>(json);
                
                if (original.ShouldNotIgnore == deserialized.ShouldNotIgnore)
                    results.Add("✓ Test_SimpleRecord5 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord5 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord5 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleRecord6(List<string> results)
        {
            try
            {
                var original = new SimpleRecord6<int>(1, 42);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleRecord6<int>>(bytes);
                
                if (original.Id == deserialized.Id && original.Data == deserialized.Data)
                    results.Add("✓ Test_SimpleRecord6 - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord6 - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord6 - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleRecord6<int>(1, 42);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleRecord6<int>>(json);
                
                if (original.Id == deserialized.Id && original.Data == deserialized.Data)
                    results.Add("✓ Test_SimpleRecord6 [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleRecord6 [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleRecord6 [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleStruct(List<string> results)
        {
            try
            {
                var original = new SimpleStruct(1, "Test", DateTime.Now);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleStruct>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleStruct - PASSED");
                else
                    results.Add("✗ Test_SimpleStruct - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleStruct - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleStruct(1, "Test", DateTime.Now);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleStruct>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleStruct [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleStruct [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleStruct [JSON] - ERROR: {ex.Message}");
            }
        }

        static void Test_SimpleClassWithConstructor(List<string> results)
        {
            try
            {
                var original = new SimpleClassWithConstructor(1, "Test", DateTime.Now);
                
                var bytes = LuminPackSerializer.Serialize(original);
                var deserialized = LuminPackSerializer.Deserialize<SimpleClassWithConstructor>(bytes);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleClassWithConstructor - PASSED");
                else
                    results.Add("✗ Test_SimpleClassWithConstructor - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleClassWithConstructor - ERROR: {ex.Message}");
            }

            // JSON Test
            try
            {
                var original = new SimpleClassWithConstructor(1, "Test", DateTime.Now);
                
                var json = LuminPackSerializer.SerializeJson(original);
                var deserialized = LuminPackSerializer.DeserializeJson<SimpleClassWithConstructor>(json);
                
                if (original.Id == deserialized.Id && original.Name == deserialized.Name && 
                    original.CreateTime == deserialized.CreateTime)
                    results.Add("✓ Test_SimpleClassWithConstructor [JSON] - PASSED");
                else
                    results.Add("✗ Test_SimpleClassWithConstructor [JSON] - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Test_SimpleClassWithConstructor [JSON] - ERROR: {ex.Message}");
            }
        }

        // Helper methods for comparison
        static bool ListsEqual<T>(List<T> list1, List<T> list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;
            
            for (int i = 0; i < list1.Count; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(list1[i], list2[i]))
                    return false;
            }
            return true;
        }

        static bool ArraysEqual<T>(T[] array1, T[] array2)
        {
            if (array1 == null && array2 == null) return true;
            if (array1 == null || array2 == null) return false;
            if (array1.Length != array2.Length) return false;
            
            for (int i = 0; i < array1.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(array1[i], array2[i]))
                    return false;
            }
            return true;
        }
    }
}
