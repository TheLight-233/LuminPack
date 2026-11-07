using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks generated scalar boundaries with generic names, both encodings and reused object storage.</summary>
internal static class SerializerJsonScalarLoopRegression
{
    /// <summary>Executes floating-point graphs and verifies that isolated scalar operations capture no state.</summary>
    /// <param name="references">The platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Text;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class SingleValue { public float Value; }
            [LuminPackable] public class Scalars<__luminReadJsonScalar, __luminJsonScalarState>
            {
                public float A, B;
                public double C, D;
                public float? Optional;
                public List<float> Values;
                public __luminReadJsonScalar First;
                public __luminJsonScalarState Second;
            }
            [LuminPackable] public class Bound
            {
                public float A { get; }
                public double B { get; }
                public Bound(float a, double b) { A = a; B = b; }
            }
            public static unsafe class ScalarEntry
            {
                public static void Check()
                {
                    Profile<LuminPackUtf8LengthOptions>();
                    Profile<LuminPackUtf16LengthOptions>();
                }
                private static void Profile<TOptions>() where TOptions : struct, ILuminPackOptions
                {
                    LuminPackSerializer.Register<Scalars<int, long>, TOptions>(Write<TOptions>, Read<TOptions>, WriteJson<TOptions>, ReadJson<TOptions>);
                    var value = new Scalars<int, long> { A = float.MaxValue, B = -0.0f, C = double.Epsilon, D = double.MinValue,
                        Optional = 1.1754944E-38f, Values = new List<float> { 0.1f, float.Epsilon, -float.MaxValue }, First = 17, Second = 19 };
                    string json = LuminPackSerializer.SerializeJson<Scalars<int, long>, TOptions>(value);
                    var encoding = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
                    byte[] bytes = encoding.GetBytes(json);
                    var original = value; var list = value.Values;
                    int consumed = LuminPackSerializer.DeserializeJson<Scalars<int, long>, TOptions>(bytes, ref value);
                    Require(consumed == bytes.Length && ReferenceEquals(value, original) && ReferenceEquals(value.Values, list), "Reader or reuse state");
                    Require(LuminPackSerializer.SerializeJson<Scalars<int, long>, TOptions>(value) == json && BitConverter.SingleToInt32Bits(value.B) < 0, "Numeric bits or rewrite");
                    Scalars<int, long> fresh = null!;
                    ReadGeneric<TOptions, int, long>(bytes, ref fresh);
                    Require(LuminPackSerializer.SerializeJson<Scalars<int, long>, TOptions>(fresh) == json, "Fresh generic graph");
                    const string reordered = "{\"D\":2.2250738585072014e-308,\"unknown\":[{},42],\"A\":1.0,\"A\":3.4028235e38,\"B\":-0.0,\"C\":9007199254740993,\"Optional\":null}";
                    LuminPackSerializer.DeserializeJson<Scalars<int, long>, TOptions>(encoding.GetBytes(reordered), ref value);
                    Require(value.A == float.MaxValue && BitConverter.SingleToInt32Bits(value.B) < 0 && value.C == 9007199254740992d && value.D == 2.2250738585072014e-308 && value.Optional == null, "Reordered duplicate numeric fields");
                    value.C = 29;
                    bool failed = false;
                    try { LuminPackSerializer.DeserializeJson<Scalars<int, long>, TOptions>(encoding.GetBytes("{\"A\":17,\"B\":1e}"), ref value); }
                    catch (FormatException) { failed = true; }
                    catch (InvalidOperationException) { failed = true; }
                    Require(failed && ReferenceEquals(value, original) && value.A == 17 && value.C == 29, "Failure publication order");
                    fresh = null!; failed = false;
                    try { LuminPackSerializer.DeserializeJson<Scalars<int, long>, TOptions>(encoding.GetBytes("{\"A\":17,\"B\":1e}"), ref fresh); }
                    catch (FormatException) { failed = true; }
                    catch (InvalidOperationException) { failed = true; }
                    Require(failed && fresh is null, "Fresh failure publication");
                    var bound = new Bound(float.MaxValue, double.Epsilon);
                    json = LuminPackSerializer.SerializeJson<Bound, TOptions>(bound);
                    Bound constructed = null!;
                    LuminPackSerializer.DeserializeJson<Bound, TOptions>(encoding.GetBytes(json), ref constructed);
                    Require(constructed.A == bound.A && constructed.B == bound.B, "Constructor arguments");
                    var single = new SingleValue();
                    LuminPackSerializer.DeserializeJson<SingleValue, TOptions>(encoding.GetBytes("{\"Value\":-0.0}"), ref single);
                    Require(BitConverter.SingleToInt32Bits(single.Value) < 0, "Small root");
                    List<float> sequence = null!;
                    LuminPackSerializer.DeserializeJson<List<float>, TOptions>(encoding.GetBytes("[1.25,-0.0]"), ref sequence);
                    Require(sequence.Count == 2 && sequence[0] == 1.25f && BitConverter.SingleToInt32Bits(sequence[1]) < 0, "Single-site collection");
                }
                private static void ReadGeneric<TOptions, __luminReadJsonScalar, __luminJsonScalarState>(byte[] bytes,
                    ref Scalars<__luminReadJsonScalar, __luminJsonScalarState> value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.DeserializeJson<Scalars<__luminReadJsonScalar, __luminJsonScalarState>, TOptions>(bytes, ref value);
                private static void Write<TOptions>(ref LuminPack.Core.LuminPackWriter writer, in Scalars<int, long> value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Serialize<Scalars<int, long>, TOptions>(ref writer, in value);
                private static void Read<TOptions>(ref LuminPack.Core.LuminPackReader reader, ref Scalars<int, long> value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Deserialize<Scalars<int, long>, TOptions>(ref reader, ref value);
                private static void WriteJson<TOptions>(ref LuminPack.Core.LuminPackJsonWriter writer, in Scalars<int, long> value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.SerializeJson<Scalars<int, long>, TOptions>(ref writer, in value);
                private static void ReadJson<TOptions>(ref LuminPack.Core.LuminPackJsonReader reader, ref Scalars<int, long> value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.DeserializeJson<Scalars<int, long>, TOptions>(ref reader, ref value);
                private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("JsonScalars" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            var assembly = Assembly.Load(image.ToArray());
            assembly.GetType("ScalarEntry")!.GetMethod("Check")!.Invoke(null, null);
            var helpers = assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(method => method.Name.Contains("__luminReadJsonScalar", StringComparison.Ordinal)).ToArray();
            Require(helpers.Length > 0, "The compound fixture did not exercise scalar boundaries.");
            foreach (var helper in helpers)
                Require(helper.GetParameters() is [{ ParameterType.IsByRef: true }] && helper.GetMethodBody()!.LocalVariables.Count == 0 &&
                    (helper.MethodImplementationFlags & MethodImplAttributes.NoInlining) != 0, "Scalar entry captured state or lost its independent budget.");
            var methods = output.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Identifier.ValueText == "RootReadJson").ToArray();
            foreach (string small in new[] { "List<float>", "SingleValue" })
            {
                var smallRoots = methods.Where(method => method.ParameterList.Parameters[1].Type!.ToString().Contains(small, StringComparison.Ordinal)).ToArray();
                Require(smallRoots.Length != 0, "The small fixture did not generate its JSON root: " + small);
                foreach (var method in smallRoots)
                    Require(!method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(), "A small root or single-site scalar list gained a boundary.");
            }
        }
        Console.WriteLine("Passed JSON scalar boundaries across four generation tiers, both encodings and fresh/reused storage.");
    }

    /// <summary>Reports a scalar generation or semantic regression.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
