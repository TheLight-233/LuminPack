# 跨语言契约

[English](portable-runtime.md)

LuminPack 的各个运行时共用一份二进制和 JSON 契约。在 C# 中定义模型，导出编译器生成的契约，再为各个应用生成所需的绑定。导出文件记录成员顺序、配置、JSON 名称、Union 分支、Version 描述符、输入限制和物理布局。模型发生变化后重新生成绑定。

| 运行时 | 指南 |
| --- | --- |
| .NET | [快速开始](getting-started.cn.md) |
| C++ | [构建 C++20 库并生成 codec](modules/luminpack.cpp.cn.md) |
| Java | [编译运行时并绑定契约](modules/luminpack.java.cn.md) |
| Go | [使用源码模块和生成的 schema](modules/luminpack.go.cn.md) |
| TypeScript | [使用 ESM 包和生成的类型](modules/luminpack.typescript.cn.md) |

## 定义并导出模型

在定义契约的 .NET 项目中使用 Next 源代码生成器。下面的示例是一个 Default 位置记录。各语言生成的绑定采用导出文件中的成员名与二进制顺序。

```csharp
using System.Collections.Generic;
using LuminPack.Attribute;

namespace Example;

[LuminPackable]
public partial class Message
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public List<int> Measurements { get; set; } = new();
}
```

在 .NET 项目文件中将导出开关暴露给编译器。

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportContract" />
</ItemGroup>
```

编译程序集并导出编译器元数据。将第二条命令中的程序集路径替换为实际构建产物。

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportContract=true
dotnet run --project tools/LuminPack.Codegen -c Release -- export path/to/Contracts.dll .tmp/contracts.json
```

## 生成语言绑定

在仓库根目录运行，每条命令指定一种目标语言和一个根类型。使用完整模型名，或导出文件中的闭合泛型 `id`。包含命令行特殊字符的名称需要用引号包裹，例如 `'Example.Box<string>'`。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language cpp .tmp/contracts.json Example.Message .tmp/message.h
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language java .tmp/contracts.json Example.Message .tmp/Message.java
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language go .tmp/contracts.json Example.Message .tmp/message.go
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

Java、Go 和 TypeScript 也可以直接加载投影后的 JSON 契约。`project` 支持将受支持的独立类型表达式用作根，例如 `'System.Collections.Generic.List<Example.Message>'`。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project .tmp/contracts.json Example.Message .tmp/message.contract.json
```

选项放在契约文件路径之前。

| 选项 | 用法 |
| --- | --- |
| `--binary-profile <profile>` | 选择 `utf8-length`、`utf8-token`、`utf16-length`、`utf16-token`、`utf8-varint` 或 `utf16-varint`。类型和成员声明的优先级更高。 |
| `--rtti` | 生成 C++ RTTI Union codec，支持相应的虚派发与运行时注册。 |
| `--package <name>` | 设置生成的 Java 或 Go 包名。 |
| `--runtime-import <specifier>` | 设置 TypeScript 运行时的 ESM 模块标识。 |
| `--native-integer-bytes <4 或 8>` | 为 Java、Go 和 TypeScript 指定托管端 `nint`、`nuint` 的宽度，默认 8。C++ 由目标 ABI 决定。 |
| `--native-scalar-alignment <4 或 8>` | 为 Java、Go 和 TypeScript 指定托管端 64 位标量的对齐方式，默认 8。托管端的原生整数可以是 4 字节，标量仍按 8 字节对齐。 |

## 对齐两端的数据格式设置

- 两端使用相同的根类型和二进制配置，默认是 UTF-16 Length。Length 和 VarInt 区分 `null` 与空字符串。Token 使用零码元作为终止符，将 `null` 写成空字符串。需要保留内嵌零码元时，使用 Length 或 VarInt。
- 两端使用相同的 JSON 编码。JSON 契约还定义成员名、枚举值、集合形状和 Union 类型标识。[数据格式](formats.cn.md)
- 需要演进并读取旧数据的模型使用 `GeneratorType.Version`。两端的 Union 分支和注册 tag 保持一致。应用注册分支的 tag 应符合导出的宽度，并避开生成分支保留的值。[版本容忍](versioning.cn.md)、[Union 多态](polymorphism.cn.md)
- Custom 格式化器定义自己的数据负载，两端需要匹配的应用绑定。导出的自动 Custom 注册包含 JSON 操作时，使用 UTF-16。仅注册 Binary 时只需提供二进制回调。注册式 Union 的分支回调处理当前 JSON 编码。[自定义格式化器](custom-formatters.cn.md)
- 构建启用引用跟踪的对象图时，复用同一对象或记录实例。在 C++ 中使用借用指针期间，保持引用文档存活。[引用跟踪](versioning.cn.md#引用跟踪)
- 跨架构传输可以选择支持的 Packed 或 Tagged 布局。Native 二进制要求 CLR 布局匹配，包括填充和对齐，原生整数宽度需单独匹配。二进制矩形数组使用零下界，JSON 支持非零下界。[结构体布局选项](options.cn.md#3264-位结构体布局)

## 验证互通

互通测试在 .NET 中生成 `SimpleClass` 和 `CharacterSaveData`，通过 Java、Go、TypeScript 读写二进制、UTF-8 JSON 和 UTF-16 JSON，再由 .NET 验证结果。`strings` 测试组将六种二进制配置与托管端生成的数据逐字节对比。C++ 的互通测试通过 CMake 运行。[C++ 指南](modules/luminpack.cpp.cn.md#验证基准模型)

```sh
python tests/LuminPack.Tests.Portable/verify.py
python tests/LuminPack.Tests.Portable/verify.py --suite strings
python tests/LuminPack.Tests.Portable/verify.py --help
```

## 运行基准测试并绘制图表

为各目标语言准备相同的托管基准数据。需要运行 C++ 基准时，在准备命令中加上 `--cpp`，一并生成 C++ 头文件。

```sh
python benchmarks/LuminPack.Benchmark.Portable/prepare.py .tmp/portable-benchmark/models
```

在仓库根目录执行对应语言的命令。C++ 脚本使用 Bash 和 CMake，TypeScript 基准使用 Node.js 22 或更高版本。

```sh
python benchmarks/LuminPack.Benchmark.Java/run.py .tmp/portable-benchmark/models .tmp/portable-benchmark/java.csv
(cd benchmarks/LuminPack.Benchmark.Go && go run . ../../.tmp/portable-benchmark/models ../../.tmp/portable-benchmark/go.csv)
npm ci --prefix src/LuminPack.TypeScript
npm run build --prefix src/LuminPack.TypeScript
npm ci --prefix benchmarks/LuminPack.Benchmark.TypeScript
node benchmarks/LuminPack.Benchmark.TypeScript/benchmark.mjs .tmp/portable-benchmark/models .tmp/portable-benchmark/typescript.csv
bash benchmarks/LuminPack.Benchmark.Portable/run-cpp.sh .tmp/portable-benchmark/models .tmp/portable-benchmark
```

将 CSV 渲染为浅色、深色图表和 HTML 报告。多次传入 `--input` 可以纳入其他语言的结果。README 中发布的图表使用 CI 产物。[基准方法与 CI](ci.cn.md#各语言-runtime-基准)

```sh
python benchmarks/LuminPack.Benchmark.Portable/chart.py .tmp/portable-benchmark/charts --input .tmp/portable-benchmark/java.csv
```
