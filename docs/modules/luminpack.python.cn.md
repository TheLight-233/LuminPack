# Python 版 LuminPack

[English](../../src/LuminPack.Python/README.md)

用导出的清单为每个根类型构建一个共享模型库。Rust 和 Python 可以加载同一个库。在 Windows 上，确保加载器能找到编译器运行时 DLL。

```sh
cmake -S src/LuminPack.Native -B .tmp/message -DCMAKE_BUILD_TYPE=Release -DLUMINPACK_MANIFEST=/absolute/contracts.json -DLUMINPACK_ROOT=Example.Message
cmake --build .tmp/message --config Release
```

使用 `python -m pip install ./src/LuminPack.Python` 安装，通过路径加载本地构建的模型库。

```python
from luminpack import Codec, Format

codec = Codec(model_library)
doc = codec.decode(binary, Format.BINARY)
output = bytearray(65536)
count = doc.encode_into(output, Format.UTF8)
json_bytes = bytes(output[:count])
```

复用 codec 和缓冲区。需要 Python 对象时，用 `doc.to_value(capacity)` 取得模型的 JSON 表示，编辑后用 `codec.from_value(value)` 构造新文档。两者接受 `fmt=Format.UTF16` 以支持 UTF-16 Custom JSON。该表示保留显式引用标记。普通 int32 或 float32 的 Binary Array/List 根可直接使用 `encode_list(array('i', values))` 和 `decode_list(data, 'i')`，float32 对应类型码 `'f'`。

Binary、UTF-8 JSON 和 UTF-16LE JSON 沿用生成的 C++ 契约、输入限制、Version 状态、Union 分支和引用图所有权。Custom 特化通过 `LUMINPACK_BINDINGS` 指定 C++ 绑定头文件；已有的生成头文件可通过 `LUMINPACK_HEADER` 接入。注册外部 Union 分支时设置 `LUMINPACK_RTTI=ON`，并用 `LUMINPACK_REGISTRATION` 指定定义了 `void luminpack_register_model()` 的 C++ 头文件。每个模型库首次操作前执行一次该函数。按数据大小提供输出缓冲区，写入失败时缓冲区可能已部分修改。[模型与递归限制](luminpack.cpp.cn.md)

## 生成业务模型的二进制读写器

下面的命令从仓库根目录运行。先将导出的契约投影到一个根类型。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- project contracts.json Example.Message message.contract.json
python -m pip install Cython setuptools
python tools/portable-direct/build_python.py message.contract.json message .
```

编译需要小端 CPython 和 C++ 编译器。将生成的扩展放在应用可导入的位置，使用编译它的 CPython ABI。

```python
import message

value = message.decode_binary(payload)
payload = message.encode_binary(value)
```

记录使用包含全部声明字段的 dict，Array/List 使用 list，字符串键字典使用 dict，整数键字典使用键值对列表。Union 使用 `{"$type": 数字标签, "$value": 记录}`。

这条路径直接处理 UTF-16 Length 二进制，无需加载模型动态库。支持非递归、位置编码的记录，静态已知的 Union 分支，int32、int64、float32、byte、bool、字符串及其嵌套集合。生成器检查投影的适用性。版本容忍、引用追踪、Custom、运行时注册的 Union 分支及其他不适用的配置继续使用上面的 Document API。生成的 JSON 方法需要对应的模型库，在二进制与原生 JSON 实现之间转换。
