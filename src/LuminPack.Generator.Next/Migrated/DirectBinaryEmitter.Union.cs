using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Exposes the Union payload read body to the Union root's case switch arms.</summary>
internal static partial class DirectBinaryEmitter
{
    /// <summary>Builds the statement lines that read a covered fixed case's Union payload.</summary>
    /// <param name="model">The case's covered model, whose members are all fixed.</param>
    /// <param name="w">The file writer that spells helper aliases.</param>
    /// <returns>The body lines, opening with the instance local.</returns>
    internal static List<string> UnionReadBody(DirectTypeModel model, DirectWriter w)
        => AddReadCallbacks(model, ReadBody(model, w, union: true));
}
