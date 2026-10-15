using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

/// <summary>Provides generic serialization test functionality.</summary>
public partial class GenericSerializationTest
{
    /// <summary>Provides generic class functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public class GenericClass<T>
    {
        /// <summary>Gets or sets the value.</summary>
        public T Value { get; set; }
        /// <summary>Gets or sets the list.</summary>
        public List<T> List { get; set; }
        /// <summary>Gets or sets the generic class2.</summary>
        public GenericClass2<int> GenericClass2 { get; set; }
        /// <summary>Gets or sets the set.</summary>
        public IReadOnlySet<int> Set { get; set; }
    }

    /// <summary>Provides generic class2 functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public class GenericClass2<T> where T : unmanaged
    {
        /// <summary>Gets or sets the value.</summary>
        public T Value { get; set; }
        /// <summary>Gets or sets the value2.</summary>
        public T Value2 { get; set; }
    }

    // Abstract generic base type.
    /// <summary>Provides abstract generic base functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    [LuminPackUnion(3, typeof(NumericGenericClass<double>))]
    [LuminPackUnion(4, typeof(NumericGenericClass<int>))]
    public abstract partial class AbstractGenericBase<T>
    {
        /// <summary>Gets or sets the base value.</summary>
        [LuminPackOrder(0)]
        public T BaseValue { get; set; }

        /// <summary>Obtains the type info for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public abstract string GetTypeInfo();
    }

    // Include this type in discovery.
    /// <summary>Provides concrete generic class functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public partial class ConcreteGenericClass<T> : AbstractGenericBase<T>
    {
        /// <summary>Gets or sets the additional value.</summary>
        [LuminPackOrder(2)]
        public T AdditionalValue { get; set; }

        /// <summary>Gets or sets the items.</summary>
        [LuminPackOrder(3)]
        public List<T> Items { get; set; }

        /// <summary>Obtains the type info for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public override string GetTypeInfo()
        {
            return $"ConcreteGenericClass<{typeof(T).Name}>";
        }
    }

    // Include this type in discovery.
    /// <summary>Provides concrete generic class2 functionality.</summary>
    [LuminPackable]
    public partial class ConcreteGenericClass2 : AbstractGenericBase<int>
    {

        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Obtains the type info for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public override string GetTypeInfo()
        {
            return $"ConcreteGenericClass<int>";
        }
    }

    // Second concrete implementation with generic constraints.
    // Exclude this type from discovery.
    /// <summary>Provides numeric generic class functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public partial class NumericGenericClass<T> : AbstractGenericBase<T> where T : struct
    {
        /// <summary>Gets or sets the min value.</summary>
        [LuminPackOrder(2)]
        public T MinValue { get; set; }

        /// <summary>Gets or sets the max value.</summary>
        [LuminPackOrder(3)]
        public T MaxValue { get; set; }

        /// <summary>Gets or sets the range.</summary>
        [LuminPackOrder(4)]
        public T[] Range { get; set; }

        /// <summary>Obtains the type info for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        public override string GetTypeInfo()
        {
            return $"NumericGenericClass<{typeof(T).Name}>";
        }
    }

    /// <summary>Verifies test generic int serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericIntSerialization(List<string> output)
    {
        try
        {
            var genericObj = new GenericClass<int>
            {
                Value = 42,
                List = new List<int> { 1, 2, 3, 4, 5 },
                GenericClass2 = new GenericClass2<int> { Value = 100, Value2 = 200 },
                Set = new HashSet<int> { 10, 20, 30 }
            };

            var buf = LuminPackSerializer.Serialize(genericObj);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass<int>>(buf);

            if (deserializedObj != null &&
                deserializedObj.Value == 42 &&
                deserializedObj.List?.Count == 5 &&
                deserializedObj.List[0] == 1 &&
                deserializedObj.GenericClass2?.Value == 100 &&
                deserializedObj.GenericClass2?.Value2 == 200 &&
                deserializedObj.Set?.Contains(10) == true)
            {
                output.Add("✓ TestGenericIntSerialization - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericIntSerialization - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericIntSerialization - ERROR: {ex.Message}");
        }
    }

    /// <summary>Provides simple generic functionality.</summary>
    [LuminPackable]
    public class SimpleGeneric
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets the age.</summary>
        public int Age { get; set; }
    }

    /// <summary>Verifies test generic class serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericClassSerialization(List<string> output)
    {
        try
        {
            var genericObj = new GenericClass<SimpleGeneric>
            {
                Value = new SimpleGeneric { Name = "Test", Age = 25 },
                List = new List<SimpleGeneric>
                {
                    new SimpleGeneric { Name = "Item1", Age = 1 },
                    new SimpleGeneric { Name = "Item2", Age = 2 }
                },
                GenericClass2 = new GenericClass2<int> { Value = 999, Value2 = 888 },
                Set = new HashSet<int> { 100, 200, 300 }
            };

            var buf = LuminPackSerializer.Serialize(genericObj);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass<SimpleGeneric>>(buf);

            if (deserializedObj != null &&
                deserializedObj.Value?.Name == "Test" &&
                deserializedObj.Value?.Age == 25 &&
                deserializedObj.List?.Count == 2 &&
                deserializedObj.List[0].Name == "Item1" &&
                deserializedObj.GenericClass2?.Value == 999 &&
                deserializedObj.Set?.Count == 3)
            {
                output.Add("✓ TestGenericClassSerialization - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericClassSerialization - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericClassSerialization - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test generic double serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericDoubleSerialization(List<string> output)
    {
        try
        {
            var genericObj = new GenericClass<double>
            {
                Value = 3.14159,
                List = new List<double> { 1.1, 2.2, 3.3 },
                GenericClass2 = new GenericClass2<int> { Value = 50, Value2 = 60 },
                Set = new HashSet<int> { 5, 15, 25 }
            };

            var buf = LuminPackSerializer.Serialize(genericObj);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass<double>>(buf);

            if (deserializedObj != null &&
                Math.Abs(deserializedObj.Value - 3.14159) < 0.00001 &&
                deserializedObj.List?.Count == 3 &&
                Math.Abs(deserializedObj.List[0] - 1.1) < 0.00001 &&
                deserializedObj.GenericClass2?.Value == 50)
            {
                output.Add("✓ TestGenericDoubleSerialization - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericDoubleSerialization - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericDoubleSerialization - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test nested generic serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestNestedGenericSerialization(List<string> output)
    {
        try
        {
            var nestedGeneric = new GenericClass<GenericClass<int>>
            {
                Value = new GenericClass<int>
                {
                    Value = 100,
                    List = new List<int> { 10, 20 },
                    GenericClass2 = new GenericClass2<int> { Value = 1, Value2 = 2 },
                    Set = new HashSet<int> { 1000 }
                },
                List = new List<GenericClass<int>>
                {
                    new GenericClass<int> { Value = 1, List = new List<int> { 1 } },
                    new GenericClass<int> { Value = 2, List = new List<int> { 2 } }
                },
                GenericClass2 = new GenericClass2<int> { Value = 9999, Value2 = 8888 },
                Set = new HashSet<int> { 111, 222 }
            };

            var buf = LuminPackSerializer.Serialize(nestedGeneric);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass<GenericClass<int>>>(buf);

            if (deserializedObj != null &&
                deserializedObj.Value?.Value == 100 &&
                deserializedObj.Value?.List?.Count == 2 &&
                deserializedObj.List?.Count == 2 &&
                deserializedObj.GenericClass2?.Value == 9999)
            {
                output.Add("✓ TestNestedGenericSerialization - PASSED");
            }
            else
            {
                output.Add("✗ TestNestedGenericSerialization - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestNestedGenericSerialization - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test generic class2 serialization.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericClass2Serialization(List<string> output)
    {
        try
        {
            var genericObj = new GenericClass2<decimal>
            {
                Value = 123.456m,
                Value2 = 789.012m
            };

            var buf = LuminPackSerializer.Serialize(genericObj);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass2<decimal>>(buf);

            if (deserializedObj != null &&
                deserializedObj.Value == 123.456m &&
                deserializedObj.Value2 == 789.012m)
            {
                output.Add("✓ TestGenericClass2Serialization - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericClass2Serialization - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericClass2Serialization - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test generic polymorphism with string.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericPolymorphismWithString(List<string> output)
    {
        try
        {
            AbstractGenericBase<string> polyObj = new ConcreteGenericClass<string>
            {
                BaseValue = "BaseString",
                AdditionalValue = "AdditionalString",
                Items = new List<string> { "Item1", "Item2", "Item3" }
            };

            var buf = LuminPackSerializer.Serialize(polyObj);
            var deserializedObj = LuminPackSerializer.Deserialize<ConcreteGenericClass<string>>(buf);

            if (deserializedObj != null &&
                deserializedObj.BaseValue == "BaseString" &&
                deserializedObj.AdditionalValue == "AdditionalString" &&
                deserializedObj.Items?.Count == 3 &&
                deserializedObj.Items[0] == "Item1" &&
                deserializedObj.GetTypeInfo() == "ConcreteGenericClass<String>")
            {
                output.Add("✓ TestGenericPolymorphismWithString - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericPolymorphismWithString - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericPolymorphismWithString - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test generic polymorphism with int.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestGenericPolymorphismWithInt(List<string> output)
    {
        try
        {
            AbstractGenericBase<int> polyObj = new ConcreteGenericClass<int>
            {
                BaseValue = 100,
                AdditionalValue = 200,
                Items = new List<int> { 1, 2, 3, 4, 5 }
            };

            var buf = LuminPackSerializer.Serialize(polyObj);
            var deserializedObj = LuminPackSerializer.Deserialize<ConcreteGenericClass<int>>(buf);

            if (deserializedObj != null &&
                deserializedObj.BaseValue == 100 &&
                deserializedObj.AdditionalValue == 200 &&
                deserializedObj.Items?.Count == 5 &&
                deserializedObj.Items[0] == 1 &&
                deserializedObj.GetTypeInfo() == "ConcreteGenericClass<Int32>")
            {
                output.Add("✓ TestGenericPolymorphismWithInt - PASSED");
            }
            else
            {
                output.Add("✗ TestGenericPolymorphismWithInt - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestGenericPolymorphismWithInt - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test numeric generic polymorphism.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestNumericGenericPolymorphism(List<string> output)
    {
        try
        {
            AbstractGenericBase<double> numericObj = new NumericGenericClass<double>
            {
                BaseValue = 50.5,
                MinValue = 0.0,
                MaxValue = 100.0,
                Range = new double[] { 0.0, 25.0, 50.0, 75.0, 100.0 }
            };

            var buf = LuminPackSerializer.Serialize(numericObj);
            var deserializedObj = LuminPackSerializer.Deserialize<NumericGenericClass<double>>(buf);

            if (deserializedObj != null &&
                Math.Abs(deserializedObj.BaseValue - 50.5) < 0.00001 &&
                Math.Abs(deserializedObj.MinValue - 0.0) < 0.00001 &&
                Math.Abs(deserializedObj.MaxValue - 100.0) < 0.00001 &&
                deserializedObj.Range?.Length == 5 &&
                Math.Abs(deserializedObj.Range[2] - 50.0) < 0.00001 &&
                deserializedObj.GetTypeInfo() == "NumericGenericClass<Double>")
            {
                output.Add("✓ TestNumericGenericPolymorphism - PASSED");
            }
            else
            {
                output.Add("✗ TestNumericGenericPolymorphism - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestNumericGenericPolymorphism - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies test complex generic polymorphism.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void TestComplexGenericPolymorphism(List<string> output)
    {
        try
        {
            // Cover polymorphic generic members inside a generic class.
            var container = new GenericClass<AbstractGenericBase<int>>
            {
                Value = new ConcreteGenericClass<int>
                {
                    BaseValue = 999,
                    AdditionalValue = 888,
                    Items = new List<int> { 1, 2, 3 }
                },
                List = new List<AbstractGenericBase<int>>
                {
                    new ConcreteGenericClass<int> { BaseValue = 1, AdditionalValue = 2 },
                    new NumericGenericClass<int> { BaseValue = 10, MinValue = 0, MaxValue = 20 }
                },
                GenericClass2 = new GenericClass2<int> { Value = 555, Value2 = 666 },
                Set = new HashSet<int> { 100, 200 }
            };

            var buf = LuminPackSerializer.Serialize(container);
            var deserializedObj = LuminPackSerializer.Deserialize<GenericClass<AbstractGenericBase<int>>>(buf);

            if (deserializedObj != null &&
                deserializedObj.Value?.BaseValue == 999 &&
                deserializedObj.List?.Count == 2 &&
                deserializedObj.GenericClass2?.Value == 555)
            {
                output.Add("✓ TestComplexGenericPolymorphism - PASSED");
            }
            else
            {
                output.Add("✗ TestComplexGenericPolymorphism - FAILED");
            }
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestComplexGenericPolymorphism - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies run all tests.</summary>
    /// <param name="output">The destination receiving the result.</param>
    public static void RunAllTests(List<string> output)
    {
        TestGenericIntSerialization(output);
        TestGenericClassSerialization(output);
        TestGenericDoubleSerialization(output);
        TestNestedGenericSerialization(output);
        TestGenericClass2Serialization(output);
        TestGenericPolymorphismWithString(output);
        TestGenericPolymorphismWithInt(output);
        TestNumericGenericPolymorphism(output);
        TestComplexGenericPolymorphism(output);
    }
}
