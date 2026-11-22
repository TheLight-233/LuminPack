# LuminPack C++ Runtime

[English](../../src/LuminPack.Cpp/README.md)

C++20 Runtime 使用调用方提供的连续缓冲区读写 LuminPack 二进制记录。Codec 统一包含公开头文件 `<luminpack/luminpack.h>`，链接 `LuminPack::Cpp` 静态库。Latin-1 SIMD 内核在私有编译单元中构建，并在支持的 x64 处理器上运行时选择。

## 构建

在仓库根目录运行。CMake 3.30 之前的版本需要使用不含 `#` 的构建输出路径。

```sh
cmake -S src/LuminPack.Cpp -B .tmp/cpp -DCMAKE_BUILD_TYPE=Release
cmake --build .tmp/cpp --config Release
ctest --test-dir .tmp/cpp -C Release --output-on-failure
```

在另一个 CMake 项目中，可以把本仓库的 C++ 模块加入子目录并链接到应用目标。

```cmake
add_subdirectory(path/to/LuminPack/src/LuminPack.Cpp luminpack-cpp)
target_link_libraries(my_app PRIVATE LuminPack::Cpp)
```

## 读写位置对象

Codec 按对应 .NET 契约声明的顺序写入成员。下面写入一个 `Int32` 成员和一个采用默认 UTF-16 Length profile 的字符串。

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

Runtime 提供位置对象头、定宽数值标量、标量数组、含 compact Latin-1 的 UTF-16 Length 字符串，以及由定宽原始字段组成的多模型 Tagged 数组。由调用方提供具体的 codec。成员顺序、标量类型、字符串 profile 和 Tagged 模型顺序均沿用源 wire 契约。用 `sizeof` 和 `offsetof` 描述本地 Tagged 步长和字段偏移。参与原始复制的字段必须接受其对象表示的任意位模式，例如定宽整数。写出本地布局前应初始化会进入 wire 的填充字节。

Reader 在 Release 构建中也会校验输入范围。读入操作返回借用目标缓冲区的视图，并区分 null 与空值。在使用视图期间保持该缓冲区有效。普通读写无需为缓冲区分配内存。跨布局转换或 Latin-1 转换遇到输入输出重叠时，会在堆上暂存输入。

x64 的 Latin-1 路径使用 SSE2 和运行时选择的 AVX2。其他目标使用可移植实现。设置 CMake 选项 `LUMINPACK_CPP_SIMD=OFF` 可在 x64 上也使用可移植实现。
