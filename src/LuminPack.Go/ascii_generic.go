//go:build (!amd64 && !arm64) || purego

package luminpack

import "encoding/binary"

func asciiPrefixVector(src []byte) int {
	const highBits = 0x8080808080808080
	pos := 0
	for len(src)-pos >= 8 && binary.LittleEndian.Uint64(src[pos:])&highBits == 0 {
		pos += 8
	}
	for pos < len(src) && src[pos] < 128 {
		pos++
	}
	return pos
}

func widenLatin1Vector(dst, src []byte) {
	for i, b := range src {
		binary.LittleEndian.PutUint16(dst[i*2:], uint16(b))
	}
}

func narrowASCIIVector(dst, src []byte) bool {
	for i := range dst {
		word := binary.LittleEndian.Uint16(src[i*2:])
		if word >= 128 {
			return false
		}
		dst[i] = byte(word)
	}
	return true
}

func jsonLiteralVector(src []byte) int {
	for i, b := range src {
		if jsonSpecial(b) {
			return i
		}
	}
	return len(src)
}

func matchPrefixVector(a, b []byte) int { return matchPrefixScalar(a, b) }
