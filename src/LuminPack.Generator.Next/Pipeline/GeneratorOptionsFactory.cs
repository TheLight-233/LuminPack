using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>Reads the project facts a model and its renderer need from the consumer compilation.</summary>
internal static class GeneratorOptionsFactory
{
    private static readonly ConditionalWeakTable<Compilation, GeneratorOptions> Cache = new();

    /// <summary>Captures the project facts once per compilation.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The immutable project facts.</returns>
    /// <remarks>The per-payload transform calls this for every attributed type. The compilation is a weak key, so the
    /// options are shared by all payloads of one run without retaining a finished compilation.</remarks>
    internal static GeneratorOptions From(Compilation compilation) => Cache.GetValue(compilation, static item => Create(item));

    /// <summary>Reads the registration argument by enum identity across both supported assembly attribute constructors.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The declared registration mode.</returns>
    private static RegisterMode Registration(Compilation compilation)
    {
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
            if (attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute")
                foreach (TypedConstant argument in attribute.ConstructorArguments)
                    if (argument.Type?.ToDisplayString() == "LuminPack.Attribute.LuminPackRegisterMode" &&
                        Convert.ToInt32(argument.Value, System.Globalization.CultureInfo.InvariantCulture) == 1)
                        return RegisterMode.Disabled;
        return RegisterMode.Enabled;
    }

    /// <summary>Reads the cursor field type from the selected Core reference.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The runtime cursor type or null when the runtime is unavailable.</returns>
    private static ITypeSymbol? CursorType(Compilation compilation) =>
        compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter")?.GetMembers("Cursor").OfType<IFieldSymbol>().FirstOrDefault()?.Type;

    /// <summary>Captures target, language, registration and entry-binding facts.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The immutable project facts.</returns>
    private static GeneratorOptions Create(Compilation compilation)
    {
        var csharp = compilation as CSharpCompilation;
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        bool interceptors = false;
        foreach (var tree in compilation.SyntaxTrees)
            if (tree.Options is CSharpParseOptions parse)
            {
                foreach (string symbol in parse.PreprocessorSymbolNames)
                    symbols.Add(symbol);
                if (SerializerCallAnalysis.SupportsCompilerInterception && (int)parse.LanguageVersion >= 1100 &&
                    (HasNamespace(parse, "InterceptorsNamespaces") || HasNamespace(parse, "InterceptorsPreviewNamespaces")))
                    interceptors = true;
            }
        bool net8 = symbols.Contains("NET8_0_OR_GREATER") || symbols.Contains("NET9_0_OR_GREATER") ||
            symbols.Contains("NET10_0_OR_GREATER") || symbols.Contains("NET11_0_OR_GREATER");
        bool unity = symbols.Any(symbol => symbol.StartsWith("UNITY", StringComparison.Ordinal)) ||
            compilation.GetTypeByMetadataName("UnityEngine.Application") != null || compilation.GetTypeByMetadataName("UnityEditor.Editor") != null;
        // Without compiler interception, the installed IL rewriter binds the same static entries after compilation.
        bool ilRewrite = !interceptors && symbols.Contains("LUMINPACK_IL_REWRITE");
        var cursor = CursorType(compilation);
        return new GeneratorOptions(net8, csharp == null ? 0 : (int)csharp.LanguageVersion, csharp?.Options.AllowUnsafe ?? false, unity,
            Registration(compilation), availableAliases: -1, compilation.AssemblyName ?? "Assembly", interceptors || ilRewrite, ilRewrite,
            cursor is IPointerTypeSymbol || cursor == null && !net8, AmbiguousNames(compilation), FormatterInterfaces(compilation),
            compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackKnownFormatters") != null,
            FormatterBuiltinCapabilities.HasQuotedScalarReader(compilation),
            cursor is INamedTypeSymbol { Name: "Span", Arity: 1 },
            generatedJsonPropertyWriter: net8 && !unity && GeneratedJsonProperty.Available(compilation),
            generatedJsonInt32Reader: net8 && !unity && GeneratedJsonInt32Runs.Available(compilation),
            hashSetRefill: FormatterConcurrentTraits.HasHashSetRefill(compilation),
            linkedListAppender: FormatterConcurrentTraits.HasLinkedListAppender(compilation),
            concurrentDictionaryBuilder: FormatterConcurrentTraits.HasConcurrentDictionaryBuilder(compilation),
            queueRefill: FormatterConcurrentTraits.HasQueueRefill(compilation),
            stackRefill: FormatterConcurrentTraits.HasStackRefill(compilation),
            readOnlyCollectionRefill: FormatterConcurrentTraits.HasReadOnlyCollectionRefill(compilation),
            readOnlyDictionaryRefill: FormatterConcurrentTraits.HasReadOnlyDictionaryRefill(compilation),
            concurrentDictionaryRefill: FormatterConcurrentTraits.HasConcurrentDictionaryRefill(compilation));
    }

    /// <summary>Checks that the compiler feature enables the generated interception namespace.</summary>
    /// <param name="parse">The language version and target symbols for parsing.</param>
    /// <param name="feature">The feature supplied to <c>HasNamespace</c>.</param>
    /// <returns>Whether the input has namespace.</returns>
    private static bool HasNamespace(CSharpParseOptions parse, string feature)
    {
        if (!parse.Features.TryGetValue(feature, out string? namespaces))
            return false;
        const string name = "LuminPack";
        int start = 0;
        while (start < namespaces.Length)
        {
            int end = namespaces.IndexOf(';', start);
            if (end < 0)
                end = namespaces.Length;
            int next = end + 1;
            while (start < end && char.IsWhiteSpace(namespaces[start]))
                start++;
            while (end > start && char.IsWhiteSpace(namespaces[end - 1]))
                end--;
            if (end - start == name.Length && string.CompareOrdinal(namespaces, start, name, 0, name.Length) == 0)
                return true;
            start = next;
        }
        return false;
    }

    /// <summary>Reads whether the referenced runtime declares the formatter interfaces that shared generic formatters call.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>2 when the block interfaces of laid-out elements exist too, 1 when only the instance-member interfaces exist,
    /// otherwise 0, which keeps closed owners.</returns>
    private static int FormatterInterfaces(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("LuminPack.Code.ILuminPackWriter`1") is not { } writer)
            return 0;
        foreach (ISymbol member in writer.GetMembers("Write"))
            if (member is IMethodSymbol { IsStatic: false })
                return compilation.GetTypeByMetadataName("LuminPack.Code.ILuminPackBlockWriter`1") is null ? 1 : 2;
        return 0;
    }

    /// <summary>The framework namespaces whose type names generated owners commonly spell.</summary>
    private static readonly string[] FrameworkNamespaces =
    {
        "System", "System.Collections", "System.Collections.Generic", "System.Collections.Concurrent", "System.Collections.Immutable",
        "System.Collections.ObjectModel", "System.Collections.Frozen", "System.Collections.Specialized", "System.Globalization",
        "System.Numerics", "System.Text", "LuminPack",
    };

    /// <summary>Finds the simple type names that the compilation's own declarations share across namespaces or with a common
    /// framework type, so generated names spelled from them would collide without their namespace.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The ambiguous names in ordinal order.</returns>
    private static string[] AmbiguousNames(Compilation compilation)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(compilation.Assembly.GlobalNamespace);
        while (pending.Count != 0)
        {
            INamespaceOrTypeSymbol container = pending.Pop();
            string? path = null;
            foreach (ISymbol member in container.GetMembers())
            {
                if (member is INamespaceSymbol nested)
                {
                    pending.Push(nested);
                    continue;
                }
                if (member is not INamedTypeSymbol type || type.IsImplicitlyDeclared)
                    continue;
                pending.Push(type);
                path ??= container.ToDisplayString();
                if (paths.TryGetValue(type.Name, out string? seen))
                {
                    if (!string.Equals(seen, path, StringComparison.Ordinal))
                        ambiguous.Add(type.Name);
                }
                else
                {
                    paths.Add(type.Name, path);
                    foreach (string framework in FrameworkNamespaces)
                        if (!string.Equals(framework, path, StringComparison.Ordinal) &&
                            compilation.GetTypeByMetadataName(framework + "." + type.MetadataName) != null)
                        {
                            ambiguous.Add(type.Name);
                            break;
                        }
                }
            }
        }
        var names = new string[ambiguous.Count];
        ambiguous.CopyTo(names);
        return names;
    }
}
