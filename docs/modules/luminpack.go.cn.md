# LuminPack for Go

[English](../../src/LuminPack.Go/README.md)

Go 模块读写 LuminPack 二进制，以及 UTF-8 或 UTF-16LE JSON。生成的 Go 文件嵌入共享契约的根 schema。[跨语言契约](../portable-runtime.cn.md)

## 从源码安装并生成绑定

使用 Go 1.23 或更高版本。在应用的 `go.mod` 中添加运行时模块依赖，并通过 `replace` 指向本地仓库。

```go
require github.com/TheLight-233/LuminPack/src/LuminPack.Go v0.0.0

replace github.com/TheLight-233/LuminPack/src/LuminPack.Go => ../LuminPack/src/LuminPack.Go
```

从 LuminPack 仓库根目录为共享指南中的 `Example.Message` 生成绑定。将输出放入应用相应的 Go 包中。默认包名为 `generated`。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language go --package generated .tmp/contracts.json Example.Message .tmp/message.go
```

## 读写数据

空类型标识选择根类型。遇到无效文档或与 schema 不匹配的模型值时，API 会返回 error。

```go
import (
    "example.com/myapp/generated"
    luminpack "github.com/TheLight-233/LuminPack/src/LuminPack.Go"
)

func transcode(binary []byte) ([]byte, error) {
    schema, err := generated.LoadMessageLuminPack()
    if err != nil {
        return nil, err
    }
    message, err := schema.DecodeBinary("", binary)
    if err != nil {
        return nil, err
    }
    return schema.EncodeJSONWithEncoding("", message, luminpack.UTF8)
}
```

加载后的 schema 可以在独立调用之间复用。可使用 `luminpack.Record{"Id": int32(7), "Text": "ready", "Measurements": []int32{2, 5}}` 构造模型。`EncodeBinary` 返回新的字节切片，`AppendBinary` 将结果附加到已有缓冲区。需要单次选择 JSON 编码时，将 `luminpack.UTF8` 或 `luminpack.UTF16` 传给 `EncodeJSONWithEncoding` 和 `DecodeJSONWithEncoding`。

| 契约值 | Go 值 |
| --- | --- |
| 记录或已建模的结构体 | 以导出成员名为键的 `luminpack.Record`。 |
| 标量数组或 List | 满足条件的存储使用有类型的切片。其他序列按传输顺序保存元素。 |
| 字典 | `luminpack.Dictionary`，按传输顺序保存键值条目。 |
| 矩形数组 | `luminpack.Rectangular`，包含长度、下界和行优先值。 |
| 声明或注册的 Union 分支 | `luminpack.Union`，包含具体类型标识和数据。 |
| 含填充的 Native 值 | `luminpack.NativeValue`，包含逻辑字段和原始二进制存储。 |
| 原始 UTF-16 码元 | 使用 `luminpack.UTF16String` 保留未配对的 UTF-16 代理码元。 |

启用引用跟踪的对象图使用同一个 `Record` map 表示重复节点。原生整数宽度由契约指定。

## 加载 schema 和绑定应用 codec

将 `project` 输出的字节传给 `luminpack.LoadSchema` 可直接使用契约。使用 `WithCustom(typeID, codec)` 绑定 Custom，使用 `WithRegisteredUnion(unionType, tag, caseType, codec)` 绑定注册分支。两者都返回新 schema。二进制读回调返回消耗的字节数。Custom 契约导出 JSON 或绑定注册式 Union 时，提供通过 `json.RawMessage` 交换数据的 JSON 回调。自动 Custom JSON 使用 UTF-16。

## SIMD 与 Go 回退实现

定宽类型切片使用带范围检查的整块传输。对于已建模的 Packed、Tagged 和 Native 结构体，schema 会将字节偏移编译为连续复制区间。Tagged 按规范布局写回，Native 按当前字段值重写并保留原有 padding。有序 JSON 读路径匹配预计算的属性头，其余输入走通用解析器。

amd64 使用 SSE2，并在硬件及系统支持时为完整数据块选用 AVX2 或 AVX-512BW。arm64 使用 NEON。文本内核加速 ASCII 扫描和适用的 UTF-16 转换。`purego` 构建标签可验证 Go 回退实现。以下命令在运行时模块目录执行。

```sh
go test ./...
go test -tags purego ./...
```

## 验证互通

```sh
python tests/LuminPack.Tests.Portable/verify.py --language go
python tests/LuminPack.Tests.Portable/verify.py --language go --suite strings --go-pure
```
