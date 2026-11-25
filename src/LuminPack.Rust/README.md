# LuminPack for Rust

[简体中文](../../docs/modules/luminpack.rust.cn.md)

Build one shared model library per root from the exported manifest. Rust and the other native bindings can load the same library. On Windows, make the compiler runtime DLLs available to the loader.

```sh
cmake -S src/LuminPack.Native -B .tmp/message -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_MANIFEST=/absolute/contracts.json -DLUMINPACK_ROOT=Example.Message
cmake --build .tmp/message --config Release
```

Add `luminpack = { path = "../LuminPack/src/LuminPack.Rust" }` to your Cargo dependencies. Use a locally built, trusted model library.

```rust
use luminpack::{Codec, Format};
fn copy_binary(path: &std::path::Path, input: &[u8], output: &mut [u8]) -> Result<usize, String> {
    let codec = unsafe { Codec::load(path)? };
    let document = codec.decode(input, Format::Binary)?;
    Ok(document.encode_into(output, Format::Binary)?.len())
}
```

Reuse the codec and output buffer. A document owns its decoded native model and borrows the codec for its lifetime. Enable the Cargo feature `values` to convert a Serde business type through the UTF-8 JSON representation with `from_value` and `to_value`. Match the wire field names, Union envelope and dictionary shapes. For ordinary binary Array/List roots of i32 or f32, `primitive::encode_list` and `decode_list` run directly in Rust. `None` represents a null collection.

Binary, UTF-8 JSON and UTF-16LE JSON use the generated C++ contract, including its limits, Version state, Union cases and tracked graph ownership. Configure `LUMINPACK_BINDINGS` with an application C++ header for Custom specializations, or supply an existing generated root through `LUMINPACK_HEADER`. For registered Union cases, set `LUMINPACK_RTTI=ON` and point `LUMINPACK_REGISTRATION` to a C++ header defining `void luminpack_register_model()`. This function runs once per model library before its first operation. Choose output capacity for your data. A failed encode may partially overwrite the destination. [Model and recursion rules](../LuminPack.Cpp/README.md)

## Generate a binary codec for a business model

Run these commands from the repository root. Project the exported manifest to the root type first.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project contracts.json Example.Message message.contract.json
python tools/portable-direct/generate.py rust message.contract.json message.rs
```

Add `luminpack` as a path dependency on `src/LuminPack.Rust` and place the generated file at `src/message.rs` in the consuming crate.

```rust
mod message;

fn round_trip(input: &[u8], output: &mut Vec<u8>) -> luminpack::direct::Result<()> {
    let value = message::decode_binary(input)?;
    message::encode_binary_into(&value, output)
}
```

`Root` uses concrete structs and enums. `encode_binary` returns an owned `Vec<u8>`. `encode_binary_into` appends to a reusable buffer and restores its original length on error. Use Document for isolated UTF-16 surrogates, which Rust `String` cannot represent.

Binary operations use the UTF-16 Length wire profile directly without loading the model library. Eligible projections include acyclic positional records, statically known Union cases, int32, int64, float32, byte, bool, strings and nested collections. The generator checks eligibility. Use the Document API above for version tolerance, reference tracking, Custom, runtime-registered Union cases and other unsupported profiles. The generated JSON helpers require the matching model library and convert through its binary and JSON codecs.
