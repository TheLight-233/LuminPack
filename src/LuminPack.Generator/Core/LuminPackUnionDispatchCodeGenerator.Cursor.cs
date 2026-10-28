using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

internal static partial class LuminPackUnionDispatchCodeGenerator
{
    /// <summary>Emits the cursor slot and an outlined bridge to the unchanged original virtual slot.</summary>
    /// <param name="sb">The receiving partial-type source.</param>
    /// <param name="padding">Declaration indentation.</param>
    /// <param name="rootType">The exact non-generic root type.</param>
    /// <param name="suffix">The original canonical slot suffix.</param>
    private static void AppendCursorRoot(StringBuilder sb, string padding, string rootType, string suffix)
    {
        AppendCursorDocumentation(sb, padding, false);
        AppendMethodAttribute(sb, padding);
        AppendHiddenAttribute(sb, padding);
        sb.AppendLine($"{padding}public virtual int __LuminPackUnionSerializeCursor_{suffix}(ref global::LuminPack.Core.LuminPackWriter writer, int initialIndex)");
        sb.AppendLine($"{padding}{{");
        sb.AppendLine($"{padding}    return __LuminPackUnionCursorFallback_{suffix}(this, ref writer, initialIndex);");
        sb.AppendLine($"{padding}}}");
        sb.AppendLine();
        sb.AppendLine($"{padding}/// <summary>Publishes the cursor before calling the original virtual or registered formatter.</summary>");
        sb.AppendLine($"{padding}/// <param name=\"value\">The original runtime case, including an older overriding subtype.</param>");
        sb.AppendLine($"{padding}/// <param name=\"writer\">The caller-owned binary operation.</param>");
        sb.AppendLine($"{padding}/// <param name=\"initialIndex\">The absolute next byte index to publish before invoking caller code.</param>");
        sb.AppendLine($"{padding}/// <returns>The absolute next byte index after the original operation, including any growth.</returns>");
        sb.AppendLine($"{padding}[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        AppendHiddenAttribute(sb, padding);
        sb.AppendLine($"{padding}protected static int __LuminPackUnionCursorFallback_{suffix}({rootType} value, ref global::LuminPack.Core.LuminPackWriter writer, int initialIndex)");
        sb.AppendLine($"{padding}{{");
        sb.AppendLine($"{padding}    writer.CurrentIndex = initialIndex;");
        sb.AppendLine($"{padding}    value.__LuminPackUnionSerialize_{suffix}(ref writer);");
        sb.AppendLine($"{padding}    return writer.GetCurrentSpanIndex();");
        sb.AppendLine($"{padding}}}");
        sb.AppendLine();
    }

    /// <summary>Retains exact-case virtual dispatch and old overriding subtype behavior.</summary>
    /// <param name="sb">The receiving case source.</param>
    /// <param name="padding">Declaration indentation.</param>
    /// <param name="member">The canonical case and its original narrow tag.</param>
    /// <param name="suffix">The root slot identity.</param>
    /// <param name="canSeal">The original case sealing decision.</param>
    /// <param name="fixedCursor">Whether this case can use the shared callback-free fixed body.</param>
    private static void AppendCursorMember(StringBuilder sb, string padding, LuminUnionMemberInfo member, string suffix, bool canSeal, bool fixedCursor)
    {
        string concreteType = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        AppendCursorDocumentation(sb, padding, fixedCursor);
        AppendMethodAttribute(sb, padding);
        AppendHiddenAttribute(sb, padding);
        sb.AppendLine($"{padding}public {(canSeal ? "sealed " : string.Empty)}override int __LuminPackUnionSerializeCursor_{suffix}(ref global::LuminPack.Core.LuminPackWriter writer, int initialIndex)");
        sb.AppendLine($"{padding}{{");
        if (fixedCursor)
        {
            sb.AppendLine($"{padding}    if (((object)this).GetType() != typeof({concreteType}))");
            sb.AppendLine($"{padding}        return __LuminPackUnionCursorFallback_{suffix}(this, ref writer, initialIndex);");
            sb.AppendLine($"{padding}    return global::LuminPack.LuminPackSerializer.SerializeUnionAt(ref writer, this, initialIndex, (byte){member.Id});");
        }
        else
        {
            // Known non-fixed cases retain the exact original virtual operation. A sealed
            // old slot can inline here without a second trip through the root fallback.
            sb.AppendLine($"{padding}    writer.CurrentIndex = initialIndex;");
            sb.AppendLine($"{padding}    __LuminPackUnionSerialize_{suffix}(ref writer);");
            sb.AppendLine($"{padding}    return writer.GetCurrentSpanIndex();");
        }
        sb.AppendLine($"{padding}}}");
        sb.AppendLine();
    }

    /// <summary>Documents the absolute cursor units and conditional publication of the new generated slot.</summary>
    /// <param name="sb">The receiving source.</param>
    /// <param name="padding">Declaration indentation.</param>
    /// <param name="fixedCase">Whether this declaration owns an exact fixed case.</param>
    private static void AppendCursorDocumentation(StringBuilder sb, string padding, bool fixedCase)
    {
        sb.AppendLine(fixedCase
            ? $"{padding}/// <summary>Writes this exact fixed Union case through the shared Serializer body; other subtypes retain original virtual dispatch.</summary>"
            : $"{padding}/// <summary>Publishes a pending cursor and delegates unsupported cases to the original virtual Union slot.</summary>");
        sb.AppendLine($"{padding}/// <param name=\"writer\">The existing caller-owned binary operation.</param>");
        sb.AppendLine($"{padding}/// <param name=\"initialIndex\">The absolute byte index at which the original tag and payload start.</param>");
        sb.AppendLine($"{padding}/// <returns>The absolute next byte index, not the payload byte count.</returns>");
        sb.AppendLine($"{padding}/// <remarks>Fixed cases may leave the cursor unpublished; the caller publishes it before observing boundaries. User-code fallbacks publish before invocation.</remarks>");
    }
}
