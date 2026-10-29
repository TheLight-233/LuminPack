using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks independent model versions and generator diagnostics for named binary contracts.</summary>
internal static class SerializerNamedVersionRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Compiles independent versions with identical CLR names and checks nested bidirectional evolution.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyLengthTableBoundaries();
        const string template = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            namespace VersionModels;
            [LuminPackable(GeneratorType.VersionTolerant)] public class Child { CHILD }
            [LuminPackable(GeneratorType.VersionTolerant)] public class Root { ROOT }
            [LuminPackable(GeneratorType.VersionTolerant)] public class Envelope<T> { public T Value; }
            [LuminPackable(GeneratorType.VersionTolerant)] public class TypeShapes
            {
                public Guid Id; public DateTime Date; public DateTimeOffset Offset; public TimeSpan Duration;
                public nint Native; public nuint UnsignedNative; public int? Optional;
                public (long Number, string Text) Pair; public Child[,] Grid;
                public Dictionary<string, List<Child[]>> Nested;
            }
            public static class Entry
            {
                public static byte[] WriteShapes() => LuminPackSerializer.Serialize(new TypeShapes());
                public static byte[] Write() => LuminPackSerializer.Serialize(new Envelope<Root> { Value = new Root
                {
                    Id = 17, Child = new Child { Number = 19, TEXT = "nested" },
                    Children = new List<Child> { new Child { Number = 23, TEXT = "list" } }
                }});
                public static string Read(byte[] bytes)
                {
                    var value = LuminPackSerializer.Deserialize<Envelope<Root>>(bytes).Value;
                    return value.Id + ":" + value.Child.Number + ":" + value.Child.TEXT + ":" + value.Children[0].Number + ":" + value.Children[0].TEXT;
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            string oldSource = template.Replace("MODE", mode)
                .Replace("CHILD", "public int Number; public string Name; public string Removed;")
                .Replace("ROOT", "public int Id; public string Removed; public Child Child; public List<Child> Children;")
                .Replace("TEXT", "Name");
            string newSource = template.Replace("MODE", mode)
                .Replace("CHILD", "public bool Added; [LuminPackOldName(\"Name\")] public string Display; public int Number;")
                .Replace("ROOT", "public List<Child> Children; public long Added; public Child Child; public int Id;")
                .Replace("TEXT", "Display");
            var oldEntry = Compile(oldSource, "NamedOld" + mode, references).GetType("VersionModels.Entry")!;
            var newEntry = Compile(newSource, "NamedNew" + mode, references).GetType("VersionModels.Entry")!;
            byte[] oldBytes = (byte[])oldEntry.GetMethod("Write")!.Invoke(null, null)!;
            byte[] newBytes = (byte[])newEntry.GetMethod("Write")!.Invoke(null, null)!;
            const string expected = "17:19:nested:23:list";
            Require((string)newEntry.GetMethod("Read")!.Invoke(null, [oldBytes])! == expected, "Nested forward evolution failed in " + mode);
            Require((string)oldEntry.GetMethod("Read")!.Invoke(null, [newBytes])! == expected, "Nested backward evolution failed in " + mode);
        }
        foreach (string members in new[]
        {
            "[LuminPackOldName(\"\")] public int A;",
            "[LuminPackOldName(null)] public int A;",
            "[LuminPackOldName(\"A\")] public int A;",
            "[LuminPackOldName(\"B\")] public int A; public int B;",
            "[LuminPackOldName(\"old\")] public int A; [LuminPackOldName(\"old\")] public int B;"
        })
        {
            string source = "using LuminPack.Attribute; [LuminPackable(GeneratorType.VersionTolerant)] public class Invalid { " + members + " }";
            var input = CSharpCompilation.Create("InvalidNames", [CSharpSyntaxTree.ParseText(source, Parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var result = NewDriver().RunGenerators(input).GetRunResult();
            Require(result.Results.All(item => item.Exception == null) && result.Diagnostics.Any(item => item.Id == "LuminPack062"),
                "Missing alias diagnostic for " + members);
        }
        Console.WriteLine("Passed named nested evolution across independent Full/Minimal assemblies and invalid alias diagnostics.");
    }

    /// <summary>Checks bulk table validation rechecks remaining entries after an extended VarInt.</summary>
    private static void VerifyLengthTableBoundaries()
    {
        var bytes = new byte[512];
        Span<byte> output = bytes;
        var writer = new LuminPack.Core.LuminPackWriter(ref output);
        writer.WriteVarInt(300);
        int firstEnd = writer.Position;
        writer.WriteVarInt(4);
        int tableEnd = writer.Position;
        Span<byte> input = bytes.AsSpan(0, tableEnd + 304);
        var reader = new LuminPack.Core.LuminPackReader(ref input);
        Span<int> lengths = stackalloc int[2];
        reader.ReadVersionLengths(lengths);
        Require(lengths[0] == 300 && lengths[1] == 4 && reader.Position == tableEnd,
            "Extended length entries or their combined payload boundary changed.");
        var known = new LuminPack.Core.LuminPackReader(ref input);
        known.ValidateVersionPayloadSize(2);
        int first = known.ReadVersionLength(1);
        int second = known.ReadVersionLength(0);
        known.ValidateVersionPayloadSize((long)first + second);
        Require(first == 300 && second == 4 && known.Position == tableEnd,
            "Generated scalar lengths differ from the mapped length table.");
        bool rejected = false;
        try
        {
            Span<byte> truncated = bytes.AsSpan(0, firstEnd);
            var incomplete = new LuminPack.Core.LuminPackReader(ref truncated);
            incomplete.ReadVersionLengths(lengths);
        }
        catch (Exception) { rejected = true; }
        Require(rejected, "An extended length consumed the bytes required by a subsequent table entry.");
        rejected = false;
        try
        {
            Span<byte> truncated = bytes.AsSpan(0, firstEnd);
            var incomplete = new LuminPack.Core.LuminPackReader(ref truncated);
            incomplete.ValidateVersionPayloadSize(2);
            incomplete.ReadVersionLength(1);
        }
        catch (Exception) { rejected = true; }
        Require(rejected, "A generated extended length consumed a subsequent table entry.");
    }

    /// <summary>Generates, compiles and loads a single independent model version.</summary>
    /// <param name="source">The version's model and entry points.</param>
    /// <param name="name">The unique assembly identity.</param>
    /// <param name="references">Runtime and platform references.</param>
    /// <returns>The independently loaded model assembly.</returns>
    private static Assembly Compile(string source, string name, MetadataReference[] references)
    {
        var input = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, Parse, name + ".cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Require(driver.GetRunResult().Results.All(item => item.Exception == null) && errors.Length == 0,
            string.Join(Environment.NewLine, errors.Select(item => item.ToString()).Concat(driver.GetRunResult().Results.Select(item => item.Exception?.ToString()))));
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(image.ToArray());
        VerifyMetadataConstants(driver, assembly);
        return assembly;
    }

    /// <summary>Checks compile-time metadata against the runtime encoder for complete names and closed type shapes.</summary>
    /// <param name="driver">The completed model generation.</param>
    /// <param name="assembly">The independently emitted models and caches.</param>
    private static void VerifyMetadataConstants(GeneratorDriver driver, Assembly assembly)
    {
        int checkedTables = 0;
        foreach (var source in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
        {
            foreach (var helper in source.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(type => type.Identifier.ValueText.StartsWith("Version_", StringComparison.Ordinal)))
            {
                var property = helper.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(member => member.Identifier.ValueText == "Metadata");
                if (property?.ExpressionBody?.Expression is not CollectionExpressionSyntax collection) continue;
                var type = assembly.GetTypes().FirstOrDefault(candidate => candidate.Name == helper.Identifier.ValueText && !candidate.ContainsGenericParameters);
                if (type == null) continue;
                var fields = (LuminPack.Core.LuminPackVersionField[])type.GetField("Fields", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                byte[] expected = LuminPack.Core.LuminPackVersionField.CreateMetadata(fields);
                byte[] actual = collection.Elements.Cast<ExpressionElementSyntax>().Select(element => (byte)(int)((LiteralExpressionSyntax)element.Expression).Token.Value!).ToArray();
                Require(actual.SequenceEqual(expected), "Constant and runtime type metadata differ for " + type.FullName);
                checkedTables++;
            }
        }
        Require(checkedTables >= 3, "Closed metadata constants were not checked.");
    }

    /// <summary>Creates an isolated driver for one compilation.</summary>
    /// <returns>The current serialization and mapping generators.</returns>
    private static GeneratorDriver NewDriver() => CSharpGeneratorDriver.Create(
        [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);

    /// <summary>Reports an evolution or generator regression.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
