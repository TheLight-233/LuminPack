import { readFileSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

const [runtime, schema, readFormat, writeFormat, input, output, fixtureBinding] = process.argv.slice(2);
if (!output) {
    throw new Error("Expected runtime, schema, input format, output format, input path, output path and optional fixture binding.");
}

const { createCodec, JsonNumber } = await import(pathToFileURL(runtime).href);
const minimumInt64 = -(1n << 63n);
const edit = fixtureBinding?.startsWith("edit-");
const codec = createCodec(JSON.parse(readFileSync(schema, "utf8")), bindingOptions(edit ? undefined : fixtureBinding));
const encodings = new Map([["json-utf8", "utf8"], ["json-utf16", "utf16"]]);
const bytes = readFileSync(input);
const model = readFormat === "binary" ? codec.readBinary(bytes) : codec.readJson(bytes, encoding(readFormat));
if (edit) editLayout(model, fixtureBinding);
const result = writeFormat === "binary" ? codec.writeBinary(model) : codec.writeJson(model, encoding(writeFormat));
writeFileSync(output, result);

function encoding(format) {
    if (!encodings.has(format)) {
        throw new Error(`Invalid format: ${format}`);
    }
    return encodings.get(format);
}

function editLayout(model, fixture) {
    switch (fixture) {
        case "edit-packed":
            model.Nested[0].Data.End = 1234;
            break;
        case "edit-tagged":
            model.Values[0].Code = 0x01234567;
            break;
        case "edit-native":
            model.Items[0].Code = 0x01234567;
            break;
        default:
            throw new Error(`Unknown layout edit: ${fixture}`);
    }
}

function bindingOptions(fixture) {
    switch (fixture) {
        case undefined:
            return {};
        case "graph-external":
            return registration("LuminPackCppReferenceFixtures.IGraphChoice",
                "LuminPackCppReferenceFixtures.GraphExternal", 600, xorFormatter(4, 59, 71));
        case "large-graph-external":
            return registration("LuminPackCppVirtualUnionFixtures.LargeGraph",
                "LuminPackCppVirtualUnionFixtures.Registered", 612, xorFormatter(4, 23, 73));
        case "registered-narrow":
            return registration("LuminPackCppRegisteredUnionFixtures.INarrow",
                "LuminPackCppRegisteredUnionFixtures.RegisteredCase", 100, xorFormatter(4, 85, 137));
        case "registered-wide":
            return registration("LuminPackCppRegisteredUnionFixtures.IWide",
                "LuminPackCppRegisteredUnionFixtures.RegisteredCase", 600, xorFormatter(4, 85, 137));
        case "custom-shifted":
            return { custom: { "LuminPackCppCustomFixtures.Shifted": xorFormatter(8, 85, 137) } };
        case "custom-binary-only":
            return { custom: { "LuminPackCppCustomFixtures.BinaryOnly": xorFormatter(1, 85, 0) } };
        default:
            throw new Error(`Unknown fixture binding: ${fixture}`);
    }
}

function registration(union, type, tag, codec) {
    return { registered: { [union]: [{ type, tag, codec }] } };
}

// Mirrors the managed fixture's application-owned payload, after any runtime Union framing.
function xorFormatter(width, binaryMask, jsonMask) {
    function decode(payload, mask) {
        return width === 8 && payload === minimumInt64 ? null : Number(payload) ^ mask;
    }

    function encode(value, mask) {
        return width === 8 && value === null ? minimumInt64 : BigInt(value ^ mask);
    }

    return {
        minimumSize: width,
        readBinary(input) {
            if (width === 1) return decode(input.u8(), binaryMask);
            if (width === 4) return decode(input.i32(), binaryMask);
            return decode(input.i64(), binaryMask);
        },
        writeBinary(output, value) {
            const payload = encode(value, binaryMask);
            if (width === 1) output.u8(Number(payload));
            else if (width === 4) output.i32(Number(payload));
            else output.u64(BigInt.asUintN(64, payload));
        },
        readJson(value) {
            if (width === 1) throw new Error("Binary-only fixture.");
            return decode(BigInt(value.text), jsonMask);
        },
        writeJson(value) {
            if (width === 1) throw new Error("Binary-only fixture.");
            return new JsonNumber(String(encode(value, jsonMask)));
        }
    };
}
