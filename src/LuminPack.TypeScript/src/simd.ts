import { textWasm } from "./text-wasm.ts";

interface TextKernels {
    readonly scratch: Uint8Array;
    readonly findZero: (offset: number, width: number) => number;
    readonly hasSurrogate: (offset: number, width: number) => number;
    readonly isAscii: (offset: number, width: number) => number;
}

const scratchOffset = 16;
const scratchWidth = 8192;
const nativeDecodeThreshold = 1024;
const nonLatin1 = /[^\u0000-\u00ff]/;
let kernels: TextKernels | null | undefined;

function textKernels(): TextKernels | null {
    if (kernels !== undefined) return kernels;
    kernels = null;
    if (typeof WebAssembly === "undefined" || !WebAssembly.validate(textWasm)) return kernels;
    try {
        const exports = new WebAssembly.Instance(new WebAssembly.Module(textWasm)).exports;
        kernels = {
            scratch: new Uint8Array((exports.memory as WebAssembly.Memory).buffer, scratchOffset, scratchWidth),
            findZero: exports.find_utf16_zero as TextKernels["findZero"],
            hasSurrogate: exports.has_utf16_surrogate as TextKernels["hasSurrogate"],
            isAscii: exports.is_ascii as TextKernels["isAscii"],
        };
    } catch (error) {
        if (!(error instanceof WebAssembly.CompileError)) throw error;
    }
    return kernels;
}

export function isLatin1(text: string): boolean {
    if (text.length > 16) return !nonLatin1.test(text);
    let index = 0;
    let high = 0;
    for (; index + 4 <= text.length; index += 4)
        high |= text.charCodeAt(index) | text.charCodeAt(index + 1) |
            text.charCodeAt(index + 2) | text.charCodeAt(index + 3);
    for (; index < text.length; index++) high |= text.charCodeAt(index);
    return high <= 0xff;
}

export function findUtf16Zero(bytes: Uint8Array, start: number): number {
    let position = start;
    const prefixEnd = Math.min(start + 32, bytes.length);
    for (; position + 1 < prefixEnd; position += 2)
        if ((bytes[position] | bytes[position + 1]) === 0) return position;

    if (bytes.length - position >= nativeDecodeThreshold) {
        const simd = textKernels();
        if (simd !== null) {
            while (bytes.length - position >= 16) {
                const width = Math.min(bytes.length - position, scratchWidth) & ~15;
                simd.scratch.set(bytes.subarray(position, position + width));
                const found = simd.findZero(scratchOffset, width);
                if (found >= 0) return position + found;
                position += width;
            }
        }
    }
    for (; position + 1 < bytes.length; position += 2)
        if ((bytes[position] | bytes[position + 1]) === 0) return position;
    return -1;
}

export function canDecodeUtf16Natively(bytes: Uint8Array): boolean {
    if (bytes.length < nativeDecodeThreshold) return false;
    const simd = textKernels();
    if (simd === null) return false;
    let offset = 0;
    while (bytes.length - offset >= 16) {
        const width = Math.min(bytes.length - offset, scratchWidth) & ~15;
        simd.scratch.set(bytes.subarray(offset, offset + width));
        if (simd.hasSurrogate(scratchOffset, width) !== 0) return false;
        offset += width;
    }
    for (; offset + 1 < bytes.length; offset += 2)
        if ((bytes[offset + 1] & 0xf8) === 0xd8) return false;
    return true;
}

export function canDecodeLatin1AsAscii(bytes: Uint8Array): boolean {
    if (bytes.length < nativeDecodeThreshold) return false;
    const simd = textKernels();
    if (simd === null) return false;
    let offset = 0;
    while (bytes.length - offset >= 16) {
        const width = Math.min(bytes.length - offset, scratchWidth) & ~15;
        simd.scratch.set(bytes.subarray(offset, offset + width));
        if (simd.isAscii(scratchOffset, width) === 0) return false;
        offset += width;
    }
    for (; offset < bytes.length; offset++)
        if (bytes[offset] > 0x7f) return false;
    return true;
}
