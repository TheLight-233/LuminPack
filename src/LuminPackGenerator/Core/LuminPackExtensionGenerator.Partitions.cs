using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
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
        var candidates = ordered.Where(type => type is not INamedTypeSymbol named || !HasPackableAttribute(named));
        var specializations = ordered.Where(type => type is INamedTypeSymbol { IsGenericType: true } named &&
            HasPackableAttribute(named) && SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly));
        var custom = ordered.Where(IsCustomGeneratorType);
        var customDependencies = GetCustomFormatterDependencies(analysis, compilation)
            .Distinct(SymbolEqualityComparer.Default).OfType<ITypeSymbol>().ToArray();
        var types = candidates.Concat(specializations).Concat(custom).Concat(customDependencies)
            .Where(type => !ContainsTypeParameter(type) && IsAotVisible(type) && IsStaticFormatterCandidate(type))
            .OrderBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal).GroupBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        string extension = GetExtensionClassName(compilation);
        var files = ImmutableArray.CreateBuilder<FrozenGeneratedSource>();
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
                else if (type is INamedTypeSymbol { IsGenericType: true } named && HasPackableAttribute(named) &&
                    SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly))
                    source = SerializerCoreProjection.Project(GenerateOperationTemplates(type, compilation, metadata), compilation, metadata, cache.Operations, type);
                else
                {
                    GenerateFormatterExtensions(body, new LuminLocalFieldData { TypeName = name, TypeSymbol = type, Name = "value", IsValue = type.IsValueType },
                        metadata, new HashSet<string>(StringComparer.Ordinal), emitOptionalVariants: true);
                    source = SerializerCoreProjection.Project(GenerateExtension(body, extension), compilation, metadata, cache.Operations);
                }
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFormatterBuilds);
                return new FrozenTypeEmission(name, source, string.Empty,
                    new FrozenLayoutGroup(extension, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null), Array.Empty<Diagnostic>());
            }, serializerFragment: true, outputVariant: "invocation:" + (reachability == null));
            if (!string.IsNullOrEmpty(fragment.Extension))
                files.Add(cache.Files.Wrap("LuminPack.Formatter", name, string.Empty, fragment.Extension, string.Empty));
        }

        var dispatchTypes = ordered.Concat(customDependencies)
            .Where(type => !ContainsTypeParameter(type) && IsAotVisible(type) && IsStaticFormatterCandidate(type))
            .OrderBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .GroupBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .Select(group => group.First());
        var dispatch = dispatchTypes.Select((type, index) => new DispatchEntry(index, FormatterTypeName.Get(type),
            CanRegisterFormatter(type, compilation, json: false), CanRegisterFormatter(type, compilation, json: true),
            CanRegisterCalculateFormatter(type, compilation))).Where(entry => entry.Binary || entry.Json || entry.Calculate).ToArray();
        var key = new StringBuilder(extension).Append('|');
        foreach (var entry in dispatch)
            key.Append(entry.Id).Append(':').Append(entry.Name.Length).Append(':').Append(entry.Name).Append(':')
                .Append(entry.Binary).Append(':').Append(entry.Json).Append(':').Append(entry.Calculate).Append(';');
        string identity = key.ToString();
        foreach (var part in DispatchFiles(compilation, metadata, cache, extension, dispatch, identity)) files.Add(part);
        files.Add(new FrozenGeneratedSource("LuminPack.FormatterBridges.g.cs", SerializerCoreProjection.Bridges(extension, metadata)));
        return files.ToImmutable();
    }

    /// <summary>Collects concrete state formatter types called by source Custom formatter methods.</summary>
    /// <param name="analysis">The compilation-local declaration index.</param>
    /// <param name="compilation">The current compilation supplying semantic argument types.</param>
    /// <returns>Types needed by legacy Writer and Reader calls inside Custom callbacks.</returns>
    private static IEnumerable<ITypeSymbol> GetCustomFormatterDependencies(CompilationTypeAnalysis analysis, Compilation compilation)
    {
        foreach (var declared in analysis.DeclaredTypes)
        {
            INamedTypeSymbol type = declared.Symbol;
            if (!IsCustomGeneratorType(type) || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) continue;
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.Parameters.Length == 0 || method.DeclaringSyntaxReferences.Length == 0) continue;
                string[] supportedCalls = method.Name switch
                {
                    "Serialize" or "SerializeJson" => new[] { "WriteValue", "WriteValueWithCompress" },
                    "Deserialize" or "DeserializeJson" => new[] { "ReadValue", "ReadFreshValue" },
                    "CalculateOffset" => new[] { "CalculateOffset" },
                    _ => Array.Empty<string>()
                };
                if (supportedCalls.Length == 0) continue;
                SyntaxNode syntax;
                try { syntax = method.DeclaringSyntaxReferences[0].GetSyntax(); }
                catch { continue; }
                string parameterName = method.Parameters[0].Name;
                SemanticModel semantic = compilation.GetSemanticModel(syntax.SyntaxTree);
                foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access ||
                        !supportedCalls.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal) ||
                        access.Expression is not IdentifierNameSyntax receiver || receiver.Identifier.ValueText != parameterName) continue;
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

    /// <summary>Renders fixed dispatch operation files only when their complete dispatch projection changes.</summary>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="cache">The generator-instance dispatch contribution cache.</param>
    /// <param name="extension">The shared partial extension container.</param>
    /// <param name="dispatch">Current dense ids and operation capabilities.</param>
    /// <param name="identity">The complete value projection used for cache validation.</param>
    /// <returns>Registration and five generic operation files.</returns>
    private static IEnumerable<FrozenGeneratedSource> DispatchFiles(Compilation compilation, MetaInfo metadata,
        InvocationSupportCache cache, string extension, DispatchEntry[] dispatch, string identity)
    {
        // One guarded cache holds the small operation array as individually reusable source values.
        var rendered = new List<FrozenGeneratedSource>();
        string[] names = { "Registration", "BinaryWrite", "BinaryRead", "Calculate", "JsonWrite", "JsonRead" };
        Dictionary<string, string> changed = null;
        foreach (string name in names)
        {
            string source = cache.Part(name).GetOrBuild(compilation, metadata, identity, Array.Empty<ITypeSymbol>(), () =>
            {
                if (changed == null)
                {
                    changed = new Dictionary<string, string>(StringComparer.Ordinal);
                    BuildGenericDispatchParts(metadata, extension, dispatch, (part, body) => changed.Add(part,
                        SerializerCoreProjection.Project(GenerateExtension(new StringBuilder(body), extension), compilation, metadata, cache.Operations)));
                }
                return changed[name];
            });
            var file = cache.Files.Wrap("LuminPack.Dispatch", name, string.Empty, source, string.Empty);
            rendered.Add(new FrozenGeneratedSource("LuminPack.Dispatch." + name + ".g.cs", file.Text));
        }
        return rendered;
    }
}
