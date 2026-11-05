using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>
/// JSON counterparts of the immutable collection parsers.  The generated code
/// keeps their token loops and pooled-buffer construction, while every element
/// is handled by a generated extension without runtime instances.
/// </summary>
internal static class ImmutableCollectionEmitterHelper
{
    /// <summary>Appends JSON writing statements for immutable collection helper values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseTypeName(fieldData.TypeName);
        if (IsDictionary(baseType))
        {
            AppendDictionarySerialize(fieldData, sb);
            return;
        }

        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("            if (value.IsDefault)");
        }
        else
        {
            sb.AppendLine("            if (value == null)");
        }
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("            if (value.Length == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArrayEnd();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        else if (baseType is not ("global::System.Collections.Immutable.ImmutableQueue" or
                     "global::System.Collections.Immutable.IImmutableQueue" or
                     "global::System.Collections.Immutable.ImmutableStack" or
                     "global::System.Collections.Immutable.IImmutableStack"))
        {
            sb.AppendLine("            if (value.Count == 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArrayEnd();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        ImmutableInterfaceWriteEmitter.AppendElements(fieldData, sb, json: true, dictionary: false);
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for immutable collection helper values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseType = GetBaseTypeName(fieldData.TypeName);
        if (IsDictionary(baseType))
        {
            AppendDictionaryDeserialize(fieldData, sb, baseType);
            return;
        }

        AppendSequenceDeserialize(fieldData, sb, baseType);
    }

    /// <summary>Appends binary reading statements for immutable collection helper values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    private static void AppendSequenceDeserialize(LuminLocalFieldData fieldData, StringBuilder sb, string baseType)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        bool immutableArray = baseType == "global::System.Collections.Immutable.ImmutableArray";
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine(immutableArray ? "                value = default;" : "                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (!reader.Read() || reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.Collections.Immutable." + GetSequenceTarget(baseType) + "<" + elementType + ">.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            const int initialCapacity = 16;");
        sb.AppendLine("            var buffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(initialCapacity);");
        sb.AppendLine("            var count = 0;");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                do");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                        continue;");
        sb.AppendLine("                    if (count >= buffer.Length)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var newBuffer = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(buffer.Length * 2);");
        sb.AppendLine("                        global::System.Array.Copy(buffer, newBuffer, count);");
        sb.AppendLine("                        global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: " + EmitterTypeTraits.GetArrayPoolClearExpression(fieldData, elementType) + ");");
        sb.AppendLine("                        buffer = newBuffer;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    " + elementType + " item = default!;");
        sb.AppendLine("                    reader.ReadValue(ref item);");
        sb.AppendLine("                    buffer[count++] = item;");
        sb.AppendLine("                } while (reader.Read());");
        AppendSequenceConstruction(sb, elementType, baseType, fieldData.CanCreateImmutableSequenceFromSpan, fieldData.CanBuildFrameworkImmutableHashSet, fieldData.CanReuseImmutableBuilder);
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(buffer, clearArray: " + EmitterTypeTraits.GetArrayPoolClearExpression(fieldData, elementType) + ");");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary size evaluation statements for immutable collection helper values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <param name="canCreateFromSpan">Whether the actual sequence factory accepts a complete span.</param>
    /// <param name="canBuildHashSet">Whether framework hashing and the referenced constructor permit complete-buffer construction.</param>
    /// <param name="canReuseBuilder">Whether matched public builder rentals are available.</param>
    private static void AppendSequenceConstruction(StringBuilder sb, string elementType, string baseType, bool canCreateFromSpan, bool canBuildHashSet, bool canReuseBuilder)
    {
        if (baseType == "global::System.Collections.Immutable.ImmutableArray")
        {
            sb.AppendLine("                if (count == 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    value = global::System.Collections.Immutable.ImmutableArray<" + elementType + ">.Empty;");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine("                var finalArray = new " + elementType + "[count];");
            sb.AppendLine("                global::System.Array.Copy(buffer, finalArray, count);");
            sb.AppendLine("#if NET8_0_OR_GREATER");
            sb.AppendLine("                value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(finalArray);");
            sb.AppendLine("#else");
            sb.AppendLine("                value = global::System.Collections.Immutable.ImmutableArray.Create<" + elementType + ">();");
            sb.AppendLine("                ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.Immutable.ImmutableArray<" + elementType + ">, global::LuminPack.Code.ImmutableArrayView<" + elementType + ">>(ref value);");
            sb.AppendLine("                view.array = finalArray;");
            sb.AppendLine("#endif");
            return;
        }

        string target = GetSequenceTarget(baseType);

        sb.AppendLine("                if (count == 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = global::System.Collections.Immutable." + target + "<" + elementType + ">.Empty;");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        if (target is "ImmutableHashSet" or "ImmutableSortedSet")
            sb.AppendLine("                if (count == 1) { value = global::System.Collections.Immutable." + target + ".Create<" + elementType + ">(buffer[0]); return; }");
        if (target == "ImmutableHashSet" && canBuildHashSet)
        {
            sb.AppendLine("                value = global::LuminPack.Utility.LuminTreeStorage.CreateFrameworkImmutableHashSet(buffer, count);");
            return;
        }
        if (target == "ImmutableList")
        {
            // The buffered sequence can build a balanced tree directly instead of inserting and rebalancing every item.
            if (canCreateFromSpan)
            {
                sb.AppendLine("                value = global::System.Collections.Immutable.ImmutableList.Create(new global::System.ReadOnlySpan<" + elementType + ">(buffer, 0, count));");
                return;
            }
            sb.AppendLine("                if (count == 1) value = global::System.Collections.Immutable.ImmutableList.Create<" + elementType + ">(buffer[0]);");
            sb.AppendLine("                else if (count <= 3)");
            sb.AppendLine("                {");
            ImmutableBuilderEmitter.Begin(sb, canReuseBuilder, "List", elementType, "                    ");
            sb.AppendLine("                    for (int i = 0; i < count; i++) builder.Add(buffer[i]);");
            sb.AppendLine("                    value = builder.ToImmutable();");
            ImmutableBuilderEmitter.End(sb, canReuseBuilder, "List", "                    ");
            sb.AppendLine("                }");
            sb.AppendLine("                else value = global::System.Collections.Immutable.ImmutableList.CreateRange(" + CollectionEmitterKit.CopyingFactorySource(elementType, "buffer", "count") + ");");
            return;
        }
        if (target == "ImmutableStack")
        {
            sb.AppendLine("                var stack = global::System.Collections.Immutable.ImmutableStack<" + elementType + ">.Empty;");
            sb.AppendLine("                for (int i = count - 1; i >= 0; i--) stack = stack.Push(buffer[i]);");
            sb.AppendLine("                value = stack;");
            return;
        }
        if (target == "ImmutableQueue")
        {
            sb.AppendLine(canCreateFromSpan
                ? "                value = global::System.Collections.Immutable.ImmutableQueue.Create(new global::System.ReadOnlySpan<" + elementType + ">(buffer, 0, count));"
                : "                value = global::System.Collections.Immutable.ImmutableQueue.CreateRange(" + CollectionEmitterKit.CopyingFactorySource(elementType, "buffer", "count") + ");");
            return;
        }
        ImmutableBuilderEmitter.Begin(sb, canReuseBuilder, target.Substring("Immutable".Length), elementType, "                ");
        sb.AppendLine("                for (int i = 0; i < count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    builder.Add(buffer[i]);");
        sb.AppendLine("                }");
        sb.AppendLine("                value = builder.ToImmutable();");
        ImmutableBuilderEmitter.End(sb, canReuseBuilder, target.Substring("Immutable".Length), "                ");
    }

    /// <summary>Gets the concrete immutable sequence family used by the existing result construction.</summary>
    /// <param name="baseType">The concrete or interface declaration.</param>
    /// <returns>The factory and empty-result family name.</returns>
    private static string GetSequenceTarget(string baseType) => baseType switch
    {
        "global::System.Collections.Immutable.ImmutableArray" => "ImmutableArray",
        "global::System.Collections.Immutable.ImmutableList" or "global::System.Collections.Immutable.IImmutableList" => "ImmutableList",
        "global::System.Collections.Immutable.ImmutableHashSet" or "global::System.Collections.Immutable.IImmutableSet" => "ImmutableHashSet",
        "global::System.Collections.Immutable.ImmutableSortedSet" => "ImmutableSortedSet",
        "global::System.Collections.Immutable.ImmutableQueue" or "global::System.Collections.Immutable.IImmutableQueue" => "ImmutableQueue",
        "global::System.Collections.Immutable.ImmutableStack" or "global::System.Collections.Immutable.IImmutableStack" => "ImmutableStack",
        _ => LuminPackExceptionHelper.ThrowInvalidOperationException<string>("Unsupported immutable collection: " + baseType)
    };

    /// <summary>Appends JSON writing statements for immutable dictionary values.</summary>
    /// <param name="fieldData">The concrete or interface dictionary contract.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    private static void AppendDictionarySerialize(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        ImmutableInterfaceWriteEmitter.AppendElements(fieldData, sb, json: true, dictionary: true);
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends binary reading statements for immutable collection helper values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    private static void AppendDictionaryDeserialize(LuminLocalFieldData fieldData, StringBuilder sb, string baseType)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        string target = baseType == "global::System.Collections.Immutable.ImmutableSortedDictionary"
            ? "ImmutableSortedDictionary"
            : "ImmutableDictionary";
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            if (!reader.Read() || reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.Collections.Immutable." + target + "<" + keyType + ", " + valueType + ">.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        ImmutableBuilderEmitter.Begin(sb, fieldData.CanReuseImmutableBuilder, target.Substring("Immutable".Length), keyType + ", " + valueType, "            ");
        sb.AppendLine("            do");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        DictionaryEmitter.AppendSkipNonArrayEntry(sb, "                ");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        DictionaryEmitter.AppendJsonEntryRead(sb, keyType, valueType, fused: false, completeEntry: fieldData.CanReadJsonCompleteEntry);
        sb.AppendLine("                builder.Add(key!, itemValue);");
        sb.AppendLine("            } while (reader.Read());");
        sb.AppendLine("            value = builder.ToImmutable();");
        ImmutableBuilderEmitter.End(sb, fieldData.CanReuseImmutableBuilder, target.Substring("Immutable".Length), "            ");
    }

    /// <summary>Determines whether the supplied value has the dictionary classification.</summary>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsDictionary(string baseType) => baseType is
        "global::System.Collections.Immutable.ImmutableDictionary" or
        "global::System.Collections.Immutable.ImmutableSortedDictionary" or
        "global::System.Collections.Immutable.IImmutableDictionary";

    /// <summary>Obtains the base type name for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetBaseTypeName(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }
}
