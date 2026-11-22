# Java、Go 和 TypeScript 互通

[English](portable-runtime.md)

Java、Go 和 TypeScript 与 [C++ Runtime](modules/luminpack.cpp.cn.md) 共用同一份 .NET 契约。生成的绑定提供根类型、成员顺序、二进制配置、JSON 名称、Union 分支和布局信息。请将导出文件和生成的绑定与对应版本的 .NET 契约一同管理。

## 导出与生成

在定义契约的 .NET 项目中使用 Next 生成器，并将导出选项暴露给编译器。

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackExportCppContract" />
</ItemGroup>
```

从仓库根目录导出编译后的程序集，并为每个根类型和目标语言分别生成绑定。

```sh
dotnet build path/to/Contracts.csproj -c Release -p:LuminPackExportCppContract=true
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- export path/to/Contracts.dll .tmp/contracts.json
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable java .tmp/contracts.json Example.Message .tmp/Message.java
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable go .tmp/contracts.json Example.Message .tmp/message.go
dotnet run --project tools/LuminPack.Codegen.Cpp -c Release -- generate-portable typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

闭合泛型根类型使用导出文件中的完整 `id`，在命令行中用引号包裹。将受支持的集合用作根类型时，传入完整的类型表达式，例如 `'System.Collections.Generic.List<Example.Message>'`。`project-portable` 使用相同的契约文件、根类型和输出路径参数，生成可直接加载的 JSON 契约。

命令选项放在契约文件路径前。`--binary-profile` 支持 `utf8-length`、`utf8-token`、`utf16-length`、`utf16-token`、`utf8-varint` 和 `utf16-varint`，类型与成员声明保留优先级。32 位 .NET 上的 `nint` 和 `nuint` 使用 `--native-integer-bytes 4`。Native Nullable 存储另有 `--native-scalar-alignment`，当对端的 64 位标量按 4 字节对齐时设为 `4`。两项默认均为 `8`。跨架构传输时可为类型选择 [Packed 或 Tagged](options.cn.md)。

## Java

使用 JDK 17 或更高版本编译生成文件与 [Java Runtime 源代码](../src/LuminPack.Java/src/main/java/luminpack)。生成类默认放在 `luminpack.generated` 包下，`--package` 可指定应用的包名。输出文件名决定生成类的名称。

```java
Object message = luminpack.generated.Message.CODEC.readBinary(binary);
byte[] json = luminpack.generated.Message.CODEC.writeJson(message, luminpack.LuminPack.UTF8);
```

记录使用以成员名为键的 Map。静态 Union 分支使用 `LuminPack.UnionValue`，无符号 32 位哈希存入 `long`。无符号 64 位值使用 `BigInteger`。使用 `LuminPack.fromManifest` 可直接加载投影后的 JSON 契约。使用 `withCustom` 绑定应用的格式化器，使用 `withUnionCase` 注册 Union 分支。

## Go

使用 Go 1.23 或更高版本。在应用模块中引用 `github.com/TheLight-233/LuminPack/src/LuminPack.Go`。从源码使用时，可通过 `replace` 指向本地 [Go Runtime](../src/LuminPack.Go)。生成包默认为 `generated`，`--package` 可指定所在目录的 Go 包名。

```go
schema, err := generated.LoadMessageLuminPack()
if err != nil {
    return err
}
message, err := schema.DecodeBinary("", binary)
if err != nil {
    return err
}
json, err := schema.EncodeJSONWithEncoding("", message, luminpack.UTF8)
```

传入空类型标识会选用导出的根类型。记录使用 `luminpack.Record`，字典使用保留 wire 顺序的 `luminpack.Dictionary`，Union 分支携带具体类型标识。使用 `luminpack.LoadSchema` 可直接加载投影后的 JSON 契约。使用 `WithCustom` 绑定应用的格式化器，使用 `WithRegisteredUnion` 注册 Union 分支。

## TypeScript

在 [TypeScript Runtime](../src/LuminPack.TypeScript) 源码目录运行 `npm ci` 和 `npm run build`，随后在应用中引用 `@luminpack/typescript`。需要其他 ESM 模块标识时，可使用 `--runtime-import`。

```ts
import { codec } from "./message.js";

const message = codec.readBinary(binary);
const json = codec.writeJson(message, "utf8");
```

生成的记录类型使用 .NET 成员名称。字典使用 `Map`，64 位整数使用 `bigint`，Union 分支使用 `{ type, value }`。二进制和 JSON 数据都使用 `Uint8Array`。使用 `createCodec` 可直接加载投影后的 JSON 契约。在生成的绑定中可调用 `bindCodec` 并传入 `custom` 或 `registered`，绑定应用格式化器或注册 Union 分支。

## Runtime 快路径

- **Java** 使用按机器字批处理的字符串操作和连续 Primitive 存储。启用 SIMD 扫描时，用 `javac --add-modules jdk.incubator.vector` 编译 `src/LuminPack.Java/src/vector/java/luminpack/VectorStringKernels.java`，并将基础 Runtime 类加入编译 classpath。运行时将 Vector 类和 `src/LuminPack.Java/src/vector/resources` 一起加入应用的 classpath，并使用 `java --add-modules jdk.incubator.vector` 启动。JVM 会选择支持的向量宽度。
- **Go** 在 amd64 使用 SSE2，并在 CPU 和操作系统均支持时为完整数据块选择 AVX2 或 AVX-512BW。在 arm64 使用 NEON。这些内核加速 ASCII 扫描和适用的 UTF-16 转换。其他架构以及 `-tags purego` 构建使用 Go 实现。
- **TypeScript** 对连续 Primitive 数据使用带边界检查的 TypedArray 快路径。支持时使用 WebAssembly SIMD 扫描长文本，其余环境走 JavaScript 路径。SIMD 模块内嵌在 Runtime 中。如修改 `wasm/text.c`，可在 TypeScript Runtime 目录运行 `node scripts/build-text-simd.mjs` 重新生成模块，需在 `PATH` 中提供 Clang 和 `wasm-ld`。

## 验证互通

两端使用相同的根类型、二进制配置和 JSON 编码。构建启用引用跟踪的对象图时，复用同一记录实例以保留引用身份。Custom 格式化器与注册式 Union 分支需要在两端提供匹配的实现。

在仓库根目录运行互通测试。默认使用 `SimpleClass` 和 `CharacterSaveData` 基准模型，验证 .NET Binary、UTF-8 JSON 和 UTF-16 JSON 的跨语言读写。通过 `--help` 查看其他测试组和工具路径设置。

```sh
python tests/LuminPack.Tests.Portable/verify.py
```
