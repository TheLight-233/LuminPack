import assert from "node:assert/strict";
import test from "node:test";
import { createCodec, WireError } from "../dist/index.js";
import { JsonByteOutput } from "../dist/json-output.js";

const scalar = type => createCodec({ version: 1, contracts: [], root: type, rootWire: { kind: "scalar", type } });
const view = new DataView(new ArrayBuffer(8));

function single(bits) {
    view.setUint32(0, bits);
    return view.getFloat32(0);
}

function double(bits) {
    view.setBigUint64(0, bits);
    return view.getFloat64(0);
}

test("writes exact C# XJB float spellings and notation boundaries", () => {
    const values = [
        [0x00000000, "0"], [0x80000000, "-0"], [0x00000001, "1E-45"],
        [0x39800000, "0.00024414062"], [0x49800002, "1048576.2"], [0xc9800002, "-1048576.2"],
        [0x4e6e6b28, "1000000000"], [0x501502f9, "1E+10"], [0x7f7fffff, "3.4028235E+38"],
    ];
    const codec = scalar("float");
    const output = new JsonByteOutput(8, 8, () => 0);
    for (const [bits, expected] of values) {
        const value = single(bits);
        output.reset();
        output.float(value, true);
        assert.deepEqual(Buffer.from(output.finish()), Buffer.from(expected), bits.toString(16));
        for (const [encoding, bufferEncoding] of [["utf8", "utf8"], ["utf16", "utf16le"]]) {
            const bytes = codec.writeJson(value, encoding);
            assert.deepEqual(Buffer.from(bytes), Buffer.from(expected, bufferEncoding), bits.toString(16));
            assert.equal(codec.readJson(bytes, encoding), value);
        }
    }
});

test("writes exact C# XJB double spellings and decimal exponent signs", () => {
    const values = [
        [double(0x0000000000000001n), "5E-324"], [-0, "-0"], [12.125, "12.125"],
        [1e-9, "0.000000001"], [1e-11, "1E-11"], [9007199254740992, "9007199254740992"],
        [1e17, "1E+17"], [double(0x7fefffffffffffffn), "1.7976931348623157E+308"],
    ];
    const codec = scalar("double");
    for (const [value, expected] of values) {
        for (const [encoding, bufferEncoding] of [["utf8", "utf8"], ["utf16", "utf16le"]]) {
            const bytes = codec.writeJson(value, encoding);
            assert.deepEqual(Buffer.from(bytes), Buffer.from(expected, bufferEncoding));
            assert.equal(codec.readJson(bytes, encoding), value);
        }
    }
});

test("writes exact CLR integer bounds and rejects non-finite floats", () => {
    for (const [type, value, expected] of [
        ["int", -2147483648, "-2147483648"],
        ["uint", 4294967295, "4294967295"],
        ["long", -9223372036854775808n, "-9223372036854775808"],
        ["ulong", 18446744073709551615n, "18446744073709551615"],
    ]) {
        const codec = scalar(type);
        const bytes = codec.writeJson(value, "utf8");
        assert.equal(Buffer.from(bytes).toString("utf8"), expected);
        assert.equal(codec.readJson(bytes, "utf8"), value);
    }
    for (const type of ["float", "double"]) {
        const codec = scalar(type);
        for (const number of [NaN, -Infinity, Infinity]) {
            assert.throws(() => codec.writeJson(number, "utf8"), WireError);
            assert.throws(() => codec.writeJson(number, "utf16"), WireError);
        }
    }
    assert.throws(() => scalar("float").writeJson(Number.MAX_VALUE, "utf8"), WireError);
});

test("matches the JavaScript integer oracle across decimal groups and CLR storage widths", () => {
    const uint32 = [0, 9, 10, 99, 100, 999, 1000, 9999, 10000, 10001, 100000001, 0x7fffffff, 0x80000000, 0xffffffff];
    const uint64 = [
        0n, 9999n, 10000n, 100000001n, 999999999999n, 1000000000000n, 1000000000001n,
        9007199199999999n, 9007199254740991n, 9007199254740992n, 9007199254740993n,
        1000000000000000001n, 0x7fff_ffff_ffff_ffffn, 0x8000_0000_0000_0000n, 0xffff_ffff_ffff_ffffn,
    ];
    let state = 0x311b92a3;
    function nextWord() {
        state ^= state << 13;
        state ^= state >>> 17;
        state ^= state << 5;
        return state >>> 0;
    }
    for (let index = 0; index < 1024; index++) {
        const low = nextWord();
        const high = nextWord();
        uint32.push(low);
        uint64.push((BigInt(high) << 32n) | BigInt(low));
    }
    for (const [type, values] of [
        ["uint", uint32], ["int", uint32.map(value => value | 0)],
        ["ulong", uint64], ["long", uint64.map(value => BigInt.asIntN(64, value))],
    ]) {
        const codec = scalar(type);
        for (const value of values) {
            const text = String(value);
            for (const [encoding, bufferEncoding] of [["utf8", "utf8"], ["utf16", "utf16le"]])
                assert.deepEqual(Buffer.from(codec.writeJson(value, encoding)), Buffer.from(text, bufferEncoding));
        }
    }
});

test("writes exact integer groups in Union identifiers, references and rectangular dimensions", () => {
    const profile = { binary: 2, declared: false };
    const choice = createCodec({ version: 1, root: "Choice", contracts: [
        { id: "Choice", kind: "Union", profile, members: [], cases: [{ id: "Case", hash: 0xffff_fffe, tag: 1 }] },
        { id: "Case", kind: "Record", profile, members: [] },
    ] });
    assert.equal(Buffer.from(choice.writeJson({ type: "Case", value: {} }, "utf8")).toString(),
        '{"$type":4294967294,"$value":{}}');

    const graph = createCodec({ version: 1, root: "Node[]",
        rootWire: { kind: "sequence", element: { kind: "contract", type: "Node" } },
        contracts: [{ id: "Node", kind: "Record", profile: { ...profile, trackReferences: true }, members: [] }],
    });
    const nodes = Array.from({ length: 102 }, () => ({}));
    nodes.push(nodes[101]);
    const graphBytes = graph.writeJson(nodes, "utf8");
    assert.ok(Buffer.from(graphBytes).toString().endsWith('{"$id":101},{"$ref":101}]'));
    const decoded = graph.readJson(graphBytes, "utf8");
    assert.equal(decoded[101], decoded[102]);

    const rectangle = createCodec({ version: 1, root: "int[,]", contracts: [],
        rootWire: { kind: "rectangular", rank: 2, element: { kind: "scalar", type: "int" } },
    });
    const value = { lengths: [0, 2147483647], lowerBounds: [-2147483647, 1], values: [] };
    const bytes = rectangle.writeJson(value, "utf8");
    assert.equal(Buffer.from(bytes).toString(), '{"Lengths":[0,2147483647],"LowerBounds":[-2147483647,1],"Values":[]}');
    assert.deepEqual(rectangle.readJson(bytes, "utf8"), value);
});
