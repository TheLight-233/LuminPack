using System;
using System.Collections.Generic;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Keeps first-use generic operations available when the consumer disables unsafe syntax.</summary>
    /// <param name="compilation">The compilation owning the concrete operations.</param>
    /// <param name="metadata">The consumer language and generation policy.</param>
    /// <param name="types">The closed concrete formatter types.</param>
    /// <param name="targets">The direct root calls recognized in each payload's contribution.</param>
    /// <returns>Module-local delegate caches; no delegate or Type is published to a runtime registry.</returns>
    private static string BuildSafeLazyFormatters(Compilation compilation, MetaInfo metadata, ITypeSymbol[] types,
        IReadOnlyDictionary<string, LazyForwardingTargets> targets)
    {
        var sb = new StringBuilder("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649\nnamespace LuminPack { public static partial class LuminPackSerializer { internal static partial class FormatterDispatch {\n");
        foreach (string operation in new[] { "Serialize", "Deserialize", "SerializeJson", "DeserializeJson" })
        {
            bool json = operation.EndsWith("Json", StringComparison.Ordinal);
            bool read = operation.StartsWith("Deserialize", StringComparison.Ordinal);
            string state = "global::LuminPack.Core.LuminPack" + (json ? read ? "JsonReader" : "JsonWriter" : read ? "Reader" : "Writer");
            string passing = read ? "ref" : "in";
            string delegateName = "Safe" + operation + "Delegate";
            string cache = "Safe" + operation;
            sb.AppendLine("private delegate void " + delegateName + "<T>(ref " + state + " state, " + passing + " T value);");
            sb.AppendLine("private static class " + cache + "<T, TProfile> where TProfile : struct, global::LuminPack.Option.ILuminPackOptions {");
            sb.AppendLine("internal static readonly " + delegateName + "<T> Invoke = Create" + cache + "<T, TProfile>(); }");
            sb.AppendLine("private static " + delegateName + "<T> Create" + cache + "<T, TProfile>() where TProfile : struct, global::LuminPack.Option.ILuminPackOptions {");
            foreach (var type in types)
            {
                if (json && !CanRegisterFormatter(type, compilation, true)) continue;
                string name = FormatterTypeName.Get(type);
                sb.AppendLine("if (typeof(T) == typeof(" + name + ")) return static (ref " + state + " state, " + passing + " T value) => {");
                string source = passing == "ref" ? "ref value" : "ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value)";
                sb.AppendLine("ref " + name + " concrete = ref global::LuminPack.Code.LuminPackMarshal.As<T, " + name + ">(" + source + ");");
                targets.TryGetValue(name, out var direct);
                sb.AppendLine("if (typeof(TProfile) == typeof(global::LuminPack.Option.LuminPackDeclaredOptions)) " +
                    (direct?.Call(operation, null, "state", "concrete") ?? "global::LuminPack.LuminPackSerializer." + operation + "(ref state, " + passing + " concrete);"));
                sb.AppendLine("else " + (direct?.Call(operation, "TProfile", "state", "concrete") ??
                    "global::LuminPack.LuminPackSerializer." + operation + "<TProfile>(ref state, " + passing + " concrete);") + " };");
            }
            sb.AppendLine("return static (ref " + state + " state, " + passing + " T value) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T)); }");
        }
        AppendGeneratedJsonHelpers(sb, compilation, types, false);
        return GeneratedSourceDocumentation.Complete(sb.AppendLine("} } }").ToString(), metadata.ParseOptions);
    }
}
