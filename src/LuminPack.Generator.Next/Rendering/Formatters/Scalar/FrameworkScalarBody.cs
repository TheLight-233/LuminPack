using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Preserves the existing framework scalar JSON representations.</summary>
internal static class FrameworkScalarBody
{
    private static readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal)
    {
        "global::System.IntPtr",
        "global::System.UIntPtr",
        "global::System.Guid",
        "global::System.DateTime",
        "global::System.DateTimeOffset",
        "global::System.TimeSpan",
        "global::System.Half",
        "global::System.Int128",
        "global::System.UInt128",
        "global::System.DateOnly",
        "global::System.TimeOnly",
        "global::System.Index",
        "global::System.Range",
        "global::System.Text.Rune",
        "global::System.Numerics.Complex",
        "global::System.Numerics.Plane",
        "global::System.Numerics.Quaternion",
        "global::System.Numerics.Matrix3x2",
        "global::System.Numerics.Matrix4x4",
        "global::System.Numerics.Vector2",
        "global::System.Numerics.Vector3",
        "global::System.Numerics.Vector4",
        "global::System.Runtime.InteropServices.GCHandle",
    };
    /// <summary>Recognizes an established fixed framework scalar.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    internal static bool Contains(string type) => Types.Contains(type);
    /// <summary>Emits a scalar body with the caller's compiled JSON profile.</summary>
    /// <param name = "writer">The source writer or operation writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "write">Whether to emit the write operation.</param>
    /// <param name="quotedScalarReader">Whether the referenced Core exposes the shared parser.</param>
    internal static void Emit(CodeWriter writer, FormatterModel model, bool write, bool quotedScalarReader = false)
    {
        var body = new StringBuilder();
        if (write)
            GenerateJsonSerializeCode(model, body);
        else
            GenerateJsonDeserializeCode(model, body, quotedScalarReader);
        string text = body.ToString();
        var methods = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SByte"] = "sbyte",
            ["Byte"] = "byte",
            ["Short"] = "short",
            ["UShort"] = "ushort",
            ["Int"] = "int",
            ["UInt"] = "uint",
            ["Long"] = "long",
            ["ULong"] = "ulong",
            ["Float"] = "float",
            ["Double"] = "double",
            ["Decimal"] = "decimal",
            ["Char"] = "char",
            ["Bool"] = "bool",
        };
        foreach (var method in methods)
        {
            text = text.Replace("writer.Write" + method.Key + "(", "writer.Write<TLuminProfile>(");
            text = text.Replace("reader.Read" + method.Key + "()", "reader.Read<" + method.Value + ", TLuminProfile>()");
        }

        text = text.Replace("reader.GetBoolean()", "reader.Read<bool, global::LuminPack.Option.LuminPackDefaultOptions>()");
        foreach (string method in new[]
        {
            "WriteGuid",
            "WriteDateTime",
            "WriteDateTimeOffset",
            "WriteTimeSpan"
        }

        )
            text = text.Replace("writer." + method + "(", "writer.Write<TLuminProfile>(");
        string[] writes =
        {
            "WriteObjectStart",
            "WriteObjectEnd",
            "WriteArrayStart",
            "WriteArrayEnd",
            "WritePropertyName",
            "WriteString",
            "WriteFormattedValue"
        };
        foreach (string method in writes)
            text = text.Replace("writer." + method + "(", "writer." + (method is "WriteString" or "WriteFormattedValue" ? "Write" : method) + "<TLuminProfile>(");
        string[] reads =
        {
            "Read",
            "Skip",
            "TryConsumeObjectStart",
            "TryConsumeArrayStart",
            "ConsumeArrayEnd",
            "ReadStringUtf8",
            "ReadStringUtf16",
            "ReadStringLength",
            "ReadNextFloatValue"
        };
        foreach (string method in reads)
            text = text.Replace("reader." + method + "(", "reader." + method + "<TLuminProfile>(");
        text = text.Replace("reader.ReadString()", "reader.Read<string, TLuminProfile>()");
        text = text.Replace("reader.Option.JsonByteCount(1)", "default(TLuminProfile).JsonByteCount(1)");
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            if (line.Length != 0)
                writer.Line(line.TrimStart());
    }

    /// <summary>Matches fixed property names from their decoded token storage.</summary>
    /// <param name="data">The formatter's runtime capabilities.</param>
    /// <param name="sb">The output receiving generated statements.</param>
    /// <param name="first">The first ASCII property name.</param>
    /// <param name="second">The second ASCII property name.</param>
    private static void AppendPropertyChoice(FormatterModel data, StringBuilder sb, string first, string second)
    {
        if (data.Supports("CanReadJsonStringChoice"))
        {
            // The span conversion of a constant byte array is emitted as static data by Roslyn.
            string Bytes(string name) => "new byte[] { " + string.Join(", ", name.Select(character =>
                ((int)character).ToString(System.Globalization.CultureInfo.InvariantCulture))) + " }";
            sb.AppendLine("int propertyChoice = reader.ReadStringChoice<TLuminProfile>(" + Bytes(first) + ", \"" + first + "\".AsSpan(), " +
                Bytes(second) + ", \"" + second + "\".AsSpan());");
            sb.AppendLine("reader.CompleteValue();");
            return;
        }

        sb.AppendLine("string propertyName = reader.Read<string, TLuminProfile>();");
        sb.AppendLine("int propertyChoice = propertyName == \"" + first + "\" ? 1 : propertyName == \"" + second + "\" ? 2 : 0;");
    }

    /// <summary>Appends JSON writing statements for unmanaged values.</summary>
    /// <param name = "data">The metadata describing the serializable type.</param>
    /// <param name = "sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(FormatterModel data, StringBuilder sb)
    {
        string write = data.Type switch
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
            string value = data.Type is "nint" or "global::System.IntPtr" ? "value.ToInt64()" : data.Type is "nuint" or "global::System.UIntPtr" ? "value.ToUInt64()" : "value";
            sb.AppendLine("            writer." + write + "(" + value + ");");
            return;
        }

        switch (data.Type)
        {
            case "global::System.Guid":
                sb.AppendLine("            writer.WriteGuid(value);");
                break;
            case "global::System.DateTime":
                sb.AppendLine("            writer.WriteDateTime(value);");
                break;
            case "global::System.DateTimeOffset":
                sb.AppendLine("            writer.WriteDateTimeOffset(value);");
                break;
            case "global::System.TimeSpan":
                sb.AppendLine("            writer.WriteTimeSpan(value);");
                break;
            case "global::System.Index":
                sb.AppendLine("            writer.WriteInt(value.IsFromEnd ? ~value.Value : value.Value);");
                break;
            case "global::System.Range":
                sb.AppendLine("            var start = value.Start;");
                sb.AppendLine("            var end = value.End;");
                sb.AppendLine("            writer.WriteArrayStart();");
                sb.AppendLine("            writer.WriteInt(start.IsFromEnd ? ~start.Value : start.Value);");
                sb.AppendLine("            writer.WriteInt(end.IsFromEnd ? ~end.Value : end.Value);");
                sb.AppendLine("            writer.WriteArrayEnd();");
                break;
            // The referenced Core asset, not the consumer's framework symbols, selects these overloads.
            case "global::System.DateOnly":
            case "global::System.TimeOnly":
                sb.AppendLine(data.FormattedJson ? "            writer.WriteFormattedValue(value);" : "            writer.WriteString(value.ToString(\"O\"));");
                break;
            case "global::System.Int128":
            case "global::System.UInt128":
                sb.AppendLine(data.FormattedJson ? "            writer.WriteFormattedValue(value);" : "            writer.WriteString(value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));");
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
                sb.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"No JSON formatter is available for " + data.Type + "\");");
                break;
        }
    }

    /// <summary>Appends JSON reading statements for unmanaged values.</summary>
    /// <param name = "data">The metadata describing the serializable type.</param>
    /// <param name = "sb">The builder receiving generated source code.</param>
    /// <param name="quotedScalarReader">Whether the referenced Core exposes the shared parser.</param>
    public static void GenerateJsonDeserializeCode(FormatterModel data, StringBuilder sb, bool quotedScalarReader = false)
    {
        string read = data.Type switch
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
            string cast = data.Type is "nint" or "global::System.IntPtr" ? "(global::System.IntPtr)" : data.Type is "nuint" or "global::System.UIntPtr" ? "(global::System.UIntPtr)" : string.Empty;
            sb.AppendLine("            value = " + cast + "reader." + read + "();");
            return;
        }

        switch (data.Type)
        {
            case "global::System.Guid":
            case "global::System.DateTime":
            case "global::System.DateTimeOffset":
            case "global::System.TimeSpan":
            case "global::System.TimeOnly":
            case "global::System.Int128":
            case "global::System.UInt128":
                sb.AppendLine("            value = " + QuotedScalarRead(data.Type, quotedScalarReader) + ";");
                break;
            case "global::System.Half":
                sb.AppendLine("            value = (global::System.Half)reader.ReadFloat();");
                break;
            case "global::System.DateOnly":
                AppendDateOnlyRead(sb, data.Supports("ParseRoundTripDate"));
                break;
            case "global::System.Text.Rune":
                sb.AppendLine("            value = new global::System.Text.Rune(reader.ReadInt());");
                break;
            case "global::System.Index":
                sb.AppendLine("            int index = reader.ReadInt();");
                sb.AppendLine("            value = new global::System.Index(index < 0 ? ~index : index, index < 0);");
                break;
            case "global::System.Range":
                sb.AppendLine("            reader.TryConsumeArrayStart();");
                sb.AppendLine("            int start = reader.ReadInt();");
                sb.AppendLine("            int end = reader.ReadInt();");
                sb.AppendLine("            reader.ConsumeArrayEnd();");
                sb.AppendLine("            value = new global::System.Range(new global::System.Index(start < 0 ? ~start : start, start < 0),");
                sb.AppendLine("                new global::System.Index(end < 0 ? ~end : end, end < 0));");
                break;
            case "global::System.Numerics.Complex":
                sb.AppendLine("            reader.TryConsumeObjectStart();");
                sb.AppendLine("            double real = 0, imaginary = 0;");
                sb.AppendLine("            while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
                sb.AppendLine("                {");
                AppendPropertyChoice(data, sb, "Real", "Imaginary");
                sb.AppendLine("                    reader.Read();");
                sb.AppendLine("                    if (propertyChoice == 1) real = reader.ReadDouble();");
                sb.AppendLine("                    else if (propertyChoice == 2) imaginary = reader.ReadDouble();");
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
                AppendPropertyChoice(data, sb, "Normal", "D");
                sb.AppendLine("                    reader.Read();");
                sb.AppendLine("                    if (propertyChoice == 1)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        reader.TryConsumeArrayStart();");
                sb.AppendLine("                        normal = new global::System.Numerics.Vector3(reader.ReadNextFloatValue(), reader.ReadNextFloatValue(), reader.ReadNextFloatValue());");
                sb.AppendLine("                        reader.ConsumeArrayEnd();");
                sb.AppendLine("                    }");
                sb.AppendLine("                    else if (propertyChoice == 2) d = reader.ReadFloat();");
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
                sb.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"No JSON formatter is available for " + data.Type + "\");");
                break;
        }
    }

    /// <summary>Checks which scalar types use the shared quoted text reader.</summary>
    /// <param name="type">The emitted framework type.</param>
    /// <returns>Whether the type has a shared parser.</returns>
    internal static bool IsQuotedScalar(string type) => type is
        "global::System.Guid" or "global::System.TimeSpan" or "global::System.DateTime" or "global::System.DateTimeOffset" or
        "global::System.TimeOnly" or "global::System.Int128" or "global::System.UInt128";

    /// <summary>Selects the shared parser or the original string parser for older runtime references.</summary>
    /// <param name="type">The framework type with equivalent string and span parsers.</param>
    /// <param name="shared">Whether the shared runtime method is available.</param>
    /// <returns>The generated read expression.</returns>
    internal static string QuotedScalarRead(string type, bool shared)
    {
        if (shared)
            return "reader.ReadQuotedScalar<" + type + ", TLuminProfile>()";
        string arguments = type switch
        {
            "global::System.DateTime" or "global::System.DateTimeOffset" => ", null, global::System.Globalization.DateTimeStyles.RoundtripKind",
            "global::System.Int128" or "global::System.UInt128" => ", global::System.Globalization.CultureInfo.InvariantCulture",
            _ => ""
        };
        return type + ".Parse(reader.Read<string, TLuminProfile>()!" + arguments + ")";
    }

    /// <summary>Parses a canonical date directly from token storage, retaining the platform parser and its fallback.</summary>
    /// <param name = "sb">The generated JSON statements.</param>
    /// <param name="sharedParser">Whether the referenced Core exposes the bounded date parser.</param>
    private static void AppendDateOnlyRead(StringBuilder sb, bool sharedParser)
    {
        const string parser = "global::LuminPack.Core.LuminPackJsonReader.ParseRoundTripDate";
        sb.AppendLine("            if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.DateOnly.ParseExact(reader.ReadString(), \"O\", global::System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            if (reader.Option.JsonByteCount(1) == sizeof(char))");
        sb.AppendLine("            {");
        sb.AppendLine("                var text = reader.ReadStringUtf16();");
        sb.AppendLine("                reader.CompleteValue();");
        sb.AppendLine(sharedParser ? "                value = " + parser + "(text);" :
            "                value = global::System.DateOnly.ParseExact(text, \"O\", global::System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                var text = reader.ReadStringUtf8();");
        sb.AppendLine("                reader.CompleteValue();");
        sb.AppendLine("                if (text.Length == 10 && (text[0] | text[1] | text[2] | text[3] | text[4] | text[5] | text[6] | text[7] | text[8] | text[9]) <= 127)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::System.Span<char> characters = stackalloc char[10];");
        sb.AppendLine("                    for (int i = 0; i < characters.Length; i++) characters[i] = (char)text[i];");
        sb.AppendLine(sharedParser ? "                    value = " + parser + "(characters);" :
            "                    value = global::System.DateOnly.ParseExact(characters, \"O\", global::System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("                }");
        sb.AppendLine("                else value = global::System.DateOnly.ParseExact(global::System.Text.Encoding.UTF8.GetString(text), \"O\", global::System.Globalization.CultureInfo.InvariantCulture);");
        sb.AppendLine("            }");
    }

    /// <summary>Appends binary writing statements for unmanaged values.</summary>
    /// <param name = "sb">The builder receiving generated source code.</param>
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

    /// <summary>Appends binary writing statements for unmanaged values.</summary>
    /// <param name = "sb">The builder receiving generated source code.</param>
    /// <param name = "expressions">The expressions used by this operation.</param>
    private static void AppendFloatArrayWrite(StringBuilder sb, params string[] expressions)
    {
        sb.AppendLine("            writer.WriteArrayStart();");
        foreach (string expression in expressions)
        {
            sb.AppendLine("            writer.WriteFloat(" + expression + ");");
        }

        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends binary reading statements for unmanaged values.</summary>
    /// <param name = "sb">The builder receiving generated source code.</param>
    private static void AppendQuaternionRead(StringBuilder sb)
    {
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            float x = 0, y = 0, z = 0, w = 0;");
        sb.AppendLine("            while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                {");
        sb.AppendLine("                    char propertyName;");
        sb.AppendLine("                    if (reader.Option.JsonByteCount(1) == 1)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var name = reader.ReadStringUtf8();");
        sb.AppendLine("                        propertyName = name.Length == 1 ? (char)name[0] : default(char);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var name = reader.ReadStringUtf16();");
        sb.AppendLine("                        propertyName = name.Length == 1 ? name[0] : default(char);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    reader.CompleteValue();");
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

    /// <summary>Appends binary reading statements for unmanaged values.</summary>
    /// <param name = "sb">The builder receiving generated source code.</param>
    /// <param name = "typeName">The C# type name to use in generated source.</param>
    /// <param name = "count">The number of entries involved in the operation.</param>
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
