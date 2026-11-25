import assert from "node:assert/strict";
import test from "node:test";
import { createCodec, WireError } from "../dist/index.js";

const profile = { binary: 2, declared: false, jsonEncoding: 1 };
const int = { kind: "scalar", type: "int" };
const string = { kind: "string" };
const utf8 = text => Buffer.from(text, "utf8");

function codec(members, options) {
    return createCodec({
        version: 1, root: "Record",
        contracts: [{ id: "Record", kind: "Record", profile, members }],
    }, options);
}

test("uses the same complete heads for ordered UTF-8 and UTF-16 records", () => {
    const model = codec([
        { name: "First", wire: int },
        { name: "Second", wire: int },
        { name: "Third", wire: int },
        { name: "Title", wire: string },
    ]);
    const value = { First: -2147483648, Second: 2147483647, Third: 0, Title: "文字😀" };
    const canonical = '{"First":-2147483648,"Second":2147483647,"Third":0,"Title":"文字😀"}';
    for (const [encoding, bufferEncoding] of [["utf8", "utf8"], ["utf16", "utf16le"]]) {
        const input = Buffer.from(canonical, bufferEncoding);
        assert.deepEqual(model.readJson(input, encoding), value);
        assert.deepEqual(Buffer.from(model.writeJson(value, encoding)), input);
    }
});

test("falls back for aliases, escaping, whitespace and shadowed fields", () => {
    const model = codec([
        { name: "Id", wire: int, oldNames: ["OldId"] },
        { name: "Name", wire: string },
    ]);
    const input = utf8(' { "N\\u0061me": "\\u6587", "OldId": 17, "Unknown": [1] } ');
    assert.deepEqual(model.readJson(input, "utf8"), { Id: 17, Name: "文" });
    assert.deepEqual(model.readJson(utf8('{"Id":"shadowed","Name":"x","Id":-0}'), "utf8"), { Id: -0, Name: "x" });
});

test("keeps the general JSON number grammar and checked array quotas", () => {
    const model = codec([
        { name: "Count", wire: int },
        { name: "Items", wire: { kind: "sequence", element: int } },
    ], { limits: { maxCollection: 2 } });
    assert.deepEqual(model.readJson(utf8('{"Count":1,"Items":[-7,0]}'), "utf8"), { Count: 1, Items: [-7, 0] });
    for (const input of [
        '{"Count":2147483648,"Items":[]}', '{"Count":-2147483649,"Items":[]}',
        '{"Count":1e0,"Items":[]}', '{"Count":01,"Items":[]}', '{"Count":-,"Items":[]}',
        '{"Count":1 "Items":[]}', '{"Count":1,"Items":[0,1,2]}', '{"Count":1,"Items":[0,]}',
        '{"Count":1,"Items":[]}{ }',
    ]) assert.throws(() => model.readJson(utf8(input), "utf8"), WireError);
});

test("keeps BigInt dictionaries, numeric key ordering and own prototype keys", () => {
    const model = codec([{ name: "Items", wire: { kind: "dictionary", key: string, value: { kind: "scalar", type: "ulong" } } }]);
    const value = { Items: new Map([["10", 18446744073709551615n], ["__proto__", 9007199254740993n], ["2", 1n]]) };
    const canonical = '{"Items":{"2":1,"10":18446744073709551615,"__proto__":9007199254740993}}';
    assert.deepEqual(model.readJson(utf8(canonical), "utf8"), value);
    assert.equal(Buffer.from(model.writeJson(value, "utf8")).toString("utf8"), canonical);
});

test("writes exact scalar typed views with the same JSON as arrays", () => {
    const model = codec([
        { name: "Ints", wire: { kind: "sequence", element: int } },
        { name: "Longs", wire: { kind: "sequence", element: { kind: "scalar", type: "long" } } },
        { name: "Floats", wire: { kind: "sequence", element: { kind: "scalar", type: "float" } } },
    ]);
    const values = {
        Ints: Int32Array.of(-2147483648, 2147483647),
        Longs: BigInt64Array.of(-9223372036854775808n, 9223372036854775807n),
        Floats: Float32Array.of(-0, 0.1),
    };
    const expected = { Ints: Array.from(values.Ints), Longs: Array.from(values.Longs), Floats: Array.from(values.Floats) };
    for (const encoding of ["utf8", "utf16"]) {
        const bytes = model.writeJson(values, encoding);
        assert.deepEqual(bytes, model.writeJson(expected, encoding));
        assert.deepEqual(model.readJson(bytes, encoding), expected);
    }
    assert.throws(() => model.writeJson({ ...values, Ints: Uint32Array.of(1) }, "utf8"), WireError);
});

test("uses Custom JSON callbacks once when the contract has no reflected members", () => {
    let reads = 0;
    const model = createCodec({
        version: 1, root: "Opaque",
        contracts: [{ id: "Opaque", kind: "Custom", profile, customJson: true }],
    }, { custom: { Opaque: {
        readBinary: input => input.u8(),
        writeBinary: (output, value) => output.u8(value),
        readJson: value => { reads++; return value; },
        writeJson: value => value,
    } } });
    assert.deepEqual(model.readJson(Buffer.from(' "\\u6587" ', "utf16le"), "utf16"), "文");
    assert.equal(reads, 1);
    assert.equal(Buffer.from(model.writeJson("文", "utf16")).toString("utf16le"), '"文"');
});

test("preserves shared references across numeric-looking object keys in wire order", () => {
    const model = createCodec({ version: 1, root: "Map", contracts: [
        { id: "Node", kind: "Record", profile: { ...profile, trackReferences: true }, members: [] },
    ], rootWire: { kind: "dictionary", key: string, value: { kind: "contract", type: "Node" } } });
    const node = {};
    const value = new Map([["10", node], ["2", node], ["__proto__", node]]);
    for (const encoding of ["utf8", "utf16"]) {
        const bytes = model.writeJson(value, encoding);
        assert.equal(Buffer.from(bytes).toString(encoding === "utf8" ? "utf8" : "utf16le"),
            '{"10":{"$id":0},"2":{"$ref":0},"__proto__":{"$ref":0}}');
        const decoded = model.readJson(bytes, encoding);
        assert.deepEqual([...decoded.keys()], [...value.keys()]);
        assert.equal(decoded.get("10"), decoded.get("2"));
        assert.equal(decoded.get("10"), decoded.get("__proto__"));
    }
});
