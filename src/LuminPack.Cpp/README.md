# LuminPack for C++

[中文](../../docs/modules/luminpack.cpp.cn.md)

The C++20 runtime reads and writes LuminPack binary records over caller-owned contiguous buffers. Codecs include one public header, `<luminpack/luminpack.h>`, and link the `LuminPack::Cpp` static library. The library builds the Latin-1 SIMD kernels in private translation units and selects them at runtime on supported x64 processors.

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

## Write and read a positional record

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

Use `nullable_codec` for a marker-prefixed `Nullable<T>` represented by `std::optional<T>`. For a fixed-width Version member, pass its full descriptor width as the second argument. `nullable_storage_codec` and `nullable_list_codec` bind the CLR stride and offsets for native dictionary values and contiguous nullable collections. Reads check both the storage range and Boolean flag bytes before converting the block to C++ values. Multi-model Tagged blocks accept every exported tag and write the canonical model. Native blocks require matching CLR layouts at both endpoints.

Bind a string codec to one of the six `string_profile` values. Length and VarInt preserve null separately from empty. Token terminates with a zero code unit and encodes null as empty. Use a length profile for text containing embedded zero units.

Bind a DTO's fields in wire order with `record_member` and `record_codec`. A Next-generated binary Union uses `union_codec` with the emitted case hash and the corresponding payload codec. Share a custom wire representation by binding application-provided read and write functions through `custom_codec`. Use the source wire contract for member order, scalar types, string profile and Tagged model order.

For Version revision 4 and 5, bind the emitted field descriptors and former name hashes through `version_member` and `version_codec`. Use `read_binary_document` and `write_binary_document` for a fresh schema registry per document. For successive records in the same operation, pass one `version_state` to the codecs. Reset it before an independent record. The writer scans the first eight registrations and creates an open-addressed index when it registers the ninth schema.

Multi-model Tagged arrays support raw fields. Describe local Tagged strides and offsets with `sizeof` and `offsetof`. Each copied field's object representation must admit every bit pattern, as fixed-width integer fields do. Initialize any padding that a native write would expose.

The generator uses Next's storage plan to bind recursively Packed fields and multi-model Tagged arrays and lists. A multi-model Tagged value uses the packed field order when it stands alone or appears as a dictionary value. Nonempty multi-model Tagged blocks carry the exported one-based tag. The writer copies matching C++ layouts directly and converts unmatched layouts to the first exported model. A tag-free raw aggregate can use the Native path when the exported CLR offsets and stride match the target's C++ layout. Generated padding fields preserve those raw bytes across DTO copies.

The reader checks input ranges in Release builds. Use `reader_limits` to bound collection counts and decoded UTF-16 lengths. Buffer-oriented read-into calls borrow the supplied storage. Keep that storage alive while using a returned view. Overlapping layout conversions or string transforms stage their input on the heap.

With SIMD enabled on x64, UTF-16 Length writes of 1–16 code units use the same bounded short Latin-1 path as C#. For 17–64 code units, the writer uses the C# head and tail paths when the complete raw payload fits in disjoint storage. UTF-8 Length writes use the C# short ASCII path for 4–16 code units with SSE2 and 17–32 code units with AVX2 when the ASCII payload fits in disjoint storage. The general x64 Latin-1 path selects AVX-512BW, AVX2, or SSE2 based on support and complete block size. Other targets use the portable implementation. Set `LUMINPACK_CPP_MAX_SIMD_BITS` to `128`, `256`, or `512` to cap the compiled width. The CMake option `LUMINPACK_CPP_SIMD=OFF` selects the portable implementation on x64 as well.

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

JSON readers scan complete literal runs before decoding escapes or non-ASCII UTF-8. The UTF-16 writer uses the same bounded scan for long literal runs. On x64 the scan selects AVX-512BW, AVX2, or SSE2 under the existing SIMD cap. ARM64 uses NEON when SIMD is enabled. Each scan finishes with a scalar tail.

For ordinary DTO properties with ASCII names, the stock codecs store the UTF-8 and UTF-16 property heads as `constexpr` data. The ordered reader compares the complete head in a validated range before advancing. The writer shares the same static bytes. The name parser handles escapes, former names, out-of-order properties and unknown members. After parsing a name, generated codecs use a length switch and exact comparisons when the encoding has at most eight candidates or every length bucket has at most four. They select this path separately for UTF-8 and UTF-16. Dense buckets and hand-written codecs use direct comparisons.

## Generate C++ DTOs and codecs

In a .NET project using Next, expose the export option to the compiler:

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportCppContract" />
</ItemGroup>
```

Run the build and the standalone tool from the repository root. Replace the assembly path and root metadata name with your model's.

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportCppContract=true
dotnet run --project tools/LuminPack.Codegen.Cpp -- export path/to/Contracts.dll .tmp/contracts.json
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/contracts.json Example.Message .tmp/message.h
```

The export is a compiler projection of the resolved member order, binary profiles, layouts, Version descriptors, and Union hashes. The tool reads assembly metadata and produces a header that includes `<luminpack/luminpack.h>`. A generated root has its own namespace containing `root_value`, `root_binary`, `root_json`, and the declared JSON encoding and read limits. For `Example.Message`, the namespace is `luminpack_generated::root_Example_x002eMessage`.

Pass `--rtti` immediately after `generate` to use C++ polymorphic DTOs for Next's closed exact-dispatch Union tier. This mode accepts up to 32 positional class cases. Each Union slot owns its case with `std::unique_ptr`. Writers compare `typeid(value)` with the declared concrete types, and readers switch on the canonical C# case hash. The option applies to Binary and JSON. The generated RTTI and variant headers for the same root use the same namespace, so choose one representation for each program.

The emitter supports acyclic positional and Version class contracts, closed class or interface Unions, enums, fixed-width primitives, text, one-dimensional arrays, List, Dictionary, Packed and Tagged aggregates with generated field contracts, and sequential raw structs with proven storage. Nullable primitives and enums also work as Version fields, array or List elements, and dictionary values. Generation reports the member or type when its model needs an unimplemented representation.

## Verify the benchmark models

The integration test uses the actual `SimpleClass.Create()` and `CharacterSaveData.Create()` factories from the managed benchmark project. Emit the .NET documents and generate both headers into the same directory:

```sh
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release -p:LuminPackExportCppContract=true -- write .tmp/cpp-models
dotnet run --project tools/LuminPack.Codegen.Cpp -- export benchmarks/LuminPack.Benchmark/bin/Release/net10.0/LuminPack.Benchmark.dll .tmp/cpp-models/manifest.json
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/cpp-models/manifest.json LuminPackBenchmark.SimpleClass .tmp/cpp-models/simple-benchmark.hpp
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/cpp-models/manifest.json LuminPackBenchmark.CharacterSaveData .tmp/cpp-models/serialize-benchmark.hpp
```

Configure CMake with the absolute fixture directory. The test transcodes the original .NET binary and JSON through the generated C++ models. Then verify that .NET reads both C++ outputs back to the original benchmark values.

```sh
cmake -S src/LuminPack.Cpp -B .tmp/cpp -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models
cmake --build .tmp/cpp --config Release
ctest --test-dir .tmp/cpp -C Release -R luminpack_cpp_models --output-on-failure
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release --no-build -- read .tmp/cpp-models/out
```

To exercise the RTTI model gate, generate both headers with `generate --rtti` and configure CMake with `LUMINPACK_CPP_RTTI_FIXTURES` pointing to that fixture directory. Run `luminpack_cpp_rtti` through CTest, then use the same managed verifier on its `out` directory.

## Compare native performance

After building the fixture gate above, configure the standalone benchmark with local checkouts of yalantinglibs 0.5.8 and RapidJSON 1.1.0. The fixture producer calls the .NET serializer's default overloads. The executable compares the generated models with `struct_pack` for binary and RapidJSON for UTF-16 JSON using the default LuminPack encoding. The RapidJSON adapter writes typed values with `Writer` and materializes the same DTOs from the RapidJSON DOM.

```sh
cmake -S benchmarks/LuminPack.Benchmark.Cpp -B .tmp/cpp-bench -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models -DLUMINPACK_CPP_YALANTINGLIBS=/absolute/path/to/yalantinglibs -DLUMINPACK_CPP_RAPIDJSON=/absolute/path/to/rapidjson
cmake --build .tmp/cpp-bench --config Release
.tmp/cpp-bench/Release/luminpack_cpp_benchmark .tmp/cpp-models > .tmp/cpp-bench/results.csv
```

For a single-configuration generator, run the executable directly under `.tmp/cpp-bench`. The benchmark verifies values before measuring, reuses writer storage, and materializes a fresh object on each read. Both JSON readers receive the same managed UTF-16 document. Each binary reader receives its format's wire bytes for the same model. The CSV includes the wire size, median and sample coefficient of variation across seven timed samples, same-run time divided by the reference time, and per-operation allocation requests and requested bytes from a separate sampling pass. Allocation sampling covers C++ `new` and the RapidJSON allocator adapter.
