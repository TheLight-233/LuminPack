package luminpack

import (
	"bytes"
	"encoding/json"
	"math"
	"math/rand"
	"strconv"
	"testing"
)

func TestCanonicalJSONInteger(t *testing.T) {
	var output [32]byte
	checkUnsigned := func(value uint64) {
		t.Helper()
		expected := strconv.FormatUint(value, 10)
		if got := string(appendJSONUint64(output[:0], value)); got != expected {
			t.Fatalf("uint64 %d: got %q, want %q", value, got, expected)
		}
		if value <= math.MaxUint32 {
			if got := string(appendJSONUint32(output[:0], uint32(value))); got != expected {
				t.Fatalf("uint32 %d: got %q, want %q", value, got, expected)
			}
		}
	}
	checkSigned := func(value int64, width int) {
		t.Helper()
		expected := "x" + strconv.FormatInt(value, 10)
		output[0] = 'x'
		if got := string(appendJSONInteger(output[:1], uint64(value), true, width)); got != expected {
			t.Fatalf("int%d %d: got %q, want %q", width*8, value, got, expected)
		}
	}
	for value := uint64(0); value <= 10_000; value++ {
		checkUnsigned(value)
		checkSigned(-int64(value), 4)
	}
	for _, value := range []uint64{65_535, 99_999_999, 100_000_000, math.MaxUint32, math.MaxUint32 + 1, math.MaxUint64} {
		checkUnsigned(value)
	}
	checkSigned(math.MinInt8, 1)
	checkSigned(math.MinInt16, 2)
	checkSigned(math.MinInt32, 4)
	checkSigned(math.MinInt64, 8)
	random := rand.New(rand.NewSource(85))
	for range 10_000 {
		value := random.Uint64()
		checkUnsigned(value)
		checkUnsigned(uint64(uint32(value)))
		checkSigned(int64(value), 8)
	}
}

func TestCanonicalJSONFloatingPoint(t *testing.T) {
	for _, test := range []struct {
		kind  string
		value any
		json  string
	}{
		{"float", math.Float32frombits(1), "1E-45"},
		{"float", math.Float32frombits(0x00800000), "1.1754944E-38"},
		{"float", math.Float32frombits(0x4f800000), "4294967300"},
		{"float", float32(-12.125), "-12.125"},
		{"float", math.Float32frombits(0x80000000), "-0"},
		{"float", math.Float64frombits(0x8000000000000000), "-0"},
		{"double", math.Float64frombits(1), "5E-324"},
		{"double", math.Float64frombits(0x0410000000000000), "4.1045368012983762E-289"},
		{"double", math.Float64frombits(0x43d0000000000000), "4.611686018427388E+18"},
		{"double", math.Float64frombits(0x8000000000000000), "-0"},
		{"double", float32(2.25), "2.25"},
	} {
		schema := floatSchema(t, test.kind)
		for _, encoding := range []JSONEncoding{UTF8, UTF16} {
			want := []byte(test.json)
			if encoding == UTF16 {
				want = appendUTF16Text(nil, test.json)
			}
			got, err := schema.EncodeJSONWithEncoding("", test.value, encoding)
			if err != nil || !bytes.Equal(got, want) {
				t.Errorf("%s encoding %d: got %q, want %q, error %v", test.kind, encoding, got, want, err)
			}
		}
	}
}

func TestJSONRefusesNonFiniteNumbers(t *testing.T) {
	for _, kind := range []string{"float", "double"} {
		schema := floatSchema(t, kind)
		for _, encoding := range []JSONEncoding{UTF8, UTF16} {
			for _, value := range []float64{math.NaN(), math.Inf(-1), math.Inf(1)} {
				if _, err := schema.EncodeJSONWithEncoding("", value, encoding); err == nil {
					t.Errorf("%s encoding %d accepted non-finite value %v", kind, encoding, value)
				}
			}
			for _, direct := range []bool{false, true} {
				schema.rootJSONDirect = direct
				for _, token := range []string{"\"NaN\"", "\"Infinity\"", "\"-Infinity\"", "1E+999"} {
					data := []byte(token)
					if encoding == UTF16 {
						data = appendUTF16Text(nil, token)
					}
					if _, err := schema.DecodeJSONWithEncoding("", data, encoding); err == nil {
						t.Errorf("%s encoding %d, direct %t accepted token %s", kind, encoding, direct, token)
					}
				}
			}
		}
	}
}

func floatSchema(t *testing.T, kind string) *Schema {
	t.Helper()
	source := "{\"version\":1,\"root\":\"Number\",\"rootWire\":{\"kind\":\"scalar\",\"type\":\"" + kind + "\"}}"
	schema, err := LoadSchema([]byte(source))
	if err != nil {
		t.Fatal(err)
	}
	return schema
}

func TestCustomJSONRetainsNumberToken(t *testing.T) {
	schema, err := LoadSchema([]byte("{\"version\":1,\"root\":\"Number\",\"contracts\":[{\"id\":\"Number\",\"kind\":\"Custom\",\"customJson\":true}]}"))
	if err != nil {
		t.Fatal(err)
	}
	var calls int
	codec := customFixtureCodec(&calls)
	codec.ReadJSON = func(value json.RawMessage) (any, error) { return string(value), nil }
	codec.WriteJSON = func(value any) (json.RawMessage, error) { return []byte(value.(string)), nil }
	schema, err = schema.WithCustom("Number", codec)
	if err != nil {
		t.Fatal(err)
	}
	const token = "100.00e-02"
	input := appendUTF16Text(nil, token)
	value, err := schema.DecodeJSONWithEncoding("", input, UTF16)
	if err != nil || value != token {
		t.Fatalf("Custom reader changed %q to %q: %v", token, value, err)
	}
	output, err := schema.EncodeJSONWithEncoding("", value, UTF16)
	if err != nil || !bytes.Equal(output, input) {
		t.Fatalf("Custom writer changed %q: %x, %v", token, output, err)
	}
}
