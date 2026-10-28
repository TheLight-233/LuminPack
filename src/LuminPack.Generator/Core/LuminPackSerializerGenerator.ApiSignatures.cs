using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{
    private static readonly ConditionalWeakTable<object, ApiSignatureCache> ApiSignatures = new();

    /// <summary>Reuses a body-free signature tree while every overload name and accessibility remains equal.</summary>
    /// <param name="compilation">The current input compilation.</param>
    /// <param name="metadata">All generation and parse settings affecting declarations.</param>
    /// <param name="kinds">Receives the immutable concrete overload offset map.</param>
    /// <returns>A symbol-free tree reusable across unrelated source edits.</returns>
    internal static SyntaxTree GetApiSignatures(Compilation compilation, MetaInfo metadata, out Dictionary<int, SerializerApiKind> kinds)
    {
        var types = LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation);
        var key = new StringBuilder();
        foreach (var type in types)
        {
            string name = FormatterTypeName.Get(type);
            key.Append(name.Length).Append(':').Append(name).Append('=').Append(HasPublicSignature(type)).Append('|');
        }
        string identity = key.ToString();
        var cache = ApiSignatures.GetValue(FileSemanticObservationCache.For(compilation).ProjectGuard, _ => new ApiSignatureCache());
        lock (cache)
        {
            if (cache.Tree == null || cache.Key != identity || !MetaInfoValueComparer.Instance.Equals(cache.Metadata, metadata))
            {
                cache.Tree = CreateApiSignatures(types, metadata, out cache.Kinds);
                cache.Key = identity;
                cache.Metadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8,
                    metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            }
            kinds = cache.Kinds;
            return cache.Tree;
        }
    }

    /// <summary>Creates an analysis-only facade using the actual emitters' method signatures.</summary>
    /// <param name="types">All candidate formatter types in canonical overload order.</param>
    /// <param name="metadata">Settings used to render the legacy signatures.</param>
    /// <param name="kinds">Receives concrete method offsets and their overload identities.</param>
    /// <returns>A temporary syntax tree that is never added to generated output.</returns>
    private static SyntaxTree CreateApiSignatures(ITypeSymbol[] types, MetaInfo metadata,
        out Dictionary<int, SerializerApiKind> kinds)
    {
        System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ApiSignatureBuilds);
        const string placeholder = "global::__LuminPackAnalysisPayload";
        const string header = "using System; using System.Buffers; using System.IO; using System.Threading; using System.Threading.Tasks; " +
            "using LuminPack.Core; using LuminPack.Option; using LuminPack.Utility; namespace LuminPack { public static partial class LuminPackSerializer {";
        string previousAccess = _currentAccess;
        var previousClassification = _currentContainsReferences;
        bool previousScalar = _currentRawScalar;
        bool previousRecord = _currentDirectRecord;
        bool previousFixed = _currentFixedRecord;
        _currentAccess = "public";
        _currentContainsReferences = null;
        _currentRawScalar = false;
        _currentDirectRecord = false;
        _currentFixedRecord = false;
        try
        {
            var concrete = new StringBuilder();
            AppendTypeOverloads(concrete, placeholder, metadata);
            string[] templates = ReadSignatures(concrete.ToString(), metadata).ToArray();
            // Profile variants depend only on the template, so each is rendered once and specialized by text.
            string[][] variants = templates.Select(template => ProfileSignatures(template, metadata).ToArray()).ToArray();
            var source = new StringBuilder(header);
            var orderedKinds = new List<SerializerApiKind>();
            foreach (var type in types)
            {
                string typeName = FormatterTypeName.Get(type);
                bool visible = HasPublicSignature(type);
                for (int index = 0; index < templates.Length; index++)
                {
                    foreach (string variant in variants[index])
                    {
                        string signature = variant.Replace(placeholder, typeName);
                        source.AppendLine(visible ? signature : signature.Replace("public static", "internal static"));
                        orderedKinds.Add((SerializerApiKind)(1 << index));
                    }
                }
            }
            var shared = new StringBuilder();
            AppendGenericFallbacks(shared, metadata, managedInput: false);
            AppendCompressDecompress(shared);
            foreach (string signature in ReadSignatures(shared.ToString(), metadata))
                foreach (string variant in ProfileSignatures(signature, metadata)) source.AppendLine(variant);
            source.Append("} }");
            var tree = CSharpSyntaxTree.ParseText(source.ToString(), metadata.ParseOptions,
                path: "LuminPack.ApiSignatures.analysis.cs");
            var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
            kinds = new Dictionary<int, SerializerApiKind>();
            for (int index = 0; index < orderedKinds.Count; index++) kinds.Add(methods[index].SpanStart, orderedKinds[index]);
            return tree;
        }
        finally
        {
            _currentAccess = previousAccess;
            _currentContainsReferences = previousClassification;
            _currentRawScalar = previousScalar;
            _currentDirectRecord = previousRecord;
            _currentFixedRecord = previousFixed;
        }
    }

    /// <summary>Extracts declarations without retaining generated bodies or documentation.</summary>
    /// <param name="methods">Methods rendered by the existing serializer emitters.</param>
    /// <param name="metadata">The consuming language settings.</param>
    /// <returns>Body-free declarations suitable for semantic overload resolution.</returns>
    private static IEnumerable<string> ReadSignatures(string methods, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText("class Analysis {" + methods + "}", metadata.ParseOptions).GetRoot();
        root = new CompiledOptionApiProjection(CompiledOptions.Default).Visit(root);
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var modifiers = SyntaxFactory.TokenList(method.Modifiers.Where(token => !token.IsKind(SyntaxKind.AsyncKeyword)))
                .Add(SyntaxFactory.Token(SyntaxKind.ExternKeyword));
            yield return method.WithAttributeLists(default).WithModifiers(modifiers).WithBody(null).WithExpressionBody(null)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).WithoutTrivia().NormalizeWhitespace().ToFullString();
        }
    }

    /// <summary>Exposes compiled state overloads to the same semantic trimming analysis as their default entries.</summary>
    /// <param name="signature">One original body-free method declaration.</param>
    /// <param name="metadata">The consumer's language settings.</param>
    /// <returns>The default signature and its stateless-profile variant when applicable.</returns>
    private static IEnumerable<string> ProfileSignatures(string signature, MetaInfo metadata)
    {
        yield return signature;
        var method = SyntaxFactory.ParseMemberDeclaration(signature, options: metadata.ParseOptions) as MethodDeclarationSyntax;
        string state = method?.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString();
        if (state == null || !(state.EndsWith("LuminPackWriter", StringComparison.Ordinal) || state.EndsWith("LuminPackReader", StringComparison.Ordinal) ||
            state.EndsWith("LuminPackEvaluator", StringComparison.Ordinal) || state.EndsWith("LuminPackJsonWriter", StringComparison.Ordinal) || state.EndsWith("LuminPackJsonReader", StringComparison.Ordinal) ||
            method.Identifier.ValueText is "Serialize" or "Deserialize" or "Sizeof" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync" or "Register")) yield break;
        string profile = "__LuminProfile";
        while (method.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == profile) == true) profile += "_";
        var parameters = (method.TypeParameterList?.Parameters ?? default).Add(SyntaxFactory.TypeParameter(profile));
        method = method.WithTypeParameterList(SyntaxFactory.TypeParameterList(parameters)).AddConstraintClauses(
            SyntaxFactory.TypeParameterConstraintClause(profile).AddConstraints(SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
                SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))));
        yield return method.NormalizeWhitespace().ToFullString();
    }

    /// <summary>Keeps only generated syntax, value settings, and offsets across compilations.</summary>
    private sealed class ApiSignatureCache
    {
        internal string Key;
        internal MetaInfo Metadata;
        internal SyntaxTree Tree;
        internal Dictionary<int, SerializerApiKind> Kinds;
    }
}
