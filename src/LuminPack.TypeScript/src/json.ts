import { WireError } from "./binary.ts";

export class JsonNumber {
    readonly text: string;

    constructor(text: string) { this.text = text; }
}

export type JsonValue = null | boolean | string | JsonNumber | JsonValue[] | JsonObject;
export interface JsonObject { [name: string]: JsonValue; }

export function put<T>(object: Record<string, T>, key: string, value: T): void {
    if (key === "__proto__")
        Object.defineProperty(object, key, { value, writable: true, enumerable: true, configurable: true });
    else object[key] = value;
}

export function jsonObject(value: JsonValue): JsonObject {
    if (value === null || typeof value !== "object" || Array.isArray(value) || value instanceof JsonNumber)
        throw new WireError("Expected a LuminPack JSON object.");
    return value;
}

export function jsonArray(value: JsonValue, max: number): JsonValue[] {
    if (!Array.isArray(value) || value.length > max) throw new WireError("Invalid LuminPack JSON array.");
    return value;
}

export const orderedJsonMiss = Symbol("LuminPack ordered JSON fallback");
const escapedJsonUnit = /[\\\x00-\x1f]/;

/** A bounded cursor for the canonical heads emitted by the contract. */
export class OrderedJsonCursor {
    private readonly numberPattern = /-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?/y;
    private position = 0;

    constructor(private readonly text: string, private readonly maxDepth: number) {}

    consume(token: string): boolean {
        if (!this.text.startsWith(token, this.position)) return false;
        this.position += token.length;
        return true;
    }

    expect(token: string): void {
        if (!this.consume(token)) throw orderedJsonMiss;
    }

    depth(depth: number): void {
        if (depth > this.maxDepth) throw orderedJsonMiss;
    }

    end(): void {
        if (this.position !== this.text.length) throw orderedJsonMiss;
    }

    string(): string {
        if (this.text.charCodeAt(this.position) !== 34) throw orderedJsonMiss;
        const start = this.position + 1;
        const end = this.text.indexOf('"', start);
        if (end < 0) throw orderedJsonMiss;
        const value = this.text.slice(start, end);
        if (escapedJsonUnit.test(value)) throw orderedJsonMiss;
        this.position = end + 1;
        return value;
    }

    number(): string {
        this.numberPattern.lastIndex = this.position;
        const match = this.numberPattern.exec(this.text);
        if (!match) throw orderedJsonMiss;
        this.position = this.numberPattern.lastIndex;
        return match[0];
    }

    int32(): number {
        let cursor = this.position;
        const negative = this.text.charCodeAt(cursor) === 45;
        if (negative) cursor++;
        const first = this.text.charCodeAt(cursor) - 48;
        if (!(first >= 0 && first <= 9)) throw orderedJsonMiss;
        let number = first;
        cursor++;
        if (number !== 0) {
            for (;;) {
                const digit = this.text.charCodeAt(cursor) - 48;
                if (!(digit >= 0 && digit <= 9)) break;
                number = number * 10 + digit;
                if (number > 2147483648) throw orderedJsonMiss;
                cursor++;
            }
        }
        if (!negative && number === 2147483648) throw orderedJsonMiss;
        this.position = cursor;
        return negative ? -number : number;
    }
}

/** Retains number lexemes until the member codec knows their CLR width. */
export function parseJson(text: string, maxDepth: number, maxCollection: number): JsonValue {
    let index = 0;
    const number = /-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?/y;
    const bad = () => { throw new WireError("Invalid LuminPack JSON document."); };

    function space(): void {
        while (index < text.length) {
            const c = text.charCodeAt(index);
            if (c !== 32 && c !== 10 && c !== 13 && c !== 9) return;
            index++;
        }
    }

    function string(): string {
        const start = index++;
        let escaped = false;
        while (index < text.length) {
            const c = text.charCodeAt(index++);
            if (c === 34) {
                if (!escaped) return text.slice(start + 1, index - 1);
                try { return JSON.parse(text.slice(start, index)) as string; }
                catch { bad(); }
            }
            if (c === 92) { escaped = true; index++; }
            else if (c < 32) bad();
        }
        return bad();
    }

    function value(depth: number): JsonValue {
        space();
        const c = text[index];
        if (c === '"') return string();
        if (c === "{" || c === "[") {
            if (depth > maxDepth) throw new WireError("LuminPack JSON nesting exceeds its limit.");
            index++;
            space();
            if (c === "[") {
                const items: JsonValue[] = [];
                if (text[index] === "]") { index++; return items; }
                do {
                    if (items.length === maxCollection) throw new WireError("LuminPack JSON array exceeds its limit.");
                    items.push(value(depth + 1));
                    space();
                    if (text[index] === "]") { index++; return items; }
                } while (text[index++] === ",");
                return bad();
            }
            const object: JsonObject = {};
            if (text[index] === "}") { index++; return object; }
            do {
                space();
                if (text[index] !== '"') return bad();
                const key = string();
                space();
                if (text[index++] !== ":") return bad();
                put(object, key, value(depth + 1));
                space();
                if (text[index] === "}") { index++; return object; }
            } while (text[index++] === ",");
            return bad();
        }
        if (text.startsWith("true", index)) { index += 4; return true; }
        if (text.startsWith("false", index)) { index += 5; return false; }
        if (text.startsWith("null", index)) { index += 4; return null; }
        number.lastIndex = index;
        const match = number.exec(text);
        if (match === null) return bad();
        index = number.lastIndex;
        return new JsonNumber(match[0]);
    }

    const result = value(1);
    space();
    if (index !== text.length) return bad();
    return result;
}

export function stringifyJson(value: JsonValue, encodedNames: ReadonlyMap<string, string>): string {
    const parts: string[] = [];
    function append(item: JsonValue): void {
        if (item instanceof JsonNumber) {
            parts.push(item.text);
        } else if (item === null || typeof item !== "object") {
            parts.push(JSON.stringify(item));
        } else if (Array.isArray(item)) {
            parts.push("[");
            for (let index = 0; index < item.length; index++) {
                if (index !== 0) parts.push(",");
                append(item[index]);
            }
            parts.push("]");
        } else {
            parts.push("{");
            let first = true;
            for (const name of Object.keys(item)) {
                if (!first) parts.push(",");
                first = false;
                parts.push(encodedNames.get(name) ?? JSON.stringify(name) + ":");
                append(item[name]);
            }
            parts.push("}");
        }
    }
    append(value);
    return parts.join("");
}
