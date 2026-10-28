using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Separates no-owner formatter bodies from generic dispatch and static registration.</summary>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="reachability">The formatter roots, or null for Full.</param>
    /// <param name="cache">Generator-instance type and dispatch contribution caches.</param>
    /// <returns>Independently comparable partial extension files.</returns>
    internal static ImmutableArray<FrozenGeneratedSource> GenerateInvocationFiles(Compilation compilation, MetaInfo metadata,
        ReachabilityAnalysis reachability, InvocationSupportCache cache)
    {
        var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
        var ordered = GetOrderedFormatterTypes(compilation, reachability);
        var candidates = ordered.Where(type => !analysis.IsPackable(type));
        var specializations = ordered.Where(type => type is INamedTypeSymbol { IsGenericType: true } named &&
            analysis.IsPackable(named) && SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly));
        var assemblyPackables = ordered.Where(analysis.IsAssemblyPackable);
        var custom = ordered.Where(IsCustomGeneratorType);
        var customTypes = ordered.OfType<INamedTypeSymbol>().Where(IsCustomGeneratorType);
        var customDependencies = GetCustomFormatterDependencies(analysis, compilation, customTypes)
            .Distinct(SymbolEqualityComparer.Default).OfType<ITypeSymbol>().ToArray();
        var types = candidates.Concat(specializations).Concat(assemblyPackables).Concat(custom).Concat(customDependencies)
            .Where(type => !ContainsTypeParameter(type) && IsAotVisible(type) && IsStaticFormatterCandidate(type, analysis))
            .OrderBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal).GroupBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        string extension = GetExtensionClassName(compilation);
        var files = ImmutableArray.CreateBuilder<FrozenGeneratedSource>();
        var lazyTargets = new Dictionary<string, LazyForwardingTargets>(StringComparer.Ordinal);
        var jsonDefinitions = new HashSet<string>(StringComparer.Ordinal);
        var unionDefinitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            string name = FormatterTypeName.Get(type);
            var fragment = cache.Fragments.GetOrBuild(type, compilation, metadata, LuminPackGenerationTier.Full, () =>
            {
                var body = new StringBuilder();
                string source;
                if (IsCustomGeneratorType(type))
                {
                    AppendCustomFormatterExtensions(body, new LuminLocalFieldData { TypeName = name, TypeSymbol = type, Name = "value", IsValue = type.IsValueType }, metadata);
                    source = SerializerCoreProjection.Project(GenerateExtension(body, extension), compilation, metadata, cache.Operations);
                }
                else if (type is INamedTypeSymbol marked && analysis.IsAssemblyPackable(marked))
                    source = SerializerCoreProjection.Project(cache.Operations.Get(type, compilation, metadata).TemplateSource ??
                        GenerateOperationTemplates(type, compilation, metadata), compilation, metadata, cache.Operations, type);
                else if (type is INamedTypeSymbol { IsGenericType: true } named && analysis.IsPackable(named) &&
                    SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly))
                    source = SerializerCoreProjection.Project(cache.Operations.Get(type, compilation, metadata).TemplateSource ??
                        GenerateOperationTemplates(type, compilation, metadata), compilation, metadata, cache.Operations, type);
                else
                {
                    GenerateFormatterExtensions(body, CreateOperationField(type, compilation, metadata),
                        metadata, new HashSet<string>(StringComparer.Ordinal), emitOptionalVariants: true);
                    source = SerializerCoreProjection.Project(GenerateExtension(body, extension), compilation, metadata, cache.Operations);
                }
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFormatterBuilds);
                return new FrozenTypeEmission(name, source, string.Empty,
                    new FrozenLayoutGroup(extension, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null), Array.Empty<Diagnostic>());
            }, serializerFragment: true, outputVariant: "invocation:" + (reachability == null));
            if (!string.IsNullOrEmpty(fragment.Extension))
            {
                files.Add(cache.Files.Wrap("LuminPack.Formatter", name, string.Empty, fragment.Extension, string.Empty));
                lazyTargets[name] = LazyForwardingTargets.Read(fragment.Extension, name);
            }
            if (type is INamedTypeSymbol union && union.IsAbstract && analysis.IsAssemblyPackable(union) &&
                !SymbolEqualityComparer.Default.Equals(union.ContainingAssembly, compilation.Assembly) &&
                unionDefinitions.Add(FormatterTypeName.Get(union.OriginalDefinition)))
            {
                var data = LuminPackSourceGenerator.GetSerializationMetadata(union.OriginalDefinition, compilation, metadata);
                string unionSource = LuminPackUnionDispatchCodeGenerator.Generate(data, compilation, metadata);
                if (!string.IsNullOrEmpty(unionSource))
                    files.Add(cache.Files.Wrap("LuminPack.Union", FormatterTypeName.Get(union.OriginalDefinition),
                        string.Empty, unionSource, string.Empty, () => GeneratedSourceDocumentation.Complete(
                            SerializerCoreProjection.RewriteCalls(unionSource, compilation, metadata), metadata.ParseOptions), metadata.ParseOptions));
            }
            if (type is INamedTypeSymbol external && analysis.IsAssemblyPackable(external) &&
                !SymbolEqualityComparer.Default.Equals(external.ContainingAssembly, compilation.Assembly) &&
                jsonDefinitions.Add(FormatterTypeName.Get(external.OriginalDefinition)))
            {
                string template = cache.Operations.Get(type, compilation, metadata).TemplateSource;
                if (template != null)
                    files.Add(cache.Files.Wrap("LuminPack.JsonSupport", FormatterTypeName.Get(external.OriginalDefinition),
                        string.Empty, template, string.Empty, () => JsonOperationTemplates.Support(template, metadata, compilation), metadata.ParseOptions));
            }
        }

        foreach (var part in DispatchFiles(compilation, metadata, cache)) files.Add(part);
        // The entries' direct root calls are part of the lazy file's identity.
        string lazyKey = string.Join("|", ordered.Select(type => FormatterTypeName.Get(type) +
            (lazyTargets.TryGetValue(FormatterTypeName.Get(type), out var targets) ? "=" + targets.Key : string.Empty)));
        string lazy = cache.Part("LazyFormatters").GetOrBuild(compilation, metadata, lazyKey, ordered,
            () => BuildLazyFormatters(compilation, metadata, reachability, lazyTargets));
        if (lazy.Length != 0) files.Add(new FrozenGeneratedSource("LuminPack.Dispatch.Lazy.g.cs", lazy));
        files.Add(new FrozenGeneratedSource("LuminPack.FormatterBridges.g.cs", SerializerCoreProjection.Bridges(extension, metadata)));
        return files.ToImmutable();
    }

    /// <summary>Collects concrete state formatter types called by source Custom formatter methods.</summary>
    /// <param name="analysis">The compilation-local declaration index.</param>
    /// <param name="compilation">The current compilation supplying semantic argument types.</param>
    /// <param name="customTypes">The Custom formatter types whose callbacks should be scanned.</param>
    /// <returns>Types needed by Writer and Reader operations inside reachable Custom callbacks.</returns>
    internal static IEnumerable<ITypeSymbol> GetCustomFormatterDependencies(CompilationTypeAnalysis analysis, Compilation compilation,
        IEnumerable<INamedTypeSymbol> customTypes)
    {
        var selectedCustomTypes = new HashSet<ITypeSymbol>(customTypes.Select(static type => type.OriginalDefinition), SymbolEqualityComparer.Default);
        foreach (var declared in analysis.DeclaredTypes)
        {
            INamedTypeSymbol type = declared.Symbol;
            if (!IsCustomGeneratorType(type) || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) continue;
            if (!selectedCustomTypes.Contains(type.OriginalDefinition)) continue;
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.Parameters.Length == 0 || method.DeclaringSyntaxReferences.Length == 0) continue;
                string[] supportedCalls = method.Name switch
                {
                    "Serialize" or "SerializeJson" => new[] { "Write", "WriteValue", "WriteValueWithCompress" },
                    "Deserialize" or "DeserializeJson" => new[] { "Read", "ReadValue", "ReadFreshValue" },
                    "CalculateOffset" => new[] { "CalculateOffset" },
                    _ => Array.Empty<string>()
                };
                if (supportedCalls.Length == 0) continue;
                SyntaxNode syntax;
                try { syntax = method.DeclaringSyntaxReferences[0].GetSyntax(); }
                catch { continue; }
                SemanticModel semantic = FileSemanticObservationCache.For(compilation).GetSemanticModel(syntax.SyntaxTree);
                foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access ||
                        !supportedCalls.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal)) continue;
                    string receiver = semantic.GetTypeInfo(access.Expression).Type?.ToDisplayString();
                    if (receiver is not ("LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or
                        "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader" or "LuminPack.Core.LuminPackEvaluator")) continue;
                    foreach (var argument in invocation.ArgumentList.Arguments)
                    {
                        var argumentType = semantic.GetTypeInfo(argument.Expression).Type;
                        if (argumentType is not null && argumentType.TypeKind != TypeKind.Error)
                        {
                            foreach (var dependency in ExpandCustomDependency(argumentType, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
                                yield return dependency;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Includes the closed formatter arguments used by a type called from Custom code.</summary>
    /// <param name="type">The direct argument formatter type.</param>
    /// <param name="visited">Types already added.</param>
    /// <returns>The concrete formatter graph required by its generated state operation.</returns>
    private static IEnumerable<ITypeSymbol> ExpandCustomDependency(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type)) yield break;
        yield return type;
        if (type is IArrayTypeSymbol array)
        {
            foreach (var child in ExpandCustomDependency(array.ElementType, visited)) yield return child;
        }
        else if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
                foreach (var child in ExpandCustomDependency(argument, visited)) yield return child;
        }
    }

    /// <summary>Renders the generic fallback dispatch only when its complete formatter table changes.</summary>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="cache">The generator-instance dispatch contribution cache.</param>
    /// <returns>The single generic dispatch file.</returns>
    private static IEnumerable<FrozenGeneratedSource> DispatchFiles(Compilation compilation, MetaInfo metadata,
        InvocationSupportCache cache)
    {
        string source = cache.Part("Formatters").GetOrBuild(compilation, metadata, CompiledOptions.Resolve(null, compilation).Identity, Array.Empty<ITypeSymbol>(),
            () => "// <auto-generated/>" + Environment.NewLine + BuildFormatterDispatch(metadata, CompiledOptions.Resolve(null, compilation)));
        var file = cache.Files.Wrap("LuminPack.Dispatch", "Formatters", string.Empty, source, string.Empty);
        return new[] { new FrozenGeneratedSource("LuminPack.Dispatch.Formatters.g.cs", file.Text) };
    }
}
