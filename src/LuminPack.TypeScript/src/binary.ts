import { canDecodeLatin1AsAscii, canDecodeUtf16Natively, findUtf16Zero, isLatin1 } from "./simd.ts";

export class WireError extends Error {
    constructor(message: string) {
        super(message);
        this.name = "WireError";
    }
}

export interface Limits {
    maxInput: number;
    maxCollection: number;
    maxString: number;
    maxDepth: number;
}

export const defaultLimits: Limits = Object.freeze({
    maxInput: 0x7fff_ffff,
    maxCollection: 0x7fff_ffff,
    maxString: 0x7fff_ffff,
    maxDepth: 512,
});

export class BinaryReader {
    readonly bytes: Uint8Array;
    readonly view: DataView;
    readonly limits: Limits;
    #position = 0;

    constructor(bytes: Uint8Array, limits: Limits = defaultLimits) {
        if (bytes.byteLength > limits.maxInput) throw new WireError("LuminPack input exceeds its limit.");
        this.bytes = bytes;
        this.view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        this.limits = limits;
    }

    get position(): number { return this.#position; }
    get remaining(): number { return this.bytes.length - this.#position; }

    reserve(width: number): number {
        if (!Number.isSafeInteger(width) || width < 0 || width > this.remaining)
            throw new WireError("Truncated LuminPack record.");
        const position = this.#position;
        this.#position += width;
        return position;
    }

    peek(): number {
        if (this.remaining === 0) throw new WireError("Truncated LuminPack record.");
        return this.bytes[this.#position];
    }

    take(width: number): Uint8Array {
        const position = this.reserve(width);
        return this.bytes.subarray(position, position + width);
    }

    subrecord(width: number): BinaryReader { return new BinaryReader(this.take(width), this.limits); }
    u8(): number { return this.bytes[this.reserve(1)]; }
    i8(): number { return this.view.getInt8(this.reserve(1)); }
    u16(): number { return this.view.getUint16(this.reserve(2), true); }
    i16(): number { return this.view.getInt16(this.reserve(2), true); }
    u32(): number { return this.view.getUint32(this.reserve(4), true); }
    i32(): number { return this.view.getInt32(this.reserve(4), true); }
    u64(): bigint { return this.view.getBigUint64(this.reserve(8), true); }
    i64(): bigint { return this.view.getBigInt64(this.reserve(8), true); }
    f32(): number { return this.view.getFloat32(this.reserve(4), true); }
    f64(): number { return this.view.getFloat64(this.reserve(8), true); }

    count(minimum: number): number | null {
        const count = this.i32();
        if (count === -1) return null;
        if (count < 0 || count > this.limits.maxCollection ||
            (minimum > 0 && count > Math.floor(this.remaining / minimum)))
            throw new WireError("Invalid LuminPack collection count.");
        return count;
    }

    end(): void {
        if (this.remaining !== 0) throw new WireError("Trailing bytes after LuminPack record.");
    }
}

export class BinaryWriter {
    #buffer: Uint8Array;
    #view: DataView;
    #position = 0;

    constructor(initialCapacity = 256) {
        this.#buffer = new Uint8Array(initialCapacity);
        this.#view = new DataView(this.#buffer.buffer);
    }

    get position(): number { return this.#position; }

    reserve(width: number): number {
        const start = this.#position;
        const end = start + width;
        if (!Number.isSafeInteger(width) || width < 0 || end > 0x7fff_ffff)
            throw new RangeError("LuminPack output exceeds its maximum size.");
        if (end > this.#buffer.length) {
            const grown = new Uint8Array(Math.max(end, Math.min(0x7fff_ffff, this.#buffer.length * 2)));
            grown.set(this.#buffer);
            this.#buffer = grown;
            this.#view = new DataView(grown.buffer);
        }
        this.#position = end;
        return start;
    }

    u8(value: number): void { const p = this.reserve(1); this.#buffer[p] = value; }
    u16(value: number): void { const p = this.reserve(2); this.#view.setUint16(p, value, true); }
    i32(value: number): void { const p = this.reserve(4); this.#view.setInt32(p, value, true); }
    u64(value: bigint): void { const p = this.reserve(8); this.#view.setBigUint64(p, value, true); }
    f32(value: number): void { const p = this.reserve(4); this.#view.setFloat32(p, value, true); }
    f64(value: number): void { const p = this.reserve(8); this.#view.setFloat64(p, value, true); }

    patchI32(offset: number, value: number): void {
        if (!Number.isInteger(offset) || offset < 0 || offset + 4 > this.#position)
            throw new RangeError("Invalid LuminPack backpatch position.");
        this.#view.setInt32(offset, value, true);
    }

    append(value: Uint8Array): void {
        const start = this.reserve(value.length);
        this.#buffer.set(value, start);
    }

    appendCountedBlock(count: number, bytes: Uint8Array): void {
        const position = this.reserve(4 + bytes.length);
        this.#buffer.set(bytes, position + 4);
        this.#view.setInt32(position, count, true);
    }

    writable(width: number): Uint8Array {
        const start = this.reserve(width);
        return this.#buffer.subarray(start, start + width);
    }

    clear(width: number): void {
        const start = this.reserve(width);
        this.#buffer.fill(0, start, start + width);
    }

    utf16(value: string, latin1: boolean): void {
        const position = this.reserve(value.length * (latin1 ? 1 : 2));
        this.#writeUtf16(position, value, latin1);
    }

    utf16Length(value: string | null): void {
        if (value === null) { this.i32(-1); return; }
        const text = value ?? "";
        if (typeof text !== "string" || text.length > 0x3fff_ffff) throw new WireError("Invalid LuminPack string.");
        const latin1 = text.length !== 0 && isLatin1(text);
        const position = this.reserve(4 + text.length * (latin1 ? 1 : 2));
        this.#view.setInt32(position, text.length * 2 + Number(latin1), true);
        this.#writeUtf16(position + 4, text, latin1);
    }

    #writeUtf16(position: number, value: string, latin1: boolean): void {
        let index = 0;
        if (latin1) {
            for (; index + 4 <= value.length; index += 4, position += 4) {
                const word = (value.charCodeAt(index) & 0xff) | ((value.charCodeAt(index + 1) & 0xff) << 8) |
                    ((value.charCodeAt(index + 2) & 0xff) << 16) | (value.charCodeAt(index + 3) << 24);
                this.#view.setUint32(position, word, true);
            }
            for (; index < value.length; index++) this.#buffer[position++] = value.charCodeAt(index);
        } else {
            for (; index + 2 <= value.length; index += 2, position += 4)
                this.#view.setUint32(position, value.charCodeAt(index) | (value.charCodeAt(index + 1) << 16), true);
            if (index < value.length) this.#view.setUint16(position, value.charCodeAt(index), true);
        }
    }

    finish(): Uint8Array { return this.#buffer.subarray(0, this.#position); }
}

const strictUtf8 = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true });
const relaxedUtf8 = new TextDecoder("utf-8", { ignoreBOM: true });
const utf16Decoder = new TextDecoder("utf-16le", { ignoreBOM: true });
const utf8Encoder = new TextEncoder();
const { isWellFormed, toWellFormed } = String.prototype as String & {
    isWellFormed?(this: string): boolean;
    toWellFormed?(this: string): string;
};
export const littleEndian = new Uint8Array(Uint16Array.of(1).buffer)[0] === 1;

export function decodeUtf8(bytes: Uint8Array, strict: boolean): string {
    try {
        return (strict ? strictUtf8 : relaxedUtf8).decode(bytes);
    } catch (error) {
        if (error instanceof TypeError) throw new WireError("Invalid LuminPack UTF-8.");
        throw error;
    }
}

export function encodeUtf8(value: string): Uint8Array {
    if (isWellFormed !== undefined) {
        if (!isWellFormed.call(value)) throw new WireError("LuminPack UTF-8 requires valid UTF-16.");
        return utf8Encoder.encode(value);
    }
    for (let index = 0; index < value.length; index++) {
        const unit = value.charCodeAt(index);
        if (unit >= 0xd800 && unit <= 0xdfff) {
            const next = value.charCodeAt(index + 1);
            if (unit >= 0xdc00 || !(next >= 0xdc00 && next <= 0xdfff))
                throw new WireError("LuminPack UTF-8 requires valid UTF-16.");
            index++;
        }
    }
    return utf8Encoder.encode(value);
}

export function decodeUtf16(bytes: Uint8Array, latin1 = false): string {
    if (!latin1 && (bytes.length & 1) !== 0) throw new WireError("Invalid UTF-16 byte count.");
    if (latin1 ? canDecodeLatin1AsAscii(bytes) : canDecodeUtf16Natively(bytes))
        return (latin1 ? relaxedUtf8 : utf16Decoder).decode(bytes);
    let text = "";
    if (latin1 || (littleEndian && (bytes.byteOffset & 1) === 0)) {
        const units = latin1 ? bytes : new Uint16Array(bytes.buffer, bytes.byteOffset, bytes.length / 2);
        for (let offset = 0; offset < units.length; offset += 8192)
            text += String.fromCharCode(...units.subarray(offset, Math.min(offset + 8192, units.length)));
        return text;
    }
    for (let offset = 0; offset < bytes.length;) {
        const length = Math.min(8192, (bytes.length - offset) / 2);
        const block = new Array<number>(length);
        for (let index = 0; index < length; index++, offset += 2)
            block[index] = bytes[offset] | (bytes[offset + 1] << 8);
        text += String.fromCharCode(...block);
    }
    return text;
}

function wellFormed(value: string): string {
    if (toWellFormed !== undefined) return toWellFormed.call(value);
    return value.replace(/[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]/gu, "\ufffd");
}

function readLength(input: BinaryReader, variable: boolean): number {
    if (!variable) return input.i32();
    let value = 0;
    for (let shift = 0; shift <= 28; shift += 7) {
        const next = input.u8();
        if (shift === 28 && next > 8) break;
        value += (next & 127) * 2 ** shift;
        if (next < 128) {
            if ((shift !== 0 && next === 0) || value > 0x8000_0000) break;
            return value - 1;
        }
    }
    throw new WireError("Invalid LuminPack variable string length.");
}

function writeLength(output: BinaryWriter, value: number, variable: boolean): void {
    if (!variable) {
        output.i32(value);
        return;
    }
    for (value++; value >= 128; value >>>= 7) output.u8((value & 127) | 128);
    output.u8(value);
}

function readUtf16Length(input: BinaryReader): string | null {
    const header = input.i32();
    if (header < 0) {
        if (header === -1) return null;
        throw new WireError("Invalid LuminPack string length.");
    }
    const length = header >>> 1;
    const latin1 = (header & 1) !== 0;
    const width = latin1 ? length : header;
    const position = input.reserve(width);
    if (length > input.limits.maxString) throw new WireError("Invalid LuminPack string character count.");
    if (length <= 64) return readShortUtf16(input, position, length, latin1);
    return decodeUtf16(input.bytes.subarray(position, position + width), latin1);
}

function readShortUtf16(input: BinaryReader, start: number, count: number, latin1: boolean): string {
    const { bytes, view } = input;
    let value = "";
    let index = start;
    if (latin1) {
        const end = start + count;
        for (; index + 4 <= end; index += 4)
            value += String.fromCharCode(bytes[index], bytes[index + 1], bytes[index + 2], bytes[index + 3]);
        for (; index < end; index++) value += String.fromCharCode(bytes[index]);
    } else {
        const end = start + count * 2;
        for (; index + 8 <= end; index += 8)
            value += String.fromCharCode(view.getUint16(index, true), view.getUint16(index + 2, true),
                view.getUint16(index + 4, true), view.getUint16(index + 6, true));
        for (; index < end; index += 2) value += String.fromCharCode(view.getUint16(index, true));
    }
    return value;
}

export function readString(input: BinaryReader, profile: number, strictSkip = false): string | null {
    if (profile === 2) return readUtf16Length(input);
    const utf8 = profile === 0 || profile === 1 || profile === 4;
    const token = profile === 1 || profile === 3;
    const variable = profile === 4 || profile === 5;
    let latin1 = false;
    let expected = -1;
    let payload: Uint8Array;
    if (token) {
        const unit = utf8 ? 1 : 2;
        const start = input.position;
        const end = utf8 ? input.bytes.indexOf(0, start) : findUtf16Zero(input.bytes, start);
        if (end < start || end + unit > input.bytes.length) throw new WireError("Missing LuminPack string terminator.");
        payload = input.take(end + unit - start).subarray(0, end - start);
    } else {
        const header = readLength(input, variable);
        if (utf8) expected = readLength(input, variable);
        if (header < -1 || (utf8 && variable && (header === -1 ? expected !== -1 :
            expected < 0 || expected > header || (header !== 0 && expected === 0))))
            throw new WireError("Invalid LuminPack string length.");
        if (header === -1) return null;
        latin1 = !utf8 && (header & 1) !== 0;
        payload = input.take(latin1 ? header >>> 1 : header);
    }
    const knownLength = utf8 ? expected : payload.length / (latin1 ? 1 : 2);
    if (knownLength > input.limits.maxString || (utf8 && expected > payload.length && payload.length !== 0))
        throw new WireError("Invalid LuminPack string character count.");
    let text = utf8 ? decodeUtf8(payload, expected > 0 || strictSkip) : decodeUtf16(payload, latin1);
    if (!utf8 && token) text = wellFormed(text);
    if (text.length > input.limits.maxString ||
        (utf8 && expected > 0 && payload.length !== 0 && expected !== text.length))
        throw new WireError("Invalid LuminPack string character count.");
    return text;
}

export function writeString(output: BinaryWriter, value: string | null, profile: number): void {
    if (profile === 2) { output.utf16Length(value); return; }
    const utf8 = profile === 0 || profile === 1 || profile === 4;
    const token = profile === 1 || profile === 3;
    const variable = profile === 4 || profile === 5;
    const text = value ?? "";
    if (typeof text !== "string" || text.length > (utf8 ? 0x7fff_ffff : 0x3fff_ffff))
        throw new WireError("Invalid LuminPack string.");
    if (utf8) {
        const payload = encodeUtf8(text);
        if (!token) {
            writeLength(output, value === null ? -1 : payload.length, variable);
            writeLength(output, value === null ? -1 : text.length, variable);
        }
        output.append(payload);
        if (token) output.u8(0);
        return;
    }
    const latin1 = !token && text.length !== 0 && isLatin1(text);
    if (!token) writeLength(output, value === null ? -1 : text.length * 2 + Number(latin1), variable);
    output.utf16(text, latin1);
    if (token) output.u16(0);
}
