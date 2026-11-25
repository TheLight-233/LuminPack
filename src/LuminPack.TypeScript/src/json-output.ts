import { WireError, encodeUtf8 } from "./binary.ts";
import { scalarCollectionView } from "./scalar.ts";
import { formatJsonNumber } from "./json-number.ts";

const quads = new Uint8Array(40000);
for (let i = 0; i < 10000; i++) {
    let n = i;
    for (let p = i * 4 + 3; p >= i * 4; p--) {
        quads[p] = 48 + n % 10;
        n = Math.trunc(n / 10);
    }
}

/** Operation-local UTF-8 cursor. The returned document owns its bytes. */
export class JsonByteOutput {
    private bytes = new Uint8Array(1024);
    private position = 0;

    constructor(private readonly maxDepth: number, private readonly maxCollection: number,
        readonly zero: (type: string, ordinal: number) => unknown) {}

    invalid(): never { throw new WireError("Invalid value for the generated LuminPack contract."); }
    depth(d: number): void { if (d > this.maxDepth) throw new WireError("LuminPack JSON nesting exceeds its limit."); }
    object(x: unknown): Record<string, unknown> {
        if (x === null || typeof x !== "object" || Array.isArray(x) || x instanceof Map) return this.invalid();
        return x as Record<string, unknown>;
    }
    items(x: unknown, scalar: string): ArrayLike<unknown> {
        const values = Array.isArray(x) ? x : scalarCollectionView(x, scalar, 8);
        if (!values || values.length > this.maxCollection) return this.invalid();
        return values;
    }
    entries(x: unknown): Map<unknown, unknown> {
        if (!(x instanceof Map) || x.size > this.maxCollection) return this.invalid();
        return x;
    }
    text(x: unknown): void {
        if (x === null) { this.ascii("null"); return; }
        if (typeof x !== "string") return this.invalid();
        this.room(x.length + 2);
        const start = this.position;
        this.bytes[this.position++] = 34;
        for (let i = 0; i < x.length; i++) {
            const code = x.charCodeAt(i);
            if (code < 32 || code === 34 || code === 92 || code >= 128) {
                this.position = start;
                this.push(JSON.stringify(x));
                return;
            }
            this.bytes[this.position++] = code;
        }
        this.bytes[this.position++] = 34;
    }
    bool(x: unknown): void {
        if (typeof x !== "boolean") return this.invalid();
        this.ascii(x ? "true" : "false");
    }
    byte(x: unknown): void {
        if (typeof x !== "number" || x < 0 || x > 255) return this.invalid();
        this.int(x);
    }
    long(x: unknown): void {
        if (typeof x !== "bigint" || BigInt.asIntN(64, x) !== x) return this.invalid();
        if (x >= -2147483648n && x <= 2147483647n) this.int(Number(x));
        else this.ascii(String(x));
    }

    private room(n: number): void {
        if (n <= this.bytes.length - this.position) return;
        const grown = new Uint8Array(Math.max(this.bytes.length * 2, this.position + n));
        grown.set(this.bytes.subarray(0, this.position));
        this.bytes = grown;
    }

    // Short property heads are packed into two compile-time constants by Codegen.
    head(first: number, last: number, length: number): void {
        this.room(length);
        const p = this.position, q = p + length - 4, b = this.bytes;
        b[p] = first; b[p+1] = first >>> 8; b[p+2] = first >>> 16; b[p+3] = first >>> 24;
        b[q] = last; b[q+1] = last >>> 8; b[q+2] = last >>> 16; b[q+3] = last >>> 24;
        this.position += length;
    }

    literal(bytes: Uint8Array): void {
        this.room(bytes.length);
        this.bytes.set(bytes, this.position);
        this.position += bytes.length;
    }

    ascii(text: string): void {
        this.room(text.length);
        for (let i = 0; i < text.length; i++) this.bytes[this.position++] = text.charCodeAt(i);
    }

    token(value: number): void {
        this.room(1);
        this.bytes[this.position++] = value;
    }

    // Writers may pass quoted Unicode text and custom JSON through the same sink.
    push(...texts: string[]): void {
        for (const text of texts) {
            this.room(text.length);
            const start = this.position;
            let i = 0;
            for (; i < text.length; i++) {
                const ch = text.charCodeAt(i);
                if (ch >= 128) break;
                this.bytes[this.position++] = ch;
            }
            if (i !== text.length) {
                const tail = encodeUtf8(text.slice(i));
                this.room(tail.length);
                this.bytes.set(tail, start + i);
                this.position += tail.length;
            }
        }
    }

    int(value: unknown): void {
        if (typeof value !== "number" || (value | 0) !== value) throw new WireError("Expected a LuminPack Int32.");
        this.room(11);
        let x = value;
        if (x < 0) { this.bytes[this.position++] = 45; x = -x; }
        this.magnitude(x);
    }

    private magnitude(x: number): void {
        let high = x, middle = -1, low = -1;
        if (high >= 10000) {
            high = Math.trunc(x / 10000);
            low = (x - high * 10000) * 4;
            if (high >= 10000) {
                const top = Math.trunc(high / 10000);
                middle = (high - top * 10000) * 4;
                high = top;
            }
        }
        let p = this.position;
        const head = high * 4, b = this.bytes;
        if (high >= 1000) b[p++] = quads[head];
        if (high >= 100) b[p++] = quads[head + 1];
        if (high >= 10) b[p++] = quads[head + 2];
        b[p++] = quads[head + 3];
        if (middle >= 0) {
            b[p++] = quads[middle]; b[p++] = quads[middle+1]; b[p++] = quads[middle+2]; b[p++] = quads[middle+3];
        }
        if (low >= 0) {
            b[p++] = quads[low]; b[p++] = quads[low+1]; b[p++] = quads[low+2]; b[p++] = quads[low+3];
        }
        this.position = p;
    }

    float(value: unknown, single: boolean): void {
        if (typeof value !== "number") throw new WireError("Expected a LuminPack floating-point value.");
        const x = single ? Math.fround(value) : value;
        if ((x | 0) === x && Math.abs(x) < 16777216 && !Object.is(x, -0)) { this.int(x); return; }
        const magnitude = Math.abs(x);
        if (single && magnitude >= 0.0001 && magnitude < 1000000) {
            // Use the same exact round-trip and significant-digit limits as shortDecimal.
            for (let decimals = 2; decimals <= 4; decimals += 2) {
                let scaled = Math.trunc(magnitude * (decimals === 2 ? 100 : 10000) + 0.5);
                if (scaled === 0 || Math.fround(scaled * (decimals === 2 ? 0.01 : 0.0001)) !== magnitude) continue;
                let places = decimals;
                while (scaled % 10 === 0) { scaled /= 10; places--; }
                if (scaled >= 1000000 || places < 0) break;
                this.room(16);
                if (x < 0) this.bytes[this.position++] = 45;
                const divisor = [1,10,100,1000,10000][places];
                this.magnitude(Math.trunc(scaled / divisor));
                if (places) {
                    this.bytes[this.position++] = 46;
                    const q = (scaled % divisor) * 4;
                    for (let i = 4 - places; i < 4; i++) this.bytes[this.position++] = quads[q + i];
                }
                return;
            }
        }
        this.ascii(formatJsonNumber(x, single));
    }

    finish(): Uint8Array { return this.bytes.slice(0, this.position); }
    reset(): boolean { this.position = 0; return this.bytes.length <= 1048576; }
}
