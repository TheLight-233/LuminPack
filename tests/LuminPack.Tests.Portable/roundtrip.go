package main

import (
	"encoding/binary"
	"encoding/json"
	"fmt"
	"math"
	"os"
	"strings"

	luminpack "github.com/TheLight-233/LuminPack/src/LuminPack.Go"
)

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func run(args []string) error {
	if len(args) < 5 || len(args) > 6 {
		return fmt.Errorf("expected schema, input format, output format, input path, output path and optional fixture binding")
	}
	schemaBytes, err := os.ReadFile(args[0])
	if err != nil {
		return err
	}
	schema, err := luminpack.LoadSchema(schemaBytes)
	if err != nil {
		return err
	}
	fixture := ""
	if len(args) == 6 {
		fixture = args[5]
	}
	edit := strings.HasPrefix(fixture, "edit-")
	if fixture != "" && !edit {
		schema, err = bind(schema, fixture)
		if err != nil {
			return err
		}
	}
	input, err := os.ReadFile(args[3])
	if err != nil {
		return err
	}
	var model any
	switch args[1] {
	case "binary":
		model, err = schema.DecodeBinary("", input)
	case "json-utf8":
		model, err = schema.DecodeJSONWithEncoding("", input, luminpack.UTF8)
	case "json-utf16":
		model, err = schema.DecodeJSONWithEncoding("", input, luminpack.UTF16)
	default:
		return fmt.Errorf("invalid input format: %s", args[1])
	}
	if err != nil {
		return err
	}
	if edit {
		editLayout(model.(luminpack.Record), fixture)
	}
	var output []byte
	switch args[2] {
	case "binary":
		output, err = schema.EncodeBinary("", model)
	case "json-utf8":
		output, err = schema.EncodeJSONWithEncoding("", model, luminpack.UTF8)
	case "json-utf16":
		output, err = schema.EncodeJSONWithEncoding("", model, luminpack.UTF16)
	default:
		return fmt.Errorf("invalid output format: %s", args[2])
	}
	if err != nil {
		return err
	}
	return os.WriteFile(args[4], output, 0666)
}

func editLayout(record luminpack.Record, fixture string) {
	switch fixture {
	case "edit-packed":
		nested := record["Nested"].([]any)[0].(luminpack.Record)
		nested["Data"].(luminpack.Record)["End"] = int16(1234)
	case "edit-tagged":
		record["Values"].([]any)[0].(luminpack.Record)["Code"] = int32(0x01234567)
	case "edit-native":
		native := record["Items"].([]any)[0].(luminpack.NativeValue)
		native.Value.(luminpack.Record)["Code"] = int32(0x01234567)
	default:
		panic("unknown layout edit: " + fixture)
	}
}

func bind(schema *luminpack.Schema, fixture string) (*luminpack.Schema, error) {
	switch fixture {
	case "graph-external":
		return schema.WithRegisteredUnion("LuminPackCppReferenceFixtures.IGraphChoice", 600,
			"LuminPackCppReferenceFixtures.GraphExternal", xorFormatter(4, 59, 71))
	case "large-graph-external":
		return schema.WithRegisteredUnion("LuminPackCppVirtualUnionFixtures.LargeGraph", 612,
			"LuminPackCppVirtualUnionFixtures.Registered", xorFormatter(4, 23, 73))
	case "registered-narrow":
		return schema.WithRegisteredUnion("LuminPackCppRegisteredUnionFixtures.INarrow", 100,
			"LuminPackCppRegisteredUnionFixtures.RegisteredCase", xorFormatter(4, 85, 137))
	case "registered-wide":
		return schema.WithRegisteredUnion("LuminPackCppRegisteredUnionFixtures.IWide", 600,
			"LuminPackCppRegisteredUnionFixtures.RegisteredCase", xorFormatter(4, 85, 137))
	case "custom-shifted":
		return schema.WithCustom("LuminPackCppCustomFixtures.Shifted", xorFormatter(8, 85, 137))
	case "custom-binary-only":
		return schema.WithCustom("LuminPackCppCustomFixtures.BinaryOnly", xorFormatter(1, 85, 0))
	default:
		return nil, fmt.Errorf("unknown fixture binding: %s", fixture)
	}
}

// Mirrors the managed fixture's application-owned payload, after any runtime Union framing.
func xorFormatter(width int, binaryMask, jsonMask int64) luminpack.CustomCodec {
	decode := func(payload, mask int64) any {
		if width == 8 && payload == math.MinInt64 {
			return nil
		}
		return payload ^ mask
	}
	encode := func(value any, mask int64) int64 {
		if width == 8 && value == nil {
			return math.MinInt64
		}
		return value.(int64) ^ mask
	}
	return luminpack.CustomCodec{
		ReadBinary: func(data []byte) (any, int, error) {
			if len(data) < width {
				return nil, 0, fmt.Errorf("truncated application payload")
			}
			var payload int64
			switch width {
			case 1:
				payload = int64(data[0])
			case 4:
				payload = int64(int32(binary.LittleEndian.Uint32(data)))
			case 8:
				payload = int64(binary.LittleEndian.Uint64(data))
			}
			return decode(payload, binaryMask), width, nil
		},
		WriteBinary: func(value any) ([]byte, error) {
			result := make([]byte, width)
			payload := encode(value, binaryMask)
			switch width {
			case 1:
				result[0] = byte(payload)
			case 4:
				binary.LittleEndian.PutUint32(result, uint32(payload))
			case 8:
				binary.LittleEndian.PutUint64(result, uint64(payload))
			}
			return result, nil
		},
		ReadJSON: func(data json.RawMessage) (any, error) {
			if width == 1 {
				return nil, fmt.Errorf("binary-only fixture")
			}
			var payload int64
			if err := json.Unmarshal(data, &payload); err != nil {
				return nil, err
			}
			return decode(payload, jsonMask), nil
		},
		WriteJSON: func(value any) (json.RawMessage, error) {
			if width == 1 {
				return nil, fmt.Errorf("binary-only fixture")
			}
			return json.Marshal(encode(value, jsonMask))
		},
	}
}
