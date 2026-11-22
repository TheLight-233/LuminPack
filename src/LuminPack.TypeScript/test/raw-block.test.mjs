import assert from "node:assert/strict";
import test from "node:test";
import { BinaryReader, BinaryWriter, readString, writeString } from "../dist/binary.js";
import { createCodec, WireError } from "../dist/index.js";

const int = { kind: "scalar", type: "int" };
const longs = { kind: "scalar", type: "long" };
const sequence = element => ({ kind: "sequence", element });

test("transfers exact typed-array slices through the ordinary and compressed Binary entries", () => {
    const codec = createCodec({ version: 1, root: "Raw", contracts: [{
        id: "Raw", kind: "Record", profile: { binary: 2 }, members: [
            { name: "Ints", wire: sequence(int) },
            { name: "Longs", wire: sequence(longs), compressed: true },
        ],
    }] });
    const ints = Int32Array.of(99, -0x8000_0000, 0, 0x7fff_ffff, 99).subarray(1, 4);
    const wide = new BigInt64Array(257).fill(-12345678912345n).subarray(1);
    const values = { Ints: [...ints], Longs: [...wide] };
    const binary = codec.writeBinary({ Ints: ints, Longs: wide });
    assert.deepEqual(binary, codec.writeBinary(values));
    assert.deepEqual(codec.readBinary(binary), values);
    assert.throws(() => codec.writeBinary({ Ints: Float32Array.of(1), Longs: wide }), WireError);
});

test("keeps the fixed UTF-16 Length framing at every short and medium bound", () => {
    for (let length = 0; length <= 65; length++) {
        for (const unit of ["\u00ff", "\u0100", "\ud800"]) {
            const writer = new BinaryWriter(1);
            writer.u8(0xaa);
            const value = unit.repeat(length);
            writeString(writer, value, 2);
            const bytes = writer.finish();
            const latin1 = length !== 0 && unit === "\u00ff";
            assert.equal(bytes.length, 5 + length * (latin1 ? 1 : 2));
            const source = new Uint8Array(bytes.length + 5);
            source.set(bytes, 3);
            const reader = new BinaryReader(source.subarray(3, 3 + bytes.length));
            assert.equal(reader.u8(), 0xaa);
            assert.equal(readString(reader, 2), value);
            reader.end();
            if (length > 0) {
                const truncated = new BinaryReader(bytes.subarray(1, -1));
                assert.throws(() => readString(truncated, 2), WireError);
            }
        }
    }
});
