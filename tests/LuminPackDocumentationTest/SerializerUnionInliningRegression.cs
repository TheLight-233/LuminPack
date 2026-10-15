using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks direct known union cases against the retained virtual and generic formatter contract.</summary>
internal static class SerializerUnionInliningRegression
{
    /// <summary>Verifies class/interface cases, wide tags, inherited fallback, closed generic precedence, and callback order.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Utility;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable, LuminPackUnion(10, typeof(Cat)), LuminPackUnion(270, typeof(Dog)), LuminPackUnion(12, typeof(Kitten))]
            public abstract partial class Animal
            {
                [LuminPackOnSerializing] public void Before() => Entry.Events.Add("before");
                [LuminPackOnSerialized] public void After() => Entry.Events.Add("after");
            }
            [LuminPackable] public partial class Cat : Animal { public string Name = "cat"; public Dictionary<int,int> Values = new() { [3] = 7 }; }
            [LuminPackable] public sealed partial class Kitten : Cat { public int Tail = 11; }
            [LuminPackable] public sealed partial class Dog : Animal { public int Number = 13; }
            public sealed class UnknownCat : Cat { }
            [LuminPackable, LuminPackUnion(4, typeof(Point)), LuminPackUnion(5, typeof(Circle))]
            public partial interface IShape { }
            [LuminPackable] public partial struct Point : IShape { public int X; public int Y; }
            [LuminPackable] public sealed partial class Circle : IShape { public int Radius = 17; }
            [LuminPackable, LuminPackUnion(1, typeof(Envelope<>)), LuminPackUnion(7, typeof(Envelope<int>)), LuminPackUnion(9, typeof(NumberEnvelope<int>))]
            public abstract partial class EnvelopeBase<T> { public T BaseItem { get; set; } }
            [LuminPackable] public partial class Envelope<T> : EnvelopeBase<T> { public T Item; public List<T> Items; }
            [LuminPackable] public partial class NumberEnvelope<T> : EnvelopeBase<T> where T : struct { public T Number { get; set; } }
            public static class Entry
            {
                public static readonly List<string> Events = new();
                public static int Check()
                {
                    int cases = 0;
                    foreach (Animal value in new Animal[] { null!, new Cat(), new Dog(), new Kitten(), new UnknownCat() })
                    {
                        Events.Clear();
                        var expected = LuminPackSerializer.Serialize<Animal>(in value);
                        string callbacks = string.Join(",", Events);
                        Events.Clear();
                        var actual = LuminPackSerializer.Serialize(in value);
                        if (!actual.AsSpan().SequenceEqual(expected) || string.Join(",", Events) != callbacks)
                            throw new Exception("Class union wire or callback order changed.");
                        if (value is not UnknownCat && LuminPackSerializer.Sizeof(value) != LuminPackSerializer.Sizeof<Animal>(value))
                            throw new Exception("Union size evaluation changed.");
                        foreach (var encoding in new[] { LuminPack.Option.LuminPackStringEncoding.UTF8, LuminPack.Option.LuminPackStringEncoding.UTF16 })
                        {
                            var option = new LuminPack.Option.LuminPackSerializerOption { StringEncoding = encoding };
                            Events.Clear();
                            string expectedJson = LuminPackSerializer.SerializeJson<Animal>(value, option);
                            callbacks = string.Join(",", Events);
                            Events.Clear();
                            string actualJson = LuminPackSerializer.SerializeJson(value, option);
                            if (actualJson != expectedJson || callbacks != string.Join(",", Events)) throw new Exception("Union JSON wire or callbacks changed.");
                            Animal jsonValue = null!;
                            LuminPackSerializer.DeserializeJson(actualJson, ref jsonValue, option);
                            if (value is null ? jsonValue is not null : jsonValue is null) throw new Exception("Union JSON null changed.");
                        }
                        var buffer = LuminBufferWriterPool.Rent();
                        try
                        {
                            Events.Clear();
                            LuminPackSerializer.Serialize(in value, buffer);
                            if (!buffer.GetSpan().SequenceEqual(expected))
                                throw new Exception("Buffered class union changed.");
                        }
                        finally { LuminBufferWriterPool.Return(buffer); }
                        Animal restored = null!;
                        LuminPackSerializer.Deserialize(actual.AsSpan(), ref restored);
                        if (value is null ? restored is not null : restored is null) throw new Exception("Union null framing changed.");
                        cases++;
                    }
                    foreach (IShape value in new IShape[] { null!, new Point { X = 19, Y = 23 }, new Circle() })
                    {
                        var expected = LuminPackSerializer.Serialize<IShape>(in value);
                        var actual = LuminPackSerializer.Serialize(in value);
                        if (!actual.AsSpan().SequenceEqual(expected)) throw new Exception("Interface union wire changed.");
                        IShape restored = null!;
                        LuminPackSerializer.Deserialize(actual.AsSpan(), ref restored);
                        if (value is Point && restored is not Point { X: 19, Y: 23 }) throw new Exception("Struct interface case changed.");
                        cases++;
                    }
                    EnvelopeBase<int> integer = new Envelope<int> { Item = 29, Items = new() { 31, 37 } };
                    var integerBytes = LuminPackSerializer.Serialize(in integer);
                    if (!integerBytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize<EnvelopeBase<int>>(in integer)) || integerBytes[0] != 7)
                        throw new Exception("Closed generic registration lost precedence.");
                    EnvelopeBase<string> text = new Envelope<string> { Item = "open", Items = new() { "case" } };
                    var textBytes = LuminPackSerializer.Serialize(in text);
                    if (!textBytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize<EnvelopeBase<string>>(in text)) || textBytes[0] != 1)
                        throw new Exception("Open generic fallback changed.");
                    if (LuminPackSerializer.Deserialize<EnvelopeBase<string>>(textBytes) is not Envelope<string> { Item: "open" })
                        throw new Exception("Generic union read changed.");
                    EnvelopeBase<int> number = new NumberEnvelope<int> { Number = 43, BaseItem = 47 };
                    var numberBytes = LuminPackSerializer.Serialize(in number);
                    if (!numberBytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize<EnvelopeBase<int>>(in number)) || numberBytes[0] != 9)
                        throw new Exception("Differently constrained union mirror families collided.");
                    if (LuminPackSerializer.Deserialize<EnvelopeBase<int>>(numberBytes) is not NumberEnvelope<int> { Number: 43, BaseItem: 47 })
                        throw new Exception("Constrained union mirror state changed.");
                    return cases + 3;
                }
            }
            """;
        var input = CSharpCompilation.Create("DirectUnionEntries", [CSharpSyntaxTree.ParseText(source, parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors));
        int entries = 0, jsonEntries = 0, sizeEntries = 0;
        foreach (var tree in output.SyntaxTrees.Where(tree => Path.GetFileName(tree.FilePath).StartsWith("LuminPackSerializer.", StringComparison.Ordinal)))
        {
            var model = output.GetSemanticModel(tree);
            foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.TypeParameterList == null &&
                method.Identifier.ValueText is "Serialize" or "SerializeJson" or "Sizeof" &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is
                    "global::Animal" or "global::IShape" or "global::EnvelopeBase<int>" or "global::EnvelopeBase<string>")))
            {
                string name = method.Identifier.ValueText;
                if (name == "Serialize") entries++;
                else if (name == "SerializeJson") jsonEntries++;
                else sizeEntries++;

                var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
                bool perTypeFormatter = calls.Any(call => (model.GetSymbolInfo(call).Symbol as IMethodSymbol) is { } symbol &&
                    symbol.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));

                // Contract: binary, size, and JSON entries all inline down to their boundary. The union
                // JSON entry expands in place and forwards only to its hidden virtual union method, so
                // none of them may retain a shared per-type formatter call.
                Require(!perTypeFormatter, "A union entry retained a formatter call: " + method);

                foreach (var call in calls)
                {
                    if (call.Expression is MemberAccessExpressionSyntax lookup)
                    {
                        Require(lookup.Name.Identifier.ValueText is not ("GetType" or "GetMethodTable" or "TryGetValue"),
                            "A concrete union entry retained a GetType/MethodTable/TryGetValue dispatch lookup: " + call);
                    }
                }

                Require(!method.DescendantNodes().OfType<SwitchStatementSyntax>().Any(statement =>
                        statement.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(identifier =>
                            identifier.Identifier.ValueText.Contains("CaseMap", StringComparison.Ordinal)) ||
                        statement.Expression.ToString().Contains("GetMethodTable", StringComparison.Ordinal)),
                    "A concrete union entry retained a switch-on-tag lookup dispatch: " + name);

                if (name is "Serialize" or "Sizeof")
                {
                    Require(calls.Any(call => call.Expression is MemberAccessExpressionSyntax forward &&
                        forward.Name.Identifier.ValueText.StartsWith("__LuminPackUnion", StringComparison.Ordinal)),
                        "A concrete union write/size entry lost its single virtual union forwarding layer: " + name);
                }
            }
        }
        Require(entries == 5, "Expected two class, one interface, and two closed generic concrete write entries.");
        Require(jsonEntries != 0 && sizeEntries != 0, "Union JSON and size entries were not inspected.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        Require((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == 11, "Union execution matrix was incomplete.");
        Console.WriteLine("Passed direct class/interface union cases, wide tags, inherited virtual fallback, closed/open generic priority, callback order, and generic wire parity.");
    }

    /// <summary>Reports a union expansion regression.</summary>
    /// <param name="condition">The required contract.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
