import assert from "node:assert/strict";
import test from "node:test";
import { createCodec, WireError } from "../dist/index.js";

const scalar = type => ({ kind: "scalar", type });
const vector = element => ({ kind: "sequence", element });
const profile = { binary: 2, declared: false, jsonEncoding: 1 };
const encode = value => new TextEncoder().encode(value);

function record(members, options = {}) {
    return createCodec({
        version: 1, root: "TestRecord", requireCompleteBinaryInput: true,
        contracts: [{ id: "TestRecord", kind: "Record", profile, members }],
        ...options,
    });
}

test("encodes the same checked primitive block from aligned and unaligned byte slices", () => {
    const codec = record([
        { name: "Ints", wire: vector(scalar("int")) },
        { name: "Longs", wire: vector(scalar("long")) },
        { name: "Booleans", wire: vector(scalar("bool")) },
        { name: "Units", wire: vector(scalar("char")) },
    ]);
    const value = {
        Ints: [-2147483648, 0, 2147483647],
        Longs: [-9223372036854775808n, 0n, 9223372036854775807n],
        Booleans: [true, false, true],
        Units: ["A", "\ud800", "\uffff"],
    };
    const bytes = codec.writeBinary(value);
    for (let offset = 0; offset < 8; offset++) {
        const padded = new Uint8Array(bytes.length + offset);
        padded.set(bytes, offset);
        assert.deepEqual(codec.readBinary(padded.subarray(offset)), value);
    }
    for (let length = 0; length < bytes.length; length++) assert.throws(() => codec.readBinary(bytes.subarray(0, length)), WireError);
    const incorrect = bytes.slice();
    incorrect[1 + 4 + 3 * 4 + 4 + 3 * 8 + 4] = 2;
    assert.throws(() => codec.readBinary(incorrect), /Boolean marker/);
    assert.throws(() => codec.writeBinary({ ...value, Ints: [2147483648] }), WireError);
});

test("keeps 64-bit JSON lexemes and both native integer widths", () => {
    for (const nativeIntegerBytes of [4, 8]) {
        const codec = record([
            { name: "Wide", wire: scalar("ulong") },
            { name: "Native", wire: vector(scalar("nint")) },
        ], { nativeIntegerBytes });
        const value = { Wide: 18446744073709551615n, Native: nativeIntegerBytes === 8 ? [2147483648n] : [-2147483648] };
        for (const encoding of ["utf8", "utf16"])
            assert.deepEqual(codec.readJson(codec.writeJson(value, encoding), encoding), value);
        assert.deepEqual(codec.readBinary(codec.writeBinary(value)), value);
        assert.throws(() => codec.readJson(encode('{"Wide":18446744073709551616}'), "utf8"), WireError);
    }
});

test("applies LUM2 to the contiguous leaf of a jagged collection", () => {
    const codec = record([{ name: "Values", wire: vector(vector(scalar("int"))), compressed: true }]);
    const value = { Values: [null, [], Array(900).fill(733)] };
    const bytes = codec.writeBinary(value);
    assert.ok(bytes.length < 300);
    assert.deepEqual(codec.readBinary(bytes), value);
    assert.deepEqual(codec.readJson(codec.writeJson(value, "utf8"), "utf8"), value);
    const corrupted = bytes.slice();
    const firstFrame = 1 + 4 + 4 + 4 + 4 + 4;
    corrupted[firstFrame] ^= 1;
    assert.throws(() => codec.readBinary(corrupted), /compressed field/);
});

test("keeps isolated UTF-16 units in unaligned binary length records", () => {
    const codec = record([{ name: "Text", wire: { kind: "string" } }]);
    const text = "\ufeff" + "文字😀".repeat(200) + "\ud800";
    assert.deepEqual(codec.readBinary(codec.writeBinary({ Text: text })), { Text: text });
});

test("writes collection roots with their own count and without record framing", () => {
    const codec = createCodec({ version: 1, root: "int[]", rootWire: vector(scalar("int")), contracts: [] });
    const binary = Uint8Array.of(2, 0, 0, 0, 7, 0, 0, 0, 255, 255, 255, 255);
    const value = [7, -1];
    assert.deepEqual(codec.readBinary(binary), value);
    assert.deepEqual(codec.writeBinary(value), binary);
    assert.deepEqual(codec.writeJson(value, "utf8"), encode("[7,-1]"));
});

test("selects Native nullable alignment independently of native integer width", () => {
    const wire = vector({
        kind: "nullable", element: scalar("long"), nullableEncoding: 2,
        nullableLayout: { wireSize: 9, tagged: false, modelSizes: [16, 12],
            modelOffsets: [[0, 8, 9, 10, 11, 12, 13, 14, 15], [0, 4, 5, 6, 7, 8, 9, 10, 11]] },
    });
    for (const nativeScalarAlignment of [4, 8]) {
        const codec = record([{ name: "Values", wire }], { nativeIntegerBytes: 4, nativeScalarAlignment });
        const value = { Values: [100n, null] };
        const binary = codec.writeBinary(value);
        assert.equal(binary.length, 5 + 2 * (nativeScalarAlignment + 8));
        assert.deepEqual(codec.readBinary(binary), value);
    }
});
