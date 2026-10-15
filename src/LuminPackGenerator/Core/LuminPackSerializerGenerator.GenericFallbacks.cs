using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{

    /// <summary>Builds the generic fallbacks source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void AppendGenericFallbacks(StringBuilder sb, MetaInfo metaInfo)
    {
        // The manual-registration API uses function pointers (delegate*), which require an unsafe
        // context.  When the project does not enable AllowUnsafeBlocks (or opts out via
        // [LuminPackGeneratorOptions(register: Disabled)]), skip it entirely so the generated
        // code compiles without unsafe.
        if (metaInfo.AllowUnsafe && metaInfo.RegisterMode != LuminPackRegisterMode.Disabled)
        {
            AppendRegister(sb);
        }
        AppendGenericSerialize(sb);
        AppendGenericSerializeBufferWriter(sb);
        AppendGenericSerializeJson(sb);
        AppendGenericSerializeJsonBufferWriter(sb);
        AppendGenericSizeof(sb);
        AppendGenericDeserializeSpan(sb);
        AppendGenericDeserializeSpanRef(sb);
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
    }
}
