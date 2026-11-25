# LuminPack for Java

[简体中文](../../docs/modules/luminpack.java.cn.md)

The Java runtime exchanges LuminPack Binary and UTF-8 or UTF-16LE JSON. A generated Java class holds the root codec and the contract exported from the C# model. [Cross-language contracts](../../docs/portable-runtime.md)

## Supported contracts

The shared collection scope covers arrays, `List<T>` and `Dictionary<TKey,TValue>`. A root or field can be an `int`, a `float`, or a closed collection such as `Dictionary<int,List<float>>`. Include a closed serializer call in the exporting C# project for collections of nullable values so the manifest records their Binary layout.

The portable contract covers positional records, declared Union cases, Version descriptors, tracked record identity and application-supplied Custom codecs. Register callbacks on the receiving runtime for Custom payloads and externally registered Union cases. Automatic Custom JSON uses UTF-16. Ordinary records use value semantics. Enable tracking on the C# contract when identity or cycles matter.

Use the generated root codec with a Map for a record, List for an array or list, and Map for a dictionary. Reuse the codec across documents and reuse the same record Map within a document for repeated tracked references.

## Build and generate

Use JDK 17 or later. From the repository root, generate a binding for the shared guide's `Example.Message` model and compile it with the runtime sources.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language java --package example.generated .tmp/contracts.json Example.Message .tmp/Message.java
javac --release 17 -encoding UTF-8 -d .tmp/java-classes src/LuminPack.Java/src/main/java/luminpack/*.java .tmp/Message.java
```

The output filename determines the Java class name. The default package is `luminpack.generated`. Put the compiled classes on the application's classpath, or include the runtime sources and generated file in the application build.

## Read and write

Use `Message.CODEC` with a model keyed by exported member names. The Binary methods use the projected operation profile. The JSON methods also accept `LuminPack.UTF8` and `LuminPack.UTF16` to choose the document encoding explicitly.

```java
import example.generated.Message;
import java.util.List;
import java.util.Map;
import luminpack.LuminPack;

var model = Map.of("Id", 7, "Text", "ready", "Measurements", List.of(2, 5));
byte[] binary = Message.CODEC.writeBinary(model);
Object copy = Message.CODEC.readBinary(binary);
byte[] json = Message.CODEC.writeJson(copy, LuminPack.UTF8);
Object fromJson = Message.CODEC.readJson(json, LuminPack.UTF8);
```

| Contract value | Java value |
| --- | --- |
| Record or modeled struct | `Map<String, Object>` with exported member names |
| Array, List, pair, tuple | List. Decoded sequences expose a List, including eligible primitive blocks. |
| Dictionary | Map. Use `LinkedHashMap` when matching a producer's entry order matters. |
| Rectangular array | Map with `lengths`, `lowerBounds`, and row-major `values`. |
| Unsigned 64-bit integer | `BigInteger`. Use the exported width for native integers. |
| Declared Union case | `LuminPack.UnionValue` with the unsigned 32-bit case hash held in a `long`. |
| Registered Union case | `LuminPack.RegisteredUnionValue` with the registered tag and payload. |

Use the same Map instance for repeated nodes in a tracked reference graph. The codec holds the contract, while each call owns the reference and Version state for its document.

## Load a contract or attach application codecs

Load the output of `project` through `LuminPack.fromManifest(Files.readString(path))`. The generated binding already holds an initialized codec. `withCustom(type, binding)` and `withUnionCase(unionType, tag, binding)` return new codec instances. Use the full contract identity and implement `LuminPack.CustomCodec` with the matching Binary and JSON payload. Registered Union callbacks receive the payload after the runtime processes the tag.

## Text and primitive fast paths

Decoded Packed, Tagged, and Native lists keep a checked byte block and materialize rows on demand. A compatible write can copy untouched row bytes. Alternate Tagged models are converted to the canonical layout, and Native writes preserve padding. Ordinary UTF-8 JSON uses precompiled property heads and direct scalar operations.

The baseline runtime uses bounded word-at-a-time text operations and contiguous primitive storage. The optional Vector API provider accelerates long ASCII and token scans. Build it using a JDK that includes `jdk.incubator.vector`, then copy the service metadata from `src/vector/resources` onto the same classpath.

```sh
javac --add-modules jdk.incubator.vector -cp .tmp/java-classes -d .tmp/java-classes src/LuminPack.Java/src/vector/java/luminpack/VectorStringKernels.java
cp -R src/LuminPack.Java/src/vector/resources/META-INF .tmp/java-classes/
java --add-modules jdk.incubator.vector -cp .tmp/java-classes example.Main
```

## Verify interoperability

```sh
python tests/LuminPack.Tests.Portable/verify.py --language java
python tests/LuminPack.Tests.Portable/verify.py --language java --suite strings --java-vector
```
