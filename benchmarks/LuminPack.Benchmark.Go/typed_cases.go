//go:build luminpack_generated

package main

import (
	"bytes"
	"fmt"
	"reflect"

	"github.com/TheLight-233/LuminPack/benchmarks/LuminPack.Benchmark.Go/generated"
)

func init() {
	generatedCases = func(model string, binary, json []byte) ([]benchmark, error) {
		switch model {
		case "simple":
			return typedCases(model, binary, json, generated.DecodeSimpleClassBinary, generated.EncodeSimpleClassBinary,
				generated.DecodeSimpleClassJSON, generated.EncodeSimpleClassJSON)
		case "serialize":
			return typedCases(model, binary, json, generated.DecodeCharacterSaveDataBinary, generated.EncodeCharacterSaveDataBinary,
				generated.DecodeCharacterSaveDataJSON, generated.EncodeCharacterSaveDataJSON)
		default:
			return nil, fmt.Errorf("unknown generated model %s", model)
		}
	}
}

func typedCases[T any](model string, binary, json []byte, readBinary func([]byte) (T, error),
	writeBinary func(T) ([]byte, error), readJSON func([]byte) (T, error), writeJSON func(T) ([]byte, error)) ([]benchmark, error) {
	value, err := readBinary(binary)
	if err != nil {
		return nil, err
	}
	other, err := readJSON(json)
	if err != nil {
		return nil, err
	}
	if !reflect.DeepEqual(value, other) {
		return nil, fmt.Errorf("%s: binary and JSON produced different Go models", model)
	}
	cases := []benchmark{}
	for _, item := range []struct {
		format  string
		fixture []byte
		write   func(T) ([]byte, error)
		read    func([]byte) (T, error)
	}{{"binary", binary, writeBinary, readBinary}, {"json", json, writeJSON, readJSON}} {
		encoded, err := item.write(value)
		if err != nil {
			return nil, err
		}
		if !bytes.Equal(encoded, item.fixture) {
			return nil, fmt.Errorf("%s: generated %s changed the managed bytes", model, item.format)
		}
		cases = addCases(cases, model, item.format, "LuminPack", encoded, func() ([]byte, error) { return item.write(value) },
			func(b []byte) (any, error) { return item.read(b) })
	}
	return cases, nil
}
