using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks framework scalar metadata selection inside objects, nullable fields and nested containers.</summary>
    /// <param name="references">Platform and runtime metadata references.</param>
    private static void VerifyModernScalarFields(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Runtime.InteropServices;
            using System.Text;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public class ModernScalars
            {
                public DateOnly Date;
                public TimeOnly Time;
                public Int128 Signed;
                public UInt128 Unsigned;
                public Half Half;
                public Rune Rune;
            }
            [LuminPackable] public class ScalarGraph
            {
                public ModernScalars Value;
                public ModernScalars[] Records;
                public List<DateOnly[]> Dates;
                public List<TimeOnly> Times;
                public Int128[] Signed;
                public UInt128[] Unsigned;
                public Half[] Halves;
                public Rune[] Runes;
                public DateOnly? OptionalDate;
                public TimeOnly? OptionalTime;
                public Int128? OptionalSigned;
                public UInt128? OptionalUnsigned;
                public Half? OptionalHalf;
                public Rune? OptionalRune;
            }
            public static class FrameworkEntry
            {
                public static void Check()
                {
                    var item = new ModernScalars { Date = new DateOnly(2024, 2, 29), Time = new TimeOnly(12, 34, 56).Add(TimeSpan.FromTicks(1234567)),
                        Signed = Int128.MinValue + 1, Unsigned = UInt128.MaxValue, Half = (Half)0.5f, Rune = new Rune(0x1F600) };
                    Raw(LuminPackSerializer.Serialize(item.Date), item.Date);
                    Raw(LuminPackSerializer.Serialize(item.Time), item.Time);
                    Raw(LuminPackSerializer.Serialize(item.Signed), item.Signed);
                    Raw(LuminPackSerializer.Serialize(item.Unsigned), item.Unsigned);
                    Raw(LuminPackSerializer.Serialize(item.Half), item.Half);
                    Raw(LuminPackSerializer.Serialize(item.Rune), item.Rune);
                    var graph = new ScalarGraph { Value = item, Records = new[] { item }, Dates = new() { new[] { item.Date, DateOnly.MinValue, DateOnly.MaxValue } },
                        Times = new() { item.Time, TimeOnly.MinValue, TimeOnly.MaxValue }, Signed = new[] { item.Signed, Int128.MaxValue },
                        Unsigned = new[] { item.Unsigned, UInt128.MinValue }, Halves = new[] { item.Half, Half.MinValue }, Runes = new[] { item.Rune, new Rune('a') },
                        OptionalDate = item.Date, OptionalTime = item.Time, OptionalSigned = item.Signed,
                        OptionalUnsigned = item.Unsigned, OptionalHalf = item.Half, OptionalRune = item.Rune };
                    byte[] bytes = LuminPackSerializer.Serialize(graph);
                    ScalarGraph binary = null;
                    Require(LuminPackSerializer.Deserialize(bytes, ref binary) == bytes.Length, "Binary consumption");
                    Require(LuminPackSerializer.Serialize(binary).AsSpan().SequenceEqual(bytes), "Binary scalar graph bytes");
                    for (int length = 0; length < bytes.Length; length++)
                    {
                        ScalarGraph truncated = null;
                        bool failed = false;
                        try { LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref truncated); }
                        catch { failed = true; }
                        Require(failed, "Truncated scalar graph accepted");
                    }
                    Profile<LuminPackUtf8LengthOptions>(graph);
                    Profile<LuminPackUtf16LengthOptions>(graph);
                }
                private static void Profile<TOptions>(ScalarGraph graph) where TOptions : struct, ILuminPackOptions
                {
                    string json = LuminPackSerializer.SerializeJson<TOptions>(graph);
                    Require(json.Contains("\"Date\":\"2024-02-29\"") && json.Contains("\"Time\":\"12:34:56.1234567\"") &&
                        json.Contains("\"Signed\":\"-170141183460469231731687303715884105727\"") &&
                        json.Contains("\"Unsigned\":\"340282366920938463463374607431768211455\"") &&
                        json.Contains("\"Half\":0.5") && json.Contains("\"Rune\":128512"), "Scalar JSON forms");
                    var encoding = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
                    byte[] bytes = encoding.GetBytes(json);
                    ScalarGraph result = null;
                    Require(LuminPackSerializer.DeserializeJson<TOptions>(bytes, ref result) == bytes.Length, "JSON consumption");
                    Require(LuminPackSerializer.SerializeJson<TOptions>(result) == json, "JSON scalar graph");
                    var original = result;
                    LuminPackSerializer.DeserializeJson<TOptions>(bytes, ref result);
                    Require(ReferenceEquals(original, result) && LuminPackSerializer.SerializeJson<TOptions>(result) == json, "JSON reuse");
                    graph.OptionalDate = null; graph.OptionalTime = null; graph.OptionalSigned = null;
                    graph.OptionalUnsigned = null; graph.OptionalHalf = null; graph.OptionalRune = null;
                    json = LuminPackSerializer.SerializeJson<TOptions>(graph);
                    LuminPackSerializer.DeserializeJson<TOptions>(encoding.GetBytes(json), ref result);
                    Require(result.OptionalDate is null && result.OptionalTime is null && result.OptionalSigned is null &&
                        result.OptionalUnsigned is null && result.OptionalHalf is null && result.OptionalRune is null, "Nullable scalars");
                    graph.OptionalDate = graph.Value.Date; graph.OptionalTime = graph.Value.Time; graph.OptionalSigned = graph.Value.Signed;
                    graph.OptionalUnsigned = graph.Value.Unsigned; graph.OptionalHalf = graph.Value.Half; graph.OptionalRune = graph.Value.Rune;
                }
                private static void Raw<T>(byte[] bytes, T value) where T : unmanaged
                    => Require(bytes.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1))), "Raw scalar wire");
                private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var compilation = CSharpCompilation.Create("ModernScalarFields" + mode + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "ModernScalars.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Execute(output);
        }
    }
}
