using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Emits cold per-payload/profile registration without an eager assembly initializer.</summary>
    /// <param name="compilation">The compilation owning the concrete roots.</param>
    /// <param name="metadata">The consumer generation policy.</param>
    /// <param name="reachability">The reachable formatter contracts.</param>
    /// <param name="targets">The direct root calls recognized in each payload's contribution.</param>
    /// <returns>Entries constructed only on first generic use, after explicit registrations are checked.</returns>
    private static string BuildLazyFormatters(Compilation compilation, MetaInfo metadata, ReachabilityAnalysis reachability,
        IReadOnlyDictionary<string, LazyForwardingTargets> targets)
    {
        var types = GetOrderedFormatterTypes(compilation, reachability)
            .Where(type => !ContainsTypeParameter(type) && IsAotVisible(type) && !IsCustomGeneratorType(type) &&
                CanRegisterFormatter(type, compilation, false) && CanRegisterCalculateFormatter(type, compilation))
            .GroupBy(FormatterTypeName.Get, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        if (!metadata.AllowUnsafe || metadata.RegisterMode == LuminPackRegisterMode.Disabled)
            return BuildSafeLazyFormatters(compilation, metadata, types, targets);
        var sb = new StringBuilder("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649\nnamespace LuminPack { public static partial class LuminPackSerializer { internal static partial class FormatterDispatch {\n");
        sb.AppendLine("private static class Generated<T, TProfile> where TProfile : struct, global::LuminPack.Option.ILuminPackOptions { internal static readonly global::LuminPack.Code.LuminPackFormatter Entry = CreateGenerated<T, TProfile>(); }");
        sb.AppendLine("private static unsafe global::LuminPack.Code.LuminPackFormatter CreateGenerated<T, TProfile>() where TProfile : struct, global::LuminPack.Option.ILuminPackOptions {");
        for (int i = 0; i < types.Length; i++)
        {
            string type = FormatterTypeName.Get(types[i]);
            sb.Append("if (typeof(T) == typeof(").Append(type).Append(")) return new global::LuminPack.Code.LuminPackFormatter { WriteValue = (nint)(delegate*<ref global::LuminPack.Core.LuminPackWriter, in ").Append(type)
                .Append(", void>)&Lazy").Append(i).Append("<TProfile>.Write, ReadValue = (nint)(delegate*<ref global::LuminPack.Core.LuminPackReader, ref ").Append(type)
                .Append(", void>)&Lazy").Append(i).Append("<TProfile>.Read, CalculateOffset = (nint)(delegate*<ref global::LuminPack.Core.LuminPackEvaluator, in ").Append(type)
                .Append(", void>)&Lazy").Append(i).Append("<TProfile>.Size");
            if (CanRegisterFormatter(types[i], compilation, true))
                sb.Append(", WriteValueJson = (nint)(delegate*<ref global::LuminPack.Core.LuminPackJsonWriter, in ").Append(type).Append(", void>)&Lazy").Append(i)
                    .Append("<TProfile>.WriteJson, ReadValueJson = (nint)(delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref ").Append(type).Append(", void>)&Lazy").Append(i).Append("<TProfile>.ReadJson");
            sb.AppendLine(" };");
        }
        sb.AppendLine("return default; }");
        for (int i = 0; i < types.Length; i++)
        {
            string type = FormatterTypeName.Get(types[i]);
            targets.TryGetValue(type, out var direct);
            sb.Append("private static class Lazy").Append(i).AppendLine("<TProfile> where TProfile : struct, global::LuminPack.Option.ILuminPackOptions {");
            foreach (string operation in new[] { "Write", "Read", "Size", "WriteJson", "ReadJson" })
            {
                bool json = operation.EndsWith("Json", StringComparison.Ordinal);
                if (json && !CanRegisterFormatter(types[i], compilation, true)) continue;
                bool read = operation.StartsWith("Read", StringComparison.Ordinal);
                string state = operation == "Size" ? "Evaluator" : json ? read ? "JsonReader" : "JsonWriter" : read ? "Reader" : "Writer";
                string api = operation == "Size" ? "Sizeof" : (read ? "Deserialize" : "Serialize") + (json ? "Json" : "");
                string argument = operation == "Size" ? "ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value)" : (read ? "ref " : "in ") + "value";
                sb.Append("internal static void ").Append(operation).Append("(ref global::LuminPack.Core.LuminPack").Append(state).Append(" state, ").Append(read ? "ref " : "in ").Append(type).AppendLine(" value) {");
                sb.Append("if (typeof(TProfile) == typeof(global::LuminPack.Option.LuminPackDeclaredOptions)) ")
                    .AppendLine(direct?.Call(api, null, "state", "value") ?? "global::LuminPack.LuminPackSerializer." + api + "(ref state, " + argument + ");");
                sb.Append("else ").Append(direct?.Call(api, "TProfile", "state", "value") ?? "global::LuminPack.LuminPackSerializer." + api + "<TProfile>(ref state, " + argument + ");").AppendLine(" }");
            }
            sb.AppendLine("}");
        }
        AppendGeneratedJsonHelpers(sb, compilation, types, true);
        return GeneratedSourceDocumentation.Complete(sb.AppendLine("} } }").ToString(), metadata.ParseOptions);
    }

    /// <summary>Emits declaration-owned JSON boundaries for either lazy dispatch representation.</summary>
    /// <param name="sb">The partial dispatch source.</param>
    /// <param name="compilation">The declaring compilation.</param>
    /// <param name="types">The available concrete payloads.</param>
    /// <param name="registry">Whether registered pointers are usable by this consumer.</param>
    private static void AppendGeneratedJsonHelpers(StringBuilder sb, Compilation compilation, ITypeSymbol[] types, bool registry)
    {
        var fallback = CompiledOptions.Resolve(null, compilation);
        var declared = types.Select(type => (Type: type, Options: CompiledOptions.Resolve(type, compilation)))
            .Where(item => item.Options.Identity != fallback.Identity).ToArray();
        if (fallback.Input.Enabled || declared.Any(item => item.Options.Input.Enabled) || SafeFacadeProjection.HasPolicies(compilation))
        {
            sb.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] internal static int __LuminPackInputLimit<T>() {");
            foreach (var item in declared)
                sb.AppendLine("if (typeof(T) == typeof(" + FormatterTypeName.Get(item.Type) + ")) return " + (item.Options.Input.Enabled ? SafeFacadeProjection.Limit(item.Options) : int.MaxValue) + ";");
            sb.AppendLine("return " + (fallback.Input.Enabled ? SafeFacadeProjection.Limit(fallback) : int.MaxValue) + "; }");
        }
        sb.AppendLine("private static global::LuminPack.Option.LuminPackStringEncoding GeneratedJsonEncoding<T>() {");
        foreach (var item in declared)
            sb.AppendLine("if (typeof(T) == typeof(" + FormatterTypeName.Get(item.Type) + ")" + (registry ? " && !global::LuminPack.Code.LuminPackFormatterRegistry<" + fallback.TypeName + ">.TryGetCachedJsonValue<T>(out _)" : "") + ") return (global::LuminPack.Option.LuminPackStringEncoding)" + item.Options.Encoding + ";");
        sb.AppendLine("return (global::LuminPack.Option.LuminPackStringEncoding)" + fallback.Encoding + "; }");
        sb.AppendLine("private static bool GeneratedJsonBoundary<T>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete) {");
        foreach (var item in declared)
        {
            sb.AppendLine("if (typeof(T) == typeof(" + FormatterTypeName.Get(item.Type) + ")" + (registry ? " && !global::LuminPack.Code.LuminPackFormatterRegistry<" + fallback.TypeName + ">.TryGetCachedJsonValue<T>(out _)" : "") + ") {");
            if (item.Options.Input.Enabled) sb.AppendLine("global::LuminPack.Security.LuminPackInputPolicy.CheckInput(reader.Buffer.Length, " + SafeFacadeProjection.Limit(item.Options) + ");");
            sb.AppendLine("if (complete) { reader.EnsureEndOfDocument<" + item.Options.TypeName + ">(); return true; } return reader.Read<" + item.Options.TypeName + ">(); }");
        }
        if (fallback.Input.Enabled) sb.AppendLine("global::LuminPack.Security.LuminPackInputPolicy.CheckInput(reader.Buffer.Length, " + SafeFacadeProjection.Limit(fallback) + ");");
        sb.AppendLine("if (complete) { reader.EnsureEndOfDocument<" + fallback.TypeName + ">(); return true; } return reader.Read<" + fallback.TypeName + ">(); }");
    }
}
