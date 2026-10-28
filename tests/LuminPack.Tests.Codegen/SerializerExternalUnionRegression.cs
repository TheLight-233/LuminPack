using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Protects virtual dispatch for assembly-marked Union roots across a real metadata boundary.</summary>
internal static class SerializerExternalUnionRegression
{
    private static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview, DocumentationMode.Parse,
        preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);

    /// <summary>Checks local overrides of imported slots, generic slot switches, and API pruning without manual registration.</summary>
    /// <param name="references">Runtime and platform references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string librarySource = """
            using LuminPack.Attribute;
            namespace ForeignUnion;
            [LuminPackable] public abstract partial class Root
            {
                private string _rootPadding = "foreign root storage";
                public int BaseNumber { get; set; } = 59;
                public string RootPadding() => _rootPadding;
            }
            [LuminPackable] public partial interface IRoot { }
            [LuminPackable] public partial interface IUnmarked { }
            [LuminPackable, LuminPackUnion(1, typeof(OwnerCase<>)), LuminPackUnion(7, typeof(OwnerCase<int>))]
            public abstract partial class GenericRoot<T> { }
            [LuminPackable] public partial class OwnerCase<T> : GenericRoot<T> { public T Value; }
            public static class OwnerCalls
            {
                public static byte[] KnownString(OwnerCase<string> value) => LuminPack.LuminPackSerializer.Serialize(value);
                public static byte[] KnownInteger(OwnerCase<int> value) => LuminPack.LuminPackSerializer.Serialize(value);
            }
            """;
        var library = Generate("ForeignUnionLibrary", librarySource, references);
        using var libraryImage = new MemoryStream();
        var libraryEmit = library.Emit(libraryImage);
        Require(libraryEmit.Success, string.Join(Environment.NewLine, libraryEmit.Diagnostics));
        byte[] libraryBytes = libraryImage.ToArray();
        var consumerReferences = references.Append(MetadataReference.CreateFromImage(libraryBytes)).ToArray();
        const string consumerSource = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            using ForeignUnion;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [assembly: LuminPackOption(StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF16)]
            [assembly: LuminPackable(typeof(Root))]
            [assembly: LuminPackable(typeof(IRoot))]
            [assembly: LuminPackable(typeof(GenericRoot<>))]
            [LuminPackable] public sealed partial class LocalCase : Root
            {
                [LuminPackInclude] private int _secret = 37;
                public string Value { get; private set; } = "private auto-property";
                public int Secret() => _secret;
            }
            [LuminPackable] public partial class LocalGeneric<T> : GenericRoot<T> { public T Value; }
            [LuminPackable] public partial struct StructCase : IRoot { public int Value; }
            [LuminPackable] public sealed partial class UnmarkedCase : IUnmarked { public int Value; }
            public static class ExternalUnionEntry
            {
                public static int Check()
                {
                    Root local = new LocalCase();
                    var bytes = LuminPackSerializer.Serialize(local);
                    if (LuminPackSerializer.Deserialize<Root>(bytes) is not LocalCase restored || restored.Secret() != 37 || restored.Value != "private auto-property" ||
                        restored.BaseNumber != 59 || restored.RootPadding() != "foreign root storage")
                        throw new Exception("External root did not discover its local private-storage case.");
                    string json = LuminPackSerializer.SerializeJson(local);
                    if (LuminPackSerializer.DeserializeJson<Root>(json) is not LocalCase { Value: "private auto-property" })
                        throw new Exception("External root JSON dispatch changed.");
                    if (LuminPackSerializer.Sizeof(local) != bytes.Length) throw new Exception("External root size dispatch changed.");
                    GenericRoot<int> localInt = new LocalGeneric<int> { Value = 43 };
                    GenericRoot<string> localString = new LocalGeneric<string> { Value = "closed generic" };
                    GenericRoot<int> ownerInt = new OwnerCase<int> { Value = 47 };
                    GenericRoot<string> ownerString = new OwnerCase<string> { Value = "owner" };
                    CheckGeneric(localInt); CheckGeneric(localString); CheckGeneric(ownerInt); CheckGeneric(ownerString);
                    if (LuminPackSerializer.Serialize(ownerInt)[0] != 7 || LuminPackSerializer.Serialize(ownerString)[0] != 1)
                        throw new Exception("Imported closed/open generic priority changed.");
                    IRoot boxed = new StructCase { Value = 53 };
                    byte[] structBytes = LuminPackSerializer.Serialize(boxed);
                    if (LuminPackSerializer.Deserialize<IRoot>(structBytes) is not StructCase { Value: 53 } ||
                        LuminPackSerializer.DeserializeJson<IRoot>(LuminPackSerializer.SerializeJson(boxed)) is not StructCase { Value: 53 } ||
                        LuminPackSerializer.Sizeof(boxed) != structBytes.Length)
                        throw new Exception("An imported interface lost its value-type case.");
                    IUnmarked unmarked = new UnmarkedCase { Value = 654 };
                    bool rejected = false;
                    try { LuminPackSerializer.Serialize(unmarked); }
                    catch (Exception) { rejected = true; }
                    if (!rejected) throw new Exception("An external interface without an assembly mark accepted a case that has no contract tag.");
                    return 6;
                }
                private static void CheckGeneric<T>(GenericRoot<T> value)
                {
                    var bytes = LuminPackSerializer.Serialize(value);
                    var restored = LuminPackSerializer.Deserialize<GenericRoot<T>>(bytes);
                    if (restored.GetType() != value.GetType()) throw new Exception("External generic Union type changed.");
                    string json = LuminPackSerializer.SerializeJson(value);
                    if (LuminPackSerializer.DeserializeJson<GenericRoot<T>>(json).GetType() != value.GetType())
                        throw new Exception("External generic Union JSON type changed.");
                    if (LuminPackSerializer.Sizeof(value) != bytes.Length) throw new Exception("External generic Union size changed.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Light", "Minimal" })
        {
            var output = Generate("ExternalUnionConsumer" + mode, consumerSource.Replace("MODE", mode, StringComparison.Ordinal), consumerReferences);
            var generated = output.SyntaxTrees.Skip(1).SelectMany(tree => tree.GetRoot().DescendantNodes()).OfType<MethodDeclarationSyntax>().ToArray();
            Require(output.SyntaxTrees.Skip(1).Any(tree => tree.ToString().Contains("_rootPadding", StringComparison.Ordinal)),
                "A source case lost the private layout slots of its referenced base.");
            var overrides = generated.Where(method => method.Identifier.ValueText.StartsWith("__LuminPackUnion", StringComparison.Ordinal) &&
                method.Parent is TypeDeclarationSyntax { Identifier.ValueText: "LocalCase" }).ToArray();
            Require(overrides.Length == 10 && overrides.All(method => method.TypeParameterList == null && method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
                method.Modifiers.Any(SyntaxKind.PublicKeyword) && !method.DescendantNodes().OfType<SwitchStatementSyntax>().Any()),
                "An imported ordinary root lost its plain virtual overrides: " + mode);
            Require(overrides.All(method => method.GetLeadingTrivia().Any(trivia => trivia.HasStructure &&
                trivia.GetStructure() is DocumentationCommentTriviaSyntax) && method.AttributeLists.ToString().Contains("EditorBrowsableState.Never", StringComparison.Ordinal)),
                "An imported root override lost XML or its hidden-completion attribute.");
            var genericOverrides = generated.Where(method => method.Identifier.ValueText.StartsWith("__LuminPackUnion", StringComparison.Ordinal) &&
                method.Parent is TypeDeclarationSyntax { Identifier.ValueText: "LocalGeneric" }).ToArray();
            Require(genericOverrides.Length == 10 && genericOverrides.All(method => method.TypeParameterList == null && method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
                method.DescendantNodes().OfType<SwitchStatementSyntax>().Any(statement => statement.Expression.ToString().StartsWith("__LuminPackUnionDispatchSlot_", StringComparison.Ordinal))),
                "An imported generic root lost its virtual plus cached-slot switch: " + mode);
            Require(!generated.Any(method => method.Identifier.ValueText == "Register" &&
                method.Parent is TypeDeclarationSyntax { Identifier.ValueText: "Root" or "GenericRoot" or "IRoot" }),
                "The consumer regenerated an imported root contract.");
            var concrete = generated.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } &&
                method.TypeParameterList == null && method.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                method.ParameterList.Parameters.Any(parameter => parameter.Type?.ToString() == "global::ForeignUnion.Root")).ToArray();
            Require(concrete.Any(method => method.Identifier.ValueText == "Serialize"), "No concrete imported root entry was generated.");
            if (mode != "Full") Require(!concrete.Any(method => method.Identifier.ValueText == "SerializeAsync"), "An unused imported root API survived pruning.");
            using var consumerImage = new MemoryStream();
            var emit = output.Emit(consumerImage);
            Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            var context = new AssemblyLoadContext("ExternalUnion" + mode, isCollectible: true);
            try
            {
                context.LoadFromStream(new MemoryStream(libraryBytes));
                var assembly = context.LoadFromStream(new MemoryStream(consumerImage.ToArray()));
                Require((int)assembly.GetType("ExternalUnionEntry")!.GetMethod("Check")!.Invoke(null, null)! == 6,
                    "External Union execution was incomplete.");
            }
            finally { context.Unload(); }
        }
        Console.WriteLine("Passed assembly-marked external Union roots, public hidden virtual slots, generic slot switches, and API pruning in Full/Light/Minimal without manual registration.");
    }

    /// <summary>Runs both production generators and requires a valid generated compilation.</summary>
    /// <param name="name">The isolated assembly identity.</param>
    /// <param name="source">The input declarations and calls.</param>
    /// <param name="references">The compilation references.</param>
    /// <returns>The generated compilation.</returns>
    private static Compilation Generate(string name, string source, MetadataReference[] references)
    {
        var input = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, Parse, name + ".cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: Parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors));
        return output;
    }

    /// <summary>Reports a broken dispatch or pruning contract.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
