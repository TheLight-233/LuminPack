package luminpack

import (
	"encoding/json"
	"errors"
	"reflect"
	"strings"
	"testing"
)

const jsonDirectManifest = "{\"version\":1,\"root\":\"Root\",\"contracts\":[{\"id\":\"Root\",\"kind\":\"Record\",\"members\":[{\"name\":\"Count\",\"oldNames\":[\"Earlier\"],\"wire\":{\"kind\":\"scalar\",\"type\":\"int\"}},{\"name\":\"Values\",\"wire\":{\"kind\":\"sequence\",\"element\":{\"kind\":\"scalar\",\"type\":\"int\"}}},{\"name\":\"Scores\",\"wire\":{\"kind\":\"dictionary\",\"key\":{\"kind\":\"scalar\",\"type\":\"int\"},\"value\":{\"kind\":\"scalar\",\"type\":\"long\"}}},{\"name\":\"Choice\",\"wire\":{\"kind\":\"contract\",\"type\":\"IChoice\"}}]},{\"id\":\"IChoice\",\"kind\":\"Union\",\"cases\":[{\"id\":\"Node\",\"hash\":12}]},{\"id\":\"Node\",\"kind\":\"Record\",\"members\":[{\"name\":\"X\",\"wire\":{\"kind\":\"scalar\",\"type\":\"float\"}}]}]}"

func TestDirectJSONMatchesStagedReader(t *testing.T) {
	fast, err := LoadSchema([]byte(jsonDirectManifest))
	if err != nil {
		t.Fatal(err)
	}
	staged, err := LoadSchema([]byte(jsonDirectManifest))
	if err != nil {
		t.Fatal(err)
	}
	if !fast.get("").jsonDirect {
		t.Fatal("test model did not select the direct reader")
	}
	staged.get("").jsonDirect = false
	const ordered = "{\"Count\":-2147483648,\"Values\":[-2,0,2147483647],\"Scores\":[[-9,9223372036854775807]],\"Choice\":{\"$type\":12,\"$value\":{\"X\":1.25}}}"
	inputs := []string{
		ordered,
		"{ \"Choice\" : {\"$value\" : {\"X\": 1.25}, \"$type\": 12}, \"Scores\": [[-9,9223372036854775807]], \"Values\": [-2, 0, 2147483647], \"Earl\\u0069er\" : -2147483648, \"Unknown\": [true]}",
		"{\"Count\":null,\"Values\":[],\"Scores\":null,\"Choice\":{\"$type\": 12,\"$value\":{\"X\":\"NaN\"},\"Extra\":1}}",
		"{\"Count\":-0,\"Count\":2,\"Choice\":{\"$type\":12,\"$type\":41,\"$value\":{\"X\":2.5}}}",
		"{}",
	}
	for _, encoding := range []JSONEncoding{UTF8, UTF16} {
		for _, text := range inputs {
			data := []byte(text)
			if encoding == UTF16 {
				data = appendUTF16Text(nil, text)
			}
			result, err := fast.DecodeJSONWithEncoding("", data, encoding)
			want, previous := staged.DecodeJSONWithEncoding("", data, encoding)
			if err != nil || previous != nil {
				t.Fatalf("decode of %s: direct error %v, staged error %v", text, err, previous)
			}
			// JSON encoding gives a stable comparison for the named NaN value.
			if strings.Contains(text, "NaN") {
				gotBytes, gotError := fast.EncodeJSONWithEncoding("", result, encoding)
				wantBytes, wantError := staged.EncodeJSONWithEncoding("", want, encoding)
				if gotError != nil || wantError != nil || !reflect.DeepEqual(gotBytes, wantBytes) {
					t.Fatalf("direct and staged NaN round trips differ: %v, %v", gotError, wantError)
				}
			} else if !reflect.DeepEqual(result, want) {
				t.Fatalf("direct and staged decode of %s differ", text)
			}
			if text == ordered {
				record := result.(Record)
				record["Values"] = []int32{-2, 0, 2147483647}
				output, err := fast.EncodeJSONWithEncoding("", record, encoding)
				if err != nil || !reflect.DeepEqual(output, data) {
					t.Fatalf("typed integer write changed canonical bytes: %v", err)
				}
			}
		}
	}
}

func TestDirectJSONRejectsInvalidHeadsAndNumbers(t *testing.T) {
	schema, err := LoadSchema([]byte(jsonDirectManifest))
	if err != nil {
		t.Fatal(err)
	}
	for _, malformed := range []string{
		"{\"Count\":01}", "{\"Count\":-2147483649}", "{\"Count\":1e2}", "{\"Count\":1.0}", "{\"Count\":2147483648}",
		"{\"Count\":", "{\"Count\":0,\"Values\":[1,]}", "{\"Count\":0 \"Values\":[1]}", "{\"Count\":0,\"Values\":[1],}",
		"{\"Choice\":{\"$type\":", "{\"Choice\":{\"$type\":12,\"$value\":{}}", "{\"Scores\":[[1,2,3]]}", "{}[]",
	} {
		_, err := schema.DecodeJSONWithEncoding("", []byte(malformed), UTF8)
		var format *FormatError
		if !errors.As(err, &format) {
			t.Errorf("expected a format error for %s, got %v", malformed, err)
		}
	}

	var source schemaWire
	json.Unmarshal([]byte(jsonDirectManifest), &source)
	for _, c := range source.Contracts {
		if c.ID == source.Root {
			c.Profile.JSONDepth = 2
		}
	}
	data, err := json.Marshal(source)
	if err != nil {
		t.Fatal(err)
	}
	limited, err := LoadSchema(data)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := limited.DecodeJSONWithEncoding("", []byte("{\"Scores\":[[1,2]]}"), UTF8); err == nil {
		t.Fatal("direct dictionary reader accepted a pair beyond the configured depth")
	}
}
