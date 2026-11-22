package luminpack

import (
	"bytes"
	"encoding/json"
	"testing"
)

func TestCustomJSONAvailability(t *testing.T) {
	s, err := LoadSchema([]byte("{\"version\":1,\"root\":\"Automatic\",\"contracts\":[{\"id\":\"Automatic\",\"kind\":\"Custom\",\"customJson\":true,\"profile\":{\"jsonEncoding\":1}},{\"id\":\"BinaryOnly\",\"kind\":\"Custom\",\"customJson\":false,\"profile\":{\"jsonEncoding\":1}},{\"id\":\"Container\",\"kind\":\"Record\",\"profile\":{\"jsonEncoding\":1},\"members\":[{\"name\":\"Value\",\"wire\":{\"kind\":\"contract\",\"type\":\"Automatic\"}}]}]}"))
	if err != nil {
		t.Fatal(err)
	}
	calls := 0
	codec := customFixtureCodec(&calls)
	s, err = s.WithCustom("Automatic", codec)
	if err != nil {
		t.Fatal(err)
	}
	s, err = s.WithCustom("BinaryOnly", codec)
	if err != nil {
		t.Fatal(err)
	}
	for _, test := range []struct {
		id       string
		encoding JSONEncoding
		text     string
		value    any
	}{
		{"Automatic", UTF8, "17", int32(17)},
		{"BinaryOnly", UTF8, "17", int32(17)},
		{"BinaryOnly", UTF16, "17", int32(17)},
		{"Container", UTF8, "{\"Value\":17}", Record{"Value": int32(17)}},
	} {
		data := []byte(test.text)
		if test.encoding == UTF16 {
			data = appendUTF16Text(nil, test.text)
		}
		if _, err := s.DecodeJSONWithEncoding(test.id, data, test.encoding); err == nil {
			t.Errorf("%s accepted an unavailable Custom JSON read", test.id)
		}
		if _, err := s.EncodeJSONWithEncoding(test.id, test.value, test.encoding); err == nil {
			t.Errorf("%s accepted an unavailable Custom JSON write", test.id)
		}
	}
	if calls != 0 {
		t.Fatalf("unavailable operations invoked %d JSON delegates", calls)
	}
	data, err := s.EncodeJSON("", int32(17))
	if err != nil || !bytes.Equal(data, appendUTF16Text(nil, "17")) {
		t.Fatalf("Custom default UTF-16 write: %x, %v", data, err)
	}
	if result, err := s.DecodeJSON("", data); err != nil || result != int32(17) {
		t.Fatalf("Custom default UTF-16 read: %v, %v", result, err)
	}
	codec.ReadJSON, codec.WriteJSON = nil, nil
	s, err = s.WithCustom("BinaryOnly", codec)
	if err != nil {
		t.Fatal(err)
	}
	result, err := s.DecodeBinary("BinaryOnly", []byte{17})
	if err != nil || result != int32(17) {
		t.Fatalf("Binary-only binding without JSON callbacks: %v, %v", result, err)
	}
}

func TestRegisteredUnionSupportsBothJSONEncodings(t *testing.T) {
	s, err := LoadSchema([]byte("{\"version\":1,\"root\":\"Base\",\"contracts\":[{\"id\":\"Base\",\"kind\":\"Union\",\"unionRegistration\":true}]}"))
	if err != nil {
		t.Fatal(err)
	}
	calls := 0
	s, err = s.WithRegisteredUnion("Base", 100, "External", customFixtureCodec(&calls))
	if err != nil {
		t.Fatal(err)
	}
	want := Union{Type: "External", Value: int32(17)}
	for _, encoding := range []JSONEncoding{UTF8, UTF16} {
		data, err := s.EncodeJSONWithEncoding("", want, encoding)
		if err != nil {
			t.Fatal(err)
		}
		if result, err := s.DecodeJSONWithEncoding("", data, encoding); err != nil || result != want {
			t.Fatalf("registered Union encoding %d: %v, %v", encoding, result, err)
		}
	}
	if calls != 4 {
		t.Fatalf("registered Union called %d JSON delegates", calls)
	}
}

func customFixtureCodec(calls *int) CustomCodec {
	return CustomCodec{
		ReadBinary:  func(input []byte) (any, int, error) { return int32(input[0]), 1, nil },
		WriteBinary: func(value any) ([]byte, error) { return []byte{byte(value.(int32))}, nil },
		ReadJSON: func(data json.RawMessage) (any, error) {
			(*calls)++
			var value int32
			err := json.Unmarshal(data, &value)
			return value, err
		},
		WriteJSON: func(value any) (json.RawMessage, error) {
			(*calls)++
			return json.Marshal(value)
		},
	}
}
