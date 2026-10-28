using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides interface enumerable emitter functionality.</summary>
public static class InterfaceEnumerableEmitter
{
    /// <summary>Appends binary writing statements for interface enumerable values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendCollectionSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface enumerable values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendArrayDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface collection emitter functionality.</summary>
public static class InterfaceCollectionEmitter
{
    /// <summary>Appends binary writing statements for interface collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendCollectionSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendArrayDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface read only collection emitter functionality.</summary>
public static class InterfaceReadOnlyCollectionEmitter
{
    /// <summary>Appends binary writing statements for interface read only collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendCollectionSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface read only collection values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendArrayDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface list emitter functionality.</summary>
public static class InterfaceListEmitter
{
    /// <summary>Appends binary writing statements for interface list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendCollectionSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendKnownReferenceReuse(fieldData, sb);
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            var items = reader.CreateFreshListArray<" + elementType + ">(length, out var list);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "items.AsSpan(0, length)", "            ");
        sb.AppendLine("            value = list;");
    }
}

/// <summary>Provides interface read only list emitter functionality.</summary>
public static class InterfaceReadOnlyListEmitter
{
    /// <summary>Appends binary writing statements for interface read only list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendCollectionSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface read only list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendArrayDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface dictionary emitter functionality.</summary>
public static class InterfaceDictionaryEmitter
{
    /// <summary>Appends binary writing statements for interface dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendDictionarySerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendDictionaryDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface read only dictionary emitter functionality.</summary>
public static class InterfaceReadOnlyDictionaryEmitter
{
    /// <summary>Appends binary writing statements for interface read only dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendDictionarySerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface read only dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendDictionaryDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface lookup emitter functionality.</summary>
public static class InterfaceLookupEmitter
{
    /// <summary>Appends binary writing statements for interface lookup values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                writer.WriteValue(item.Key);");
        sb.AppendLine($"                global::System.Collections.Generic.IEnumerable<{elementType}> elements = item;");
        sb.AppendLine("                writer.WriteValue(in elements);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for interface lookup values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine($"            var dict = new global::System.Collections.Generic.Dictionary<{keyType}, global::System.Linq.IGrouping<{keyType}, {elementType}>>();");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {keyType} key = default!;");
        sb.AppendLine($"                global::System.Collections.Generic.IEnumerable<{elementType}> values = default!;");
        sb.AppendLine("                reader.ReadValue(ref key);");
        sb.AppendLine("                reader.ReadValue(ref values);");
        sb.AppendLine("                if (key != null && values != null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var grouping = new global::LuminPack.Utility.LuminPackGrouping<{keyType}, {elementType}>(key, values);");
        sb.AppendLine("                    dict.Add(key, grouping);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine($"            value = new global::LuminPack.Utility.LuminPackLookup<{keyType}, {elementType}>(dict);");
    }

    /// <summary>Appends JSON writing statements for interface lookup values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count == 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteArrayEnd();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Linq.IGrouping<" + keyType + ", " + elementType + "> grouping = item;");
        sb.AppendLine("                writer.WriteValue(in grouping);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for interface lookup values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            var dict = new global::System.Collections.Generic.Dictionary<" + keyType + ", global::System.Linq.IGrouping<" + keyType + ", " + elementType + ">>();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                global::System.Linq.IGrouping<" + keyType + ", " + elementType + "> grouping = default!;");
        sb.AppendLine("                reader.ReadValue(ref grouping);");
        sb.AppendLine("                dict.Add(grouping.Key, grouping);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::LuminPack.Utility.LuminPackLookup<" + keyType + ", " + elementType + ">(dict);");
    }
}

/// <summary>Provides interface grouping emitter functionality.</summary>
public static class InterfaceGroupingEmitter
{
    /// <summary>Appends binary writing statements for interface grouping values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteValue(value.Key);");
        sb.AppendLine($"            global::System.Collections.Generic.IEnumerable<{elementType}> elements = value;");
        sb.AppendLine("            writer.WriteValue(in elements);");
    }

    /// <summary>Appends binary reading statements for interface grouping values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);

        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            {keyType} key = default!;");
        sb.AppendLine($"            global::System.Collections.Generic.IEnumerable<{elementType}> values = default!;");
        sb.AppendLine("            reader.ReadValue(ref key);");
        sb.AppendLine("            reader.ReadValue(ref values);");
        sb.AppendLine();
        if (fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol grouping &&
            !grouping.TypeArguments[0].IsValueType)
        {
            sb.AppendLine("            if (key is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(key));");
        }
        sb.AppendLine("            if (values is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(values));");
        sb.AppendLine();
        sb.AppendLine($"            value = new global::LuminPack.Utility.LuminPackGrouping<{keyType}, {elementType}>(key, values);");
    }

    /// <summary>Appends statements that calculate the binary payload size.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + keyType + " key = value.Key;");
        sb.AppendLine("            evaluator.CalculateOffset(ref key);");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = value;");
        sb.AppendLine("            evaluator.CalculateOffset(ref elements);");
    }

    /// <summary>Appends JSON writing statements for interface grouping values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteObjectStart();");
        sb.AppendLine("            writer.WritePropertyName(\"Key\");");
        sb.AppendLine("            var key = value.Key;");
        sb.AppendLine("            writer.WriteValue(in key);");
        sb.AppendLine("            writer.WritePropertyName(\"Elements\");");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = value;");
        sb.AppendLine("            writer.WriteValue(in elements);");
        sb.AppendLine("            writer.WriteObjectEnd();");
    }

    /// <summary>Appends JSON reading statements for interface grouping values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var elementType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            " + keyType + " key = default!;");
        sb.AppendLine("            global::System.Collections.Generic.IEnumerable<" + elementType + "> elements = default!;");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                {");
        sb.AppendLine("                    string propertyName = reader.ReadString();");
        sb.AppendLine("                    reader.Read();");
        sb.AppendLine("                    if (propertyName == \"Key\")");
        sb.AppendLine("                        reader.ReadValue(ref key);");
        sb.AppendLine("                    else if (propertyName == \"Elements\")");
        sb.AppendLine("                        reader.ReadValue(ref elements);");
        sb.AppendLine("                    else");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::LuminPack.Utility.LuminPackGrouping<" + keyType + ", " + elementType + ">(key!, elements!);");
    }
}

/// <summary>Provides interface set emitter functionality.</summary>
public static class InterfaceSetEmitter
{
    /// <summary>Appends binary writing statements for interface set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendSetSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendSetDeserialize(fieldData, sb);
    }
}

/// <summary>Provides interface read only set emitter functionality.</summary>
public static class InterfaceReadOnlySetEmitter
{
    /// <summary>Appends binary writing statements for interface read only set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendSetSerialize(fieldData, sb);
    }

    /// <summary>Appends binary reading statements for interface read only set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        InterfaceCollectionEmitterHelper.AppendSetDeserialize(fieldData, sb);
    }
}

/// <summary>
/// Shared statements for interface-typed collections. Writes test the exact runtime type of the
/// common BCL implementation first, so its contiguous storage or struct enumerator is used without
/// interface dispatch or an enumerator allocation; every other implementation keeps its enumeration.
/// </summary>
internal static class InterfaceCollectionEmitterHelper
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";

    /// <summary>Gets a type name accepted by <c>typeof</c>, without nullable reference annotations.</summary>
    /// <param name="type">The type symbol, when available.</param>
    /// <param name="fallback">The generated type name.</param>
    /// <returns>The source spelling of the runtime type.</returns>
    internal static string RuntimeName(ITypeSymbol type, string fallback)
        => type != null ? LuminPack.Code.Core.FormatterTypeName.Get(type) : fallback.TrimEnd('?');

    /// <summary>Gets the generic arguments of the collection type.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="index">The zero-based argument index.</param>
    /// <returns>The argument symbol, or null when unavailable.</returns>
    private static ITypeSymbol Argument(LuminLocalFieldData fieldData, int index)
        => fieldData.TypeSymbol is INamedTypeSymbol { TypeArguments.Length: > 0 } named && index < named.TypeArguments.Length
            ? named.TypeArguments[index]
            : null;

    /// <summary>Appends binary writing statements for a sequence interface value.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendCollectionSerialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var elementType = GetFirstGeneric(fieldData.TypeName);
        string exact = RuntimeName(element, elementType);
        string list = "global::System.Collections.Generic.List<" + elementType + ">";
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        sb.AppendLine("            if (value.GetType() == typeof(global::System.Collections.Generic.List<" + exact + ">))");
        sb.AppendLine("            {");
        CollectionEmitterKit.AppendSpanWrite(sb, element, elementType,
            "global::LuminPack.Code.LuminPackMarshal.GetListSpan(" + Unsafe + ".As<" + list + ">(value))", "                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (value.GetType() == typeof(" + exact + "[]))");
        sb.AppendLine("            {");
        CollectionEmitterKit.AppendSpanWrite(sb, element, elementType, Unsafe + ".As<" + elementType + "[]>(value)", "                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", CollectionEmitterKit.ContiguousElementWrite(element, elementType, "v") }, "            ", element);
    }

    /// <summary>Appends binary reading statements that materialize a sequence interface as an array.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendArrayDeserialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        AppendKnownReferenceReuse(fieldData, sb);
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var elementType = GetFirstGeneric(fieldData.TypeName);
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ");
        sb.AppendLine("            if (length == 0) { value = global::System.Array.Empty<" + elementType + ">(); return; }");
        sb.AppendLine("            var array = global::LuminPack.Code.LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(length);");
        CollectionEmitterKit.AppendSpanRead(sb, element, elementType, "array.AsSpan()", "            ");
        sb.AppendLine("            value = array;");
    }

    /// <summary>Selects exact mutable targets for supported reference and raw scalar element reuse.</summary>
    /// <param name="fieldData">The original sequence interface contract.</param>
    /// <param name="sb">The generated statements.</param>
    internal static void AppendKnownReferenceReuse(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        if (element?.SpecialType != SpecialType.System_String && !EmitterTypeTraits.UsesOrdinaryObjectHeader(element) &&
            !EmitterTypeTraits.IsKnownScalar(element)) return;
        string elementType = GetFirstGeneric(fieldData.TypeName);
        string exact = RuntimeName(element, elementType);
        string list = "global::System.Collections.Generic.List<" + elementType + ">";
        string array = elementType + "[]";
        foreach (bool isList in new[] { true, false })
        {
            string concrete = isList ? list : array;
            string runtime = isList ? "global::System.Collections.Generic.List<" + exact + ">" : exact + "[]";
            string helper = isList ? "ReadKnownList" : "ReadKnownArray";
            sb.AppendLine("            if (value is not null && value.GetType() == typeof(" + runtime + "))");
            sb.AppendLine("            {");
            sb.AppendLine("                var selected = " + Unsafe + ".As<" + concrete + ">(value);");
            sb.AppendLine("                " + helper + "(ref reader, ref selected);");
            sb.AppendLine("                value = selected;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine("            static void " + helper + "(ref global::LuminPack.Core.LuminPackReader reader, ref " + concrete + "? value)");
            sb.AppendLine("            {");
            var field = new LuminLocalFieldData
            {
                TypeName = concrete, TypeSymbol = fieldData.TypeSymbol, Name = fieldData.Name,
                DeclaringTypeName = fieldData.DeclaringTypeName, IsValue = false
            };
            if (isList) ListEmitter.GenerateDeserializeCode(field, sb);
            else ArrayEmitter.GenerateDeserializeCode(field, sb);
            sb.AppendLine("            }");
        }
    }

    /// <summary>Appends binary writing statements for a dictionary interface value.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendDictionarySerialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);
        string dictionary = "global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">";
        string exact = "global::System.Collections.Generic.Dictionary<" + RuntimeName(Argument(fieldData, 0), keyType) + ", " + RuntimeName(Argument(fieldData, 1), valueType) + ">";
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        AppendDelegatedWrite(sb, fieldData, exact, dictionary, DictionaryEmitter.GenerateSerializeCode, "WriteDictionary");
        // Every implementation shares the record of the exact Dictionary: raw entry storage when both members are raw, otherwise
        // each member through its own formatter, which is also how the reader below decodes it.
        var entry = new System.Collections.Generic.List<string>();
        if (DictionaryEmitter.UsesRawPair(fieldData.TypeSymbol))
        {
            var raw = new StringBuilder();
            DictionaryEmitter.AppendRawPair(raw, "item.Key", "item.Value", valueType, "", write: true);
            foreach (var line in raw.ToString().Split('\n'))
                if (line.Trim().Length != 0) entry.Add(line.Trim());
        }
        else
        {
            entry.Add("writer.WriteValue(item.Key);");
            entry.Add("writer.WriteValue(item.Value);");
        }
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", entry.ToArray(), "            ");
        AppendDelegatedBody(sb, fieldData, dictionary, DictionaryEmitter.GenerateSerializeCode, "WriteDictionary");
    }

    /// <summary>Forwards an exact runtime implementation to the established write of its concrete type.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="fieldData">The interface field; its type arguments describe the concrete type.</param>
    /// <param name="exact">The <c>typeof</c> spelling of the concrete type.</param>
    /// <param name="concrete">The concrete type name.</param>
    /// <param name="emitter">The concrete type's binary write emitter.</param>
    /// <param name="function">The local function receiving the forwarded value.</param>
    private static void AppendDelegatedWrite(StringBuilder sb, LuminLocalFieldData fieldData, string exact, string concrete, System.Action<LuminLocalFieldData, StringBuilder> emitter, string function)
    {
        sb.AppendLine("            if (value.GetType() == typeof(" + exact + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                " + function + "(ref writer, " + Unsafe + ".As<" + concrete + ">(value));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
    }

    /// <summary>Declares the local function that runs the concrete type's established write.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="fieldData">The interface field; its type arguments describe the concrete type.</param>
    /// <param name="concrete">The concrete type name.</param>
    /// <param name="emitter">The concrete type's binary write emitter.</param>
    /// <param name="function">The local function name.</param>
    private static void AppendDelegatedBody(StringBuilder sb, LuminLocalFieldData fieldData, string concrete, System.Action<LuminLocalFieldData, StringBuilder> emitter, string function)
    {
        var concreteField = new LuminLocalFieldData
        {
            TypeName = concrete,
            TypeSymbol = fieldData.TypeSymbol,
            CanGroupSmallHashSetWrites = fieldData.CanGroupSmallHashSetWrites,
            Name = fieldData.Name,
            DeclaringTypeName = fieldData.DeclaringTypeName,
            IsValue = false
        };
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            static void " + function + "(ref global::LuminPack.Core.LuminPackWriter writer, " + concrete + " value)");
        sb.AppendLine("            {");
        emitter(concreteField, sb);
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements with exact reference-value dictionary reuse.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendDictionaryDeserialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        // Unknown implementations retain fresh materialization. Eligible exact targets use the existing concrete reader.
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);
        string dictionary = "global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">";
        var concreteField = new LuminLocalFieldData
        {
            TypeName = dictionary,
            TypeSymbol = fieldData.TypeSymbol,
            Name = fieldData.Name,
            DeclaringTypeName = fieldData.DeclaringTypeName,
            IsValue = false
        };
        var element = Argument(fieldData, 1);
        bool reuse = element?.SpecialType == SpecialType.System_String || EmitterTypeTraits.UsesOrdinaryObjectHeader(element);
        string selected = "null";
        if (reuse)
        {
            string exact = "global::System.Collections.Generic.Dictionary<" + RuntimeName(Argument(fieldData, 0), keyType) + ", " + RuntimeName(element, valueType) + ">";
            selected = "value is not null && value.GetType() == typeof(" + exact + ") ? " + Unsafe + ".As<" + dictionary + ">(value) : null";
        }
        sb.AppendLine("            " + dictionary + "? dict = " + selected + ";");
        sb.AppendLine("            ReadDictionary(ref reader, ref dict);");
        sb.AppendLine("            value = dict;");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            static void ReadDictionary(ref global::LuminPack.Core.LuminPackReader reader, ref " + dictionary + "? value)");
        sb.AppendLine("            {");
        DictionaryEmitter.GenerateDeserializeCode(concreteField, sb);
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary writing statements for a set interface value.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendSetSerialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var elementType = GetFirstGeneric(fieldData.TypeName);
        string set = "global::System.Collections.Generic.HashSet<" + elementType + ">";
        sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
        AppendDelegatedWrite(sb, fieldData, "global::System.Collections.Generic.HashSet<" + RuntimeName(element, elementType) + ">", set, HashSetEmitter.GenerateSerializeCode, "WriteSet");
        CollectionEmitterKit.AppendCountedWrite(sb, "var item", "value", new[] { "var v = item;", "writer.WriteValue(in v);" }, "            ", element);
        AppendDelegatedBody(sb, fieldData, set, HashSetEmitter.GenerateSerializeCode, "WriteSet");
    }

    /// <summary>Materializes a set interface through the existing HashSet entry and bucket construction.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendSetDeserialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            int length = reader.ReadLength(" + CollectionEmitterKit.MinimumElementSize(element, elementType, scalarElements: true) + ");");
        sb.AppendLine("            if (length < 0) { value = null; return; }");
        sb.AppendLine("            if (length == 0) { value = new global::System.Collections.Generic.HashSet<" + elementType + ">(0); return; }");
        sb.AppendLine("            var set = new global::System.Collections.Generic.HashSet<" + elementType + ">(length);");
        sb.AppendLine("            {");
        sb.AppendLine("                var setView = global::LuminPack.Code.LuminPackMarshal.GetHashSetView(set);");
        sb.AppendLine("                ref var entries = ref global::LuminPack.Code.LuminPackMarshal.GetArrayReference(setView._entries);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        if (CollectionEmitterKit.IsRawScalar(element, elementType))
        {
            sb.AppendLine("                    ref var entry = ref " + Unsafe + ".Add(ref entries, (nint)(uint)i);");
            sb.AppendLine("                    reader.ReadValue(ref entry.Value!);");
        }
        else
        {
            sb.AppendLine("                    " + elementType + " item = default!;");
            sb.AppendLine("                    reader.ReadValue(ref item);");
            sb.AppendLine("                    " + Unsafe + ".Add(ref entries, (nint)(uint)i).Value = item;");
        }
        sb.AppendLine("                }");
        sb.AppendLine("                global::LuminPack.Code.LuminPackMarshal.RebuildHashSetBuckets(setView, length);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = set;");
    }
}
