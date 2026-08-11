using System.Linq;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static class UnmanagedEmitter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);");
        sb.AppendLine($"            writer.Advance(Unsafe.SizeOf<{data.TypeName}>());");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            value = Unsafe.ReadUnaligned<{data.TypeName}>(ref reader.GetCurrentSpanReference());");
        sb.AppendLine($"            reader.Advance(Unsafe.SizeOf<{data.TypeName}>());");
    }

    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine($"            evaluator += Unsafe.SizeOf<{data.TypeName}>();");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        string write = data.TypeName switch
        {
            "sbyte" or "global::System.SByte" => "WriteSByte",
            "byte" or "global::System.Byte" => "WriteByte",
            "short" or "global::System.Int16" => "WriteShort",
            "ushort" or "global::System.UInt16" => "WriteUShort",
            "int" or "global::System.Int32" => "WriteInt",
            "uint" or "global::System.UInt32" => "WriteUInt",
            "long" or "global::System.Int64" => "WriteLong",
            "ulong" or "global::System.UInt64" => "WriteULong",
            "float" or "global::System.Single" => "WriteFloat",
            "double" or "global::System.Double" => "WriteDouble",
            "decimal" or "global::System.Decimal" => "WriteDecimal",
            "char" or "global::System.Char" => "WriteChar",
            "bool" or "global::System.Boolean" => "WriteBool",
            "nint" or "global::System.IntPtr" => "WriteLong",
            "nuint" or "global::System.UIntPtr" => "WriteULong",
            _ => null
        };

        if (write is not null)
        {
            string value = data.TypeName is "nint" or "global::System.IntPtr" ? "value.ToInt64()"
                : data.TypeName is "nuint" or "global::System.UIntPtr" ? "value.ToUInt64()"
                : "value";
            sb.AppendLine("            writer." + write + "(" + value + ");");
            return;
        }

        switch (data.TypeName)
        {
            case "global::System.Guid":
                sb.AppendLine("            writer.WriteString(value.ToString());");
                break;
            case "global::System.DateTime":
            case "global::System.DateTimeOffset":
            case "global::System.DateOnly":
            case "global::System.TimeOnly":
                sb.AppendLine("            writer.WriteString(value.ToString(\"O\"));");
                break;
            case "global::System.TimeSpan":
            case "global::System.Int128":
            case "global::System.UInt128":
                sb.AppendLine("            writer.WriteString(value.ToString());");
                break;
            case "global::System.Half":
                sb.AppendLine("            writer.WriteFloat((float)value);");
                break;
            case "global::System.Text.Rune":
                sb.AppendLine("            writer.WriteInt(value.Value);");
                break;
            case "global::System.Numerics.Complex":
                sb.AppendLine("            writer.WriteObjectStart();");
                sb.AppendLine("            writer.WritePropertyName(\"Real\");");
                sb.AppendLine("            writer.WriteDouble(value.Real);");
                sb.AppendLine("            writer.WritePropertyName(\"Imaginary\");");
                sb.AppendLine("            writer.WriteDouble(value.Imaginary);");
                sb.AppendLine("            writer.WriteObjectEnd();");
                break;
            case "global::System.Numerics.Plane":
                sb.AppendLine("            writer.WriteObjectStart();");
                sb.AppendLine("            writer.WritePropertyName(\"Normal\");");
                sb.AppendLine("            writer.WriteArrayStart();");
                sb.AppendLine("            writer.WriteFloat(value.Normal.X);");
                sb.AppendLine("            writer.WriteFloat(value.Normal.Y);");
                sb.AppendLine("            writer.WriteFloat(value.Normal.Z);");
                sb.AppendLine("            writer.WriteArrayEnd();");
                sb.AppendLine("            writer.WritePropertyName(\"D\");");
                sb.AppendLine("            writer.WriteFloat(value.D);");
                sb.AppendLine("            writer.WriteObjectEnd();");
                break;
            case "global::System.Numerics.Quaternion":
                AppendQuaternionWrite(sb);
                break;
            case "global::System.Numerics.Matrix3x2":
                AppendFloatArrayWrite(sb, "value.M11", "value.M12", "value.M21", "value.M22", "value.M31", "value.M32");
                break;
            case "global::System.Numerics.Matrix4x4":
                AppendFloatArrayWrite(sb, "value.M11", "value.M12", "value.M13", "value.M14", "value.M21", "value.M22", "value.M23", "value.M24", "value.M31", "value.M32", "value.M33", "value.M34", "value.M41", "value.M42", "value.M43", "value.M44");
                break;
            case "global::System.Numerics.Vector2":
                AppendFloatArrayWrite(sb, "value.X", "value.Y");
                break;
            case "global::System.Numerics.Vector3":
                AppendFloatArrayWrite(sb, "value.X", "value.Y", "value.Z");
                break;
            case "global::System.Numerics.Vector4":
                AppendFloatArrayWrite(sb, "value.X", "value.Y", "value.Z", "value.W");
                break;
            default:
                sb.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"No JSON formatter is available for " + data.TypeName + "\");");
                break;
        }
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        string read = data.TypeName switch
        {
            "sbyte" or "global::System.SByte" => "ReadSByte",
            "byte" or "global::System.Byte" => "ReadByte",
            "short" or "global::System.Int16" => "ReadShort",
            "ushort" or "global::System.UInt16" => "ReadUShort",
            "int" or "global::System.Int32" => "ReadInt",
            "uint" or "global::System.UInt32" => "ReadUInt",
            "long" or "global::System.Int64" => "ReadLong",
            "ulong" or "global::System.UInt64" => "ReadULong",
            "float" or "global::System.Single" => "ReadFloat",
            "double" or "global::System.Double" => "ReadDouble",
            "decimal" or "global::System.Decimal" => "ReadDecimal",
            "char" or "global::System.Char" => "ReadChar",
            "bool" or "global::System.Boolean" => "GetBoolean",
            "nint" or "global::System.IntPtr" => "ReadLong",
            "nuint" or "global::System.UIntPtr" => "ReadULong",
            _ => null
        };

        if (read is not null)
        {
            string cast = data.TypeName is "nint" or "global::System.IntPtr" ? "(global::System.IntPtr)"
                : data.TypeName is "nuint" or "global::System.UIntPtr" ? "(global::System.UIntPtr)"
                : string.Empty;
            sb.AppendLine("            value = " + cast + "reader." + read + "();");
            return;
        }

        switch (data.TypeName)
        {
            case "global::System.Guid":
                sb.AppendLine("            value = global::System.Guid.Parse(reader.ReadString());");
                break;
            case "global::System.DateTime":
                sb.AppendLine("            value = global::System.DateTime.Parse(reader.ReadString(), null, global::System.Globalization.DateTimeStyles.RoundtripKind);");
                break;
            case "global::System.DateTimeOffset":
                sb.AppendLine("            value = global::System.DateTimeOffset.Parse(reader.ReadString(), null, global::System.Globalization.DateTimeStyles.RoundtripKind);");
                break;
            case "global::System.TimeSpan":
                sb.AppendLine("            value = global::System.TimeSpan.Parse(reader.ReadString());");
                break;
            case "global::System.Half":
                sb.AppendLine("            value = (global::System.Half)reader.ReadFloat();");
                break;
            case "global::System.Int128":
                sb.AppendLine("            value = global::System.Int128.Parse(reader.ReadString());");
                break;
            case "global::System.UInt128":
                sb.AppendLine("            value = global::System.UInt128.Parse(reader.ReadString());");
                break;
            case "global::System.DateOnly":
                sb.AppendLine("            value = global::System.DateOnly.Parse(reader.ReadString());");
                break;
            case "global::System.TimeOnly":
                sb.AppendLine("            value = global::System.TimeOnly.Parse(reader.ReadString());");
                break;
            case "global::System.Text.Rune":
                sb.AppendLine("            value = new global::System.Text.Rune(reader.ReadInt());");
                break;
            case "global::System.Numerics.Complex":
                sb.AppendLine("            reader.TryConsumeObjectStart();");
                sb.AppendLine("            double real = 0, imaginary = 0;");
                sb.AppendLine("            while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
                sb.AppendLine("                {");
                sb.AppendLine("                    string propertyName = reader.ReadString();");
                sb.AppendLine("                    reader.Read();");
                sb.AppendLine("                    if (propertyName == \"Real\") real = reader.ReadDouble();");
                sb.AppendLine("                    else if (propertyName == \"Imaginary\") imaginary = reader.ReadDouble();");
                sb.AppendLine("                    else reader.Skip();");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            value = new global::System.Numerics.Complex(real, imaginary);");
                break;
            case "global::System.Numerics.Plane":
                sb.AppendLine("            reader.TryConsumeObjectStart();");
                sb.AppendLine("            global::System.Numerics.Vector3 normal = default;");
                sb.AppendLine("            float d = 0;");
                sb.AppendLine("            while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
                sb.AppendLine("                {");
                sb.AppendLine("                    string propertyName = reader.ReadString();");
                sb.AppendLine("                    reader.Read();");
                sb.AppendLine("                    if (propertyName == \"Normal\")");
                sb.AppendLine("                    {");
                sb.AppendLine("                        reader.TryConsumeArrayStart();");
                sb.AppendLine("                        normal = new global::System.Numerics.Vector3(reader.ReadNextFloatValue(), reader.ReadNextFloatValue(), reader.ReadNextFloatValue());");
                sb.AppendLine("                        reader.ConsumeArrayEnd();");
                sb.AppendLine("                    }");
                sb.AppendLine("                    else if (propertyName == \"D\") d = reader.ReadFloat();");
                sb.AppendLine("                    else reader.Skip();");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            value = new global::System.Numerics.Plane(normal, d);");
                break;
            case "global::System.Numerics.Quaternion":
                AppendQuaternionRead(sb);
                break;
            case "global::System.Numerics.Matrix3x2":
                AppendFloatArrayRead(sb, "global::System.Numerics.Matrix3x2", 6);
                break;
            case "global::System.Numerics.Matrix4x4":
                AppendFloatArrayRead(sb, "global::System.Numerics.Matrix4x4", 16);
                break;
            case "global::System.Numerics.Vector2":
                AppendFloatArrayRead(sb, "global::System.Numerics.Vector2", 2);
                break;
            case "global::System.Numerics.Vector3":
                AppendFloatArrayRead(sb, "global::System.Numerics.Vector3", 3);
                break;
            case "global::System.Numerics.Vector4":
                AppendFloatArrayRead(sb, "global::System.Numerics.Vector4", 4);
                break;
            default:
                sb.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"No JSON formatter is available for " + data.TypeName + "\");");
                break;
        }
    }

    private static void AppendQuaternionWrite(StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteObjectStart();");
        sb.AppendLine("            writer.WritePropertyName(\"X\");");
        sb.AppendLine("            writer.WriteFloat(value.X);");
        sb.AppendLine("            writer.WritePropertyName(\"Y\");");
        sb.AppendLine("            writer.WriteFloat(value.Y);");
        sb.AppendLine("            writer.WritePropertyName(\"Z\");");
        sb.AppendLine("            writer.WriteFloat(value.Z);");
        sb.AppendLine("            writer.WritePropertyName(\"W\");");
        sb.AppendLine("            writer.WriteFloat(value.W);");
        sb.AppendLine("            writer.WriteObjectEnd();");
    }

    private static void AppendFloatArrayWrite(StringBuilder sb, params string[] expressions)
    {
        sb.AppendLine("            writer.WriteArrayStart();");
        foreach (string expression in expressions)
        {
            sb.AppendLine("            writer.WriteFloat(" + expression + ");");
        }
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    private static void AppendQuaternionRead(StringBuilder sb)
    {
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            float x = 0, y = 0, z = 0, w = 0;");
        sb.AppendLine("            while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                {");
        sb.AppendLine("                    char propertyName = reader.ReadChar();");
        sb.AppendLine("                    reader.Read();");
        sb.AppendLine("                    if (propertyName == 'X') x = reader.ReadFloat();");
        sb.AppendLine("                    else if (propertyName == 'Y') y = reader.ReadFloat();");
        sb.AppendLine("                    else if (propertyName == 'Z') z = reader.ReadFloat();");
        sb.AppendLine("                    else if (propertyName == 'W') w = reader.ReadFloat();");
        sb.AppendLine("                    else reader.Skip();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::System.Numerics.Quaternion(x, y, z, w);");
    }

    private static void AppendFloatArrayRead(StringBuilder sb, string typeName, int count)
    {
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        for (int i = 0; i < count; i++)
        {
            sb.AppendLine("            var v" + i + " = reader.ReadNextFloatValue();");
        }
        sb.AppendLine("            reader.ConsumeArrayEnd();");
        sb.AppendLine("            value = new " + typeName + "(" + string.Join(", ", Enumerable.Range(0, count).Select(i => "v" + i)) + ");");
    }
}
