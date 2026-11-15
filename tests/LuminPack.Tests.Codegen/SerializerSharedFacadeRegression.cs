using System.Runtime.Loader;
using System.Numerics;
using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks shared convenience bodies, concrete adapters and incremental support selection.</summary>
internal static class SerializerSharedFacadeRegression
{
    /// <summary>Compiles modern and compatibility facades with identical APIs and operation semantics.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyPrivateIdentities();
        foreach (bool modern in new[] { true, false }) Verify(references, modern);
        Console.WriteLine("Passed shared facade signatures, direct adapters, scalar results, async/profile behavior, raw sequences and retained/fresh selection.");
    }

    /// <summary>Preserves every existing identity bit while shortening private generated metadata names.</summary>
    private static void VerifyPrivateIdentities()
    {
        var encode = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.SourceGenerator.SharedFacadeProjection", throwOnError: true)!
            .GetMethod("EncodeIdentity", BindingFlags.NonPublic | BindingFlags.Static)!;
        var vectors = new List<byte[]> { new byte[16], Enumerable.Repeat((byte)255, 16).ToArray() };
        for (int bit = 0; bit < 128; bit++)
        {
            var value = new byte[16];
            value[bit / 8] = (byte)(1 << (bit % 8));
            vectors.Add(value);
        }
        var random = new Random(21473);
        for (int index = 0; index < 1024; index++)
        {
            var value = new byte[16];
            random.NextBytes(value);
            vectors.Add(value);
        }
        foreach (var bytes in vectors)
        {
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true) << 2;
            var expected = new char[26];
            const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
            for (int index = expected.Length - 1; index >= 0; index--)
            {
                value = BigInteger.DivRem(value, 32, out var digit);
                expected[index] = alphabet[(int)digit];
            }
            string hexadecimal = Convert.ToHexString(bytes);
            string actual = (string)encode.Invoke(null, [hexadecimal])!;
            Require(value.IsZero && actual == new string(expected) && actual == (string)encode.Invoke(null, [hexadecimal.ToLowerInvariant()])!,
                "A private facade identity lost bits, canonical padding or hexadecimal-case independence.");
        }
    }

    /// <summary>Exercises one target's call-selected complete facade and removal of those calls.</summary>
    /// <param name="references">The current platform and runtime references.</param>
    /// <param name="modern">Whether scoped state APIs and modern async templates are selected.</param>
    private static void Verify(MetadataReference[] references, bool modern)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose, preprocessorSymbols: modern
            ? ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"] : ["UNITY_2022_3_OR_NEWER"]);
        var settings = CSharpSyntaxTree.ParseText("[assembly: LuminPack.Attribute.LuminPackGeneratorOptions]", parse, "Settings.cs");
        var models = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Buffers;
            using System.IO;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [LuminPackable] public sealed class A { public int Id; public string Text = ""; }
            [LuminPackable] public sealed class B { public int Id; public string Note = ""; }
            [LuminPackable] public sealed class C { public int Id; public bool Flag; }
            public static class Entry
            {
                private static void Write(ref LuminPackWriter writer, in A value)
                    => LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(ref writer, in value);
                private static void Read(ref LuminPackReader reader, ref A value)
                    => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(ref reader, ref value);
                public static void Run()
                {
                    LuminPackSerializer.Register<A, LuminPackUtf16LengthOptions>(Write, Read);
                    var value = new A { Id = 17, Text = "\u00E9\u4E2D" };
                    var bytes = LuminPackSerializer.Serialize<A, LuminPackUtf16LengthOptions>(value);
                    var target = new A();
                    var original = target;
                    LuminPackSerializer.Deserialize<A, LuminPackUtf16LengthOptions>(bytes.AsSpan(), ref target);
                    if (!ReferenceEquals(original, target) || target.Id != 17 || target.Text != value.Text) throw new Exception("Profile reuse changed.");
                    string json = LuminPackSerializer.SerializeJson(value);
                    target = null;
                    LuminPackSerializer.DeserializeJson(json, ref target);
                    if (target.Id != 17 || target.Text != value.Text) throw new Exception("JSON changed.");
                    using var stream = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<A, LuminPackUtf16LengthOptions>(stream, value).GetAwaiter().GetResult();
                    if (!stream.ToArray().SequenceEqual(bytes)) throw new Exception("Async profile write changed.");
                    stream.Position = 0;
                    target = LuminPackSerializer.DeserializeAsync<A, LuminPackUtf16LengthOptions>(stream, value).GetAwaiter().GetResult();
                    if (ReferenceEquals(target, value) || target.Id != 17 || !stream.CanRead) throw new Exception("Async fresh read or ownership changed.");
                    var fixedValue = new C { Id = 5, Flag = true };
                    var fixedBytes = LuminPackSerializer.Serialize(fixedValue);
                    C fixedCopy = null;
                    if (LuminPackSerializer.Deserialize(fixedBytes.AsSpan(), ref fixedCopy) != fixedBytes.Length || fixedCopy.Id != 5 || !fixedCopy.Flag) throw new Exception("Fixed record round trip changed.");
                    byte tag = 7;
                    byte[] data = LuminPackSerializer.Serialize(tag);
                    byte[] array = LuminPackSerializer.Serialize(data);
                    byte[] restored = null;
                    LuminPackSerializer.Deserialize(array.AsSpan(), ref restored);
                    if (data.Length != 1 || data[0] != tag || !restored.SequenceEqual(data)) throw new Exception("Byte-array API result changed.");
                    int scalar = 0;
                    var sequence = new ReadOnlySequence<byte>(LuminPackSerializer.Serialize(23));
                    if (LuminPackSerializer.Deserialize(sequence, ref scalar) != 4 || scalar != 23) throw new Exception("Raw sequence changed.");
                    var tuple = (17, 19);
                    var tupleBytes = LuminPackSerializer.Serialize(tuple);
                    var tupleTarget = (0, 0);
                    LuminPackSerializer.Deserialize(tupleBytes.AsSpan(), ref tupleTarget);
                    if (tupleTarget != tuple) throw new Exception("Existing tuple fallback changed.");
                }
            }
            """, parse, "Models.cs");
        var selections = CSharpSyntaxTree.ParseText("using System; using System.IO; using System.Buffers; using LuminPack; using LuminPack.Utility; public static class SelectedApis {" +
            string.Join("\n", new[] { "A", "B" }.Select(type => $$"""
                public static void Select({{type}} value, LuminBufferWriter buffer, IBufferWriter<byte> output, Stream stream,
                    ReadOnlySpan<byte> bytes, ReadOnlySpan<char> chars, ReadOnlySequence<byte> sequence, string json)
                {
                    LuminPackSerializer.Serialize(value); LuminPackSerializer.Serialize(value, buffer); LuminPackSerializer.Serialize(value, output);
                    LuminPackSerializer.SerializeJson(value); LuminPackSerializer.SerializeJson(value, buffer); LuminPackSerializer.SerializeJson(value, output);
                    LuminPackSerializer.Deserialize(bytes, ref value); LuminPackSerializer.Deserialize(buffer, ref value); LuminPackSerializer.Deserialize(sequence, ref value);
                    LuminPackSerializer.DeserializeJson(json, ref value); LuminPackSerializer.DeserializeJson(bytes, ref value);
                    LuminPackSerializer.DeserializeJson(chars, ref value); LuminPackSerializer.DeserializeJson(buffer, ref value);
                    _ = LuminPackSerializer.SerializeAsync(stream, value); _ = LuminPackSerializer.DeserializeAsync(stream, value);
                }
                """)) + "}", parse, "Selections.cs");
        var input = CSharpCompilation.Create("SharedFacade_" + modern, [settings, models, selections], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        var driver = NewDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        CheckErrors(output, diagnostics);
        var methods = FacadeMethods(driver, parse);
        var facade = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" }).ToArray();
        foreach (string type in new[] { "global::A", "global::B" })
        {
            var concrete = facade.Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value" && parameter.Type?.ToString() == type)).ToArray();
            Require(concrete.Count(method => method.TypeParameterList == null) == 15 &&
                !concrete.Any(method => method.TypeParameterList?.Parameters.Count == 1 &&
                    method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.RefKeyword) != true),
                "The legacy facade changed default signatures or retained profile convenience overloads for " + type);
            var profiles = methods.Where(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText.StartsWith("LuminEntry_", StringComparison.Ordinal) &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value" && parameter.Type?.ToString() == type)).ToArray();
            Require(profiles.Length == 15 && profiles.All(method => method.Body!.Statements.Count == 1 && method.Body.ToString().Contains("LuminFacade_", StringComparison.Ordinal)),
                "A managed full facade retained repeated profile bodies.");
            var adapters = methods.Where(method => method.Parent is StructDeclarationSyntax && method.ParameterList.Parameters.LastOrDefault()?.Type?.ToString() == type).ToArray();
            // Record allocation widths do not belong to the shared operation contract.
            Require(adapters.Length == 4 && adapters.All(method =>
                method.ExpressionBody?.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } } &&
                generic.TypeArgumentList.Arguments.Count == 1), "Concrete adapters added generic payload dispatch.");
            Require(adapters.All(method => method.ParameterList.Parameters.All(parameter => parameter.Modifiers.Any(SyntaxKind.ScopedKeyword) == modern)),
                "Adapter ref-safety annotations differ from the target state APIs.");
        }
        // Both compiler profiles reference the same managed-cursor Core asset.
        var fixedWrite = methods.Single(method => method.Parent is ClassDeclarationSyntax parent && parent.Identifier.ValueText.StartsWith("LuminEntry_", StringComparison.Ordinal) &&
            method.Identifier.ValueText == "Serialize" && method.TypeParameterList?.Parameters.Count == 1 &&
            method.ReturnType.ToString() == "byte[]" && method.ParameterList.Parameters.Count == 1 && method.ParameterList.Parameters[0].Type?.ToString() == "global::C");
        string fixedBody = fixedWrite.Body!.ToString();
        Require(fixedBody.Contains("new byte[", StringComparison.Ordinal) && !fixedBody.Contains("LuminBufferWriterPool", StringComparison.Ordinal),
            "A fixed record lost direct allocation on the managed-cursor runtime.");
        Require(facade.Any(method => method.Identifier.ValueText.StartsWith("LuminFacade_", StringComparison.Ordinal) && method.TypeParameterList?.Parameters.Count == 3),
            "No generic shared body was emitted.");
        Require(methods.Any(method => method.Identifier.ValueText == "Deserialize" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "int") &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().EndsWith("ReadOnlySequence<byte>", StringComparison.Ordinal) == true) &&
            method.Body is { } body && body.DescendantNodes().OfType<StackAllocArrayCreationExpressionSyntax>().Any() &&
            body.ToString().Contains("LuminPackReader.Read<int>", StringComparison.Ordinal)),
            "Sharing replaced architecture-specific raw sequence staging.");
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        var context = new AssemblyLoadContext("SharedFacade", isCollectible: true);
        image.Position = 0;
        context.LoadFromStream(image).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null);
        context.Unload();

        var minimal = input.RemoveSyntaxTrees(selections);
        var retained = driver.RunGeneratorsAndUpdateCompilation(minimal, out var minimalOutput, out diagnostics);
        CheckErrors(minimalOutput, diagnostics);
        Require(Text(retained) == Text(NewDriver(parse).RunGenerators(minimal)), "Removing complete API roots retained stale shared support.");
        Require(!Text(retained).Contains("LuminFacadeOperations", StringComparison.Ordinal), "Pruned APIs acquired unused full-facade adapter support.");
        var restoredDriver = retained.RunGeneratorsAndUpdateCompilation(input, out var restoredOutput, out diagnostics);
        CheckErrors(restoredOutput, diagnostics);
        Require(Text(restoredDriver) == Text(driver), "Restoring complete API roots lost shared operations.");
    }

    /// <summary>Creates a fresh production driver for one conditional target.</summary>
    /// <param name="parse">The target language and framework symbols.</param>
    /// <returns>The two production generators with no retained state.</returns>
    private static GeneratorDriver NewDriver(CSharpParseOptions parse)
        => CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);

    /// <summary>Reads facade declarations, including their private support methods.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <param name="parse">The current parse settings.</param>
    /// <returns>The methods from all facade partitions.</returns>
    private static MethodDeclarationSyntax[] FacadeMethods(GeneratorDriver driver, CSharpParseOptions parse)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Where(file => (file.HintName.EndsWith(".Api.g.cs", StringComparison.Ordinal) || file.HintName == "LuminPackGen.LuminPackSerializer.g.cs"))
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();

    /// <summary>Captures deterministic generated values for retained/fresh comparisons.</summary>
    /// <param name="driver">The completed production driver.</param>
    /// <returns>Hint names and exact source values in canonical order.</returns>
    private static string Text(GeneratorDriver driver)
        => string.Join("\n", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText));

    /// <summary>Rejects generator or generated-compilation errors.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    /// <param name="diagnostics">The generator diagnostics.</param>
    private static void CheckErrors(Compilation output, IEnumerable<Diagnostic> diagnostics)
    {
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
    }

    /// <summary>Fails a concrete sharing invariant without changing production diagnostics.</summary>
    /// <param name="condition">The expected condition.</param>
    /// <param name="message">The invariant description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
