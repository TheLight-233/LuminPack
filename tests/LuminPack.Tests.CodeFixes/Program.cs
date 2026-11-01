using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using LuminPack.CodeFixes;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

await CodeFixChecks.RunAsync(args);

/// <summary>Exercises exported fixes against real generator diagnostics in a Roslyn workspace.</summary>
internal static class CodeFixChecks
{
    private const string Imports = "using LuminPack.Attribute; using LuminPack.Core;\n";
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    /// <summary>Runs diagnostic, workspace, Fix All and package regressions.</summary>
    /// <param name="args">Optional focused verification selectors.</param>
    /// <returns>The asynchronous test operation.</returns>
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length == 2 && args[0] == "--verify-package")
        {
            VerifyPackage(args[1]);
            Console.WriteLine("Passed bundled generator and code-fix package layout.");
            return;
        }
        if (args.Contains("--verify-cross-document", StringComparer.Ordinal))
        {
            await VerifyCrossDocumentAsync();
            Console.WriteLine("Passed cross-document code fixes.");
            return;
        }
        if (args.Contains("--verify-fix-all", StringComparer.Ordinal))
        {
            await VerifyFixAllAsync();
            Console.WriteLine("Passed document, project and solution Fix All.");
            return;
        }
        var cases = new (string Id, string Source)[]
        {
            ("LuminPack001", "[LuminPackable] public static class Model { }"),
            ("LuminPack005", "[LuminPackable] public partial class Model { [LuminPackOnSerializing] private void Before() { } }"),
            ("LuminPack006", "[LuminPackable] public partial struct Model { public int Value; [LuminPackOnSerializing, LuminPackOnDeserialized] public void Before() { } }"),
            ("LuminPack007", "[LuminPackable] public partial class Parent { public virtual int Value { get; set; } } [LuminPackable] public partial class Model : Parent { [LuminPackIgnore, LuminPackInclude] public override int Value { get; set; } }"),
            ("LuminPack008", "[LuminPackable, LuminPackUnion(0, typeof(Case))] public sealed partial class Model { } [LuminPackable] public partial class Case : Model { }"),
            ("LuminPack009", "[LuminPackable, LuminPackUnion(0, typeof(Case))] public partial class Model { } [LuminPackable] public partial class Case : Model { }"),
            ("LuminPack014", "[LuminPackable, LuminPackUnion(0, typeof(Case))] public abstract partial class Model { } public class Case : Model { public int Value; }"),
            ("LuminPack025", "[LuminPackable] public partial class Model { public int Value; public Model() { } public Model(int value) { Value = value; } }"),
            ("LuminPack025", "[LuminPackable] public partial class Model { public int Value; [LuminPackConstructor] public Model() { } [LuminPackConstructor] public Model(int value) { Value = value; } }"),
            ("LuminPack027", "[LuminPackable] public partial class Model { private Model() { } }"),
            ("LuminPack028", "public partial class Container { [LuminPackable] private partial class Model { public int Value; } }"),
            ("LuminPack029", "public partial class Container { [LuminPackable] private protected partial class Model { public int Value; } }"),
            ("LuminPack033", "[LuminPackable] public partial class Model { [LuminPackPoolRent] public Model Rent() => new Model(); }"),
            ("LuminPack034", "[LuminPackable] public partial class Model { [LuminPackCompress] public string[] Values = null!; }"),
            ("LuminPack034", "[LuminPackable] public partial class Model { [LuminPackCompress] public string[] Values { get; set; } = null!; }"),
            ("LuminPack035", "[LuminPackable] public abstract partial class Model { } [LuminPackable] public class Case : Model { }"),
            ("LuminPack101", "[LuminPackable(GeneratorType.Custom)] public class Model { public void Serialize(ref LuminPackWriter writer, in Model value) { } public static void Deserialize(ref LuminPackReader reader, ref Model value) { } }"),
            ("LuminPack103", "[LuminPackable(GeneratorType.Custom)] public class Model { private static void Serialize(ref LuminPackWriter writer, in Model value) { } public static void Deserialize(ref LuminPackReader reader, ref Model value) { } }")
        };
        foreach (var test in cases)
        {
            using var workspace = CreateWorkspace(test.Source);
            await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), test.Id);
        }
        await VerifyCrossDocumentAsync();
        await VerifyTriviaAndAliasesAsync();
        await VerifyFixAllAsync();
        await VerifyUnavailableFixesAsync();
        VerifyExportsAndDependencies();
        Console.WriteLine($"Passed {cases.Length} generator diagnostic cases, cross-document edits, aliases, trivia, Fix All, cancellation and exports.");
    }

    /// <summary>Creates a project with separate source files and real runtime metadata.</summary>
    /// <param name="sources">The contents of each source document.</param>
    /// <returns>The initialized workspace.</returns>
    private static AdhocWorkspace CreateWorkspace(params string[] sources)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixFixture", LanguageNames.CSharp)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: new[] { "NET8_0_OR_GREATER" }))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true))
            .AddMetadataReferences(References);
        for (int index = 0; index < sources.Length; index++)
            project = project.AddDocument($"Model{index}.cs", SourceText.From(Imports + sources[index]), filePath: $"Model{index}.cs").Project;
        Require(workspace.TryApplyChanges(project.Solution), "Could not initialize the workspace.");
        return workspace;
    }

    /// <summary>Runs both generators to obtain their real diagnostic locations and metadata.</summary>
    /// <param name="project">The input project.</param>
    /// <returns>The generator diagnostics.</returns>
    private static async Task<ImmutableArray<Diagnostic>> DiagnosticsAsync(Project project)
    {
        var compilation = await project.GetCompilationAsync() ?? throw new InvalidOperationException("Missing compilation.");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ISourceGenerator[]
        {
            new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()
        }, parseOptions: (CSharpParseOptions)project.ParseOptions!);
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics;
    }

    /// <summary>Selects and applies an offered code action, then rechecks compilation and diagnostics.</summary>
    /// <param name="project">The project to fix.</param>
    /// <param name="id">The diagnostic to repair.</param>
    /// <param name="actionText">An optional title fragment identifying the chosen alternative.</param>
    /// <returns>The project after applying the fix.</returns>
    private static async Task<Project> VerifyFixAsync(Project project, string id, string? actionText = null)
    {
        var diagnostics = await DiagnosticsAsync(project);
        var diagnostic = diagnostics.FirstOrDefault(value => value.Id == id)
            ?? throw new InvalidOperationException($"Expected {id}, got: {string.Join("; ", diagnostics)}");
        var document = project.GetDocument(diagnostic.Location.SourceTree!)!;
        CodeFixProvider provider = id is "LuminPack025" or "LuminPack027"
            ? new LuminPackConstructorCodeFixProvider() : new LuminPackCodeFixProvider();
        var actions = await ActionsAsync(provider, document, diagnostic);
        var action = actions.FirstOrDefault(value => actionText == null || value.Title.Contains(actionText, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No fix offered for {diagnostic}.");
        Require(action.EquivalenceKey != null, "Every fix must have a stable equivalence key.");
        var changed = (await ApplyAsync(action)).GetProject(project.Id)!;
        var remaining = await DiagnosticsAsync(changed);
        Require(!remaining.Any(value => value.Id == id), $"{id} remains after fixing: {string.Join("; ", remaining)}");
        Require(!remaining.Any(value => value.Id is "LuminPack099" or "CS8785"), $"The fix caused a generator failure: {string.Join("; ", remaining)}");
        var compilation = (await changed.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(value => value.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, $"The edited source does not compile: {string.Join("; ", errors.Select(value => value.ToString()))}");
        Console.WriteLine($"Passed {id}: {action.Title}");
        return changed;
    }

    /// <summary>Captures the actions offered for a diagnostic.</summary>
    /// <param name="provider">The provider being tested.</param>
    /// <param name="document">The document containing the diagnostic.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <returns>The registered actions.</returns>
    private static async Task<List<CodeAction>> ActionsAsync(CodeFixProvider provider, Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    /// <summary>Executes the same change operation used by an IDE.</summary>
    /// <param name="action">The selected code action.</param>
    /// <returns>The changed solution.</returns>
    private static async Task<Solution> ApplyAsync(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
    }

    /// <summary>Checks symbol-directed edits across files and all containing partial declarations.</summary>
    /// <returns>The asynchronous test operation.</returns>
    private static async Task VerifyCrossDocumentAsync()
    {
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public partial class Holder { public Nested Value; }",
            "public record Nested { public int Value { get; init; } }"))
        {
            var fixedProject = await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack002");
            string holder = (await fixedProject.Documents.First().GetTextAsync()).ToString();
            Require(!holder.Contains("global::LuminPack.Attribute.LuminPackableAttribute", StringComparison.Ordinal), "Annotated the holder instead of the nested element.");
        }
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public abstract partial class Root { } public class Outer { [LuminPackable] public class Case : Root { } }",
            "public partial class Outer { public partial class Case { public int Value; } }"))
        {
            await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack035");
        }
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public partial class Model { [LuminPackConstructor] public Model() { } public int Value; }",
            "public partial class Model { [LuminPackConstructor] public Model(int value) { Value = value; } }"))
        {
            var fixedProject = await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack025", "(int value)");
            int selected = 0;
            foreach (var document in fixedProject.Documents)
                selected += (await document.GetSyntaxRootAsync())!.DescendantNodes().OfType<AttributeSyntax>()
                    .Count(attribute => attribute.Name.ToString().Contains("LuminPackConstructor", StringComparison.Ordinal));
            Require(selected == 1, "Constructor selection must leave exactly one annotation.");
        }
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public partial class Holder { public Nested Value = null!; }",
            "public class Nested { private Nested() { } }"))
        {
            var fixedProject = await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack027");
            Require((await fixedProject.Documents.Last().GetTextAsync()).ToString().Contains("public Nested()", StringComparison.Ordinal), "Edited the holder instead of its nested member type.");
        }
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public partial class Model { [LuminPackOnSerializing] private partial void Before(); }",
            "public partial class Model { private partial void Before() { } }"))
        {
            await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack005");
        }
        using (var workspace = CreateWorkspace(
            "[LuminPackable] public partial class Model { [LuminPackPoolRent] public partial Model Rent(); }",
            "public partial class Model { public partial Model Rent() => new Model(); }"))
        {
            await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack033");
        }
    }

    /// <summary>Checks semantic attribute matching and preservation of documentation and neighboring attributes.</summary>
    /// <returns>The asynchronous test operation.</returns>
    private static async Task VerifyTriviaAndAliasesAsync()
    {
        using (var workspace = CreateWorkspace("""
            using Pack = LuminPack.Attribute.LuminPackableAttribute;
            /// <summary>Keep this documentation.</summary>
            [System.Obsolete, Pack]
            public static class Model { }
            """))
        {
            var fixedProject = await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack001");
            string text = (await fixedProject.Documents.Single().GetTextAsync()).ToString();
            Require(text.Contains("Keep this documentation.", StringComparison.Ordinal) && text.Contains("System.Obsolete", StringComparison.Ordinal), "Lost unrelated attributes or documentation.");
        }
        using (var workspace = CreateWorkspace("""
            [LuminPackable] public partial class Model
            {
                /// <summary>Keep this callback documentation.</summary>
                [LuminPackOnSerializing]
                private /* Keep access comment. */ protected void Before() { }
            }
            """))
        {
            var fixedProject = await VerifyFixAsync(workspace.CurrentSolution.Projects.Single(), "LuminPack005");
            string text = (await fixedProject.Documents.Single().GetTextAsync()).ToString();
            Require(text.Contains("Keep this callback documentation.", StringComparison.Ordinal) && text.Contains("Keep access comment.", StringComparison.Ordinal), "Lost modifier trivia.");
        }
        // LuminPack021 is retained for older target contracts but is not currently emitted.
        using (var workspace = CreateWorkspace("[LuminPackable] public partial class Model { [LuminPackInclude] public int Value; }"))
        {
            var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
            var field = (await document.GetSyntaxRootAsync())!.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
            var diagnostic = Diagnostic.Create(new DiagnosticDescriptor("LuminPack021", "Target accessor", "Target accessor", "Tests", DiagnosticSeverity.Warning, true), field.Identifier.GetLocation());
            var actions = await ActionsAsync(new LuminPackCodeFixProvider(), document, diagnostic);
            var changed = await ApplyAsync(actions.Single());
            Require(!(await changed.GetDocument(document.Id)!.GetTextAsync()).ToString().Contains("[LuminPackInclude]", StringComparison.Ordinal), "Legacy include annotation remains.");
        }
    }

    /// <summary>Checks the actual batch provider at document, project and solution scope.</summary>
    /// <returns>The asynchronous test operation.</returns>
    private static async Task VerifyFixAllAsync()
    {
        foreach (var scope in new[] { FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution })
        {
            using var workspace = CreateWorkspace(
                "[LuminPackable] public partial class First { [LuminPackOnSerializing] private void A() { } [LuminPackOnSerialized] private void B() { } }",
                "[LuminPackable] public partial class Second { [LuminPackOnSerializing] private void A() { } }");
            var project = workspace.CurrentSolution.Projects.Single();
            var provider = new LuminPackCodeFixProvider();
            var document = project.Documents.First();
            var context = new FixAllContext(document, provider, scope, "LuminPack005", new[] { "LuminPack005" }, new GeneratorDiagnostics(), CancellationToken.None);
            var action = await provider.GetFixAllProvider().GetFixAsync(context);
            Require(action != null, "Fix All did not produce an action.");
            var changed = (await ApplyAsync(action!)).GetProject(project.Id)!;
            var remaining = (await DiagnosticsAsync(changed)).Where(diagnostic => diagnostic.Id == "LuminPack005").ToArray();
            Require(remaining.Length == (scope == FixAllScope.Document ? 1 : 0), $"Fix All failed at {scope} scope.");
        }
        Require(new LuminPackConstructorCodeFixProvider().GetFixAllProvider() == null, "Constructor choices must not participate in Fix All.");
    }

    /// <summary>Checks unsupported, stale and metadata-only diagnostics and cancellation.</summary>
    /// <returns>The asynchronous test operation.</returns>
    private static async Task VerifyUnavailableFixesAsync()
    {
        using var workspace = CreateWorkspace("public class Model { public int Value; }");
        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var field = (await document.GetSyntaxRootAsync())!.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
        foreach (var id in new[] { "LuminPack002", "LuminPack034", "LuminPack099", "LuminPack022" })
        {
            var descriptor = new DiagnosticDescriptor(id, "Unavailable fix", "Unavailable fix", "Tests", DiagnosticSeverity.Error, true);
            var diagnostic = Diagnostic.Create(descriptor, field.Identifier.GetLocation());
            Require((await ActionsAsync(new LuminPackCodeFixProvider(), document, diagnostic)).Count == 0, $"Unexpected edit for {id}.");
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            var descriptor = new DiagnosticDescriptor("LuminPack034", "Cancelled fix", "Cancelled fix", "Tests", DiagnosticSeverity.Error, true);
            var diagnostic = Diagnostic.Create(descriptor, field.Identifier.GetLocation());
            await new LuminPackCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic, (_, _) => { }, cancellation.Token));
            throw new InvalidOperationException("Cancellation was ignored.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Checks IDE discovery and keeps Workspaces out of the generator dependency graph.</summary>
    private static void VerifyExportsAndDependencies()
    {
        var assembly = typeof(LuminPackCodeFixProvider).Assembly;
        foreach (var provider in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(CodeFixProvider).IsAssignableFrom(type)))
        {
            Require(provider.GetCustomAttribute<ExportCodeFixProviderAttribute>()?.Languages.Contains(LanguageNames.CSharp) == true, "Missing C# code-fix export.");
            Require(provider.GetCustomAttribute<System.Composition.SharedAttribute>() != null, "Missing shared export lifetime.");
        }
        Require(!typeof(LuminPackSourceGenerator).Assembly.GetReferencedAssemblies().Any(reference => reference.Name!.Contains("Workspaces", StringComparison.Ordinal)), "Generator gained a Workspaces dependency.");
        Require(!assembly.GetReferencedAssemblies().Any(reference => reference.Name is "LuminPack.Core" or "LuminPack.Generator"), "Code fixes must load without product assemblies.");
    }

    /// <summary>Checks the actual distributable rather than project output directories.</summary>
    /// <param name="path">The packed generator NuGet file.</param>
    private static void VerifyPackage(string path)
    {
        using var package = ZipFile.OpenRead(path);
        string[] assemblies = package.Entries.Where(entry => entry.FullName.EndsWith(".dll", StringComparison.Ordinal))
            .Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Require(assemblies.SequenceEqual(new[] { "analyzers/dotnet/cs/LuminPack.CodeFixes.dll", "analyzers/dotnet/cs/LuminPack.Generator.dll" }),
            "The generator package must contain exactly the generator and code-fix assemblies under analyzers/dotnet/cs.");
        foreach (string name in new[] { "LuminPack.Generator", "LuminPack.CodeFixes" })
        {
            Require(package.GetEntry($"analyzers/dotnet/cs/{name}.xml") != null, "Missing analyzer XML documentation.");
            using var stream = package.GetEntry($"analyzers/dotnet/cs/{name}.dll")!.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var assembly = Assembly.Load(buffer.ToArray());
            Require(assembly.GetName().Name == name, "The packed assembly identity is incorrect.");
        }
        using var manifest = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        Require(!XDocument.Load(manifest).Descendants().Any(element => element.Name.LocalName == "dependency"),
            "IDE service dependencies must not flow to consuming projects.");
        Require(!package.Entries.Any(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)), "Analyzer assemblies must not become runtime references.");
    }

    /// <summary>Fails a regression with a useful description.</summary>
    /// <param name="condition">The required condition.</param>
    /// <param name="message">The failure description.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Supplies generator diagnostics to Roslyn's batch fix implementation.</summary>
    private sealed class GeneratorDiagnostics : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc />
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            return (await DiagnosticsAsync(document.Project)).Where(diagnostic => diagnostic.Location.SourceTree == tree);
        }

        /// <inheritdoc />
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<Diagnostic>());

        /// <inheritdoc />
        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            => await DiagnosticsAsync(project);
    }
}
