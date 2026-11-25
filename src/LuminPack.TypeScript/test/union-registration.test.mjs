import assert from "node:assert/strict";
import test from "node:test";
import { createCodec, JsonNumber, WireError } from "../dist/index.js";

const profile = { binary: 2, declared: false, jsonEncoding: 0 };
const payloadCodec = {
    readBinary: input => input.u8(),
    writeBinary: (output, value) => output.u8(value),
    readJson: value => Number(value.text),
    writeJson: value => new JsonNumber(String(value)),
};

function bind(wide, declaredTag, generatedHash, registeredTag) {
    return createCodec({
        version: 1, root: "Choice",
        contracts: [
            {
                id: "Choice", kind: "Union", profile, unionRegistration: true, unionWideTags: wide, members: [],
                cases: [{ id: "Generated", hash: generatedHash, tag: declaredTag }],
            },
            { id: "Generated", kind: "Record", profile, members: [] },
        ],
    }, { registered: { Choice: [{ type: "External", tag: registeredTag, codec: payloadCodec }] } });
}

test("reserves both the declared numeric Union tag and generated case hash", () => {
    for (const [wide, declared, hash] of [[false, 17, 101], [true, 65000, 65535]]) {
        const isReserved = { name: "WireError", message: /belongs to a generated case/ };
        assert.throws(() => bind(wide, declared, hash, declared), isReserved);
        assert.throws(() => bind(wide, declared, hash, hash), isReserved);
    }
});

test("distinguishes wide tag zero from reserved headers", () => {
    const codec = bind(true, 42, 0x10000, 0);
    const value = { type: "External", value: 7 };
    const bytes = codec.writeBinary(value);
    assert.deepEqual(codec.readBinary(bytes), value);
    for (let marker = 251; marker <= 255; marker++) {
        bytes[4] = marker;
        assert.throws(() => codec.readBinary(bytes), WireError);
    }
});

test("round trips free numeric tags in narrow and wide Unions", () => {
    const value = { type: "External", value: 7 };
    for (const [wide, registeredTag, header] of [[false, 255, [255]], [true, 65535, [250, 255, 255]]]) {
        const codec = bind(wide, 42, 0x10000, registeredTag);
        const binary = Uint8Array.of(255, 255, 255, 255, ...header, 7);
        assert.deepEqual(codec.writeBinary(value), binary);
        assert.deepEqual(codec.readBinary(binary), value);
        const json = new TextEncoder().encode('{"$type":' + registeredTag + ',"$value":7}');
        assert.deepEqual(codec.writeJson(value, "utf8"), json);
        assert.deepEqual(codec.readJson(json, "utf8"), value);
    }
    assert.throws(() => bind(false, 42, 0x10000, 256), WireError);
});
