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

The runtime exposes positional framing, fixed-width numeric scalars, raw scalar arrays, UTF-16 Length records with compact Latin-1, and multi-model Tagged arrays of raw fields. Codecs are supplied by the caller. Use the source wire contract for member order, scalar types, string profile and Tagged model order. Describe local Tagged strides and offsets with `sizeof` and `offsetof`. Each copied field's object representation must admit every bit pattern, as fixed-width integer fields do. Initialize any padding that a native write would expose.

The reader checks input ranges in Release builds. Read-into calls borrow the supplied storage and distinguish null from an empty result. Keep that storage alive while using a returned view. Ordinary reads and writes have no buffer allocations. An overlapping layout conversion or Latin-1 transform stages its input on the heap.

The x64 Latin-1 path uses SSE2 and CPU-dispatched AVX2. Other targets use the portable implementation. The CMake option `LUMINPACK_CPP_SIMD=OFF` selects the portable implementation on x64 as well.
