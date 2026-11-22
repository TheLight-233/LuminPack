# Java, Go, and TypeScript interoperability

[Chinese](portable-runtime.cn.md)

Generate a portable binding from the same .NET contract used by the [C++ runtime](../src/LuminPack.Cpp/README.md). Each binding supplies the root contract, member order, Binary profiles, JSON names, Union cases, and layout information used by the language runtime. Keep the export and generated bindings with the version of the .NET contract they describe.

## Export and generate

Use the Next generator in the .NET project that owns the contracts. Make its export option visible to the compiler.

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportCppContract" />
</ItemGroup>
```

From the repository root, export the built assembly and generate one binding per root and target language.

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportCppContract=true
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- export path/to/Contracts.dll .tmp/contracts.json
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable java .tmp/contracts.json Example.Message .tmp/Message.java
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable go .tmp/contracts.json Example.Message .tmp/message.go
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

Copy a closed generic root's full `id` from the export and quote it on the command line. To exchange a supported collection as the root, use its full type expression, such as `'System.Collections.Generic.List<Example.Message>'`. The `project-portable` command takes the same manifest, root and output arguments and writes a runtime-loadable JSON contract.

Place command options before the manifest path. `--binary-profile` accepts `utf8-length`, `utf8-token`, `utf16-length`, `utf16-token`, `utf8-varint`, or `utf16-varint`. Type and member declarations retain precedence. `--native-integer-bytes 4` selects a 32-bit managed endpoint for `nint` and `nuint`. Native Nullable slots use a separate `--native-scalar-alignment` setting. Set it to `4` if the endpoint uses 4-byte alignment for 64-bit scalars. Both settings default to `8`. For formats shared across architectures, choose the type's [Packed or Tagged option](options.md#struct-layout).

## Java

Compile the generated source together with the [Java runtime sources](../src/LuminPack.Java/src/main/java/luminpack) using JDK 17 or later. The default generated package is `luminpack.generated`. Set `--package` to put the binding in an application package. The output filename supplies the Java class name.

```java
Object message = luminpack.generated.Message.CODEC.readBinary(binary);
byte[] json = luminpack.generated.Message.CODEC.writeJson(message, luminpack.LuminPack.UTF8);
```

Records use maps keyed by member names. Statically declared Union cases use `LuminPack.UnionValue`, with an unsigned 32-bit hash in a `long`. Unsigned 64-bit values use `BigInteger`. To load a projected JSON manifest directly, call `LuminPack.fromManifest`. Attach application formatters with `withCustom` and registered Union cases with `withUnionCase`.

## Go

Use Go 1.23 or later. Reference `github.com/TheLight-233/LuminPack/src/LuminPack.Go` in the application module, with a `replace` directive pointing to the checked-out [Go runtime](../src/LuminPack.Go) when using it from source. The generated package defaults to `generated`. Set `--package` to match the surrounding Go files.

```go
schema, err := generated.LoadMessageLuminPack()
if err != nil {
    return err
}
message, err := schema.DecodeBinary("", binary)
if err != nil {
    return err
}
json, err := schema.EncodeJSONWithEncoding("", message, luminpack.UTF8)
```

An empty type identity selects the exported root. Records use `luminpack.Record`, dictionaries use wire-ordered `luminpack.Dictionary`, and Union cases carry a concrete type identity. Use `luminpack.LoadSchema` to load a projected JSON manifest. Attach application formatters with `WithCustom` and registered cases with `WithRegisteredUnion`.

## TypeScript

Build the [TypeScript runtime](../src/LuminPack.TypeScript) with `npm ci` and `npm run build` in its source directory, then reference it from the application as `@luminpack/typescript`. Use `--runtime-import` to set another ESM module specifier.

```ts
import { codec } from "./message.js";

const message = codec.readBinary(binary);
const json = codec.writeJson(message, "utf8");
```

Generated record types expose the .NET member names. Dictionaries use `Map`, 64-bit integers use `bigint`, and Union cases use `{ type, value }`. Binary and JSON documents use `Uint8Array`. To load a projected manifest directly, call `createCodec`. To supply application formatters or registered cases on a generated binding, call `bindCodec` with the corresponding `custom` or `registered` options.

## Runtime fast paths

- **Java** uses word-at-a-time text operations and contiguous primitive storage. For SIMD scans, build `src/LuminPack.Java/src/vector/java/luminpack/VectorStringKernels.java` with `javac --add-modules jdk.incubator.vector` and the baseline classes on the classpath. Include `src/LuminPack.Java/src/vector/resources` and the vector classes on the application classpath. Launch with `java --add-modules jdk.incubator.vector`. The JVM chooses a supported vector width.
- **Go** uses SSE2 on amd64 and selects AVX2 or AVX-512BW for complete blocks when the CPU and operating system support them. It uses NEON on arm64. These kernels serve ASCII scans and supported UTF-16 conversions. Other architectures and builds with `-tags purego` use the Go implementation.
- **TypeScript** uses checked typed-array paths for contiguous primitives. Long text scans use WebAssembly SIMD where available, with a JavaScript fallback. The SIMD module is embedded in the runtime. To rebuild it after editing `wasm/text.c`, run `node scripts/build-text-simd.mjs` from the TypeScript runtime with a Clang and `wasm-ld` toolchain on `PATH`.

## Verify an exchange

Keep the same root, Binary profile and JSON encoding at both endpoints. For a tracked graph, preserve identity by reusing the same record instance when constructing the graph. Custom formatters and registered Union cases require matching application bindings on each endpoint.

Run the managed interoperability harness from the repository root. The default suite uses the `SimpleClass` and `CharacterSaveData` benchmark models, passing documents through managed Binary, UTF-8 JSON, and UTF-16 JSON. Use `--help` for more suites and executable overrides.

```sh
python tests/LuminPack.Tests.Portable/verify.py
```
