using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides immutable array emitter functionality.</summary>
public static class ImmutableArrayEmitter
{
    /// <summary>Appends binary writing statements for immutable array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value.IsDefault)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                var span = value.AsSpan();");
        sb.AppendLine($"                if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.EnsureAdditionalCapacity(checked(sizeof(int) + span.Length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">()));");
        sb.AppendLine("                    writer.DangerousWriteUnmanagedSpan(ref index, span, out var spanOffset);");
        sb.AppendLine("                    writer.Advance(spanOffset);");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (span.IsEmpty)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                        writer.Advance(sizeof(int));");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("                        writer.Advance(sizeof(int));");
        sb.AppendLine("                        foreach (var item in span)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            writer.CheckBuffer();");
        sb.AppendLine("                            var temp = item;");
        sb.AppendLine("                            writer.WriteValue(in temp!);");
        sb.AppendLine("                        }");
        sb.AppendLine("                        writer.CheckBuffer();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary reading statements for immutable array values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine($"            {elementType}[]? array = null;");
        sb.AppendLine("            ref var readIndex = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.DangerousReadUnmanagedArray(ref readIndex, ref array!, out var readOffset);");
        sb.AppendLine("                reader.Advance(readOffset);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!reader.TryReadCollectionHead(ref readIndex, out var length))");
        sb.AppendLine("                {");
        sb.AppendLine("                    array = null;");
        sb.AppendLine("                    reader.Advance(4);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.Advance(4);");
        sb.AppendLine("                    if (length is 0)");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        array = global::System.Array.Empty<{elementType}>();");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        array = LuminPackMarshal.AllocateUninitializedArray<{elementType}>(length);");
        sb.AppendLine("                        ref var first = ref LuminPackMarshal.GetNotNullArrayReference(array!);");
        sb.AppendLine("                        for (nint i = 0; i < length; i++)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (array is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (array.Length == 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = global::System.Collections.Immutable.ImmutableArray<{elementType}>.Empty;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine($"            value = global::System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(array);");
        sb.AppendLine("#else");
        sb.AppendLine($"            value = global::System.Collections.Immutable.ImmutableArray.Create<{elementType}>();");
        sb.AppendLine($"            ref var view = ref global::LuminPack.Code.LuminPackMarshal.As<global::System.Collections.Immutable.ImmutableArray<{elementType}>, global::LuminPack.Code.ImmutableArrayView<{elementType}>>(ref value);");
        sb.AppendLine("            view.array = array;");
        sb.AppendLine("#endif");
    }
}
