package luminpack

import (
	"encoding/binary"
	"math/bits"
)

func matchPrefix(a, b []byte) int {
	pos := matchPrefixWide(a, b)
	if pos < len(a) && a[pos] != b[pos] {
		return pos
	}
	end := len(a) &^ (asciiBlock - 1)
	if end > pos {
		pos += matchPrefixVector(a[pos:end], b[pos:end])
		if pos != end {
			return pos
		}
	}
	return pos + matchPrefixScalar(a[pos:], b[pos:])
}

func matchPrefixScalar(a, b []byte) int {
	i := 0
	for len(a)-i >= 8 {
		word := binary.LittleEndian.Uint64(a[i:]) ^ binary.LittleEndian.Uint64(b[i:])
		if word != 0 {
			return i + bits.TrailingZeros64(word)/8
		}
		i += 8
	}
	for i < len(a) && a[i] == b[i] {
		i++
	}
	return i
}
