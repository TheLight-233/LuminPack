"""Emit direct Rust or CPython binary DTO operations from a projected manifest."""

import argparse
import json
import re
from pathlib import Path

SCALARS = {
    "int": ("i32", "int", "integer", 4),
    "long": ("i64", "long", "wide", 8),
    "float": ("f32", "float", "single", 4),
    "byte": ("u8", "byte", "byte", 1),
    "bool": ("bool", "boolean", "boolean", 1),
}


def symbol(name):
    return re.sub(r"[^a-zA-Z0-9]", lambda m: f"_x{ord(m[0]):x}_", name)


def check(condition, reason):
    if not condition:
        raise ValueError(reason)


class Schema:
    def __init__(self, manifest):
        self.root = manifest["root"]
        self.contracts = {
            c["type"].replace("global::", ""): c for c in manifest["contracts"]
        }
        self.shapes = {}
        self.names = {t: "Model" + symbol(c["name"]) for t, c in self.contracts.items()}
        check(len(set(self.names.values())) == len(self.names), "Ambiguous type names")
        check(
            manifest.get("binaryProfile") == 2 and self.root in self.contracts,
            "Expected a UTF-16 Length record projection",
        )
        profile = self.contracts[self.root]["profile"]
        self.limits = [
            profile.get(key, 2147483647)
            for key in ("maxInput", "maxCollection", "maxString")
        ]
        self.complete = manifest.get("requireCompleteBinaryInput", True)
        layouts = {v["type"]: v["fields"] for v in manifest.get("valueLayouts", [])}
        for t, c in self.contracts.items():
            check(
                c["kind"] in ("Union", "Record", "Struct")
                and not any(
                    c.get(k)
                    for k in (
                        "versioned",
                        "storage",
                        "nativeStorage",
                        "sequenceBase",
                        "sequenceWire",
                        "collectionBase",
                        "unionWideTags",
                    )
                )
                and c.get("binaryMemberCount") is None,
                "Requires document codec: " + t,
            )
            self.profile(c["profile"])
            fields = c["members"]
            check(len(fields) <= 249, "Too many fields")
            check(
                len({m["name"] for m in fields}) == len(fields),
                "Ambiguous member names",
            )
            if c["kind"] == "Struct":
                check(
                    c.get("storageKind") == "RawUnmanaged"
                    and fields
                    and layouts.get(t) == [m["name"] for m in fields]
                    and all(
                        m["wire"]["kind"] == "scalar"
                        and m["wire"]["type"] == fields[0]["wire"]["type"]
                        for m in fields
                    ),
                    "Expected uniform raw storage",
                )
            for m in fields:
                check(
                    not m.get("compressed") and not m.get("fixedNullable"),
                    "Unsupported member framing",
                )
                self.profile(m["profile"])
                self.shape(m["wire"])
            for case in c.get("cases") or []:
                check(
                    self.contracts[case["type"].replace("global::", "")]["kind"]
                    == "Record",
                    "Expected record case",
                )
            if c["kind"] == "Union":
                tags = [case["hash"] for case in c["cases"]]
                check(
                    tags
                    and len(set(tags)) == len(tags)
                    and all(0 < tag <= 0xFFFFFFFF for tag in tags),
                    "Invalid static union cases",
                )
        # Owned recursive values and graphs with a finite declared depth use the document codec.
        depths = {}

        def visit(t, path):
            check(
                t not in path and len(path) < 64,
                "Recursive or deeply nested projection",
            )
            if t in depths:
                return depths[t]
            c = self.contracts[t]

            def walk(w):
                if w["kind"] == "contract":
                    return visit(w["type"].replace("global::", ""), path | {t})
                return 1 + max(
                    (walk(w[k]) for k in ("element", "key", "value") if k in w),
                    default=0,
                )

            children = [walk(m["wire"]) for m in c["members"]]
            children.extend(
                visit(case["type"].replace("global::", ""), path | {t})
                for case in c.get("cases") or []
            )
            depths[t] = 1 + max(children, default=0)
            return depths[t]

        check(visit(self.root, set()) <= 64, "Deeply nested projection")

    def profile(self, p):
        check(
            not p.get("trackReferences")
            and p.get("binary") == 2
            and p.get("maxDepth", 512) >= 512,
            "Requires tracked, depth-limited or non-default document codec",
        )
        check(
            not p.get("bound")
            and self.limits
            == [
                p.get(k, 2147483647) for k in ("maxInput", "maxCollection", "maxString")
            ]
            and all(0 <= n <= 2147483647 for n in self.limits),
            "Unsupported local limits",
        )

    def shape(self, w):
        k = w["kind"]
        check(
            not any(
                w.get(a)
                for a in (
                    "layout",
                    "profile",
                    "nullableLayout",
                    "rawCompoundStorage",
                    "required",
                    "memory",
                )
            ),
            "Unsupported storage override",
        )
        if k == "contract":
            return self.names[w["type"].replace("global::", "")]
        if k == "scalar":
            check(w["type"] in SCALARS, "Unsupported scalar")
            n = w["type"]
        elif k == "string":
            n = "text"
        elif k == "sequence":
            check(
                w.get("family")
                not in (
                    "System.Memory",
                    "System.ReadOnlyMemory",
                    "System.Buffers.ReadOnlySequence",
                ),
                "Memory framing",
            )
            n = "list_" + self.shape(w["element"])
        elif k == "dictionary":
            check(
                w["key"]["kind"] == "string"
                or w["key"].get("type") in ("int", "long", "byte"),
                "Unsupported key",
            )
            n = "dict_" + self.shape(w["key"]) + "_" + self.shape(w["value"])
        else:
            raise ValueError("Unsupported wire kind: " + k)
        self.shapes[n] = w
        return n

    def width(self, w):
        if w["kind"] == "scalar":
            return SCALARS[w["type"]][3]
        if w["kind"] == "contract":
            c = self.contracts[w["type"].replace("global::", "")]
            if c["kind"] == "Struct":
                return sum(self.width(m["wire"]) for m in c["members"])
            return 4 if c["kind"] == "Union" else 1
        return 4

    def rust_type(self, w):
        k = w["kind"]
        if k == "scalar":
            return SCALARS[w["type"]][0]
        if k == "contract":
            t = w["type"].replace("global::", "")
            return (
                self.names[t]
                if self.contracts[t]["kind"] == "Struct"
                else f"Option<{self.names[t]}>"
            )
        if k == "string":
            return "Option<String>"
        if k == "sequence":
            return f"Option<Vec<{self.rust_type(w['element'])}>>"
        v = self.rust_type(w["value"])
        if w["key"]["kind"] == "string":
            return f"Option<std::collections::HashMap<String,{v}>>"
        return f"Option<Vec<({self.rust_type(w['key'])},{v})>>"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("language", choices=["rust", "python"])
    parser.add_argument("projection", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    schema = Schema(json.loads(args.projection.read_bytes()))
    if args.language == "rust":
        from rust import emit
    else:
        from python_model import emit
    args.output.write_text(emit(schema), encoding="utf-8")


if __name__ == "__main__":
    main()
