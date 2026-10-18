using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>
/// Emits the <c>LuminPackSerializer</c> entry-point class for a compilation.
/// Full generation preserves the complete concrete API surface. Light and Minimal
/// retain concrete overloads selected by project calls, alongside shared generic
/// fallbacks that route through generated formatter dispatch.
/// </summary>
public static partial class LuminPackSerializerGenerator
{
    [ThreadStatic] private static string _currentAccess = "public";
    [ThreadStatic] private static bool? _currentContainsReferences;

    /// <summary>Builds the serializer facade for the types reachable in the compilation.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="reachability">The reachability used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GenerateSerializerClass(Compilation compilation, MetaInfo metaInfo, ReachabilityAnalysis? reachability = null)
        => GenerateSerializerClass(compilation, metaInfo, reachability, new SerializerFacadeCache());

    /// <summary>Builds the legacy facade from independently cached concrete overload and XML fragments.</summary>
    /// <param name="compilation">The compilation supplying current semantic facts.</param>
    /// <param name="metaInfo">The current generation settings.</param>
    /// <param name="reachability">The existing reachability filter.</param>
    /// <param name="fragments">The generator-instance cache for concrete facade contributions.</param>
    /// <returns>The exact legacy serializer source.</returns>
    internal static string GenerateSerializerClass(Compilation compilation, MetaInfo metaInfo, ReachabilityAnalysis? reachability, SerializerFacadeCache fragments)
    {
        var parts = BuildSerializerParts(compilation, metaInfo, reachability, fragments);
        return fragments.Assemble(parts.Header, parts.Blocks.Select(block => block.Value).ToArray(), parts.Tail)
            .Replace("    public static class LuminPackSerializer", "    public static partial class LuminPackSerializer");
    }

    /// <summary>Emits common helpers and independently cached concrete partial declarations.</summary>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="reachability">The formatter and call-selected API roots.</param>
    /// <param name="cache">The generator-instance fragment and file cache.</param>
    /// <returns>Common serializer support followed by stable per-type files.</returns>
    internal static ImmutableArray<FrozenGeneratedSource> GenerateSerializerFiles(Compilation compilation, MetaInfo metadata,
        ReachabilityAnalysis reachability, SerializerFacadeCache cache)
    {
        var parts = BuildSerializerParts(compilation, metadata, reachability, cache);
        const string declaration = "    public static class LuminPackSerializer";
        const string partialDeclaration = "    public static partial class LuminPackSerializer";
        string footer = "    }" + Environment.NewLine + "}" + Environment.NewLine;
        var files = ImmutableArray.CreateBuilder<FrozenGeneratedSource>(parts.Blocks.Count + 1);
        var common = cache.Files.Wrap("LuminPackSerializer.Common", "common", parts.Header.Replace(declaration, partialDeclaration), parts.Tail, string.Empty);
        files.Add(new FrozenGeneratedSource("LuminPackSerializer.g.cs", common.Text));
        string typeHeader = parts.Prelude + partialDeclaration + Environment.NewLine + "    {" + Environment.NewLine;
        foreach (var block in parts.Blocks)
            files.Add(cache.Files.Wrap("LuminPackSerializer", block.Key, typeHeader, block.Value, footer));
        return files.MoveToImmutable();
    }

    /// <summary>Builds reusable serializer contributions without copying every concrete body into one string.</summary>
    /// <param name="compilation">The compilation supplying current formatter facts.</param>
    /// <param name="metaInfo">Current generation settings.</param>
    /// <param name="reachability">The current formatter and facade root filter.</param>
    /// <param name="fragments">The generator-instance concrete contribution cache.</param>
    /// <returns>The unchanged legacy source components and canonical concrete identities.</returns>
    private static SerializerParts BuildSerializerParts(Compilation compilation, MetaInfo metaInfo, ReachabilityAnalysis reachability, SerializerFacadeCache fragments)
    {
        // The serializer entry class is always emitted so user code can reference
        // LuminPackSerializer.Serialize<T> / Deserialize<T> (and the IDE offers completion) even when
        // pruning yields no concrete formatter candidates.  The generic fallbacks below route through
        // the generic dispatch, which throws the normal "no formatter" error for unreachable types.
        ITypeSymbol[] orderedTypes = LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation, reachability);
        var apiUsage = reachability?.ApiUsage;
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Buffers;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using global::System.IO;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::System.Runtime.InteropServices;");
        sb.AppendLine("using global::System.Text;");
        sb.AppendLine("using global::System.Threading;");
        sb.AppendLine("using global::System.Threading.Tasks;");
        sb.AppendLine("using global::LuminPack.Core;");
        sb.AppendLine("using global::LuminPack.Code;");
        sb.AppendLine("using global::LuminPack.Attribute;");
        sb.AppendLine("using global::LuminPack.Generated;");
        sb.AppendLine("using global::LuminPack.Option;");
        sb.AppendLine("using global::LuminPack.Utility;");
        sb.AppendLine("using static global::LuminPack.Code.LuminPackMarshal;");
        sb.AppendLine();
        sb.AppendLine("namespace LuminPack");
        sb.AppendLine("{");
        string prelude = sb.ToString();
        sb.AppendLine("    /// <summary>Provides convenience and reusable-buffer APIs for LuminPack binary and JSON serialization.</summary>");
        sb.AppendLine("    /// <remarks>Options are compiled from declarations or stateless profile types. Overloads accepting <see cref=\"LuminBufferWriter\"/> use");
        sb.AppendLine("    /// the reference-tracking state owned by that writer for a ThreadStatic-free high-performance path.</remarks>");
        sb.AppendLine("    public static class LuminPackSerializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [ThreadStatic]");
        sb.AppendLine("        private static LuminPackWriterOptionalState? _threadStaticWriterOptionalState;");
        sb.AppendLine("        [ThreadStatic]");
        sb.AppendLine("        private static LuminPackReaderOptionalState? _threadStaticReaderOptionalState;");
        sb.AppendLine("        [ThreadStatic]");
        sb.AppendLine("        private static LuminPackEvaluatorOptionState? _threadStaticEvaluatorOptionalState;");
        sb.AppendLine();

        string header = sb.ToString();
        var blocks = new List<KeyValuePair<string, string>>(orderedTypes.Length);
        foreach (ITypeSymbol type in orderedTypes)
        {
            string typeName = FormatterTypeName.Get(type);
            var apis = apiUsage?.GetApis(type) ?? SerializerApiKind.All;
            if (apis == SerializerApiKind.None) continue;
            _currentAccess = HasPublicSignature(type) ? "public" : "internal";
            var frozen = fragments.Fragments.GetOrBuild(type, compilation, metaInfo, LuminPackGenerationTier.Full, () =>
            {
                string text = RenderOverloadFragment(type, typeName, compilation, metaInfo, apis);
                return new FrozenTypeEmission(typeName, text, string.Empty,
                    new FrozenLayoutGroup(string.Empty, metaInfo.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null), Array.Empty<Diagnostic>());
            }, serializerFragment: true, outputVariant: apis == SerializerApiKind.All ? null : ((int)apis).ToString(System.Globalization.CultureInfo.InvariantCulture));
            blocks.Add(new KeyValuePair<string, string>(typeName, frozen.Extension));
        }

        _currentAccess = "public";
        string documentedTail = fragments.GetCommon(metaInfo, CompiledOptions.Resolve(null, compilation).Identity, () =>
        {
            var tail = new StringBuilder();
            AppendGenericFallbacks(tail, metaInfo);
            AppendCompressDecompress(tail);
            return DocumentFragment(tail.ToString(), Array.Empty<ITypeSymbol>(), compilation, metaInfo) + "    }" + Environment.NewLine + "}" + Environment.NewLine;
        });
        return new SerializerParts(prelude, header, blocks, documentedTail);
    }

    /// <summary>Preserves the facade's visibility policy while checking enclosing and constructed types.</summary>
    /// <param name="type">The concrete overload payload.</param>
    /// <returns>Whether the existing public overload policy can expose this complete signature.</returns>
    private static bool HasPublicSignature(ITypeSymbol type)
        => type.DeclaredAccessibility == Accessibility.Public && IsPublicSignatureType(type);

    /// <summary>Checks the effective accessibility of all components of a payload type.</summary>
    /// <param name="type">A payload, enclosing type, or generic argument.</param>
    /// <returns>Whether external consumers can name the complete type.</returns>
    private static bool IsPublicSignatureType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return IsPublicSignatureType(array.ElementType);
        if (type is not INamedTypeSymbol named) return type.DeclaredAccessibility == Accessibility.Public;
        return named.DeclaredAccessibility == Accessibility.Public &&
            (named.ContainingType == null || IsPublicSignatureType(named.ContainingType)) &&
            named.TypeArguments.All(IsPublicSignatureType);
    }

    /// <summary>Keeps legacy composition available while production emits independent partial files.</summary>
    private sealed class SerializerParts
    {
        internal readonly string Prelude;
        internal readonly string Header;
        internal readonly IReadOnlyList<KeyValuePair<string, string>> Blocks;
        internal readonly string Tail;

        /// <summary>Groups the value-only serializer file contributions.</summary>
        /// <param name="prelude">Imports and namespace before the type declaration.</param>
        /// <param name="header">The complete legacy header, including operation state fields.</param>
        /// <param name="blocks">Canonical type identities and concrete overload bodies.</param>
        /// <param name="tail">Common APIs, helpers, and closing declarations.</param>
        internal SerializerParts(string prelude, string header, IReadOnlyList<KeyValuePair<string, string>> blocks, string tail)
        { Prelude = prelude; Header = header; Blocks = blocks; Tail = tail; }
    }

    /// <summary>Produces and documents one concrete type's unchanged overload block.</summary>
    /// <param name="type">The concrete type.</param>
    /// <param name="typeName">Its canonical formatter type name.</param>
    /// <param name="compilation">The compilation supplying contract metadata.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <param name="apis">Concrete overloads selected by current project calls.</param>
    /// <returns>The complete overload block with legacy documentation.</returns>
    private static string RenderOverloadFragment(ITypeSymbol type, string typeName, Compilation compilation, MetaInfo metadata, SerializerApiKind apis)
    {
        var builder = new StringBuilder();
        var previousClassification = _currentContainsReferences;
        _currentContainsReferences = !global::LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.CanUseUnmanagedSerialization(type);
        try
        {
            AppendTypeOverloads(builder, typeName, metadata, apis);
            return DocumentFragment(builder.ToString(), new[] { type }, compilation, metadata);
        }
        finally { _currentContainsReferences = previousClassification; }
    }

    /// <summary>Forwards a convenience entry to the unique state-based binary implementation.</summary>
    /// <param name="builder">The concrete overload source builder.</param>
    /// <param name="read">Whether the operation reads into the value.</param>
    /// <param name="indent">Indentation at the operation site.</param>
    private static void AppendBinaryOperation(StringBuilder builder, bool read, int indent = 16)
    {
        builder.Append(' ', indent).AppendLine(read ? "global::LuminPack.LuminPackSerializer.Deserialize(ref reader, ref value);" : "global::LuminPack.LuminPackSerializer.Serialize(ref writer, in value);");
    }

    /// <summary>Forwards a convenience entry to the unique state-based JSON implementation.</summary>
    /// <param name="builder">The concrete JSON source builder.</param>
    /// <param name="read">Whether the operation reads JSON.</param>
    /// <param name="indent">Indentation at the operation site.</param>
    private static void AppendJsonOperation(StringBuilder builder, bool read, int indent = 16)
    {
        builder.Append(' ', indent).AppendLine(read ? "global::LuminPack.LuminPackSerializer.DeserializeJson(ref reader, ref value);" : "global::LuminPack.LuminPackSerializer.SerializeJson(ref writer, in value);");
    }

    /// <summary>Projects and documents one facade fragment through a single parsed syntax tree.</summary>
    /// <param name="fragment">The unchanged method block.</param>
    /// <param name="types">Concrete documentation lookup types for this block.</param>
    /// <param name="compilation">The compilation supplying contract metadata.</param>
    /// <param name="metadata">The current parse and generation settings.</param>
    /// <returns>Only the documented block; the temporary wrapper is discarded.</returns>
    private static string DocumentFragment(string fragment, ITypeSymbol[] types, Compilation compilation, MetaInfo metadata)
    {
        string header = "namespace LuminPack" + Environment.NewLine + "{" + Environment.NewLine + "    public static class LuminPackSerializer" + Environment.NewLine + "    {" + Environment.NewLine;
        string footer = "    }" + Environment.NewLine + "}" + Environment.NewLine;
        string source = System.Text.RegularExpressions.Regex.Replace(header + fragment + footer,
            "(?m)^\\s*/// <param name\\s*=\\s*\"(?:option|options)\">[^\\r\\n]*\\r?\\n", string.Empty);
        var options = CompiledOptions.Resolve(types.FirstOrDefault(), compilation);
        var root = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse)).GetCompilationUnitRoot();
        root = (CompilationUnitSyntax)new CompiledOptionApiProjection(options).Visit(root);
        root = (CompilationUnitSyntax)new CompiledOperationProjection(compilation, options).Visit(root);
        string documented = LuminPackSerializerDocumentation.Enhance(root, types, compilation, metadata);
        return documented.Substring(header.Length, documented.Length - header.Length - footer.Length);
    }

    /// <summary>Builds the type overloads source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="apis">The exact concrete overloads to emit, or all for legacy behavior.</param>
    private static void AppendTypeOverloads(StringBuilder sb, string typeName, MetaInfo metaInfo, SerializerApiKind apis = SerializerApiKind.All)
    {
        if ((apis & SerializerApiKind.SerializeArray) != 0) AppendSerialize(sb, typeName);
        if ((apis & SerializerApiKind.SerializeBuffer) != 0) AppendSerializeBuffer(sb, typeName);
        if ((apis & SerializerApiKind.SerializeJsonString) != 0) AppendSerializeJson(sb, typeName);
        if ((apis & SerializerApiKind.SerializeJsonBuffer) != 0) AppendSerializeJsonBuffer(sb, typeName);
        if ((apis & SerializerApiKind.SerializeAsync) != 0) AppendSerializeAsync(sb, typeName, metaInfo);
        if ((apis & SerializerApiKind.DeserializeSpanRef) != 0) AppendDeserializeSpanRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeBufferRef) != 0) AppendDeserializeBufferRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeSequenceRef) != 0) AppendDeserializeSequenceRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeJsonStringRef) != 0) AppendDeserializeJsonStringRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeJsonSpanByteRef) != 0) AppendDeserializeJsonSpanByteRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeJsonSpanCharRef) != 0) AppendDeserializeJsonSpanCharRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeJsonBufferRef) != 0) AppendDeserializeJsonBufferRef(sb, typeName);
        if ((apis & SerializerApiKind.DeserializeAsyncValue) != 0) AppendDeserializeAsyncValue(sb, typeName, metaInfo);
        if ((apis & SerializerApiKind.Sizeof) != 0) AppendSizeof(sb, typeName);
    }

    /// <summary>Builds the sizeof source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    private static void AppendSizeof(StringBuilder sb, string typeName)
    {
        sb.AppendLine("        /// <summary>Calculates the binary payload size for <paramref name=\"value\"/> without producing the payload.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The value whose serialized size is calculated.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this calculation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes required by binary serialization.</returns>");
        sb.AppendLine("        /// <remarks>This convenience API may use thread-static evaluator state and does not modify a BufferWriter.</remarks>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Sizeof(" + typeName + " value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = _threadStaticEvaluatorOptionalState ??= new LuminPackEvaluatorOptionState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var totalLength = 0;");
        sb.AppendLine();
        sb.AppendLine("                var evaluator = new LuminPackEvaluator(ref totalLength, state);");
        sb.AppendLine();
        sb.AppendLine("                global::LuminPack.LuminPackSerializer.Sizeof(ref evaluator, ref value);");
        sb.AppendLine();
        sb.AppendLine("                return totalLength;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Builds the register source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendRegister(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Manually registers write/read method pointers for a type the source generator did not cover.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The runtime type to register.</typeparam>");
        sb.AppendLine("        /// <param name=\"writeValue\">Static binary writer, e.g. <c>&amp;MyType.Write</c>.</param>");
        sb.AppendLine("        /// <param name=\"readValue\">Static binary reader, e.g. <c>&amp;MyType.Read</c>.</param>");
        sb.AppendLine("        /// <remarks>Requires an unsafe context at the call site.</remarks>");
        sb.AppendLine("        public static unsafe void Register<T>(");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackWriter, in T, void> writeValue,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackReader, ref T, void> readValue)");
        sb.AppendLine("            => global::LuminPack.Code.LuminPackFormatterRegistry.Register(typeof(T), (nint)writeValue, (nint)readValue);");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Manually registers write/read + JSON method pointers for a type the source generator did not cover.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The runtime type to register.</typeparam>");
        sb.AppendLine("        /// <param name=\"writeValue\">Static binary writer, e.g. <c>&amp;MyType.Write</c>.</param>");
        sb.AppendLine("        /// <param name=\"readValue\">Static binary reader, e.g. <c>&amp;MyType.Read</c>.</param>");
        sb.AppendLine("        /// <param name=\"writeValueJson\">Static JSON writer, e.g. <c>&amp;MyType.WriteJson</c>.</param>");
        sb.AppendLine("        /// <param name=\"readValueJson\">Static JSON reader, e.g. <c>&amp;MyType.ReadJson</c>.</param>");
        sb.AppendLine("        /// <remarks>Requires an unsafe context at the call site.</remarks>");
        sb.AppendLine("        public static unsafe void Register<T>(");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackWriter, in T, void> writeValue,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackReader, ref T, void> readValue,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackJsonWriter, in T, void> writeValueJson,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref T, void> readValueJson)");
        sb.AppendLine("            => global::LuminPack.Code.LuminPackFormatterRegistry.Register(typeof(T), (nint)writeValue, (nint)readValue, (nint)writeValueJson, (nint)readValueJson);");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Manually registers write/read + JSON + offset method pointers for a type the source generator did not cover.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The runtime type to register.</typeparam>");
        sb.AppendLine("        /// <param name=\"writeValue\">Static binary writer, e.g. <c>&amp;MyType.Write</c>.</param>");
        sb.AppendLine("        /// <param name=\"readValue\">Static binary reader, e.g. <c>&amp;MyType.Read</c>.</param>");
        sb.AppendLine("        /// <param name=\"writeValueJson\">Static JSON writer, e.g. <c>&amp;MyType.WriteJson</c>.</param>");
        sb.AppendLine("        /// <param name=\"readValueJson\">Static JSON reader, e.g. <c>&amp;MyType.ReadJson</c>.</param>");
        sb.AppendLine("        /// <param name=\"calculateOffset\">Static size calculator, e.g. <c>&amp;MyType.CalcSize</c>.</param>");
        sb.AppendLine("        /// <remarks>Requires an unsafe context at the call site.</remarks>");
        sb.AppendLine("        public static unsafe void Register<T>(");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackWriter, in T, void> writeValue,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackReader, ref T, void> readValue,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackJsonWriter, in T, void> writeValueJson,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref T, void> readValueJson,");
        sb.AppendLine("            delegate*<ref global::LuminPack.Core.LuminPackEvaluator, in T, void> calculateOffset)");
        sb.AppendLine("            => global::LuminPack.Code.LuminPackFormatterRegistry.Register(typeof(T), (nint)writeValue, (nint)readValue, (nint)writeValueJson, (nint)readValueJson, (nint)calculateOffset);");
        sb.AppendLine();
    }

    /// <summary>Builds the generic sizeof source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendGenericSizeof(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>Calculates the binary payload size for a <typeparamref name=\"T\"/> without producing the payload.</summary>");
        sb.AppendLine("        /// <typeparam name=\"T\">The value type to evaluate.</typeparam>");
        sb.AppendLine("        /// <param name=\"value\">The value whose serialized size is calculated.</param>");
        sb.AppendLine("        /// <param name=\"option\">The configuration for this calculation, or <see langword=\"null\"/> for defaults.</param>");
        sb.AppendLine("        /// <returns>The number of bytes required by binary serialization.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        " + _currentAccess + " static int Sizeof<T>(T value, LuminPackSerializerOption? option = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            var state = _threadStaticEvaluatorOptionalState ??= new LuminPackEvaluatorOptionState();");
        sb.AppendLine("            state.Init(option);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var totalLength = 0;");
        sb.AppendLine("                var evaluator = new LuminPackEvaluator(ref totalLength, state);");
        sb.AppendLine("                evaluator.CalculateOffset(ref value);");
        sb.AppendLine("                return totalLength;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                state.Reset();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
