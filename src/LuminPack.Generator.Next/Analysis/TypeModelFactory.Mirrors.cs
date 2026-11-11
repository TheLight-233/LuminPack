using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    private sealed partial class Session
    {
        /// <summary>Models the inheritance chain of physical Local storage without flattening field layout.</summary>
        /// <param name="type">The concrete payload.</param>
        /// <param name="identity">The payload's normalized identity.</param>
        /// <param name="issues">Receives unsupported physical storage.</param>
        /// <returns>The base-first declarations, or an empty array for a single-level payload.</returns>
        private static StorageMirrorModel[] CaptureMirrors(INamedTypeSymbol type, TypeIdentity identity, List<string> issues)
        {
            List<INamedTypeSymbol> levels = Levels(type);
            if (levels.Count < 2)
                return Array.Empty<StorageMirrorModel>();
            string root = MirrorName(type, identity);
            string parameters = identity.TypeParameters.Length == 0 ? string.Empty : "<" + string.Join(", ", identity.TypeParameters) + ">";
            string stem = parameters.Length == 0 ? root : root.Substring(0, root.Length - parameters.Length);
            string[] constraints = TypeConstraints(type);
            var mirrors = new StorageMirrorModel[levels.Count];
            string? parent = null;
            for (int index = 0; index < levels.Count; index++)
            {
                INamedTypeSymbol level = levels[index];
                MirrorFields(level, out string[]? fields, out int[]? offsets, out string? layout, includeBase: false);
                if (fields == null)
                {
                    issues.Add("the inherited storage mirror of '" + level.ToDisplayString() + "' cannot be reproduced");
                    return Array.Empty<StorageMirrorModel>();
                }
                string name = index == levels.Count - 1 ? root : stem + "_Base" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + parameters;
                mirrors[index] = new StorageMirrorModel(name, parent, fields, offsets, layout, constraints);
                parent = name;
            }
            return mirrors;
        }
    }
}
