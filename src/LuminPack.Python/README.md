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

## Generate a binary codec for a business model

Run these commands from the repository root. Project the exported manifest to the root type first.

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project contracts.json Example.Message message.contract.json
python -m pip install Cython setuptools
python tools/portable-direct/build_python.py message.contract.json message .
```

Build on little-endian CPython with a C++ compiler. Place the extension on the application’s import path and use the CPython ABI it was built for.

```python
import message

value = message.decode_binary(payload)
payload = message.encode_binary(value)
```

Supply all declared record fields in a dict. Arrays and lists decode to lists, string-key dictionaries to dicts, and integer-key dictionaries to lists of key/value pairs. A Union uses `{"$type": numeric_tag, "$value": record}`.

Binary operations use the UTF-16 Length wire profile directly without loading the model library. Eligible projections include acyclic positional records, statically known Union cases, int32, int64, float32, byte, bool, strings and nested collections. The generator checks eligibility. Use the Document API above for version tolerance, reference tracking, Custom, runtime-registered Union cases and other unsupported profiles. The generated JSON helpers require the matching model library and convert through its binary and JSON codecs.
