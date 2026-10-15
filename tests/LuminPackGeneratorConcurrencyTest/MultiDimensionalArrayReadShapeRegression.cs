using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides multi dimensional array read shape regression functionality.</summary>
internal static class MultiDimensionalArrayReadShapeRegression
{
    /// <summary>Verifies run.</summary>
    public static void Run()
    {
        AssertShape("global::System.Int32[,]");
        AssertShape("global::System.Int32[,,]");
    }

    /// <summary>Verifies assert shape.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AssertShape(string typeName)
    {
        var builder = new StringBuilder();
        MultiDimensionalArrayEmitter.GenerateDeserializeCode(
            new LuminLocalFieldData { TypeName = typeName }, builder);
        string read = builder.ToString();

        AssertContains(read, "ThrowNegativeMultiDimensionalArrayDimension()", typeName);
        AssertContains(read, "ThrowMultiDimensionalArrayDimensionsTooLarge()", typeName);
        AssertContains(read, "ThrowMultiDimensionalArrayLengthMismatch()", typeName);
        if (read.Contains("throw new global::System.FormatException", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                typeName + " reintroduced inline exception construction into the generated ReadValue body." +
                Environment.NewLine + read);
        }
    }

    /// <summary>Verifies assert contains.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertContains(string text, string expected, string scenario)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                scenario + " is missing the outlined multidimensional-array validation call '" + expected + "'." +
                Environment.NewLine + text);
        }
    }
}
