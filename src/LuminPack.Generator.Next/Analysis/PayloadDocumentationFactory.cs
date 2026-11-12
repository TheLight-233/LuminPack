using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Captures declaration-only documentation dependencies at the Roslyn analysis boundary.</summary>
internal static class PayloadDocumentationFactory
{
    /// <summary>Freezes links, CLR layout and serialized member types beside the completed value model.</summary>
    /// <param name="symbol">The declared payload.</param>
    /// <param name="model">Its completed serialization contract.</param>
    /// <returns>The immutable documentation facts.</returns>
    internal static PayloadDocumentation Create(INamedTypeSymbol symbol, TypeModel model)
    {
        var declared = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        for (INamedTypeSymbol? level = symbol; level != null; level = level.BaseType)
            foreach (ISymbol member in level.GetMembers())
                if (member is IFieldSymbol or IPropertySymbol && !member.IsStatic && !declared.ContainsKey(member.Name))
                    declared.Add(member.Name, member);
        var types = new string[model.Members.Length];
        var ids = new string[types.Length];
        for (int index = 0; index < types.Length; index++)
        {
            if (declared.TryGetValue(model.Members[index].Name, out ISymbol? member))
            {
                ITypeSymbol type = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;
                types[index] = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                ids[index] = member.GetDocumentationCommentId() ?? string.Empty;
            }
            else
            {
                types[index] = "formatter-defined";
                ids[index] = string.Empty;
            }
        }
        var names = new List<string>();
        var storageTypes = new List<string>();
        var offsets = new List<string>();
        if (model is StructModel { Storage: StructStorage.RawUnmanaged or StructStorage.PortableUnmanaged })
            foreach (ISymbol member in symbol.GetMembers())
                if (member is IFieldSymbol { IsStatic: false } field)
                {
                    names.Add((field.AssociatedSymbol ?? field).Name);
                    storageTypes.Add(field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    string offset = "runtime-defined";
                    foreach (AttributeData attribute in field.GetAttributes())
                        if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute" &&
                            attribute.ConstructorArguments.Length == 1)
                            offset = Convert.ToString(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) ?? offset;
                    offsets.Add(offset);
                }
        return new PayloadDocumentation(model, symbol.GetDocumentationCommentId() ?? string.Empty, Layout(symbol), types, ids,
            names.ToArray(), storageTypes.ToArray(), offsets.ToArray());
    }

    /// <summary>Captures physical layout settings independently of whether the serializer needs a mirror.</summary>
    /// <param name="symbol">The declared payload.</param>
    /// <returns>The CLR layout and explicit pack or size settings.</returns>
    private static string Layout(INamedTypeSymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.InteropServices.StructLayoutAttribute" ||
                attribute.ConstructorArguments.Length == 0)
                continue;
            string kind = Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture) switch
            {
                0 => "Sequential",
                2 => "Explicit",
                3 => "Auto",
                _ => "Runtime-defined",
            };
            var text = new StringBuilder(kind);
            foreach (KeyValuePair<string, TypedConstant> setting in attribute.NamedArguments)
                text.Append("; ").Append(setting.Key).Append(" = ").Append(Convert.ToString(setting.Value.Value, CultureInfo.InvariantCulture));
            return text.ToString();
        }
        return symbol.IsValueType ? "Sequential (CLR default)" : "Auto (CLR default)";
    }
}
