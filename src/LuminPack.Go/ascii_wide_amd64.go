//go:build !purego

package luminpack

const (
	avx2Lanes   = 32
	avx512Lanes = 64
)

// The probe checks CPUID and the OS-enabled vector register state once.
var vectorFeatures = detectVectorFeatures()

func detectVectorFeatures() uint8

//go:noescape
func asciiPrefixAVX2(src []byte) int

//go:noescape
func widenLatin1AVX2(dst, src []byte)

//go:noescape
func narrowASCIIAVX2(dst, src []byte) bool

//go:noescape
func asciiPrefixAVX512(src []byte) int

//go:noescape
func widenLatin1AVX512(dst, src []byte)

//go:noescape
func narrowASCIIAVX512(dst, src []byte) bool

//go:noescape
func matchPrefixAVX2(a, b []byte) int

//go:noescape
func matchPrefixAVX512(a, b []byte) int

func asciiPrefixWide(src []byte) int {
	pos := 0
	if length := len(src) &^ (avx512Lanes - 1); length > 0 && vectorFeatures&2 != 0 {
		pos = asciiPrefixAVX512(src[:length])
		if pos < length {
			return pos
		}
	}
	if length := len(src) &^ (avx2Lanes - 1); length > pos && vectorFeatures&1 != 0 {
		pos += asciiPrefixAVX2(src[pos:length])
	}
	return pos
}

func widenLatin1Wide(dst, src []byte) int {
	pos := 0
	if length := len(src) &^ (avx512Lanes - 1); length > 0 && vectorFeatures&2 != 0 {
		widenLatin1AVX512(dst[:length*2], src[:length])
		pos = length
	}
	if length := len(src) &^ (avx2Lanes - 1); length > pos && vectorFeatures&1 != 0 {
		widenLatin1AVX2(dst[pos*2:length*2], src[pos:length])
		pos = length
	}
	return pos
}

func narrowASCIIWide(dst, src []byte) int {
	pos := 0
	if length := len(src) / 2 &^ (avx512Lanes - 1); length > 0 && vectorFeatures&2 != 0 {
		if !narrowASCIIAVX512(dst[:length], src[:length*2]) {
			return -1
		}
		pos = length
	}
	if length := len(src) / 2 &^ (avx2Lanes - 1); length > pos && vectorFeatures&1 != 0 {
		if !narrowASCIIAVX2(dst[pos:length], src[pos*2:length*2]) {
			return -1
		}
		pos = length
	}
	return pos
}

func matchPrefixWide(a, b []byte) int {
	pos := 0
	if length := len(a) &^ (avx512Lanes - 1); length > 0 && vectorFeatures&2 != 0 {
		pos = matchPrefixAVX512(a[:length], b[:length])
		if pos < length {
			return pos
		}
	}
	if length := len(a) &^ (avx2Lanes - 1); length > pos && vectorFeatures&1 != 0 {
		pos += matchPrefixAVX2(a[pos:length], b[pos:length])
	}
	return pos
}
