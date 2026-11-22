import { BinaryReader, BinaryWriter, WireError, decodeUtf8 } from "./binary.ts";

export function readReferenceNumber(input: BinaryReader): number {
    const marker = input.i8();
    if (marker >= 0) return marker;
    let value: number;
    switch (marker) {
        case -121: value = input.u8(); break;
        case -122: value = input.i8(); break;
        case -123: value = input.u16(); break;
        case -124: value = input.i16(); break;
        case -125: value = input.u32(); break;
        case -126: value = input.i32(); break;
        case -127: value = Number(input.u64()); break;
        case -128: value = Number(input.i64()); break;
        default: throw new WireError("Invalid LuminPack reference number.");
    }
    if (value < 0 || value > 0xffff_ffff) throw new WireError("Invalid LuminPack reference number.");
    return value;
}

export function writeReferenceNumber(output: BinaryWriter, value: number): void {
    if (value <= 127) output.u8(value);
    else if (value <= 0xffff) { output.u8(133); output.u16(value); }
    else { output.u8(131); output.i32(value); }
}

function readVarint(input: BinaryReader, mask: number, canonical: boolean): number {
    let value = 0;
    for (let shift = 0; shift <= 28; shift += 7) {
        const next = input.u8();
        if (shift === 28 && next > mask) break;
        value += (next & 127) * 2 ** shift;
        if (next < 128) {
            if (canonical && shift !== 0 && next === 0) break;
            return value;
        }
    }
    throw new WireError("Invalid LuminPack Version variable integer.");
}

export function readLegacyMetadata(input: BinaryReader): string {
    return decodeUtf8(input.take(readVarint(input, 7, false)), true);
}

function writeOrdinal(output: BinaryWriter, ordinal: number): void {
    while (ordinal >= 128) {
        output.u8((ordinal & 127) | 128);
        ordinal >>>= 7;
    }
    output.u8(ordinal);
}

export function schemaHash(descriptors: readonly bigint[]): bigint {
    const prime = 1099511628211n;
    let hash = BigInt.asUintN(64, (14695981039346656037n ^ BigInt(descriptors.length)) * prime);
    for (let descriptor of descriptors) {
        for (let part = 0; part < 8; part++, descriptor >>= 8n)
            hash = BigInt.asUintN(64, (hash ^ (descriptor & 255n)) * prime);
    }
    return hash;
}

export function nameKey(descriptor: bigint): number { return Number(descriptor & 0xffff_ffffn); }
export function widthCode(descriptor: bigint): number { return Number(descriptor >> 56n); }
export function typeKey(descriptor: bigint): bigint { return (descriptor >> 32n) & 0xff_ffffn; }

export class VersionState {
    private readonly entries: Array<{ hash: bigint; fields: readonly bigint[] }> = [];

    get size(): number { return this.entries.length; }

    truncate(size: number): void { this.entries.length = size; }

    read(input: BinaryReader, profile: number, form: number): readonly bigint[] {
        if (form === (0x50 | profile)) {
            const entry = this.entries[readVarint(input, 3, true)];
            if (!entry) throw new WireError("Unknown LuminPack Version schema ordinal.");
            return entry.fields;
        }
        if (form !== (0x40 | profile)) throw new WireError("Invalid LuminPack Version form or profile.");
        const count = input.u8();
        const hash = input.u64();
        if (count > 249 || count * 8 > input.remaining) throw new WireError("Invalid LuminPack Version descriptor count.");
        const fields: bigint[] = [];
        const names = new Set<number>();
        for (let index = 0; index < count; index++) {
            const field = input.u64();
            const name = nameKey(field);
            const width = widthCode(field);
            if (names.has(name) || (width > 250 && width < 254))
                throw new WireError("Invalid LuminPack Version descriptor.");
            names.add(name);
            fields.push(field);
        }
        if (schemaHash(fields) !== hash) throw new WireError("Invalid LuminPack Version schema hash.");
        this.entries.push({ hash, fields });
        return fields;
    }

    write(output: BinaryWriter, profile: number, fields: readonly bigint[], hash: bigint): void {
        const ordinal = this.entries.findIndex(entry => entry.hash === hash &&
            (entry.fields === fields || (entry.fields.length === fields.length && entry.fields.every((field, i) => field === fields[i]))));
        output.u8(251);
        if (ordinal >= 0) {
            output.u8(0x50 | profile);
            writeOrdinal(output, ordinal);
        } else {
            output.u8(0x40 | profile);
            output.u8(fields.length);
            output.u64(hash);
            for (const field of fields) output.u64(field);
            this.entries.push({ hash, fields });
        }
    }
}
