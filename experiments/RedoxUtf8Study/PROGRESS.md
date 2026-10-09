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
| 时间/Guid 零分配格式化 | 已提交 `bf69e3e` | DateTime/DateTimeOffset/Guid/TimeSpan 耗时 0.45–0.72x，分配 −99.4% |
| Sizeof 合并长度少算 | 已提交 `d5383ae` | 运行时尺寸表达式被解析为 0；已复现并修复 |
| WriteUnmanaged 越界写 | **待修复** | 无容量检查，固定 span 过小时静默越界（已复现） |
| >255 字段的成员数截断 | **待修复** | `(byte)` 静默截断对象头计数 |
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

## 时间 / Guid 零分配格式化

改动前生成代码写 `writer.WriteString(value.ToString("O"))`，每个值先分配一个中间字符串再转码。新增 `LuminPackJsonWriter.WriteFormattedValue` 与 `WriteDateTime` / `WriteDateTimeOffset` / `WriteGuid` / `WriteTimeSpan`：先 `TryFormat` 进 `stackalloc` 缓冲区，再复用既有内容写入路径（含引号与逗号状态），生成器 4 处相应改为直接调用。索引与时间戳格式不含任何需要 JSON 转义的字符，因此内容路径只需转码或拷贝。

配对基准（同一次运行内与 `ToString` 路径比较，各写入 1000 个值）：耗时降到 **0.45–0.72 倍**（Guid / TimeSpan 0.45、DateTime 0.66、DateTimeOffset 0.72），分配从 56,520–96,536 B 降到 536 B，即 **−99.4%**。剩下的 536 B 是基准自身每次调用构造一个 `LuminPackWriterOptionalState`；xUnit 用例走线程静态状态池，64 值 × 4 类型实测 **0 B**。数据为 `round3-formatted-scalar-benchmark.md`，基准为 `JsonFormattedScalarBenchmark.cs`。

正确性按「与旧行为逐字节一致」验证，而不是只看往返：`JsonFormattedScalarTest` 对每种类型的最小/最大、UTC/Local/Unspecified、±14:00 与 ±12:00 偏移、亚毫秒 tick 等取值，断言写入器字节 == `ToString` 文本，并断言 UTF-8 与 UTF-16 两种编码输出相同的 JSON 字符串；另有嵌套/集合/可空往返与 0 分配用例。基准首次运行曾因 UTF-16 需要双倍缓冲而抛 `SpanOutOfRange`，当时据此认为「固定容量写入器一律抛异常而非越界写」；后续审查推翻了这个结论，见下节（该异常来自字符串路径的 `EnsureCapacity`，`WriteUnmanaged` 并无检查）。

范围限制：`DateOnly` / `TimeOnly` / `Int128` / `UInt128` 保留原 `ToString` 路径。低于 net8.0 的消费方会解析到 LuminPack 的 netstandard2.1 资产，该目标不可能声明这些类型的方法，生成代码若直接调用会编译失败；这四类在负载中也少见，故不为此引入跨资产可用性判断。

73 / 73 / 74 项完整测试通过，五目标构建成功，生成器并发回归通过。读取侧未改动：`DateTime.Parse` 等仍需先由 `ReadString()` 产生字符串，需要 reader 暴露 span 才能继续优化，属独立改动。

## 审查发现：Sizeof 合并长度少算（已修复）

`GetFixedFieldLength` 的兜底分支返回了一个畸形字符串（末尾多一个 `>`）：`"Unsafe.SizeOf<" + typeName + ">()>"`。调用方用 `int.TryParse` 消费它，解析失败即按 **0** 计。这不止是拼写问题：即使去掉多余的 `>`，该分支产出的是**表达式**而非数字字面量，`int.TryParse` 一样会读成 0。真正的修法是把合并组累加成表达式项：

```csharp
totalLength += 4 + Unsafe.SizeOf<global::System.Guid>();
```

只改了 `IsMergeableField` 守卫的两处（`LuminPackCodeGenerator.cs` 的容器元素合并路径）；另两处由 `IsUnmanagedFiledType` 守卫，只会产出数字字面量，不受影响。

触发条件：**托管**容器元素中出现 **≥2 个连续** mergeable 字段，其中至少一个是不在 switch 里的 unmanaged 类型（Guid / DateTime / DateTimeOffset / TimeSpan / Int128 / UInt128 / Half / Rune / Vector* / Complex / Plane / Quaternion / Matrix* / GCHandle）。单个 Guid 字段不触发（合并需要连续两个），纯 unmanaged 元素走 `SizeOf<element>*Count` 也绕开。修复前实测：

```
List<Element Guid,Guid,string>  : actual 98 B, Sizeof 34 B, delta 64  !! UNDER-REPORTED
List<Element int,DateTime,string>: actual 58 B, Sizeof 42 B, delta 16  !! UNDER-REPORTED
```

影响：公开 API `Sizeof<T>()` 静默返回错值，违反仓库已有断言 `ArraySizeofMatchesSerializedLength`。修复后上述两例均为 delta 0。

验证：新增 `SizeEvaluationMergeTest`，含 `List` 与数组两种形态、6 种运行时尺寸类型同组、以及单字段的对照用例（确认没有过度修正）。把生成器改动 stash 后重跑：合并用例**失败**、对照用例通过；恢复后 75 / 75 / 76 项完整测试通过，五目标构建成功，生成器并发回归通过。

## 审查发现：待修复项

**`WriteUnmanaged` 无容量检查（内存越界）**。`LuminPackWriter.Unmanaged.cs:19-41` 直接算指针 + `Unsafe.WriteUnaligned`，既无 `EnsureCapacity` 也无边界判断；`Advance` 的检查是 `#if DEBUG` 且发生在写之后。实测 32 字节写入 4 字节 span：**不抛异常，span 之后的 canary 被改写**。固定容量写入器（`LuminPackWriter(ref Span<byte>, state)`）是公开 API，且与上一项的少算组合后，用 `Sizeof` 定长分配再写入即可静默损坏内存。本轮按要求仅修上一项，此项仍未修。

**>255 字段的成员数截断**。`LuminPackCodeGenerator.cs:2690` 生成 `writer.WriteUnmanaged((byte){fields.Count}`，`(byte)300` 在 unchecked 下静默变 44；而 `WriteObjectHeader` 形参本身是 `byte`，线格式上限 255（兄弟分支传 `int` 字面量，超限反而编译失败，两条路径行为不一致）。

以下经复核**确认不是缺陷**：循环引用表的 `GetValueRefOrAddDefault → TryAdd → CuckooInsert → Resize + TryAdd` 递归，因 `Resize` 恒为容量翻倍而必然收敛；`GetEnumFieldType` 的 `sbyte → Byte` 映射在二进制路径按同宽度位级往返，正确；`nint`/`nuint` 按平台宽度序列化，进程内自洽，但二进制载荷不可跨 32/64 位移植，属文档事项。
