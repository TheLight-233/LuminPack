// Adapted from LuminPack's XjbFloat32, XjbFloat64 and LuminPackJsonFloatFormatter.
// Copyright 2026 xjb714 and contributors. Licensed under Apache-2.0.
// The upstream license is included as LICENSE-xjb.txt.
import { WireError } from "./binary.ts";
import { jsonNumberWasm } from "./json-number-wasm.ts";
import { float32H37, float32Pow10, float64Pow10 } from "./xjb-tables.ts";

interface NumberKernels {
    readonly buffer: Uint8Array;
    readonly float32: (value: number) => number;
    readonly float64: (value: number) => number;
}

const tokenCapacity = 32;
const wordMask = 0xffff_ffff_ffff_ffffn;
const float32Mask = 0x7f_ffff;
const float64Mask = 0x000f_ffff_ffff_ffffn;
const dotQuarter = 1n << 62n;
const bits = new DataView(new ArrayBuffer(8));
const radix = 10_000;
const twoQuads = 100_000_000;
const threeQuads = 1_000_000_000_000n;
const safeInteger = 9_007_199_254_740_991n;
const digitChars = "0123456789";
const digitPairs = Array.from({ length: 100 }, (_, index) => {
    const tens = (index / 10) | 0;
    return digitChars[tens] + digitChars[index - tens * 10];
});
// A cached ASCII string plays the role of each packed four-byte entry in the C# writer.
const digitQuads = Array.from({ length: radix }, (_, index) => {
    const high = (index / 100) | 0;
    return digitPairs[high] + digitPairs[index - high * 100];
});
const leadingQuads = digitQuads.map((quad, index) => {
    if (index < 10) return digitChars[index];
    if (index < 100) return digitPairs[index];
    return index < 1000 ? quad.slice(1) : quad;
});
let kernels: NumberKernels | null | undefined;

function fixedTwoQuads(value: number): string {
    const high = (value / radix) | 0;
    return digitQuads[high] + digitQuads[value - high * radix];
}

function leadingTwoQuads(value: number): string {
    if (value < radix) return leadingQuads[value];
    const high = (value / radix) | 0;
    return leadingQuads[high] + digitQuads[value - high * radix];
}

function unsignedNumber(value: number): string {
    if (value < twoQuads) return leadingTwoQuads(value);
    // The largest safe Number has an eight-digit leading group. All products are exact integers.
    const high = (value / twoQuads) | 0;
    return leadingTwoQuads(high) + fixedTwoQuads(value - high * twoQuads);
}

function unsignedBigInt(value: bigint): string {
    if (value <= safeInteger) return unsignedNumber(Number(value));
    // For UInt64, a single split leaves an eight-digit head and a twelve-digit tail.
    const high = value / threeQuads;
    const tail = Number(value - high * threeQuads);
    const firstQuad = (tail / twoQuads) | 0;
    return leadingTwoQuads(Number(high)) + digitQuads[firstQuad] + fixedTwoQuads(tail - firstQuad * twoQuads);
}

function numberKernels(): NumberKernels | null {
    if (kernels !== undefined) return kernels;
    kernels = null;
    if (typeof WebAssembly === "undefined" || !WebAssembly.validate(jsonNumberWasm)) return kernels;
    try {
        const operations = new WebAssembly.Instance(new WebAssembly.Module(jsonNumberWasm)).exports;
        const memory = operations.memory as WebAssembly.Memory;
        const bufferAddress = (operations.json_number_buffer as () => number)();
        kernels = {
            buffer: new Uint8Array(memory.buffer, bufferAddress, tokenCapacity),
            float32: operations.format_float32 as NumberKernels["float32"],
            float64: operations.format_float64 as NumberKernels["float64"],
        };
    } catch (error) {
        if (!(error instanceof WebAssembly.CompileError)) throw error;
    }
    return kernels;
}

interface Decimal { readonly digits: bigint; readonly exponent: number; }

function normalize(digits: bigint, exponent: number): Decimal {
    while (digits % 10n === 0n) {
        digits /= 10n;
        exponent++;
    }
    return { digits, exponent };
}

function shortestFloat32(raw: number, exponent: number): Decimal {
    const sig = raw & float32Mask;
    const q = exponent === 0 ? -149 : exponent - 150;
    if (sig === 0) {
        const k = (q * 1233 - 512) >> 12;
        const h = q + ((k * -1701 - 1701) >> 9);
        const pow10 = float32Pow10[45 + k];
        const high = pow10 >> BigInt(4 - h);
        const ten = (high >> 36n) * 10n;
        const mask = (1n << 36n) - 1n;
        const dot = high & mask;
        const half = pow10 >> BigInt(28 - h);
        const offset = (1n << 34n) - 7n + (dot >> 32n);
        let one = (dot * 5n + offset) >> 35n;
        if ((half >> 1n) > dot) one = 0n;
        if (q === 31 - 150 || q === 214 - 150 || q === 217 - 150) one++;
        if (half > mask - dot) one = 10n;
        return normalize(ten + one, k);
    }

    const c = BigInt(exponent === 0 ? sig : sig | 0x80_0000);
    const k = (q * 1233) >> 12;
    const shift = float32H37[exponent];
    const pow10 = float32Pow10[45 + k];
    const high = ((c << BigInt(shift)) * pow10) >> 64n;
    const half = (pow10 >> BigInt(65 - shift)) + BigInt((sig + 1) & 1);
    const shorter = ((high + half) >> 36n) * 10n;
    const longer = (high * 5n + (1n << 34n) - 7n + ((high >> 32n) & 15n)) >> 35n;
    return normalize(((high - half) >> 36n) < ((high + half) >> 36n) ? shorter : longer, k);
}

function shortestFloat64(raw: bigint, exponent: number): Decimal {
    const sig = raw & float64Mask;
    const q = exponent === 0 ? -1074 : exponent - 1075;
    if (sig === 0n) {
        const k = (q * 315653 - 131072) >> 20;
        const h = q + ((k * -217707 - 217707) >> 16);
        const pow10 = float64Pow10[584 - 2 * k];
        const half = pow10 >> BigInt(-h);
        const dot = (pow10 << BigInt(53 + h)) & wordMask;
        const ten = (pow10 >> BigInt(11 - h)) * 10n;
        let one = ((dot >> BigInt(53 + h)) * 5n + (1n << BigInt(9 - h))) >> BigInt(10 - h);
        const tail = (dot >> 54n) * 5n;
        if ((tail & 511n) > (half >> 55n) * 5n) one = (tail >> 9n) + 1n;
        if (dot === dotQuarter) one = 2n;
        if ((half >> 1n) > dot) one = 0n;
        if (half > wordMask - dot) one = 10n;
        return normalize(ten + one, k);
    }

    const c = exponent === 0 ? sig : sig | (1n << 52n);
    const k = ((exponent - 1075) * 78913) >> 18;
    const h = q + (((-1 - k) * 217707) >> 16);
    const shifted = c << BigInt(h + 7);
    const index = 584 - 2 * k;
    const pow10High = float64Pow10[index];
    const a = shifted * pow10High;
    const bHigh = (shifted * float64Pow10[index + 1]) >> 64n;
    const sum = a + bHigh;
    const high = sum >> 64n;
    const dot = ((sum & wordMask) >> 6n) | ((high << 58n) & wordMask);
    const half = (pow10High >> BigInt(-h)) + ((sig + 1n) & 1n);
    const ten = (high >> 6n) * 10n;
    const addend = dot === dotQuarter ? 0n : (1n << 63n) + 6n;
    let one = (dot * 10n + addend) >> 64n;
    if (dot < half) one = 0n;
    if (wordMask - dot < half) one = 10n;
    return normalize(ten + one, k);
}

function emitFixed(digits: string, point: number): string {
    if (point <= 0) return "0." + "0".repeat(-point) + digits;
    if (point >= digits.length) return digits + "0".repeat(point - digits.length);
    return digits.slice(0, point) + "." + digits.slice(point);
}

function shortDecimal(magnitude: number, single: boolean): string | undefined {
    if (magnitude > 2147483648) return;
    if (!single && !Number.isInteger(magnitude * 1000)) return;
    for (let decimals = 2; decimals <= 4; decimals += 2) {
        const power = decimals === 2 ? 100 : 10000;
        let scaled = Math.trunc(magnitude * power + 0.5);
        if (scaled === 0) continue;
        const quotient = single ? Math.fround(scaled * (decimals === 2 ? 0.01 : 0.0001)) : scaled / power;
        if (quotient !== magnitude) continue;
        let places = decimals;
        while (scaled % 10 === 0) {
            scaled /= 10;
            places--;
        }
        if (scaled >= (single ? 1_000_000 : 1_000_000_000_000_000)) return;
        const digits = unsignedNumber(scaled);
        return emitFixed(digits, digits.length - places);
    }
}

function formatPortable(value: number, magnitude: number, single: boolean): string {
    const sign = value < 0 ? "-" : "";
    let decimal: Decimal;
    let legacy: boolean;
    if (single) {
        bits.setFloat32(0, magnitude);
        const raw = bits.getUint32(0);
        const exponent = (raw >>> 23) & 255;
        legacy = exponent >= 95 && exponent <= 159;
        decimal = shortestFloat32(raw, exponent);
    } else {
        bits.setFloat64(0, magnitude);
        const raw = bits.getBigUint64(0);
        const exponent = Number(raw >> 52n) & 2047;
        legacy = exponent >= 991 && exponent <= 1055;
        const shortcut = legacy ? shortDecimal(magnitude, false) : undefined;
        if (shortcut !== undefined) return sign + shortcut;
        decimal = shortestFloat64(raw, exponent);
    }

    const digits = unsignedBigInt(decimal.digits);
    const point = digits.length + decimal.exponent;
    const leading = point - 1;
    if ((legacy && decimal.exponent >= -(single ? 9 : 17)) || (leading >= -4 && leading < (single ? 9 : 17)))
        return sign + emitFixed(digits, point);
    const coefficient = digits.length === 1 ? digits : digits[0] + "." + digits.slice(1);
    const exponentMagnitude = Math.abs(leading);
    const exponentText = exponentMagnitude < 100 ? digitPairs[exponentMagnitude] : leadingQuads[exponentMagnitude];
    return sign + coefficient + "E" + (leading < 0 ? "-" : "+") + exponentText;
}

/** Writes the C# integer token after the scalar codec checks the CLR range. */
export function formatJsonInteger(value: number | bigint): string {
    if (typeof value === "number") return value < 0 ? "-" + unsignedNumber(-value) : unsignedNumber(value);
    return value < 0n ? "-" + unsignedBigInt(-value) : unsignedBigInt(value);
}

/** Writes the shortest decimal with the C# writer's fixed/scientific layout. */
export function formatJsonNumber(value: number, single: boolean): string {
    if (single) value = Math.fround(value);
    if (!Number.isFinite(value)) throw new WireError("LuminPack JSON numbers must be finite.");
    if (value === 0) return Object.is(value, -0) ? "-0" : "0";
    const magnitude = Math.abs(value);
    // The double integral bound also intersects the historical exponent window at 2^33.
    if (magnitude < (single ? 16777216 : 8589934592) && Number.isInteger(value)) return formatJsonInteger(value);

    // The single-precision shortcut uses the same round-trip and digit bounds
    // as the portable formatter. Resolve it before crossing into WebAssembly.
    if (single && magnitude >= 0.0001 && magnitude <= 2147483648) {
        const shortcut = shortDecimal(magnitude, true);
        if (shortcut !== undefined) return value < 0 ? "-" + shortcut : shortcut;
    }
    const native = numberKernels();
    if (native === null) return formatPortable(value, magnitude, single);
    const length = (single ? native.float32 : native.float64)(value);
    if (length < 1 || length > native.buffer.length) throw new WireError("Invalid LuminPack JSON numeric token length.");
    return String.fromCharCode(...native.buffer.subarray(0, length));
}
