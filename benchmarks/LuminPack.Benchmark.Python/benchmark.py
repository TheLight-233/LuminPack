"""Compare generated CPython business model codecs with native Python serializers."""

import csv, json, os, sys, time, random, pickle, importlib
from pathlib import Path
import msgspec, msgpack, orjson, luminpack as lp

models = Path(os.environ["LP_MODELS"])
sys.path.insert(0, str(models))


def normalized(v):
    if isinstance(v, dict):
        return {k: normalized(x) for k, x in v.items()}
    if isinstance(v, list):
        a = [normalized(x) for x in v]
        return (
            sorted(a, key=lambda x: x[0])
            if a
            and all(
                isinstance(x, list) and len(x) == 2 and isinstance(x[0], int) for x in a
            )
            else a
        )
    return v


def run(m):
    path = models
    blob = (path / (m + ".bin")).read_bytes()
    js = (path / (m + ".utf8.json")).read_bytes()
    direct = importlib.import_module(m)
    assert direct.encode_binary(json.loads(js)) == blob
    value = direct.decode_binary(blob)
    expected = normalized(value)
    js = json.dumps(
        value, separators=(",", ":"), ensure_ascii=False, allow_nan=False
    ).encode()
    suffix = (
        ".dll" if os.name == "nt" else ".dylib" if sys.platform == "darwin" else ".so"
    )
    codec = lp.Codec(models / (m + suffix))
    out = bytearray(262144)
    tasks = []
    for fmt, l, e, d in [
        ("binary", "pickle5", lambda v: pickle.dumps(v, protocol=5), pickle.loads),
        ("binary", "msgpack", msgpack.packb, msgpack.unpackb),
        ("binary", "msgspec", msgspec.msgpack.encode, msgspec.msgpack.decode),
        (
            "json",
            "json",
            lambda v: json.dumps(
                v, separators=(",", ":"), ensure_ascii=False, allow_nan=False
            ).encode(),
            json.loads,
        ),
        ("json", "orjson", orjson.dumps, orjson.loads),
        ("json", "msgspec", msgspec.json.encode, msgspec.json.decode),
    ]:
        encoded = e(value)
        assert normalized(d(encoded)) == expected
        b = js if fmt == "json" else encoded
        assert normalized(d(b)) == expected
        tasks.extend(
            [
                (fmt, l, "encode", len(encoded), lambda e=e: e(value)),
                (fmt, l, "decode", len(b), lambda d=d, b=b: d(b)),
            ]
        )
    encoded = direct.encode_binary(value)
    assert encoded == blob, m
    assert normalized(direct.decode_binary(blob)) == expected
    tasks.extend(
        [
            (
                "binary",
                "LuminPack",
                "encode",
                len(encoded),
                lambda: direct.encode_binary(value),
            ),
            (
                "binary",
                "LuminPack",
                "decode",
                len(blob),
                lambda: direct.decode_binary(blob),
            ),
        ]
    )
    assert normalized(direct.decode_json(codec, js, out)) == expected

    def jenc():
        return bytes(memoryview(out)[: direct.encode_json_into(codec, value, out)])

    assert normalized(direct.decode_json(codec, jenc(), out)) == expected
    tasks.extend(
        [
            ("json", "LuminPack", "encode", len(jenc()), jenc),
            (
                "json",
                "LuminPack",
                "decode",
                len(js),
                lambda: direct.decode_json(codec, js, out),
            ),
        ]
    )
    rng = random.Random(57)
    for sample in range(10):
        rng.shuffle(tasks)
        for fmt, l, op, size, func in tasks:
            start = time.perf_counter_ns()
            n = 0
            while time.perf_counter_ns() - start < 30_000_000:
                for _ in range(8):
                    func()
                n += 8
            if sample:
                yield [
                    "python",
                    m,
                    fmt,
                    l,
                    op,
                    sample,
                    (time.perf_counter_ns() - start) / n,
                    size,
                    "",
                ]


writer = csv.writer(sys.stdout, lineterminator="\n")
writer.writerow(
    [
        "language",
        "model",
        "format",
        "library",
        "operation",
        "sample",
        "ns_per_op",
        "bytes",
        "alloc_bytes_per_op",
    ]
)
for model in ["simple", "serialize"]:
    writer.writerows(run(model))
