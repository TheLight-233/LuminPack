using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Builds the generic fallbacks source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="managedInput">Whether the referenced runtime advances a tracked managed reference, so a root entry may bind
    /// a state directly to a managed array or span without pinning it.</param>
    private static void AppendGenericFallbacks(StringBuilder sb, MetaInfo metaInfo, bool managedInput)
    {
        // The manual-registration API uses function pointers (delegate*), which require an unsafe
        // context.  When the project does not enable AllowUnsafeBlocks (or opts out via
        // [LuminPackGeneratorOptions(register: Disabled)]), skip it entirely so the generated
        // code compiles without unsafe.
        if (metaInfo.AllowUnsafe && metaInfo.RegisterMode != LuminPackRegisterMode.Disabled)
        {
            AppendRegister(sb);
        }
        AppendGenericSerialize(sb, managedInput);
        AppendSerializeBufferWriter(sb);
        AppendGenericSerializeJson(sb);
        AppendSerializeJsonBufferWriter(sb);
        AppendGenericDeserializeSpan(sb, managedInput);
        AppendGenericDeserializeSpanRef(sb, managedInput);
        AppendGenericDeserializeBuffer(sb);
        AppendGenericDeserializeBufferRef(sb);
        AppendGenericDeserializeSequence(sb);
        AppendGenericDeserializeSequenceRef(sb);
        AppendGenericDeserializeJsonString(sb);
        AppendGenericDeserializeJsonSpanByte(sb);
        AppendGenericDeserializeJsonSpanChar(sb);
        AppendGenericDeserializeJsonBuffer(sb);
        AppendGenericDeserializeAsync(sb);
        AppendGenericDeserializeAsyncValue(sb);
        AppendAdditionalGenericFallbacks(sb);
        AppendStateFallbacks(sb, metaInfo);
    }

    /// <summary>Routes all generic state entry points through one dispatch body per operation.</summary>
    /// <param name="sb">The shared Serializer source builder.</param>
    /// <param name="metaInfo">The consumer's reference-safety settings.</param>
    private static void AppendStateFallbacks(StringBuilder sb, MetaInfo metaInfo)
    {
        foreach (var operation in new[] { "Serialize", "Deserialize", "Sizeof", "SerializeJson", "DeserializeJson" })
        {
            bool write = operation.StartsWith("Serialize", System.StringComparison.Ordinal);
            string state = operation == "Sizeof" ? "Evaluator" : operation.EndsWith("Json", System.StringComparison.Ordinal)
                ? write ? "JsonWriter" : "JsonReader" : write ? "Writer" : "Reader";
            string modifier = write ? "in" : "ref";
            sb.AppendLine("        /// <summary>Processes <typeparamref name=\"T\"/> using the supplied operation state.</summary>");
            sb.AppendLine("        /// <typeparam name=\"T\">The serialized payload type.</typeparam>");
            sb.AppendLine("        /// <param name=\"state\">The existing operation state, including its cursor and reference tracking.</param>");
            sb.AppendLine("        /// <param name=\"value\">The payload storage.</param>");
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            // The exact-width evaluator only serves generated direct records, so its entry stays inside the assembly.
            sb.Append(operation == "Sizeof" ? "        internal static void " : "        public static void ").Append(operation).Append("<T>(ref global::LuminPack.Core.LuminPack").Append(state)
                .Append(" state, ").Append(metaInfo.IsNet8 ? "scoped " : string.Empty).Append(modifier).Append(" T value) => FormatterDispatch.")
                .Append(operation).Append("(ref state, ").Append(modifier).AppendLine(" value);");
        }
    }
}
