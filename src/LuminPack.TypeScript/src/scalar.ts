import { BinaryReader, BinaryWriter, WireError, littleEndian } from "./binary.ts";
import { formatJsonNumber, JsonNumber, type JsonValue } from "./json.ts";

const integerBounds = new Map<string, readonly [number, number]>([
    ["byte", [0, 0xff]], ["sbyte", [-0x80, 0x7f]], ["short", [-0x8000, 0x7fff]],
    ["ushort", [0, 0xffff]], ["int", [-0x8000_0000, 0x7fff_ffff]], ["uint", [0, 0xffff_ffff]],
    ["nint", [-0x8000_0000, 0x7fff_ffff]], ["nuint", [0, 0xffff_ffff]],
]);

export function scalarWidth(type: string, nativeBytes: number): number {
    switch (type) {
        case "bool": case "byte": case "sbyte": return 1;
        case "char": case "short": case "ushort": return 2;
        case "int": case "uint": case "float": return 4;
        case "long": case "ulong": case "double": return 8;
        case "nint": case "nuint": return nativeBytes;
        default: throw new WireError("Unsupported LuminPack scalar " + type + ".");
    }
}

function isWide(type: string, nativeBytes: number): boolean {
    return type === "long" || type === "ulong" || (nativeBytes === 8 && (type === "nint" || type === "nuint"));
}

export function defaultScalar(type: string, nativeBytes: number): unknown {
    if (type === "bool") return false;
    if (type === "char") return "\0";
    return isWide(type, nativeBytes) ? 0n : 0;
}

export function readScalar(input: BinaryReader, type: string, nativeBytes: number): unknown {
    switch (type) {
        case "bool": {
            const byte = input.u8();
            if (byte > 1) throw new WireError("Invalid LuminPack Boolean marker.");
            return byte === 1;
        }
        case "byte": return input.u8();
        case "sbyte": return input.i8();
        case "short": return input.i16();
        case "ushort": return input.u16();
        case "char": return String.fromCharCode(input.u16());
        case "int": return input.i32();
        case "uint": return input.u32();
        case "nint": return nativeBytes === 4 ? input.i32() : input.i64();
        case "nuint": return nativeBytes === 4 ? input.u32() : input.u64();
        case "long": return input.i64();
        case "ulong": return input.u64();
        case "float": return input.f32();
        case "double": return input.f64();
        default: throw new WireError("Unsupported LuminPack scalar " + type + ".");
    }
}

function smallInteger(value: unknown, type: string): number {
    const bounds = integerBounds.get(type);
    if (typeof value !== "number" || !Number.isInteger(value) || !bounds || value < bounds[0] || value > bounds[1])
        throw new WireError("LuminPack scalar is outside the " + type + " range.");
    return value;
}

function wideInteger(value: unknown, type: string): bigint {
    const unsigned = type === "ulong" || type === "nuint";
    if (typeof value !== "bigint" || value < (unsigned ? 0n : -0x8000_0000_0000_0000n) ||
        value > (unsigned ? 0xffff_ffff_ffff_ffffn : 0x7fff_ffff_ffff_ffffn))
        throw new WireError("LuminPack scalar is outside the " + type + " range.");
    return value;
}

export function writeScalar(output: BinaryWriter, type: string, value: unknown, nativeBytes: number): void {
    if (type === "bool") {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack Boolean.");
        output.u8(Number(value));
    } else if (type === "char") {
        if (typeof value === "string" && value.length === 1) output.u16(value.charCodeAt(0));
        else output.u16(smallInteger(value, "ushort"));
    } else if (type === "float" || type === "double") {
        if (typeof value !== "number") throw new WireError("Expected a LuminPack floating-point value.");
        if (type === "float") output.f32(value);
        else output.f64(value);
    } else if (isWide(type, nativeBytes)) output.u64(wideInteger(value, type));
    else {
        const integer = smallInteger(value, type);
        switch (scalarWidth(type, nativeBytes)) {
            case 1: output.u8(integer); break;
            case 2: output.u16(integer); break;
            default: output.i32(integer);
        }
    }
}

type NumberArray = Int8Array | Uint8Array | Int16Array | Uint16Array | Int32Array | Uint32Array | Float32Array | Float64Array;
type Integer64Array = BigInt64Array | BigUint64Array;
type NumericView = NumberArray | Integer64Array;

function numericView(bytes: Uint8Array, count: number, type: string): NumericView | undefined {
    const { buffer, byteOffset } = bytes;
    switch (type) {
        case "sbyte": return new Int8Array(buffer, byteOffset, count);
        case "byte": return bytes;
        case "short": return new Int16Array(buffer, byteOffset, count);
        case "ushort": return new Uint16Array(buffer, byteOffset, count);
        case "int": return new Int32Array(buffer, byteOffset, count);
        case "uint": return new Uint32Array(buffer, byteOffset, count);
        case "float": return new Float32Array(buffer, byteOffset, count);
        case "double": return new Float64Array(buffer, byteOffset, count);
        case "long": return new BigInt64Array(buffer, byteOffset, count);
        case "ulong": return new BigUint64Array(buffer, byteOffset, count);
        default: return undefined;
    }
}

function nativeScalar(type: string, nativeBytes: number): string {
    if (type === "nint") return nativeBytes === 8 ? "long" : "int";
    if (type === "nuint") return nativeBytes === 8 ? "ulong" : "uint";
    return type;
}

function readRows<T>(count: number, width: number, get: (offset: number) => T): T[] {
    const values = new Array<T>(count);
    for (let index = 0; index < count; index++) values[index] = get(index * width);
    return values;
}

/** Checks the full contiguous block before using native typed views or unaligned reads. */
export function readScalars(input: BinaryReader, count: number, type: string, nativeBytes: number): unknown[] {
    const scalar = nativeScalar(type, nativeBytes);
    const width = scalarWidth(scalar, nativeBytes);
    const bytes = input.take(count * width);
    if (scalar === "bool") {
        return readRows(count, 1, offset => {
            const raw = bytes[offset];
            if (raw > 1) throw new WireError("Invalid LuminPack Boolean marker.");
            return raw === 1;
        });
    }
    if (width === 1 || littleEndian && bytes.byteOffset % width === 0) {
        if (scalar === "char") return Array.from(new Uint16Array(bytes.buffer, bytes.byteOffset, count), unit => String.fromCharCode(unit));
        const view = numericView(bytes, count, scalar)!;
        return scalar === "long" || scalar === "ulong" ? Array.from(view as Integer64Array) : Array.from(view as NumberArray);
    }
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    switch (scalar) {
        case "char": return readRows(count, width, offset => String.fromCharCode(view.getUint16(offset, true)));
        case "short": return readRows(count, width, offset => view.getInt16(offset, true));
        case "ushort": return readRows(count, width, offset => view.getUint16(offset, true));
        case "int": return readRows(count, width, offset => view.getInt32(offset, true));
        case "uint": return readRows(count, width, offset => view.getUint32(offset, true));
        case "float": return readRows(count, width, offset => view.getFloat32(offset, true));
        case "double": return readRows(count, width, offset => view.getFloat64(offset, true));
        case "long": return readRows(count, width, offset => view.getBigInt64(offset, true));
        case "ulong": return readRows(count, width, offset => view.getBigUint64(offset, true));
        default: throw new WireError("Unsupported LuminPack scalar " + scalar + ".");
    }
}

function arrayNumber(value: unknown, scalar: string, bounds?: readonly [number, number]): number {
    if (scalar === "bool") {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack Boolean.");
        return Number(value);
    }
    if (scalar === "char" && typeof value === "string" && value.length === 1) return value.charCodeAt(0);
    if (typeof value !== "number" || bounds && (!Number.isInteger(value) || value < bounds[0] || value > bounds[1]))
        throw new WireError("LuminPack scalar is outside the " + scalar + " range.");
    return value;
}

/** Reserves a contiguous block once and keeps scalar range and Boolean checks. */
export function writeScalars(output: BinaryWriter, values: readonly unknown[], type: string, nativeBytes: number): void {
    const scalar = nativeScalar(type, nativeBytes);
    const width = scalarWidth(scalar, nativeBytes);
    const bytes = output.writable(values.length * width);
    const wide = scalar === "long" || scalar === "ulong";
    const bounds = integerBounds.get(scalar === "char" ? "ushort" : scalar);
    if (width === 1 || littleEndian && bytes.byteOffset % width === 0) {
        const view = scalar === "char" ? new Uint16Array(bytes.buffer, bytes.byteOffset, values.length) :
            scalar === "bool" ? bytes : numericView(bytes, values.length, scalar)!;
        if (wide) {
            const aligned = view as Integer64Array;
            for (let index = 0; index < values.length; index++) aligned[index] = wideInteger(values[index], scalar);
        } else {
            const aligned = view as NumberArray;
            for (let index = 0; index < values.length; index++) aligned[index] = arrayNumber(values[index], scalar, bounds);
        }
        return;
    }
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    if (wide) {
        for (let index = 0; index < values.length; index++) view.setBigUint64(index * width, wideInteger(values[index], scalar), true);
        return;
    }
    const store = scalar === "float" ? view.setFloat32 : scalar === "double" ? view.setFloat64 :
        width === 2 ? view.setUint16 : view.setUint32;
    for (let index = 0; index < values.length; index++)
        store.call(view, index * width, arrayNumber(values[index], scalar, bounds), true);
}

export function readJsonScalar(value: JsonValue, type: string, nativeBytes: number, utf8: boolean): unknown {
    if (type === "bool") {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack JSON Boolean.");
        return value;
    }
    if (type === "char") {
        if (typeof value !== "string" || (utf8 && value.length > 2)) throw new WireError("Invalid LuminPack JSON character.");
        return value[0] ?? "\0";
    }
    if (!(value instanceof JsonNumber)) throw new WireError("Expected a LuminPack JSON number.");
    if (type === "float" || type === "double") {
        const decoded = Number(value.text);
        const rounded = type === "float" ? Math.fround(decoded) : decoded;
        if (!Number.isFinite(rounded)) throw new WireError("Invalid LuminPack JSON floating-point value.");
        return rounded;
    }
    if (/[.eE]/.test(value.text)) throw new WireError("Expected a LuminPack JSON integer.");
    if (isWide(type, nativeBytes)) return wideInteger(BigInt(value.text), type);
    return smallInteger(Number(value.text), type);
}

export function writeJsonScalar(type: string, value: unknown, nativeBytes: number): JsonValue {
    if (type === "bool") {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack Boolean.");
        return value;
    }
    if (type === "char") {
        if (typeof value === "string" && value.length === 1) return value;
        return String.fromCharCode(smallInteger(value, "ushort"));
    }
    if (type === "float" || type === "double") {
        if (typeof value !== "number") throw new WireError("Expected a LuminPack floating-point value.");
        return formatJsonNumber(type === "float" ? Math.fround(value) : value, type === "float");
    }
    return new JsonNumber(String(isWide(type, nativeBytes) ? wideInteger(value, type) : smallInteger(value, type)));
}
