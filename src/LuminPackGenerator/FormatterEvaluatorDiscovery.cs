using System;
using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.Formatter;

namespace LuminPack.SourceGenerator;

/// <summary>
/// Evaluator discovery is kept separate so the binary/JSON formatter table remains the
/// four-entry table used by formatter generation.
/// </summary>
public static class FormatterEvaluatorDiscovery
{
    public static Action<LuminLocalFieldData, StringBuilder> GetFormatter(string typeName)
    {
        if (FormatterDiscovery.KnownValueTypes.Contains(typeName))
        {
            return UnmanagedFormatter.GenerateCalculateOffsetCode;
        }

        return typeName is "string" or "global::System.String"
            ? StringFormatter.GenerateCalculateOffsetCode
            : null;
    }
}
