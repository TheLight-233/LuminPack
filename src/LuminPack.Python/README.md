# LuminPack for Python

[简体中文](../../docs/modules/luminpack.python.cn.md)

Build one shared model library per root from the exported manifest. Python and the other native bindings can load the same library. On Windows, make the compiler runtime DLLs available to the loader.

```sh
cmake -S src/LuminPack.Native -B .tmp/message -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_MANIFEST=/absolute/contracts.json -DLUMINPACK_ROOT=Example.Message
cmake --build .tmp/message --config Release
```

Install with `python -m pip install ./src/LuminPack.Python`. Load the locally built model library by path.

```python
from luminpack import Codec, Format

codec = Codec(model_library)
doc = codec.decode(binary, Format.BINARY)
output = bytearray(65536)
count = doc.encode_into(output, Format.UTF8)
json_bytes = bytes(output[:count])
```

Reuse the codec and buffer. For Python values, `doc.to_value(capacity)` produces the model's JSON representation. Edit it and call `codec.from_value(value)` to construct a new native document. Both accept `fmt=Format.UTF16` for UTF-16 Custom JSON. Reference markers are explicit in this representation. Ordinary Binary Array/List roots of int32 or float32 can use `encode_list(array('i', values))` and `decode_list(data, 'i')` directly. Use typecode `'f'` for float32.

Binary, UTF-8 JSON and UTF-16LE JSON use the generated C++ contract, including its limits, Version state, Union cases and tracked graph ownership. Configure `LUMINPACK_BINDINGS` with an application C++ header for Custom specializations, or supply an existing generated root through `LUMINPACK_HEADER`. For registered Union cases, set `LUMINPACK_RTTI=ON` and point `LUMINPACK_REGISTRATION` to a C++ header defining `void luminpack_register_model()`. This function runs once per model library before its first operation. Choose output capacity for your data. A failed encode may partially overwrite the destination. [Model and recursion rules](../LuminPack.Cpp/README.md)
