# Rust 版 LuminPack

[English](../../src/LuminPack.Rust/README.md)

用导出的清单为每个根类型构建一个共享模型库。Rust 和 Python 可以加载同一个库。在 Windows 上，确保加载器能找到编译器运行时 DLL。

```sh
cmake -S src/LuminPack.Native -B .tmp/message -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_MANIFEST=/absolute/contracts.json -DLUMINPACK_ROOT=Example.Message
cmake --build .tmp/message --config Release
```

在 Cargo 依赖中添加 `luminpack = { path = "../LuminPack/src/LuminPack.Rust" }`。使用本地构建的可信模型库。

```rust
use luminpack::{Codec, Format};
fn copy_binary(path: &std::path::Path, input: &[u8], output: &mut [u8]) -> Result<usize, String> {
    let codec = unsafe { Codec::load(path)? };
    let document = codec.decode(input, Format::Binary)?;
    Ok(document.encode_into(output, Format::Binary)?.len())
}
```

复用 codec 和输出缓冲区。Document 持有已解码的原生模型，并借用 codec。启用 Cargo 的 `values` 功能后，可用 `from_value`、`to_value` 经 UTF-8 JSON 表示转换 Serde 业务类型。字段名、Union 包装和字典形状需与契约一致。对于普通 i32 或 f32 的 Binary Array/List 根，`primitive::encode_list` 和 `decode_list` 直接使用 Rust 实现，`None` 表示空引用。

Binary、UTF-8 JSON 和 UTF-16LE JSON 沿用生成的 C++ 契约、输入限制、Version 状态、Union 分支和引用图所有权。Custom 特化通过 `LUMINPACK_BINDINGS` 指定 C++ 绑定头文件；已有的生成头文件可通过 `LUMINPACK_HEADER` 接入。注册外部 Union 分支时设置 `LUMINPACK_RTTI=ON`，并用 `LUMINPACK_REGISTRATION` 指定定义了 `void luminpack_register_model()` 的 C++ 头文件。每个模型库首次操作前执行一次该函数。按数据大小提供输出缓冲区，写入失败时缓冲区可能已部分修改。[模型与递归限制](luminpack.cpp.cn.md)

## 生成业务模型的二进制读写器

下面的命令从仓库根目录运行。先将导出的契约投影到一个根类型。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project contracts.json Example.Message message.contract.json
python tools/portable-direct/generate.py rust message.contract.json message.rs
```

在业务 crate 中添加指向 `src/LuminPack.Rust` 的 `luminpack` 路径依赖，将生成文件放在业务 crate 的 `src/message.rs`。

```rust
mod message;

fn round_trip(input: &[u8], output: &mut Vec<u8>) -> luminpack::direct::Result<()> {
    let value = message::decode_binary(input)?;
    message::encode_binary_into(&value, output)
}
```

`Root` 使用具体结构体和枚举。`encode_binary` 返回新的 `Vec<u8>`，`encode_binary_into` 追加到已有缓冲区，出错会回退到原长度。Rust `String` 不能保存孤立 UTF-16 代理项，遇到这种输入请使用 Document。

这条路径直接处理 UTF-16 Length 二进制，无需加载模型动态库。支持非递归、位置编码的记录，静态已知的 Union 分支，int32、int64、float32、byte、bool、字符串及其嵌套集合。生成器检查投影的适用性。版本容忍、引用追踪、Custom、运行时注册的 Union 分支及其他不适用的配置继续使用上面的 Document API。生成的 JSON 方法需要对应的模型库，在二进制与原生 JSON 实现之间转换。
