import assert from "node:assert/strict";
import test from "node:test";

import { BinaryReader, BinaryWriter, decodeUtf16, readString, writeString } from "../dist/binary.js";
import { canDecodeLatin1AsAscii, canDecodeUtf16Natively, findUtf16Zero, isLatin1 } from "../dist/simd.js";
import { textWasm } from "../dist/text-wasm.js";

test("bounded UTF-16 token search uses SIMD and keeps the same scalar results", async () => {
    const original = globalThis.WebAssembly;
    globalThis.WebAssembly = undefined;
    try {
        const { findUtf16Zero: scalar } = await import("../dist/simd.js?scalar");
        for (const units of [0, 1, 7, 8, 9, 15, 16, 17, 511, 512, 513, 4095, 4096, 4097, 8201]) {
            const bytes = new Uint8Array(units * 2 + 3).fill(41);
            const zero = units * 2 + 1;
            bytes.fill(0, zero);
            assert.equal(scalar(bytes, 1), zero);
            bytes[zero + 1] = 41;
            assert.equal(scalar(bytes, 1), -1);
        }
    } finally {
        globalThis.WebAssembly = original;
    }
    assert.equal(WebAssembly.validate(textWasm), true);
    for (const units of [0, 1, 7, 8, 9, 15, 16, 17, 511, 512, 513, 4095, 4096, 4097, 8201]) {
        const bytes = new Uint8Array(units * 2 + 3).fill(41);
        const zero = units * 2 + 1;
        bytes.fill(0, zero);
        assert.equal(findUtf16Zero(bytes, 1), zero);
        bytes[zero + 1] = 41;
        assert.equal(findUtf16Zero(bytes, 1), -1);
    }
});

test("native text decode and scalar tails keep exact CLR code units", () => {
    const units = "\ufeff" + "\u96ea".repeat(4100);
    const bytes = Buffer.from(units, "utf16le");
    assert.equal(canDecodeUtf16Natively(bytes), true);
    assert.equal(decodeUtf16(bytes), units);
    for (const index of [0, 7, 8, 4095, 4096, units.length - 1]) {
        const edited = units.slice(0, index) + "\ud800" + units.slice(index + 1);
        const encoded = Buffer.from(edited, "utf16le");
        assert.equal(canDecodeUtf16Natively(encoded), false);
        assert.equal(decodeUtf16(encoded), edited);
    }
    const latin = new Uint8Array(8193).fill(0x61);
    assert.equal(canDecodeLatin1AsAscii(latin), true);
    latin[latin.length - 1] = 0x80;
    assert.equal(canDecodeLatin1AsAscii(latin), false);
    assert.equal(decodeUtf16(latin, true), "a".repeat(8192) + "\u0080");
});

test("compact UTF-16 Length and VarInt use Latin-1 only when every code unit fits", () => {
    for (const length of [1, 4, 7, 8, 15, 16, 17, 127, 128, 1025]) {
        const compact = "\u00ff".repeat(length);
        const wide = compact.slice(1) + "\u0100";
        assert.equal(isLatin1(compact), true);
        assert.equal(isLatin1(wide), false);
        for (const profile of [2, 5]) {
            for (const value of [compact, wide]) {
                const output = new BinaryWriter();
                writeString(output, value, profile);
                const input = new BinaryReader(output.finish());
                assert.equal(readString(input, profile), value);
                input.end();
            }
        }
    }
});
