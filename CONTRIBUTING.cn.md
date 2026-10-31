# 参与贡献

[English](CONTRIBUTING.md)

修改运行时或生成器代码前，请先阅读[机制契约](docs/mechanism-contract.cn.md)和[架构](docs/architecture.cn.md)。

## 构建

解决方案包含 `net11.0` 目标，需要 .NET 11 预览版 SDK。`global.json` 允许从 .NET 10 前滚并允许预览版 SDK。

```text
dotnet build LuminPack.sln -c Release
```

## 检查

运行覆盖本次改动的检查，而不是每次都跑全部测试。运行时测试规模很大，建议只选一个框架并加 `--filter`。

```text
# 源码规范：XML 文档、英文文本、异常路由、原始复制路由
dotnet run --project tools/LuminPack.RepositoryChecks/LuminPack.RepositoryChecks.csproj -c Release -- .

# 生成代码契约（SDK Roslyn）；加 --verify-<area> 只跑某一类
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release

# Roslyn 4.3 上的生成器流水线，以及增量失效
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-incremental

# 运行时测试：单个框架、针对性过滤
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~ReaderFixedBoundsPolicyTest"

# 各测试区只编译自己的源码，规模更小
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Safety
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Simd
dotnet test tests/LuminPack.Tests/LuminPack.Tests.csproj -c Release -f net10.0 -p:LuminPackTestArea=Hooks

# 用仓库中的夹具检查数据格式兼容性
dotnet run --project tests/LuminPack.Tests.Aot/LuminPack.Tests.Aot.csproj -c Release -- --wire verify tests/LuminPack.Tests.Aot/Fixtures

# 平台适配（AspNetCore、Avalonia、Drawing、Maui、MonoGame、SkiaSharp、Wpf）
dotnet run --project tests/LuminPack.Tests.Platforms -c Release -p:LuminPackPlatform=Drawing
```

- 修改 Reader 边界检查时：在 Debug 和 Release 下运行 `ReaderFixedBoundsPolicyTest` 和 `ReaderPreaccessBoundsTest`，并运行生成代码契约中的 `--verify-codegen-performance`。
- 兼容性重构：先用原版本写出夹具（`--wire write .tmp/wire-baseline`），再用新版本校验。临时输出放在 `.tmp`。
- 各测试区和平台选择分别构建到独立的 `obj/<area>`、`bin/<area>` 目录。
- 新的测试用例加入现有的测试套件和矩阵，不要为每个优化新建一个项目或测试类。

## 持续集成

- `ci.yml`：仓库检查、构建、生成代码与生成器契约、增量验证、运行时测试、Hook 测试区、数据格式夹具，以及从新打包的 NuGet 包安装的测试。
- `runtime-compatibility.yml`：在 Windows、Linux、macOS（x64 和 ARM64）上运行裁剪版和 NativeAOT 程序，标量/SSSE3/AVX2 回退以及 x86。AVX-512 硬件测试在推送及同仓库拉取请求时自动运行，手动运行时也默认开启；手动运行可取消勾选 `run_avx512`，或勾选 `avx512_only` 只运行此硬件检查。默认并行探测四台 `ubuntu-latest` 运行器的真实 AVX-512F/BW 支持；所有支持的运行器都会执行测试，任何测试失败或没有运行器支持时硬件检查失败。GitHub 不保证一定能分配到支持的运行器。可设置仓库变量 `LUMINPACK_AVX512_RUNNER`，指定支持 AVX-512 的 Linux x64 runner 标签（如 `avx512`），固定使用专用运行器。来自 fork 的拉取请求不执行此任务，即使选择了自托管 runner。按 CPU 厂商和 ARM32 的运行仍需手动触发，并需要带对应标签的自托管 runner。
- 配置 `UNITY_EMAIL`、`UNITY_PASSWORD` 和 `UNITY_SERIAL` 后，所有分支的推送和同仓库拉取请求会在 Linux 托管运行器上用 Unity 2022.3.51f1 和 6000.0.43f1 依次运行 Editor 测试、IL2CPP smoke Player、Unity 对象和 Burst Editor 合约测试，并在无头 Chrome 中运行 WebGL IL2CPP smoke Player，共用一个许可证。Personal 序列号可按 GameCI 激活文档从本机 `.ulf` 提取，现有 `UNITY_LICENSE` 可保留；也可以使用有效的 `UNITY_LICENSE` 代替序列号。未配置密钥时许可证检查会失败。各次运行共用一个许可证，最多排队 100 次，不会取消先前的排队运行。Fork 拉取请求跳过需要许可证的任务。手动运行的 Windows 和 macOS 原生 Player 检查仍需要预先激活许可证的自托管运行器；Linux 测试不能验证这些平台。
- `test-areas.yml` 在 Windows、Linux 和 macOS 上以 Debug、Release 运行 Safety 测试区；三个系统均执行 AspNetCore、Avalonia、Maui、MonoGame、SkiaSharp 适配契约，Windows 额外执行 Drawing 和 Wpf。适配模块使用已有 Core 二进制，因此先构建 Core。
- 推送到 `main`、`refactor` 或创建拉取请求均会触发托管检查。Unity 执行需要配置许可证密钥；`.github/scripts` 下的辅助脚本随工作流纳入版本控制。
- 基准测试只能通过手动的 `run_benchmarks` 输入触发，明确限定为现有四类：`SimpleBenchmark`、`SerializeBenchmark`、`JsonBenchmark`、`PolymorphismBenchmark`，不作为正确性门禁。
- 基准测试矩阵使用 Linux x64、macOS Intel x64 和 Linux ARM64 原生主机。计时前校验实际进程与操作系统架构，以及 Intel 主机的 CPUID 厂商；各主机分别上传结果和环境信息。应在同一主机内比较各库的比值，跨主机的操作系统和 CPU 均有差异。
- 手动输入 `benchmark_jit` 默认为 `both`，分别运行 `TieredPGO`（启用分层编译和动态 PGO）及 `FullOpts`（两者均关闭）。FullOpts 是用于排查 JIT 稳定性的对照，不能替代生产环境 PGO 结果。每组使用三个进程、8–30 次自适应预热和 15 次以一秒为目标的正式测量，保留离群值。固定随机种子与时间戳保证各进程的输入字节一致；计时前用 `--verify-fixtures` 验证新实例及重复初始化。
- 完整 JSON 和测量 CSV 产物保留每个进程的数据。`process-stability.md` 与任务摘要报告进程均值、变异系数、最大值/最小值、分配量，以及相同操作、相同 JIT 配置下的 LuminPack/参照库比值。进程差异超过 10% 时发出警告；宣称提升前应检查分布与原生代码。各库的独立进程不能按进程编号当作配对样本。
- BenchmarkDotNet 的多态反汇编在 Windows/Linux 上运行；当前版本不支持 macOS 上的 CoreCLR 反汇编。独立的非计时 JIT 探针在 Linux 基准主机上采集 LuminPack 分区内核、Nino 多态读写的优化后汇编及生成方法信息。LuminPack 分区内核使用激进优化编译时可能显示 Tier0-FullOpts；这不是 Tier1 PGO 结果。Intel macOS 主机运行全部四类耗时和分配测试，但不运行这些探针。基准验证、执行或必需探针失败会使任务失败。
- CI 按事件区分并发组，推送不会取消手动启动的基准测试。注册表子进程测试启动测试程序集旁的 apphost，以保持与测试进程一致的架构，包括自包含 x86。

## 基准测试

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --jit-mode both --filter *PolymorphismBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
python .github/scripts/analyze_benchmarks.py .tmp/benchmark-artifacts
python benchmarks/make-charts.py .tmp/benchmark-artifacts .tmp/benchmark-charts --job TieredPGO
```

用 LuminPack 耗时除以参照库耗时来评判结果，数据必须来自同一台机器的同一次运行；见契约第 5 节。
二进制表格在每种操作内以 MemoryPack 为基线，JSON 表格以 System.Text.Json 为基线。报告含有多个 JIT 配置时，生成图表必须用 `--job` 选择，不能合并不同配置的数据行。

## 源码规范

- 遵循 `.editorconfig`：4 空格缩进，UTF-8。
- 代码、注释、XML 文档、诊断、测试和维护文档使用英文。README 和 `*.cn.md` 中文版是例外。测试中的多语言字符串用 Unicode 转义书写。
- 维护的源码中每个方法、构造函数、运算符和转换都要有 XML 文档，包括摘要、参数、类型参数和返回值。注释写在特性之前。生成代码仅为可直接调用的 `LuminPackSerializer` API 和门面类型生成文档；内部根方法、转发方法及辅助声明省略 XML，并屏蔽 CS1591。
- emitter 分文件存放；大型生成器按职责拆分为 partial 文件，保持类型和成员名称不变。
- 通过 `LuminPackExceptionHelper` 抛出异常，保留异常类型、参数名和清理逻辑。需要保留原始异常身份时使用 `Rethrow`。
- 修改名称、成员顺序、枚举值、null 标记、字符串模式、帧格式或存储视图都可能破坏已持久化的数据。本仓库中没有调用点的公共成员仍可能被使用者或生成代码使用。
- 修改移植的格式化代码时，保留第三方声明（`docs/xjb-LICENSE.txt`、`docs/xjb-NOTICE.md`）。

## 文档

- README 是项目封面：介绍、安装、使用和链接。README 和 `docs` 中不放发布说明、版本历史、迁移通知或实验报告。
- 每个 README、AGENTS、CONTRIBUTING 以及 `docs` 页面都有英文 `name.md` 和中文 `name.cn.md`，两者一起更新。
- 公共用法改变时更新 `skills/luminpack`。

## 提交规范

- 每条提交标题使用 `type(scope): description`。类型和组件范围用小写，描述用简洁的英文祈使句，末尾不加句号。
- 类型：`feat`（新功能）、`fix`（修复）、`perf`（性能优化）、`refactor`（结构调整）、`test`（测试）、`docs`（文档）、`ci`（工作流）、`build`（构建工具）、`chore`（维护）。性能前缀是 `perf`，不能写成 `pref`。
- 范围选择受影响的组件，例如 `core`、`generator`、`benchmarks`、`unity`、`burst` 或 `repo`。基准夹具改动按目的分类：`fix(benchmarks)` 修正夹具，`ci(benchmarks)` 修改其 CI 执行或采集方式。
- 独立改动分别提交。正文可说明原因和相关验证。破坏性变更在范围后加 `!`，并通过 `BREAKING CHANGE:` 页脚说明。
- 示例：`perf(core): reduce fixed record cursor writes`、`fix(generator): reserve large union payloads`、`ci(benchmarks): capture polymorphism JIT assembly on all hosts`。
- 完成一个功能或修复并进行相应验证后，默认按本规范提交本任务的改动，无需逐次询问提交权限。排除无关或未完成的改动。提交后询问是否推送；所有者已授权该次推送时直接执行。打标签和重写已发布历史仍需明确授权。重写获准的远端分支时，使用明确指定预期版本的 `--force-with-lease`，防止覆盖并发改动。
- 代表所有者创建的每个提交，author 与 committer 均使用 `TheLight-233 <2802799687@qq.com>`，不得添加 `Co-Authored-By` 或任何 AI 署名 trailer。环境会注入 `GIT_COMMITTER_NAME=trae-solo-agent` 和 `GIT_COMMITTER_EMAIL=solo-agent@trae.ai`；每次 `git commit` 都要在同一 shell 内覆盖这两个变量，并用 `git log -1 --format='%an <%ae> | %cn <%ce>'` 校验署名。
