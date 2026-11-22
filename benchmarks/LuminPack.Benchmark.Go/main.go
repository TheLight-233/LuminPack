package main

import (
	"bytes"
	"encoding/csv"
	"encoding/gob"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"reflect"
	"runtime"
	"strconv"
	"time"

	luminpack "github.com/TheLight-233/LuminPack/src/LuminPack.Go"
	"github.com/fxamacker/cbor/v2"
	gojson "github.com/goccy/go-json"
	jsoniter "github.com/json-iterator/go"
	"github.com/vmihailenco/msgpack/v5"
)

type referenceLibrary struct {
	name, format string
	encode       func(any) ([]byte, error)
	decode       func([]byte, any) error
}

var baselineLibraries = []referenceLibrary{
	{"MessagePack (vmihailenco)", "binary", msgpack.Marshal, msgpack.Unmarshal},
	{"CBOR (fxamacker)", "binary", cbor.Marshal, cbor.Unmarshal},
	{"encoding/gob", "binary", encodeGob, decodeGob},
	{"encoding/json", "json", json.Marshal, json.Unmarshal},
	{"go-json (goccy)", "json", gojson.Marshal, gojson.Unmarshal},
	{"jsoniter", "json", jsoniter.ConfigCompatibleWithStandardLibrary.Marshal, jsoniter.ConfigCompatibleWithStandardLibrary.Unmarshal},
}

type benchmark struct {
	model, format, library, operation string
	size                              int
	run                               func() error
}

type result struct {
	ns, allocated float64
}

var outputBytes []byte
var outputModel any

func main() {
	if err := run(os.Args[1:]); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func run(args []string) error {
	flags := flag.NewFlagSet("LuminPack.Benchmark.Go", flag.ContinueOnError)
	samples := flags.Int("samples", 7, "measured samples per codec operation")
	sampleTime := flags.Duration("sample-time", 40*time.Millisecond, "minimum duration per measured sample")
	warmup := flags.Duration("warmup-time", 30*time.Millisecond, "minimum warmup duration per codec operation")
	check := flags.Bool("check", false, "verify all round trips without timing")
	if err := flags.Parse(args); err != nil {
		return err
	}
	if flags.NArg() != 2 || *samples < 1 || *sampleTime <= 0 || *warmup <= 0 {
		return errors.New("usage: go run . [flags] <models-directory> <output.csv>")
	}
	var cases []benchmark
	for _, root := range []string{"simple", "serialize"} {
		modelCases, err := prepare(flags.Arg(0), root)
		if err != nil {
			return fmt.Errorf("%s: %w", root, err)
		}
		cases = append(cases, modelCases...)
	}
	if *check {
		fmt.Fprintf(os.Stderr, "Verified %d codec operations for both managed benchmark models.\n", len(cases))
		return nil
	}
	var data bytes.Buffer
	report := csv.NewWriter(&data)
	report.Write([]string{"language", "model", "format", "library", "operation", "sample", "ns_per_op", "bytes", "alloc_bytes_per_op"})
	for _, entry := range cases {
		if _, _, err := execute(entry.run, *warmup); err != nil {
			return fmt.Errorf("warm up %s: %w", entry.library, err)
		}
	}
	for sample := 0; sample < *samples; sample++ {
		// Rotate the ordering to spread process warmup and GC effects.
		for index := range cases {
			entry := cases[(index+sample)%len(cases)]
			measurement, err := measure(entry, *sampleTime)
			if err != nil {
				return fmt.Errorf("measure %s %s %s: %w", entry.model, entry.library, entry.operation, err)
			}
			report.Write([]string{"go", entry.model, entry.format, entry.library, entry.operation,
				strconv.Itoa(sample + 1), decimal(measurement.ns), strconv.Itoa(entry.size), decimal(measurement.allocated)})
		}
	}
	report.Flush()
	if err := report.Error(); err != nil {
		return err
	}
	return os.WriteFile(flags.Arg(1), data.Bytes(), 0666)
}

func measure(entry benchmark, duration time.Duration) (result, error) {
	var before, after runtime.MemStats
	runtime.ReadMemStats(&before)
	elapsed, iterations, err := execute(entry.run, duration)
	runtime.ReadMemStats(&after)
	if err != nil {
		return result{}, err
	}
	return result{float64(elapsed.Nanoseconds()) / float64(iterations),
		float64(after.TotalAlloc-before.TotalAlloc) / float64(iterations)}, nil
}

func execute(operation func() error, duration time.Duration) (time.Duration, int, error) {
	start := time.Now()
	iterations := 0
	for {
		for range 16 {
			if err := operation(); err != nil {
				return 0, 0, err
			}
		}
		iterations += 16
		if elapsed := time.Since(start); elapsed >= duration {
			return elapsed, iterations, nil
		}
	}
}

func decimal(value float64) string {
	return strconv.FormatFloat(value, 'f', 3, 64)
}

func prepare(directory, model string) ([]benchmark, error) {
	load := func(suffix string) ([]byte, error) { return os.ReadFile(filepath.Join(directory, model+suffix)) }
	manifest, err := load(".manifest.json")
	if err != nil {
		return nil, err
	}
	schema, err := luminpack.LoadSchema(manifest)
	if err != nil {
		return nil, err
	}
	projection, err := referenceProjection(manifest)
	if err != nil {
		return nil, err
	}
	binary, err := load(".bin")
	if err != nil {
		return nil, err
	}
	jsonDocument, err := load(".utf8.json")
	if err != nil {
		return nil, err
	}
	input, err := schema.DecodeBinary("", binary)
	if err != nil {
		return nil, err
	}
	typed := projection.convert(input).Interface()
	binaryEncode := func() ([]byte, error) { return schema.EncodeBinary("", input) }
	binaryDecode := func(data []byte) (any, error) { return schema.DecodeBinary("", data) }
	jsonEncode := func() ([]byte, error) { return schema.EncodeJSONWithEncoding("", input, luminpack.UTF8) }
	jsonDecode := func(data []byte) (any, error) { return schema.DecodeJSONWithEncoding("", data, luminpack.UTF8) }
	luminBinary, err := binaryEncode()
	if err != nil {
		return nil, err
	}
	if !bytes.Equal(luminBinary, binary) {
		return nil, errors.New("LuminPack changed the managed default Binary bytes")
	}
	managedJSON, err := jsonDecode(jsonDocument)
	if err != nil {
		return nil, err
	}
	if !reflect.DeepEqual(typed, projection.convert(managedJSON).Interface()) {
		return nil, errors.New("managed Binary and JSON decoded to different values")
	}
	luminJSON, err := jsonEncode()
	if err != nil {
		return nil, err
	}
	outputJSON, err := jsonDecode(luminJSON)
	if err != nil {
		return nil, err
	}
	if !reflect.DeepEqual(typed, projection.convert(outputJSON).Interface()) {
		return nil, errors.New("LuminPack JSON round trip changed the benchmark model")
	}
	var cases []benchmark
	cases = addCases(cases, model, "binary", "LuminPack", luminBinary, binaryEncode, binaryDecode)
	cases = addCases(cases, model, "json", "LuminPack", luminJSON, jsonEncode, jsonDecode)
	for _, library := range baselineLibraries {
		encode := func() ([]byte, error) { return library.encode(typed) }
		decode := func(data []byte) (any, error) {
			value := reflect.New(projection.typ.Elem())
			if err := library.decode(data, value.Interface()); err != nil {
				return nil, err
			}
			return value.Interface(), nil
		}
		payload, err := encode()
		if err != nil {
			return nil, fmt.Errorf("%s encode: %w", library.name, err)
		}
		roundtrip, err := decode(payload)
		if err != nil {
			return nil, fmt.Errorf("%s decode: %w", library.name, err)
		}
		if !reflect.DeepEqual(typed, roundtrip) {
			return nil, fmt.Errorf("%s round trip changed the benchmark model", library.name)
		}
		cases = addCases(cases, model, library.format, library.name, payload, encode, decode)
	}
	return cases, nil
}

func addCases(cases []benchmark, model, format, library string, encoded []byte,
	encode func() ([]byte, error), decode func([]byte) (any, error)) []benchmark {
	return append(cases,
		benchmark{model, format, library, "encode", len(encoded), func() error {
			var err error
			outputBytes, err = encode()
			return err
		}},
		benchmark{model, format, library, "decode", len(encoded), func() error {
			var err error
			outputModel, err = decode(encoded)
			return err
		}},
	)
}

func encodeGob(value any) ([]byte, error) {
	var buffer bytes.Buffer
	if err := gob.NewEncoder(&buffer).Encode(value); err != nil {
		return nil, err
	}
	return buffer.Bytes(), nil
}

func decodeGob(data []byte, value any) error {
	return gob.NewDecoder(bytes.NewReader(data)).Decode(value)
}
