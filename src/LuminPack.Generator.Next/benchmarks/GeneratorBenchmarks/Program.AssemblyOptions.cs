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
        foreach (bool unsafeCode in new[] { true, false })
        foreach (bool registration in new[] { true, false })
        {
            string settings = "[assembly: LuminPack.Attribute.LuminPackGeneratorOptions(register: LuminPack.Attribute.LuminPackRegisterMode." +
                (registration ? "Auto" : "Disabled") + ")]";
            CheckSettings(settings, 15, registration, application: false, unsafeCode);
            CheckSettings(settings + "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(" +
                Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral("Friend", true) + ")]", 15, registration, application: true, unsafeCode);
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

        (GeneratorDriver Driver, Compilation Output) GenerateAssembly(string source, OutputKind kind, bool allowUnsafe)
        {
            var tree = CSharpSyntaxTree.ParseText(source, parse, "AssemblyOptions.cs");
            var input = CSharpCompilation.Create("AssemblyOptions" + checks++, new[] { tree }, references,
                new CSharpCompilationOptions(kind, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            foreach (var result in driver.GetRunResult().Results)
                RequireIncremental(result.Exception == null, result.Exception?.ToString() ?? string.Empty);
            foreach (Diagnostic diagnostic in diagnostics.Concat(output.GetDiagnostics()))
                RequireIncremental(diagnostic.Severity != DiagnosticSeverity.Error, diagnostic.ToString());
            return (driver, output);
        }

        static string Bool(bool value) => value ? "true" : "false";
    }

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
