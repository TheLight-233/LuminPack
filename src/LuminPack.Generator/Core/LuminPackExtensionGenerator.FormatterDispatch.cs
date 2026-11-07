using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    private const string Declared = "global::LuminPack.Option.LuminPackDeclaredOptions";
    private const string ProfileConstraint = "where TLuminProfile : struct, global::LuminPack.Option.ILuminPackOptions";

    /// <summary>Builds generic operation boundaries that invoke explicitly registered delegates.</summary>
    /// <param name="metaInfo">Current language, framework, and registration settings.</param>
    /// <param name="fallback">The consuming assembly's constants for unknown registered types.</param>
    /// <param name="inputPolicies">Whether generic facade staging requires an assembly input limit.</param>
    /// <returns>Shared registered dispatch without generated payload tables or factories.</returns>
    private static string BuildFormatterDispatch(MetaInfo metaInfo, CompiledOptions fallback, bool inputPolicies)
    {
        var sb = new StringBuilder();
        string scopedIn = metaInfo.IsNet8 ? "scoped in " : "in ";
        string scopedRef = metaInfo.IsNet8 ? "scoped ref " : "ref ";
        string scopedState = metaInfo.IsNet8 ? "scoped " : string.Empty;
        bool registry = metaInfo.RegisterMode != LuminPackRegisterMode.Disabled;
        string fallbackProfile = "global::LuminPack.Option.LuminPackFallbackOptions<TLuminProfile, " + fallback.TypeName + ">";
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

        // Concrete overloads own ordinary calls; generic calls require an actual registration.
        foreach (var operation in new[]
        {
            new DispatchOperation("Serialize", "LuminPackWriter", "writer", "in", true, "WriteValue", "in", false),
            new DispatchOperation("Deserialize", "LuminPackReader", "reader", "ref", true, "ReadValue", "ref", false),
            new DispatchOperation("SerializeJson", "LuminPackJsonWriter", "writer", "in", false, "WriteValueJson", "in", true),
            new DispatchOperation("DeserializeJson", "LuminPackJsonReader", "reader", "ref", false, "ReadValueJson", "ref", true)
        })
            AppendOperation(sb, operation, scopedState, operation.Keyword == "in" ? scopedIn : scopedRef, registry, fallbackProfile);

        sb.AppendLine("            /// <summary>Reports whether a missing member must be cleared before a reused read, as the formatter's null rule requires.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The reader supplying the next member header.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The member type.</typeparam>");
        sb.AppendLine("            /// <returns>Whether the member storage must be reset before the formatter reads it.</returns>");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static bool LuminPackShouldResetMember<T>(ref global::LuminPack.Core.LuminPackReader reader)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (typeof(T).IsValueType) return false;");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Gets the assembly default encoding of an opaque registered JSON document.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <returns>The consuming assembly's compiled JSON encoding.</returns>");
        sb.AppendLine("            internal static global::LuminPack.Option.LuminPackStringEncoding LuminPackJsonEncoding<T>()");
        sb.AppendLine("                => (global::LuminPack.Option.LuminPackStringEncoding)" + fallback.Encoding + ";");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Gets the JSON document encoding for a profile.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"TOptions\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <returns>The assembly encoding for the declared marker; otherwise the explicit profile's encoding.</returns>");
        sb.AppendLine("            internal static global::LuminPack.Option.LuminPackStringEncoding LuminPackJsonEncoding<T, TOptions>() where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("                => typeof(TOptions) == typeof(" + Declared + ") ? LuminPackJsonEncoding<T>() : default(TOptions).StringEncoding;");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Reads the next JSON token or finishes the document with the assembly profile.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The JSON reader.</param>");
        sb.AppendLine("            /// <param name=\"complete\">Whether the complete document must now be exhausted.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <returns>Whether a token was read, or true after a successful end-of-document check.</returns>");
        sb.AppendLine("            internal static bool LuminPackJsonReadBoundary<T>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete)");
        sb.AppendLine("            {");
        if (fallback.Input.Enabled)
            sb.AppendLine("                global::LuminPack.Security.LuminPackInputPolicy.CheckInput(reader.Buffer.Length, " + SafeFacadeProjection.Limit(fallback) + ");");
        sb.AppendLine("                if (complete) { reader.EnsureEndOfDocument<" + fallback.TypeName + ">(); return true; }");
        sb.AppendLine("                return reader.Read<" + fallback.TypeName + ">();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Reads the next JSON token or finishes the document with a compiled profile.</summary>");
        sb.AppendLine("            /// <param name=\"reader\">The JSON reader.</param>");
        sb.AppendLine("            /// <param name=\"complete\">Whether the complete document must now be exhausted.</param>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"TOptions\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <returns>Whether a token was read, or true after a successful end-of-document check.</returns>");
        sb.AppendLine("            internal static bool LuminPackJsonReadBoundary<T, TOptions>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            {");
        sb.AppendLine("                if (typeof(TOptions) == typeof(" + Declared + ")) return LuminPackJsonReadBoundary<T>(ref reader, complete);");
        sb.AppendLine("                if (complete)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.EnsureEndOfDocument<TOptions>();");
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("                return reader.Read<TOptions>();");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (inputPolicies)
        {
            sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine("            internal static int LuminPackInputLimit<T>() => " +
                (fallback.Input.Enabled ? SafeFacadeProjection.Limit(fallback) : int.MaxValue) + ";");
        }
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
        sb.AppendLine("            /// <typeparam name=\"TLuminProfile\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <param name=\"" + operation.Parameter + "\">The operation state.</param>");
        sb.AppendLine("            /// <param name=\"value\">The payload storage.</param>");
        sb.AppendLine("            /// <remarks>Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion.</remarks>");
        sb.AppendLine("            [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static void " + operation.Name + "<T, TLuminProfile>" + signature);
        sb.AppendLine("                " + ProfileConstraint);
        sb.AppendLine("            {");
        sb.AppendLine("                " + operation.Name + "Registered<T, TLuminProfile>" + call + ";");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            /// <summary>Invokes the explicit registration of a generic payload.</summary>");
        sb.AppendLine("            /// <typeparam name=\"T\">The payload type.</typeparam>");
        sb.AppendLine("            /// <typeparam name=\"TLuminProfile\">The stateless profile.</typeparam>");
        sb.AppendLine("            /// <param name=\"" + operation.Parameter + "\">The operation state.</param>");
        sb.AppendLine("            /// <param name=\"value\">The payload storage.</param>");
        sb.AppendLine("            /// <remarks>Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion.</remarks>");
        sb.AppendLine("            [MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine("            private static void " + operation.Name + "Registered<T, TLuminProfile>" + signature);
        sb.AppendLine("                " + ProfileConstraint);
        sb.AppendLine("            {");
        if (!registry)
        {
            sb.AppendLine("                " + MissingRegistration);
        }
        else
        {
            sb.AppendLine("                global::LuminPack.Code.LuminPackFormatterRegistry<" + fallbackProfile + ">." + (operation.Binary ? "TryGetCachedValue" : "TryGetCachedJsonValue") + "<T>(out var registration);");
            sb.AppendLine("                if (registration." + operation.Callback + " == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    " + MissingRegistration);
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
            string callback = "global::LuminPack.Code.LuminPack" + (operation.Binary ? "" : "Json") +
                (operation.CallbackKeyword == "in" ? "Write" : "Read") + "Delegate<T>";
            sb.AppendLine("                ((" + callback + ")registration." + operation.Callback + ")(ref " + operation.Parameter + ", " + operation.CallbackKeyword + " value);");
        }
        sb.AppendLine("            }");
        sb.AppendLine();
    }

    private const string MissingRegistration = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowMessage(\"No formatter is registered for type '\" + typeof(T) + \"' and the requested operation. Call LuminPackSerializer.Register before using generic entry points.\");";

    /// <summary>Describes one generic operation of the dispatch: its state and registered operation callback.</summary>
    private sealed class DispatchOperation
    {
        /// <summary>Initializes a new instance of <c>DispatchOperation</c>.</summary>
        /// <param name="name">The operation name on the Serializer.</param>
        /// <param name="state">The operation state type name.</param>
        /// <param name="parameter">The state parameter name.</param>
        /// <param name="keyword">The payload reference keyword of the generic entry.</param>
        /// <param name="binary">Whether this is a binary operation.</param>
        /// <param name="callback">The registry entry member for this operation.</param>
        /// <param name="callbackKeyword">The payload reference keyword of the registered delegate.</param>
        /// <param name="optionalCallback">Whether a registration may omit this operation.</param>
        internal DispatchOperation(string name, string state, string parameter, string keyword, bool binary, string callback, string callbackKeyword, bool optionalCallback)
        {
            Name = name;
            State = state;
            Parameter = parameter;
            Keyword = keyword;
            Binary = binary;
            Callback = callback;
            CallbackKeyword = callbackKeyword;
            OptionalCallback = optionalCallback;
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
        internal string Callback { get; }
        /// <summary>Gets the payload reference keyword of the registered delegate.</summary>
        internal string CallbackKeyword { get; }
        /// <summary>Gets whether a registration may omit this operation.</summary>
        internal bool OptionalCallback { get; }
    }

}
