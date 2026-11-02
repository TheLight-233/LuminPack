using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Stores immutable input-boundary constants separately from existing string and formatter profiles.</summary>
internal sealed class CompiledInputPolicy
{
    internal static readonly CompiledInputPolicy Default = new CompiledInputPolicy();
    /// <summary>Gets the policy of write-only profiles, whose identities never depend on input checks.</summary>
    internal static readonly CompiledInputPolicy Trusted = new CompiledInputPolicy { Enabled = false };
    // Untrusted reads are the default. Limits are unbounded until declared: allocations stay proportional to the input,
    // and recursion keeps its stack guard. Frozen unbounded limits fold away in generated checks.
    internal bool Enabled = true;
    // Whether a declaration explicitly selects hostile-input handling; only then do JSON reads switch to the strict grammar.
    internal bool Explicit;
    internal int Input = int.MaxValue;
    internal int Decompressed = 16 * 1024 * 1024;
    internal long Allocation = 64L * 1024 * 1024;
    internal int Collection = int.MaxValue;
    internal int String = int.MaxValue;
    internal int Objects = 100_000;
    internal int Depth = int.MaxValue;
    internal long Work = 32L * 1024 * 1024;
    // Only the limits enforced by generated checks select distinct plans.
    internal string Identity => !Enabled ? "" : "|Safe3:" + Input.ToString(CultureInfo.InvariantCulture) + ":" + Collection.ToString(CultureInfo.InvariantCulture) +
        ":" + String.ToString(CultureInfo.InvariantCulture) + ":" + Depth.ToString(CultureInfo.InvariantCulture) + (Explicit ? ":x" : "");

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
                case "UntrustedData": next.Enabled = (bool)argument.Value.Value; next.Explicit = next.Enabled; break;
                case "MaxInputBytes": next.Input = (int)argument.Value.Value; next.Explicit = true; break;
                case "MaxDecompressedBytes": next.Decompressed = (int)argument.Value.Value; break;
                case "MaxAllocationBytes": next.Allocation = (long)argument.Value.Value; break;
                case "MaxCollectionLength": next.Collection = (int)argument.Value.Value; next.Explicit = true; break;
                case "MaxStringLength": next.String = (int)argument.Value.Value; next.Explicit = true; break;
                case "MaxObjectCount": next.Objects = (int)argument.Value.Value; break;
                case "MaxDepth": next.Depth = (int)argument.Value.Value; next.Explicit = true; break;
                case "MaxReadWork": next.Work = (long)argument.Value.Value; break;
            }
        }
        return next;
    }
}
