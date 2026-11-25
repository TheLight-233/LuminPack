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
