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

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/contracts.json Example.Message .tmp/message.h
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language java .tmp/contracts.json Example.Message .tmp/Message.java
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language go .tmp/contracts.json Example.Message .tmp/message.go
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

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

The `json-numbers` suite compares exact UTF-8 and UTF-16 JSON bytes and verifies the decoded Binary bits. After changing the checked-in C# XJB tables, run `python tools/xjb-tables/emit_portable.py` to regenerate the portable tables. Then run `node src/LuminPack.TypeScript/scripts/build-json-number.mjs` with a WebAssembly-capable Clang and linker to rebuild the embedded TypeScript kernel.

```sh
python tests/LuminPack.Tests.Portable/verify.py
python tests/LuminPack.Tests.Portable/verify.py --suite strings
python tests/LuminPack.Tests.Portable/verify.py --help
```

## Run and chart language benchmarks

Prepare the same managed benchmark fixtures for each target. Add `--cpp` to the prepare command if you plan to run the C++ benchmark so it also generates the C++ headers.

```sh
python benchmarks/LuminPack.Benchmark.Portable/prepare.py .tmp/portable-benchmark/models
```

Run the command for your language from the repository root. The C++ script uses Bash and CMake, and the TypeScript benchmark uses Node.js 22 or later.

```sh
python benchmarks/LuminPack.Benchmark.Java/run.py .tmp/portable-benchmark/models .tmp/portable-benchmark/java.csv
(cd benchmarks/LuminPack.Benchmark.Go && go run . ../../.tmp/portable-benchmark/models ../../.tmp/portable-benchmark/go.csv)
npm ci --prefix src/LuminPack.TypeScript
npm run build --prefix src/LuminPack.TypeScript
npm ci --prefix benchmarks/LuminPack.Benchmark.TypeScript
node benchmarks/LuminPack.Benchmark.TypeScript/benchmark.mjs .tmp/portable-benchmark/models .tmp/portable-benchmark/typescript.csv
bash benchmarks/LuminPack.Benchmark.Portable/run-cpp.sh .tmp/portable-benchmark/models .tmp/portable-benchmark
```

Render the CSV to light and dark charts and an HTML report. Repeat `--input` to include results for more languages. Published README charts use CI artifacts. [Benchmark methodology and CI](ci.md#language-runtime-benchmarks)

```sh
python benchmarks/LuminPack.Benchmark.Portable/chart.py .tmp/portable-benchmark/charts --input .tmp/portable-benchmark/java.csv
```
