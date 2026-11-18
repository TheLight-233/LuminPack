using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks that compound collection loops forward the original state and only compound nested members are outlined.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunTraversalBoundaries(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [LuminPackable] public sealed class FlatTraversalChild { public int Number; public string Text; }
            [LuminPackable] public sealed class CompoundTraversalChild { public List<string> Items; }
            [LuminPackable] public sealed class TraversalParent
            {
                public FlatTraversalChild Flat;
                public CompoundTraversalChild Compound;
            }
            public static class Entry
            {
                public static void Check()
                {
                    List<TraversalParent> source = new()
                    {
                        new() { Flat = new() { Number = 17, Text = "text" }, Compound = new() { Items = new() { "two", "five" } } },
                        new() { Flat = null, Compound = null },
                    };
                    byte[] bytes = LuminPackSerializer.Serialize(source);
                    List<TraversalParent> copy = null;
                    for (int i = 0; i < 2; ++i)
                    {
                        if (LuminPackSerializer.Deserialize(bytes, ref copy) != bytes.Length ||
                            copy is not [{ Flat: { Number: 17, Text: "text" }, Compound.Items: ["two", "five"] }, { Flat: null, Compound: null }])
                            throw new Exception("Compound collection traversal lost framing or reused member storage.");
                    }
                }
            }
            """;
        Execute(references, source, "TraversalBoundaries", Inspect);
        Console.WriteLine("Passed compact and compound nested member boundaries, collection loop shapes, and fresh and reused reads.");

        static void Inspect(GeneratorDriverRunResult result)
        {
            var owners = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()).ToArray();
            var parent = owners.Single(owner => owner.Identifier.ValueText == "TraversalParentFormatter" &&
                owner.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Write" && method.Body != null));
            foreach (string direction in new[] { "Write", "Read" })
            {
                var operation = parent.Members.OfType<MethodDeclarationSyntax>()
                    .Single(method => method.Identifier.ValueText == direction && method.Body != null);
                var helpers = operation.DescendantNodes().OfType<LocalFunctionStatementSyntax>()
                    .Where(method => method.Identifier.ValueText.StartsWith(direction + "Nested", StringComparison.Ordinal)).ToArray();
                if (helpers.Length != 1 || !helpers[0].ToString().Contains("global::CompoundTraversalChild", StringComparison.Ordinal) ||
                    !helpers[0].AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal))
                    throw new InvalidOperationException(direction + " outlined the wrong nested member: " +
                        string.Join(", ", helpers.Select(helper => helper.Identifier.ValueText + helper.ParameterList)) + Environment.NewLine + operation);
            }

            var collection = owners.Single(owner =>
                (owner.Identifier.ValueText == "ListOfTraversalParentFormatter" ||
                    owner.Identifier.ValueText.StartsWith("ListOfElementFormatter", StringComparison.Ordinal)) &&
                owner.Members.OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Write" && method.Body != null));
            var write = collection.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Write" && method.Body != null);
            var read = collection.Members.OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "Read" && method.Body != null);
            if (write.ToString().Contains("loopWriter", StringComparison.Ordinal) || write.DescendantNodes().OfType<TryStatementSyntax>().Any() ||
                read.ToString().Contains("loopReader", StringComparison.Ordinal))
                throw new InvalidOperationException("A compound collection staged the formatter's operation state.");
        }
    }
}
