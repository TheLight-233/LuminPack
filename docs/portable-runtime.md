# Cross-language contracts

[Chinese](portable-runtime.cn.md)

LuminPack uses one contract for Binary and JSON across its runtimes. Define the model in C#, export its compiler-generated contract, and generate the bindings needed by each application. The export records member order, profiles, JSON names, Union cases, Version descriptors, input limits, and physical layouts. Regenerate the bindings when the model changes.

| Runtime | Guide |
| --- | --- |
| .NET | [Getting started](getting-started.md) |
| C++ | [Build the C++20 library and generate codecs](../src/LuminPack.Cpp/README.md) |
| Java | [Compile the runtime and bind a contract](../src/LuminPack.Java/README.md) |
| Go | [Use the source module and generated schema](../src/LuminPack.Go/README.md) |
| TypeScript | [Use the ESM package and generated types](../src/LuminPack.TypeScript/README.md) |
| Rust | [Generate a typed binary codec or load a native model library](../src/LuminPack.Rust/README.md) |
| Python | [Compile a binary codec for dicts and lists or use native documents](../src/LuminPack.Python/README.md) |

## Use the generated binary API

Generate the binding once for the root model. Each call below reads or writes the positional record Example.Message as a complete binary document. Both Go calls return a result and an error. The TypeScript example uses `import * as message from "./message.js"`.

| Language | Serialize | Deserialize |
| --- | --- | --- |
| C++ | `luminpack::serialize(value)` | `luminpack::deserialize<message::root_value>(bytes)` |
| Java | `Message.serialize(value)` | `Message.deserialize(bytes)` |
| Go | `generated.SerializeMessage(value)` | `generated.DeserializeMessage(data)` |
| TypeScript | `message.serialize(value)` | `message.deserialize(bytes)` |
| Rust | `message::serialize(&value)?` | `message::deserialize(&bytes)?` |
| Python | `message.serialize(value)` | `message.deserialize(data)` |

In C++, message aliases the generated root namespace. Use `message::serialize(value)` for standard containers or scalars and `message::serialize(value, output)` for single-pass Custom or RTTI Union writes. The Rust and Python short forms use their direct model generators. Their Document APIs handle versioned, tracked and Custom contracts. Each runtime guide covers its JSON operations and encoding options.

## Define and export a model

Use the Next source generator in the .NET project that owns your contracts. This example defines a Default positional record. Each generated binding follows the exported member names and Binary order.

```csharp
using System.Collections.Generic;
using LuminPack.Attribute;

namespace Example;

[LuminPackable]
public partial class Message
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public List<int> Measurements { get; set; } = new();
}
```

Expose the export switch to the compiler in the .NET project file.

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportContract" />
</ItemGroup>
```

Build the assembly, then export its compiler metadata. Substitute the built assembly's path in the second command.

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportContract=true
dotnet run --project tools/LuminPack.Codegen -c Release -- export path/to/Contracts.dll .tmp/contracts.json
```

## Generate bindings

From the repository root, choose one target language and one root per command. Use the full model name or the exact closed generic `id` from the manifest. Quote names containing shell metacharacters, such as `'Example.Box<string>'`.

A C# node derived from `List<Node>` is a nested list in Java, Go, and TypeScript. When tracking is enabled, use a record with a `$value` list so repeated elements can point to the same node.

A root can also be a shared primitive or a closed collection, for example `int`, `float[]`, `System.Collections.Generic.List<int?>`, or `System.Collections.Generic.Dictionary<int,float>`. For nullable collection elements or values, include a closed serializer call for that exact collection in the exporting C# project and regenerate the manifest. This exports the CLR slot layout used by Binary. A derived List with `IncludeCollectionBase = true` exposes its inherited elements as `$items` in JSON and portable records (`collection_items` in C++). The Binary header counts only declared fields.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/contracts.json Example.Message .tmp/message.h
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language java .tmp/contracts.json Example.Message .tmp/Message.java
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language go .tmp/contracts.json Example.Message .tmp/message.go
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

For an eligible closed Go record such as `Message`, Codegen emits `MessageModel`, `DecodeMessageBinary`, `EncodeMessageBinary`, `DecodeMessageJSON` and `EncodeMessageJSON`. The JSON functions use UTF-8. The model uses typed slices, ordered `[]luminpack.TypedEntry[K,V]` dictionaries and Go interfaces for declared Union cases. Fields use the prefix `F` followed by the encoded member name. Use `luminpack.Text{Present: true, Value: "hello"}` for text and the zero value for null. The optional `Units` pointer retains exact UTF-16 units on Binary paths. The direct model supports ordinary positional records, scalar-only raw structs and their supported collections. Use the generated `LoadMessageLuminPack` schema for versioned or tracked graphs, Custom formatters, registered Union cases, UTF-16 JSON or other projections.

The generated TypeScript module exposes the same `codec` and `bindCodec(options)` entry points. Eligible records include specialized Binary bodies. Eligible closed graphs also include a UTF-8 writer and an ordered JSON reader. Former names, missing properties and other noncanonical inputs retain the manifest reader's behavior.

Java, Go, and TypeScript can also load a projected JSON contract. The `project` command accepts supported standalone type expressions, such as `'System.Collections.Generic.List<Example.Message>'`.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project .tmp/contracts.json Example.Message .tmp/message.contract.json
```

Place options before the manifest path.

| Option | Usage |
| --- | --- |
| `--binary-profile <profile>` | Select `utf8-length`, `utf8-token`, `utf16-length`, `utf16-token`, `utf8-varint`, or `utf16-varint`. Declared type and member profiles retain precedence. |
| `--rtti` | Generate C++ RTTI Union codecs, including supported virtual dispatch and runtime registration. |
| `--package <name>` | Set the generated Java or Go package. |
| `--runtime-import <specifier>` | Set the TypeScript runtime's ESM module specifier. |
| `--native-integer-bytes <4 or 8>` | Set the managed `nint` and `nuint` width for Java, Go, and TypeScript. The default is 8. C++ uses the target ABI. |
| `--native-scalar-alignment <4 or 8>` | Set the managed storage alignment of 64-bit scalars for Java, Go, and TypeScript. The default is 8. A managed endpoint can use 4-byte native integers and 8-byte scalar alignment. |

Generated Go loaders and TypeScript codecs automatically bind fixed-offset binary readers and writers for raw structs made entirely of `int` and `float` fields. The binding checks the exported field names, types, order and layout. Regenerate with the runtime version used by the application. Other contracts continue through the schema codec, including versioned objects, tracked references and custom handlers.

## Match the wire options

- Use the same root and Binary profile at both endpoints. The default is UTF-16 Length. Length and VarInt preserve null and empty strings separately. Token uses a zero terminator and represents null as an empty string. Use Length or VarInt for embedded zero units.
- Use the same JSON encoding at both endpoints. The JSON wire contract also defines names, enum values, collection shapes, and Union discriminators. [Wire formats](formats.md)
- The portable runtimes use the C# JSON number policy for supported integers, `float`, and `double`. Floating-point output keeps signed zero and the C# fixed and scientific notation rules. The XJB tables for shortest decimal conversion come from the same checked-in C# data. [XJB notice](xjb-NOTICE.md)
- Use `GeneratorType.Version` for models that must evolve and read old data. Match the Union cases and registered tags across endpoints. Assign application cases tags within the exported width and outside the generated cases' reserved values. [Version tolerance](versioning.md), [Unions](polymorphism.md)
- Custom formatters define their own payload and require matching application bindings. For an exported automatic Custom registration, JSON uses UTF-16 when the contract includes JSON operations. Binary-only registrations require only the Binary callbacks. A registered Union case supplies its own JSON callbacks for the selected encoding. [Custom formatters](custom-formatters.md)
- Preserve tracked object identity by sharing the same object or record instance when constructing the graph. Keep a C++ reference document alive while using its borrowed pointers. [Reference tracking](versioning.md#reference-tracking)
- For cross-architecture exchange, choose a supported Packed or Tagged layout. Native Binary requires matching CLR layout, including padding and alignment. Match native integer width separately. Binary rectangular arrays use zero lower bounds. JSON supports nonzero lower bounds. [Struct layout options](options.md#3264-bit-struct-layouts)

## Verify an exchange

The interoperability harness writes `SimpleClass` and `CharacterSaveData` in .NET, passes Binary, UTF-8 JSON, and UTF-16 JSON through Java, Go, and TypeScript, then has .NET verify the results. The `strings` suite checks the six Binary profiles against managed bytes. The C++ guide has its CMake interoperability commands. [C++ guide](../src/LuminPack.Cpp/README.md#verify-the-benchmark-models)

The `primitive-roots` suite exchanges Binary and both JSON encodings for standalone roots, nullable collections and a List subclass. It writes C++ headers alongside the fixtures. Set the CMake option `LUMINPACK_CPP_PRIMITIVE_ROOT_FIXTURES` to that `managed-models` directory and run the `luminpack_cpp_primitive_roots` test.

The `json-numbers` suite compares exact UTF-8 and UTF-16 JSON bytes and verifies the decoded Binary bits. After changing the checked-in C# XJB tables, run `python tools/xjb-tables/emit_portable.py` to regenerate the portable tables. Then run `node src/LuminPack.TypeScript/scripts/build-json-number.mjs` with a WebAssembly-capable Clang and linker to rebuild the embedded TypeScript kernel.

```sh
python tests/LuminPack.Tests.Portable/verify.py
python tests/LuminPack.Tests.Portable/verify.py --suite strings
python tests/LuminPack.Tests.Portable/verify.py --help
```

## Run and chart language benchmarks

Prepare the same managed benchmark fixtures for each target. Pass `--generated` to emit the Go and TypeScript benchmark modules. The portable benchmark workflow compiles and times those entry points. Add `--cpp` to the prepare command if you plan to run the C++ benchmark so it also generates the C++ headers.

```sh
python benchmarks/LuminPack.Benchmark.Portable/prepare.py .tmp/portable-benchmark/models --generated --cpp
```

Run the command for your language from the repository root. The C++ script uses Bash and CMake, and the TypeScript benchmark uses Node.js 22 or later.

```sh
python benchmarks/LuminPack.Benchmark.Java/run.py .tmp/portable-benchmark/models .tmp/portable-benchmark/java.csv
mkdir -p benchmarks/LuminPack.Benchmark.Go/generated
cp .tmp/portable-benchmark/models/go/*.go benchmarks/LuminPack.Benchmark.Go/generated/
(cd benchmarks/LuminPack.Benchmark.Go && go run -tags luminpack_generated . ../../.tmp/portable-benchmark/models ../../.tmp/portable-benchmark/go.csv)
npm ci --prefix src/LuminPack.TypeScript
npm run build --prefix src/LuminPack.TypeScript
npm ci --prefix benchmarks/LuminPack.Benchmark.TypeScript
mkdir -p benchmarks/LuminPack.Benchmark.TypeScript/generated
cp .tmp/portable-benchmark/models/typescript/*.ts benchmarks/LuminPack.Benchmark.TypeScript/generated/
src/LuminPack.TypeScript/node_modules/.bin/tsc --strict --target ES2022 --module NodeNext --moduleResolution NodeNext benchmarks/LuminPack.Benchmark.TypeScript/generated/*.ts
node benchmarks/LuminPack.Benchmark.TypeScript/benchmark.mjs --generated .tmp/portable-benchmark/models .tmp/portable-benchmark/typescript.csv
bash benchmarks/LuminPack.Benchmark.Portable/run-cpp.sh .tmp/portable-benchmark/models .tmp/portable-benchmark
```

Render the CSV to light and dark charts and an HTML report. Repeat `--input` to include results for more languages. Published README charts use CI artifacts. [Benchmark methodology and CI](ci.md#language-runtime-benchmarks)

```sh
python benchmarks/LuminPack.Benchmark.Portable/chart.py .tmp/portable-benchmark/charts --input .tmp/portable-benchmark/java.csv
```
