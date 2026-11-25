"""Generate CPython binary operations over dictionaries and lists."""

from itertools import groupby
from pathlib import Path

from generate import SCALARS


def store_fields(pairs):
    types = {
        "int": "int32_t",
        "long": "int64_t",
        "float": "float",
        "byte": "uint8_t",
        "bool": "uint8_t",
    }
    declarations, body = [], ["if v is None: raise TypeError('Expected record')"]
    for index, (scalar, run) in enumerate(
        groupby(pairs, key=lambda pair: pair[1] in types)
    ):
        fields = list(run)
        if not scalar or len(fields) == 1:
            body.extend(f"wr_{ch}(out,v[{key}])" for key, ch in fields)
            continue
        width = sum(SCALARS[ch][3] for _, ch in fields)
        declarations.append(f"cdef char block{index}[{width}]")
        offset = 0
        for key, ch in fields:
            name = f"field{index}_{offset}"
            declarations.append(f"cdef {types[ch]} {name}")
            body.append(f"x=v[{key}]")
            if ch == "bool":
                body.append("if type(x) is not bool: raise TypeError('Expected bool')")
            elif ch == "float":
                body.append(
                    "if type(x) is not float and (not isinstance(x,(int,float)) or type(x) is bool): raise TypeError('Expected number')"
                )
            else:
                body.append(
                    "if type(x) is not int and (not isinstance(x,int) or type(x) is bool): raise TypeError('Expected integer')"
                )
            body.extend(
                [
                    f"{name}=x",
                    f"memcpy(block{index}+{offset}, &{name}, {SCALARS[ch][3]})",
                ]
            )
            offset += SCALARS[ch][3]
        body.append(f"out.append(block{index},{width})")
    return declarations + body


def emit(s):
    out = [
        "# distutils: language = c++",
        "# cython: language_level=3",
        f"DEF COLLECTION_LIMIT = {s.limits[1]}",
        f"DEF STRING_LIMIT = {s.limits[2]}",
        Path(__file__).with_name("python_runtime.pxi").read_text(encoding="utf-8"),
    ]

    def fn(sig, body):
        out.append(sig + ":\n" + "\n".join("    " + line for line in body))

    for n, w in list(s.shapes.items()):
        k = w["kind"]
        if k == "scalar":
            rd = [f"return r.{SCALARS[n][2]}()"]
            if n == "bool":
                wr = [
                    "if type(v) is not bool: raise TypeError('Expected bool')",
                    "out.push_back(<char>(1 if v else 0))",
                ]
            else:
                wr = [
                    (
                        "if not isinstance(v,(int,float)) or type(v) is bool: raise TypeError('Expected number')"
                        if n == "float"
                        else "if not isinstance(v,int) or type(v) is bool: raise TypeError('Expected integer')"
                    )
                ]
                wr += [
                    "out.push_back(<char><uint8_t>v)"
                    if n == "byte"
                    else f"write_{n}(out,v)"
                ]
        elif k == "string":
            rd = ["return r.text()"]
            wr = ["write_text(out,v)"]
        else:
            dic = k == "dictionary"
            strings = dic and w["key"]["kind"] == "string"
            elem = w["value" if dic else "element"]
            ch = s.shape(elem)
            minimum = s.width(elem) + (s.width(w["key"]) if dic else 0)
            rd = [
                f"cdef int32_t count=r.count({minimum})",
                "cdef Py_ssize_t i",
                "if count == -1: return None",
            ]
            wr = [
                "if v is None:",
                "    write_int(out,-1)",
                "    return",
                "if not isinstance(v,"
                + ("dict" if strings else "(list,tuple)")
                + "): raise TypeError('Invalid collection')",
                "if len(v)>2147483647: raise ValueError('Collection too large')",
                "write_int(out,<int32_t>len(v))",
            ]
            if dic:
                key = s.shape(w["key"])
                rd += ["result=" + ("{}" if strings else "[]")]
                if not strings:
                    rd += ["seen=set()"]
                    wr += ["seen=set()"]
                rd += [
                    "for i in range(count):",
                    f"    key=rd_{key}(r)",
                    "    if key is None: raise ValueError('Null key')",
                    "    if key in "
                    + ("result" if strings else "seen")
                    + ": raise ValueError('Duplicate key')",
                ]
                if strings:
                    rd += [f"    result[key]=rd_{ch}(r)"]
                else:
                    rd += ["    seen.add(key)", f"    result.append([key,rd_{ch}(r)])"]
                wr += [
                    "for key,x in " + ("(<dict>v).items()" if strings else "v") + ":",
                    "    if key is None: raise ValueError('Null key')",
                ]
                if not strings:
                    wr += [
                        "    if key in seen: raise ValueError('Duplicate key')",
                        "    seen.add(key)",
                    ]
                wr += [f"    wr_{key}(out,key)", f"    wr_{ch}(out,x)"]
                rd += ["return result"]
            else:
                rd += [f"return [rd_{ch}(r) for i in range(count)]"]
                wr += ["for x in v:", f"    wr_{ch}(out,x)"]
        fn(f"cdef inline object rd_{n}(Reader r)", rd)
        fn(f"cdef inline void wr_{n}(DirectBytes& out,object v) except *", wr)
    for t, c in s.contracts.items():
        n = s.names[t]
        fields = c["members"]
        record = c["kind"] == "Record"
        if c["kind"] == "Union":
            rd = ["cdef uint32_t tag=<uint32_t>r.integer()", "if tag==0: return None"]
            wr = [
                "cdef uint32_t tag",
                "if v is None:",
                "    write_int(out,0)",
                "    return",
                "raw_tag=v['$type']",
                "if type(raw_tag) is not int: raise TypeError('Expected Union tag')",
                "tag=raw_tag",
            ]
            for x in c["cases"]:
                case = s.names[x["type"].replace("global::", "")]
                h = x["hash"]
                rd += [f"if tag=={h}: return {{'$type':{h},'$value':body_{case}(r)}}"]
                wr += [
                    f"if tag=={h}:",
                    f"    write_int(out,<int32_t><uint32_t>{h})",
                    f"    store_{case}(out,v['$value'])",
                    "    return",
                ]
            rd += ["raise ValueError('Unknown Union tag')"]
            wr += ["raise ValueError('Unknown Union tag')"]
        else:
            pairs = [(repr(m["name"]), s.shape(m["wire"])) for m in fields]
            fn(
                f"cdef inline object body_{n}(Reader r)",
                ["return {" + ",".join(f"{f}:rd_{ch}(r)" for f, ch in pairs) + "}"],
            )
            fn(
                f"cdef inline void store_{n}(DirectBytes& out,dict v) except *",
                store_fields(pairs),
            )
            rd = []
            wr = []
            if record:
                rd = [
                    "cdef uint8_t header=r.byte()",
                    "if header==255: return None",
                    f"if header!={len(fields)}: raise ValueError('Invalid record header')",
                ]
                wr = [
                    "if v is None:",
                    "    out.push_back(<char>255)",
                    "    return",
                    f"out.push_back(<char>{len(fields)})",
                ]
            rd += [f"return body_{n}(r)"]
            wr += [f"store_{n}(out,v)"]
        fn(f"cdef inline object rd_{n}(Reader r)", rd)
        fn(f"cdef inline void wr_{n}(DirectBytes& out,object v) except *", wr)
    root = s.names[s.root]
    rd = [
        f"if data is None or len(data)>{s.limits[0]}: raise ValueError('Invalid binary input')",
        "cdef Reader r=Reader.__new__(Reader)",
        "r.pos=PyBytes_AS_STRING(data)",
        "r.remaining=len(data)",
        f"v=rd_{root}(r)",
    ]
    if s.complete:
        rd += ["if r.remaining: raise ValueError('Trailing bytes')"]
    fn("def decode_binary(bytes data)", rd + ["return v"])
    fn(
        "def encode_binary(value)",
        [
            "global previous_size",
            "cdef DirectBytes out",
            "out.reserve(previous_size)",
            f"wr_{root}(out,value)",
            "result=out.finish()",
            "previous_size=min(len(result),1048576)",
            "return result",
        ],
    )
    fn(
        "def decode_json(codec,bytes data,bytearray binary_scratch,fmt=1)",
        [
            "doc=codec.decode(data,fmt)",
            "size=doc.encode_into(binary_scratch)",
            "return decode_binary(bytes(memoryview(binary_scratch)[:size]))",
        ],
    )
    fn(
        "def encode_json_into(codec,value,bytearray output,fmt=1)",
        ["return codec.decode(encode_binary(value)).encode_into(output,fmt)"],
    )
    return "\n\n".join(out) + "\n"
