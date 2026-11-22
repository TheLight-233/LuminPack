const magic = 0x324d554c;
const headerSize = 8;
const maximumSource = 2_130_836_464;
const minimumMatch = 4;
const lastLiterals = 5;
const matchGuard = 12;
const saturated = 15;
const lengthEscape = 255;

let positions = new Uint16Array(0);

/** Encodes the managed LUM2 match, lazy probe and length-extension format. */
export function compressLum2(source: Uint8Array): Uint8Array {
    const length = source.length;
    if (length > maximumSource) throw new RangeError("LUM2 source exceeds the maximum length.");
    const result = new Uint8Array(length === 0 ? headerSize : headerSize + length + (length >>> 7) + 16);
    const destination = new DataView(result.buffer);
    destination.setUint32(0, magic, true);
    destination.setUint32(4, length, true);
    if (length === 0) return result;

    const input = new DataView(source.buffer, source.byteOffset, length);
    const bits = Math.max(8, Math.min(15, 32 - Math.clz32((length - 1) | 1)));
    const shift = 32 - bits;
    if (positions.length < (1 << bits)) positions = new Uint16Array(1 << bits);
    const table = positions;
    const hash = (word: number) => Math.imul(word, 2654435761) >>> shift;
    const load = (offset: number) => input.getUint32(offset, true);
    let output = headerSize;
    let anchor = 0;

    function extension(size: number): void {
        if (size >= lengthEscape) {
            result[output++] = lengthEscape;
            size -= lengthEscape;
            while (size >= 128) {
                result[output++] = (size & 0x7f) | 0x80;
                size >>>= 7;
            }
        }
        result[output++] = size;
    }

    function literals(start: number, size: number, match: number): void {
        result[output++] = (Math.min(saturated, size) << 4) | Math.min(saturated, match);
        if (size >= saturated) extension(size - saturated);
        result.set(source.subarray(start, start + size), output);
        output += size;
    }

    function matchLength(position: number, candidate: number, end: number): number {
        let offset = minimumMatch;
        for (; position + offset + 4 <= end; offset += 4) {
            const difference = load(position + offset) ^ load(candidate + offset);
            if (difference !== 0) return offset + ((31 - Math.clz32(difference & -difference)) >>> 3);
        }
        while (position + offset < end && source[position + offset] === source[candidate + offset]) ++offset;
        return offset;
    }

    if (length >= minimumMatch + lastLiterals + matchGuard) {
        const matchLimit = length - matchGuard;
        const extensionLimit = length - lastLiterals;
        table[hash(load(0))] = 0;
        let position = 1;
        let skip = 32;
        while (position < matchLimit) {
            const word = load(position);
            const slot = hash(word);
            const distance = (position - table[slot]) & 0xffff;
            table[slot] = position;
            if (distance === 0 || distance > position || load(position - distance) !== word) {
                position += skip >>> 5;
                ++skip;
                continue;
            }
            skip = 32;
            let candidate = position - distance;
            while (position > anchor && candidate !== 0 && source[position - 1] === source[candidate - 1]) {
                --position;
                --candidate;
            }
            let match = matchLength(position, candidate, extensionLimit);
            const lazyPosition = position + 1;
            if (lazyPosition < matchLimit) {
                const lazyWord = load(lazyPosition);
                const lazySlot = hash(lazyWord);
                const lazyDistance = (lazyPosition - table[lazySlot]) & 0xffff;
                table[lazySlot] = lazyPosition;
                const lazyCandidate = lazyPosition - lazyDistance;
                if (lazyDistance !== 0 && lazyDistance <= lazyPosition && extensionLimit - lazyPosition > match &&
                    load(lazyCandidate) === lazyWord &&
                    load(lazyPosition + match - 3) === load(lazyCandidate + match - 3)) {
                    const lazyLength = matchLength(lazyPosition, lazyCandidate, extensionLimit);
                    if (lazyLength > match) {
                        position = lazyPosition;
                        candidate = lazyCandidate;
                        match = lazyLength;
                        while (position > anchor && candidate !== 0 && source[position - 1] === source[candidate - 1]) {
                            --position;
                            --candidate;
                            ++match;
                        }
                    }
                }
            }
            const nibble = match - minimumMatch;
            literals(anchor, position - anchor, nibble);
            destination.setUint16(output, position - candidate, true);
            output += 2;
            if (nibble >= saturated) extension(nibble - saturated);
            position += match;
            anchor = position;
            if (position < matchLimit) {
                for (let boundary = position - 2; boundary < position; ++boundary) table[hash(load(boundary))] = boundary;
            }
        }
    }
    literals(anchor, length - anchor, 0);
    return result.subarray(0, output);
}

/** Expands exactly the byte count validated by the enclosing collection. */
export function decompressLum2(frame: Uint8Array, expected: number): Uint8Array {
    function invalid(): never { throw new RangeError("Invalid LUM2 compression frame."); }
    if (!Number.isSafeInteger(expected) || expected < 0 || expected > 0x7fffffff || frame.length < headerSize) invalid();
    const input = new DataView(frame.buffer, frame.byteOffset, frame.length);
    if (input.getUint32(0, true) !== magic || input.getUint32(4, true) !== expected) invalid();
    if (expected === 0) {
        if (frame.length !== headerSize) invalid();
        return new Uint8Array(0);
    }

    function readBody(result?: Uint8Array): void {
        let position = headerSize;
        let produced = 0;
        function readByte(): number {
            if (position === frame.length) invalid();
            return frame[position++];
        }
        function readExtension(length: number): number {
            const first = readByte();
            length += first;
            if (length > expected - produced) invalid();
            if (first !== lengthEscape) return length;
            let extra = 0;
            for (let shift = 0; shift <= 28; shift += 7) {
                const next = readByte();
                if (shift === 28 && next > 7) invalid();
                extra += (next & 0x7f) * 2 ** shift;
                if (extra > expected - produced - length) invalid();
                if (next < 128) {
                    if (shift !== 0 && next === 0) invalid();
                    return length + extra;
                }
            }
            return invalid();
        }
        while (position < frame.length) {
            const token = readByte();
            let literals = token >>> 4;
            if (literals === saturated) literals = readExtension(literals);
            if (literals > frame.length - position || literals > expected - produced) invalid();
            if (result && literals !== 0) result.set(frame.subarray(position, position + literals), produced);
            position += literals;
            produced += literals;
            const nibble = token & 15;
            if (position === frame.length) {
                if (nibble !== 0 || produced !== expected) invalid();
                return;
            }

            if (frame.length - position < 2) invalid();
            const distance = input.getUint16(position, true);
            position += 2;
            if (distance === 0 || distance > produced) invalid();
            const match = nibble === saturated ? readExtension(nibble + minimumMatch) : nibble + minimumMatch;
            if (match > expected - produced) invalid();
            if (result) {
                const origin = produced - distance;
                for (let copied = 0; copied < match;) {
                    const count = Math.min(distance + copied, match - copied);
                    result.copyWithin(produced + copied, origin, origin + count);
                    copied += count;
                }
            }
            produced += match;
        }
        invalid();
    }
    if (Math.floor((expected - 1) / lengthEscape) >= frame.length - headerSize) readBody();
    const result = new Uint8Array(expected);
    readBody(result);
    return result;
}
