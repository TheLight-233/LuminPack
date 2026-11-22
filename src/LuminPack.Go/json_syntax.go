package luminpack

import (
	"bytes"
	"encoding/json"
	"unicode/utf16"
	"unicode/utf8"
)

type jsonScanner struct {
	data []byte
	pos  int
}

func (r *jsonScanner) space() {
	for r.pos < len(r.data) {
		switch r.data[r.pos] {
		case ' ', '\t', '\r', '\n':
			r.pos++
		default:
			return
		}
	}
}

func (r *jsonScanner) accept(ch byte) bool {
	r.space()
	if r.pos == len(r.data) || r.data[r.pos] != ch {
		return false
	}
	r.pos++
	return true
}

func (r *jsonScanner) expect(ch byte) {
	if !r.accept(ch) {
		fail(r.pos, "unexpected JSON token")
	}
}

func (r *jsonScanner) value(depth int) any {
	r.space()
	if r.pos >= len(r.data) {
		fail(r.pos, "truncated JSON value")
	}
	switch r.data[r.pos] {
	case '{':
		if depth == 0 {
			fail(r.pos, "configured JSON depth exceeded")
		}
		r.pos++
		fields := make(jsonObject, 0, 8)
		if r.accept('}') {
			return fields
		}
		for {
			r.expect('"')
			name := r.text()
			r.expect(':')
			fields = append(fields, jsonPair{name, r.value(depth - 1)})
			if !r.accept(',') {
				break
			}
		}
		r.expect('}')
		return fields
	case '[':
		if depth == 0 {
			fail(r.pos, "configured JSON depth exceeded")
		}
		r.pos++
		items := make([]any, 0, 8)
		if r.accept(']') {
			return items
		}
		for {
			items = append(items, r.value(depth-1))
			if !r.accept(',') {
				break
			}
		}
		r.expect(']')
		return items
	case '"':
		r.pos++
		return r.text()
	case 't':
		r.literal("true")
		return true
	case 'f':
		r.literal("false")
		return false
	case 'n':
		r.literal("null")
		return nil
	default:
		return r.number()
	}
}

func (r *jsonScanner) literal(text string) {
	if !bytes.HasPrefix(r.data[r.pos:], []byte(text)) {
		fail(r.pos, "invalid JSON literal")
	}
	r.pos += len(text)
}

func (r *jsonScanner) digit() bool {
	return r.pos < len(r.data) && r.data[r.pos] >= '0' && r.data[r.pos] <= '9'
}

func (r *jsonScanner) digits() {
	if !r.digit() {
		fail(r.pos, "expected JSON number")
	}
	for r.digit() {
		r.pos++
	}
}

func (r *jsonScanner) number() json.Number {
	return json.Number(r.numberToken())
}

func (r *jsonScanner) numberToken() []byte {
	start := r.pos
	if r.data[r.pos] == '-' {
		r.pos++
	}
	if r.pos < len(r.data) && r.data[r.pos] == '0' {
		r.pos++
	} else {
		r.digits()
	}
	if r.pos < len(r.data) && r.data[r.pos] == '.' {
		r.pos++
		r.digits()
	}
	if r.pos < len(r.data) && (r.data[r.pos] == 'E' || r.data[r.pos] == 'e') {
		r.pos++
		if r.pos < len(r.data) && (r.data[r.pos] == '+' || r.data[r.pos] == '-') {
			r.pos++
		}
		r.digits()
	}
	return r.data[start:r.pos]
}

func (r *jsonScanner) text() string {
	start := r.pos
	r.pos += jsonLiteralPrefix(r.data[r.pos:])
	if r.pos < len(r.data) && r.data[r.pos] == '"' {
		text := string(r.data[start:r.pos])
		r.pos++
		return text
	}
	var local [128]byte
	output := append(local[:0], r.data[start:r.pos]...)
	for r.pos < len(r.data) {
		next := r.data[r.pos]
		switch {
		case next == '"':
			r.pos++
			return string(output)
		case next == '\\':
			r.pos++
			output = r.escape(output)
		case next < 0x20:
			fail(r.pos, "unescaped JSON control byte")
		default:
			if next >= utf8.RuneSelf {
				ch, width := utf8.DecodeRune(r.data[r.pos:])
				if ch == utf8.RuneError && width == 1 {
					fail(r.pos, "invalid JSON UTF-8")
				}
				output = append(output, r.data[r.pos:r.pos+width]...)
				r.pos += width
			} else {
				prefix := jsonLiteralPrefix(r.data[r.pos:])
				output = append(output, r.data[r.pos:r.pos+prefix]...)
				r.pos += prefix
			}
		}
	}
	fail(r.pos, "unterminated JSON string")
	return ""
}

func (r *jsonScanner) escape(output []byte) []byte {
	if r.pos == len(r.data) {
		fail(r.pos, "truncated JSON escape")
	}
	next := r.data[r.pos]
	r.pos++
	switch next {
	case '"', '\\', '/':
		return append(output, next)
	case 'b':
		return append(output, '\b')
	case 'f':
		return append(output, '\f')
	case 'n':
		return append(output, '\n')
	case 'r':
		return append(output, '\r')
	case 't':
		return append(output, '\t')
	case 'u':
		a := r.hex()
		if a >= 0xd800 && a <= 0xdbff {
			if len(r.data)-r.pos < 2 || r.data[r.pos] != '\\' || r.data[r.pos+1] != 'u' {
				fail(r.pos, "missing JSON low-surrogate escape")
			}
			r.pos += 2
			b := r.hex()
			if b < 0xdc00 || b > 0xdfff {
				fail(r.pos, "invalid JSON low-surrogate escape")
			}
			return utf8.AppendRune(output, utf16.DecodeRune(a, b))
		}
		if a >= 0xdc00 && a <= 0xdfff {
			fail(r.pos, "unexpected JSON low-surrogate escape")
		}
		return utf8.AppendRune(output, a)
	default:
		fail(r.pos, "invalid JSON escape")
	}
	return nil
}

func (r *jsonScanner) hex() rune {
	if len(r.data)-r.pos < 4 {
		fail(r.pos, "truncated JSON Unicode escape")
	}
	var value rune
	for _, b := range r.data[r.pos : r.pos+4] {
		switch {
		case b >= '0' && b <= '9':
			b -= '0'
		case b >= 'a' && b <= 'f':
			b -= 'a' - 10
		case b >= 'A' && b <= 'F':
			b -= 'A' - 10
		default:
			fail(r.pos, "invalid JSON Unicode escape")
		}
		value = value<<4 | rune(b)
	}
	r.pos += 4
	return value
}
