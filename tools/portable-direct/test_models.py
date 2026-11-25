"""Compile a small CPython codec and exercise framing and schema rejection."""

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from generate import Schema


def manifest():
    profile = {"binary": 2, "maxCollection": 2, "maxString": 8}
    scalar = {"kind": "scalar", "type": "int"}
    dictionary = {"kind": "dictionary", "key": scalar, "value": scalar}
    return {
        "binaryProfile": 2,
        "root": "Message",
        "contracts": [
            {
                "kind": "Record",
                "type": "Message",
                "name": "Message",
                "profile": profile,
                "members": [
                    {"name": "text", "profile": profile, "wire": {"kind": "string"}},
                    {"name": "pairs", "profile": profile, "wire": dictionary},
                ],
            }
        ],
    }


class ModelTests(unittest.TestCase):
    def test_document_only_contracts(self):
        for feature in ("versioned", "unionWideTags", "storage"):
            projection = manifest()
            projection["contracts"][0][feature] = True
            with self.assertRaises(ValueError):
                Schema(projection)
        projection = manifest()
        projection["contracts"][0]["members"][0]["wire"] = {
            "kind": "contract",
            "type": "Message",
        }
        with self.assertRaises(ValueError):
            Schema(projection)

    def test_compiled_wire_contract(self):
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            projection = work / "projection.json"
            projection.write_text(json.dumps(manifest()), encoding="utf-8")
            subprocess.run(
                [
                    sys.executable,
                    str(Path(__file__).with_name("build_python.py")),
                    str(projection),
                    "message",
                    temporary,
                ],
                check=True,
            )
            # Import in a child so Windows can release the extension before cleanup.
            subprocess.run(
                [
                    sys.executable,
                    "-c",
                    """
import message
def rejected(f, *args):
    try: f(*args)
    except ValueError: return
    raise AssertionError(args)
value = {"text": chr(0xd800), "pairs": [[3, 7]]}
wire = bytes.fromhex("02 02000000 00d8 01000000 03000000 07000000")
assert message.encode_binary(value) == wire
assert message.decode_binary(wire) == value
assert message.serialize(value) == wire
assert message.deserialize(wire) == value
assert message.decode_binary(bytes([255])) is None
for i in range(len(wire)):
    rejected(message.decode_binary, wire[:i])
rejected(message.decode_binary, wire + bytes(1))
rejected(message.encode_binary, dict(value, pairs=[[3,7],[3,8]]))
rejected(message.decode_binary, wire[:7] + bytes.fromhex("02000000") + wire[-8:] * 2)
rejected(message.decode_binary, message.encode_binary(dict(value, text="123456789")))
rejected(message.decode_binary, message.encode_binary(dict(value, pairs=[[1,1],[2,2],[3,3]])))
""",
                ],
                cwd=work,
                check=True,
            )


if __name__ == "__main__":
    unittest.main()
