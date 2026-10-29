using System.Text;

namespace LuminPack.Code.Core;

public static partial class LuminPackSerializerGenerator
{
    /// <summary>Completes an allocating interface entry with its proven exact implementation and unchanged pooled fallback.</summary>
    /// <param name="sb">The builder after the public entry's opening brace.</param>
    /// <param name="typeName">The closed interface contract.</param>
    /// <param name="implementation">The exact List or HashSet implementation with accepted native scalar storage.</param>
    /// <param name="smallHashSetWrite">Whether the referenced runtime supports grouped stores for this small raw element.</param>
    private static void AppendExactScalarInterfaceSerialize(StringBuilder sb, string typeName, string implementation, bool smallHashSetWrite)
    {
        string elementType = global::LuminPack.SourceGenerator.CodeEmitterRegistry.GetFirstGeneric(implementation);
        sb.AppendLine("            var selectedValue = value;");
        sb.AppendLine("            if (selectedValue is not null && selectedValue.GetType() == typeof(" + implementation + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                int directLength = checked(sizeof(int) + Unsafe.As<" + implementation + ">(selectedValue).Count * Unsafe.SizeOf<" + elementType + ">());");
        sb.AppendLine("                var directBuffer = AllocateUninitializedArray<byte>(directLength);");
        sb.AppendLine("                var directSpan = directBuffer.AsSpan();");
        sb.AppendLine("                var writer = new LuminPackWriter(ref directSpan);");
        sb.AppendLine("                RootWriteExactInterface(ref writer, selectedValue);");
        sb.AppendLine("                if (writer.Position == directLength) return directBuffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            return SerializeInterfaceSlow(in selectedValue);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Eligible binary element roots are profile-independent. Share the original pooled body between profile shells.
        sb.AppendLine("        /// <summary>Preserves pooled operation ownership for other interface implementations.</summary>");
        sb.AppendLine("        /// <param name=\"value\">The original interface value.</param>");
        sb.AppendLine("        /// <returns>The complete original binary record.</returns>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine("        private static byte[] SerializeInterfaceSlow(in " + typeName + " value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var writerBuffer = LuminBufferWriterPool.Rent();");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                var writer = new LuminPackWriter(writerBuffer);");
        AppendBinaryOperation(sb, read: false);
        sb.AppendLine("                var buffer = AllocateUninitializedArray<byte>(writer.CurrentIndex);");
        sb.AppendLine("                writer.GetSpan().CopyTo(buffer.AsSpan());");
        sb.AppendLine("                return buffer;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally");
        sb.AppendLine("            {");
        sb.AppendLine("                LuminBufferWriterPool.Return(writerBuffer);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>Runs the existing raw kernel after checking the exact runtime implementation.</summary>");
        sb.AppendLine("        /// <param name=\"writer\">The complete output state.</param>");
        sb.AppendLine("        /// <param name=\"value\">The already checked interface value.</param>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static void RootWriteExactInterface(ref LuminPackWriter writer, " + typeName + " value)");
        sb.AppendLine("        {");
        if (implementation.StartsWith("global::System.Collections.Generic.List<", System.StringComparison.Ordinal))
        {
            sb.AppendLine("            var items = global::LuminPack.Code.LuminPackMarshal.GetListSpan(Unsafe.As<" + implementation + ">(value));");
            sb.AppendLine("            writer.Write<" + elementType + ">(items, items.Length);");
        }
        else
        {
            string kernel = smallHashSetWrite ? "WriteSmallHashSetRaw" : "WriteHashSetRaw";
            sb.AppendLine("            global::LuminPack.Code.LuminPackMarshal." + kernel + "(ref writer, Unsafe.As<" + implementation + ">(value));");
        }
        sb.AppendLine("        }");
        sb.AppendLine();
    }
}
