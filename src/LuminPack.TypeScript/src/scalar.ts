import { BinaryReader, BinaryWriter, WireError, littleEndian } from "./binary.ts";
import { formatJsonInteger, formatJsonNumber } from "./json-number.ts";
import { JsonNumber, type JsonValue } from "./json.ts";

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

export function scalarCollectionView(value: unknown, type: string, nativeBytes: number): NumericView | undefined {
    if (!ArrayBuffer.isView(value)) return undefined;
    switch (nativeScalar(type, nativeBytes)) {
        case "sbyte": return value instanceof Int8Array ? value : undefined;
        case "byte": return value instanceof Uint8Array ? value : undefined;
        case "short": return value instanceof Int16Array ? value : undefined;
        case "ushort": return value instanceof Uint16Array ? value : undefined;
        case "int": return value instanceof Int32Array ? value : undefined;
        case "uint": return value instanceof Uint32Array ? value : undefined;
        case "float": return value instanceof Float32Array ? value : undefined;
        case "double": return value instanceof Float64Array ? value : undefined;
        case "long": return value instanceof BigInt64Array ? value : undefined;
        case "ulong": return value instanceof BigUint64Array ? value : undefined;
        default: return undefined;
    }
}

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

export type FixedScalarRead = (view: DataView, offset: number) => unknown;
export type FixedScalarWrite = (view: DataView, offset: number, value: unknown) => void;

const fixedScalarReads: Readonly<Record<string, FixedScalarRead>> = Object.assign(Object.create(null), {
    bool: (view: DataView, offset: number) => {
        const value = view.getUint8(offset);
        if (value > 1) throw new WireError("Invalid LuminPack Boolean marker.");
        return value === 1;
    },
    byte: (view: DataView, offset: number) => view.getUint8(offset),
    sbyte: (view: DataView, offset: number) => view.getInt8(offset),
    short: (view: DataView, offset: number) => view.getInt16(offset, true),
    ushort: (view: DataView, offset: number) => view.getUint16(offset, true),
    char: (view: DataView, offset: number) => String.fromCharCode(view.getUint16(offset, true)),
    int: (view: DataView, offset: number) => view.getInt32(offset, true),
    uint: (view: DataView, offset: number) => view.getUint32(offset, true),
    long: (view: DataView, offset: number) => view.getBigInt64(offset, true),
    ulong: (view: DataView, offset: number) => view.getBigUint64(offset, true),
    float: (view: DataView, offset: number) => view.getFloat32(offset, true),
    double: (view: DataView, offset: number) => view.getFloat64(offset, true),
} satisfies Record<string, FixedScalarRead>);

export function fixedScalarRead(type: string, nativeBytes: number): FixedScalarRead {
    const read = fixedScalarReads[nativeScalar(type, nativeBytes)];
    if (!read) throw new WireError("Unsupported LuminPack scalar " + type + ".");
    return read;
}

export function fixedScalarWrite(type: string, nativeBytes: number): FixedScalarWrite {
    if (type === "bool") return (view, offset, value) => {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack Boolean.");
        view.setUint8(offset, Number(value));
    };
    if (type === "char") return (view, offset, value) => view.setUint16(offset,
        typeof value === "string" && value.length === 1 ? value.charCodeAt(0) : smallInteger(value, "ushort"), true);
    if (type === "float" || type === "double") {
        const store = type === "float" ? DataView.prototype.setFloat32 : DataView.prototype.setFloat64;
        return (view, offset, value) => {
            if (typeof value !== "number") throw new WireError("Expected a LuminPack floating-point value.");
            store.call(view, offset, value, true);
        };
    }
    if (isWide(type, nativeBytes))
        return (view, offset, value) => view.setBigUint64(offset, wideInteger(value, type), true);
    const width = scalarWidth(type, nativeBytes);
    const store = width === 1 ? DataView.prototype.setUint8 :
        width === 2 ? DataView.prototype.setUint16 : DataView.prototype.setInt32;
    return (view, offset, value) => store.call(view, offset, smallInteger(value, type), true);
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
    return readJsonNumericScalar(value.text, type, nativeBytes);
}

export function readJsonNumericScalar(text: string, type: string, nativeBytes: number): number | bigint {
    if (type === "float" || type === "double") {
        const decoded = Number(text);
        const rounded = type === "float" ? Math.fround(decoded) : decoded;
        if (!Number.isFinite(rounded)) throw new WireError("Invalid LuminPack JSON floating-point value.");
        return rounded;
    }
    if (/[.eE]/.test(text)) throw new WireError("Expected a LuminPack JSON integer.");
    if (isWide(type, nativeBytes)) return wideInteger(BigInt(text), type);
    return smallInteger(Number(text), type);
}

export function writeJsonScalar(type: string, value: unknown, nativeBytes: number): string {
    if (type === "bool") {
        if (typeof value !== "boolean") throw new WireError("Expected a LuminPack Boolean.");
        return value ? "true" : "false";
    }
    if (type === "char") {
        if (typeof value === "string" && value.length === 1) return JSON.stringify(value);
        return JSON.stringify(String.fromCharCode(smallInteger(value, "ushort")));
    }
    if (type === "float" || type === "double") {
        if (typeof value !== "number") throw new WireError("Expected a LuminPack floating-point value.");
        return formatJsonNumber(value, type === "float");
    }
    return formatJsonInteger(isWide(type, nativeBytes) ? wideInteger(value, type) : smallInteger(value, type));
}
