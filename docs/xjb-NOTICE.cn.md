# 第三方许可声明

## XJB 浮点转字符串算法

LuminPack 的 C#、C++、Java、Go 和 TypeScript JSON 浮点格式化器使用 XJB 算法的移植实现。

| 项目 | 说明 |
|---|---|
| 上游项目 | [xjb](https://github.com/xjb714/xjb) |
| 固定提交 | `80cc89574a8f8457ffbf951afa2fd27c2459bd4a`（2026-08-10） |
| 论文 | Xiang 和 Wang，《xjb: Fast Float to String Algorithm》，预印本 202511.1698 |
| 许可 | [Apache License 2.0](xjb-LICENSE.txt) |

C# 从上游移植了以下内容。

| 上游文件 | 移植内容 | LuminPack 文件 |
|---|---|---|
| `bench/xjb/float_to_decimal/xjb32_i.cpp`（`xjb_v2_f32_to_dec`） | binary32 最短十进制转换、`pow10_table_reverse`、`h37` | `src/LuminPack.Core/Core/Json/Xjb/XjbFloat32.cs`、`XjbTables.g.cs` |
| `bench/xjb/float_to_decimal/xjb64_i.cpp`（`xjb64_v2_f64_to_dec`） | binary64 最短十进制转换和 `g[1234]` | `src/LuminPack.Core/Core/Json/Xjb/XjbFloat64.cs`、`XjbTables.g.cs` |
| `bench/xjb/float_to_decimal/xjb64_i.cpp` | 用 `h7[2048]` 校对可移植的 `h` 公式 | `tests/LuminPack.Tests/JsonXjbFormatterTest.cs` |

`tools/xjb-tables/emit_portable.py` 从已入库的 C# 数据生成以下运行时的 XJB 表。

| 运行时 | 转换代码 |
|---|---|
| C++ | `src/LuminPack.Cpp/src/json_number_format.cpp` |
| Java | `src/LuminPack.Java/src/main/java/luminpack/XjbDecimal.java` |
| Go | `src/LuminPack.Go/xjb.go` |
| TypeScript | `src/LuminPack.TypeScript/src/json-number.ts`、`src/LuminPack.TypeScript/wasm/json-number.c` |

- 上游许可文件使用 Apache License 2.0。完整许可文本见 [xjb-LICENSE.txt](xjb-LICENSE.txt)。
- `tools/xjb-tables/extract_tables.py` 从上游源码提取查找表，还会推导 `h` 移位值并输出供单元测试验证的 FNV-1a 哈希。
- `src/LuminPack.Core/Core/Json/Xjb/LuminPackJsonFloatFormatter.cs` 定义 LuminPack 自身的 JSON 数字格式和写入逻辑。JSON 浮点输出只接受有限值。
