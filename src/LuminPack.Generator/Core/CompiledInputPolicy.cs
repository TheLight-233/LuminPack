using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Stores immutable input-boundary constants separately from existing string and formatter profiles.</summary>
internal sealed class CompiledInputPolicy
{
    internal static readonly CompiledInputPolicy Default = new CompiledInputPolicy();
    internal bool Enabled;
    internal int Input = 8 * 1024 * 1024;
    internal int Decompressed = 16 * 1024 * 1024;
    internal long Allocation = 64L * 1024 * 1024;
    internal int Collection = 100_000;
    internal int String = 1024 * 1024;
    internal int Objects = 100_000;
    internal int Depth = 64;
    internal long Work = 32L * 1024 * 1024;
    internal string Identity => !Enabled ? "" : "|Safe2:" + Input + ":" + Decompressed + ":" + Allocation + ":" + Collection + ":" + String + ":" + Objects + ":" + Depth + ":" + Work;
    internal string Arguments => Input + ", " + Decompressed + ", " + Allocation.ToString(CultureInfo.InvariantCulture) + "L, " + Collection + ", " + String + ", " + Objects + ", " + Depth + ", " + Work.ToString(CultureInfo.InvariantCulture) + "L";

    /// <summary>Overlays only explicit input policy constants, retaining inherited limits.</summary>
    /// <param name="attributes">The current declaration attributes.</param>
    /// <returns>The existing policy when unchanged, otherwise an immutable-by-convention copy.</returns>
    internal CompiledInputPolicy Apply(System.Collections.Generic.IEnumerable<AttributeData> attributes)
    {
        var next = this;
        foreach (var attribute in attributes.Where(a => a.AttributeClass?.ToDisplayString() == CompiledOptions.AttributeName))
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key is not ("UntrustedData" or "MaxInputBytes" or "MaxDecompressedBytes" or "MaxAllocationBytes" or
                "MaxCollectionLength" or "MaxStringLength" or "MaxObjectCount" or "MaxDepth" or "MaxReadWork")) continue;
            if (ReferenceEquals(next, this)) next = (CompiledInputPolicy)MemberwiseClone();
            switch (argument.Key)
            {
                case "UntrustedData": next.Enabled = (bool)argument.Value.Value; break;
                case "MaxInputBytes": next.Input = (int)argument.Value.Value; break;
                case "MaxDecompressedBytes": next.Decompressed = (int)argument.Value.Value; break;
                case "MaxAllocationBytes": next.Allocation = (long)argument.Value.Value; break;
                case "MaxCollectionLength": next.Collection = (int)argument.Value.Value; break;
                case "MaxStringLength": next.String = (int)argument.Value.Value; break;
                case "MaxObjectCount": next.Objects = (int)argument.Value.Value; break;
                case "MaxDepth": next.Depth = (int)argument.Value.Value; break;
                case "MaxReadWork": next.Work = (long)argument.Value.Value; break;
            }
        }
        return next;
    }
}
