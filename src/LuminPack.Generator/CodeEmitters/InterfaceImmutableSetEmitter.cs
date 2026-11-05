using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides interface immutable set emitter functionality.</summary>
public static class InterfaceImmutableSetEmitter
{
    /// <summary>Appends binary writing statements for interface immutable set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
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
        ImmutableInterfaceWriteEmitter.AppendElements(fieldData, sb, json: false, dictionary: false);
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for interface immutable set values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
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
        sb.AppendLine("            if (length is 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (length == 1)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableHashSet.Create(item);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (fieldData.CanBuildFrameworkImmutableHashSet)
        {
            sb.AppendLine("            if (length == 2)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {elementType} first = default!, second = default!;");
            sb.AppendLine("                reader.ReadValue(ref first);");
            sb.AppendLine("                reader.ReadValue(ref second);");
            sb.AppendLine("                value = global::LuminPack.Utility.LuminTreeStorage.CreateFrameworkImmutableHashSet(first, second);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine($"            var items = global::System.Buffers.ArrayPool<{elementType}>.Shared.Rent(length);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = 0; i < length; i++)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {elementType} item = default!;");
            sb.AppendLine("                    reader.ReadValue(ref item);");
            sb.AppendLine("                    items[i] = item;");
            sb.AppendLine("                }");
            sb.AppendLine("                value = global::LuminPack.Utility.LuminTreeStorage.CreateFrameworkImmutableHashSet(items, length);");
            sb.AppendLine("            }");
            sb.AppendLine($"            finally {{ global::System.Buffers.ArrayPool<{elementType}>.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>()); }}");
            return;
        }
        ImmutableBuilderEmitter.Begin(sb, fieldData.CanReuseImmutableBuilder, "HashSet", elementType, "            ");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementType} item = default;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                builder.Add(item);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = builder.ToImmutable();");
        ImmutableBuilderEmitter.End(sb, fieldData.CanReuseImmutableBuilder, "HashSet", "            ");
    }
}
