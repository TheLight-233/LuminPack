using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

internal static partial class PortableGenerateCommand
{
    /// <summary>Emits the canonical JSON graph with direct calls between record, collection and Union bodies.</summary>
    /// <param name="output">The generated module.</param>
    /// <param name="schema">The closed root projection.</param>
    /// <returns>Whether a complete document codec was generated.</returns>
    private static bool EmitTypeScriptDocument(StringBuilder output, JsonObject schema)
    {
        if (schema["rootWire"] != null) return false;
        var contracts = PortableManifest.Objects(schema, "contracts").ToDictionary(
            c => PortableManifest.Normalize((string)c["type"]!), StringComparer.Ordinal);
        var shapes = new Dictionary<string, JsonObject>();
        string Id(string type) => "C" + Identifier(PortableManifest.Normalize(type));
        string Shape(JsonObject w) => (string)w["kind"]! switch
        {
            "contract" => Id((string)w["type"]!), "scalar" => "S" + (string)w["type"]!, "string" => "Text",
            "sequence" => "List_" + Shape((JsonObject)w["element"]!),
            _ => "Dict_" + Shape((JsonObject)w["key"]!) + "_" + Shape((JsonObject)w["value"]!)
        };
        bool Accept(JsonObject w)
        {
            bool supported = (string?)w["kind"] switch
            {
                "scalar" => (string?)w["type"] is "int" or "long" or "byte" or "float" or "bool",
                "contract" => contracts.ContainsKey(PortableManifest.Normalize((string)w["type"]!)), "string" => true,
                "sequence" => (bool?)w["required"] != true && (string?)w["family"] is not ("System.Memory" or "System.ReadOnlyMemory" or "System.Buffers.ReadOnlySequence") && Accept((JsonObject)w["element"]!),
                "dictionary" => (string?)w["key"]?["kind"] is "string" or "scalar" &&
                    Accept((JsonObject)w["key"]!) && Accept((JsonObject)w["value"]!),
                _ => false
            };
            if (supported && (string?)w["kind"] != "contract") shapes.TryAdd(Shape(w), w);
            return supported;
        }
        if (contracts.Values.Any(c => (string?)c["kind"] is not ("Record" or "Struct" or "Union") ||
            c["sequenceWire"] != null || c["sequenceBase"] != null || (bool?)c["profile"]?["trackReferences"] == true ||
            PortableManifest.Objects(c, "members").Any(m => m["wire"] is not JsonObject w || !Accept(w) ||
                ((string)m["name"]!).Length == 0 || char.IsAsciiDigit(((string)m["name"]!)[0]) ||
                ((string)m["name"]!).Any(ch => ch < ' ' || ch > '~' || ch is '"' or '\\')))) return false;
        var heads = new Dictionary<string, int>(StringComparer.Ordinal);
        string Head(string text)
        {
            if (!heads.TryGetValue(text, out var index)) heads.Add(text, index = heads.Count);
            return "jsonHead" + index;
        }
        string WriteHead(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length is >= 4 and <= 8)
            {
                uint first = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                uint last = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4));
                return $"out.head({first},{last},{bytes.Length})";
            }
            return $"out.literal({Head(text)})";
        }
        void Line(string s) => output.AppendLine(s);
        string root = Id((string)schema["root"]!);
        Line($"const documentGraph: GeneratedDocument = {{ read: gr{root}, write: gw{root} }};");
        foreach (var (name, wire) in shapes)
        {
            string kind = (string)wire["kind"]!;
            if (kind == "scalar" || kind == "string")
            {
                string method = kind == "string" ? "checkedString()" : (string)wire["type"]! switch
                {
                    "int" => "int32()", "float" => "floating(true)", "long" => "long()", "byte" => "byte()", _ => "boolean()"
                };
                string store = kind == "string" ? "out.text(x)" : (string)wire["type"]! switch
                {
                    "int" => "out.int(x)", "float" => "out.float(x,true)", "long" => "out.long(x)",
                    "byte" => "out.byte(x)", _ => "out.bool(x)"
                };
                Line($"function gr{name}(r: DocumentCursor, _d: number): unknown {{ return r.{method}; }}");
                Line($"function gw{name}(out: DocumentOutput,x: unknown,_d: number): void {{ {store}; }}");
                continue;
            }
            Line($"function gr{name}(r: DocumentCursor,d: number): unknown {{");
            Line("  if (r.consume('null')) return null; r.depth(d);");
            if (kind == "sequence")
            {
                string elem = Shape((JsonObject)wire["element"]!);
                Line("  const values: unknown[] = []; r.expect('['); if (r.consume(']')) return values;");
                Line($"  do {{ r.count(values.length); values.push(gr{elem}(r,d+1)); }} while (r.consume(',')); r.expect(']'); return values;");
            }
            else
            {
                string k = Shape((JsonObject)wire["key"]!), v = Shape((JsonObject)wire["value"]!);
                if (k == "Text")
                {
                    Line("  if (r.consume('{')) { const fields: Record<string,unknown> = Object.create(null); let count=0;");
                    Line($"    if (!r.consume('}}')) {{ do {{ const key=r.string(); r.key(key); if (!Object.prototype.hasOwnProperty.call(fields,key)) r.count(count++); r.expect(':'); fields[key]=gr{v}(r,d+1); }} while(r.consume(',')); r.expect('}}'); }} return new Map(Object.entries(fields)); }}");
                }
                Line("  const entries=new Map(); let count=0; r.expect('['); if(r.consume(']')) return entries;");
                Line($"  do {{ r.count(count++); r.count(1); r.depth(d+1); r.expect('['); const key=gr{k}(r,d+2); if(key===null) r.miss(); r.expect(','); entries.set(key,gr{v}(r,d+2)); r.expect(']'); }} while(r.consume(',')); r.expect(']'); return entries;");
            }
            Line("}");
            Line($"function gw{name}(out: DocumentOutput,x: unknown,d: number): void {{");
            Line("  if (x===null) {out.ascii('null');return;}");
            if (kind == "sequence")
            {
                var element = (JsonObject)wire["element"]!;
                string scalar = (string?)element["kind"] == "scalar" ? (string)element["type"]! : "";
                Line($"  const items=out.items(x,{Quote(scalar)});out.token(91);for(let i=0;i<items.length;i++){{if(i)out.token(44);gw{Shape(element)}(out,items[i],d);}}out.token(93);");
            }
            else
            {
                string k = Shape((JsonObject)wire["key"]!), v = Shape((JsonObject)wire["value"]!);
                Line("  const entries=out.entries(x);let first=true;");
                if (k == "Text")
                {
                    Line("  const fields: Record<string,unknown>=Object.create(null);for(const [key,value] of entries){if(typeof key!=='string')out.invalid();fields[key]=value;}");
                    Line($"  out.token(123);for(const key of Object.keys(fields)){{if(!first)out.token(44);first=false;out.text(key);out.token(58);gw{v}(out,fields[key],d);}}out.token(125);");
                }
                else Line($"  out.token(91);for(const [key,value] of entries){{if(!first)out.token(44);first=false;out.token(91);gw{k}(out,key,d);out.token(44);gw{v}(out,value,d);out.token(93);}}out.token(93);");
            }
            Line("}");
        }
        foreach (var c in contracts.Values)
        {
            string name = Id((string)c["type"]!);
            var fields = PortableManifest.Objects(c, "members").ToArray();
            var cases = PortableManifest.Objects(c, "cases").ToArray();
            bool union = (string?)c["kind"] == "Union", nullable = (string?)c["kind"] != "Struct";
            Line($"function gr{name}(r: DocumentCursor,d: number): unknown {{");
            if (nullable) Line("  if(r.consume('null'))return null;");
            Line("  r.depth(d);");
            if (union)
            {
                foreach (var item in cases)
                {
                    string head = "{\"$type\":" + item["hash"] + ",\"$value\":";
                    Line($"  if(r.consume({Quote(head)})){{const value=gr{Id((string)item["type"]!)}(r,d+1);r.expect('}}');return value===null?null:{{type:{Quote(PortableManifest.Normalize((string)item["id"]!))},value}};}}");
                }
                Line("  return r.miss();");
            }
            else
            {
                if (fields.Length == 0) Line("  r.expect('{');");
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    string head = (i == 0 ? "{" : ",") + Quote((string)f["name"]!) + ":";
                    Line($"  r.expect({Quote(head)});const v{i}=gr{Shape((JsonObject)f["wire"]!)}(r,d+1);");
                }
                Line("  r.expect('}');return {" + string.Join(",", fields.Select((f,i) => (string?)f["name"] == "__proto__" ? $"[\"__proto__\"]:v{i}" : $"{Quote((string)f["name"]!)}:v{i}")) + "};");
            }
            Line("}");
            Line($"function gw{name}(out: DocumentOutput,x: unknown,d: number): void {{");
            Line("  out.depth(d);");
            if (nullable) Line("  if(x===null){out.ascii('null');return;}");
            Line("  const obj=out.object(x);");
            if (union)
            {
                Line("  if(typeof obj.type!=='string'||obj.value==null)return out.invalid();switch(obj.type.replace(/global::/g,'').trim()){");
                foreach (var item in cases)
                    Line($"    case {Quote(PortableManifest.Normalize((string)item["id"]!))}:out.literal({Head("{\"$type\":"+item["hash"]+",\"$value\":")});gw{Id((string)item["type"]!)}(out,obj.value,d+1);break;");
                Line("    default:return out.invalid();}out.token(125);");
            }
            else
            {
                if (fields.Length == 0) Line("  out.token(123);");
                for (int i=0; i<fields.Length;i++)
                {
                    var f=fields[i]; var wire=(JsonObject)f["wire"]!;
                    string field=Quote((string)f["name"]!), head=(i==0?"{":",")+Quote((string)f["name"]!)+":";
                    string fallback=(string?)wire["kind"] switch {
                        "scalar" => (string?)wire["type"] switch { "bool"=>"false","long"=>"0n",_=>"0" },
                        "contract" when (string?)contracts[PortableManifest.Normalize((string)wire["type"]!)]["kind"]=="Struct" => $"out.zero({Quote((string)c["id"]!)},{i})",
                        _=>"null"
                    };
                    Line($"  {WriteHead(head)};gw{Shape(wire)}(out,Object.prototype.hasOwnProperty.call(obj,{field})?obj[{field}]:{fallback},d+1);");
                }
                Line("  out.token(125);");
            }
            Line("}");
        }
        foreach (var head in heads)
            Line($"const jsonHead{head.Value} = Uint8Array.of({string.Join(",", Encoding.UTF8.GetBytes(head.Key))});");
        return true;
    }
}
