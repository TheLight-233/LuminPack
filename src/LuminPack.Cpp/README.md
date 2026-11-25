# LuminPack for C++

[中文](../../docs/modules/luminpack.cpp.cn.md)

## Serialize and deserialize

For a standalone scalar or standard container, select the generated root explicitly with `message::serialize(value)` and `message::deserialize(bytes)`. `message::serialize(value, output)` writes once into a supplied span. Use this overload for Custom or RTTI Union callbacks. Tracked reads return the document that owns the graph.

```cpp
#include "message.h"
namespace message = luminpack_generated::root_Example_x002eMessage;

// value has type message::root_value
const auto bytes = luminpack::serialize(value);
auto copy = luminpack::deserialize<message::root_value>(bytes);
```

The C++20 runtime reads and writes LuminPack Binary and UTF-8 or UTF-16LE JSON over caller-owned contiguous buffers. Generated codecs include the public header `<luminpack/luminpack.h>` and link the `LuminPack::Cpp` static library. The runtime selects supported SIMD kernels at run time. [Cross-language contracts](../../docs/portable-runtime.md)

## Supported contracts

The shared collection scope covers arrays, `List<T>` and `Dictionary<TKey,TValue>`. A root or field can be an `int`, a `float`, or a closed collection such as `Dictionary<int,List<float>>`. Include a closed serializer call in the exporting C# project for collections of nullable values so the manifest records their Binary layout.

The portable contract covers positional records, declared Union cases, Version descriptors, tracked record identity and application-supplied Custom codecs. Register callbacks on the receiving runtime for Custom payloads and externally registered Union cases. Automatic Custom JSON uses UTF-16. Ordinary records use value semantics. Enable tracking on the C# contract when identity or cycles matter.

Ordinary recursive nodes use owning pointers. A Union case can own a recursive subtree. The generator rejects a path from an owning recursive node through a Union back to owning recursion, and a tracked descendant that closes such a cycle. See the recursion rules below before choosing the DTO ownership model.

## Build

Run from the repository root. For CMake versions before 3.30, use a build path without a `#` character.

```sh
cmake -S src/LuminPack.Cpp -B .tmp/cpp -DCMAKE_BUILD_TYPE=Release
cmake --build .tmp/cpp --config Release
ctest --test-dir .tmp/cpp -C Release --output-on-failure
```

To use the runtime from another CMake project, add the repository's C++ module as a subdirectory and link it to your target.

```cmake
add_subdirectory(path/to/LuminPack/src/LuminPack.Cpp luminpack-cpp)
target_link_libraries(my_app PRIVATE LuminPack::Cpp)
```

## Low-level cursor example

A codec encodes members in the order declared by the matching .NET contract. This example writes one `Int32` member followed by one string using the default UTF-16 Length profile.

```cpp
#include <luminpack/luminpack.h>

#include <array>
#include <cstdint>

int main()
{
    std::array<std::byte, 64> bytes{};
    luminpack::writer output(bytes);
    luminpack::write_object_header(output, 2);
    output.write<std::int32_t>(42);
    luminpack::write_utf16(output, u"wire");

    luminpack::reader input(output.written());
    if (luminpack::read_object_header(input) != 2)
        return 1;
    const auto id = input.read<std::int32_t>();
    std::array<char16_t, 16> storage{};
    const auto name = luminpack::read_utf16_into(input, storage);
    input.require_end();
    return id == 42 && name == u"wire" ? 0 : 1;
}
```

## Compose codecs

Use `scalar_codec`, `boolean_codec`, and `enum_codec` for fixed-width values. `list_codec` is used for a one-dimensional array or List with the same element representation. Its numeric and enum paths copy one validated contiguous block. `dictionary_codec` uses a count and key/value pairs. Its unordered map preserves key/value equivalence. Pass an ordered sequence of unique pairs to `write_entries` when the output must reproduce another writer's enumeration order.

Use `nullable_codec` for a marker-prefixed `Nullable<T>` represented by `std::optional<T>`. For a fixed-width Version member, pass its full descriptor width as the second argument. `nullable_storage_codec` and `nullable_list_codec` bind the CLR stride and offsets for native dictionary values and contiguous nullable collections. Reads check both the storage range and Boolean flag bytes before converting the block to C++ values. Multi-model Tagged blocks accept every exported tag and write the canonical model. For Native storage, generated code checks the target ABI's presence flag, payload offsets and stride against the exported CLR models at compile time. Both endpoints require the same layout.

`pair_codec` maps a .NET `KeyValuePair<TKey, TValue>` field or dictionary value to `std::pair`. Binary writes the key followed by the value. JSON uses `[key,value]`. Generated arrays and lists support element-wise pairs, unpadded fixed-width pairs, and Packed pairs with a matching storage plan. For multi-model Tagged pairs of fixed-width numeric, character, or enum fields, `tagged_pair_list_codec` accepts every exported layout tag and converts the fields to the canonical CLR model on write.

`tuple_codec` maps a supported .NET `ValueTuple` to `std::tuple`. Binary writes the CLR component stream and JSON uses an array, with an eighth `Rest` component represented as a nested tuple. Generated collections use the exported Packed or Tagged layout, or a proven dense declaration-order CLR representation. Tagged reads validate direct and nested Boolean bytes before publishing the result. The codec accesses tuple components through `std::get`.

For a proven headerless CLR pair or tuple layout with padding, codegen emits an aggregate with explicit padding fields and checks its C++ offsets and stride at compile time. Binary copies preserve the complete representation, including the padding. JSON uses the original pair or tuple components and initializes the extra padding to zero.

Generated `Memory<T>`, `ReadOnlyMemory<T>`, and supported `ReadOnlySequence<T>` fields own their decoded elements in `std::vector`. A null marker decodes to an empty vector. `ArraySegment<T>` and `ImmutableArray<T>` use `std::optional<std::vector<T>>` so a default value retains the CLR null marker. `ReadOnlySequence<T>` flattens the segments under one length and uses the CLR's standalone representation for modeled structs, pairs, tuples, and nullable values. Its raw element slots require a proven untagged layout. Contiguous memory and array views use the CLR block codecs where the exported layout requires them.

Bind a string codec to one of the six `string_profile` values. Length and VarInt preserve null separately from empty. Token terminates with a zero code unit and encodes null as empty. Use a length profile for text containing embedded zero units.

Bind a DTO's fields in wire order with `record_member` and `record_codec`. A Next-generated binary Union uses `union_codec` with the emitted case hash and the corresponding payload codec. Share a custom wire representation by binding application-provided read and write functions through `custom_codec`. Use the source wire contract for member order, scalar types, string profile and Tagged model order.

For Version revision 4 and 5, bind the emitted field descriptors and former name hashes through `version_member` and `version_codec`. Use `read_binary_document` and `write_binary_document` for a fresh schema registry per document. For successive records in the same operation, pass one `version_state` to the codecs. Reset it before an independent record. The writer scans the first eight registrations and creates an open-addressed index when it registers the ninth schema.

To read archived revision-1 Version records, export the .NET contract again and regenerate the C++ header. The generated reader checks complete legacy field names, aliases, and type contracts before decoding their length-bounded payloads. Use the same `read_binary_document` or `read_binary_reference_document` entry as for that model's revision-4 and revision-5 data. Writing the decoded value uses the current Version format.

Multi-model Tagged arrays support raw fields. Describe local Tagged strides and offsets with `sizeof` and `offsetof`. Each copied field's object representation must admit every bit pattern, as fixed-width integer fields do. Initialize any padding that a native write would expose.

CLR rectangular arrays map to `std::optional<rectangular_array<T, Rank>>` with fixed-size `lengths` and `lower_bounds` and a row-major `values` vector. Binary uses zero lower bounds and the projected collection block. JSON preserves nonzero bounds through the C# `Lengths`, `LowerBounds`, and `Values` object. Both readers check the shape against the element count.

The generator uses Next's storage plan to bind recursively Packed fields and multi-model Tagged arrays and lists. A multi-model Tagged value uses the packed field order when it stands alone or appears as a dictionary value. Nonempty multi-model Tagged blocks carry the exported one-based tag. The writer copies matching C++ layouts directly and converts unmatched layouts to the first exported model. A tag-free raw aggregate can use the Native path when the exported CLR offsets and stride match the target's C++ layout. Generated padding fields preserve those raw bytes across DTO copies.

For a cross-model Tagged raw block, the C++ runtime builds the C# shuffle and grouped conversion plans from the exported offsets. It uses complete SIMD windows with a scalar tail. Cross-model reads preserve the caller's padding, matching-layout copies keep the raw bytes, and canonical writes clear the padding.

The reader checks input ranges in Release builds. Use `reader_limits` to bound collection counts and decoded UTF-16 lengths. Buffer-oriented read-into calls borrow the supplied storage. Keep that storage alive while using a returned view. Overlapping layout conversions or string transforms stage their input on the heap.

With SIMD enabled on x64, UTF-16 Length writes of 1–16 code units use the same bounded short Latin-1 path as C#. For 17–64 code units, the writer uses the C# head and tail paths when the complete raw payload fits in disjoint storage. UTF-8 Length writes use the C# short ASCII path for 4–16 code units with SSE2 or NEON and 17–32 code units with AVX2 when the ASCII payload fits in disjoint storage. The general x64 Latin-1 path selects AVX-512BW, AVX2, or SSE2 based on support and complete block size. ARM64 can widen Latin-1 with NEON. Set `LUMINPACK_CPP_MAX_SIMD_BITS` to `128`, `256`, or `512` to cap the compiled width. The CMake option `LUMINPACK_CPP_SIMD=OFF` selects the portable implementation.

UTF-16 Length reads use the C# 4–16 code-unit Latin-1 widening path for fresh strings and eligible list prefixes. Each record's complete window and string quota are checked before decoding.

The UTF-8 byte-count pass finds the end of the leading ASCII run with AVX-512BW, AVX2, SSE2, NEON, or packed words. For longer Unicode input, SSE2 and NEON count extra UTF-8 bytes and validate surrogate pairs in complete vectors before the scalar decoder handles the remainder. Fixed UTF-8 Length writes use the C# long ASCII groups with AVX-512BW or AVX2 and the consecutive two-byte or three-byte UTF-8 SIMD paths. The writer reserves the complete record before storing any payload. ARM64 uses NEON to narrow complete Latin-1 blocks. Three-byte BMP runs use constant shuffle tables and can resume after one ASCII separator.

When the homogeneous probe declines a Latin-1-leading string, the writer can use the C# mixed ASCII and Latin-1 path. All 256 lane masks and exact write widths are compile-time constants. The general UTF-8 encoder resumes at the first code unit outside Latin-1.

After a homogeneous UTF-8 Length prefix, a remaining span of at least 64 code units uses the C# platform transcoder's paired conversion. ARM64 uses the same conversion for mixed input above 32 code units. Runs of ASCII use bounded SSE4.1 or NEON narrowing where available, with a packed four-code-unit fallback. The caller has already validated the complete string and reserved the output record.

Fixed UTF-8 Length reads use bounded ASCII widening when both length headers agree. The short path checks every byte before storing. The long AVX2 path validates while widening into an unpublished string. The general UTF-8 reader classifies the complete payload as ASCII with bounded AVX2, SSE2, NEON, or packed-word checks before widening ASCII or entering the Unicode decoder.

### Tracked references

When the generated header sets `root_requires_reference_graph`, read Binary with `read_binary_reference_document<root_binary>(bytes, root_binary_limits, root_binary_reference_depth)` and JSON with `read_reference_json_document<root_json, root_json_encoding>(bytes, root_json_options)`. The returned document owns the tracked objects in its `graph` and exposes the decoded root in `value`. A tracked class field is a nullable pointer into that graph. Retain the document while using those pointers. To construct a graph, allocate the tracked DTOs through `reference_graph::make<T>()` and link the returned pointers. Write with `write_binary_reference_document` or `write_reference_json_document` to start a fresh identity table.

Default and Version tracked records preserve shared objects and cycles through direct members, supported collections, and variant or RTTI Unions. Binary tracks their explicit IDs together with the current Version scope. JSON uses the existing `$id` and `$ref` properties. The portable Binary graph limit defaults to 512 when the .NET root declares no finite limit. The read entry accepts a caller-supplied depth limit.

## JSON

Use `read_json_document` and `write_json_document` with a concrete JSON codec. The document defaults to little-endian UTF-16. Pass `json_encoding::utf8` as the second template argument to match a UTF-8 .NET root, regardless of the string profile on its individual binary members.

```cpp
using Number = luminpack::scalar_codec<std::int32_t>;
using Numbers = luminpack::json_codec<luminpack::list_codec<Number>>;
auto numbers = luminpack::read_json_document<Numbers, luminpack::json_encoding::utf8>(input_bytes);
auto written = luminpack::write_json_document<Numbers, luminpack::json_encoding::utf8>(output_bytes, numbers);
```

Use `json_record_member` to bind a DTO field to its C# property name and any former names, and `json_record_codec` to compose the object. Use `json_record_body` for a non-null value or Union case payload. `json_union_codec` uses the emitted `$type` hash and `$value` envelope. `json_custom_codec` binds application-owned JSON functions.

The reader validates JSON grammar and the document boundary. Set `json_read_options::max_depth` to limit nested objects and arrays. The default is 1024. String-key dictionaries write object properties and accept either properties or pairs on read. Other dictionaries use arrays of key/value pairs.

JSON readers scan complete literal runs before decoding escapes or non-ASCII UTF-8. The UTF-16 writer uses the same bounded scan for long literal runs. On x64 the scan selects AVX-512BW, AVX2, or SSE2 under the existing SIMD cap. ARM64 uses NEON when SIMD is enabled. Each scan finishes with a scalar tail. The UTF-8 writer also uses the C# short ASCII path for 4–16 code units with SSE2 or NEON and 17–32 with AVX2. It validates the whole span for JSON escapes before committing the narrowed bytes.

The stock codecs store unescaped Unicode property heads as UTF-8 and UTF-16 `constexpr` data. The ordered reader compares the complete head in a validated range before advancing. Generated writers copy the opening brace and first property head together when they fit, and include the comma in later property heads. The name parser handles escapes, former names, out-of-order properties and unknown members. Generated codecs use a length switch when the encoding has at most eight candidates or every length bucket has at most four. Dense tables use a switch on the same seed-zero XXH3 as C#. Both strategies compare the complete name before accepting a match. The generator chooses independently for UTF-8 and UTF-16. Hand-written record codecs use direct comparisons.

For eligible ordinary class members, the Next contract also identifies runs of consecutive writable `Int32` fields. C++ codegen matches their property heads and decimal values with a bounded local cursor, publishes the completed prefix after each chunk of at most sixteen fields, and resumes the ordinary reader at the first unmatched field.

## Generate C++ DTOs and codecs

In a .NET project using Next, expose the export option to the compiler:

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportContract" />
</ItemGroup>
```

Run the build and the standalone tool from the repository root. Replace the assembly path and root metadata name with your model's.

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportContract=true
dotnet run --project tools/LuminPack.Codegen -c Release -- export path/to/Contracts.dll .tmp/contracts.json
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/contracts.json Example.Message .tmp/message.h
```

The export is a compiler projection of the resolved member order, binary profiles, layouts, Version descriptors, and Union hashes. The tool reads assembly metadata and produces a header that includes `<luminpack/luminpack.h>`. A generated root has its own namespace containing `root_value`, `root_binary`, `root_json`, and the declared JSON encoding and read limits. For `Example.Message`, the namespace is `luminpack_generated::root_Example_x002eMessage`.

Pass `generate --language cpp --binary-profile utf8-length` to match an explicit C# Binary operation such as `Serialize<Message, LuminPackUtf8LengthOptions>`. The other built-in choices are `utf8-token`, `utf16-length`, `utf16-token`, `utf8-varint`, and `utf16-varint`. A member's declared settings take precedence over its enclosing type's declared profile, and the operation profile supplies inherited settings. JSON uses its declared document options. An explicit profile adds a namespace suffix such as `_binary_utf8_length`, so its header can coexist with the default header and headers for other profiles. `--binary-profile` and `--rtti` can be combined.

A modeled closed generic type can be exported when a concrete C# serialization call reaches it directly or through a container. Copy its exact `id` from the manifest and quote it when passing it to `generate --language cpp .tmp/contracts.json 'Example.Box<string>' .tmp/box.h`.

Pass the generated `root_binary_limits` or `root_json_options` to its document reader to enforce a declared `MaxInputBytes` before decoding. The limit counts bytes in the supplied document. Advancing cursors keep their collection, string, and depth checks.

Use `read_root_binary(bytes)` from a newly generated header to match the managed assembly's `RequireCompleteBinaryInput` setting. It applies the generated input and depth limits and returns an owning graph document for a tracked root. The standalone `read_binary_document` and `read_binary_reference_document` entry points require a complete document by default.

For a Next Custom registration, provide a specialization such as `luminpack::custom_binding<u"Example.Message">` before including the generated header. The specialization supplies a `binary` codec and, when the .NET registration includes JSON, a `json` codec for the same value type. These can use `custom_codec` and `json_custom_codec`. The application owns the wire bytes and null representation. Generated Custom JSON uses the automatic registration's UTF-16 profile. A Binary-only registration requires only the `binary` codec.

Pass `--rtti` to `generate` to use C++ polymorphic DTOs for Next's exact and virtual Union dispatch tiers. An exact-tier writer compares `typeid(value)` with the declared concrete types. A virtual-tier writer calls the case's generated dispatch slot for the active Binary profile or JSON encoding. A generic declaration with multiple closed cases shares its compile-time case hashes across Binary and JSON. Readers use the canonical C# case hash in a `switch` in both tiers. When all direct cases are ordinary, each slot owns its case with `std::unique_ptr`. When a direct case is tracked, every slot in that Union borrows a pointer from the document graph, including ordinary and registered cases. The generated RTTI and variant headers for the same root and profile use the same namespace, so choose one representation per profile.

An RTTI Union exported from an assembly with registration enabled exposes `root_binary::registry`. Register an app-owned case derived from `registry::base_type` with `registry::register_case<Case, Binding>(tag)`. The binding supplies `read_binary` and `read_json` for the case payload and `write_binary` and `write_json` for the tagged case. JSON methods support the writer's UTF-8 and UTF-16 encodings. The Binary codec writes the escape hash before calling the delegate. Use `write_registered_union_tag<registry::wide_tags>(output, tag)` in the Binary delegate, and write the numeric `$type` and application payload in the `$value` envelope in the JSON delegate. Set the same tag and representation in C#. Generated cases keep their hash dispatch before the registry.

The emitter supports positional and Version class contracts, including tracked reference graphs, closed class or interface Unions, enums, fixed-width primitives, text, vectors and rectangular arrays, List, Dictionary, Packed and Tagged aggregates with generated field contracts, and sequential raw structs with proven storage. Nullable primitives and enums also work as Version fields, array or List elements, and dictionary values. Generation reports the member or type when its model needs an unimplemented representation.

For `[LuminPackCompress]` fields, the emitter binds scalar, enum, and proven headerless unmanaged array or List storage to the managed LUM2 collection framing. Forwarded jagged vectors keep their outer counts and compress each supported leaf vector. Binary preserves the distinct null and empty counts. JSON uses the ordinary element representation. Allow space for the count, frame length, and `compression::max_compressed_size` of the raw element bytes when writing a nonempty compressed field.

The emitter maps C# `nint` and `nuint` to `std::intptr_t` and `std::uintptr_t` in scalar fields and supported collections. Binary preserves the managed native width, including inside length-delimited Version fields. Use matching 32-bit or 64-bit endpoints for Binary. JSON uses decimal integer tokens and follows the default managed 64-bit parse and native-width conversion.

Self-referential List classes use a collection count in Binary and a JSON array. Generated C++ nodes expose an `items` vector. With reference tracking the graph owns the nodes and JSON uses the `$id` and `$value` envelope. An owning tree may contain a Union field whose cases are acyclic.

Ordinary recursive classes use `std::unique_ptr` for each recursive node, including values reached through List and Dictionary. Acyclic enclosing classes keep their ordinary value type. Default and Version use the Binary and JSON representations of their managed declarations. A Default Binary read follows the root's declared recursion depth. The native stack guard limits recursive record traversal to 512 levels.

A tracked object or an ordinary RTTI Union case can own an ordinary recursive subtree through a field. An ordinary recursive subtree can also refer to acyclic tracked descendants. The graph document owns the tracked objects and the recursive fields own their decoded copies. An ordinary recursive record can itself be a Union case. Its first node uses the case payload and its descendants use their normal record headers. A self-list Union case uses collection framing. Generation diagnoses Union descendants that lead back into owning recursion, and tracked descendants that lead back into recursion. It also diagnoses independent descendant limits, finite depth across a mixed recursive graph, and finite depth across isolated Version reads.

An ordinary Version class can be a generated variant or RTTI Union case. Its Binary payload shares the enclosing document's Version schema registry. Binary and JSON readers accept a known Version case whose payload is null and return a null Union value.

## Verify the benchmark models

The integration test uses the actual `SimpleClass.Create()` and `CharacterSaveData.Create()` factories from the managed benchmark project. Emit the .NET documents and generate both headers into the same directory:

```sh
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release -p:LuminPackExportContract=true -- write .tmp/cpp-models
dotnet run --project tools/LuminPack.Codegen -c Release -- export benchmarks/LuminPack.Benchmark/bin/Release/net10.0/LuminPack.Benchmark.dll .tmp/cpp-models/manifest.json
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/cpp-models/manifest.json LuminPackBenchmark.SimpleClass .tmp/cpp-models/simple-benchmark.hpp
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/cpp-models/manifest.json LuminPackBenchmark.CharacterSaveData .tmp/cpp-models/serialize-benchmark.hpp
```

Configure CMake with the absolute fixture directory. The test transcodes the original .NET binary and JSON through the generated C++ models. Then verify that .NET reads both C++ outputs back to the original benchmark values.

```sh
cmake -S src/LuminPack.Cpp -B .tmp/cpp -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models
cmake --build .tmp/cpp --config Release
ctest --test-dir .tmp/cpp -C Release -R luminpack_cpp_models --output-on-failure
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release --no-build -- read .tmp/cpp-models/out
```

To exercise the RTTI model gate, generate both headers with `generate --language cpp --rtti` and configure CMake with `LUMINPACK_CPP_RTTI_FIXTURES` pointing to that fixture directory. Run `luminpack_cpp_rtti` through CTest, then use the same managed verifier on its `out` directory.

## Compare native performance

After building the fixture gate above, configure the standalone benchmark with local checkouts of yalantinglibs 0.5.8 and RapidJSON at `297637bc0a0f00dc504dbc1a47ebdaa577f8ae19`. This RapidJSON revision includes the fix for an assignment operator that recent GCC and Clang reject in the 1.1.0 header. The fixture producer calls the .NET serializer's default overloads. The executable compares the generated models with `struct_pack` for binary and RapidJSON for UTF-16 and UTF-8 JSON. UTF-16 is the default LuminPack JSON encoding. The RapidJSON adapter writes typed values with `Writer` and materializes the same DTOs from the RapidJSON DOM. [Tested RapidJSON revision](https://github.com/Tencent/rapidjson/tree/297637bc0a0f00dc504dbc1a47ebdaa577f8ae19)

Set any of the additional binary dependency paths to add Bitsery 5.2.5 (`v5.2.5`), cereal 1.3.2 (`v1.3.2`), or MessagePack-C++ 7.0.0 (`cpp-7.0.0`). Bitsery uses its checked buffer reader and compact collection lengths. cereal uses `BinaryArchive` over memory streams. The MessagePack-C++ adapter writes records as arrays, stores the input UTF-16LE text in BIN values, and materializes the DTO from the parsed object. No text transcoding is timed for those binary adapters.

The optional UTF-8 rows use jsoncons 1.3.2 (`v1.3.2`), nlohmann/json 3.12.0 (`v3.12.0`), and simdjson 3.12.3 (`v3.12.3`). jsoncons uses its compact streaming encoder. nlohmann/json builds a DOM before `dump()`, including the returned string allocation. Both readers parse to a DOM and materialize the DTO. The simdjson row measures reading only with a reusable DOM parser and pre-padded input. The timed adapters include conversions between UTF-8 input or output and the generated DTO's UTF-16 strings.

```sh
cmake -S benchmarks/LuminPack.Benchmark.Cpp -B .tmp/cpp-bench -DCMAKE_BUILD_TYPE=Release \
  -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models \
  -DLUMINPACK_CPP_YALANTINGLIBS=/absolute/path/to/yalantinglibs \
  -DLUMINPACK_CPP_RAPIDJSON=/absolute/path/to/rapidjson \
  -DLUMINPACK_CPP_BITSERY=/absolute/path/to/bitsery \
  -DLUMINPACK_CPP_CEREAL=/absolute/path/to/cereal \
  -DLUMINPACK_CPP_MSGPACK=/absolute/path/to/msgpack-c \
  -DLUMINPACK_CPP_JSONCONS=/absolute/path/to/jsoncons \
  -DLUMINPACK_CPP_NLOHMANN_JSON=/absolute/path/to/nlohmann-json \
  -DLUMINPACK_CPP_SIMDJSON=/absolute/path/to/simdjson
cmake --build .tmp/cpp-bench --config Release
.tmp/cpp-bench/Release/luminpack_cpp_benchmark .tmp/cpp-models > .tmp/cpp-bench/results.csv
```

For a single-configuration generator, run the executable directly under `.tmp/cpp-bench`. The benchmark verifies values before measuring, reuses supported writer buffers, and materializes a fresh object on each read. The UTF-16 readers receive the same managed document. The UTF-8 readers receive its identical transcoding prepared outside the timing loop. Each binary reader receives its format's wire bytes for the same model. The CSV includes the wire size, median and sample coefficient of variation across seven timed samples, and per-operation allocation requests and requested bytes from a separate sampling pass. `ratio_to_reference` divides each median by the matching `struct_pack` binary or RapidJSON JSON median of the same encoding from the same run. Allocation sampling covers C++ `new` and the RapidJSON allocator adapter. The MessagePack-C++ zone uses additional C heap allocations beyond the reported count and bytes.

### Compare GCC and Clang

Use a separate CMake build directory for each compiler. With Ninja, add `-DCMAKE_CXX_COMPILER=g++` or `-DCMAKE_CXX_COMPILER=clang++` to both the fixture gate and the benchmark configure commands. GCC 16.2 and Clang 23.1 were exercised on Windows x64 with the same MinGW-w64 UCRT toolchain. Put the toolchain's `bin` directory on `PATH` so the benchmark and fixture executables can load its runtime DLLs. Keep the CPU affinity, dependency revisions, fixture bytes and build options fixed. Run each compiler several times, alternating their order and retaining each CSV.

The native chart command uses the main README renderer. [Chart script](../../benchmarks/LuminPack.Benchmark.Cpp/chart.py)

```sh
python benchmarks/LuminPack.Benchmark.Cpp/chart.py .tmp/cpp-charts \
  --current 'GCC=.tmp/gcc-current-1.csv' \
  --current 'Clang=.tmp/clang-current-1.csv'
```

Repeat `--current` for the remaining runs. Open `index.html` for the light and dark SVG charts. Bars show the median of the run medians. Chart ratios divide each library's time by LuminPack's time in the same run. `summary.csv` records the median ratio, ratio range, highest sample variation, wire size and allocation measurements. Use CI data for published benchmark figures.
