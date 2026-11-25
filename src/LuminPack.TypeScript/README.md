# LuminPack for TypeScript

[简体中文](../../docs/modules/luminpack.typescript.cn.md)

## Serialize and deserialize

The generated module selects the root and returns a `Uint8Array`. For JSON or per-application bindings, use `codec` or `bindCodec(options)`.

```ts
import { serialize, deserialize } from "./message.js";

const bytes = serialize({ Id: 7, Text: "ready", Measurements: [2, 5] });
const copy = deserialize(bytes);
```

The TypeScript ESM runtime reads and writes LuminPack Binary and UTF-8 or UTF-16LE JSON. The generator emits model types, a root codec, and a function for applying application bindings. [Cross-language contracts](../../docs/portable-runtime.md)

## Supported contracts

The shared collection scope covers arrays, `List<T>` and `Dictionary<TKey,TValue>`. A root or field can be an `int`, a `float`, or a closed collection such as `Dictionary<int,List<float>>`. Include a closed serializer call in the exporting C# project for collections of nullable values so the manifest records their Binary layout.

The portable contract covers positional records, declared Union cases, Version descriptors, tracked record identity and application-supplied Custom codecs. Register callbacks on the receiving runtime for Custom payloads and externally registered Union cases. Automatic Custom JSON uses UTF-16. Ordinary records use value semantics. Enable tracking on the C# contract when identity or cycles matter.

Import the generated `codec` for the root, or call `bindCodec(options)` to supply registrations. Use arrays for lists and a Map for each dictionary. Generated Binary record bodies and eligible UTF-8 JSON writers run behind the same API. The returned model retains bigint fields and typed Union payloads.

## Build and generate

Build `@luminpack/typescript` from source with Node.js and npm.

```sh
npm ci --prefix src/LuminPack.TypeScript
npm run build --prefix src/LuminPack.TypeScript
```

Install the built module in your application through a local path dependency or a workspace. From the repository root, generate a binding for the shared guide's `Example.Message`.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

The generated source imports `@luminpack/typescript`. Use `--runtime-import` with the module specifier used by a different bundling setup.

## Read and write

Import `codec` and `Root` from the generated file. Binary and JSON documents both use `Uint8Array`. The JSON encoding argument is `"utf8"` or `"utf16"`, and omitting it uses the contract's JSON encoding.

```ts
import { codec, type Root } from "./message.js";

const model: Root = { Id: 7, Text: "ready", Measurements: [2, 5] };
const binary = codec.writeBinary(model);
const copy = codec.readBinary(binary);
const json = codec.writeJson(copy, "utf8");
const fromJson = codec.readJson(json, "utf8");
```

| Contract value | TypeScript value |
| --- | --- |
| Record or modeled struct | Object with the exported member names. |
| Array or List | Array with the generated element type. Writes also accept a matching TypedArray for fixed-width numeric elements. |
| Dictionary | `Map<K, V>`. |
| Signed or unsigned 64-bit integer | `bigint`. 32-bit native integers use `number`. |
| Pair or tuple | Tuple with the corresponding element types. |
| Rectangular array | `{ lengths, lowerBounds, values }` with row-major values. |
| Union case | `{ type, value }`, where `type` is the concrete type identity. |

Reuse the same JavaScript object for repeated nodes in tracked graphs. The runtime keeps integer precision while reading and writing LuminPack JSON.

Generated bindings expose `Root` for decoded values and `RootInput` for writes. `RootInput` also permits the matching numeric TypedArray. For a projected manifest, `createCodec<Root, RootInput>(manifest)` accepts the same pair of types.

## Load a contract or attach application codecs

Use `createCodec<MyRoot>(manifest)` with the parsed output from `project`. Pass `{ custom, registered }` as options to bind application payloads. The generated file also exposes `bindCodec(options)`. Custom bindings are keyed by full type identity. For registered Union cases, key the list by the Union identity and give each entry its concrete `type`, numeric `tag`, and payload `codec`.

## WebAssembly SIMD and JavaScript fallback

The runtime uses checked typed-array paths for contiguous scalars. Little-endian Binary writers copy matching numeric TypedArray inputs directly, including compressed field payloads. Boolean and character collections use their usual array values. Binary struct blocks validate the full window, then access the exported Packed, Tagged, or Native offsets. Rewriting a decoded Native value copies its saved bytes and stores the current fields, preserving padding. Long text scans use the embedded WebAssembly SIMD module when supported, with a tested JavaScript fallback.

```sh
npm test --prefix src/LuminPack.TypeScript
python tests/LuminPack.Tests.Portable/verify.py --language typescript
python tests/LuminPack.Tests.Portable/verify.py --language typescript --suite strings --typescript-no-wasm
```

After changing `wasm/text.c`, regenerate the embedded module from the runtime directory with `node scripts/build-text-simd.mjs`. Put Clang and `wasm-ld` on `PATH` for this rebuild.
