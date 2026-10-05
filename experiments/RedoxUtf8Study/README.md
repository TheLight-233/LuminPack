# RE:Dox 与 LuminPack 性能借鉴分析

本文保留初始研究结论。已接入生产代码的第一轮改动及最终验证见 [迭代记录](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/ITERATION.md>)。

分析日期：2026-10-05。依据本地源码，RE:Dox 实际根目录为 `C:/Users/28027/Downloads/REDox-main/REDox-main`。结论对应当前工作区，不代表两个项目所有版本。没有修改 LuminPack 的运行时、生成器或已有 benchmark；新增独立可复现的实验。

最值得先做的是 **JSON 字符串按片段转码**，其次是 **避免整体复制和重复扫描**。RE:Dox 的优势不能归结为一个更快的 UTF-8 内核：其 `Utf8Helper.GetUtf8String` / `GetUtf16String` 直接调用 `Encoding.UTF8.GetBytes` / `GetString`，而 LuminPack 在 .NET 8+ 已使用 `Utf8.FromUtf16` / `ToUtf16`。

## 1. 优先实施：JSON 字符串按连续片段编码

对应代码：

- [RE:Dox JsonTextEncoder.Encode](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/Json/JsonTextEncoder.cs:299>)：使用静态缓存的 `SearchValues<char>`，以 `IndexOfAny` 找到转义字符，再对前面的连续片段调用一次 `Encoding.UTF8.GetBytes`。
- [LuminPack WriteStringContentUtf8](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/Json/LuminPackJsonWriter.cs:386>)：先逐字符扫描；纯 ASCII 再逐字符窄化；任何非 ASCII 都进入慢路径。
- [LuminPack WriteStringContentSlow](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/Json/LuminPackJsonWriter.cs:611>)：对每个非 ASCII 字符或代理对调用 `Encoding.UTF8.GetBytes`，随后 `WriteRaw`；ASCII 也逐字节写入。

中文本身不需要 JSON 转义。当前把“需要转码”与“需要转义”合并判断，使大量普通中文付出逐字符编码、容量检查和方法调用成本。长纯 ASCII 则付出标量扫描和标量复制的成本。

建议 .NET 8+ 使用仅含 U+0000–U+001F、双引号、反斜杠的静态 `SearchValues<char>`。一次找到下一个转义位置，整段编码，然后输出那个转义字符。保留当前转义字节形式，包括 `\b` / `\f` 当前写成 `\u0008` / `\u000c` 的行为。沿用 .NET 对无效 UTF-16 代理项的替换语义；不能直接套用二进制 writer 的严格拒绝模式。

稀疏转义时保持大块编码；密集转义时采用小段的标量输出或经测试的密度切换。原型表明无条件反复搜索和调用编码器会让密集转义退化，因此混合策略的阈值必须由数据确定。

### 本机实验

`Program.cs` 独立实现上述策略，与从当前源码重新构建的 `LuminPackJsonWriter.WriteString` 比较。两边均创建 writer、写入开闭引号、复用相同 optional state 和预分配缓冲区。原型利用已充足的连续目标空间直接写内容，没有实现生产 writer 的逐段扩容逻辑。

环境：.NET 10.0.9、Windows x64、20 个逻辑处理器；两次独立进程复跑，`DOTNET_TieredCompilation=0`。每组 15,000 次预热、7 轮交替计时、取中位数；基于 Stopwatch 的可行性实验，**不是 BenchmarkDotNet 正式结论，也不是 RE:Dox 整库对比**。

4096 个 UTF-16 code unit 的结果（第一次稳定运行）：

| 输入 | 当前 writer | 按片段编码原型 | 两次运行的加速比 |
|---|---:|---:|---:|
| 纯 ASCII | 3149.5 ns | 238.1 ns | 13.23–13.24x |
| 纯中文 | 34346.1 ns | 2988.8 ns | 11.49–11.66x |
| 中英混合 | 14054.9 ns | 1448.6 ns | 9.70–10.47x |
| emoji 混合 | 19444.5 ns | 2423.6 ns | 7.90–8.02x |
| 稀疏转义：63 个中文后一个换行 | 33991.1 ns | 3727.2 ns | 9.03–9.12x |
| 密集转义 | 17437.8 ns | 22301.4 ns | 0.78–0.81x，退化 |

两边计时阶段均为 0 B/op。8 个 ASCII 字符的收益只有约 1.08–1.09x；不要把长字符串倍率推广到短属性名、小对象或整个序列化过程。部分名为 Mixed/Emoji 的 8 字符样本只是模式的 ASCII 前缀，不能用来判断短 emoji 性能。

正确性：2,012 个边界及随机用例，每个验证输出字节与当前 writer 完全一致、System.Text.Json 解析后的 roundtrip、输出尾部 canary 未改变、精确容量目标缓冲区成功。覆盖空字符串、控制字符、正常及截断代理对、孤立高/低代理项。另对 24 组计时输入执行同样检查。

完整数据：[第一次稳定运行](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/results-no-tiering.md>)、[独立进程复跑](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/results-confirmation.md>)。

## 2. UTF8Helper.EncodeToString：学习分段处理和长度知识

[RE:Dox EncodeToString](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/Utf8Helper.cs:1606>) 的单块路径调用 `Encoding.UTF8.GetString`；多块路径先计算 UTF-16 长度，再 `string.Create` 一次创建最终字符串，并用同一个有状态 Decoder 解码多个缓冲区，避免先合并为完整 byte[]。

`CountUtf16CharsFromValidUtf8` 利用已知合法 UTF-8 的结构计数：ASCII 加一、continuation byte 不加、2/3 字节首字节加一、4 字节首字节加二。跨块计数不需要解码状态，跨块解码需要；不能分别对每一块 `GetString`，否则跨块 emoji 会损坏。

它当前是标量计数，不能假定比 .NET 的计数路径更快。只适用于已经保证合法的数据，不能用作外部 UTF-8 校验器。

[LuminPack ReadUtf8StringWithLength](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/LuminPackReader.cs:811>) 已经利用 wire header 中的 UTF-16 长度直接 `string.Create`，且有 ASCII SIMD 解码；这部分比“先计数再分配”更直接，不需要为了模仿 RE:Dox 再扫描一遍。

[SerializeJson 返回 string](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPackGenerator/Core/LuminPackSerializerGenerator.cs:766>) 仍然把最终 UTF-8 内容交给 `GetString`。可实验 writer 同时累计最终 UTF-16 长度，在保证内部输出合法的情况下用 `string.Create` 直接解码；也可比较专用 UTF-16 string 输出路径。累计长度会给每次写入增加成本，两种输出编码的缓冲大小与数字格式化成本也不同，必须比较整体吞吐与分配，不能仅凭少一次扫描判断胜负。

## 3. 输出缓冲和多段输入：减少整体复制

[RE:Dox DocumentWriter](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/DocumentWriter.cs:546>) 保留多个 pooled chunk，提交流输出时按块写入，增长时无需搬移此前所有内容。它提交到外部 `IBufferWriter` 时仍会复制 chunk；这不是完全零复制。

LuminPack 有两个具体的研究目标：

- [生成的 SerializeJson<T>(IBufferWriter<byte>)](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPackGenerator/Core/LuminPackSerializerGenerator.cs:734>)：先完整写进自己的 `LuminBufferWriter`，再申请完整目标区并 `CopyTo`。可增加专用顺序 JSON writer，直接向外部 `IBufferWriter` 获取/提交片段，或先尝试分块提交以避免外部一次大容量申请。直接写入才能去掉内部整体缓冲；仅分块复制不会消除内部缓冲和复制量。
- [ReadOnlySequence<byte> 构造 reader](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/LuminPackReader.cs:86>)：单段直接引用，多段 `ToArray()`，有完整分配和复制。可保留现有连续 reader，并新增独立 sequence reader：数据在当前段内走原有快路径，只有跨段字段才拼接必要字节。

不要把二进制 writer 直接改成任意分块 writer。二进制代码有按 index 定位、预留和写回头部、连续 unmanaged 读写等要求，适合先用独立入口优化，避免改变已有 wire format。分段读取还必须处理跨段长度头、字符串、unmanaged 数据和截断输入。

## 4. 属性名：编译期已知的信息要用起来

[RE:Dox Utf8Symbol](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/Serialization/Utf8Symbol.cs:10>) 缓存 UTF-8 字节和 EscapeMask；writer 用它决定是否跳过重复转义处理。其类转换器把名称预排序，用 [FindProperty](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/Serialization/Converters/ClassTypeConverter.cs:728>) 二分查找，每次最终仍比较实际字节。

LuminPack 普通对象写入已经生成包含引号、冒号、逗号的 UTF-8/UTF-16 前缀，并直接 `WriteRaw`，这一点无需退回运行时 Utf8Symbol。循环引用等分支还调用 `WritePropertyName`，可以统一使用编译期转义后的前缀，进一步减少扫描。

[LuminPack JSON 读取生成器](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPackGenerator/Core/Json/LuminPackJsonCodeGenerator.cs:367>) 对每个名称计算 XxHash3 再 switch，而且所检查的普通对象分派路径没有实际名称复核。可对小模型生成“长度 + 若干字节判别 + SequenceEqual”的分派，大模型保留 hash，并在命中后验证实际名称。收益需测试不同字段数量、名称长度、未知属性和属性乱序；不要假设二分查找一定比生成代码快。

实际字节复核也是正确性要求：只比较 64 位 hash 不能保证名称相等；编译期已知字段之间的 hash 冲突也要生成可处理的代码。这里没有构造碰撞输入，也没有修改该分派。

## 5. 跳过未知字段：扫描与物化分开

[SkipStringUtf8](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/Json/LuminPackJsonReader.cs:575>) 直接调用 `ReadStringUtf8()`；带转义的未知字段值因此仍会执行反转义，首次或更大字符串会分配 scratch array。UTF-16 路径类似。

借鉴 RE:Dox 对结构和物化分层的思路，建立只扫描、不产生字符串的 skip 路径。仍需要验证控制字符、合法 escape、`\uXXXX`、代理项配对和结束引号，并保持现有错误语义。适合包含大量未知字段的版本兼容 JSON。它与替换 UTF-8 转码内核无关，但可能直接减少内存分配。

## 6. 并行反序列化：只为有边界的大数组考虑

[RE:Dox ParallelSzArrayConverter](<C:/Users/28027/Downloads/REDox-main/REDox-main/src/REDox/Serialization/Converters/ArrayConverter.cs:573>) 利用 token 的 count/link 和元素 token id，为每个线程创建独立 DataReader。创建该转换器还要求未启用 PreserveReferencesHandling；默认选项要求至少 4 个元素、约 1000 个 token，最大并行度为 4。并行也会引入 token-id 数组租赁、调度及对象分配竞争。

LuminPack 直接 reader 的当前位置与 optional state 不能交给多个线程共同推进。可研究不共享引用、无副作用 callback、独立可解码元素的大型数组，先建立元素边界，再给每个任务独立 reader。小消息的边界扫描和任务调度很可能抵消收益。启用引用保持或跨元素依赖时不能套用。不要为了小消息性能给所有读取强制增加 token DOM；RE:Dox 的结构索引还服务于编辑、格式转换和随机读取，是不同的产品取舍。

## 7. 不建议直接照搬的部分

- `Utf8Helper.Compare` 的 8 字节比较和 ToUpper 是定制 ASCII 忽略大小写排序，可支撑它自己的查找表；其先比较长度、再按整数块排序的结果不等于普通字典序，也不是 Unicode OrdinalIgnoreCase。LuminPack 默认大小写敏感时没有必要付出这些逻辑成本。
- `Utf8CharsToUtf32Char` 等轻量 helper 不提供完整边界/Unicode 合法性校验；内部已验证数据的前提必须保留，不能当作公开严格解码器。
- [现有 LuminUtf8SimdV3 实验](<C:/Users/28027/Desktop/C#/LuminPack/benchmarks/LuminPackBenchmark/Utf8SimdBenchmark.cs:261>) 在 benchmark 额外保留 32-byte padding，允许 SIMD over-store。它不能直接替换接受任意精确 Span 的 StringSerializer，且必须维持 DestinationTooSmall、NeedMoreData、InvalidData、已消费长度和严格/替换模式等契约。中文专用内核与 portable fallback 可以另立实验。
- netstandard2.1 / Unity 没有此处 .NET 8+ 的 SearchValues API；保留单独 fallback，测试 IL2CPP/Mono。此次实验只验证 .NET 10 x64 JIT，没有验证 Unity、ARM64 或 NativeAOT。
- 旧的 [LuminPackMarshal.GetBytes](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Utility/LuminPackMarshal.cs:620>) 在项目内部搜索只找到定义，目前不是 writer 的热路径。它的无效代理项分支值得另做正确性检查，不应误认为生产代码正在使用这一实现。

## 建议实施次序与验收

| 次序 | 改动 | 验收重点 |
|---|---|---|
| 1 | JSON 按片段编码，并处理密集转义 | 短/长、ASCII/中文/2-byte 文本/emoji、转义密度、无效代理项；字节一致性、扩容及精确 Span |
| 2 | skip 不物化；属性分派减少名称处理并复核名称 | 未知属性、乱序、转义属性名、畸形 JSON；同等错误语义和分配 |
| 3 | string 返回路径、外部 IBufferWriter 和 sequence 独立入口 | 端到端吞吐、峰值缓冲、复制字节数、跨段 emoji/头部、异常清理 |
| 4 | CJK 专用 SIMD 与大数组并行 | JIT/AOT、x64/ARM64/Unity、严格模式和边界；单线程基线与调度阈值 |

正式基准应包括现有 CharacterSaveData 和真实中文模型，分别比较返回 string、输出 UTF-8 bytes、复用 buffer 三种 API；匹配输出字段、默认值/空值省略、数字格式、引用与并行设置。此次微实验不使用整库间不同配置的结果推断优劣，也没有重新运行完整单元测试，因为生产代码没有改动。

## 复现

先在仓库根目录构建当前 runtime：

```powershell
dotnet build src/LuminPack/LuminPack.csproj -c Release -f net10.0 --no-restore -p:GeneratePackageOnBuild=false -p:PublishAot=false
```

然后进入 `experiments/RedoxUtf8Study`（SDK 10.0.301 由本目录 global.json 固定）：

```powershell
$env:DOTNET_TieredCompilation = '0'
dotnet run --project RedoxUtf8Study.csproj -c Release --no-launch-profile
dotnet bin/Release/net10.0/RedoxUtf8Study.dll
```

原型无第三方依赖，引用刚构建的 runtime DLL；不会加载下载目录中的 RE:Dox 代码。运行时构建成功，存在现有编译警告。原型构建成功，两次进程复跑的正确性检查均通过。
