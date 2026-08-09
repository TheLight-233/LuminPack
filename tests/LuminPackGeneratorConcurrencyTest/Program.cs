using System.Collections.Concurrent;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const int CompilationCount = 32;
const int ValidTypesPerCompilation = 16;

var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Select(static path => MetadataReference.CreateFromFile(path))
    .ToArray();

if (args.Contains("--verify-scoped-emission", StringComparer.Ordinal))
{
    VerifyScopedEmission(references);
    Console.WriteLine("Passed scoped emission verification for C# 10/non-NET8 and Preview/NET8.");
    return;
}

if (args.Contains("--verify-formatter-scope", StringComparer.Ordinal))
{
    VerifyFormatterScope(references);
    Console.WriteLine("Passed formatter scope verification for Unity-style references and known closed types.");
    return;
}

if (args.Contains("--verify-generic-union-dispatch", StringComparer.Ordinal))
{
    VerifyGenericUnionDispatch(references);
    Console.WriteLine("Passed constant-time closed-generic union dispatch verification.");
    return;
}

if (args.Contains("--verify-union-auto-discovery", StringComparer.Ordinal))
{
    VerifyUnionAutoDiscovery(references);
    Console.WriteLine("Passed union auto-discovery verification for explicit tags and used closed generic members.");
    return;
}

if (args.Contains("--verify-direct-static-dispatch", StringComparer.Ordinal))
{
    VerifyDirectStaticDispatch(references);
    Console.WriteLine("Passed Roslyn concrete-overload binding and generic fallback verification.");
    return;
}

var failures = new ConcurrentQueue<string>();
Parallel.For(0, CompilationCount, compilationIndex =>
{
    var source = BuildSource(compilationIndex);
    var parseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp10)
        .WithPreprocessorSymbols(compilationIndex % 2 == 0
            ? new[] { "NET8_0_OR_GREATER" }
            : Array.Empty<string>());
    var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
    var compilation = CSharpCompilation.Create(
        $"GeneratorConcurrency_{compilationIndex}",
        new[] { tree },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    driver = driver.RunGenerators(compilation);
    var runResult = driver.GetRunResult();
    var result = runResult.Results.Single();

    if (result.Exception is not null)
    {
        failures.Enqueue($"Compilation {compilationIndex} generator exception: {result.Exception}");
        return;
    }

    var ownInvalidName = $"Invalid_{compilationIndex}";
    var errorDiagnostics = result.Diagnostics
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToArray();
    if (errorDiagnostics.Length != 1 ||
        !errorDiagnostics[0].GetMessage().Contains(ownInvalidName, StringComparison.Ordinal))
    {
        failures.Enqueue(
            $"Compilation {compilationIndex} diagnostics leaked or were lost: " +
            string.Join(" | ", errorDiagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    var generatedHints = result.GeneratedSources
        .Select(static generated => generated.HintName)
        .ToHashSet(StringComparer.Ordinal);
    for (var typeIndex = 0; typeIndex < ValidTypesPerCompilation; typeIndex++)
    {
        var expectedHint = $"Case_{compilationIndex}.Model_{typeIndex}.Extension.g.cs";
        if (!generatedHints.Contains(expectedHint))
        {
            failures.Enqueue($"Compilation {compilationIndex} is missing {expectedHint}.");
        }
    }

    if (generatedHints.Any(hint =>
            hint.Contains("Case_", StringComparison.Ordinal) &&
            !hint.Contains($"Case_{compilationIndex}.", StringComparison.Ordinal)))
    {
        failures.Enqueue($"Compilation {compilationIndex} received generated source from another compilation.");
    }
});

if (!failures.IsEmpty)
{
    throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
}

VerifyUnionPartialDiagnostic(references);
VerifyScopedEmission(references);

Console.WriteLine(
    $"Passed {CompilationCount} parallel compilations and " +
    $"{CompilationCount * ValidTypesPerCompilation} valid generated types.");

static void VerifyUnionPartialDiagnostic(MetadataReference[] references)
{
    const string source = """
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }

        [LuminPack.Attribute.LuminPackable]
        public interface INonPartialUnion
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed class NonPartialUnionMember : INonPartialUnion
        {
        }
        """;

    var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
    var compilation = CSharpCompilation.Create(
        "UnionPartialDiagnostic",
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    var diagnostics = driver.RunGenerators(compilation)
        .GetRunResult()
        .Results.Single()
        .Diagnostics
        .Where(static diagnostic => diagnostic.Id == "LuminPack035")
        .ToArray();

    if (diagnostics.Length != 2 ||
        !diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("INonPartialUnion", StringComparison.Ordinal)) ||
        !diagnostics.Any(diagnostic => diagnostic.GetMessage().Contains("NonPartialUnionMember", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "Expected LuminPack035 for both a non-partial Union root and its non-partial local member.");
    }
}

static void VerifyScopedEmission(MetadataReference[] references)
{
    const string source = """
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }

        [LuminPack.Attribute.LuminPackable]
        public partial struct ScopedEmissionModel
        {
            public int Value;
        }
        """;

    AssertScopedEmission(
        source,
        references,
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10),
        shouldEmitScoped: false,
        "C# 10 / non-NET8");

    AssertScopedEmission(
        source,
        references,
        CSharpParseOptions.Default
            // Microsoft.CodeAnalysis.CSharp 4.3.0 exposes the C# 11 syntax
            // level through Preview rather than a CSharp11 enum value.
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("NET8_0_OR_GREATER"),
        shouldEmitScoped: true,
        "C# 11 / NET8");
}

static void VerifyUnionAutoDiscovery(MetadataReference[] references)
{
    const string source = """
        using System;

        namespace LuminPack.Attribute
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : System.Attribute
            {
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, AllowMultiple = true)]
            public sealed class LuminPackUnionAttribute : System.Attribute
            {
                public LuminPackUnionAttribute(ushort tag, Type type)
                {
                }
            }
        }

        [LuminPack.Attribute.LuminPackable]
        [LuminPack.Attribute.LuminPackUnion(42, typeof(ExplicitMessage))]
        public partial interface IMessage
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed partial class ExplicitMessage : IMessage
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed partial class AutoMessage : IMessage
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public partial interface IGenericMessage<T>
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed partial class GenericMessage<T> : IGenericMessage<T>
        {
            public T Value = default!;
        }

        public static class ProjectUsage
        {
            public static readonly Type ClosedMember = typeof(GenericMessage<int>);
        }
        """;

    var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
    var compilation = CSharpCompilation.Create(
        "UnionAutoDiscovery",
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    var compilationErrors = compilation.GetDiagnostics()
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToArray();
    if (compilationErrors.Length != 0)
    {
        throw new InvalidOperationException(
            "Union auto-discovery input did not compile: " +
            string.Join(" | ", compilationErrors.Select(static diagnostic => diagnostic.ToString())));
    }
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
    var errors = result.Diagnostics
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .ToArray();
    if (errors.Length != 0)
    {
        throw new InvalidOperationException(
            "Union auto-discovery produced diagnostics: " +
            string.Join(" | ", errors.Select(static diagnostic => diagnostic.ToString())));
    }

    string messageSource = result.GeneratedSources
        .Where(static generated => generated.HintName.Contains("IMessage.Extension", StringComparison.Ordinal))
        .Select(static generated => generated.SourceText.ToString())
        .Single();
    if (!messageSource.Contains("case 42:", StringComparison.Ordinal) ||
        !messageSource.Contains("global::ExplicitMessage member", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The explicit [LuminPackUnion] tag was not preserved.");
    }
    if (!messageSource.Contains("case 0:", StringComparison.Ordinal) ||
        !messageSource.Contains("global::AutoMessage member", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("A local [LuminPackable] derived type was not auto-registered.");
    }

    string genericRootSource = result.GeneratedSources
        .Where(static generated => generated.HintName.Contains("IGenericMessage", StringComparison.Ordinal))
        .Select(static generated => generated.SourceText.ToString())
        .First(static generated => generated.Contains("ReadPolymorphismValue", StringComparison.Ordinal));
    if (!genericRootSource.Contains("global::GenericMessage<int> member", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The closed GenericMessage<int> observed by the semantic syntax walk was not auto-registered.");
    }
}

static void VerifyGenericUnionDispatch(MetadataReference[] references)
{
    const string source = """
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
            public sealed class LuminPackUnionAttribute : Attribute
            {
                public LuminPackUnionAttribute(ushort tag, Type type)
                {
                }
            }
        }

        [LuminPack.Attribute.LuminPackable]
        [LuminPack.Attribute.LuminPackUnion(3, typeof(ClosedGenericMember<double>))]
        [LuminPack.Attribute.LuminPackUnion(4, typeof(ClosedGenericMember<int>))]
        public abstract partial class GenericUnionRoot<T>
        {
        }

        [LuminPack.Attribute.LuminPackable]
        public sealed partial class ClosedGenericMember<TMember> : GenericUnionRoot<TMember>
        {
            public TMember Value = default!;
        }
        """;

    var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
    var compilation = CSharpCompilation.Create(
        "GenericUnionDispatch",
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
    if (result.Exception is not null)
    {
        throw new InvalidOperationException("Generic union generator failed.", result.Exception);
    }

    var generated = result.GeneratedSources
        .FirstOrDefault(static item => item.HintName.EndsWith(".UnionDispatch.g.cs", StringComparison.Ordinal))
        .SourceText?
        .ToString();
    if (string.IsNullOrEmpty(generated))
    {
        throw new InvalidOperationException("The generic union dispatch source was not generated.");
    }

    if (generated.Contains("if (typeof(T)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The generic union hot path still contains a linear typeof(T) chain.");
    }
    if (!generated.Contains("LuminUnionMap<int>", StringComparison.Ordinal) ||
        !generated.Contains("switch (__LuminPackUnionDispatchSlot_", StringComparison.Ordinal) ||
        !generated.Contains("LuminPack.Code.LuminPackMarshal.GetMethodTable", StringComparison.Ordinal) ||
        !generated.Contains("LuminPack.Code.LuminPackMarshal.As<", StringComparison.Ordinal) ||
        !generated.Contains("typeof(global::ClosedGenericMember<TMember>)", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The generic union dispatch did not emit its MethodTable cache and exact closed-type static call path.");
    }
}

static void VerifyDirectStaticDispatch(MetadataReference[] platformReferences)
{
    var parseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.Preview)
        .WithPreprocessorSymbols("NET8_0_OR_GREATER");
    var runtimeReference = MetadataReference.CreateFromFile(
        typeof(global::LuminPack.LuminPackSerializer).Assembly.Location);
    var references = platformReferences.Append(runtimeReference).ToArray();

    const string directSource = """
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using LuminPack.Attribute;

        [LuminPackable]
        public partial class DirectStaticModel
        {
            public string[] Names = default!;
            public List<string> Labels = default!;
            public Dictionary<string, List<int>> Index = default!;
            public IReadOnlyCollection<string> ReadOnlyLabels { get; set; } = default!;
            public ImmutableArray<string> ImmutableLabels;
            public KeyValuePair<int, string> Pair;
        }
        """;

    var directInput = CSharpCompilation.Create(
        "DirectStaticDispatch",
        new[] { CSharpSyntaxTree.ParseText(directSource, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    var (directOutput, _) = RunFormatterGenerators(directInput, parseOptions);
    AssertNoCompilationErrors(directOutput, "direct-static closed formatter graph", ignoreRoslyn43RefReadonlyMismatch: true);

    const string generatedContainer = "LuminPack.Generated.LuminPackExtensions_DirectStaticDispatch";
    int directWrites = 0;
    int directReads = 0;
    int directCalculates = 0;
    int directObjectFieldCalculates = 0;
    var runtimeFallbacks = new List<string>();
    foreach (SyntaxTree tree in directOutput.SyntaxTrees.Skip(directInput.SyntaxTrees.Count()))
    {
        SemanticModel model = directOutput.GetSemanticModel(tree);
        foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                continue;

            string containingType = method.ContainingType?.ToDisplayString() ?? string.Empty;
            if (containingType == "LuminPack.Core.LuminPackLocalExtension" &&
                method.Name is "WriteValue" or "ReadValue")
            {
                runtimeFallbacks.Add(invocation.ToString());
            }

            if (containingType != generatedContainer || method.IsGenericMethod)
                continue;

            if (method.Name == "WriteValue") directWrites++;
            else if (method.Name == "ReadValue") directReads++;
            else if (method.Name == "CalculateOffset")
            {
                directCalculates++;
                if (invocation.ArgumentList.Arguments.Any(static argument =>
                        argument.Expression.ToString().Contains("__luminPackOffsetValue", StringComparison.Ordinal)))
                {
                    directObjectFieldCalculates++;
                }
            }
        }
    }

    if (runtimeFallbacks.Count != 0)
    {
        throw new InvalidOperationException(
            "Closed local formatter calls bound to LuminPackLocalExtension<T>: " +
            string.Join(" | ", runtimeFallbacks.Take(8)));
    }
    if (directWrites < 12 || directReads < 12 || directCalculates < 8 || directObjectFieldCalculates < 4)
    {
        throw new InvalidOperationException(
            $"Insufficient concrete generated bindings: Write={directWrites}, Read={directReads}, " +
            $"Calculate={directCalculates}, ObjectFieldCalculate={directObjectFieldCalculates}.");
    }

    const string externalSource = """
        using LuminPack.Attribute;

        namespace ExternalContracts
        {
            [LuminPackable]
            public partial class ExternalContract
            {
                public int Value;
            }
        }
        """;
    var externalCompilation = CSharpCompilation.Create(
        "ExternalContracts",
        new[] { CSharpSyntaxTree.ParseText(externalSource, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    using var externalImage = new MemoryStream();
    var externalEmit = externalCompilation.Emit(externalImage);
    if (!externalEmit.Success)
    {
        throw new InvalidOperationException(
            "External contract metadata compilation failed: " +
            string.Join(" | ", externalEmit.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
    }

    var externalReference = MetadataReference.CreateFromImage(externalImage.ToArray());
    const string consumerSource = """
        using System.Collections.Generic;
        using ExternalContracts;
        using LuminPack.Attribute;

        [LuminPackable]
        public partial class ExternalConsumer
        {
            public List<ExternalContract> Items = default!;
        }
        """;
    var consumerInput = CSharpCompilation.Create(
        "ExternalConsumer",
        new[] { CSharpSyntaxTree.ParseText(consumerSource, parseOptions) },
        references.Append(externalReference),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    var (consumerOutput, _) = RunFormatterGenerators(consumerInput, parseOptions);
    AssertNoCompilationErrors(consumerOutput, "external packable collection fallback", ignoreRoslyn43RefReadonlyMismatch: true);

    int fallbackWrites = 0;
    int fallbackReads = 0;
    int fallbackCalculates = 0;
    foreach (SyntaxTree tree in consumerOutput.SyntaxTrees.Skip(consumerInput.SyntaxTrees.Count()))
    {
        SemanticModel model = consumerOutput.GetSemanticModel(tree);
        foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                method.ContainingType?.ToDisplayString() != "LuminPack.Core.LuminPackLocalExtension" ||
                !method.IsGenericMethod ||
                method.TypeArguments.Length != 1 ||
                method.TypeArguments[0].ToDisplayString().TrimEnd('?') != "ExternalContracts.ExternalContract")
            {
                continue;
            }

            if (method.Name == "WriteValue") fallbackWrites++;
            else if (method.Name == "ReadValue") fallbackReads++;
            else if (method.Name == "CalculateOffset") fallbackCalculates++;
        }
    }

    if (fallbackWrites == 0 || fallbackReads == 0 || fallbackCalculates == 0)
    {
        throw new InvalidOperationException(
            $"Referenced [LuminPackable] fallback bindings missing: Write={fallbackWrites}, " +
            $"Read={fallbackReads}, Calculate={fallbackCalculates}.");
    }
}

static (Compilation Output, string Generated) RunFormatterGenerators(
    CSharpCompilation input,
    CSharpParseOptions parseOptions)
{
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    driver = driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out _);
    var runResult = driver.GetRunResult();
    foreach (GeneratorRunResult result in runResult.Results)
    {
        if (result.Exception is not null)
            throw new InvalidOperationException("Formatter generator failed.", result.Exception);
    }

    string generated = string.Join(
        Environment.NewLine,
        runResult.Results.SelectMany(static result => result.GeneratedSources)
            .Select(static source => source.SourceText.ToString()));
    return (output, generated);
}

static void AssertNoCompilationErrors(
    Compilation compilation,
    string scenario,
    bool ignoreRoslyn43RefReadonlyMismatch = false)
{
    Diagnostic[] errors = compilation.GetDiagnostics()
        .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        .Where(diagnostic => !ignoreRoslyn43RefReadonlyMismatch || !IsRoslyn43RefReadonlyMismatch(diagnostic))
        .ToArray();
    if (errors.Length != 0)
    {
        var rendered = errors.Take(16).Select(static error =>
        {
            var span = error.Location.GetLineSpan();
            string sourceLine = error.Location.SourceTree is { } tree
                ? tree.GetText().Lines[span.StartLinePosition.Line].ToString().Trim()
                : string.Empty;
            return error + (sourceLine.Length == 0 ? string.Empty : " [source: " + sourceLine + "]");
        });
        throw new InvalidOperationException(
            scenario + " produced compilation errors: " + string.Join(" | ", rendered));
    }
}

static bool IsRoslyn43RefReadonlyMismatch(Diagnostic diagnostic)
{
    if (diagnostic.Id != "CS1620" || diagnostic.Location.SourceTree is not { } tree)
        return false;

    var span = diagnostic.Location.GetLineSpan();
    return tree.GetText().Lines[span.StartLinePosition.Line].ToString()
        .Contains("Unsafe.AsRef(in value)", StringComparison.Ordinal);
}

static void VerifyFormatterScope(MetadataReference[] references)
{
    const string runtimeSource = """
        using System;

        namespace LuminPack
        {
            public static class LuminPackSerializer
            {
                public static void Touch<T>() { }
            }
        }

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }
        """;
    var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
    var runtime = CSharpCompilation.Create(
        "LuminPack",
        new[] { CSharpSyntaxTree.ParseText(runtimeSource, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    var targetReferences = references.Append(runtime.ToMetadataReference()).ToArray();

    const string unrelatedUnityAssembly = """
        using System.Collections.Generic;

        public sealed class InputControl { }
        public sealed class InputSystemState
        {
            public List<int> KnownClosedType = new();
            public List<InputControl> UnknownClosedType = new();
        }
        """;
    var unrelatedSources = RunMapGenerator(unrelatedUnityAssembly, "Unity.InputSystem", targetReferences, parseOptions);
    if (unrelatedSources.Length != 0)
    {
        throw new InvalidOperationException(
            "An assembly that only receives Unity's automatic LuminPack reference must not emit support code.");
    }

    const string luminPackConsumer = """
        using System;
        using System.Collections.Generic;
        using LuminPack;

        public sealed class InputControl { }
        public static class Consumer
        {
            public static void Touch()
            {
                LuminPackSerializer.Touch<int>();
                _ = typeof(Dictionary<string, List<int>>);
                _ = typeof(List<InputControl>);
                _ = typeof(nuint);
                _ = typeof(UIntPtr);
            }
        }
        """;
    var consumerSources = RunMapGenerator(luminPackConsumer, "Assembly-CSharp", targetReferences, parseOptions);
    var generated = string.Join(Environment.NewLine, consumerSources);
    if (!generated.Contains("Dictionary<", StringComparison.Ordinal) ||
        !generated.Contains("List<int>", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Registered closed generic formatter support was not emitted.");
    }
    if (!generated.Split('\n').Any(static line =>
            line.Contains("Cache<", StringComparison.Ordinal) &&
            line.Contains("Dictionary<", StringComparison.Ordinal) &&
            line.Contains(">.CalculateOffset =", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("Closed dictionary formatter did not register its size evaluator.");
    }
    if (generated.Contains("InputControl", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("An unknown closed generic argument leaked into formatter support.");
    }
    if (generated.Contains(" in nuint value", StringComparison.Ordinal) ||
        generated.Contains(" ref nuint value", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("nuint and UIntPtr were not canonicalized to one generated signature.");
    }
}

static string[] RunMapGenerator(
    string source,
    string assemblyName,
    MetadataReference[] references,
    CSharpParseOptions parseOptions)
{
    var compilation = CSharpCompilation.Create(
        assemblyName,
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminMapSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    return driver.RunGenerators(compilation)
        .GetRunResult()
        .Results.Single()
        .GeneratedSources
        .Select(static generated => generated.SourceText.ToString())
        .ToArray();
}

static void AssertScopedEmission(
    string source,
    MetadataReference[] references,
    CSharpParseOptions parseOptions,
    bool shouldEmitScoped,
    string scenario)
{
    var compilation = CSharpCompilation.Create(
        "ScopedEmission_" + scenario,
        new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    GeneratorDriver driver = CSharpGeneratorDriver.Create(
        new[] { new LuminPackSourceGenerator().AsSourceGenerator() },
        parseOptions: parseOptions);
    var generatedSource = string.Join(
        Environment.NewLine,
        driver.RunGenerators(compilation)
            .GetRunResult()
            .Results.Single()
            .GeneratedSources
            .Select(static generated => generated.SourceText.ToString()));
    var emitsScoped = generatedSource.Contains("scoped ", StringComparison.Ordinal);

    if (emitsScoped != shouldEmitScoped)
    {
        throw new InvalidOperationException(
            $"{scenario} scoped emission mismatch. Expected {shouldEmitScoped}, got {emitsScoped}.");
    }
}

static string BuildSource(int compilationIndex)
{
    var types = string.Join(
        Environment.NewLine,
        Enumerable.Range(0, ValidTypesPerCompilation).Select(typeIndex => $$"""
            [LuminPack.Attribute.LuminPackable]
            public partial class Model_{{typeIndex}}
            {
                public int Value_{{typeIndex}};
            }
            """));

    return $$"""
        using System;

        namespace LuminPack.Attribute
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
            public sealed class LuminPackableAttribute : Attribute
            {
            }
        }

        namespace Case_{{compilationIndex}}
        {
            {{types}}

            [LuminPack.Attribute.LuminPackable]
            public static partial class Invalid_{{compilationIndex}}
            {
            }
        }
        """;
}
