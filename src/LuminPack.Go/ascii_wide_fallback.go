//go:build !amd64 || purego

package luminpack

func asciiPrefixWide([]byte) int         { return 0 }
func widenLatin1Wide([]byte, []byte) int { return 0 }
func narrowASCIIWide([]byte, []byte) int { return 0 }
func matchPrefixWide([]byte, []byte) int { return 0 }
