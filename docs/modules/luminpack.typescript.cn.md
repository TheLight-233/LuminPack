# LuminPack for TypeScript

[English](../../src/LuminPack.TypeScript/README.md)

## 一行读写二进制

生成模块选择根契约，编码返回 `Uint8Array`。JSON 或自定义绑定使用 `codec` 或 `bindCodec(options)`。

```ts
import { serialize, deserialize } from "./message.js";

const bytes = serialize({ Id: 7, Text: "ready", Measurements: [2, 5] });
const copy = deserialize(bytes);
```

TypeScript ESM 运行时读写 LuminPack 二进制，以及 UTF-8 或 UTF-16LE JSON。生成器输出模型类型、根 codec 和用于绑定应用 codec 的入口。[跨语言契约](../portable-runtime.cn.md)

## 支持范围

四端共同支持的集合范围包括数组、`List<T>` 和 `Dictionary<TKey,TValue>`。根或字段可以是 `int`、`float`，也可以是 `Dictionary<int,List<float>>` 这样的闭合类型。对元素或值可空的集合，请在导出端 C# 项目中调用该闭合类型的序列化方法，以便清单记录二进制布局。

跨语言契约覆盖按位置编码的记录、声明式 Union 多态、Version 版本描述、记录引用追踪，以及应用提供的 Custom 编解码器。Custom 负载和外部注册的 Union 分支需要在接收端绑定对应回调。自动注册的 Custom JSON 使用 UTF-16。普通记录按值处理，需要保留对象身份或循环引用时，在 C# 契约上启用追踪。

从生成模块导入根类型的 `codec`，需要注册回调时调用 `bindCodec(options)`。列表使用数组，字典使用 Map。生成的二进制记录代码和符合条件的 UTF-8 JSON 写入代码都由同一入口调用。返回模型保留 bigint 字段和带类型标识的 Union 负载。

## 构建并生成绑定

使用 Node.js 和 npm 从源码构建 `@luminpack/typescript`。

```sh
npm ci --prefix src/LuminPack.TypeScript
npm run build --prefix src/LuminPack.TypeScript
```

在应用中通过本地路径依赖或 workspace 引用构建后的模块。在仓库根目录为共享指南中的 `Example.Message` 生成绑定。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language typescript .tmp/contracts.json Example.Message .tmp/message.ts
```

生成文件默认导入 `@luminpack/typescript`。其他打包方式可以通过 `--runtime-import` 指定运行时模块标识。

## 读写数据

从生成文件导入 `codec` 和 `Root`。二进制与 JSON 文档都使用 `Uint8Array`。JSON 编码参数为 `"utf8"` 或 `"utf16"`，省略时使用契约的 JSON 编码。

```ts
import { codec, type Root } from "./message.js";

const model: Root = { Id: 7, Text: "ready", Measurements: [2, 5] };
const binary = codec.writeBinary(model);
const copy = codec.readBinary(binary);
const json = codec.writeJson(copy, "utf8");
const fromJson = codec.readJson(json, "utf8");
```

| 契约值 | TypeScript 值 |
| --- | --- |
| 记录或已建模的结构体 | 使用导出成员名的对象。 |
| 数组或 List | 采用生成的元素类型的数组。写入定宽数值元素时也接受匹配的 TypedArray。 |
| 字典 | `Map<K, V>`。 |
| 64 位有符号或无符号整数 | `bigint`。32 位原生整数使用 `number`。 |
| Pair 或 Tuple | 具有对应元素类型的元组。 |
| 矩形数组 | `{ lengths, lowerBounds, values }`，元素采用行优先顺序。 |
| Union 分支 | `{ type, value }`，`type` 是具体类型标识。 |

启用引用跟踪的对象图使用同一个 JavaScript 对象表示重复节点。LuminPack JSON 读写保留整数精度。

生成的绑定以 `Root` 表示读出的值，以 `RootInput` 表示写入值。`RootInput` 也允许匹配的数值 TypedArray。直接加载投影契约时，可通过 `createCodec<Root, RootInput>(manifest)` 指定相同的类型。

## 加载契约和绑定应用 codec

调用 `createCodec<MyRoot>(manifest)` 可使用解析后的 `project` 输出。传入 `{ custom, registered }` 选项绑定应用数据。生成文件也提供 `bindCodec(options)`。Custom 绑定以完整类型标识为键。Union 注册按 Union 类型标识组织列表，每项设置具体 `type`、数字 `tag` 和数据 `codec`。

## WebAssembly SIMD 和 JavaScript 回退

运行时通过带边界检查的 TypedArray 路径处理连续标量。在小端平台，Binary 写入会直接复制匹配的数值 TypedArray 字节，压缩字段也使用这段字节作为输入。布尔和字符集合使用普通数组。二进制结构体块先校验完整范围，再按导出的 Packed、Tagged 或 Native 偏移访问字段。重新写入已读取的 Native 值时，会复制保存的字节并写入当前字段，从而保留 padding。环境支持时，长文本扫描使用内嵌的 WebAssembly SIMD 模块，并保留经测试的 JavaScript 回退路径。

```sh
npm test --prefix src/LuminPack.TypeScript
python tests/LuminPack.Tests.Portable/verify.py --language typescript
python tests/LuminPack.Tests.Portable/verify.py --language typescript --suite strings --typescript-no-wasm
```

修改 `wasm/text.c` 后，在运行时目录执行 `node scripts/build-text-simd.mjs` 重新生成内嵌模块。重新构建时需在 `PATH` 中提供 Clang 和 `wasm-ld`。
