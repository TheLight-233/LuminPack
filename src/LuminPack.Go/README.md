# LuminPack for Go

[简体中文](../../docs/modules/luminpack.go.cn.md)

## Serialize and deserialize

The root name keeps multiple models in one package distinct. Eligible records use the generated typed functions. Other roots use a lazily loaded default schema. For application registrations, configure a schema returned by `LoadMessageLuminPack` and call its methods. Check `err` before using the decoded value.

```go
data, err := generated.SerializeMessage(value)
if err != nil { return err }
copy, err := generated.DeserializeMessage(data)
```

The Go module reads and writes LuminPack Binary and UTF-8 or UTF-16LE JSON. A generated Go file embeds the root schema from the shared contract. [Cross-language contracts](../../docs/portable-runtime.md)

## Supported contracts

The shared collection scope covers arrays, `List<T>` and `Dictionary<TKey,TValue>`. A root or field can be an `int`, a `float`, or a closed collection such as `Dictionary<int,List<float>>`. Include a closed serializer call in the exporting C# project for collections of nullable values so the manifest records their Binary layout.

The portable contract covers positional records, declared Union cases, Version descriptors, tracked record identity and application-supplied Custom codecs. Register callbacks on the receiving runtime for Custom payloads and externally registered Union cases. Automatic Custom JSON uses UTF-16. Ordinary records use value semantics. Enable tracking on the C# contract when identity or cycles matter.

For an eligible closed positional root, use the generated typed functions directly. For the Message example, decode Binary with `message, err := generated.DecodeMessageBinary(data)`. After checking the error, call `generated.EncodeMessageJSON(message)` for UTF-8 JSON. The generated model uses typed slices and ordered dictionary entries. For Version, tracking, registered cases, Custom or UTF-16 JSON, use the schema API shown below.

## Install from source and generate

Use Go 1.23 or later. Add the runtime module requirement and a local replacement to the application's `go.mod`. Set the replacement to the path of your checkout.

```go
require github.com/TheLight-233/LuminPack/src/LuminPack.Go v0.0.0

replace github.com/TheLight-233/LuminPack/src/LuminPack.Go => ../LuminPack/src/LuminPack.Go
```

From the LuminPack repository root, generate the shared guide's `Example.Message` binding. Put the output in the Go package that will own it. The default package name is `generated`.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language go --package generated .tmp/contracts.json Example.Message .tmp/message.go
```

## Transcode to JSON with a schema

Use the empty type identity to select the root. The schema reports invalid wire data or incompatible model values as errors.

```go
import (
    "example.com/myapp/generated"
    luminpack "github.com/TheLight-233/LuminPack/src/LuminPack.Go"
)

func transcode(binary []byte) ([]byte, error) {
    schema, err := generated.LoadMessageLuminPack()
    if err != nil {
        return nil, err
    }
    message, err := schema.DecodeBinary("", binary)
    if err != nil {
        return nil, err
    }
    return schema.EncodeJSONWithEncoding("", message, luminpack.UTF8)
}
```

Reuse the loaded schema across independent operations. To create a model, use a value such as `luminpack.Record{"Id": int32(7), "Text": "ready", "Measurements": []int32{2, 5}}`. Use `EncodeBinary` for a new byte slice or `AppendBinary` to append to an existing destination. Use `EncodeJSONWithEncoding` and `DecodeJSONWithEncoding` with `luminpack.UTF8` or `luminpack.UTF16` when choosing the JSON encoding per call.

| Contract value | Go value |
| --- | --- |
| Record or modeled struct | `luminpack.Record`, keyed by exported member names. |
| Scalar array or List | Typed slices for eligible storage. Other sequences expose element values in wire order. |
| Dictionary | `luminpack.Dictionary`, a slice of key-value entries in wire order. |
| Rectangular array | `luminpack.Rectangular`, with lengths, lower bounds, and row-major values. |
| Declared or registered Union case | `luminpack.Union`, with the concrete type identity and payload. |
| Native padded value | `luminpack.NativeValue`, which carries logical fields and the original Binary storage. |
| Raw UTF-16 units | `luminpack.UTF16String` for values containing unpaired UTF-16 code units. |

Reuse the same `Record` map for repeated nodes in tracked graphs. Native integers use the widths described by the contract.

## Load a schema or attach application codecs

Pass the bytes from `project` to `luminpack.LoadSchema` to use a contract directly. Use `WithCustom(typeID, codec)` for Custom contracts and `WithRegisteredUnion(unionType, tag, caseType, codec)` for registered cases. Both return a new schema. The Binary reader returns the consumed byte count. Supply the JSON functions when the Custom contract exports JSON or when binding a registered Union case. These functions exchange `json.RawMessage` payloads. Automatic Custom JSON uses UTF-16.

## SIMD and scalar builds

Fixed-width typed slices use checked block transfers. For modeled Packed, Tagged, and Native rows, the schema compiles byte offsets into copy ranges. Tagged writes use the canonical layout, and Native rewrites retain saved padding around the current field values. An ordered JSON reader matches cached property heads and uses the general parser for documents outside its fast path.

The runtime uses SSE2 on amd64 and selects AVX2 or AVX-512BW when available for complete blocks. On arm64 it uses NEON. The text kernels cover ASCII scans and supported UTF-16 conversions. The `purego` build tag exercises the portable implementation. Run the following commands from the runtime module.

```sh
go test ./...
go test -tags purego ./...
```

## Verify interoperability

```sh
python tests/LuminPack.Tests.Portable/verify.py --language go
python tests/LuminPack.Tests.Portable/verify.py --language go --suite strings --go-pure
```
