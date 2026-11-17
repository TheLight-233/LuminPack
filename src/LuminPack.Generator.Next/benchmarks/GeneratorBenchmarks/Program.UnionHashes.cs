using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Checks the hash wire and the exact and virtual writer boundaries with real generated code.</summary>
    /// <param name="references">The consumer's framework and runtime metadata.</param>
    /// <param name="parse">The consumer compiler options.</param>
    /// <param name="coreDll">The runtime used to execute the fixture.</param>
    /// <returns>Zero after all checks pass.</returns>
    private static int VerifyUnionHashes(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        const string integerCase = "LuminPackUnion(101, typeof(Large32<int>))";
        const string stringCase = "LuminPackUnion(102, typeof(Large32<string>))";
        string closedCases = integerCase + ", " + stringCase;
        string source = """
            using System;
            using System.Buffers.Binary;
            using System.Collections.Generic;
            using System.Reflection;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            [LuminPackable, LuminPackUnion(7, typeof(Case<int>)), LuminPackUnion(9, typeof(Case<string>))]
            public abstract class Root<T> { }
            [LuminPackable] public sealed class Case<T> : Root<T> { public T Value = default!; }
            [LuminPackable, LuminPackUnion(1, typeof(OpenCase<>))] public abstract class OpenRoot<T> { }
            [LuminPackable] public sealed class OpenCase<T> : OpenRoot<T> { public T Value = default!; }
            public sealed class Registered : Root<int> { public int Value; }
            [LuminPackable] public abstract class Small { }
            [LuminPackable, CLOSED_CASES] public abstract partial class Large<T> { }
            public static class Entry
            {
                public static void Run()
                {
                    Root<int> integer = new Case<int> { Value = 42 };
                    Root<string> text = new Case<string> { Value = "value" };
                    var a = LuminPackSerializer.Serialize(integer);
                    var b = LuminPackSerializer.Serialize(text);
                    Require(a.AsSpan().SequenceEqual(Convert.FromHexString("7E4893A22A000000")));
                    Require(BinaryPrimitives.ReadUInt32LittleEndian(b) == Hash("Case<System.String>"u8));
                    Require(LuminPackSerializer.Deserialize<Root<int>>(a) is Case<int> { Value: 42 });
                    Require(LuminPackSerializer.Deserialize<Root<string>>(b) is Case<string> { Value: "value" });
                    OpenRoot<int> open = new OpenCase<int> { Value = 17 };
                    byte[] c = LuminPackSerializer.Serialize(open);
                    Require(BinaryPrimitives.ReadUInt32LittleEndian(c) == Hash("OpenCase<!0>"u8));
                    Require(LuminPackSerializer.Deserialize<OpenRoot<int>>(c) is OpenCase<int> { Value: 17 });
                    byte[] empty = LuminPackSerializer.Serialize<Root<int>>(null!);
                    Require(empty.AsSpan().SequenceEqual(new byte[4]));
                    Require(LuminPackSerializer.Deserialize<Root<int>>(empty) is null);
                    Root<int>? unknown = null;
                    bool rejected = false;
                    try { LuminPackSerializer.Deserialize<Root<int>>(new byte[] { 1, 0, 0, 0 }, ref unknown); }
                    catch (FormatException) { rejected = true; }
                    Require(rejected && unknown is null);
                    for (int length = 0; length < a.Length; length++)
                    {
                        Root<int>? restored = null;
                        try { LuminPackSerializer.Deserialize<Root<int>>(a.AsSpan(0, length), ref restored); }
                        catch (Exception error) when (error.GetType().Name == "LuminPackException") { Require(restored is null); continue; }
                        throw new Exception("A truncated Union was accepted.");
                    }
                    Small s = new Small31 { Value = 31 };
                    Require(LuminPackSerializer.Deserialize<Small>(LuminPackSerializer.Serialize(s)) is Small31 { Value: 31 });
                    Large<int> l = new Large32<int> { Value = 32 };
                    Large<string> other = new Large32<string> { Value = 33 };
                    byte[] largeInteger = LuminPackSerializer.Serialize(l);
                    byte[] largeString = LuminPackSerializer.Serialize(other);
                    int integerHash = unchecked((int)Hash("Large32<System.Int32>"u8));
                    int stringHash = unchecked((int)Hash("Large32<System.String>"u8));
                    Require(CachedDispatchId(typeof(Large32<int>)) == integerHash);
                    Require(CachedDispatchId(typeof(Large32<string>)) == stringHash);
                    Require(CachedDispatchId(typeof(Large32<>).MakeGenericType(typeof(double))) == -1);
                    Require(BinaryPrimitives.ReadInt32LittleEndian(largeInteger) == integerHash);
                    Require(BinaryPrimitives.ReadInt32LittleEndian(largeString) == stringHash);
                    Require(LuminPackSerializer.Deserialize<Large<int>>(largeInteger) is Large32<int> { Value: 32 });
                    Require(LuminPackSerializer.Deserialize<Large<string>>(largeString) is Large32<string> { Value: 33 });
                    Require(LuminPackSerializer.SerializeJson(l).Contains("\"$type\":101", StringComparison.Ordinal));
                    Require(LuminPackSerializer.SerializeJson(other).Contains("\"$type\":102", StringComparison.Ordinal));
                    Require(LuminPackSerializer.SerializeJson(integer).Contains("\"$type\":7", StringComparison.Ordinal));
                    bool reserved = false;
                    try { LuminPackSerializer.Register<Root<int>, Registered>(7, Write, Read, WriteJson, ReadJson); }
                    catch (ArgumentException) { reserved = true; }
                    Require(reserved);
                    LuminPackSerializer.Register<Root<int>, Registered>(11, Write, Read, WriteJson, ReadJson);
                    byte[] registered = LuminPackSerializer.Serialize<Root<int>>(new Registered { Value = 19 });
                    Require(registered.AsSpan().SequenceEqual(Convert.FromHexString("FFFFFFFF0B13000000")));
                    Require(LuminPackSerializer.Deserialize<Root<int>>(registered) is Registered { Value: 19 });
                }

                private static void Write(ref LuminPackWriter writer, ref Root<int> value)
                {
                    writer.WriteUnionHeader(11);
                    writer.Write(((Registered)value).Value);
                }

                private static void Read(ref LuminPackReader reader, ref Root<int> value)
                    => value = new Registered { Value = reader.Read<int>() };

                private static void WriteJson(ref LuminPackJsonWriter writer, ref Root<int> value) => throw new NotSupportedException();
                private static void ReadJson(ref LuminPackJsonReader reader, ref Root<int> value) => throw new NotSupportedException();

                private static uint Hash(ReadOnlySpan<byte> bytes)
                {
                    uint hash = 2166136261u;
                    foreach (byte value in bytes) hash = unchecked((hash ^ value) * 16777619u);
                    return hash;
                }

                private static int CachedDispatchId(Type type)
                {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (!field.Name.StartsWith("LuminPackUnionDispatchSlot_", StringComparison.Ordinal)) continue;
                        Require(field.FieldType == typeof(int) && field.IsInitOnly);
                        return (int)field.GetValue(null)!;
                    }
                    throw new Exception("A virtual generic Union has no cached case ID.");
                }

                private static void Require(bool condition)
                {
                    if (!condition) throw new Exception("Union wire or dispatch check failed.");
                }
            }
            """.Replace("CLOSED_CASES", closedCases, StringComparison.Ordinal);
        for (int index = 0; index < 33; index++)
        {
            if (index < 32)
                source += $"\n[LuminPackable] public sealed class Small{index} : Small {{ public int Value; }}";
            source += $"\n[LuminPackable] public sealed partial class Large{index}<T> : Large<T> {{ public int Value; }}";
        }

        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        var assembly = Generate(source, out var generated, out var diagnostics);
        RequireIncremental(diagnostics.All(item => item.Severity != DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        RequireIncremental(generated.Split('\n').Any(line => line.Contains("GetType() == typeof(global::Case<int>)", StringComparison.Ordinal) &&
            line.Contains("return; }", StringComparison.Ordinal)), "Closed generic exact branch occupies one line.");
        RequireIncremental(generated.Contains("public virtual void Write_", StringComparison.Ordinal), "Large union did not retain virtual write dispatch.");
        Generate(source.Replace("abstract partial class Large<T>", "abstract class Large<T>"), out _, out var invalid);
        RequireIncremental(invalid.Any(item => item.Id == "LuminPack035"), "Large non-partial union did not report LuminPack035.");
        Generate(source + "\n[LuminPackable] public abstract class HashRoot { }\n" +
            "[LuminPackable] public sealed class HashCase112789 : HashRoot { public int Value; }\n" +
            "[LuminPackable] public sealed class HashCase349192 : HashRoot { public int Value; }", out _, out invalid);
        RequireIncremental(invalid.Any(item => item.Id == "LuminPack036"), "A known name hash collision did not report LuminPack036.");
        Execute(assembly);
        var reordered = Generate(source.Replace(closedCases, stringCase + ", " + integerCase, StringComparison.Ordinal),
            out _, out diagnostics, "ReorderedUnionHashes");
        RequireIncremental(diagnostics.All(item => item.Severity != DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        Execute(reordered);
        Console.WriteLine("Passed binary Union hashes, non-partial exact cases, order-independent generic cache IDs, truncation and the 32/33 boundary.");
        return 0;

        static void Execute(Compilation compilation)
        {
            using var image = new MemoryStream();
            var emit = compilation.Emit(image);
            RequireIncremental(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Run")!.Invoke(null, null);
        }

        Compilation Generate(string input, out string generatedSource, out Diagnostic[] allDiagnostics, string name = "NextUnionHashes")
        {
            var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(input, parse, "UnionHashes.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator()], parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
            generatedSource = string.Join("\n", driver.GetRunResult().GeneratedTrees);
            allDiagnostics = generatorDiagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
            return output;
        }
    }
}
