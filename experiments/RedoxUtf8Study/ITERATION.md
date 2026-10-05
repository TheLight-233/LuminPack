# 第一轮生产迭代：JSON 字符串与跳过操作

本轮实现两个改动，保持公开 API 和 JSON 输出字节形式。旧版比较基线为 `807f6e83d2454eecccde53a37bc07af37c4f6966` 中的 writer / reader；其余运行时源码与当前工作区共享，以隔离本轮改动。

## 实现

[LuminPackJsonWriter.cs](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/Json/LuminPackJsonWriter.cs>)：

- .NET 8+ 缓存 `SearchValues<char>` / `SearchValues<byte>`，普通字符串和 UTF-8 属性名按转义位置分段处理。
- 长普通片段直接批量转码或复制；16 个字符以内的普通 ASCII 一次扫描并写入。
- 优先使用实际剩余空间；只有转码空间不足才计算精确字节数并扩容。精确容量的调用方 Span 不需要预留最大编码长度。
- 连续转义字符直接写入。密集前缀中的至少三个连续转义字符保留原标量处理，以最多 64 个 UTF-16 单元的块推进；完整代理对不会在块边界断开，后面的普通尾部重新进入批量编码。
- 保留 `\b` / `\f` 的原有 `\u0008` / `\u000c` 输出形式，以及无效 UTF-16 的替换行为。netstandard2.1 的编码实现保持原路径。

[LuminPackJsonReader.cs](<C:/Users/28027/Desktop/C#/LuminPack/src/LuminPack/Core/Json/LuminPackJsonReader.cs>)：

- 读取和跳过复用同一字符串边界扫描器。
- 跳过带转义的值时只验证 escape、十六进制和代理项配对，不生成 UTF-8 / UTF-16 scratch array。
- 保持对控制字符、无效 escape、截断 Unicode escape、孤立代理项和未结束字符串的拒绝行为。

## 验证

五个运行时目标框架均构建成功：netstandard2.1、net8.0、net9.0、net10.0、net11.0。构建存在项目已有警告，零错误。

完整 xUnit 测试通过：.NET 9 为 47 项，.NET 10 为 47 项，.NET 11 为 48 项。各逻辑组内部包含多条回归用例，本轮新增：

- 500 组随机 UTF-16 和边界字符串的输出字节比较、精确容量及少一字节目标、前后 canary。
- UTF-8 / UTF-16 属性名、所有控制字符、emoji、孤立代理项、64 单元块边界的完整代理对。
- 超过初始容量的大中文字符串、扩容后的后续值与逗号状态。
- 嵌套未知字段中畸形 escape 的拒绝，以及跳过后读取已知字段。
- 重复跳过大段转义文本，UTF-8 / UTF-16 均验证 0 B 分配；输入、状态和搜索表预热在测量之外。

旧、新真实 writer 的独立实验另外通过 2,012 个边界 / 随机输入及 30 组计时输入的字节一致性、JSON 解析、canary 和精确容量检查。

## 性能与边界

测试环境为本机 i7-13650HX、Windows x64、.NET 10.0.9。以下只描述本机样本，没有验证 ARM64、Unity/IL2CPP 或 NativeAOT 执行性能。

正常长文本的可行性计时仍显示明显收益：4096 单元中文约 11 倍，中英混合约 9–10 倍，emoji 混合约 7.6 倍。它们是字符串写入微实验，不是整库倍率。两边计时阶段均为 0 B/op。

Stopwatch 对密集转义的复跑波动很大，因此该场景另用同进程 BenchmarkDotNet 验证。最新结果中，8 单元为 42.51 → 44.71 ns，4096 单元为 18.91 → 19.81 μs，约有 **5% 分派开销**，没有把它宣传成无回退优化。此前一次同配置运行在相近水平；最终数据见 [密集转义正式基准](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/dense-benchmark.md>)。这项开销需要后续继续压缩。

实际 CharacterSaveData 模型分别比较复用 buffer 的序列化和完整反序列化。固定 affinity=3（逻辑核心 0/1）、禁用 tiered compilation、5 次预热、12 次迭代、250 ms/迭代。单核心设置会触发现有基准中 ReDox 最小并行度限制，故使用两个逻辑核心；测量的 LuminPack 方法仍为顺序路径。

| 实际模型路径 | 旧版 | 最终版本 | 结论 |
|---|---:|---:|---|
| JSON 序列化至复用 buffer | 31.65 μs | 29.14 μs | 耗时减少约 7.9%，0 B/op |
| JSON 完整反序列化 | 94.05 μs | 93.74 μs | 基本相近，约 62,441 B/op |

端到端原始数据：[旧版](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/end-to-end-baseline.md>)、[最终版本](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/end-to-end-candidate.md>)。普通已知字段样本没有体现 skip 的分配收益；skip 的收益由新增未知字段测试单独验证。

## 复现

在仓库根目录执行：

```powershell
# 旧、新真实 writer：同进程，CPU 0，分别校准约 50 ms 的计时批次。
# 每组 15,000 次预热，7 轮交替取中位数，两次独立进程复跑。
./experiments/RedoxUtf8Study/CompareRuntime.ps1

# 密集转义：同进程 BenchmarkDotNet，使用项目已有的 0.15.6 版本。
./experiments/RedoxUtf8Study/CompareRuntime.ps1 -DenseBenchmark
```

脚本只在忽略的 `.tmp` 目录生成旧源码快照、项目和 DLL，读取指定 Git 基线，不修改生产源码或已有 benchmark。默认比较无第三方依赖；`-DenseBenchmark` 使用 [保存的基准源码](<C:/Users/28027/Desktop/C#/LuminPack/experiments/RedoxUtf8Study/DenseStringBenchmark.cs.txt>)。脚本会恢复调用前的工作目录和 tiering 环境变量。

运行脚本会重写相应结果。初始可行性研究的 `results-*.md` 保留原型数据；`runtime-comparison*.md` 对应实际运行时比较。构建与完整测试日志保留于 `.tmp/redox-comparison`。

## 后续迭代顺序

先压缩密集转义的分派成本，再研究属性名分派的实际名称复核与小模型专用分派。外部 IBufferWriter 直接输出和独立 sequence reader 单独迭代，避免一次改变二进制连续内存、写回头部和引用状态契约。并行解码与 CJK SIMD 仍需独立的负载、边界和平台实验。
