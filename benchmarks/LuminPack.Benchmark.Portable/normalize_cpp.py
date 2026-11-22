"""Join native C++ process medians into the portable benchmark CSV schema."""

import argparse
import csv
from pathlib import Path


MODELS = {"SimpleClass": "simple", "CharacterSaveData": "serialize"}
FORMATS = {"binary": "binary", "json-utf8": "json"}
OPERATIONS = {"write": "encode", "read": "decode"}
LIBRARIES = {"luminpack": "LuminPack", "struct_pack": "struct_pack", "bitsery": "Bitsery", "cereal": "cereal",
             "msgpack-cxx": "MessagePack-C++", "rapidjson": "RapidJSON", "simdjson": "simdjson",
             "jsoncons": "jsoncons", "nlohmann-json": "nlohmann/json"}
HEADER = ("language", "model", "format", "library", "operation", "sample", "ns_per_op", "bytes", "alloc_bytes_per_op")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("runs", nargs="+", type=Path)
    args = parser.parse_args()
    rows = []
    expected = None
    for sample, run in enumerate(args.runs, 1):
        results = set()
        with run.open(encoding="utf-8-sig", newline="") as source:
            for row in csv.DictReader(source):
                if row["format"] == "json-utf16":
                    continue
                key = (MODELS[row["model"]], FORMATS[row["format"]], LIBRARIES[row["library"]],
                       OPERATIONS[row["direction"]])
                if key in results:
                    raise ValueError(f"Repeated measurement in {run}: {key}")
                results.add(key)
                rows.append(("cpp", *key, sample, row["median_ns"], row["wire_bytes"], row["allocated_bytes"]))
        if not results or (expected is not None and results != expected):
            raise ValueError(f"Inconsistent C++ benchmark rows in {run}")
        expected = results
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="") as output:
        writer = csv.writer(output)
        writer.writerow(HEADER)
        writer.writerows(rows)


if __name__ == "__main__":
    main()
