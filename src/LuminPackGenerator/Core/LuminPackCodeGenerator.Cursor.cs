using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{
    /// <summary>Identifies compression operations that retain the original shared cursor convention.</summary>
    /// <param name="field">The current inline field contract.</param>
    /// <returns>Whether this field or one of its inline children uses compression.</returns>
    private static bool ContainsCompressedField(LuminDataField field) => field.IsCompress || field.ClassFields.Any(ContainsCompressedField);

    /// <summary>Publishes a cached cursor before an operation that observes writer state.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="stateOffset">The cached root cursor, or null for the original byref convention.</param>
    private static void AppendWriterCursorPublish(StringBuilder sb, string indent, string stateOffset)
    {
        if (stateOffset != null) sb.AppendLine(indent + "writer.GetCurrentSpanOffset() = " + stateOffset + ";");
    }

    /// <summary>Advances a local cursor between state-observing boundaries.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="count">The original byte contribution.</param>
    /// <param name="stateOffset">The cached root cursor, or null for the original byref convention.</param>
    /// <param name="checkBuffer">Whether this is an existing variable-length buffer check.</param>
    private static void AppendWriterCursorAdvance(StringBuilder sb, string indent, string count, string stateOffset, bool checkBuffer = false)
    {
        if (stateOffset == null)
            sb.AppendLine(indent + "writer." + (checkBuffer ? "AdvanceSafe" : "Advance") + "(" + count + ");");
        else if (!checkBuffer)
            sb.AppendLine(indent + stateOffset + " += " + count + ";");
        else
        {
            AppendWriterCursorPublish(sb, indent, stateOffset);
            sb.AppendLine(indent + "writer.AdvanceSafe(" + count + ");");
            sb.AppendLine(indent + stateOffset + " = writer.GetCurrentSpanIndex();");
        }
    }

    /// <summary>Retains the original buffer-check and cursor-publication order at a boundary.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="offset">The position published by the original operation.</param>
    /// <param name="stateOffset">The cached root cursor, or null for the original byref convention.</param>
    private static void AppendWriterCursorFlush(StringBuilder sb, string indent, string offset, string stateOffset)
    {
        AppendWriterCursorPublish(sb, indent, stateOffset);
        sb.AppendLine(indent + "writer.FlushCurrentIndex(" + offset + ");");
        if (stateOffset != null && stateOffset != offset) sb.AppendLine(indent + stateOffset + " = " + offset + ";");
    }
}
