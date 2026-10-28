using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    private const string Declared = "global::LuminPack.Option.LuminPackDeclaredOptions";
    private const string ProfileConstraint = "where __LuminProfile : struct, global::LuminPack.Option.ILuminPackOptions";

    /// <summary>Builds generic operation boundaries with explicit registration precedence and first-use generated entries.</summary>
    /// <param name="metaInfo">Current language, framework, and registration settings.</param>
    /// <param name="fallback">The consuming assembly's constants for unknown registered types.</param>
    /// <returns>Shared generic dispatch with separate cold generated-entry factories.</returns>
    private static string BuildFormatterDispatch(MetaInfo metaInfo, CompiledOptions fallback)
    {
        var sb = new StringBuilder();
        string scopedIn = metaInfo.IsNet8 ? "scoped in " : "in ";
        string scopedRef = metaInfo.IsNet8 ? "scoped ref " : "ref ";
        string scopedState = metaInfo.IsNet8 ? "scoped " : string.Empty;
        bool registry = metaInfo.AllowUnsafe && metaInfo.RegisterMode != LuminPackRegisterMode.Disabled;
        string fallbackProfile = "global::LuminPack.Option.LuminPackFallbackOptions<__LuminProfile, " + fallback.TypeName + ">";
        sb.AppendLine("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649");
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack.Code;");
        sb.AppendLine("using global::LuminPack.Core;");
        sb.AppendLine();
        sb.AppendLine("namespace LuminPack");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class LuminPackSerializer");
        sb.AppendLine("    {");
        sb.AppendLine("        internal static partial class FormatterDispatch");
        sb.AppendLine("        {");

        // Concrete overloads own ordinary calls; generic calls prefer explicit registrations over generated entries.
        foreach (var operation in new[]
        {
            new DispatchOperation("Serialize", "LuminPackWriter", "writer", "in", true, "WriteValue", "in", false),
            new DispatchOperation("Deserialize", "LuminPackReader", "reader", "ref", true, "ReadValue", "ref", false),
            new DispatchOperation("Sizeof", "LuminPackEvaluator", "evaluator", "ref", true, "CalculateOffset", "in", true),
            new DispatchOperation("SerializeJson", "LuminPackJsonWriter", "writer", "in", false, "WriteValueJson", "in", true),
            new DispatchOperation("DeserializeJson", "LuminPackJsonReader", "reader", "ref", false, "ReadValueJson", "ref", true)
        })
            AppendOperation(sb, operation, scopedState, operation.Keyword == "in" ? scopedIn : scopedRef, registry, fallbackProfile);

        sb.AppendLine("            /// <summary>Reports whether a missing member must be cleared before a reused read, as the formatter's null rule requires.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The reader supplying the next member header.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The member type.</typeparam>");
        sb.AppendLine("            /// <returns>Whether the member storage must be reset before the formatter reads it.</returns>");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static bool __LuminPackShouldResetMember<T>(ref global::LuminPack.Core.LuminPackReader reader)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (typeof(T).IsValueType) return false;");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Gets the assembly default encoding of an opaque registered JSON document.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <returns>The consuming assembly's compiled JSON encoding.</returns>");
        sb.AppendLine("            internal static global::LuminPack.Option.LuminPackStringEncoding __LuminPackJsonEncoding<T>()");
        sb.AppendLine("                => GeneratedJsonEncoding<T>();");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Gets the JSON document encoding for a profile.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"TOptions\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <returns>The assembly encoding for the declared marker; otherwise the explicit profile's encoding.</returns>");
        sb.AppendLine("            internal static global::LuminPack.Option.LuminPackStringEncoding __LuminPackJsonEncoding<T, TOptions>() where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("                => typeof(TOptions) == typeof(" + Declared + ") ? __LuminPackJsonEncoding<T>() : default(TOptions).StringEncoding;");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Reads the next JSON token or finishes the document with the assembly profile.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The JSON reader.</param>");
        sb.AppendLine("            /// <param name=\"complete\">Whether the complete document must now be exhausted.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <returns>Whether a token was read, or true after a successful end-of-document check.</returns>");
        sb.AppendLine("            internal static bool __LuminPackJsonReadBoundary<T>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete)");
        sb.AppendLine("            {");
            sb.AppendLine("                return GeneratedJsonBoundary<T>(ref reader, complete);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Reads the next JSON token or finishes the document with a compiled profile.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The JSON reader.</param>");
        sb.AppendLine("            /// <param name=\"complete\">Whether the complete document must now be exhausted.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"TOptions\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <returns>Whether a token was read, or true after a successful end-of-document check.</returns>");
        sb.AppendLine("            internal static bool __LuminPackJsonReadBoundary<T, TOptions>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            {");
        sb.AppendLine("                if (typeof(TOptions) == typeof(" + Declared + ")) return __LuminPackJsonReadBoundary<T>(ref reader, complete);");
        sb.AppendLine("                if (complete)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.EnsureEndOfDocument<TOptions>();");
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("                return reader.Read<TOptions>();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Appends the profile-neutral entry and the profile entry of one generic operation.</summary>
    /// <param name="sb">The destination source.</param>
    /// <param name="operation">Describes the operation being appended.</param>
    /// <param name="scopedState">The scoped prefix accepted by the consumer's language version for the state parameter.</param>
    /// <param name="scopedPayload">The payload passing convention with its scoped prefix.</param>
    /// <param name="registry">Whether manual registrations can be invoked.</param>
    /// <param name="fallbackProfile">The registry profile spelling.</param>
    private static void AppendOperation(StringBuilder sb, DispatchOperation operation, string scopedState, string scopedPayload, bool registry, string fallbackProfile)
    {
        string stateType = "global::LuminPack.Core." + operation.State;
        string signature = "(" + scopedState + "ref " + stateType + " " + operation.Parameter + ", " + scopedPayload + "T value)";
        string call = "(ref " + operation.Parameter + ", " + operation.Keyword + " value)";
        sb.AppendLine("            /// <summary>Processes a payload with the assembly profile through the registered fallback.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <param name=\"" + operation.Parameter + "\">The operation state.</param>");
        sb.AppendLine("            /// <param name=\"value\">The payload storage.</param>");
        sb.AppendLine("            /// <remarks>Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion.</remarks>");
        sb.AppendLine("            [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static void " + operation.Name + "<T>" + signature);
        sb.AppendLine("                => " + operation.Name + "<T, " + Declared + ">" + call + ";");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Processes a payload with a compiled profile through the generic fallback.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"__LuminProfile\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <param name=\"" + operation.Parameter + "\">The operation state.</param>");
        sb.AppendLine("            /// <param name=\"value\">The payload storage.</param>");
        sb.AppendLine("            /// <remarks>Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion.</remarks>");
        sb.AppendLine("            [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static void " + operation.Name + "<T, __LuminProfile>" + signature);
        sb.AppendLine("                " + ProfileConstraint);
        sb.AppendLine("            {");
        sb.AppendLine("                " + operation.Name + "Registered<T, __LuminProfile>" + call + ";");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Invokes the manual registration of a payload without a generated formatter.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"__LuminProfile\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <param name=\"" + operation.Parameter + "\">The operation state.</param>");
        sb.AppendLine("            /// <param name=\"value\">The payload storage.</param>");
        sb.AppendLine("            /// <remarks>Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion.</remarks>");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine("            private static void " + operation.Name + "Registered<T, __LuminProfile>" + signature);
        sb.AppendLine("                " + ProfileConstraint);
        sb.AppendLine("            {");
        if (!registry)
        {
            // The cache lives in this generated module, so its delegates cannot retain an external registration owner.
            sb.AppendLine("                Safe" + operation.Name + "<T, __LuminProfile>.Invoke" + call + ";");
        }
        else
        {
            sb.AppendLine("                if (!global::LuminPack.Code.LuminPackFormatterRegistry<" + fallbackProfile + ">." + (operation.Binary ? "TryGetCachedValue" : "TryGetCachedJsonValue") + "<T>(out var __entry))");
            sb.AppendLine("                    __entry = Generated<T, __LuminProfile>.Entry;");
            sb.AppendLine("                if (__entry." + operation.Pointer + " == 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                unsafe");
            sb.AppendLine("                {");
            sb.AppendLine("                    ((delegate*<ref " + stateType + ", " + operation.PointerKeyword + " T, void>)__entry." + operation.Pointer + ")(ref " + operation.Parameter + ", " + operation.PointerKeyword + " value);");
            sb.AppendLine("                }");
        }
        sb.AppendLine("            }");
        sb.AppendLine();
    }

    /// <summary>Describes one generic operation of the dispatch: its state and registered operation pointer.</summary>
    private sealed class DispatchOperation
    {
        /// <summary>Initializes a new instance of <c>DispatchOperation</c>.</summary>
        /// <param name="name">The operation name on the Serializer.</param>
        /// <param name="state">The operation state type name.</param>
        /// <param name="parameter">The state parameter name.</param>
        /// <param name="keyword">The payload reference keyword of the generic entry.</param>
        /// <param name="binary">Whether this is a binary operation.</param>
        /// <param name="pointer">The registry entry member for this operation.</param>
        /// <param name="pointerKeyword">The payload reference keyword of the registered method pointer.</param>
        /// <param name="optionalPointer">Whether a registration may omit this operation.</param>
        internal DispatchOperation(string name, string state, string parameter, string keyword, bool binary, string pointer, string pointerKeyword, bool optionalPointer)
        {
            Name = name;
            State = state;
            Parameter = parameter;
            Keyword = keyword;
            Binary = binary;
            Pointer = pointer;
            PointerKeyword = pointerKeyword;
            OptionalPointer = optionalPointer;
        }

        /// <summary>Gets the operation name on the Serializer.</summary>
        internal string Name { get; }
        /// <summary>Gets the operation state type name.</summary>
        internal string State { get; }
        /// <summary>Gets the state parameter name.</summary>
        internal string Parameter { get; }
        /// <summary>Gets the payload reference keyword of the generic entry.</summary>
        internal string Keyword { get; }
        /// <summary>Gets whether this is a binary operation.</summary>
        internal bool Binary { get; }
        /// <summary>Gets the registry entry member for this operation.</summary>
        internal string Pointer { get; }
        /// <summary>Gets the payload reference keyword of the registered method pointer.</summary>
        internal string PointerKeyword { get; }
        /// <summary>Gets whether a registration may omit this operation.</summary>
        internal bool OptionalPointer { get; }
    }

}
