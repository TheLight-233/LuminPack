package luminpack

import (
	"bytes"
	"encoding/json"
	"fmt"
	"reflect"
	"testing"
)

func TestCompiledStructStorage(t *testing.T) {
	native := buildStorageSchema(t, "nativeStorage", false, []int{8}, [][]int{{0, 1, 2, 3, 4}})
	storage := []byte{7, 0, 0, 0, 1, 0x81, 0x92, 0xa3}
	value, err := native.DecodeBinary("Value", storage)
	if err != nil {
		t.Fatal(err)
	}
	carrier := value.(NativeValue)
	storage[5] = 0
	carrier.Value.(Record)["Count"] = int32(8)
	out, err := native.EncodeBinary("Value", carrier)
	if err != nil || !bytes.Equal(out, []byte{8, 0, 0, 0, 1, 0x81, 0x92, 0xa3}) {
		t.Fatalf("Native padding or patched field changed: %x, %v", out, err)
	}

	tagged := buildStorageSchema(t, "storage", true, []int{8, 5}, [][]int{{0, 1, 2, 3, 4}, {1, 2, 3, 4, 0}})
	alternate := []byte{1, 0, 0, 0, 2, 1, 7, 0, 0, 0}
	value, err = tagged.DecodeBinary("Items", alternate)
	if err != nil || !reflect.DeepEqual(value, []any{Record{"Count": int32(7), "Valid": true}}) {
		t.Fatalf("alternate Tagged layout decode: %v, %v", value, err)
	}
	out, err = tagged.EncodeBinary("Items", value)
	if err != nil || !bytes.Equal(out, []byte{1, 0, 0, 0, 1, 7, 0, 0, 0, 1, 0, 0, 0}) {
		t.Fatalf("Tagged canonical write: %x, %v", out, err)
	}

	packed := buildStorageSchema(t, "storage", false, nil, nil)
	packedBytes := []byte{1, 0, 0, 0, 7, 0, 0, 0, 1}
	value, err = packed.DecodeBinary("Items", packedBytes)
	if err != nil || !reflect.DeepEqual(value, []any{Record{"Count": int32(7), "Valid": true}}) {
		t.Fatalf("Packed block decode: %v, %v", value, err)
	}
	if result, err := packed.EncodeBinary("Items", value); err != nil || !bytes.Equal(result, packedBytes) {
		t.Fatalf("Packed block round trip: %x, %v", result, err)
	}
	if _, err := packed.DecodeBinary("Items", packedBytes[:len(packedBytes)-1]); err == nil {
		t.Fatal("Packed block accepted truncated input")
	}
}

func buildStorageSchema(t *testing.T, property string, tagged bool, strides []int, offsets [][]int) *Schema {
	t.Helper()
	layout, err := json.Marshal(valueLayout{WireSize: 5, Tagged: tagged, ModelSizes: strides, ModelOffsets: offsets,
		Fields: []storageField{
			{Name: "Count", Storage: &valueLayout{WireSize: 4}},
			{Name: "Valid", Storage: &valueLayout{WireSize: 1}},
		}})
	if err != nil {
		t.Fatal(err)
	}
	source := fmt.Sprintf("{\"version\":1,\"root\":\"Items\",\"rootWire\":{\"kind\":\"sequence\",\"element\":{\"kind\":\"contract\",\"type\":\"Value\"}},\"contracts\":[{\"id\":\"Value\",\"type\":\"Value\",\"kind\":\"Struct\",\"storageKind\":\"RawUnmanaged\",%q:%s,\"members\":[{\"name\":\"Count\",\"wire\":{\"kind\":\"scalar\",\"type\":\"int\"}},{\"name\":\"Valid\",\"wire\":{\"kind\":\"scalar\",\"type\":\"bool\"}}]}]}", property, layout)
	schema, err := LoadSchema([]byte(source))
	if err != nil {
		t.Fatal(err)
	}
	return schema
}

func TestBooleanBlockRejectsInvalidLanesAndTail(t *testing.T) {
	schema, err := LoadSchema([]byte("{\"version\":1,\"root\":\"Flags\",\"rootWire\":{\"kind\":\"sequence\",\"element\":{\"kind\":\"scalar\",\"type\":\"bool\"}}}"))
	if err != nil {
		t.Fatal(err)
	}
	want := []bool{true, false, true, true, false, true, false, false, true, false, true, true, false, true, false, false, true}
	encoded, err := schema.EncodeBinary("", want)
	if err != nil {
		t.Fatal(err)
	}
	value, err := schema.DecodeBinary("", encoded)
	if err != nil || !reflect.DeepEqual(value, want) {
		t.Fatalf("Boolean block round trip: %v, %v", value, err)
	}
	for i := 4; i < len(encoded); i++ {
		copy := bytes.Clone(encoded)
		copy[i] = 0x81
		if _, err := schema.DecodeBinary("", copy); err == nil {
			t.Fatalf("accepted noncanonical Boolean byte at %d", i)
		}
	}
}
