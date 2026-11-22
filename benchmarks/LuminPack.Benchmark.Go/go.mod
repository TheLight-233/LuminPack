module github.com/TheLight-233/LuminPack/benchmarks/LuminPack.Benchmark.Go

go 1.23

require (
	github.com/TheLight-233/LuminPack/src/LuminPack.Go v0.0.0
	github.com/fxamacker/cbor/v2 v2.9.2
	github.com/goccy/go-json v0.11.2
	github.com/json-iterator/go v1.1.12
	github.com/vmihailenco/msgpack/v5 v5.4.1
)

require (
	github.com/modern-go/concurrent v0.0.0-20180228061459-e0a39a4cb421 // indirect
	github.com/modern-go/reflect2 v1.0.2 // indirect
	github.com/vmihailenco/tagparser/v2 v2.0.0 // indirect
	github.com/x448/float16 v0.8.4 // indirect
)

replace github.com/TheLight-233/LuminPack/src/LuminPack.Go => ../../src/LuminPack.Go
