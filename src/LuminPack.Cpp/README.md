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

Bind a string codec to one of the six `string_profile` values. Length and VarInt preserve null separately from empty. Token terminates with a zero code unit and encodes null as empty. Use a length profile for text containing embedded zero units.

Bind a DTO's fields in wire order with `record_member` and `record_codec`. A Next-generated binary Union uses `union_codec` with the emitted case hash and the corresponding payload codec. Share a custom wire representation by binding application-provided read and write functions through `custom_codec`. Use the source wire contract for member order, scalar types, string profile and Tagged model order.

Multi-model Tagged arrays support raw fields. Describe local Tagged strides and offsets with `sizeof` and `offsetof`. Each copied field's object representation must admit every bit pattern, as fixed-width integer fields do. Initialize any padding that a native write would expose.

The reader checks input ranges in Release builds. Use `reader_limits` to bound collection counts and decoded UTF-16 lengths. Buffer-oriented read-into calls borrow the supplied storage. Keep that storage alive while using a returned view. Overlapping layout conversions or string transforms stage their input on the heap.

The x64 Latin-1 path selects AVX-512BW, AVX2, or SSE2 based on support and complete block size. Other targets use the portable implementation. Set `LUMINPACK_CPP_MAX_SIMD_BITS` to `128`, `256`, or `512` to cap the compiled width. The CMake option `LUMINPACK_CPP_SIMD=OFF` selects the portable implementation on x64 as well.
