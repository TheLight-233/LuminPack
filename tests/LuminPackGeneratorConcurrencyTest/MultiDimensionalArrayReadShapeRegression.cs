using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;

internal static class MultiDimensionalArrayReadShapeRegression
{
    public static void Run()
    {
        AssertShape("global::System.Int32[,]");
        AssertShape("global::System.Int32[,,]");
    }

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
