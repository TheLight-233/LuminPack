import sys, json, re
from pathlib import Path

sys.path.insert(0, "tools/portable-direct")
from generate import Schema, symbol
from rust import emit

p = Path(sys.argv[1])
q = Path(sys.argv[2])
for m in ("simple", "serialize"):
    s = Schema(json.loads((p / f"{m}.manifest.json").read_bytes()))
    src = emit(s)
    for t, c in s.contracts.items():
        n = s.names[t]
        if c["kind"] == "Union":
            cases = ",".join(
                s.names[x["type"].replace("global::", "")] + "=" + str(x["hash"])
                for x in c["cases"]
            )
            src = re.sub(
                "pub enum " + n + r" \{[^}]*\}", f"union_model!({n},{cases});", src
            )
        else:
            src = src.replace(
                "pub struct " + n + "{",
                "#[derive(Serialize,Deserialize)] pub struct " + n + "{",
            )
    names = {f["name"] for c in s.contracts.values() for f in c["members"]}
    for name in names:
        word = "pub f_" + symbol(name) + ":"
        src = src.replace(word, "#[serde(rename=" + json.dumps(name) + ")] " + word)
    (q / (m + ".rs")).write_text("use super::*;\n" + src)
