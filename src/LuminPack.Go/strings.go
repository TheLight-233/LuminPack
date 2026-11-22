package luminpack

import (
	"bytes"
	"encoding/binary"
	"math"
	"slices"
	"unicode/utf16"
	"unicode/utf8"
)

func (r *binaryReader) varuint(max uint64, allowNoncanonical bool) uint64 {
	var result uint64
	for shift := 0; shift <= 28; shift += 7 {
		b := r.byte()
		result |= uint64(b&127) << shift
		if b < 128 {
			if result <= max && (allowNoncanonical || shift == 0 || b != 0) {
				return result
			}
			break
		}
	}
	fail(r.pos, "invalid variable-length integer")
	return 0
}

func (w *binaryWriter) varuint(value uint64) { w.data = binary.AppendUvarint(w.data, value) }

func (r *binaryReader) textLength(variable bool) int {
	if variable {
		return int(r.varuint(0x80000000, false)) - 1
	}
	return int(int32(r.u32()))
}

func (w *binaryWriter) textLength(length int, variable bool) {
	if variable {
		w.varuint(uint64(length + 1))
	} else {
		w.u32(uint32(int32(length)))
	}
}

func (r *binaryReader) text(profile int) any {
	return r.readText(profile, false)
}

func (r *binaryReader) readText(profile int, strictSkip bool) any {
	validateBinaryProfile(profile)
	uses8 := profile == 0 || profile == 1 || profile == 4
	token := profile == 1 || profile == 3
	variable := profile == 4 || profile == 5
	characters := -1
	latin1 := false
	var payload []byte
	if token {
		rest := r.data[r.pos:]
		if uses8 {
			end := bytes.IndexByte(rest, 0)
			if end < 0 {
				fail(r.pos, "missing UTF-8 token terminator")
			}
			payload = r.take(end + 1)[:end]
		} else {
			end := 0
			for len(rest)-end >= 2 && (rest[end] != 0 || rest[end+1] != 0) {
				end += 2
			}
			if len(rest)-end < 2 {
				fail(r.pos, "missing UTF-16 token terminator")
			}
			payload = r.take(end + 2)[:end]
		}
	} else {
		header := r.textLength(variable)
		if uses8 {
			characters = r.textLength(variable)
		}
		if header < -1 {
			fail(r.pos, "invalid string length")
		}
		if uses8 && variable && (header == -1 && characters != -1 || header >= 0 && (characters < 0 || characters > header || header != 0 && characters == 0)) {
			fail(r.pos, "invalid UTF-8 character count")
		}
		if header == -1 {
			return nil
		}
		latin1 = !uses8 && header&1 != 0
		if latin1 {
			header >>= 1
		}
		payload = r.take(header)
	}
	if uses8 {
		if len(payload) == 0 {
			return ""
		}
		if characters > len(payload) || characters > r.maxString {
			fail(r.pos, "invalid UTF-8 character count")
		}
		units, decoded := readUTF8Text(payload, characters <= 0 && !strictSkip)
		if units > r.maxString || characters > 0 && characters != units {
			fail(r.pos, "invalid UTF-8 character count")
		}
		return decoded
	}
	if latin1 {
		if len(payload) > r.maxString {
			fail(r.pos, "UTF-16 text exceeds configured limit")
		}
		for _, b := range payload {
			if b >= 128 {
				result := make([]byte, 0, len(payload)+len(payload)/2)
				for _, ch := range payload {
					result = utf8.AppendRune(result, rune(ch))
				}
				return string(result)
			}
		}
		return string(payload)
	}
	if len(payload)/2 > r.maxString {
		fail(r.pos, "UTF-16 text exceeds configured limit")
	}
	return decodeUTF16(payload, token)
}

func readUTF8Text(data []byte, replace bool) (int, string) {
	units := asciiPrefix(data)
	var repaired []byte
	last := 0
	for pos := units; pos < len(data); {
		ch, size := utf8.DecodeRune(data[pos:])
		if ch == utf8.RuneError && size == 1 {
			if !replace {
				fail(pos, "invalid UTF-8 payload")
			}
			size = invalidUTF8Prefix(data[pos:])
			repaired = append(repaired, data[last:pos]...)
			repaired = utf8.AppendRune(repaired, utf8.RuneError)
			last = pos + size
		}
		pos += size
		units++
		if ch > 0xffff {
			units++
		}
	}
	if repaired != nil {
		return units, string(append(repaired, data[last:]...))
	}
	return units, string(data)
}

// The managed fallback consumes the longest valid prefix of an incomplete
// scalar, then emits one replacement character for that prefix.
func invalidUTF8Prefix(data []byte) int {
	first := data[0]
	width, lower, upper := 1, byte(0x80), byte(0xbf)
	switch {
	case first >= 0xc2 && first <= 0xdf:
		width = 2
	case first >= 0xe0 && first <= 0xef:
		width = 3
		if first == 0xe0 {
			lower = 0xa0
		} else if first == 0xed {
			upper = 0x9f
		}
	case first >= 0xf0 && first <= 0xf4:
		width = 4
		if first == 0xf0 {
			lower = 0x90
		} else if first == 0xf4 {
			upper = 0x8f
		}
	}
	size := 1
	for size < min(width, len(data)) && data[size] >= lower && data[size] <= upper {
		size++
		lower, upper = 0x80, 0xbf
	}
	return size
}

func decodeUTF16(data []byte, replace bool) any {
	if len(data)%2 != 0 {
		fail(-1, "invalid UTF-16 byte length")
	}
	if len(data) > 0 && binary.LittleEndian.Uint16(data) < utf8.RuneSelf {
		var local [256]byte
		var text []byte
		if length := len(data) / 2; length <= len(local) {
			text = local[:length]
		} else {
			text = make([]byte, length)
		}
		if narrowASCII(text, data) {
			return string(text)
		}
	}
	units := make([]uint16, len(data)/2)
	for i := range units {
		units[i] = binary.LittleEndian.Uint16(data[i*2:])
	}
	return fromUnits(units, replace)
}

func measureUTF16(value string) (units int, latin1 bool) {
	latin1 = true
	position := asciiPrefix(asciiStringBytes(value))
	for remaining := value[position:]; len(remaining) != 0; {
		ch, width := utf8.DecodeRuneInString(remaining)
		if ch == utf8.RuneError && width == 1 {
			fail(-1, "invalid Go UTF-8 string")
		}
		remaining = remaining[width:]
		units++
		if ch > 0xffff {
			units++
		}
		if ch > 255 {
			latin1 = false
		}
	}
	return units + position, latin1
}

func (w *binaryWriter) text(value any, profile int) {
	validateBinaryProfile(profile)
	uses8 := profile == 0 || profile == 1 || profile == 4
	token := profile == 1 || profile == 3
	variable := profile == 4 || profile == 5
	if value == nil && !token {
		w.textLength(-1, variable)
		if uses8 {
			w.textLength(-1, variable)
		}
		return
	}
	var text string
	var raw UTF16String
	switch v := value.(type) {
	case nil:
	case string:
		text = v
	case UTF16String:
		if uses8 {
			if converted, ok := fromUnits(v, false).(string); ok {
				text = converted
			} else {
				fail(-1, "invalid UTF-16 surrogate for UTF-8 encoding")
			}
		} else {
			raw = v
		}
	default:
		fail(-1, "expected text value")
	}
	units, latin1 := measureUTF16(text)
	if raw != nil {
		units = len(raw)
		for _, unit := range raw {
			if unit > 255 {
				latin1 = false
				break
			}
		}
	}
	if len(text) > math.MaxInt32 || units > math.MaxInt32 || (!uses8 && units > math.MaxInt32/2) {
		fail(-1, "string exceeds wire length")
	}
	if uses8 {
		if !token {
			w.textLength(len(text), variable)
			w.textLength(units, variable)
		}
		w.data = append(w.data, text...)
		if token {
			w.byte(0)
		}
		return
	}
	compact := latin1 && units > 0 && !token
	if !token {
		header := units * 2
		if compact {
			header++
		}
		w.textLength(header, variable)
	}
	if compact {
		if raw != nil {
			for _, unit := range raw {
				w.byte(byte(unit))
			}
		} else if len(text) == units {
			w.data = append(w.data, text...)
		} else {
			for _, ch := range text {
				w.byte(byte(ch))
			}
		}
		return
	}
	if raw != nil {
		for _, unit := range raw {
			w.u16(unit)
		}
	} else {
		w.data = appendUTF16Text(w.data, text)
	}
	if token {
		w.u16(0)
	}
}

func appendUTF16Text(output []byte, text string) []byte {
	bytes := asciiStringBytes(text)
	prefix := asciiPrefix(bytes)
	if prefix > 0 {
		start := len(output)
		if prefix > (math.MaxInt-start)/2 {
			fail(-1, "UTF-16 output exceeds size limit")
		}
		output = slices.Grow(output, prefix*2)[:start+prefix*2]
		widenLatin1(output[start:], bytes[:prefix])
	}
	for _, ch := range text[prefix:] {
		if ch > 0xffff {
			first, second := utf16.EncodeRune(ch)
			output = binary.LittleEndian.AppendUint16(output, uint16(first))
			output = binary.LittleEndian.AppendUint16(output, uint16(second))
		} else {
			output = binary.LittleEndian.AppendUint16(output, uint16(ch))
		}
	}
	return output
}

func parseJSONString(text any) string {
	if value, ok := text.(string); ok {
		return value
	}
	if raw, ok := text.(UTF16String); ok {
		return string(utf16.Decode(raw))
	}
	fail(-1, "expected JSON string")
	return ""
}
