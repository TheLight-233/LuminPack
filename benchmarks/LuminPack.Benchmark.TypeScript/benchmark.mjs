import assert from "node:assert/strict";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { cpus } from "node:os";
import { dirname, join, resolve } from "node:path";
import { parseArgs } from "node:util";
import { Writer } from "@jsonjoy.com/buffers/lib/Writer.js";
import { JsonDecoder, JsonEncoder } from "@jsonjoy.com/json-pack/lib/json/index.js";
import { Decoder as MessagePackDecoder, Encoder as MessagePackEncoder } from "@msgpack/msgpack";
import { Decoder as CborDecoder, Encoder as CborEncoder } from "cbor-x";
import fastJsonStringify from "fast-json-stringify";
import { Packr, Unpackr } from "msgpackr";
import { createCodec } from "../../src/LuminPack.TypeScript/dist/index.js";

const csvHeader = "language,model,format,library,operation,sample,ns_per_op,bytes,alloc_bytes_per_op";
const safeWideInteger = BigInt(Number.MAX_SAFE_INTEGER);
const now = process.hrtime.bigint;
let sink;

function readModels(directory) {
    return ["simple", "serialize"].map(name => {
        const manifest = JSON.parse(readFileSync(join(directory, name + ".manifest.json"), "utf8"));
        assert.equal(manifest.binaryProfile, 2, name + " must use the default UTF-16 Length binary profile");
        const binary = readFileSync(join(directory, name + ".bin"));
        const json = readFileSync(join(directory, name + ".utf8.json"));
        const luminpack = createCodec(manifest);
        const luminValue = luminpack.readBinary(binary);
        assert.deepEqual(luminpack.readJson(json, "utf8"), luminValue, name + " managed Binary/JSON parity");
        checkSafeIntegers(luminValue);
        return { name, luminpack, luminValue, genericValue: JSON.parse(json.toString("utf8")), manifest };
    });
}

function checkSafeIntegers(value) {
    if (typeof value === "bigint") {
        assert(value >= -safeWideInteger && value <= safeWideInteger,
            "A benchmark fixture uses an integer that the ECMAScript JSON reader cannot represent exactly.");
    } else if (value instanceof Map) {
        for (const [key, item] of value) {
            checkSafeIntegers(key);
            checkSafeIntegers(item);
        }
    } else if (value !== null && typeof value === "object") {
        for (const item of Object.values(value)) checkSafeIntegers(item);
    }
}

// Give the JSON code generator stable fields and leave polymorphic payloads dynamic.
function buildJsonSchema(manifest) {
    const identities = new Map();
    const definitions = Object.fromEntries(manifest.contracts.map((contract, index) => {
        const id = "Contract" + index;
        identities.set(contract.id.replaceAll("global::", ""), id);
        identities.set(contract.type.replaceAll("global::", ""), id);
        return [id, {}];
    }));
    const contractRef = type => ({ $ref: "#/definitions/" + identities.get(type) });
    function schema(wire) {
        switch (wire.kind) {
            case "scalar":
                if (wire.type === "bool") return { type: "boolean" };
                if (wire.type === "char") return { type: "string" };
                return { type: wire.type === "double" || wire.type === "float" ? "number" : "integer" };
            case "string": return { type: "string", nullable: true };
            case "contract": return contractRef(wire.type);
            case "sequence": return { type: "array", nullable: true, items: schema(wire.element) };
            case "dictionary": return wire.key.kind === "string"
                ? { type: "object", nullable: true, additionalProperties: schema(wire.value) }
                : { type: "array", nullable: true, items: {
                    type: "array", items: [schema(wire.key), schema(wire.value)], additionalItems: false,
                } };
            case "nullable": return { anyOf: [{ type: "null" }, schema(wire.element)] };
            case "pair": case "tuple": return { type: "array", items: wire.elements.map(schema), additionalItems: false };
            default: throw new Error("Unsupported benchmark JSON schema: " + wire.kind);
        }
    }
    for (const contract of manifest.contracts) {
        const id = identities.get(contract.id.replaceAll("global::", ""));
        const properties = contract.kind === "Union"
            ? { $type: { type: "integer" }, $value: {} }
            : Object.fromEntries(contract.members.map(member => [member.name, schema(member.wire)]));
        definitions[id] = { type: "object", nullable: true, properties, required: Object.keys(properties) };
    }
    return { ...contractRef(manifest.root), definitions };
}

function libraries(model) {
    const { manifest, luminpack } = model;
    const messagePackWriter = new MessagePackEncoder();
    const messagePackReader = new MessagePackDecoder();
    const packr = new Packr({ useRecords: true, sequential: false });
    const unpackr = new Unpackr({ useRecords: true, sequential: false });
    const cborWriter = new CborEncoder({ useRecords: true, sequential: false });
    const cborReader = new CborDecoder({ useRecords: true, sequential: false });
    const jsonPackWriter = new JsonEncoder(new Writer());
    const jsonPackReader = new JsonDecoder();
    const stringifySchema = fastJsonStringify(buildJsonSchema(manifest));
    const v8Decode = input => JSON.parse(input.toString("utf8"));
    return [
        { library: "LuminPack", format: "binary", encode: value => luminpack.writeBinary(value), decode: input => luminpack.readBinary(input) },
        { library: "@msgpack/msgpack", format: "binary", encode: value => messagePackWriter.encode(value), decode: input => messagePackReader.decode(input) },
        { library: "msgpackr", format: "binary", encode: value => packr.pack(value), decode: input => unpackr.unpack(input) },
        { library: "cbor-x", format: "binary", encode: value => cborWriter.encode(value), decode: input => cborReader.decode(input) },
        { library: "LuminPack", format: "json", encode: value => luminpack.writeJson(value, "utf8"), decode: input => luminpack.readJson(input, "utf8") },
        { library: "V8 JSON", format: "json", encode: value => Buffer.from(JSON.stringify(value), "utf8"), decode: v8Decode },
        { library: "@jsonjoy.com/json-pack", format: "json", encode: value => jsonPackWriter.encode(value), decode: input => jsonPackReader.decode(input) },
        { library: "fast-json-stringify + V8", format: "json", encode: value => Buffer.from(stringifySchema(value), "utf8"), decode: v8Decode },
    ];
}

function prepare(model) {
    return libraries(model).flatMap(({ library, format, encode, decode }) => {
        const isLuminPack = library === "LuminPack";
        const value = isLuminPack ? model.luminValue : model.genericValue;
        // Store a separate input because several encoders allocate views into an internal buffer.
        const input = Buffer.from(encode(value));
        assert(input.byteLength > 0);
        const result = decode(input);
        assert.deepEqual(result, value, model.name + " round trip with " + library + " " + format);
        if (!isLuminPack) {
            const json = Buffer.from(JSON.stringify(result), "utf8");
            // Prove the reference representation round trips every typed field of the managed model.
            assert.deepEqual(model.luminpack.readJson(json, "utf8"), model.luminValue, model.name + " typed reference " + library);
        }
        const task = { model: model.name, format, library, bytes: input.byteLength };
        return [
            { ...task, operation: "encode", execute: () => encode(value) },
            { ...task, operation: "decode", execute: () => decode(input) },
        ];
    });
}

function batch(task, iterations) {
    for (let index = 0; index < iterations; index++) sink = task.execute();
}

function calibrate(task) {
    // Keep a timer-free inner loop and take regular time observations between batches.
    for (let iterations = 1; iterations < 8192; iterations *= 2) {
        const start = now();
        batch(task, iterations);
        if (now() - start >= 1_000_000n) return iterations;
    }
    return 8192;
}

function sample(task, duration) {
    const start = now();
    const deadline = start + duration;
    let iterations = 0;
    let end;
    do {
        batch(task, task.batchIterations);
        iterations += task.batchIterations;
        end = now();
    } while (end < deadline);
    return Number(end - start) / iterations;
}

const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
        samples: { type: "string", default: "9" },
        "sample-ms": { type: "string", default: "100" },
        "warmup-ms": { type: "string", default: "200" },
        "verify-only": { type: "boolean", default: false },
    },
});
if (positionals.length !== 2)
    throw new Error("Usage: node benchmark.mjs <models-directory> <output.csv> [--samples N] [--sample-ms N] [--warmup-ms N] [--verify-only]");

function positiveInteger(value, option) {
    const parsed = Number(value);
    if (!/^[1-9][0-9]*$/.test(value) || !Number.isSafeInteger(parsed))
        throw new Error("Expected a positive integer for --" + option);
    return parsed;
}

const samples = positiveInteger(values.samples, "samples");
const sampleDuration = BigInt(positiveInteger(values["sample-ms"], "sample-ms")) * 1_000_000n;
const warmupDuration = BigInt(positiveInteger(values["warmup-ms"], "warmup-ms")) * 1_000_000n;
const tasks = readModels(resolve(positionals[0])).flatMap(prepare);
const output = resolve(positionals[1]);
const rows = [csvHeader];
if (!values["verify-only"]) {
    for (const task of tasks) {
        task.batchIterations = calibrate(task);
        sample(task, warmupDuration);
    }
    // Rotate the starting point so a single task does not always run first in a sample round.
    for (let repeat = 0; repeat < samples; repeat++) {
        for (let index = 0; index < tasks.length; index++) {
            const task = tasks[(index + repeat) % tasks.length];
            const elapsed = sample(task, sampleDuration);
            rows.push(["typescript", task.model, task.format, task.library, task.operation, repeat + 1,
                elapsed.toFixed(3), task.bytes, ""].join(","));
        }
    }
    assert(sink !== undefined);
}
mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, rows.join("\n") + "\n");
console.error(`Node ${process.version} / V8 ${process.versions.v8} / ${process.platform} ${process.arch} / ${cpus()[0]?.model ?? "unknown CPU"}`);
console.error(`Verified ${tasks.length / 2} round trips and wrote ${rows.length - 1} timing samples to ${output}.`);
