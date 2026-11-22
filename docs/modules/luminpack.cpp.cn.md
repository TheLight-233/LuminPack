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

`nullable_codec` 用 `std::optional<T>` 表示带标记的 `Nullable<T>` 字段。定宽 Version 字段的第二个模板参数是描述符给出的总宽度。`nullable_storage_codec` 和 `nullable_list_codec` 用 CLR 的步长和偏移描述原生字典值与连续可空集合。读取先校验整个范围及布尔标记，再转换为 C++ 值。多模型 Tagged 集合接受导出的每个标签，并以规范模型写出。对于 Native 存储，生成代码在编译时核对目标 ABI 的存在标记偏移、载荷偏移和步长，要求它们与导出的 CLR 布局匹配。两端须使用相同的布局。

`pair_codec` 将 .NET 的 `KeyValuePair<TKey, TValue>` 字段或字典值映射为 `std::pair`。二进制按键、值顺序写入，JSON 使用 `[key,value]`。生成的一维数组和 List 支持逐元素编码、无填充的定宽键值对，以及存储方案匹配的 Packed 键值对。对于定宽数值、字符或枚举组成的多模型 Tagged 键值对，`tagged_pair_list_codec` 接受所有已导出的布局标签，写入时将字段转换到 CLR 的规范模型。

`tuple_codec` 将受支持的 .NET `ValueTuple` 映射为 `std::tuple`。二进制沿用 CLR 的组件顺序，JSON 使用数组，第八个 `Rest` 组件保留为嵌套元组。生成的集合使用导出的 Packed 或 Tagged 布局，或经过证明的声明顺序无填充 CLR 布局。Tagged 读取在发布结果前验证直接和嵌套的布尔字节。codec 用 `std::get` 访问元组组件。

对已证明且带填充的无标签 CLR pair 或 tuple 布局，生成器会发出带显式填充字段的结构体，并在编译期检查 C++ 的偏移和步长。二进制复制保留包括填充在内的完整表示。JSON 沿用 pair 或 tuple 的组件，读取时将额外填充置零。

生成的 `Memory<T>`、`ReadOnlyMemory<T>` 和受支持的 `ReadOnlySequence<T>` 字段用 `std::vector` 持有元素。null 标记读为空 vector。`ArraySegment<T>` 和 `ImmutableArray<T>` 使用 `std::optional<std::vector<T>>`，保留 CLR 默认值的 null 标记。`ReadOnlySequence<T>` 将各段合并为同一个长度记录，对已建模的结构体、pair、tuple 和可空值沿用 CLR 的单元素格式。原始元素槽要求可证明的无标签布局。连续内存和数组视图按导出的布局选择 CLR 块 codec。

字符串 codec 可绑定六种 `string_profile`。Length 与 VarInt 区分 null 和空字符串。Token 使用零码元结束，null 与空字符串使用同一表示。含内嵌零码元的文本应使用长度模式。

用 `record_member` 和 `record_codec` 按 wire 顺序绑定 DTO 字段。Next 生成的二进制 Union 用 `union_codec` 绑定已有的 case hash 和各 case 的 payload codec。应用可通过 `custom_codec` 绑定自己实现的读写函数来复用同一份 wire 契约。成员顺序、标量类型、字符串 profile 和 Tagged 模型顺序均沿用源 wire 契约。

Version rev4 和 rev5 用 `version_member` 与 `version_codec` 绑定生成的字段描述符和旧名称哈希。`read_binary_document` 和 `write_binary_document` 为每份独立文档创建 schema 注册表。同一操作中连续读写多条记录时，给 codec 传入同一个 `version_state`。开始独立记录前调用 `reset()`。写入端顺序查找前八项，在注册第九个 schema 时创建开放寻址索引。

多模型 Tagged 数组支持原始字段。用 `sizeof` 和 `offsetof` 描述本地步长和字段偏移。参与原始复制的字段必须接受其对象表示的任意位模式，例如定宽整数。写出本地布局前应初始化会进入 wire 的填充字节。

生成器沿用 Next 的存储方案绑定递归 Packed 字段以及多模型 Tagged 数组和 List。多模型 Tagged 值独立出现或作为字典值时，使用紧凑字段顺序。非空的多模型 Tagged 集合携带导出模型的从 1 开始的标签。C++ 字段偏移与步长匹配模型时直接复制，否则转换到导出的第一个模型。无需标签的原始结构体可在 CLR 与目标 C++ 的字段偏移和步长一致时走 Native 路径。生成的填充字段让 DTO 复制保留这些原始字节。

跨模型 Tagged 原始块根据导出的偏移构建与 C# 一致的重排和分组转换计划。SIMD 只处理完整窗口，尾部由标量路径处理。异布局读取保留目标填充，同布局复制保留原始字节，写入规范布局时将填充清零。

Reader 在 Release 构建中也会校验输入范围。用 `reader_limits` 限制集合元素数量和解码后的 UTF-16 码元数量。读入缓冲区的操作返回借用目标存储的视图，在使用视图期间保持缓冲区有效。跨布局转换或字符串转换遇到输入输出重叠时，会在堆上暂存输入。

x64 启用 SIMD 时，UTF-16 Length 写入 1–16 个码元会使用与 C# 相同的有界短 Latin-1 路径。写入 17–64 个码元时，若原始 payload 完整放得下且输入输出 payload 不重叠，写入器使用 C# 的头尾路径。UTF-8 Length 写入使用 C# 的短 ASCII 路径，SSE2 支持 4–16 个码元，AVX2 支持 17–32 个码元，两者都要求 ASCII payload 完整放得下且输入输出 payload 不重叠。通用 x64 Latin-1 内核按 CPU 支持和完整块大小选择 AVX-512BW、AVX2 或 SSE2。其他目标使用可移植实现。设置 `LUMINPACK_CPP_MAX_SIMD_BITS` 为 `128`、`256` 或 `512` 可限制编译的最大向量宽度。设置 `LUMINPACK_CPP_SIMD=OFF` 可在 x64 上也使用可移植实现。

UTF-16 Length 新建字符串和读取符合条件的列表前缀时，使用 C# 的 4–16 码元 Latin-1 扩展路径。每条记录在解码前检查完整窗口与字符串配额。

固定 UTF-8 Length 写入也使用 C# 的长 ASCII 前缀 AVX2 路径，以及连续双字节或三字节 UTF-8 字符的 SIMD 路径。这些路径先探测前缀，再处理完整、已验证的向量分组。三字节 BMP 使用常量重排表，可以在单个 ASCII 分隔符后继续处理下一段。

如果同质探测未命中且字符串以 Latin-1 非 ASCII 字符开头，写入器可使用 C# 的混合 ASCII 与 Latin-1 路径。256 种重排掩码及精确写入宽度都是编译期常量。遇到第一个超出 Latin-1 的码元时恢复通用 UTF-8 编码器。

固定 UTF-8 Length 读取在两个长度字段相等时尝试有界 ASCII 扩展。短路径在写入前验证每个字节。长串 AVX2 路径在扩展到尚未返回的字符串时同步验证。其他输入由通用解码器校验。

### 引用跟踪

生成的头文件声明 `root_requires_reference_graph` 时，Binary 用 `read_binary_reference_document<root_binary>(bytes, root_binary_limits, root_binary_reference_depth)` 读取，JSON 用 `read_reference_json_document<root_json, root_json_encoding>(bytes, root_json_options)` 读取。返回的文档通过 `graph` 持有跟踪对象，`value` 是解码后的根值。跟踪类字段是指向图中对象的可空指针，使用这些指针期间需要保留文档。构造图时可用 `reference_graph::make<T>()` 分配跟踪 DTO，再连接返回的指针。写入使用 `write_binary_reference_document` 或 `write_reference_json_document`，每次开始新的标识表。

Default 和 Version 跟踪记录保留直接成员、受支持的集合和默认 variant Union 中的共享实例及环。Binary 的显式对象 ID 随操作与 Version 作用域一同传递。JSON 沿用 `$id` 和 `$ref`。当 .NET 根类型没有声明有限深度时，C++ Binary 图默认最多允许 512 层首次出现对象。调用方可以向读取入口传入深度上限。

## JSON

用 `read_json_document` 和 `write_json_document` 调用具体的 JSON codec。文档默认使用小端 UTF-16。与 UTF-8 .NET 根类型互通时，将第二个模板参数设为 `json_encoding::utf8`。二进制成员使用的字符串 profile 不会改变 JSON 文档的编码。

```cpp
using Number = luminpack::scalar_codec<std::int32_t>;
using Numbers = luminpack::json_codec<luminpack::list_codec<Number>>;
auto numbers = luminpack::read_json_document<Numbers, luminpack::json_encoding::utf8>(input_bytes);
auto written = luminpack::write_json_document<Numbers, luminpack::json_encoding::utf8>(output_bytes, numbers);
```

用 `json_record_member` 绑定 C# 属性名和旧名称，以 `json_record_codec` 组合 DTO。非空值或 Union case 的对象体使用 `json_record_body`。`json_union_codec` 使用 Next 发出的 `$type` 哈希和 `$value` 包装。`json_custom_codec` 绑定应用自己的 JSON 读写函数。

Reader 校验 JSON 语法和文档边界。`json_read_options::max_depth` 限制对象和数组的嵌套深度，默认为 1024。字符串键字典写成 JSON 对象，读取时兼容对象和键值对数组。其他键类型的字典使用键值对数组。

JSON 读取器先扫描完整的普通文本段，再解码转义或非 ASCII UTF-8。UTF-16 写入器对较长的普通文本段使用同一个有界扫描。x64 按现有 SIMD 宽度上限选择 AVX-512BW、AVX2 或 SSE2。ARM64 启用 SIMD 时使用 NEON。每条路径均用标量处理尾部。UTF-8 写入器还沿用 C# 的短 ASCII 路径，SSE2 和 NEON 处理 4–16 个码元，AVX2 处理 17–32 个码元。它先校验整个范围内的 JSON 转义条件，再提交窄化后的字节。

内置 codec 将无需转义的 Unicode 属性头保存为 UTF-8 和 UTF-16 `constexpr` 数据。顺序读取在校验可读范围后比较完整属性头，匹配才移动游标。空间充足时，生成的写入器一次复制左花括号和首个属性头，后续属性头包含逗号。转义、旧名称、乱序和未知属性由名称解析器处理。若某种编码的候选名总数不超过八个，或各长度分组均不超过四个，生成 codec 使用长度 `switch`。密集分组使用与 C# 相同的零种子 XXH3 `switch`。两种路径都在返回匹配结果前比较完整名称。UTF-8 和 UTF-16 各自选择路径。手写 codec 使用逐项精确匹配。

对符合条件的普通 class，Next 契约还会标记连续可写的 `Int32` 字段。C++ 生成器用有界局部游标匹配属性头和十进制值，每组最多十六个字段，提交已读取的前缀，并从第一个未匹配字段恢复通用读取。

## 生成 C++ DTO 和 codec

使用 Next 的 .NET 项目可以将导出开关提供给编译器：

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportCppContract" />
</ItemGroup>
```

在仓库根目录运行构建和独立生成工具，将示例程序集路径和根类型名改为你的项目实际值。

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportCppContract=true
dotnet run --project tools/LuminPack.Codegen.Cpp -- export path/to/Contracts.dll .tmp/contracts.json
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/contracts.json Example.Message .tmp/message.h
```

导出内容取自 Next 实际解析的成员顺序、二进制 profile、布局、Version 描述符和 Union 哈希。工具读取程序集元数据并生成包含 `<luminpack/luminpack.h>` 的头文件。每个生成根类型有自己的命名空间，提供 `root_value`、`root_binary`、`root_json`，以及声明的 JSON 编码和读取限制。`Example.Message` 的命名空间为 `luminpack_generated::root_Example_x002eMessage`。

将生成的 `root_binary_limits` 或 `root_json_options` 传给对应的文档读取入口，就会在解码前检查声明的 `MaxInputBytes`。限制以传入文档的字节数为单位。用于连续读取的游标继续执行集合、字符串和深度检查。

对 Next Custom 注册，在包含生成头文件之前提供特化，例如 `luminpack::custom_binding<u"Example.Message">`。特化提供 `binary` codec。如果 .NET 注册包含 JSON，也提供值类型相同的 `json` codec。可以使用 `custom_codec` 和 `json_custom_codec`。wire 字节和 null 表示由应用负责。生成的 Custom JSON 使用自动注册的 UTF-16 profile。仅注册 Binary 时只需提供 `binary` codec。

在 `generate` 后加 `--rtti`，可为 Next 的封闭精确分派层生成 C++ 多态 DTO，支持最多 32 个位置类 case。每个 Union 用 `std::unique_ptr` 持有具体对象。写入将 `typeid(value)` 与声明的具体类型逐个比较，读取按 C# 的规范 case hash 执行 `switch`。该选项同时适用于 Binary 和 JSON。同一个根的 RTTI 和 variant 头文件使用相同命名空间，每个程序按需选择一种表示。

从启用了注册的程序集导出的 RTTI Union 提供 `root_binary::registry`。应用 case 继承自 `registry::base_type`，通过 `registry::register_case<Case, Binding>(tag)` 注册。绑定提供读取 payload 的 `read_binary` 和 `read_json`，以及写入带标签 case 的 `write_binary` 和 `write_json`。JSON 方法支持 UTF-8 和 UTF-16 writer。Binary codec 在调用绑定前写入转义哈希，Binary 绑定可用 `write_registered_union_tag<registry::wide_tags>(output, tag)` 写入标签。JSON 绑定写入数字 `$type`，并用 `$value` 包装应用 payload。C# 与 C++ 使用相同的标签和表示。生成的 case 仍优先按哈希分派。

生成器支持位置类和 Version 类，包括引用跟踪对象图、封闭的类或接口 Union、枚举、定宽标量、文本、一维数组、List、Dictionary，以及具有已生成字段契约的 Packed 和 Tagged 聚合和存储经过验证的顺序布局原始结构体。可空原生类型和枚举也可以用作 Version 字段、数组或 List 元素和字典值。遇到尚未实现的 wire 表示时，生成器会报告对应成员或类型。

## 验证基准模型

互通测试调用托管基准项目实际使用的 `SimpleClass.Create()` 和 `CharacterSaveData.Create()`。将 .NET 文档和两个生成头文件放在同一目录。

```sh
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release -p:LuminPackExportCppContract=true -- write .tmp/cpp-models
dotnet run --project tools/LuminPack.Codegen.Cpp -- export benchmarks/LuminPack.Benchmark/bin/Release/net10.0/LuminPack.Benchmark.dll .tmp/cpp-models/manifest.json
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/cpp-models/manifest.json LuminPackBenchmark.SimpleClass .tmp/cpp-models/simple-benchmark.hpp
dotnet run --project tools/LuminPack.Codegen.Cpp -- generate .tmp/cpp-models/manifest.json LuminPackBenchmark.CharacterSaveData .tmp/cpp-models/serialize-benchmark.hpp
```

用 fixture 的绝对路径配置 CMake。测试用生成的 C++ codec 在 .NET 原始 Binary 和 JSON 之间转换，再由 .NET 读取 C++ 输出并与原始模型值对比。

```sh
cmake -S src/LuminPack.Cpp -B .tmp/cpp -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models
cmake --build .tmp/cpp --config Release
ctest --test-dir .tmp/cpp -C Release -R luminpack_cpp_models --output-on-failure
dotnet run --project tests/LuminPack.Tests.Cpp.Interop -c Release --no-build -- read .tmp/cpp-models/out
```

验证 RTTI 模型时，用 `generate --rtti` 生成两个头文件，并把 CMake 的 `LUMINPACK_CPP_RTTI_FIXTURES` 指向这份测试目录。通过 CTest 运行 `luminpack_cpp_rtti` 后，用同一个 .NET 验证器读取它生成的 `out` 目录。

## 对比原生性能

先完成上面的模型互通验证，再用本地的 yalantinglibs 0.5.8 和 RapidJSON 1.1.0 源码配置独立基准。.NET 测试数据由默认重载生成。可执行程序用生成的模型与 `struct_pack` 对比二进制性能，并按 LuminPack 默认编码与 RapidJSON 对比 UTF-16 JSON 性能。RapidJSON 适配器用 `Writer` 编码类型化数据，从 RapidJSON DOM 还原相同的 DTO。

```sh
cmake -S benchmarks/LuminPack.Benchmark.Cpp -B .tmp/cpp-bench -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_CPP_MODEL_FIXTURES=/absolute/path/to/.tmp/cpp-models -DLUMINPACK_CPP_YALANTINGLIBS=/absolute/path/to/yalantinglibs -DLUMINPACK_CPP_RAPIDJSON=/absolute/path/to/rapidjson
cmake --build .tmp/cpp-bench --config Release
.tmp/cpp-bench/Release/luminpack_cpp_benchmark .tmp/cpp-models > .tmp/cpp-bench/results.csv
```

使用单配置生成器时，在 `.tmp/cpp-bench` 下直接运行可执行程序。基准在计时前校验模型内容，写入复用缓冲区，每次读取新建对象。两边的 JSON 读取共用同一份 .NET UTF-16 文档。二进制读取使用同一个模型各自的线格式。CSV 记录负载字节数、七轮计时的中位数和样本变异系数，以及同轮耗时除以对照库耗时。每次操作的分配请求次数和请求字节数单独采样，覆盖 C++ `new` 和 RapidJSON 分配器适配器。
