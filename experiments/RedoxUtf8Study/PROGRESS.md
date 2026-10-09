# ReDox 优化清单执行记录

每项通过验证后独立提交。基线 `9b857c4` 包含第一轮优化和研究记录。

| 候选 | 状态 | 结论 |
|---|---|---|
| JSON 长字符串分段编码 | 已提交 `9e3f095` | 实际模型耗时减少约 7.9% |
| 跳过未知字符串不分配 scratch | 已提交 `cf5edb8` | UTF-8 / UTF-16 转义值跳过为 0 B 分配 |
| 密集转义的小片段编码 | 已提交 `1bc8cd2` | 直接输出 UTF-8 scalar，耗时减少 24–28% |
| 属性名分派与名称复核 | 已提交 `f95029f` | 小模型免 hash；大模型和引用元数据复核名称 |
| string 输出的转码与计数 | 测量完成，保持默认 | UTF-16 输出中文 2.0x、纯 ASCII −15%，不改默认 |
| 外部 IBufferWriter 直接输出 | 已提交 `a679db8` | 按块直接写入，去掉完整中间缓冲和整体复制 |
| 多段输入 | 验证完成 | 复用 native staging，消除每次大数组分配 |
| CJK SIMD | 已提交 `845e24f` | 纯中文 32 / 4096 单元 2.4x / 3.4x；混合不倒退 |
| JSON 枚举底层宽度 | 已提交 `92f0d4c` | 修复 64 位截断与窄底层越界读；8 种底层类型全覆盖 |
| 大数组并行解码 | 待验证 | 元素边界、独立状态、引用与回调限制 |

## 密集转义

新版小片段编码器直接写入 2/3/4 字节 UTF-8，孤立代理项仍替换为 U+FFFD，完整代理对输出 4 字节。转义输出只进行一次容量检查。长普通片段仍走 .NET 批量转码。

与上一轮 `9b857c4` 比较，同进程 BenchmarkDotNet、.NET 10、5 次预热、12 次 250 ms 迭代：

| 输入 UTF-16 单元 | 上一轮 | 本轮 | 耗时减少 |
|---|---:|---:|---:|
| 8 | 42.23 ns | 31.93 ns | 24.4% |
| 4096 | 18.51 μs | 13.36 μs | 27.8% |

两边 0 B/op。原始数据：`round2-dense-benchmark.md`。

新增回归逐个验证全部 65,536 个 BMP 码元及代理对边界，另有随机、精确容量、少一字节与 canary 验证。三个框架完整测试通过（47 / 47 / 48 项），五个运行时目标构建成功。

## 属性名分派

字段数不超过 8 的模型按 UTF-8 字节长度 / UTF-16 单元长度分组，再比较实际名称。大模型按 hash 分组，命中后仍验证实际字节 / 字符；生成期 hash 碰撞会合并到同一 case，不生成重复 case。循环引用的 `$id` / `$ref` 使用同样的编码与名称复核。两种编码共享字段处理 switch，减少重复生成代码。

实际模型旧生成器读取均值 100.6 μs，新版 97.71 μs；波动范围有重叠，不能将约 2.9% 均值差视为普遍保证。分配保持 60.98 KB/op。原始数据为 `round2-property-*.md`。

三框架完整测试 53 / 53 / 54 项通过，覆盖同长度名称、转义名称、中文名称、大小写敏感、未知嵌套值和两种编码的循环引用。生成器并发回归通过 32 个并行编译、512 个有效类型。该项同时消除了仅凭 hash 识别名称的正确性缺口。

## 外部 IBufferWriter

新增直接 JSON 输出构造路径及 `FlushToBufferWriter`。生成的 `SerializeJson<T>(IBufferWriter<byte>)` 不再租用完整中间 native buffer，不再整体 CopyTo。UTF-8 大片段分批转码，UTF-16 和 raw 前缀分块复制；常规申请最多 4096 字节，状态、引号、逗号和深度跨块保留。`BytesWritten` 统计已提交和当前块；已有 CurrentIndex / GetSpan 在新构造路径中表示当前块。输出异常可能留下已提交的部分 JSON，操作状态仍会清理。

同一实现的直接输出与原“中间缓冲 + 复制”相比，30,000 中文单元的模型从 24.92 μs 到 23.40 μs，耗时约减少 6.1%，双方复用缓冲时 0 B/op。16 单元模型 352.6 / 361.2 ns，差异落在波动范围。基准源码为 `JsonBufferWriterBenchmark.cs`，数据为 `round2-stream-benchmark.md`。

59 / 59 / 60 项完整测试通过，五目标构建成功，生成器并发回归通过。新增测试检查原始 UTF-8/UTF-16 字节一致性、跨块 emoji、孤立代理项、转义属性名、最大申请、canary、重复 flush、字节计数和输出异常后的下一次操作。UTF-16 原始输出仍保持其原有孤立代理项字节，不能用会规范化的 string 返回 API 充当原始字节基线。

## 多段二进制输入

生成的 typed / generic sequence 入口使用可复用 native staging buffer，单段仍直接引用，unmanaged typed 快路径保持原样。新增 `LoadSequence` 负责容量和发布长度，finally 归还 staging，遵守既有最大缓存容量限制。连续 reader 的低层索引、布局和公开构造器契约保持兼容。

此项消除了每次 ToArray 的分配，仍需要一次连续化复制。现有生成 formatter 依赖任意 index 的连续 ref 访问，强制改成按段窗口会改变这项契约；此次采用可验证的入口优化。

复用 byte[] 结果的 1 KB 输入从 103.04 ns / 1056 B 到 58.60 ns / 0 B；64 KB 从 4.882 μs / 65,568 B 到 2.295 μs / 0 B。源数据为 `round2-sequence-benchmark.md`，基准为 `SequenceInputBenchmark.cs`。

63 / 63 / 64 项完整测试、五目标构建及生成器并发回归通过。新增空段、逐字节头部拆分、中文与 emoji、UTF-8 / UTF-16 选项、消耗长度、重复数组读取零 staging 分配、大小序列切换和空输入发布验证。

## CJK SIMD

新增 `Internal/Utf8BmpTranscoder`：SSSE3 精确存储前缀内核，每次处理 8 个 U+0800..U+FFFF 非代理项字符，直接写出 24 字节（16 + 8），不依赖调用方预留 padding。`StringSerializer.Serialize` 仅在 `mode == Utf8`、源至少 32 单元、目标至少 24 字节且首字符落在 3 字节 BMP 区间时尝试；命中后剩余部分仍交给平台 `Utf8.FromUtf16`，因此 `OperationStatus`、已消费长度、严格/替换语义完全保持不变。首字符为代理项（emoji）时直接跳过内核，避免无谓的向量探测。

净效果（`BeforeKernel` 复刻未加内核的同一包装，同进程比较）：纯中文 32 单元 53.4 → 22.3 ns（2.4x），4096 单元 6.07 → 1.80 μs（3.4x）。`ChineseFirstMixed` 只有前 8 单元为纯 BMP 时才命中，首个块含 ASCII 时短串多约 5 ns；ASCII 与 emoji 在噪声内。全部 0 B/op。源数据为 `round2-cjk-benchmark.md`，基准为 `Utf8BmpBenchmark.cs`。

netstandard2.1 不进入该内核，走原 `SerializeAsUtf8`。64 / 64 / 65 项完整测试、五目标构建通过。`Utf8BmpTranscoderTest` 遍历纯中文前缀加 ASCII / 2 字节 / emoji / 孤立及配对代理项后缀，对 0..长度×3+4 的全部目标容量核验状态、消费长度、写出字节与前后 canary。

## string 返回路径

`SerializeJson<T>()` 返回 string 时，当前用 UTF-8 内部输出再 `GetString`。实测把内部输出换成 UTF-16（结果字符串完全相同）中文文档 9.92 → 4.97 μs，但纯 ASCII 的 `CharacterSaveData` 32.4 → 37.3 μs，回退约 15%，因为输出缓冲翻倍而 ASCII 写入已接近拷贝成本。

这是数据分布取舍而非普适收益，因此**不改默认**：继续 UTF-8，中文为主的调用方可显式传 `StringEncoding = LuminPackStringEncoding.UTF16` 获得约 2 倍。数据为 `round2-string-return.md`，基准为 `JsonStringReturnBenchmark.cs`。`writer` 内累计 UTF-16 长度只省一次计数扫描，量级远小于 2 倍，且需在每个写入点计数，未采用。

## JSON 枚举底层宽度（正确性修复）

JSON 把枚举写成整数 token，但两条发射路径都先把值收窄到 `int`：`EnumEmitter` 的独立/集合 formatter 用 `Unsafe.As<T, int>`，内联代码生成器的 4 处用 `(int)` 强转。后果是 64 位底层枚举静默截断，`byte`/`short` 等窄底层枚举被当作 4 字节读取。修复前实测：

```
written : {"LongValue":705032704,"ULongValue":-989331456}
expected: LongValue=5000000000, ULongValue=18000000000000000000
```

`5,000,000,000` 只写回低 32 位，读回同样是错值，属静默数据损坏；`ulong` 更严重。二进制路径不受影响（用 `Unsafe.SizeOf<T>` 与 `WriteUnaligned`），只有 JSON 坏了；现有测试里唯一的非 int 底层枚举 `TestEnumVal : short` 只覆盖了二进制，因此没有拦住。

修复方式：新增 `EnumEmitter.GetJsonEnumTypeNames`，从 Roslyn 的 `INamedTypeSymbol.EnumUnderlyingType`（权威来源，而不是可能失真的 `LuminEnumFieldType`）解析底层关键字与匹配的 `WriteXxx`/`ReadXxx`，两条路径共用，共 6 处。

行为变化：8/16 位及有符号 32 位枚举的输出与修复前**逐字节一致**；64 位底层枚举由截断值变为正确值；`uint` 底层枚举大于 `int.MaxValue` 的值由负数变为正确的正数。

验证：新增 `JsonEnumBackingTypeTest` 覆盖全部 8 种底层类型的最小/最大值，含对象字段、独立枚举 formatter、`List`/数组三条路径，并对每个属性断言类型化数值。把生成器改动 stash 后重跑，**4 项全部失败**，证明用例确实能拦住该缺陷。恢复后 68 / 68 / 69 项完整测试通过，五目标构建成功，生成器并发回归通过（32 个并行编译、512 个有效类型）。
