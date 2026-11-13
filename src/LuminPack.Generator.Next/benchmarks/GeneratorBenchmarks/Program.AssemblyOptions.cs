using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Checks assembly registration policy, forced operations, legacy constructors and fallback input profiles.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    /// <param name="parse">The consumer language and target settings.</param>
    /// <param name="coreDll">The runtime used to execute generated registration fixtures.</param>
    /// <returns>Zero when all assembly settings retain their documented semantics.</returns>
    private static int VerifyAssemblyOptions(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        int checks = 0;
        for (int mask = 0; mask < 16; mask++)
        {
            string settings = "[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(" +
                "ForceGenerateSerialize = " + Bool((mask & 1) != 0) + ", ForceGenerateDeserialize = " + Bool((mask & 2) != 0) +
                ", ForceGenerateSerializeJson = " + Bool((mask & 4) != 0) + ", ForceGenerateDeserializeJson = " + Bool((mask & 8) != 0) + ")]";
            CheckSettings(settings, mask, registration: true, application: true, unsafeCode: true);
        }
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        foreach (bool registration in new[] { true, false })
            CheckSettings("[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(LuminPack.Attribute.LuminPackGenerationMode." +
                mode + ", LuminPack.Attribute.LuminPackRegisterMode." + (registration ? "Auto" : "Disabled") + ")]",
                0, registration, application: true, unsafeCode: true);
        {
            const string source = """
                using LuminPack;
                using LuminPack.Attribute;
                [LuminPackable] public sealed class UsedPayload { public int Number; }
                [LuminPackable] public sealed class UnusedPayload { public int Number; }
                public sealed class OrdinaryMembers { public int ReadValue = 3; public int WriteValueJson => 4; }
                public static class Entry
                {
                    public static void Main()
                    {
                        var ordinary = new OrdinaryMembers();
                        LuminPackSerializer.Serialize(new UsedPayload { Number = ordinary.ReadValue + ordinary.WriteValueJson });
                    }
                }
                """;
            var result = GenerateAssembly(source, OutputKind.ConsoleApplication, allowUnsafe: true);
            var used = result.Output.GetTypeByMetadataName("LuminPack.LuminPackSerializer+UsedPayloadFormatter");
            RequireIncremental(used?.GetMembers("Write").Length > 0 && used.GetMembers("Read").IsEmpty &&
                used.GetMembers("WriteJson").IsEmpty && used.GetMembers("ReadJson").IsEmpty,
                "An ordinary field or property with a generated extension name must not retain unrelated directions.");
            RequireIncremental(result.Output.GetTypeByMetadataName("LuminPack.LuminPackSerializer+UnusedPayloadFormatter") == null,
                "An ordinary field or property with a generated extension name must not retain unused payloads.");
        }
        foreach (bool unsafeCode in new[] { true, false })
        foreach (bool registration in new[] { true, false })
        {
            string settings = "[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(register: LuminPack.Attribute.LuminPackRegisterMode." +
                (registration ? "Auto" : "Disabled") + ")]";
            CheckSettings(settings, 15, registration, application: false, unsafeCode);
            CheckSettings(settings + "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(" +
                Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral("Friend", true) + ")]", 15, registration, application: true, unsafeCode);
        }
        {
            const string source = """
                using LuminPack;
                using LuminPack.Attribute;
                [LuminPackable] public sealed class PendingPayload { public int Number; }
                public static class Entry
                {
                    public static void Main() => LuminPackSerializer.Serialize(new SuppliedByAnotherGenerator());
                }
                """;
            var tree = CSharpSyntaxTree.ParseText(source, parse, "PendingArgument.cs");
            var input = CSharpCompilation.Create("PendingArgument", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true));
            CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var root = output.GetTypeByMetadataName("LuminPack.LuminPackSerializer+PendingPayloadFormatter");
            RequireIncremental(root?.GetMembers("Write").Length > 0 && root.GetMembers("Read").IsEmpty &&
                root.GetMembers("WriteJson").IsEmpty && root.GetMembers("ReadJson").IsEmpty,
                "An unresolved serializer call should keep only the direction selected by its name.");
        }
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        foreach (bool registration in new[] { true, false })
        {
            string source = AssemblyRegistrationFixture.Replace("REGISTRATION", registration ? "Auto" : "Disabled")
                .Replace("EXPECTED", Bool(registration));
            var result = GenerateAssembly(source, OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false);
            string text = string.Concat(Sources(result.Driver).Values);
            RequireIncremental(text.Contains("TryGetFormatter(", StringComparison.Ordinal) == registration, "Union registry lookup ignored the assembly setting.");
            RequireIncremental(text.Contains("ReserveTags(", StringComparison.Ordinal) == registration, "Union tag reservation ignored the assembly setting.");
            using var image = new MemoryStream();
            var emitted = result.Output.Emit(image);
            RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            checks += (int)Assembly.Load(image.ToArray()).GetType("RegistrationFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        }
        foreach (string encoding in new[] { "UTF8", "UTF16" })
        {
            string source = AssemblyInputFixture.Replace("ENCODING", encoding).Replace("PROFILE",
                encoding == "UTF8" ? "LuminPackUtf8LengthOptions" : "LuminPackUtf16LengthOptions");
            var result = GenerateAssembly(source, OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false);
            using var image = new MemoryStream();
            var emitted = result.Output.Emit(image);
            RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            checks += (int)Assembly.Load(image.ToArray()).GetType("AssemblyInputFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        }
        foreach (bool intercept in new[] { true, false })
        {
            var result = GenerateAssembly(AutomaticRegistrationFixture, OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false,
                intercept ? parse : parse.WithFeatures(Array.Empty<KeyValuePair<string, string>>()));
            var files = result.Driver.GetRunResult().Results.Single().GeneratedSources;
            string registry = files.Single(file => file.HintName.EndsWith("CustomFormatterRegistry.g.cs", StringComparison.Ordinal)).SourceText.ToString();
            RequireIncremental(registry.Contains("static LuminPackSerializer()", StringComparison.Ordinal) &&
                !registry.Contains("ModuleInitializer", StringComparison.Ordinal), "Formatter registration must run on first facade use.");
            RequireIncremental(registry.Contains("RegisterGenerated<global::System.Collections.Generic.Dictionary<int, string>>", StringComparison.Ordinal) &&
                !registry.Contains("RegisterGenerated<int>", StringComparison.Ordinal), "Only the closed collection needs a generated registration.");
            foreach (var file in files.Where(file => file.HintName.EndsWith("Interceptors.g.cs", StringComparison.Ordinal)))
            {
                foreach (var method in file.SyntaxTree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>())
                {
                    if (method.Body is not { } body)
                        continue;
                    var declaration = method.GetFirstToken().GetLocation().GetLineSpan().StartLinePosition;
                    var open = body.OpenBraceToken.GetLocation().GetLineSpan().StartLinePosition;
                    var close = body.CloseBraceToken.GetLocation().GetLineSpan().StartLinePosition;
                    RequireIncremental(declaration.Character == open.Character && declaration.Character == close.Character, "Interceptor brace indentation.");
                }
            }
            using var image = new MemoryStream();
            var emitted = result.Output.Emit(image);
            RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            checks += (int)Assembly.Load(image.ToArray()).GetType("AutomaticRegistrationFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        }
        checks += VerifyStaticBackends(references, parse);
        {
            var result = GenerateAssembly(AnnotatedMembersFixture, OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false);
            using var image = new MemoryStream();
            var emitted = result.Output.Emit(image);
            RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
            string registry = Sources(result.Driver).Single(file => file.Key.EndsWith("CustomFormatterRegistry.g.cs", StringComparison.Ordinal)).Value;
            RequireIncremental(!registry.Contains("RegisterGenerated<global::ManualPayload>", StringComparison.Ordinal),
                "NonGenerator must not publish an automatic registration.");
            checks += (int)Assembly.Load(image.ToArray()).GetType("AnnotatedMembersFixture")!.GetMethod("Exercise")!.Invoke(null, null)!;
        }
        Console.WriteLine($"Next assembly-option contracts: {checks} matrix and runtime checks passed.");
        return 0;

        void CheckSettings(string settings, int mask, bool registration, bool application, bool unsafeCode)
        {
            string source = "using LuminPack.Attribute; " + settings + " [LuminPackable] public sealed class SettingsPayload " +
                "{ public int Number; public string Text; } public static class SettingsEntry { public static void Main() { } }";
            var result = GenerateAssembly(source, application ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary, unsafeCode);
            var root = result.Output.GetTypeByMetadataName("LuminPack.LuminPackSerializer+SettingsPayloadFormatter");
            string[] names = { "Write", "Read", "WriteJson", "ReadJson" };
            for (int index = 0; index < names.Length; index++)
                RequireIncremental((root?.GetMembers(names[index]).Length > 0) == ((mask & (1 << index)) != 0), "Forced operation selection: " + names[index]);
            var facade = result.Output.GetTypeByMetadataName("LuminPack.LuminPackSerializer")!;
            RequireIncremental(facade.GetMembers("Register").Length == (registration ? 5 : 0), "Assembly registration facade availability.");
            string text = string.Concat(Sources(result.Driver).Values);
            RequireIncremental(text.Contains("TryGetCachedValue<", StringComparison.Ordinal) == registration, "Binary fallback registry policy.");
            RequireIncremental(text.Contains("TryGetCachedJsonValue<", StringComparison.Ordinal) == registration, "JSON fallback registry policy.");
            checks++;
        }

        (GeneratorDriver Driver, Compilation Output) GenerateAssembly(string source, OutputKind kind, bool allowUnsafe, CSharpParseOptions? parseOverride = null)
        {
            CSharpParseOptions syntax = parseOverride ?? parse;
            var tree = CSharpSyntaxTree.ParseText(source, syntax, "AssemblyOptions.cs");
            var input = CSharpCompilation.Create("AssemblyOptions" + checks++, new[] { tree }, references,
                new CSharpCompilationOptions(kind, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: syntax)
                .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            foreach (var result in driver.GetRunResult().Results)
                RequireIncremental(result.Exception == null, result.Exception?.ToString() ?? string.Empty);
            foreach (Diagnostic diagnostic in diagnostics.Concat(output.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, diagnostic.ToString());
            return (driver, output);
        }

        static string Bool(bool value) => value ? "true" : "false";
    }

    /// <summary>Compares named-member conflicts with the legacy generator's diagnostic contract.</summary>
    /// <param name="references">The framework and runtime references.</param>
    /// <param name="parse">The consumer parse options.</param>
    /// <param name="legacyDll">The legacy generator assembly.</param>
    /// <returns>The number of compared declarations.</returns>
    private static int VerifyAliasDiagnostics(MetadataReference[] references, CSharpParseOptions parse, string legacyDll)
    {
        var assembly = new AssemblyLoadContext("AliasDiagnosticReference").LoadFromAssemblyPath(Path.GetFullPath(legacyDll));
        ISourceGenerator[] legacy = CreateLegacyGenerators(assembly);
        ISourceGenerator[] next = { new NextGenerator().AsSourceGenerator() };
        var cases = new (string Mode, string Fields)[]
        {
            ("VersionTolerant", """[LuminPackOldName("")] public int A;"""),
            ("VersionTolerant", """[LuminPackOldName(null)] public int A;"""),
            ("VersionTolerant", """[LuminPackOldName("A")] public int A;"""),
            ("VersionTolerant", """[LuminPackOldName("B")] public int A; public int B;"""),
            ("VersionTolerant", """[LuminPackOldName("old")] public int A; [LuminPackOldName("old")] public int B;"""),
            ("VersionTolerant", """[LuminPackOldName("\uD800")] public int A;"""),
            ("Object", """[LuminPackOldName("old")] public int A;"""),
        };
        foreach (var item in cases)
        {
            string source = "using LuminPack.Attribute; [LuminPackable(GeneratorType." + item.Mode +
                ")] public class Invalid { " + item.Fields + " }";
            var tree = CSharpSyntaxTree.ParseText(source, parse, "InvalidNames.cs");
            var input = CSharpCompilation.Create("InvalidNames", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            var expected = Diagnostics(legacy, input);
            var actual = Diagnostics(next, input);
            RequireIncremental(expected.Length == 1 && expected.SequenceEqual(actual),
                item.Fields + " diagnostic mismatch: " + string.Join(", ", expected) + " / " + string.Join(", ", actual));
        }
        return cases.Length;

        (string Message, DiagnosticSeverity Severity)[] Diagnostics(ISourceGenerator[] generators, Compilation compilation)
        {
            var result = CSharpGeneratorDriver.Create(generators, parseOptions: parse).RunGenerators(compilation).GetRunResult();
            foreach (var generator in result.Results)
                RequireIncremental(generator.Exception == null, generator.Exception?.ToString() ?? string.Empty);
            return result.Diagnostics.Where(item => item.Id == "LuminPack062")
                .Select(item => (item.GetMessage(), item.Severity)).Distinct().ToArray();
        }
    }

    private const string AutomaticRegistrationFixture = """
        using System;
        using System.Collections.Generic;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Code;
        using LuminPack.Core;
        using LuminPack.Option;

        [LuminPackable] public sealed class AutoPayload
        {
            public Dictionary<int, string> Values = new();
        }

        [LuminPackable(GeneratorType.Custom)] public sealed class AutoCustom
        {
            public int Number;
            public static void Serialize(ref LuminPackWriter writer, in AutoCustom value) => writer.Write(value.Number);
            public static void Deserialize(ref LuminPackReader reader, ref AutoCustom value) => value = new AutoCustom { Number = reader.Read<int>() };
            public static void SerializeJson(ref LuminPackJsonWriter writer, in AutoCustom value) => writer.Write<LuminPackUtf16LengthOptions>(value.Number);
            public static void DeserializeJson(ref LuminPackJsonReader reader, ref AutoCustom value) => value = new AutoCustom { Number = reader.Read<int, LuminPackUtf16LengthOptions>() };
        }

        public static class AutomaticRegistrationFixture
        {
            public static int Exercise()
            {
                nint key = LuminPackMarshal.GetMethodTable(typeof(AutoCustom));
                if (LuminPackFormatterRegistry.TryGetValue(key, out _)) throw new Exception("Custom registration ran at module load.");
                if (Read<int>(Write(17)) != 17) throw new Exception("Built-in binary registration.");
                if (ReadJson<AutoCustom>(WriteJson(new AutoCustom { Number = 23 })).Number != 23) throw new Exception("Custom first-use registration.");
                var values = new Dictionary<int, string> { [7] = "seven" };
                if (Read<AutoPayload>(Write(new AutoPayload { Values = values })).Values[7] != "seven") throw new Exception("Generated model registration.");
                if (Read<Dictionary<int, string>>(Write(values))[7] != "seven") throw new Exception("Closed collection binary registration.");
                if (ReadUtf8Json<Dictionary<int, string>>(WriteUtf8Json(values))[7] != "seven") throw new Exception("Closed collection profile registration.");
                if (!LuminPackSerializer.Serialize(values).AsSpan().SequenceEqual(Write(values))) throw new Exception("Concrete and generic bytes differ.");
                LuminPackSerializer.Register<AutoPayload>(
                    static (ref LuminPackWriter writer, in AutoPayload value) => writer.Write(123),
                    static (ref LuminPackReader reader, ref AutoPayload value) => value = new AutoPayload { Values = { [reader.Read<int>()] = "manual" } });
                if (Read<AutoPayload>(Write(new AutoPayload())).Values[123] != "manual") throw new Exception("Explicit registration lost priority.");
                return 7;
            }
            private static byte[] Write<T>(T value) => LuminPackSerializer.Serialize(value);
            private static T Read<T>(byte[] bytes) => LuminPackSerializer.Deserialize<T>(bytes);
            private static string WriteJson<T>(T value) => LuminPackSerializer.SerializeJson(value);
            private static T ReadJson<T>(string json) => LuminPackSerializer.DeserializeJson<T>(json);
            private static string WriteUtf8Json<T>(T value) => LuminPackSerializer.SerializeJson<T, LuminPackUtf8LengthOptions>(value);
            private static T ReadUtf8Json<T>(string json) => LuminPackSerializer.DeserializeJson<T, LuminPackUtf8LengthOptions>(json);
        }
        """;

    private const string AnnotatedMembersFixture = """
        using System;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;

        public interface IManualValue { int Number { get; } }
        [LuminPackable(GeneratorType.NonGenerator)]
        public sealed class ManualPayload : IManualValue { public int Number { get; set; } }
        [LuminPackable] public sealed class AutomaticChild { public int Number; }

        [LuminPackable]
        public sealed class AnnotationHolder
        {
            [LuminPackableObject] public IManualValue Manual { get; set; }
            [LuminPackableObject] public AutomaticChild Child;
            public ManualPayload Value;
        }

        public static class AnnotatedMembersFixture
        {
            public static int Exercise()
            {
                LuminPackSerializer.Register<IManualValue>(
                    static (ref LuminPackWriter w, in IManualValue v) => w.Write(v.Number + 1),
                    static (ref LuminPackReader r, ref IManualValue v) => v = new ManualPayload { Number = r.Read<int>() - 1 },
                    static (ref LuminPackJsonWriter w, in IManualValue v) => w.Write<LuminPackUtf16LengthOptions>(v.Number + 1),
                    static (ref LuminPackJsonReader r, ref IManualValue v) => v = new ManualPayload { Number = r.Read<int, LuminPackUtf16LengthOptions>() - 1 });
                LuminPackSerializer.Register<ManualPayload>(
                    static (ref LuminPackWriter w, in ManualPayload v) => w.Write(v.Number + 2),
                    static (ref LuminPackReader r, ref ManualPayload v) => v = new ManualPayload { Number = r.Read<int>() - 2 },
                    static (ref LuminPackJsonWriter w, in ManualPayload v) => w.Write<LuminPackUtf16LengthOptions>(v.Number + 2),
                    static (ref LuminPackJsonReader r, ref ManualPayload v) => v = new ManualPayload { Number = r.Read<int, LuminPackUtf16LengthOptions>() - 2 });
                var value = new AnnotationHolder { Manual = new ManualPayload { Number = 10 },
                    Child = new AutomaticChild { Number = 20 }, Value = new ManualPayload { Number = 30 } };
                byte[] bytes = LuminPackSerializer.Serialize(value);
                string json = LuminPackSerializer.SerializeJson(value);
                if (!bytes.AsSpan().SequenceEqual(new byte[] { 3, 11, 0, 0, 0, 1, 20, 0, 0, 0, 32, 0, 0, 0 }))
                    throw new InvalidOperationException("The member formatter changed binary framing.");
                var binary = LuminPackSerializer.Deserialize<AnnotationHolder>(bytes);
                var text = LuminPackSerializer.DeserializeJson<AnnotationHolder>(json);
                if (binary.Manual.Number != 10 || binary.Child.Number != 20 || binary.Value.Number != 30 ||
                    text.Manual.Number != 10 || text.Child.Number != 20 || text.Value.Number != 30)
                    throw new InvalidOperationException("The annotated members failed to round trip.");
                return 3;
            }
        }
        """;

    private const string AssemblyRegistrationFixture = """
        using System;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Code;
        using LuminPack.Core;
        using LuminPack.Option;
        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.REGISTRATION)]
        [LuminPackable, LuminPackUnion(1, typeof(KnownCase))] public abstract partial class SettingsUnion { }
        [LuminPackable] public sealed partial class KnownCase : SettingsUnion { public int Value; }
        public sealed class ExtraCase : SettingsUnion { public int Value; }
        public sealed class ManualValue { public int Value; }
        public static class RegistrationFixture
        {
            public static int Exercise()
            {
                LuminPackFormatterRegistry.Register(typeof(ManualValue), new LuminPackWriteDelegate<ManualValue>(Write),
                    new LuminPackReadDelegate<ManualValue>(Read), new LuminPackJsonWriteDelegate<ManualValue>(WriteJson),
                    new LuminPackJsonReadDelegate<ManualValue>(ReadJson));
                bool binary = false, json = false;
                try { binary = OpaqueRead<ManualValue>(OpaqueWrite(new ManualValue { Value = 17 })).Value == 17; }
                catch (Exception error) { if (!error.Message.Contains("No formatter is registered")) throw; }
                try { json = OpaqueReadJson<ManualValue>(OpaqueWriteJson(new ManualValue { Value = 23 })).Value == 23; }
                catch (Exception error) { if (!error.Message.Contains("No formatter is registered")) throw; }
                if (binary != EXPECTED || json != EXPECTED) throw new Exception("Registered fallback ignored the assembly policy.");
                SettingsUnion known = new KnownCase { Value = 29 };
                SettingsUnion copy = null;
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(known), ref copy);
                if (copy is not KnownCase item || item.Value != 29) throw new Exception("Known Union dispatch changed.");
                LuminPackUnionFormatter<SettingsUnion>.Register<ExtraCase>(77, WriteUnion, ReadUnion, WriteUnionJson, ReadUnionJson);
                bool external = false;
                try
                {
                    SettingsUnion extra = new ExtraCase { Value = 31 };
                    LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(extra), ref copy);
                    external = copy is ExtraCase restored && restored.Value == 31;
                }
                catch (Exception error) { if (!error.Message.Contains("Union", StringComparison.OrdinalIgnoreCase)) throw; }
                if (external != EXPECTED) throw new Exception("Registered Union fallback ignored the assembly policy.");
                return 4;
            }
            private static byte[] OpaqueWrite<T>(T value) => LuminPackSerializer.Serialize(value);
            private static T OpaqueRead<T>(byte[] bytes) => LuminPackSerializer.Deserialize<T>(bytes);
            private static string OpaqueWriteJson<T>(T value) => LuminPackSerializer.SerializeJson(value);
            private static T OpaqueReadJson<T>(string json) => LuminPackSerializer.DeserializeJson<T>(json);
            private static void Write(ref LuminPackWriter writer, in ManualValue value) => writer.Write(value.Value);
            private static void Read(ref LuminPackReader reader, ref ManualValue value) => value = new ManualValue { Value = reader.Read<int>() };
            private static void WriteJson(ref LuminPackJsonWriter writer, in ManualValue value) => writer.Write<LuminPackUtf16LengthOptions>(value.Value);
            private static void ReadJson(ref LuminPackJsonReader reader, ref ManualValue value) => value = new ManualValue { Value = reader.Read<int, LuminPackUtf16LengthOptions>() };
            private static void WriteUnion(ref LuminPackWriter writer, ref SettingsUnion value) { writer.WriteUnionHeader(77); writer.Write(((ExtraCase)value).Value); }
            private static void ReadUnion(ref LuminPackReader reader, ref SettingsUnion value) => value = new ExtraCase { Value = reader.Read<int>() };
            private static void WriteUnionJson(ref LuminPackJsonWriter writer, ref SettingsUnion value) => throw new Exception("Not used.");
            private static void ReadUnionJson(ref LuminPackJsonReader reader, ref SettingsUnion value) => throw new Exception("Not used.");
        }
        """;

    private const string AssemblyInputFixture = """
        using System;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Code;
        using LuminPack.Core;
        using LuminPack.Option;
        [assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.ENCODING, UntrustedData = true, MaxInputBytes = 8, MaxDepth = 2)]
        public static class AssemblyInputFixture
        {
            public static int Exercise()
            {
                LuminPackFormatterRegistry<PROFILE>.Register(typeof(int), new LuminPackWriteDelegate<int>(Write), new LuminPackReadDelegate<int>(Read),
                    new LuminPackJsonWriteDelegate<int>(WriteJson), new LuminPackJsonReadDelegate<int>(ReadJson));
                if (OpaqueRead<int>("1") != 1) throw new Exception("The assembly JSON encoding did not reach fallback.");
                Reject("01");
                Reject("123456789");
                return 3;
            }
            private static void Reject(string text) { try { OpaqueRead<int>(text); } catch { return; } throw new Exception("Assembly input policy was bypassed: " + text); }
            private static T OpaqueRead<T>(string text) => LuminPackSerializer.DeserializeJson<T>(text);
            private static void Write(ref LuminPackWriter writer, in int value) => writer.Write(value);
            private static void Read(ref LuminPackReader reader, ref int value) => value = reader.Read<int>();
            private static void WriteJson(ref LuminPackJsonWriter writer, in int value) => writer.Write<PROFILE>(value);
            private static void ReadJson(ref LuminPackJsonReader reader, ref int value) => value = reader.Read<int, PROFILE>();
        }
        """;
}
