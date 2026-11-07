using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks concrete interceptor binding against the existing generated wire operations.</summary>
internal static partial class SerializerInterceptorRegression
{
    /// <summary>Executes ordinary calls with and without the compiler interception feature.</summary>
    /// <param name="references">The framework and runtime metadata references.</param>
    /// <param name="il">Whether to exercise the C# 9 IL backend.</param>
    internal static void Run(MetadataReference[] references, bool il = false)
    {
        if (il)
            references = PortableReferences();
        const string source = """"
            using System;
            using System.Collections.Generic;
            using System.IO;
            using System.Runtime.CompilerServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            [LuminPackable] public partial class Payload { public int Number; public string Text; }
            [LuminPackable, LuminPackOption(UntrustedData = true, MaxDepth = 4)] public partial class StrictPayload { public int[] Values; }
            [LuminPackable, LuminPackUnion(3, typeof(UnionCase))] public abstract partial class Union { }
            [LuminPackable] public sealed partial class UnionCase : Union { public int Number; }
            public interface ISink { byte[] Save<T>(T value); }
            public sealed class Sink : ISink { public byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value); }
            public static class GenericOwner<T> { public static byte[] Save(T value) => LuminPackSerializer.Serialize(value); }
            public static class Entry
            {
                public static int Saved;
                public static string ParameterName;
                public static string Selected;
                public static string Caller;
                public static void RecordCaller([CallerMemberName] string caller = "") { Caller = caller; }
                public static void Mark(object value) { Selected = "object"; }
                public static void Mark(Payload value) { Selected = "payload"; }
                private static byte[] Save<T>(T value)
                {
                    Saved++;
                    ParameterName = nameof(T);
                    RecordCaller();
                    Mark(value);
                    return SaveCore(value);
                }
                private static byte[] SaveCore<T>(T value) => LuminPackSerializer.Serialize(value);
                private static byte[] LiteralSave<T>(T value)
                {
                    string literal = @"first
                second";
                    if (literal.Replace("\r\n", "\n") != "first\n    second") throw new Exception("Verbatim literal indentation changed.");
                    #if !NETSTANDARD2_1
                    string raw = """
                        first
                            second
                        """;
                    if (raw.Replace("\r\n", "\n") != "first\n    second") throw new Exception("Raw literal indentation changed.");
                    #endif
                    return LuminPackSerializer.Serialize(value);
                }
                public static byte[] DirectWrite(Payload value) => LuminPackSerializer.Serialize(value);
                public static Payload DirectRead(byte[] value) => LuminPackSerializer.Deserialize<Payload>(value);
                public static byte[] DirectWrapper(Payload value) => SaveCore(value);
                private static byte[] Profile<TOptions>(Payload value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Serialize<TOptions>(value);
                private static byte[] ProfileExplicit<TOptions>(Payload value) where TOptions : struct, ILuminPackOptions
                    => LuminPackSerializer.Serialize<Payload, TOptions>(value);
                private static void Write(ref LuminPackWriter writer, in Payload value) => LuminPackSerializer.Serialize(ref writer, in value);
                private static void Read(ref LuminPackReader reader, ref Payload value) => LuminPackSerializer.Deserialize(ref reader, ref value);
                #if !NO_REGISTER
                public static unsafe void CheckFallback(Payload value, byte[] expected)
                {
                    ISink sink = new Sink();
                    bool missing = false;
                    try { sink.Save(value); } catch (Exception error) when (error.GetType().Name == "LuminPackException") { missing = true; }
                    if (!missing) throw new Exception("An opaque call bypassed registration.");
                    LuminPackSerializer.Register<Payload>(&Write, &Read);
                    if (Convert.ToBase64String(sink.Save(value)) != Convert.ToBase64String(expected)) throw new Exception("Registered fallback mismatch.");
                }
                #endif
                public static string Check()
                {
                    var value = new Payload { Number = 37, Text = "sample" };
                    StrictPayload strict = null;
                    bool rejected = false;
                    try { LuminPackSerializer.DeserializeJson("{\"Values\":[1,2,]}", ref strict); } catch (Exception) { rejected = true; }
                    if (!rejected) throw new Exception("Declaration-owned strict JSON was not enforced.");
                    var bytes = LuminPackSerializer.Serialize(value);
                    Payload restored = null;
                    LuminPackSerializer.Deserialize(bytes, ref restored);
                    if (restored.Number != value.Number || restored.Text != value.Text) throw new Exception("Binary mismatch.");
                    var json = LuminPackSerializer.SerializeJson(value);
                    Payload jsonValue = null;
                    LuminPackSerializer.DeserializeJson(json, ref jsonValue);
                    if (jsonValue.Number != value.Number || jsonValue.Text != value.Text) throw new Exception("JSON mismatch.");
                    var values = new List<Payload> { value };
                    var listBytes = LuminPackSerializer.Serialize(values);
                    List<Payload> list = null;
                    LuminPackSerializer.Deserialize(listBytes, ref list);
                    if (list.Count != 1 || list[0].Number != 37) throw new Exception("Collection mismatch.");
                    #if INTERCEPTED
                    if (LuminPackSerializer.Deserialize<Payload>(bytes).Number != 37) throw new Exception("Value read mismatch.");
                    if (LuminPackSerializer.Serialize<Payload>(value).Length != bytes.Length) throw new Exception("Explicit write mismatch.");
                    if (Convert.ToBase64String(LiteralSave(value)) != Convert.ToBase64String(bytes)) throw new Exception("Literal wrapper mismatch.");
                    if (Convert.ToBase64String(Save(value)) != Convert.ToBase64String(bytes) || Saved != 1 || ParameterName != "T" || Selected != "object" || Caller != "Save")
                        throw new Exception("Closed wrapper semantics changed.");
                    if (LuminPackSerializer.Deserialize<int>(Save(49)) != 49 || Saved != 2) throw new Exception("Closed scalar wrapper mismatch.");
                    var profiled = LuminPackSerializer.Serialize<LuminPackUtf16TokenOptions>(value);
                    Payload profileValue = null;
                    LuminPackSerializer.Deserialize<LuminPackUtf16TokenOptions>(profiled, ref profileValue);
                    if (profileValue.Text != value.Text || Convert.ToBase64String(Profile<LuminPackUtf16TokenOptions>(value)) != Convert.ToBase64String(profiled))
                        throw new Exception("Profile-only spelling mismatch.");
                    if (Convert.ToBase64String(ProfileExplicit<LuminPackUtf16TokenOptions>(value)) != Convert.ToBase64String(profiled)) throw new Exception("Open profile mismatch.");
                    if (Convert.ToBase64String(GenericOwner<Payload>.Save(value)) != Convert.ToBase64String(bytes)) throw new Exception("Generic owner mismatch.");
                    int? nullable = 19;
                    if (LuminPackSerializer.Deserialize<int?>(LuminPackSerializer.Serialize(nullable)) != nullable) throw new Exception("Nullable mismatch.");
                    Union union = new UnionCase { Number = 82 };
                    if (((UnionCase)LuminPackSerializer.Deserialize<Union>(LuminPackSerializer.Serialize(union))).Number != 82) throw new Exception("Union mismatch.");
                    using var stream = new MemoryStream();
                    LuminPackSerializer.SerializeAsync(stream, value).GetAwaiter().GetResult();
                    stream.Position = 0;
                    if (LuminPackSerializer.DeserializeAsync<Payload>(stream).GetAwaiter().GetResult().Number != 37) throw new Exception("Async mismatch.");
                    #if !NO_REGISTER
                    CheckFallback(value, bytes);
                    #endif
                    #endif
                    return Convert.ToBase64String(bytes) + "|" + json + "|" + Convert.ToBase64String(listBytes);
                }
            }
            """";
        string baseline = Execute(references, source, false, il: il);
        string intercepted = Execute(references, source, true, il: il);
        Require(baseline == intercepted, "Interception changed the binary or JSON representation.");
        Require(baseline == Execute(references, source.Replace("LuminPackGenerationMode.Minimal", "LuminPackGenerationMode.Full"), true, il: il),
            "An ignored legacy mode changed the wire representation.");
        Require(baseline == Execute(references, source.Replace("[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]", ""), true, il: il),
            "Automatic generation without an assembly setting changed the wire representation.");
        Require(baseline == Execute(references, source.Replace("LuminPackGenerationMode.Minimal", "LuminPackRegisterMode.Disabled"), true, register: false, il: il),
            "Disabling registration changed static interception.");
        VerifyIncremental(references, il);
        Console.WriteLine("Passed " + (il ? "C# 9 IL rewriting" : "compiler interception and incremental edits") + ": binary/JSON, wrappers, profiles, unions, async, registered fallback, direct-call IL and legacy wire equality.");
    }

    /// <summary>Generates, compiles and executes one independent consumer.</summary>
    /// <param name="references">The framework and runtime metadata references.</param>
    /// <param name="source">The consumer fixture.</param>
    /// <param name="intercept">Whether the compiler enables generated interception.</param>
    /// <param name="register">Whether this fixture exercises opaque registered calls.</param>
    /// <param name="il">Whether to use the installed IL backend.</param>
    /// <returns>The fixture's wire digest.</returns>
    private static string Execute(MetadataReference[] references, string source, bool intercept, bool register = true, bool il = false)
    {
        var symbols = il ? new List<string> { "NETSTANDARD2_1" } : new List<string> { "NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER" };
        if (intercept)
            symbols.Add("INTERCEPTED");
        if (!register)
            symbols.Add("NO_REGISTER");
        if (il && intercept)
            symbols.Add("LUMINPACK_IL_REWRITE");
        var options = new CSharpParseOptions(il ? LanguageVersion.CSharp9 : LanguageVersion.Preview, DocumentationMode.Parse, preprocessorSymbols: symbols);
        if (intercept && !il)
            options = options.WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack") });
        var input = CSharpCompilation.Create("InterceptorConsumer", new[] { CSharpSyntaxTree.ParseText(source, options, "Calls.cs", Encoding.UTF8) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[]
        {
            new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()
        }, parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
        {
            Directory.CreateDirectory(".tmp/interceptor-failure");
            foreach (var generated in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
                File.WriteAllText(Path.Combine(".tmp/interceptor-failure", generated.HintName), generated.SourceText.ToString());
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
        }
        if (intercept)
        {
            var generated = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
            VerifyIndentation(generated);
            Require(generated.Any(file => file.HintName == "LuminPack.Interceptors.g.cs"), "The enabled compiler did not receive interceptors.");
            var outerMethods = generated.SelectMany(file => file.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(type => type.Identifier.ValueText == "LuminPackSerializer").SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>());
            Require(!outerMethods.Any(method => method.Identifier.ValueText == "Serialize" &&
                method.TypeParameterList == null &&
                method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() == "global::Payload"),
                "A concrete allocating overload remained in the global facade.");
        }
        using var image = new MemoryStream();
        using var pdb = new MemoryStream();
        var emit = il ? output.Emit(image, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb)) : output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        image.Position = 0;
        if (il && intercept)
        {
            VerifyIlStackValidation(image.ToArray(), references);
            var result = LuminPack.Build.StaticCallAssembly.Rewrite(image.ToArray(), pdb.ToArray(), references.OfType<PortableExecutableReference>().Select(reference => reference.FilePath!).ToArray());
            Require(result != null && result.Calls > 0 && result.Pdb.Length > 0, "The legacy backend did not replace calls and preserve portable symbols.");
            image.SetLength(0);
            image.Write(result!.Pe);
            image.Position = 0;
            using var resolver = new Mono.Cecil.DefaultAssemblyResolver();
            foreach (string directory in references.OfType<PortableExecutableReference>().Select(reference => Path.GetDirectoryName(reference.FilePath)!).Distinct())
                resolver.AddSearchDirectory(directory);
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(image, new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver });
            Require(LuminPack.Build.StaticCallRewriter.Rewrite(module) == 0, "IL rewriting was not idempotent.");
            using var rewritten = new MemoryStream();
            module.Write(rewritten);
            image.SetLength(0);
            rewritten.Position = 0;
            rewritten.CopyTo(image);
            image.Position = 0;
        }
        var context = new AssemblyLoadContext("interceptor-consumer", isCollectible: true);
        try
        {
            if (il)
                context.LoadFromAssemblyPath(references.OfType<PortableExecutableReference>().Single(reference => Path.GetFileName(reference.FilePath) == "LuminPack.Core.dll").FilePath!);
            var assembly = context.LoadFromStream(image);
            if (intercept)
                VerifyDirectCalls(assembly);
            return (string)assembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>Uses the actual portable runtime and reference pack supported by Unity's compiler.</summary>
    /// <returns>The netstandard 2.1 reference closure.</returns>
    private static MetadataReference[] PortableReferences()
    {
        string dotnet = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "../../.."));
        string pack = Path.Combine(dotnet, "packs", "NETStandard.Library.Ref", "2.1.0", "ref", "netstandard2.1");
        string core = Path.GetFullPath("src/LuminPack.Core/bin/Release/netstandard2.1");
        return Directory.GetFiles(pack, "*.dll").Concat(new[] { "LuminPack.Core.dll", "System.Collections.Immutable.dll", "System.Runtime.CompilerServices.Unsafe.dll" }
                .Select(name => Path.Combine(core, name))).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    /// <summary>Rejects a mismatched generated mapping before publishing an invalid assembly.</summary>
    /// <param name="image">A valid consumer image before rewriting.</param>
    /// <param name="references">Its assembly reference closure.</param>
    private static void VerifyIlStackValidation(byte[] image, MetadataReference[] references)
    {
        using var resolver = new Mono.Cecil.DefaultAssemblyResolver();
        foreach (string directory in references.OfType<PortableExecutableReference>().Select(reference => Path.GetDirectoryName(reference.FilePath)!).Distinct())
            resolver.AddSearchDirectory(directory);
        using var stream = new MemoryStream(image);
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(stream, new Mono.Cecil.ReaderParameters { AssemblyResolver = resolver });
        var target = module.Types.Single(type => type.FullName == "LuminPack.LuminPackSerializer").Methods.First(method =>
            method.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "LuminPack.Generated.StaticCallAttribute") && method.Parameters.Count != 0);
        target.Parameters[0].ParameterType = module.TypeSystem.String;
        bool rejected = false;
        try { LuminPack.Build.StaticCallRewriter.Rewrite(module); }
        catch (InvalidOperationException error) when (error.Message.Contains("argument mismatch", StringComparison.Ordinal)) { rejected = true; }
        Require(rejected, "IL rewriting accepted an incompatible generated argument type.");
    }

    /// <summary>Fails an interceptor contract with its specific reason.</summary>
    /// <param name="condition">The required condition.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
