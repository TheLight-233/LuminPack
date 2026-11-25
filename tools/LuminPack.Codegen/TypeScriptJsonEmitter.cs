using System.Text;
using System.Text.Json.Nodes;

internal static partial class PortableGenerateCommand
{
    /// <summary>Emits canonical JSON property heads and object construction in declaration order.</summary>
    /// <param name="output">The generated TypeScript module.</param>
    /// <param name="contracts">The declarations in the root projection.</param>
    private static void EmitTypeScriptJsonBodies(StringBuilder output, IEnumerable<JsonObject> contracts)
    {
        output.AppendLine("const jsonBodies: GeneratedJsonRecord[] = [");
        foreach (var contract in contracts)
        {
            if ((string?)contract["kind"] is not ("Struct" or "Record") || (bool?)contract["profile"]?["trackReferences"] == true)
                continue;
            var fields = PortableManifest.Objects(contract, "members").Select(member =>
            {
                string name = PortableManifest.Text(member, "name");
                var wire = (JsonObject)member["wire"]!;
                return (Name: name, Kind: (string)wire["kind"]! == "scalar" ? (string)wire["type"]! : (string)wire["kind"]!);
            }).ToArray();
            // Integer property names reorder under Object.keys. Escaped names use the runtime's ECMAScript spelling.
            if (fields.Length == 0 || fields.Any(f => f.Name.Length == 0 || char.IsAsciiDigit(f.Name[0]) ||
                f.Name.Any(ch => ch < ' ' || ch > '~' || ch is '"' or '\\'))) continue;
            output.Append("  { type: ").Append(Quote(PortableManifest.Text(contract, "id")))
                .Append(", names: [").Append(string.Join(", ", fields.Select(f => Quote(f.Name))))
                .Append("], kinds: [").Append(string.Join(", ", fields.Select(f => Quote(f.Kind)))).AppendLine("],");
            string Head(int i) => Quote((i == 0 ? "{" : ",") + Quote(fields[i].Name) + ":");
            output.AppendLine("    readBinary(input, profile, state, nested) { return {");
            for (int i = 0; i < fields.Length; i++)
            {
                var (name, kind) = fields[i];
                string read = kind switch
                {
                    "int" => "input.i32()", "float" => "input.f32()", "double" => "input.f64()",
                    "long" => "input.i64()", "byte" => "input.u8()", _ => $"nested[{i}](input, profile, state)"
                };
                output.Append("      [").Append(Quote(name)).Append("]: ").Append(read).AppendLine(",");
            }
            output.AppendLine("    }; },\n    writeBinary(output, value, profile, state, nested) {");
            for (int i = 0; i < fields.Length; i++)
            {
                var (name, kind) = fields[i];
                if (kind is "int" or "float" or "double" or "long" or "byte" or "bool")
                {
                    string field = Quote(name);
                    string zero = kind switch { "long" => "0n", "bool" => "false", _ => "0" };
                    output.Append("      writeGeneratedScalar(output, ").Append(Quote(kind))
                        .Append(", Object.prototype.hasOwnProperty.call(value, ").Append(field).Append(") ? value[")
                        .Append(field).Append("] : ").Append(zero).AppendLine(", 8);");
                }
                else output.Append("      nested[").Append(i).AppendLine("](output, value, profile, state);");
            }
            output.AppendLine("    },\n    read(input, utf8, depth, nested) {");
            for (int i = 0; i < fields.Length; i++)
            {
                string read = fields[i].Kind switch
                {
                    "int" => "input.int32()", "float" => "input.floating(true)", "double" => "input.floating(false)",
                    "string" => "input.checkedString()", "long" => "input.long()", "byte" => "input.byte()", "bool" => "input.boolean()",
                    _ => $"nested[{i}](input, utf8, depth)"
                };
                output.Append("      input.expect(").Append(Head(i)).Append(");\n      const v").Append(i)
                    .Append(" = ").Append(read).AppendLine(";");
            }
            output.Append("      input.expect(\"}\");\n      return { ")
                .Append(string.Join(", ", fields.Select((f, i) => $"[{Quote(f.Name)}]: v{i}")))
                .AppendLine(" };\n    },\n    write(value, encoding, state, parts, nested) {");
            for (int i = 0; i < fields.Length; i++)
            {
                var (name, kind) = fields[i];
                if (kind is "int" or "float" or "double" or "bool" or "byte")
                {
                    string field = Quote(name);
                    string zero = kind == "bool" ? "false" : "0";
                    output.Append("      parts.push(").Append(Head(i)).Append(" + formatGeneratedJsonScalar(")
                        .Append(Quote(kind)).Append(", Object.prototype.hasOwnProperty.call(value, ").Append(field)
                        .Append(") ? value[").Append(field).Append("] : ").Append(zero).AppendLine(", 8));");
                }
                else output.Append("      parts.push(").Append(Head(i)).Append(");\n      nested[").Append(i)
                    .AppendLine("](value, encoding, state, parts);");
            }
            output.AppendLine("      parts.push(\"}\");\n    },\n    writeBytes(value, state, out, nested) {");
            for (int i = 0; i < fields.Length; i++)
            {
                var (name, kind) = fields[i];
                output.Append("      out.ascii(").Append(Head(i)).AppendLine(");");
                string field = Quote(name);
                string value = $"Object.prototype.hasOwnProperty.call(value, {field}) ? value[{field}] : 0";
                if (kind == "int") output.Append("      out.int(").Append(value).AppendLine(");");
                else if (kind is "float" or "double") output.Append("      out.float(").Append(value)
                    .Append(kind == "float" ? ", true" : ", false").AppendLine(");");
                else output.Append("      nested[").Append(i).AppendLine("](value, \"utf8\", state, out);");
            }
            output.AppendLine("      out.token(125);\n    },\n  },");
        }
        output.AppendLine("];");
    }
}
