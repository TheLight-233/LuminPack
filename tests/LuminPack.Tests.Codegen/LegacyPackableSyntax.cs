using Microsoft.CodeAnalysis.CSharp;

/// <summary>Supplies the frozen marker contract to tests that compile their inputs with the legacy generator.</summary>
internal static class LegacyPackableSyntax
{
    internal const string FileName = "LegacyPackableAttribute.cs";

    /// <summary>Adds a source declaration that binds the historical marker constructor for a frozen fixture.</summary>
    /// <param name="compilation">The fixture compilation using the current runtime metadata.</param>
    /// <returns>The fixture with its historical attribute declaration.</returns>
    internal static CSharpCompilation WithLegacyPackableSyntax(this CSharpCompilation compilation) =>
        compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Source, (CSharpParseOptions)compilation.SyntaxTrees.First().Options, FileName));

    private const string Source = """
        namespace LuminPack.Attribute
        {
            internal enum GeneratorType : byte
            {
                Object, VersionTolerant, CircleReference, NonGenerator, Custom,
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct |
                System.AttributeTargets.Interface | System.AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
            internal sealed class LuminPackableAttribute : System.Attribute
            {
                public LuminPackableAttribute(GeneratorType generatorType = GeneratorType.Object) { }
                public LuminPackableAttribute(System.Type targetType) { }
            }
        }
        """;
}
