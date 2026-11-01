using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks registered generic overloads, concrete roots and independent consumers.</summary>
internal static class SerializerGenericOverloadRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Executes independent consumers and checks roots supplied by another source generator.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void Run(MetadataReference[] references)
    {
        VerifyRegisteredOnly(references);
        const string models = """
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
            [LuminPackable] public sealed class GenericPayload { public int Value; public string Text = ""; }
            [LuminPackable] public sealed class UnusedPayload { public long Value; }
            public static class GenericRegistration
            {
                public static unsafe void Register<TProfile>() where TProfile : struct, LuminPack.Option.ILuminPackOptions
                    => LuminPack.LuminPackSerializer.Register<GenericPayload, TProfile>(&Write<TProfile>, &Read<TProfile>, &WriteJson<TProfile>, &ReadJson<TProfile>);
                private static void Write<TProfile>(ref LuminPack.Core.LuminPackWriter writer, in GenericPayload value) where TProfile : struct, LuminPack.Option.ILuminPackOptions
                    => LuminPack.LuminPackSerializer.Serialize<TProfile>(ref writer, in value);
                private static void Read<TProfile>(ref LuminPack.Core.LuminPackReader reader, ref GenericPayload value) where TProfile : struct, LuminPack.Option.ILuminPackOptions
                    => LuminPack.LuminPackSerializer.Deserialize<TProfile>(ref reader, ref value);
                private static void WriteJson<TProfile>(ref LuminPack.Core.LuminPackJsonWriter writer, in GenericPayload value) where TProfile : struct, LuminPack.Option.ILuminPackOptions
                    => LuminPack.LuminPackSerializer.SerializeJson<TProfile>(ref writer, in value);
                private static void ReadJson<TProfile>(ref LuminPack.Core.LuminPackJsonReader reader, ref GenericPayload value) where TProfile : struct, LuminPack.Option.ILuminPackOptions
                    => LuminPack.LuminPackSerializer.DeserializeJson<TProfile>(ref reader, ref value);
            }
            """;
        var input = CSharpCompilation.Create("GenericOverloadLibrary", [CSharpSyntaxTree.ParseText(models, Parse, "Models.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = NewDriver().RunGeneratorsAndUpdateCompilation(input, out var library, out _);
        CheckAppendEntries(driver);
        Require(ConcreteMethods(driver).All(method => !method.ToString().Contains("global::UnusedPayload", StringComparison.Ordinal)),
            "A registration callback retained an unrelated concrete payload API.");
        byte[] image = Emit(library);
        const string consumer = """
            using System;
            using System.IO;
            using System.Text;
            using System.Threading;
            using LuminPack;
            using LuminPack.Option;
            using LuminPack.Utility;
            public static class Consumer
            {
                public static int Check()
                {
                    int cases = 0;
                    foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            {
                if (encoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)
                    RunProfile<global::LuminPack.Option.LuminPackUtf8LengthOptions>();
                else
                    RunProfile<global::LuminPack.Option.LuminPackUtf16LengthOptions>();
                void RunProfile<TProfile>()
                    where TProfile : struct, global::LuminPack.Option.ILuminPackOptions
                {
                    GenericRegistration.Register<TProfile>();
                    var value = new GenericPayload
                    {
                        Value = 97,
                        Text = new string ('\u03bb', 1400)
                    };
                    byte[] expected = LuminPackSerializer.Serialize<GenericPayload, TProfile>(in value);
                    string json = LuminPackSerializer.SerializeJson<GenericPayload, TProfile>(value);
                    byte[] jsonBytes = (encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
                    var buffer = LuminBufferWriterPool.Rent();
                    try
                    {
                        var binaryReader = new LuminPack.Core.LuminPackReader(buffer);
                        var jsonReader = new LuminPack.Core.LuminPackJsonReader(buffer);
                        if (!ReferenceEquals(binaryReader.OptionState, buffer.ReaderState) ||
                            !ReferenceEquals(jsonReader.OptionState, buffer.ReaderState))
                            throw new Exception("Buffer readers did not share the buffer reference state.");
                        LuminPackSerializer.Serialize<GenericPayload, TProfile>(in value, buffer);
                        if (!buffer.GetSpan().SequenceEqual(expected))
                            throw new Exception("Buffer-owned binary fallback ignored its configuration.");
                        GenericPayload restored = null!;
                        LuminPackSerializer.Deserialize<GenericPayload, TProfile>(buffer, ref restored);
                        Validate(value, restored);
                        buffer.ResetCore();
                        LuminPackSerializer.SerializeJson<GenericPayload, TProfile>(value, buffer);
                        if (!buffer.GetSpan().SequenceEqual(jsonBytes))
                            throw new Exception("Buffer-owned JSON fallback ignored its encoding.");
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(buffer, ref restored) != jsonBytes.Length)
                            throw new Exception("Buffer JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (typeof(LuminBufferWriter).GetProperty("Option") != null)
                            throw new Exception("The buffer retained mutable configuration.");
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(json, ref restored) != jsonBytes.Length)
                            throw new Exception("String JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(json.AsSpan(), ref restored) != jsonBytes.Length)
                            throw new Exception("Character JSON consumed-byte count changed.");
                        Validate(value, restored);
                        if (LuminPackSerializer.DeserializeJson<GenericPayload, TProfile>(jsonBytes.AsSpan(), ref restored) != jsonBytes.Length)
                            throw new Exception("Byte JSON consumed-byte count changed.");
                        Validate(value, restored);
                    }
                    finally
                    {
                        LuminBufferWriterPool.Return(buffer);
                    }

                    using var stream = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(stream, value).GetAwaiter().GetResult();
                    if (!stream.ToArray().AsSpan().SequenceEqual(expected))
                        throw new Exception("Async generic fallback changed the binary payload.");
                    stream.Position = 0;
                    Validate(value, LuminPackSerializer.DeserializeAsync<GenericPayload, TProfile>(stream).GetAwaiter().GetResult());
                    using var cancelled = new MemoryStream();
                    try
                    {
                        LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(cancelled, value, new CancellationToken(true)).GetAwaiter().GetResult();
                        throw new Exception("Async cancellation was ignored.");
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    using var recovered = new MemoryStream();
                    LuminPackSerializer.SerializeAsync<GenericPayload, TProfile>(recovered, value).GetAwaiter().GetResult();
                    if (!recovered.ToArray().AsSpan().SequenceEqual(expected))
                        throw new Exception("Cancelled write leaked pooled operation state.");
                    cases++;
                }
            }        return cases;
                }
                private static void Validate(GenericPayload expected, GenericPayload actual)
                {
                    if (actual.Value != expected.Value || actual.Text != expected.Text) throw new Exception("Generic fallback lost payload data.");
                }
            }
            """;
        var consumerInput = CSharpCompilation.Create("GenericOverloadConsumer", [CSharpSyntaxTree.ParseText(consumer, Parse)], references.Append(MetadataReference.CreateFromImage(image)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var loadContext = new AssemblyLoadContext("GenericOverloadConsumers", isCollectible: true);
        try
        {
            loadContext.LoadFromStream(new MemoryStream(image));
            var consumerAssembly = loadContext.LoadFromStream(new MemoryStream(Emit(consumerInput)));
            Require((int)consumerAssembly.GetType("Consumer")!.GetMethod("Check")!.Invoke(null, null)! == 2, "Generic overload consumers omitted an encoding.");
        }
        finally { loadContext.Unload(); }

        const string source = """
            public static class Entry
            {
                public static string Check()
                {
                    GenericRegistration.Register<LuminPack.Option.LuminPackDefaultOptions>();
                    var value = new GenericPayload { Value = 103, Text = "other generator" };
                    foreach (var copy in new[] { value, OtherContext.Value })
                    {
                        string json = LuminPack.LuminPackSerializer.SerializeJson(copy);
                        LuminPack.LuminPackSerializer.DeserializeJson(OtherContext.Json, ref value);
                    }
                    return value.Text;
                }
            }
            """;
        var dependent = input.WithAssemblyName("OtherGeneratorOverloadRoots").AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, Parse, "Entry.cs"));
        var otherDriver = NewDriver(new OtherContextGenerator()).RunGeneratorsAndUpdateCompilation(dependent, out var completed, out _);
        Require(ConcreteMethods(otherDriver).All(method => !method.ToString().Contains("global::UnusedPayload", StringComparison.Ordinal)),
            "An unresolved contextual type retained unrelated concrete overloads.");
        var completedAssembly = Assembly.Load(Emit(completed));
        Require((string)completedAssembly.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)! == "from generated context",
            "A later generator's contextual type did not bind through the generic fallback.");
        Console.WriteLine("Passed complete external generic overloads, UTF-8/UTF-16 ref reads, buffer contexts, cancellation recovery, and cross-generator API pruning.");
    }

    /// <summary>Checks registered-only generic boundaries while concrete roots preserve bytes and state ownership.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void VerifyRegisteredOnly(MetadataReference[] references)
    {
        const string fixture = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Generated;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.__MODE__, __REGISTRATION__)]
            [LuminPackable] public partial class Item { public int Id; public string Name = ""; }
            [LuminPackable] public partial class Box<T> { public T Value = default!; }
            [LuminPackable, LuminPackUnion(3, typeof(Case))] public abstract partial class Union { }
            [LuminPackable] public sealed partial class Case : Union { public int Value; }
            [LuminPackable] public partial struct OverrideValue { public int Value; }
            #if REGISTER
            [LuminPackable(GeneratorType.Custom)] public partial struct CustomValue
            {
                public int Value;
                public static void Serialize(ref LuminPackWriter writer, in CustomValue value) => writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader, ref CustomValue value) => reader.Read(ref value.Value);
            }
            #endif
            public static class Entry
            {
                static byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value);
                static T Load<T>(ReadOnlySpan<byte> bytes) { T value = default!; LuminPackSerializer.Deserialize(bytes, ref value); return value; }
                static string SaveJson<T>(T value) => LuminPackSerializer.SerializeJson(value);
                static T LoadJson<T>(string json) { T value = default!; LuminPackSerializer.DeserializeJson(json, ref value); return value; }
                static byte[] SaveProfile<T>(T value) => LuminPackSerializer.Serialize<T, LuminPack.Option.LuminPackUtf8LengthOptions>(value);
                static T LoadProfile<T>(ReadOnlySpan<byte> bytes) { T value = default!; LuminPackSerializer.Deserialize<T, LuminPack.Option.LuminPackUtf8LengthOptions>(bytes, ref value); return value; }
                static void ExpectMissing(Action action)
                {
                    try { action(); }
                    catch (Exception exception) when (exception.Message.StartsWith("No formatter is registered", StringComparison.Ordinal)) { return; }
                    throw new Exception("An unregistered generic operation did not report a missing formatter.");
                }
                static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
                public static string Check()
                {
                    var digest = new List<byte>();
                    var item = new Item { Id = 42, Name = "example" };
                    var bytes = LuminPackSerializer.Serialize(item); Item restored = null!;
                    LuminPackSerializer.Deserialize(bytes, ref restored); Require(restored.Id == 42 && restored.Name == "example", "Direct object"); digest.AddRange(bytes);
                    int number = 37; var integers = LuminPackSerializer.Serialize(number); int restoredNumber = 0;
                    LuminPackSerializer.Deserialize(integers, ref restoredNumber); Require(restoredNumber == number, "Direct int"); digest.AddRange(integers);
                    string text = "plain"; var strings = LuminPackSerializer.Serialize(text); string restoredText = null!;
                    LuminPackSerializer.Deserialize(strings, ref restoredText); Require(restoredText == text, "Direct string"); digest.AddRange(strings);
                    var box = new Box<int> { Value = 51 }; var boxBytes = LuminPackSerializer.Serialize(box); Box<int> restoredBox = null!;
                    LuminPackSerializer.Deserialize(boxBytes, ref restoredBox); Require(restoredBox.Value == 51, "Direct closed generic model"); digest.AddRange(boxBytes);
                    var list = new List<Item> { item }; var listBytes = LuminPackSerializer.Serialize(list); List<Item> restoredList = null!;
                    LuminPackSerializer.Deserialize(listBytes, ref restoredList); Require(restoredList[0].Name == item.Name, "Direct collection"); digest.AddRange(listBytes);
                    var map = new Dictionary<string, Item> { ["key"] = item }; var mapBytes = LuminPackSerializer.Serialize(map); Dictionary<string, Item> restoredMap = null!;
                    LuminPackSerializer.Deserialize(mapBytes, ref restoredMap); Require(restoredMap["key"].Id == 42, "Direct dictionary"); digest.AddRange(mapBytes);
                    var array = new[] { 3, 5, 7 }; var arrayBytes = LuminPackSerializer.Serialize(array); int[] restoredArray = null!;
                    LuminPackSerializer.Deserialize(arrayBytes, ref restoredArray); Require(restoredArray.SequenceEqual(array), "Direct array"); digest.AddRange(arrayBytes);
                    Union union = new Case { Value = 97 }; var unionBytes = LuminPackSerializer.Serialize(union); Union restoredUnion = null!;
                    LuminPackSerializer.Deserialize(unionBytes, ref restoredUnion); Require(restoredUnion is Case { Value: 97 }, "Direct Union"); digest.AddRange(unionBytes);
                    string json = LuminPackSerializer.SerializeJson(item); Item restoredJson = null!;
                    LuminPackSerializer.DeserializeJson(json, ref restoredJson); Require(restoredJson.Id == 42 && restoredJson.Name == "example", "Direct JSON"); digest.AddRange(System.Text.Encoding.UTF8.GetBytes(json));
                    ExpectMissing(() => Save(item)); ExpectMissing(() => Load<Item>(bytes));
                    ExpectMissing(() => Save(number)); ExpectMissing(() => Save(text));
                    ExpectMissing(() => SaveJson(item)); ExpectMissing(() => LoadJson<Item>(json));
            #if REGISTER
                    Register();
                    Require(Save(item).SequenceEqual(bytes) && Load<Item>(bytes).Name == "example", "Registered generic binary");
                    Require(SaveJson(item) == json && LoadJson<Item>(json).Id == 42, "Registered generic JSON");
                    var overridden = new OverrideValue { Value = 37 };
                    var directOverride = LuminPackSerializer.Serialize(overridden);
                    Require(Load<OverrideValue>(Save(overridden)).Value == 37 && !Save(overridden).SequenceEqual(directOverride), "Registration precedence");
                    Require(LuminPackSerializer.Serialize(overridden).SequenceEqual(directOverride), "Concrete API bypasses registration");
                    Require(LuminPackSerializer.Serialize(number).SequenceEqual(integers), "Concrete API bypasses registration");
                    Require(LuminPackSerializer.Serialize(item).SequenceEqual(bytes), "Concrete object API after registration");
                    var profileBytes = LuminPackSerializer.Serialize<LuminPack.Option.LuminPackUtf8LengthOptions>(item);
                    Require(SaveProfile(item).SequenceEqual(profileBytes) && LoadProfile<Item>(profileBytes).Name == "example", "Registered explicit profile");
                    Require(Load<CustomValue>(Save(new CustomValue { Value = 901 })).Value == 901, "Custom automatic Register");
            #endif
                    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(digest.ToArray()));
                }
            #if REGISTER
                static unsafe void Register()
                {
                    LuminPackSerializer.Register<Item>(&Write, &Read, &WriteJson, &ReadJson);
                    LuminPackSerializer.Register<OverrideValue>(&WriteOverride, &ReadOverride);
                    LuminPackSerializer.Register<Item, LuminPack.Option.LuminPackUtf8LengthOptions>(&WriteProfile, &ReadProfile);
                }
                static void Write(ref LuminPackWriter writer, in Item value) => LuminPackSerializer.Serialize(ref writer, in value);
                static void Read(ref LuminPackReader reader, ref Item value) => LuminPackSerializer.Deserialize(ref reader, ref value);
                static void WriteJson(ref LuminPackJsonWriter writer, in Item value) => LuminPackSerializer.SerializeJson(ref writer, in value);
                static void ReadJson(ref LuminPackJsonReader reader, ref Item value) => LuminPackSerializer.DeserializeJson(ref reader, ref value);
                static void WriteOverride(ref LuminPackWriter writer, in OverrideValue value) { var changed = new OverrideValue { Value = value.Value + 1000 }; LuminPackSerializer.Serialize(ref writer, in changed); }
                static void ReadOverride(ref LuminPackReader reader, ref OverrideValue value) { LuminPackSerializer.Deserialize(ref reader, ref value); value.Value -= 1000; }
                static void WriteProfile(ref LuminPackWriter writer, in Item value) => LuminPackSerializer.Serialize<LuminPack.Option.LuminPackUtf8LengthOptions>(ref writer, in value);
                static void ReadProfile(ref LuminPackReader reader, ref Item value) => LuminPackSerializer.Deserialize<LuminPack.Option.LuminPackUtf8LengthOptions>(ref reader, ref value);
            #endif
            }
            """;
        string? digest = null;
        foreach (string mode in new[] { "Full", "Minimal" })
        foreach (var (allowUnsafe, disabled) in new[] { (true, false), (false, false), (true, true) })
        {
            bool registered = allowUnsafe && !disabled;
            var parse = Parse.WithPreprocessorSymbols("NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", registered ? "REGISTER" : "SAFE");
            string source = fixture.Replace("__MODE__", mode, StringComparison.Ordinal)
                .Replace("__REGISTRATION__", disabled ? "LuminPackRegisterMode.Disabled" : "LuminPackRegisterMode.Auto", StringComparison.Ordinal);
            var input = CSharpCompilation.Create("RegisteredDispatch" + mode + allowUnsafe + disabled,
                [CSharpSyntaxTree.ParseText(source, parse, "Contracts.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver Fresh() => CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            string Text(GeneratorDriver current) => string.Join("\n", current.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .OrderBy(file => file.HintName, StringComparer.Ordinal).Select(file => file.HintName + "\n" + file.SourceText.ToString()));
            var driver = Fresh().RunGeneratorsAndUpdateCompilation(input, out var output, out _);
            var files = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToArray();
            Require(!files.Any(file => file.HintName == "LuminPack.Dispatch.Lazy.g.cs"), "The generated fallback table file remains.");
            string dispatch = files.Single(file => file.HintName == "LuminPack.Dispatch.Formatters.g.cs").SourceText.ToString();
            Require(!dispatch.Contains("typeof(T) == typeof(", StringComparison.Ordinal) && !dispatch.Contains("Generated<", StringComparison.Ordinal) &&
                !dispatch.Contains("SafeSerialize", StringComparison.Ordinal), "A generated payload factory or delegate cache remains.");
            var loaded = Assembly.Load(Emit(output));
            string result = (string)loaded.GetType("Entry")!.GetMethod("Check")!.Invoke(null, null)!;
            digest ??= result;
            Require(result == digest, "Concrete binary or JSON bytes changed across generation and registration modes.");
            var edited = input.ReplaceSyntaxTree(input.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(
                source.Replace("public int Id;", "public long Id;", StringComparison.Ordinal), parse, "Contracts.cs"));
            var retained = driver.RunGenerators(edited);
            Require(Text(retained) == Text(Fresh().RunGenerators(edited)) && Text(retained.RunGenerators(input)) == Text(driver),
                "Registered dispatch or Root navigation differs after a retained/fresh edit or restoration.");
        }
        Console.WriteLine("Passed registered-only generic dispatch, concrete wire equality, Custom/profile registrations, missing-entry invalidation, disabled registration and retained/fresh edits.");
    }

    /// <summary>Creates independent serializer generators and an optional contextual source generator.</summary>
    /// <param name="additional">An optional source generator supplying contextual types after root analysis.</param>
    /// <returns>A driver using the same framework settings as the fixture.</returns>
    private static GeneratorDriver NewDriver(ISourceGenerator? additional = null)
    {
        var generators = new List<ISourceGenerator> { new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator() };
        if (additional != null) generators.Add(additional);
        return CSharpGeneratorDriver.Create(generators, parseOptions: Parse);
    }

    /// <summary>Checks caller-buffer ownership and exceptional cursor detachment in shared compiled-profile entries.</summary>
    /// <param name="driver">The completed generator driver.</param>
    private static void CheckAppendEntries(GeneratorDriver driver)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        Require(!methods.SelectMany(method => method.DescendantNodes().OfType<VariableDeclaratorSyntax>()).Any(variable =>
            variable.Initializer?.Value is MemberAccessExpressionSyntax access &&
            access.Name.Identifier.ValueText is "ReaderState" or "WriterState"),
            "A generated facade retains an unnecessary buffer state local.");
        foreach (string core in new[] { "SerializeCore", "SerializeJsonCore" })
        {
            var bodies = methods.Where(method => method.Identifier.ValueText == core).ToArray();
            Require(bodies.Length > 0, "A pooled write core is missing.");
            foreach (var body in bodies)
                Require(body.ParameterList.Parameters.All(parameter => !parameter.Type!.ToString().Contains("OptionalState", StringComparison.Ordinal)),
                    "A pooled write core still takes a separate state.");
            var boundBodies = bodies.Where(body => body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Any(creation => creation.Type.ToString() is "LuminPackWriter" or "LuminPackJsonWriter")).ToArray();
            Require(boundBodies.Length > 0, "A pooled write core has no bound state body.");
            foreach (var body in boundBodies)
            {
                Require(body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(creation =>
                    creation.Type.ToString() is "LuminPackWriter" or "LuminPackJsonWriter" && creation.ArgumentList!.Arguments.Count == 1),
                    "A pooled write core does not use the single-buffer constructor: " + body.ToString());
            }
        }
        var asyncWrites = methods.Where(method => method.Identifier.ValueText == "SerializeAsync" && method.TypeParameterList?.Parameters.Count == 1).ToArray();
        Require(asyncWrites.Length > 0, "A generic asynchronous write is missing.");
        Require(asyncWrites.All(method => !method.ToString().Contains("_threadStaticWriterOptionalState", StringComparison.Ordinal)),
            "A pooled asynchronous write still uses serializer TLS.");
        var entries = methods.Where(method => method.Identifier.ValueText is "Serialize" or "SerializeJson" &&
            method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "LuminBufferWriter")).ToArray();
        Require(entries.Length >= 4, "Shared binary/JSON default and compiled-profile append entries are missing.");
        foreach (var entry in entries)
            Require(!entry.ToString().Contains("ResetCore(", StringComparison.Ordinal) &&
                !entry.ToString().Contains("ResetOperationState(", StringComparison.Ordinal) &&
                !entry.DescendantNodes().OfType<CatchClauseSyntax>().Any(),
                "A caller-buffer write entry still clears or catches the caller's session.");
        var boundEntries = entries.Where(method => method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Any(creation => creation.Type.ToString() is "LuminPackWriter" or "LuminPackJsonWriter")).ToArray();
        Require(boundEntries.Length >= 2, "Shared append entries lost their concrete binary or JSON execution state.");
        foreach (var entry in boundEntries)
        {
            bool json = entry.Identifier.ValueText == "SerializeJson";
            Require(!entry.DescendantNodes().OfType<FinallyClauseSyntax>().Any(clause => clause.ToString().Contains("CompleteWrite(")),
                "Binary successful-boundary publication or JSON pointer detachment changed.");
            Require(entry.ToString().Contains("CompleteWrite("), "An append entry never publishes successful completion.");
        }
    }

    /// <summary>Obtains concrete public serializer declarations without counting shared generic cores.</summary>
    /// <param name="driver">The completed generator driver.</param>
    /// <returns>Concrete generated facade methods.</returns>
    private static MethodDeclarationSyntax[] ConcreteMethods(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Where(file => file.HintName.StartsWith("LuminPackSerializer.", StringComparison.Ordinal))
            .SelectMany(file => CSharpSyntaxTree.ParseText(file.SourceText, Parse).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.TypeParameterList == null &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() is "global::GenericPayload" or "global::UnusedPayload")).ToArray();

    /// <summary>Compiles a complete fixture and reports every actual compiler error.</summary>
    /// <param name="compilation">The library or consumer compilation.</param>
    /// <returns>The emitted managed assembly image.</returns>
    private static byte[] Emit(Compilation compilation)
    {
        using var image = new MemoryStream();
        var result = compilation.Emit(image);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return image.ToArray();
    }

    /// <summary>Reports a generic overload or API-root contract failure.</summary>
    /// <param name="condition">The expected invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Supplies contextual expressions that another generator cannot see during initial root analysis.</summary>
    private sealed class OtherContextGenerator : ISourceGenerator
    {
        /// <summary>Registers no syntax analysis for this fixed contextual fixture.</summary>
        /// <param name="context">The generator initialization context.</param>
        public void Initialize(GeneratorInitializationContext context) { }

        /// <summary>Produces strongly typed properties that repair the original contextual binding.</summary>
        /// <param name="context">The current generation context.</param>
        public void Execute(GeneratorExecutionContext context)
            => context.AddSource("OtherContext.g.cs", """
                public static class OtherContext
                {
                    public static GenericPayload Value => new GenericPayload();
                    public static string Json => "{\"Value\":107,\"Text\":\"from generated context\"}";
                }
                """);
    }
}
