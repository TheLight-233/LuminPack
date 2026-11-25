package luminpack

import (
	"fmt"
	"math"
	"strconv"
	"testing"
)

func TestGeneratedDocumentFloat(t *testing.T) {
	limits := GeneratedLimits{Input: 1024, JSONDepth: 32}
	read := func(r *DocumentJSONReader, _ int) float32 { return r.Float() }
	compare := func(token string) {
		t.Helper()
		got, err := ReadJSONDocument([]byte(token), limits, read)
		want, referenceError := strconv.ParseFloat(token, 32)
		if err != nil || referenceError != nil || math.Float32bits(got) != math.Float32bits(float32(want)) {
			t.Fatalf("%q: %v (%v), reference %v (%v)", token, got, err, want, referenceError)
		}
	}
	for _, token := range []string{"-0", "-0.00", "1e-45", "12.3000000001", "12345678.1", "16777217", "0.9999999", "1.17549435e-38", "3.4028234E38"} {
		compare(token)
	}
	for n := 0; n < 10000; n++ {
		compare(fmt.Sprintf("-%d.%03d", n, n%1000))
	}
	for _, token := range []string{"", ".", "-.5", "1.", "01", "1e", "3.5e38", "--1", "1..2", "[1]"} {
		if _, err := ReadJSONDocument([]byte(token), limits, read); err == nil {
			t.Fatalf("accepted %q", token)
		}
	}
}

func TestGeneratedDocumentOwnershipAndLimits(t *testing.T) {
	input := []byte{2, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0}
	read := func(r *DocumentReader, _ int) []int32 { return ReadDocumentScalars[int32](r) }
	write := func(w *DocumentWriter, v []int32, _ int) { WriteDocumentScalars(w, v) }
	limits := GeneratedLimits{Input: 100, Collection: 2, Complete: true}
	v, err := ReadDocument(input, limits, read)
	if err != nil {
		t.Fatal(err)
	}
	input[4] = 42
	if v[0] != 1 {
		t.Fatal("decoded values alias input")
	}
	first, err := WriteDocument(v, 1, write)
	if err != nil {
		t.Fatal(err)
	}
	v[0] = 99
	if _, err := WriteDocument(v, 1, write); err != nil || first[4] != 1 {
		t.Fatal("encoded data reused", err)
	}
	for _, b := range [][]byte{input[:11], append(input, 0), {3, 0, 0, 0}, {254, 255, 255, 255}} {
		if _, err := ReadDocument(b, limits, read); err == nil {
			t.Fatal("invalid collection accepted", b)
		}
	}
	readText := func(r *DocumentJSONReader, _ int) Text { return r.Text() }
	limits.String = 1
	if _, err := ReadJSONDocument([]byte("\"🦊\""), limits, readText); err == nil {
		t.Fatal("UTF-16 quota ignored")
	}
	limits.String = 2
	if v, err := ReadJSONDocument([]byte("\"🦊\""), limits, readText); err != nil || v.Value != "🦊" {
		t.Fatal(v, err)
	}
}
