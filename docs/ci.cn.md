# 持续集成

[English](ci.md) · [贡献指南](CONTRIBUTING.cn.md)

推送到 `main`、`refactor` 或创建拉取请求都会触发托管检查。`.github/scripts` 下的辅助脚本是工作流的一部分。CI 按事件区分并发组，推送不会取消手动启动的基准测试。

| 工作流 | 内容 |
| --- | --- |
| `ci.yml` | 仓库检查、构建、生成代码与生成器契约、增量验证、运行时测试、Hooks 测试区、数据格式夹具，以及从新打包的 NuGet 包安装的测试。手动运行时可加跑基准测试。 |
| `test-areas.yml` | 在 Windows、Linux、macOS 上以 Debug 和 Release 运行 Safety 测试区和平台适配契约。 |
| `runtime-compatibility.yml` | 裁剪与 NativeAOT 程序、跨语言 Runtime 互通、SIMD 回退路径、x86 以及 AVX-512 硬件测试。 |
| `unity.yml`、`unity-extensions.yml` | Unity Editor 测试、IL2CPP 与 WebGL Player、Unity 对象与 Burst 契约。 |
| `benchmark-matrix.yml` | 序列化矩阵的往返校验；手动运行时计时。 |
| `benchmark-matrix-paired.yml` | 手动在同一 runner 上以 FullOpts 和 TieredPGO 对照两个或三个版本的指定 Matrix 用例。 |
| `fuzz.yml` | 有预算上限的 SharpFuzz/libFuzzer 测试，见 [Fuzz 测试](fuzzing.cn.md)。 |
| `release.yml` | 构建候选包、比较 API，并通过配置的版本标签发布到 NuGet。见[发布](releasing.cn.md)。 |

## 测试与平台

- `test-areas.yml` 在三个系统上都运行 AspNetCore、Avalonia、Maui、MonoGame、SkiaSharp 适配契约，Windows 额外运行 Drawing 和 Wpf。适配模块使用已经构建好的 Core 二进制，所以先构建 Core。
- 注册表子进程测试启动测试程序集旁边的 apphost，保持与测试进程一致的架构，包括自包含 x86。
- `runtime-compatibility.yml` 在 Windows、Linux、macOS（x64 和 ARM64）上运行裁剪版与 NativeAOT 程序、标量/SSSE3/AVX2 回退以及 x86。按 CPU 厂商和 ARM32 的运行需要手动触发，并需要带对应标签的自托管 runner。
- Linux x64 和 ARM64 任务也会验证 Go 和 TypeScript 的 SIMD 与标量路径，并在 .NET 与 Java、Go、TypeScript 之间交换基准模型和字符串边界夹具。Java 的字符串测试启用 Vector API。

### AVX-512

AVX-512 硬件测试在推送和同仓库拉取请求时自动运行，手动运行时也默认开启。手动运行可取消勾选 `run_avx512` 跳过，或勾选 `avx512_only` 只跑这项检查。

默认并行探测四台 `ubuntu-latest` 运行器是否真正支持 AVX-512F/BW。所有支持的运行器都执行测试，任一候选任务失败或被取消时检查失败。四台都不支持时检查成功，并附带跳过覆盖的提示，不算硬件验证通过。GitHub 不保证一定能分到支持的机器。可以把仓库变量 `LUMINPACK_AVX512_RUNNER` 设为支持 AVX-512 的 Linux x64 runner 标签（例如 `avx512`），固定使用专用机器。来自 fork 的拉取请求不运行此任务，选择了自托管 runner 也一样。

### Unity

配置 `UNITY_EMAIL`、`UNITY_PASSWORD` 和 `UNITY_SERIAL` 用于在线激活。Personal 序列号可以按 GameCI 激活文档从本机 `.ulf` 中提取；也可以用有效的 `UNITY_LICENSE` 代替序列号。未配置密钥时许可证检查会失败。

Unity 2022.3.51f1 和 6000.0.43f1 依次运行 Editor 测试、IL2CPP smoke Player、Unity 对象与 Burst Editor 契约，以及在无头 Chrome 中运行的 WebGL IL2CPP smoke Player，共用一个许可证。多次运行会排队（最多 100 个），不会取消之前的运行。Fork 拉取请求跳过需要许可证的任务。手动的 Windows 和 macOS 原生 Player 检查需要预先激活许可证的自托管 runner；Linux 托管机器无法验证这些平台。

## 基准测试

手动触发 `ci.yml` 并开启 `run_benchmarks` 后，会为 BenchmarkDotNet 主套件、Nino 的 Workloads 和 AppData 套件以及[序列化矩阵](../benchmarks/LuminPack.MatrixBenchmark)计时。

| 输入 | 默认值 | 作用 |
| --- | --- | --- |
| `run_benchmarks` | 关闭 | 在三台主机上运行 BenchmarkDotNet 主套件，并为 Nino 对照和矩阵计时。 |
| `benchmark_jit` | `both` | 选择 BenchmarkDotNet 主套件的任务。`TieredPGO` 启用分层编译和动态 PGO，`FullOpts` 关闭两者。 |
| `benchmark_satori` | `only` | 针对 BenchmarkDotNet 主套件，`only` 只测 `eng/SatoriCompiler` 准备的 Satori GC 运行时，`both` 加测 stock 运行时，`off` 只测 stock 运行时。 |

### BenchmarkDotNet

- 任务在 Linux x64、macOS Intel x64 和 Linux ARM64 主机上运行 `SimpleBenchmark`、`SerializeBenchmark`、`JsonBenchmark` 和 `PolymorphismBenchmark`。计时前校验进程与系统架构，以及 Intel 主机的 CPUID 厂商。每台主机分别上传 `benchmark-artifacts-*` 结果和环境信息；各库的比值只在同一主机内比较。
- 每个任务使用三个进程、8–30 次自适应预热和 15 次以一秒为目标的正式测量，保留离群值。固定随机种子和时间戳让各进程的输入字节一致；计时前用 `--verify-fixtures` 检查新实例和重复初始化。
- 完整的 JSON 和测量 CSV 产物保留每个进程的数据。`process-stability.md` 和任务摘要列出进程均值、变异系数、最大值/最小值、分配量，以及同一操作、同一 JIT 任务内的 LuminPack/对照库比值。进程间差异超过 10% 会产生警告；在宣称更快之前先检查分布和原生代码。各库独立启动的进程不按进程编号配对。
- BenchmarkDotNet 的多态反汇编在 Windows 和 Linux 上运行；当前版本不支持 macOS 上的 CoreCLR 反汇编。Linux 主机上另有不计时的 JIT 探针，采集 LuminPack 和 Nino 多态读写的优化后汇编以及生成根方法的信息。校验、执行或必需探针失败都会让任务失败。

### Nino Workloads 和 AppData

`LuminPack.NinoBenchmark` 已加入解决方案。它移植了 [Nino 基准套件](https://github.com/JasonXuDeveloper/Nino/tree/185220ad24f07a9aeff53c21c4f30a0556c17e23/runtimes/csharp/benchmarks/Nino.Benchmark)的 12 个 Workloads 场景和 8 个 AppData 场景，用于对比 LuminPack 和 Nino。每项操作都以 Nino 为基准，报告分配量和耗时比值。常规 CI 在 Linux、Windows 和 macOS 上校验夹具。手动计时任务在 Linux x64 上使用 stock .NET 10、TieredPGO 和 Server GC，每项操作启动三个进程。`nino-benchmark` 产物包含逐次测量的 CSV 和完整 JSON 报告。

两个库把相同的数据写入各自预分配且重复使用的缓冲区。每次调用都写出独立文档。LuminPack 有状态的场景会同时重置游标以及版本描述表或引用状态。反序列化读取各自的格式，仅在相同的两个场景复用读取目标。结果反映各库默认格式，包括 Nino 自适应的 Latin-1/UTF-16 字符串和 LuminPack 的 UTF-16 字符串。`payload-sizes.csv` 列出每项操作的两份负载大小，可与耗时和分配量一起解读。夹具验证检查重复写出的字节是否符合预期，以及两个缓冲区是否保有初始容量。

```bash
dotnet run --project benchmarks/LuminPack.NinoBenchmark -c Release -- --verify
dotnet run --project benchmarks/LuminPack.NinoBenchmark -c Release -- --filter '*' --artifacts .tmp/nino-benchmark
```

### 序列化矩阵

`benchmark-matrix.yml` 在相关推送和拉取请求上做往返校验。手动触发该工作流，或在 `ci.yml` 中开启 `run_benchmarks`，会在同一台 runner 上使用 FullOpts 和 Server GC 为所有格式和场景计时。每个场景跑三个进程，每个进程九轮。FullOpts 关闭分层编译和动态 PGO。

最终校准采样次数前，矩阵会对同一个操作的各库打乱顺序、交错预热。每个库的每项操作至少调用 256 次，并累计达到 FullOpts 的 15 毫秒或 TieredPGO 的 300 毫秒实际运行时间。TieredPGO 还要求连续 50 毫秒未观测到进程范围的 JIT 活动，达到最低调用次数和运行时间后，预热可在总时长满两秒时结束。如果同一进程的前三轮和最后三轮中位数相差至少 20%，报告会标记该项，并保留所有样本。

`benchmark-matrix` 产物包含记录了预热数据的原始 JSONL、机器信息、`stability.csv` 和 `REPORT.md`。库之间的比值在同一次运行内比较。

手动触发 `benchmark-matrix-paired.yml` 时，指定基线版本、一个格式和场景以及准确的用例名，也可指定第三个对照版本。每种 JIT 模式在同一台主机上使用相同的夹具源码构建各版本，并轮换进程启动顺序。对于双方计时期间均未观察到全局 JIT 活动的每一轮，用 LuminPack 耗时除以同轮参考库耗时。先在每个进程内对这些比值取中位数，再对各进程的结果取中位数，得到 CSV 中的 `over_reference`。各版本的这个值除以基线的值即为 `ratio_over_stock`。CSV 同时记录进程间比值的波动、绝对耗时和分配量。

可选输入 `initial_warmup` 会生成 `post-validation-initial-warmup.csv`，单独对照每项操作在当前夹具验证之后、校准之前的前 256 次计时调用。夹具准备和验证可能已经调用序列化器，更早的操作和夹具也可能预热共享代码，因此这项指标只反映验证后的初始预热。

如何用这些产物生成图表，见[贡献指南](CONTRIBUTING.cn.md#基准测试)。
