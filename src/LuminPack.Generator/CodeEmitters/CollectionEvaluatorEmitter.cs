using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>
/// Shared size evaluators for closed collection types. These preserve the old parser
/// behavior while dispatching each element through the source-generated formatter cache.
/// </summary>
public static class CollectionEvaluatorEmitter
{
    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateArrayCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = fieldData.TypeName.Substring(0, fieldData.TypeName.LastIndexOf("[]", System.StringComparison.Ordinal));
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");

        ITypeSymbol elementSymbol = fieldData.TypeSymbol is IArrayTypeSymbol array
            ? array.ElementType
            : null;
        var portable = PortableStructPlan.TryCreate(elementSymbol);
        if (portable != null)
        {
            sb.AppendLine("            evaluator += checked(4 + " + PortableStructEmitter.PayloadSize(portable, "value.Length") + ");");
            return;
        }
        if (EmitterTypeTraits.TryGetIsUnmanaged(elementSymbol, out bool isUnmanaged))
        {
            if (isUnmanaged)
            {
                AppendUnmanagedArrayOffset(sb, elementType);
            }
            else
            {
                AppendManagedArrayOffset(sb);
            }
            return;
        }

        sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
        sb.AppendLine("            {");
        AppendUnmanagedArrayOffset(sb, elementType, "    ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        AppendManagedArrayOffset(sb);
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="indentSuffix">The indent suffix used by this operation.</param>
    private static void AppendUnmanagedArrayOffset(StringBuilder sb, string elementType, string indentSuffix = "")
    {
        sb.AppendLine("            " + indentSuffix + "evaluator += checked(sizeof(int) + value.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendManagedArrayOffset(StringBuilder sb)
    {
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator.CalculateOffset(ref item);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateEnumerableCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        string family = fieldData.TypeName.Substring(0, fieldData.TypeName.IndexOf('<'));
        bool rawElements = family is "global::System.Collections.Generic.List" or "global::System.Collections.Generic.Stack" or
            "global::System.Collections.Generic.SortedSet" or "global::System.Collections.Concurrent.ConcurrentBag" or
            "global::System.Collections.Concurrent.ConcurrentStack" or "global::System.Collections.ObjectModel.Collection" or
            "global::System.Collections.ObjectModel.ObservableCollection" or "global::System.Collections.ObjectModel.ReadOnlyCollection" or
            "global::System.Collections.ObjectModel.ReadOnlyObservableCollection" or "global::System.Runtime.CompilerServices.ReadOnlyCollectionBuilder" or
            "global::System.Collections.Generic.IEnumerable" or "global::System.Collections.Generic.ICollection" or
            "global::System.Collections.Generic.IReadOnlyCollection" or "global::System.Collections.Generic.IList" or "global::System.Collections.Generic.IReadOnlyList";
        sb.AppendLine("            evaluator += sizeof(int);");
        if (family is "global::System.Collections.Generic.HashSet" or "global::System.Collections.Generic.LinkedList" or
            "global::System.Collections.Immutable.ImmutableHashSet" or
            "global::System.Collections.Immutable.ImmutableSortedSet" &&
            CodeEmitterRegistry.KnownValueTypes.Contains(elementType) &&
            CollectionEmitterKit.IsRawScalar(EmitterTypeTraits.GetFirstTypeArgument(fieldData), elementType))
        {
            sb.AppendLine("            evaluator += checked(value.Count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
            return;
        }
        if (rawElements && family != "global::System.Collections.Generic.IEnumerable" &&
            AppendContiguousPayloadSize(fieldData, sb, "value.Count", allowTag: family == "global::System.Collections.Generic.List")) return;
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        if (!rawElements || !AppendContiguousPayloadSize(fieldData, sb, "1"))
            sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateValueEnumerableCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            evaluator += sizeof(int);");
        sb.AppendLine("            if (value.IsDefault) return;");
        if (AppendContiguousPayloadSize(fieldData, sb, "value.Length")) return;
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDictionaryCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        string valueType = CodeEmitterRegistry.GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            evaluator += sizeof(int);");
        string family = fieldData.TypeName.Substring(0, fieldData.TypeName.IndexOf('<'));
        if (family is "global::System.Collections.Generic.Dictionary" or "global::System.Collections.Generic.IDictionary" or
                "global::System.Collections.Generic.IReadOnlyDictionary" && DictionaryEmitter.UsesRawPair(fieldData.TypeSymbol))
        {
            // The writer stores each entry as the raw storage of its key and value, whatever framing their formatters add.
            sb.AppendLine("            evaluator += checked(value.Count * (global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + keyType +
                ">() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + valueType + ">()));");
            return;
        }
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " key = item.Key;");
        sb.AppendLine("                evaluator.CalculateOffset(ref key);");
        sb.AppendLine("                " + valueType + " itemValue = item.Value;");
        sb.AppendLine("                evaluator.CalculateOffset(ref itemValue);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary size evaluation statements for collection evaluator values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateQueueCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null || value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += sizeof(int);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            evaluator += sizeof(int) * 4;");
        if (AppendContiguousPayloadSize(fieldData, sb, "value.Count")) return;
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + elementType + " current = item;");
        sb.AppendLine("                evaluator.CalculateOffset(ref current);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends the complete payload width selected by a contiguous collection writer.</summary>
    /// <param name="fieldData">The collection metadata.</param>
    /// <param name="sb">The emitted statements.</param>
    /// <param name="count">The logical element count.</param>
    /// <param name="allowTag">Whether the family's writer emits one layout-tagged payload instead of packed single values.</param>
    /// <returns>Whether the element contract has a complete fixed payload width.</returns>
    internal static bool AppendContiguousPayloadSize(LuminLocalFieldData fieldData, StringBuilder sb, string count, bool allowTag = false)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        var portable = allowTag ? PortableStructPlan.TryCreate(element) : PortableStructPlan.TryCreateContiguous(element);
        if (portable != null)
        {
            // Only families whose writer emits one tagged payload may evaluate it; every other family keeps the packed width.
            sb.AppendLine("            evaluator += checked(" + PortableStructEmitter.PayloadSize(portable, count) + ");");
            return true;
        }
        if (PortableStructPlan.TryCreate(element) != null) return false;
        if (!CollectionEmitterKit.IsRaw(element, elementType)) return false;
        sb.AppendLine("            evaluator += checked(" + count + " * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
        return true;
    }
}
