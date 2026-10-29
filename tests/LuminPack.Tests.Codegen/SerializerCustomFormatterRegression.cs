using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Executes nested custom contracts whose payloads differ from their CLR storage.</summary>
internal static class SerializerCustomFormatterRegression
{
    /// <summary>Checks pointer entry points, nested values, null ownership, collections, and sequence readers.</summary>
    /// <param name="references">Platform and LuminPack runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Buffers;
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Generated;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable(GeneratorType.Custom)] public sealed class CustomValue
            {
                public int Number;
                public static void Serialize(ref LuminPackWriter writer, in CustomValue value)
                { Entry.Writes++; writer.Write(value == null ? -999 : value.Number ^ 85); }
                public static void Deserialize(ref LuminPackReader reader, ref CustomValue value)
                { Entry.Reads++; int encoded = 0; reader.Read(ref encoded); value = encoded == -999 ? null! : new() { Number = encoded ^ 85 }; }
                public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomValue value)
                { Entry.JsonWrites++; writer.Write(value == null ? -999 : value.Number ^ 85); }
                public static void DeserializeJson(ref LuminPackJsonReader reader, ref CustomValue value)
                { Entry.JsonReads++; int encoded = reader.Read<int, global::LuminPack.Option.LuminPackDefaultOptions>(); value = encoded == -999 ? null! : new() { Number = encoded ^ 85 }; }
            }
            [LuminPackable(GeneratorType.Custom)] public struct CustomRaw
            {
                public int Number;
                public static void Serialize(ref LuminPackWriter writer, in CustomRaw value)
                { Entry.RawWrites++; byte encoded = (byte)(value.Number ^ 85); writer.Write(encoded); }
                public static void Deserialize(ref LuminPackReader reader, ref CustomRaw value)
                { Entry.RawReads++; byte encoded = 0; reader.Read(ref encoded); value.Number = encoded ^ 85; }
                public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomRaw value) => writer.Write(value.Number ^ 85);
                public static void DeserializeJson(ref LuminPackJsonReader reader, ref CustomRaw value) => value.Number = reader.Read<int, global::LuminPack.Option.LuminPackDefaultOptions>() ^ 85;
            }
            [LuminPackable] public sealed class Nested { public CustomValue Value = null!; public CustomValue Missing = null!; }
            [LuminPackable] public sealed class CallbackChild
            {
                [LuminPackInclude] private int _value;
                public CallbackChild() => _value = 73;
                public int GetValue() => _value;
                public void SetValue(int value) => _value = value;
            }
            [LuminPackable(GeneratorType.Custom)] public sealed class CallbackCustom
            {
                public static CallbackChild Current = new();
                public static void Serialize(ref LuminPackWriter writer, in CallbackCustom value)
                { var output = writer; output.Write(Current); writer = output; }
                public static void Deserialize(ref LuminPackReader reader, ref CallbackCustom value)
                { var input = reader; input.Read(ref Current); reader = input; value = new(); }
            }
            [LuminPackable] public sealed class CallbackOwner { public CallbackCustom Value = new(); }
            [LuminPackable] public sealed class UnusedCallbackChild { [LuminPackInclude] private int _value = 91; }
            [LuminPackable(GeneratorType.Custom)] public sealed class UnusedCallbackCustom
            {
                public static void Serialize(ref LuminPackWriter writer, in UnusedCallbackCustom value) => writer.Write(new UnusedCallbackChild());
                public static void Deserialize(ref LuminPackReader reader, ref UnusedCallbackCustom value)
                { UnusedCallbackChild child = new(); reader.Read(ref child); value = new(); }
            }
            [LuminPackable] public struct RawOwner { public int Prefix; public CustomRaw Value; public int Suffix; }
            [LuminPackable] public sealed class GenericOwner<T> { public T Value = default!; public T[] Values = []; }
            [LuminPackable] public struct ConstrainedOwner<T> where T : unmanaged { public byte Tag; public T Value; }
            [LuminPackable] public sealed class Host
            {
                public int Prefix;
                public CustomValue Left = null!;
                public CustomValue Right = null!;
                public CustomValue Missing = null!;
                public Nested Child = new();
                public CustomValue[] Values = [];
                public List<CustomValue> Items = [];
                public Dictionary<int, CustomValue> Map = [];
                public CustomRaw Raw;
                public CustomRaw? Optional;
                public CustomRaw[] Raws = [];
                public List<CustomRaw> RawItems = [];
                public Dictionary<int, CustomRaw> RawMap = [];
                public RawOwner RawOwner;
                public RawOwner[] Owners = [];
                public int Suffix;
            }
            public static class Entry
            {
                public static int Writes, Reads, JsonWrites, JsonReads, RawWrites, RawReads;
                public static int Check()
                {
                    var value = new Host { Prefix = 11, Left = new() { Number = 12 }, Right = new() { Number = 13 },
                        Child = new() { Value = new() { Number = 14 } }, Values = [new() { Number = 15 }, null!],
                        Items = [new() { Number = 16 }, null!], Map = new() { [1] = new() { Number = 17 }, [2] = null! },
                        Raw = new() { Number = 18 }, Optional = new() { Number = 19 }, Raws = [new() { Number = 20 }],
                        RawItems = [new() { Number = 21 }], RawMap = new() { [3] = new() { Number = 22 } },
                        RawOwner = new() { Prefix = 23, Value = new() { Number = 24 }, Suffix = 25 },
                        Owners = [new() { Prefix = 26, Value = new() { Number = 27 }, Suffix = 28 }], Suffix = 29 };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    if (Writes != 11 || RawWrites != 7) throw new Exception($"Custom write bypass: {Writes}/{RawWrites}");
                    Host restored = new();
                    LuminPackSerializer.Deserialize(bytes.AsSpan(), ref restored);
                    if (Reads != Writes || RawReads != RawWrites) throw new Exception($"Custom read bypass: {Writes}/{Reads}, {RawWrites}/{RawReads}; bytes={Convert.ToHexString(bytes)}");
                    Validate(restored);
                    string json = LuminPackSerializer.SerializeJson(value);
                    Host jsonValue = null!;
                    LuminPackSerializer.DeserializeJson(json, ref jsonValue);
                    Validate(jsonValue);
                    if (JsonWrites != Writes || JsonReads != JsonWrites) throw new Exception($"Custom JSON bypass: {JsonWrites}/{JsonReads} vs {Writes}.");
                    int previousJsonWrites = JsonWrites;
                    bool encodingRejected = false;
                    try { _ = LuminPackSerializer.SerializeJson<CustomValue, LuminPack.Option.LuminPackUtf8LengthOptions>(value.Left); }
                    catch (Exception) { encodingRejected = true; }
                    if (!encodingRejected || JsonWrites != previousJsonWrites)
                        throw new Exception("A UTF-16 Custom formatter entered a UTF-8 JSON document.");
                    CustomRaw raw = new() { Number = 31 };
                    var rawBytes = LuminPackSerializer.Serialize(raw);
                    if (rawBytes.Length != 1) throw new Exception("Custom root used raw storage.");
                    CustomRaw rawResult = default;
                    LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(rawBytes), ref rawResult);
                    if (rawResult.Number != 31) throw new Exception("Custom sequence used raw storage.");
                    var ownerBytes = LuminPackSerializer.Serialize(value.RawOwner);
                    RawOwner owner = default;
                    LuminPackSerializer.Deserialize(new ReadOnlySequence<byte>(ownerBytes), ref owner);
                    if (owner.Value.Number != 24 || owner.Suffix != 25) throw new Exception("Nested custom sequence used raw storage.");
                    var generic = new GenericOwner<CustomRaw> { Value = raw, Values = [raw] };
                    var genericBytes = LuminPackSerializer.Serialize(generic);
                    GenericOwner<CustomRaw> genericResult = null!;
                    LuminPackSerializer.Deserialize(genericBytes.AsSpan(), ref genericResult);
                    if (genericResult.Value.Number != 31 || genericResult.Values[0].Number != 31) throw new Exception("Generic custom bypass.");
                    var constrained = new ConstrainedOwner<CustomRaw> { Tag = 7, Value = raw };
                    int previousWrites = RawWrites;
                    var constrainedBytes = LuminPackSerializer.Serialize(constrained);
                    if (RawWrites != previousWrites + 1) throw new Exception("An unmanaged constraint bypassed its actual Custom argument.");
                    var constrainedResult = LuminPackSerializer.Deserialize<ConstrainedOwner<CustomRaw>>(constrainedBytes);
                    if (constrainedResult.Tag != 7 || constrainedResult.Value.Number != 31)
                        throw new Exception("An unmanaged-constrained Custom contract changed.");
                    var ordinary = new ConstrainedOwner<int> { Tag = 11, Value = 127 };
                    if (LuminPackSerializer.Serialize(ordinary).Length != System.Runtime.CompilerServices.Unsafe.SizeOf<ConstrainedOwner<int>>())
                        throw new Exception("An unrelated Custom formatter disabled an ordinary constrained raw-copy contract.");
                    var callbackBytes = LuminPackSerializer.Serialize(new CallbackOwner());
                    CallbackCustom.Current.SetValue(0);
                    CallbackOwner callbackResult = new();
                    LuminPackSerializer.Deserialize(callbackBytes.AsSpan(), ref callbackResult);
                    if (CallbackCustom.Current.GetValue() != 73) throw new Exception("Custom callback packable dependency was not generated.");
                    var customKeys = new Dictionary<CustomRaw, string> { [raw] = "custom-key" };
                    previousWrites = RawWrites;
                    var customKeyBytes = LuminPackSerializer.Serialize(customKeys);
                    if (RawWrites != previousWrites + 1) throw new Exception("A mixed dictionary bypassed its unmanaged Custom key.");
                    var customKeyResult = LuminPackSerializer.Deserialize<Dictionary<CustomRaw, string>>(customKeyBytes);
                    if (customKeyResult[raw] != "custom-key")
                        throw new Exception("An unmanaged Custom dictionary key changed its formatter contract.");
                    return 1;
                }
                private static void Validate(Host value)
                {
                    if (value.Prefix != 11 || value.Left.Number != 12 || value.Right.Number != 13 || value.Missing != null ||
                        value.Child.Value.Number != 14 || value.Child.Missing != null || value.Values[0].Number != 15 || value.Values[1] != null ||
                        value.Items[0].Number != 16 || value.Items[1] != null || value.Map[1].Number != 17 || value.Map[2] != null ||
                        value.Raw.Number != 18 || value.Optional?.Number != 19 || value.Raws[0].Number != 20 || value.RawItems[0].Number != 21 ||
                        value.RawMap[3].Number != 22 || value.RawOwner.Prefix != 23 || value.RawOwner.Value.Number != 24 || value.RawOwner.Suffix != 25 ||
                        value.Owners[0].Prefix != 26 || value.Owners[0].Value.Number != 27 || value.Owners[0].Suffix != 28 || value.Suffix != 29)
                        throw new Exception("Custom payload or surrounding field changed.");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var input = CSharpCompilation.Create("NestedCustom" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var generatedSources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
            var errors = output.GetDiagnostics().Concat(driver.GetRunResult().Diagnostics).Where(item => item.Severity == DiagnosticSeverity.Error);
            if (errors.Any()) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            var stateMethods = generatedSources
                .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.TypeParameterList == null && method.ParameterList.Parameters.Count == 2 &&
                    method.ParameterList.Parameters[0].Identifier.ValueText is "writer" or "reader" &&
                    method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::CustomValue").ToArray();
            var compiledMethods = generatedSources.SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 2 &&
                    // Generic dispatch bridges forward a dispatch section to these operations rather than owning one.
                    !method.Identifier.ValueText.StartsWith("__", StringComparison.Ordinal) &&
                    method.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal) &&
                    method.ParameterList.Parameters[0].Identifier.ValueText is "writer" or "reader" &&
                    method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::CustomValue").ToArray();
            if (stateMethods.Length != 4 || compiledMethods.Length != 4 || compiledMethods.Any(method =>
                !method.ToString().Contains("LuminPackFormatterRegistry<", StringComparison.Ordinal) ||
                !method.ToString().Contains(method.Identifier.ValueText.EndsWith("Json", StringComparison.Ordinal) ? ".TryGetCachedJsonValue<" : ".TryGetCachedValue<", StringComparison.Ordinal) ||
                !method.ToString().Contains("delegate*", StringComparison.Ordinal)))
            {
                string candidates = string.Join(" | ", driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                    .SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                    .Where(method => method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString().Contains("CustomValue", StringComparison.Ordinal) == true))
                    .Select(method => method.ToString()).Take(10));
                throw new InvalidOperationException("Custom state overloads must invoke the registry directly: " + stateMethods.Length + ". " + candidates);
            }
            if (mode == "Minimal")
            {
                string dispatch = string.Join(Environment.NewLine, generatedSources
                    .Where(file => file.HintName.StartsWith("LuminPack.Dispatch.", StringComparison.Ordinal))
                    .Select(file => file.SourceText.ToString()));
                if (dispatch.Contains("UnusedCallbackChild", StringComparison.Ordinal))
                    throw new InvalidOperationException("An unreachable Custom callback dependency survived Minimal pruning.");
            }
            using var image = new MemoryStream();
            var emit = output.Emit(image);
            if (!emit.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
            var assembly = Assembly.Load(image.ToArray());
            if ((int)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! != 1)
                throw new InvalidOperationException("Nested custom contract failed in " + mode + ".");
        }
    }
}
