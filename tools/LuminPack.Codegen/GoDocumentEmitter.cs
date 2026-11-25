using System.Text;
using System.Text.Json.Nodes;

internal static partial class PortableGenerateCommand
{
    /// <summary>Emits a concrete Go model and its four document operations for a closed supported projection.</summary>
    /// <param name="schema">The projected root and reachable declarations.</param>
    /// <returns>Generated declarations, or an empty string when the schema requires the dynamic codec.</returns>
    private static string GoDocuments(JsonObject schema)
    {
        if (schema["rootWire"] != null)
            return "";
        var contracts = PortableManifest.Objects(schema, "contracts").ToDictionary(c =>
            PortableManifest.Normalize(PortableManifest.Text(c, "type")), StringComparer.Ordinal);
        if (!contracts.TryGetValue(PortableManifest.Text(schema, "root"), out var root))
            return "";
        string rootName = Identifier(PortableManifest.Text(root, "name"));
        if (rootName.Length == 0 || !char.IsAsciiLetterUpper(rootName[0]))
            rootName = "Root" + rootName;
        var shapes = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var outp = new StringBuilder();
        var scalarTypes = new Dictionary<string, (string Type, string Method, int Width)>
        {
            ["int"] = ("int32", "Int", 4),
            ["float"] = ("float32", "Float", 4),
            ["long"] = ("int64", "Long", 8),
            ["bool"] = ("bool", "Bool", 1),
            ["byte"] = ("byte", "Byte", 1)
        };

        string Id(JsonObject c) => PortableManifest.Normalize(PortableManifest.Text(c, "type"));
        string Symbol(JsonObject c) => ReferenceEquals(c, root) ? rootName + "Model" : rootName + "_" + Identifier(Id(c));
        string Field(JsonObject m) => "F" + Identifier(PortableManifest.Text(m, "name"));
        string Kind(JsonObject w) => PortableManifest.Text(w, "kind");
        JsonObject Child(JsonObject w, string key) => (JsonObject)w[key]!;
        string Type(JsonObject w) => Kind(w) switch
        {
            "scalar" => scalarTypes[(string)w["type"]!].Type,
            "string" => "luminpack.Text",
            "contract" => Model(contracts[PortableManifest.Normalize((string)w["type"]!)]),
            "sequence" => "[]" + Type(Child(w, "element")),
            _ => "[]luminpack.TypedEntry[" + Type(Child(w, "key")) + "," + Type(Child(w, "value")) + "]"
        };
        string Model(JsonObject c) => ((string?)c["kind"] == "Record" ? "*" : "") + Symbol(c);
        string Shape(JsonObject w) => Kind(w) switch
        {
            "scalar" => scalarTypes[(string)w["type"]!].Method,
            "string" => "Text",
            "contract" => Symbol(contracts[PortableManifest.Normalize((string)w["type"]!)]),
            "sequence" => "Slice_" + Shape(Child(w, "element")),
            _ => "Dict_" + Shape(Child(w, "key")) + "_" + Shape(Child(w, "value"))
        };
        string Function(string prefix, JsonObject w) => prefix + (Kind(w) == "contract" ? "" : rootName + "_") + Shape(w);
        bool Supported(JsonObject w)
        {
            if (w["layout"] != null || w["profile"] != null || w["nullableLayout"] != null || w["rawCompoundStorage"] != null ||
                (bool?)w["required"] == true || (bool?)w["memory"] == true)
                return false;
            bool ok = Kind(w) switch
            {
                "scalar" => scalarTypes.ContainsKey((string)w["type"]!),
                "string" => true,
                "contract" => contracts.ContainsKey(PortableManifest.Normalize((string)w["type"]!)),
                "sequence" => (string?)w["family"] is not ("System.Memory" or "System.ReadOnlyMemory" or "System.Buffers.ReadOnlySequence")
                    && Supported(Child(w, "element")),
                "dictionary" => Supported(Child(w, "key")) && Supported(Child(w, "value")) &&
                    (Kind(Child(w, "key")) == "string" || (string?)w["key"]?["type"] is "int" or "long" or "byte"),
                _ => false
            };
            if (ok && Kind(w) != "contract")
                shapes.TryAdd(Shape(w), w);
            return ok;
        }
        bool Profile(JsonNode? p) => p == null || ((bool?)p["declared"] != true || (int?)p["binary"] == (int?)schema["binaryProfile"])
            && (bool?)p["trackReferences"] != true && (bool?)p["strictJson"] != true;
        foreach (var c in contracts.Values)
        {
            var fields = PortableManifest.Objects(c, "members").ToArray();
            if ((string?)c["kind"] is not ("Struct" or "Record" or "Union") || c["storage"] != null ||
                c["nativeStorage"] != null || c["sequenceWire"] != null || c["sequenceBase"] != null ||
                c["binaryMemberCount"] != null || (bool?)c["versioned"] == true || !Profile(c["profile"]) ||
                fields.Any(m => ((string)m["name"]!).Any(ch => ch < 32 || ch > 126 || ch is '"' or '\\')) || fields.Length > 249 || ((string?)c["kind"] == "Struct" && (string?)c["storageKind"] != "RawUnmanaged") ||
                fields.Any(m => m["wire"] is not JsonObject w || !Supported(w) || (bool?)m["compressed"] == true ||
                    (bool?)m["fixedNullable"] == true || !Profile(m["profile"])))
                return "";
            if ((string?)c["kind"] == "Struct" && fields.Any(m => Kind(Child(m, "wire")) != "scalar"))
                return "";
            if (PortableManifest.Objects(c, "cases").Any(k => !contracts.TryGetValue(PortableManifest.Normalize((string)k["type"]!), out var target)
                || (string?)target["kind"] != "Record"))
                return "";
        }
        int profile = (int?)schema["binaryProfile"] ?? 2;
        var settings = root["profile"]!;
        int Bound(string name, int maximum) => (int?)settings[name] is >= 0 and var bound ? Math.Min(bound, maximum) : maximum;
        int jsonDepth = (int?)settings["jsonDepth"] ?? 1024;
        string limits = "limits" + rootName;
        void Line(string s) => outp.AppendLine(s);
        Line($"var {limits} = luminpack.GeneratedLimits{{Input:{((bool?)settings["trusted"] == true ? int.MaxValue : Bound("maxInput", int.MaxValue))}, Collection:{Bound("maxCollection", int.MaxValue)}, String:{Bound("maxString", int.MaxValue)}, Depth:{Bound("maxDepth", 512)}, JSONDepth:{(jsonDepth > 0 ? Math.Min(jsonDepth, 1024) : 1024)}, Complete:{((bool?)schema["requireCompleteBinaryInput"] != false ? "true" : "false")}}}");
        foreach (var c in contracts.Values)
        {
            string n = Symbol(c);
            if ((string?)c["kind"] == "Union")
            {
                Line($"type {n} interface {{ is{n}() }}");
                foreach (var item in PortableManifest.Objects(c, "cases"))
                    Line($"func (*{Symbol(contracts[PortableManifest.Normalize((string)item["type"]!)])}) is{n}() {{}}");
            }
            else
            {
                Line($"type {n} struct {{");
                foreach (var m in PortableManifest.Objects(c, "members"))
                    Line("\t" + Field(m) + " " + Type(Child(m, "wire")));
                Line("}");
            }
        }
        foreach (var w in shapes.Values)
        {
            string t = Type(w), kind = Kind(w), name = Shape(w);
            foreach (bool json in new[] { false, true })
            {
                string pre = json ? "JSON" : "", read = "read" + pre, write = "write" + pre;
                string r = "*luminpack.Document" + pre + "Reader", wr = "*luminpack.Document" + pre + "Writer";
                Line($"func {Function(read, w)}(r {r}, d int) {t} {{");
                bool block = !json && kind == "sequence" && Child(w, "element")["kind"]?.GetValue<string>() == "scalar"
                    && (string?)w["element"]?["type"] != "bool";
                if (block)
                    Line($"return luminpack.ReadDocumentScalars[{Type(Child(w, "element"))}](r)");
                else if (kind is "scalar" or "string")
                    Line($"return r.{name}({(!json && kind == "string" ? profile.ToString() : "")})");
                else
                {
                    bool dict = kind == "dictionary", obj = dict && Kind(Child(w, "key")) == "string";
                    string open = obj ? "{" : "[", close = obj ? "}" : "]";
                    if (json)
                    {
                        if (obj)
                            Line($"if r.Null() {{return nil}};luminpack.DocumentDepth(d);pairs:=r.Accept('[');close:=byte(']');if !pairs {{ r.Expect('{{');close='}}' }};v:=make({t},0);if r.Accept(close){{return v}}");
                        else
                            Line($"if r.Null() {{return nil}}; luminpack.DocumentDepth(d); r.Expect('{open}'); v:=make({t},0); if r.Accept('{close}') {{return v}}");
                        Line("for { r.CheckCount(len(v))");
                    }
                    else
                    {
                        var child = Child(w, dict ? "key" : "element");
                        int Minimum(JsonObject wire) => Kind(wire) switch
                        {
                            "scalar" => scalarTypes[(string)wire["type"]!].Width,
                            "contract" when (string?)contracts[PortableManifest.Normalize((string)wire["type"]!)]["kind"] == "Struct" => 0,
                            _ => 1
                        };
                        int minimum = Minimum(child) + (dict ? Minimum(Child(w, "value")) : 0);
                        Line($"count:=r.Count({minimum}); if count<0 {{return nil}}; v:=make({t},0,min(count,256))");
                        if (dict)
                            Line($"seen:=make(map[{(obj ? "string" : Type(child))}]struct{{}},min(count,256))");
                        Line("for range count {");
                    }
                    if (dict)
                    {
                        var k = Child(w, "key");
                        var val = Child(w, "value");
                        if (json && obj)
                            Line("valueDepth:=d-1;if pairs {luminpack.DocumentDepth(valueDepth);valueDepth--;r.Expect('[')}");
                        if (json && !obj)
                            Line("luminpack.DocumentDepth(d-1); r.Expect('[')");
                        if (json && !obj)
                            Line("r.RequireKey()");
                        Line($"key:={Function(read, k)}(r,d-{(json ? 1 : 0)})");
                        if (obj)
                            Line("if !key.Present {luminpack.DocumentError(\"null dictionary key\")}");
                        if (json && obj)
                            Line("if pairs {r.Expect(',')}else{r.Expect(':')}");
                        else if (json)
                            Line("r.Expect(',')");
                        else
                        {
                            Line($"identity:={(obj ? "key.Identity()" : "key")}; if _,dup:=seen[identity];dup {{luminpack.DocumentError(\"duplicate dictionary key\")}};seen[identity]=struct{{}}{{}}");
                        }
                        Line($"v=append(v,luminpack.TypedEntry[{Type(k)},{Type(val)}]{{Key:key,Value:{Function(read, val)}(r,{(json && obj ? "valueDepth" : json ? "d-2" : "d")})}})");
                        if (json && obj)
                            Line("if pairs {r.Expect(']')}");
                        else if (json)
                            Line("r.Expect(']')");
                    }
                    else
                        Line($"v=append(v,{Function(read, Child(w, "element"))}(r,d-{(json ? 1 : 0)}))");
                    if (json)
                        Line("if !r.Accept(','){break}");
                    Line("}");
                    if (json)
                        Line(obj ? "r.Expect(close)" : $"r.Expect('{close}')");
                    Line("return v");
                }
                Line("}");
                Line($"func {Function(write, w)}(w {wr}, v {t}, d int) {{");
                if (block)
                    Line("luminpack.WriteDocumentScalars(w,v)");
                else if (kind is "scalar" or "string")
                    Line($"w.{name}(v{(!json && kind == "string" ? "," + profile : "")})");
                else
                {
                    bool dict = kind == "dictionary", obj = dict && Kind(Child(w, "key")) == "string";
                    if (!json && dict)
                        Line($"seen:=make(map[{(obj ? "string" : Type(Child(w, "key")))}]struct{{}},len(v))");
                    if (json)
                        Line($"if v==nil {{w.Raw(\"null\");return}};luminpack.DocumentDepth(d);w.Raw(\"{(obj ? "{" : "[")}\")");
                    else
                        Line("if v==nil {w.Count(-1);return};w.Count(len(v))");
                    Line(json ? "for i,x:=range v {if i>0 {w.Raw(\",\")}" : "for _,x:=range v {");
                    if (dict)
                    {
                        if (obj)
                            Line("if !x.Key.Present {luminpack.DocumentError(\"null dictionary key\")}");
                        if (!json)
                            Line($"identity:={(obj ? "x.Key.Identity()" : "x.Key")};if _,dup:=seen[identity];dup{{luminpack.DocumentError(\"duplicate dictionary key\")}};seen[identity]=struct{{}}{{}}");
                        if (json && !obj)
                            Line("luminpack.DocumentDepth(d-1);w.Raw(\"[\")");
                        Line($"{Function(write, Child(w, "key"))}(w,x.Key,d-{(json ? 1 : 0)})");
                        if (json)
                            Line($"w.Raw(\"{(obj ? ":" : ",")}\")");
                        Line($"{Function(write, Child(w, "value"))}(w,x.Value,d-{(json ? (obj ? 1 : 2) : 0)})");
                        if (json && !obj)
                            Line("w.Raw(\"]\")");
                    }
                    else
                        Line($"{Function(write, Child(w, "element"))}(w,x,d-{(json ? 1 : 0)})");
                    Line("}");
                    if (json)
                        Line($"w.Raw(\"{(obj ? "}" : "]")}\")");
                }
                Line("}");
            }
        }
        foreach (var c in contracts.Values)
        {
            var members = PortableManifest.Objects(c, "members").ToArray();
            string n = Symbol(c), t = Model(c);
            bool record = (string?)c["kind"] == "Record", union = (string?)c["kind"] == "Union";
            string zero = record ? "new(" + n + ")" : n + "{}";
            foreach (bool json in new[] { false, true })
            {
                string pre = json ? "JSON" : "", read = "read" + pre, write = "write" + pre;
                string r = "*luminpack.Document" + pre + "Reader", wr = "*luminpack.Document" + pre + "Writer";
                Line($"func {read}{n}(r {r}, d int) {t} {{");
                if (union)
                {
                    if (json)
                    {
                        Line("if r.Null(){return nil};luminpack.DocumentDepth(d)");
                        Line("if r.Match(\"{\\\"$type\\\":\") {tag:=r.Tag(); if r.Match(\",\\\"$value\\\":\") {v:=" + read + n + "Case(r,tag,d-1);r.FinishUnion(d-1);return v};return " + read + n + "Envelope(r,tag,true,d)}");
                        Line("r.Expect('{');return " + read + n + "Envelope(r,0,false,d)");
                    }
                    else
                        Line("luminpack.DocumentDepth(d);tag:=r.Tag();if tag==0{return nil};return " + read + n + "Case(r,tag,d-1)");
                    Line("}");
                    if (json)
                    {
                        Line($"func {read}{n}Envelope(r {r},tag uint32,hasTag bool,d int) {t} {{");
                        Line("var payload *luminpack.DocumentJSONReader; if hasTag {r.Expect(',')};for {key:=r.Name();r.Expect(':');switch key{case \"$type\":if hasTag{r.Skip(d-1)}else{tag=r.Tag();hasTag=true};case \"$value\":if payload==nil{payload=r.Capture(d-1)}else{r.Skip(d-1)};default:r.Skip(d-1)};if !r.Accept(','){break}};r.Expect('}');if !hasTag||payload==nil{luminpack.DocumentError(\"incomplete union\")};return " + read + n + "Case(payload,tag,d-1)}");
                    }
                    Line($"func {read}{n}Case(r {r},tag uint32,d int) {t} {{switch tag {{");
                    foreach (var k in PortableManifest.Objects(c, "cases"))
                    {
                        string child = Symbol(contracts[PortableManifest.Normalize((string)k["type"]!)]);
                        Line($"case {k["hash"]}:v:={read}{child}{(json ? "" : "Body")}(r,d);if v==nil{{return nil}};return v");
                    }
                    Line("};luminpack.DocumentError(\"unknown union tag\");return nil");
                }
                else
                {
                    if (json)
                    {
                        if (record)
                            Line("if r.Null(){return nil}");
                        Line($"luminpack.DocumentDepth(d); v:={zero};r.Expect('{{')");
                        foreach (var m in members.Select((value, index) => (value, index)))
                        {
                            string head = (m.index == 0 ? "" : ",") + ("\"" + (string)m.value["name"]! + "\"") + ":";
                            Line($"if !r.Match({Quote(head)}) {{goto field{m.index}}};v.{Field(m.value)}={Function(read, Child(m.value, "wire"))}(r,d-1)");
                        }
                        Line(members.Length == 0 ? "if r.Accept('}') {return v};goto fields" : "if r.Accept('}') {return v};r.Expect(',');goto fields");
                        for (int i = 0; i < members.Length; i++)
                        {
                            Line($"field{i}: if r.Accept('}}'){{return v}}");
                            if (i != 0)
                                Line("r.Expect(',')");
                            Line("goto fields");
                        }
                        Line("fields: for {key:=r.Name();r.Expect(':');switch key {");
                        var names = members.ToDictionary(m => (string)m["name"]!, m => m, StringComparer.Ordinal);
                        foreach (var m in members)
                            foreach (var alias in m["oldNames"] as JsonArray ?? [])
                                names.TryAdd((string)alias!, m);
                        foreach (var m in members)
                            Line($"case {string.Join(", ", names.Where(pair => pair.Value == m).Select(pair => Quote(pair.Key)))}:v.{Field(m)}={Function(read, Child(m, "wire"))}(r,d-1)");
                        Line("default:r.Skip(d-1)};if !r.Accept(','){break}};r.Expect('}');return v");
                    }
                    else
                    {
                        if (record)
                            Line($"luminpack.DocumentDepth(d);if !r.Header({members.Length}){{return nil}}");
                        Line($"return {read}{n}Body(r,d)}}");
                        Line($"func {read}{n}Body(r {r}, d int) {t} {{luminpack.DocumentDepth(d);v:={zero}");
                        foreach (var m in members)
                            Line($"v.{Field(m)}={Function(read, Child(m, "wire"))}(r,d-1)");
                        Line("return v");
                    }
                }
                Line("}");
                Line($"func {write}{n}(w {wr},v {t},d int) {{");
                if (union)
                {
                    Line("luminpack.DocumentDepth(d);switch x:=v.(type){case nil:");
                    Line(json ? "w.Raw(\"null\")" : "w.Tag(0)");
                    foreach (var k in PortableManifest.Objects(c, "cases"))
                    {
                        string child = Symbol(contracts[PortableManifest.Normalize((string)k["type"]!)]);
                        Line($"case *{child}: if x==nil{{luminpack.DocumentError(\"nil union payload\")}}");
                        if (json)
                            Line("w.Raw(" + Quote("{\"$type\":" + k["hash"] + ",\"$value\":") + ");" + write + child + "(w,x,d-1);w.Raw(\"}\")");
                        else
                            Line($"w.Tag({k["hash"]});{write}{child}Body(w,x,d-1)");
                    }
                    Line("default:luminpack.DocumentError(\"unsupported union case\")}");
                }
                else
                {
                    if (record)
                        Line(json ? "if v==nil {w.Raw(\"null\");return}" : $"luminpack.DocumentDepth(d);if v==nil {{w.Header(255);return}};w.Header({members.Length})");
                    if (!json)
                    { Line($"{write}{n}Body(w,v,d)}}"); Line($"func {write}{n}Body(w {wr},v {t},d int) {{"); }
                    Line("luminpack.DocumentDepth(d)");
                    foreach (var m in members.Select((value, index) => (value, index)))
                    {
                        if (json)
                            Line("w.Raw(" + Quote((m.index == 0 ? "{" : ",") + ("\"" + (string)m.value["name"]! + "\"") + ":") + ")");
                        Line($"{Function(write, Child(m.value, "wire"))}(w,v.{Field(m.value)},d-1)");
                    }
                    if (json)
                        Line(members.Length == 0 ? "w.Raw(\"{}\")" : "w.Raw(\"}\")");
                }
                Line("}");
            }
        }
        foreach (bool json in new[] { false, true })
        {
            string pre = json ? "JSON" : "", api = json ? "JSON" : "Binary", n = Symbol(root), t = Model(root);
            Line($"func Decode{rootName}{api}(data []byte) ({t},error) {{return luminpack.Read{pre}Document(data,{limits},read{pre}{n})}}");
            Line($"func Encode{rootName}{api}(v {t}) ([]byte,error) {{return luminpack.Write{pre}Document(v,{limits}.{(json ? "JSONDepth" : "Depth")},write{pre}{n})}}");
        }
        return outp.ToString();
    }
}
