//go:build (amd64 || arm64) && !purego

package luminpack

// The amd64 and arm64 baselines include SSE2 and Advanced SIMD respectively.
// These entry points receive nonempty blocks proved bounded by ascii.go.

//go:noescape
func asciiPrefixVector(src []byte) int

//go:noescape
func widenLatin1Vector(dst, src []byte)

//go:noescape
func narrowASCIIVector(dst, src []byte) bool

//go:noescape
func jsonLiteralVector(src []byte) int

//go:noescape
func matchPrefixVector(a, b []byte) int
