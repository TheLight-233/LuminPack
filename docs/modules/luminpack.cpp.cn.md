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

## 组合 codec

用 `scalar_codec`、`boolean_codec` 和 `enum_codec` 读写定宽值。`list_codec` 适用于采用相同元素表示的一维数组与 List。数值与枚举列表校验完整范围后一次复制。`dictionary_codec` 写入数量及键值序列。内部的无序映射保留键值语义。需要重现另一端的枚举顺序时，向 `write_entries` 传入按目标顺序排列且键唯一的键值序列。

字符串 codec 可绑定六种 `string_profile`。Length 与 VarInt 区分 null 和空字符串。Token 使用零码元结束，null 与空字符串使用同一表示。含内嵌零码元的文本应使用长度模式。

用 `record_member` 和 `record_codec` 按 wire 顺序绑定 DTO 字段。Next 生成的二进制 Union 用 `union_codec` 绑定已有的 case hash 和各 case 的 payload codec。应用可通过 `custom_codec` 绑定自己实现的读写函数来复用同一份 wire 契约。成员顺序、标量类型、字符串 profile 和 Tagged 模型顺序均沿用源 wire 契约。

Version rev4 和 rev5 用 `version_member` 与 `version_codec` 绑定生成的字段描述符和旧名称哈希。`read_binary_document` 和 `write_binary_document` 为每份独立文档创建 schema 注册表。同一操作中连续读写多条记录时，给 codec 传入同一个 `version_state`。开始独立记录前调用 `reset()`。

多模型 Tagged 数组支持原始字段。用 `sizeof` 和 `offsetof` 描述本地步长和字段偏移。参与原始复制的字段必须接受其对象表示的任意位模式，例如定宽整数。写出本地布局前应初始化会进入 wire 的填充字节。

Reader 在 Release 构建中也会校验输入范围。用 `reader_limits` 限制集合元素数量和解码后的 UTF-16 码元数量。读入缓冲区的操作返回借用目标存储的视图，在使用视图期间保持缓冲区有效。跨布局转换或字符串转换遇到输入输出重叠时，会在堆上暂存输入。

x64 的 Latin-1 内核按 CPU 支持和完整块大小选择 AVX-512BW、AVX2 或 SSE2。其他目标使用可移植实现。设置 `LUMINPACK_CPP_MAX_SIMD_BITS` 为 `128`、`256` 或 `512` 可限制编译的最大向量宽度。设置 `LUMINPACK_CPP_SIMD=OFF` 可在 x64 上也使用可移植实现。
