package luminpack

import (
	"bytes"
	"encoding/binary"
	"testing"
)

func TestBoundedASCIITranscoding(t *testing.T) {
	testBoundedASCIITranscoding(t)
}

func testBoundedASCIITranscoding(t *testing.T) {
	for length := 0; length <= 97; length++ {
		input := make([]byte, length+1)[1:]
		for i := range input {
			input[i] = byte((i*17 + 1) % 128)
		}
		if n := asciiPrefix(input); n != length {
			t.Fatalf("ASCII prefix of %d bytes: %d", length, n)
		}
		wide := bytes.Repeat([]byte{0xa5}, length*2+2)
		widenLatin1(wide[1:len(wide)-1], input)
		if wide[0] != 0xa5 || wide[len(wide)-1] != 0xa5 {
			t.Fatalf("widening at length %d modified guard byte", length)
		}
		for i, want := range input {
			if got := binary.LittleEndian.Uint16(wide[1+i*2:]); got != uint16(want) {
				t.Fatalf("widening byte %d of %d: %d, want %d", i, length, got, want)
			}
		}
		narrow := bytes.Repeat([]byte{0xa5}, length+2)
		if !narrowASCII(narrow[1:len(narrow)-1], wide[1:len(wide)-1]) ||
			!bytes.Equal(narrow[1:len(narrow)-1], input) ||
			narrow[0] != 0xa5 || narrow[len(narrow)-1] != 0xa5 {
			t.Fatalf("narrowing at length %d failed or modified a guard byte", length)
		}
		for i, b := range input {
			input[i] = b | 0x80
			if n := asciiPrefix(input); n != i {
				t.Fatalf("ASCII prefix of %d-byte mixed text: %d, want %d", length, n, i)
			}
			widenLatin1(wide[1:len(wide)-1], input)
			if got := binary.LittleEndian.Uint16(wide[1+i*2:]); got != uint16(input[i]) {
				t.Fatalf("Latin-1 widening byte %d of %d: %d, want %d", i, length, got, input[i])
			}
			if narrowASCII(narrow[1:len(narrow)-1], wide[1:len(wide)-1]) {
				t.Fatalf("accepted non-ASCII at position %d of %d", i, length)
			}
			wide[1+i*2], wide[2+i*2] = 0, 1
			if narrowASCII(narrow[1:len(narrow)-1], wide[1:len(wide)-1]) {
				t.Fatalf("accepted non-Latin-1 at position %d of %d", i, length)
			}
			input[i] = b
		}
	}
}

func TestBoundedJSONLiteralPrefix(t *testing.T) {
	for n := 0; n < 128; n++ {
		text := bytes.Repeat([]byte{'A'}, n)
		if pos := jsonLiteralPrefix(text); pos != n {
			t.Fatalf("ASCII text of length %d stopped at %d", n, pos)
		}
		for i := range text {
			for _, ch := range []byte{0, '\t', 0x1f, '"', '\\', 0x80, 0xff} {
				text[i] = ch
				if pos := jsonLiteralPrefix(text); pos != i {
					t.Fatalf("special byte %x at position %d of %d, stopped at %d", ch, i, n, pos)
				}
			}
			text[i] = 'A'
		}
	}
}

func TestBoundedMatchPrefix(t *testing.T) {
	testBoundedMatchPrefix(t)
}

func testBoundedMatchPrefix(t *testing.T) {
	for length := 0; length <= 145; length++ {
		a := make([]byte, length+1)[1:]
		b := make([]byte, length+3)[3:]
		for i := range a {
			a[i], b[i] = byte(i*37+1), byte(i*37+1)
		}
		if pos := matchPrefix(a, b); pos != length {
			t.Fatalf("equal ranges of %d stopped at %d", length, pos)
		}
		for i := range a {
			b[i] ^= 0xff
			if pos := matchPrefix(a, b); pos != i {
				t.Fatalf("mismatch at %d of %d stopped at %d", i, length, pos)
			}
			b[i] ^= 0xff
		}
	}
}
