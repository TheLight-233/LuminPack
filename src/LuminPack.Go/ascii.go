package luminpack

import (
	"encoding/binary"
	"unicode/utf8"
	"unsafe"
)

const asciiBlock = 16

func asciiStringBytes(text string) []byte {
	// Callers inspect the immutable string storage before copying to a separate
	// output. The slice never escapes or grants mutable access to a caller.
	return unsafe.Slice(unsafe.StringData(text), len(text))
}

func asciiPrefix(data []byte) int {
	vectorEnd := len(data) &^ (asciiBlock - 1)
	pos := asciiPrefixWide(data)
	if pos < len(data) && data[pos] >= utf8.RuneSelf {
		return pos
	}
	if vectorEnd > pos {
		pos += asciiPrefixVector(data[pos:vectorEnd])
		if pos != vectorEnd {
			return pos
		}
	}
	for pos < len(data) && data[pos] < utf8.RuneSelf {
		pos++
	}
	return pos
}

func widenLatin1(dst, src []byte) {
	if len(dst)/2 < len(src) {
		fail(-1, "truncated UTF-16 output")
	}
	vectorEnd := len(src) &^ (asciiBlock - 1)
	start := widenLatin1Wide(dst, src)
	if vectorEnd > start {
		widenLatin1Vector(dst[start*2:vectorEnd*2], src[start:vectorEnd])
	}
	for i := vectorEnd; i < len(src); i++ {
		binary.LittleEndian.PutUint16(dst[i*2:], uint16(src[i]))
	}
}

func narrowASCII(dst, src []byte) bool {
	if len(src)%2 != 0 || len(dst) < len(src)/2 {
		fail(-1, "invalid UTF-16 transcode width")
	}
	vectorEnd := (len(src) / 2) &^ (asciiBlock - 1)
	start := narrowASCIIWide(dst, src)
	if start < 0 || (vectorEnd > start && !narrowASCIIVector(dst[start:vectorEnd], src[start*2:vectorEnd*2])) {
		return false
	}
	for i := vectorEnd; i < len(src)/2; i++ {
		unit := binary.LittleEndian.Uint16(src[i*2:])
		if unit >= utf8.RuneSelf {
			return false
		}
		dst[i] = byte(unit)
	}
	return true
}

func jsonSpecial(b byte) bool {
	return b < 0x20 || b >= utf8.RuneSelf || b == '"' || b == '\\'
}

func jsonLiteralPrefix(data []byte) int {
	pos := 0
	// Most generated names and short strings end before one vector.
	for limit := min(len(data), 4); pos < limit; pos++ {
		if jsonSpecial(data[pos]) {
			return pos
		}
	}
	vectorEnd := pos + (len(data)-pos)&^(asciiBlock-1)
	if vectorEnd > pos {
		pos += jsonLiteralVector(data[pos:vectorEnd])
		if pos != vectorEnd {
			return pos
		}
	}
	for pos < len(data) && !jsonSpecial(data[pos]) {
		pos++
	}
	return pos
}
