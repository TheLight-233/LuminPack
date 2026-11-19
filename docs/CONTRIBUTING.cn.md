# 参与贡献

[English](../CONTRIBUTING.md)

感谢参与！欢迎提交 Issue 和 Pull Request。LuminPack 2.0 正在 `refactor` 分支上重构，请基于该分支开发。

修改运行时或生成器代码前，请先阅读[机制契约](mechanism-contract.cn.md)（所有改动都必须遵守的规则）和[架构](architecture.cn.md)。AI 编程助手遵循 [AGENTS.md](../AGENTS.md)，其中的规则与本文一致。

## 构建

解决方案包含 `net11.0` 目标。请使用 `global.json` 指定的 .NET SDK 11.0.100，或 .NET 11.0 后续的功能版本。

```text
dotnet build LuminPack.sln -c Release
```

在 x64 或 Arm64 的 Windows、Linux 和 macOS 上，用 .NET 11 SDK 构建本仓库时，MSBuild 默认在隔离的 Satori GC 运行时中启动 SDK 的 C# 编译器。还原会下载固定版本的实验性运行时包。构建宿主、IDE 的设计时编译和应用程序继续使用各自的常规运行时。若要在某次构建中使用 SDK 编译器的常规运行时，请传入 `-p:LuminPackUseSatori=false`。

## 检查

运行覆盖本次改动的检查，而不是每次都跑全部测试。运行时测试规模大、编译慢，建议只选一个框架并加 `--filter`。

```text
# 源码规范：XML 文档、英文文本、异常路由、原始复制路由
dotnet run --project tools/LuminPack.RepositoryChecks/LuminPack.RepositoryChecks.csproj -c Release -- .

# 生成代码契约（SDK Roslyn）；加 --verify-<area> 只跑某一类
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release

# Roslyn 4.3 上的生成器流水线，以及增量失效
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-incremental
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-source-references

# 静态入口后端，包括真实 Roslyn 4.3 和可移植运行时
dotnet run --project tests/LuminPack.Tests.Codegen/LuminPack.Tests.Codegen.csproj -c Release -- --verify-interceptors
dotnet build src/LuminPack.Core/LuminPack.Core.csproj -c Release -f netstandard2.1 -p:CopyLocalLockFileAssemblies=true
dotnet run --project tests/LuminPack.Tests.Generator/LuminPack.Tests.Generator.csproj -c Release -- --verify-il-rewriting

# IDE 代码修复：真实诊断、跨文档修改和 Fix All
dotnet run --project tests/LuminPack.Tests.CodeFixes/LuminPack.Tests.CodeFixes.csproj -c Release

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

# 发布候选：打包、API 比较和安装检查（不会发布）
pwsh -File .github/scripts/release.ps1 -ApiBaseRef HEAD

# 确定性的畸形输入冒烟测试
pwsh -File .github/scripts/fuzz.ps1
```

- 修改 Reader 边界检查时：在 Debug 和 Release 下运行 `ReaderFixedBoundsPolicyTest` 和 `ReaderPreaccessBoundsTest`，并运行生成代码契约中的 `--verify-codegen-performance`。
- 兼容性重构：先用原版本写出夹具（`--wire write .tmp/wire-baseline`），再用新版本校验。临时输出放在 `.tmp`。
- 各测试区和平台选择分别构建到独立的 `obj/<area>`、`bin/<area>` 目录。
- 运行时 profile 矩阵使用测试专用的 `LuminPackUnitTest.TestProfileSerializer` 适配器，它与生成的 `LuminPack.LuminPackSerializer` 分开定义；手写同名 partial 声明可能让 IDE 在生成成员缺失时仍然显示正常。普通序列化调用使用正式 API。
- 新的测试用例加入现有的测试套件和矩阵，不要为每个优化新建一个项目或测试类。

各工作流运行哪些内容，以及 Unity、AVX-512 和基准测试任务如何配置，见[持续集成](ci.cn.md)。

## 基准测试

性能用 LuminPack 耗时除以对照库耗时来评判，数据必须来自同一台机器的同一次运行，并包含多次采样、离散度和分配量（见[契约](mechanism-contract.cn.md)第 5 节）。公开发布的数字只来自 CI 运行。

[BenchmarkDotNet 套件](../benchmarks/LuminPack.Benchmark)在二进制上对比 Nino、MemoryPack 和 MessagePack，在 JSON 上对比 System.Text.Json、Newtonsoft.Json 和 RE:Dox。

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --jit-mode both --filter *PolymorphismBenchmark*
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
```

`LUMINPACK_BENCHMARK_SATORI` 选择被测运行时：`1` 或 `only` 只测 `eng/SatoriCompiler` 准备的 Satori GC 运行时，`both` 在 stock 任务旁增加 `TieredPGO-Satori` 和 `FullOpts-Satori`，`off` 保持 stock。宿主仍使用已安装的 SDK；被测进程通过 apphost 加载私有的 .NET 11 框架，若 Satori GC 未生效则本次运行失败。`--verify-runtime` 输出当前进程的运行时、服务器 GC 和 Satori GC 状态。

独立的[序列化矩阵](../benchmarks/LuminPack.MatrixBenchmark)覆盖框架类型、模型图和 51 类集合。二进制对照 MemoryPack 和 Nino（包括 Nino 官方的不可变集合格式化器），JSON 对照使用源生成元数据的 System.Text.Json。Write 返回新的字节数组或字符串，Fresh 读入空目标，Reuse 读入已有目标；System.Text.Json 没有根对象 Reuse 操作。报告列出不支持和未通过往返校验的操作，并按对照库分别计算同一进程内有效配对的比值。本地校验某个场景时，传入格式、场景、轮数和 JSONL 输出路径；轮数为零时只做校验。

```bash
dotnet run --project benchmarks/LuminPack.MatrixBenchmark -c Release -p:LuminPackUseSatori=false -- binary small 0 matrix.jsonl
```

### README 图表

README 中的图表来自开启 `run_benchmarks` 的一次手动 `ci.yml` 运行。把产物下载到 `.tmp` 后执行：

```text
python .github/scripts/analyze_benchmarks.py .tmp/benchmark-artifacts
python benchmarks/make-charts.py .tmp/benchmark-artifacts/benchmark-artifacts-linux-x64 docs/assets/benchmarks --job TieredPGO-Satori
python benchmarks/LuminPack.MatrixBenchmark/chart.py .tmp/benchmark-artifacts/benchmark-matrix docs/assets/benchmarks
```

`make-charts.py` 只画一台主机、一个 JIT 任务；报告含多个任务时必须用 `--job` 选择，不会合并不同任务的数据。`chart.py` 需要计时过的矩阵产物（只做校验的运行没有样本）。同时更新两个 README 版本中的测试环境表。

## 源码规范

- 遵循 `.editorconfig`：4 空格缩进，UTF-8。
- 代码、注释、XML 文档、诊断、测试和维护文档使用英文。README 和 `*.cn.md` 中文版是例外。测试中的多语言字符串用 Unicode 转义书写。
- 维护的源码中每个方法、构造函数、运算符和转换都要有 XML 文档，包括摘要、参数、类型参数和返回值。注释写在特性之前。生成代码仅为可直接调用的 `LuminPackSerializer` API 和门面类型生成文档；内部根方法、转发方法及辅助声明省略 XML，并屏蔽 CS1591。
- emitter 分文件存放；大型生成器按职责拆分为 partial 文件，保持类型和成员名称不变。
- 生成器改动只进入 `src/LuminPack.Generator.Next`。旧版生成器（`src/LuminPack.Generator`）已冻结，不再维护：不要修改它，不要把 Next 的改动同步过去，也不必维持它的对照测试。
- `src` 下每行只写一条语句，不使用含连续两个下划线的标识符；`tools/LuminPack.RepositoryChecks` 会检查这两点。ChaCha20 和 Poly1305 的轮函数各保持一行，公开成员 `MethodTable.__PInvoke` 保留原名。
- 通过 `LuminPackExceptionHelper` 抛出异常，保留异常类型、参数名和清理逻辑。需要保留原始异常身份时使用 `Rethrow`。
- 修改名称、成员顺序、枚举值、null 标记、字符串模式、帧格式或存储视图都可能破坏已持久化的数据。本仓库中没有调用点的公共成员仍可能被使用者或生成代码使用。
- 修改移植的格式化代码时，保留第三方声明（`docs/xjb-LICENSE.txt`、`docs/xjb-NOTICE.md`）。

### 非托管结构体布局

- `Tagged` 是默认模式。存在多个布局模型的连续集合块写入经验证的本机布局标签和原始字节。同布局读取端整块复制，异布局读取端在条件满足时用 SIMD 转换，并保留标量回退。本机布局未被建模时，写入端转换成规范模型。单值省略标签，多布局值按 Packed 字段顺序编码，单布局 Tagged 值直接复制原生字节。
- `Packed` 按存储声明顺序写入可跨平台使用的无填充字节。它递归影响外层存储、框架键值对和值元组。显式 `Native` 声明形成存储边界。只有证明原生顺序与宽度吻合时才整块复制。
- `Native` 保留包括填充在内的完整本机存储，适用于两端布局相同的场景。
- 类型有布局声明时遵守该声明。无法在类型上声明选项的值使用消费项目的程序集选项。保持标签含义稳定，验证 wire 夹具和双向布局互读。参见[机制契约](mechanism-contract.cn.md#表示)。

## 文档

- README 是项目封面：介绍、安装、使用和链接。README 和 `docs` 中不放发布说明、版本历史、迁移通知或实验报告。
- 每个 README、CONTRIBUTING 以及 `docs` 页面都有英文 `name.md` 和中文 `name.cn.md`，两者一起更新。中文版统一放在 `docs` 下：根目录页面译文直接放在 `docs`，模块 README 译文放在 `docs/modules/<模块名>.cn.md`，功能指南译文与英文版放在一起。英文文档保留原有位置。`AGENTS.md` 只有英文版。
- 公共用法改变时更新 `skills/luminpack`。

### 贡献者参考

| 页面 | 内容 |
| --- | --- |
| [机制契约](mechanism-contract.cn.md) | 运行时、生成器和生成代码必须遵守的规则 |
| [架构](architecture.cn.md) | 项目划分、分层，以及一次操作如何生成 |
| [Next 生成器](generator-next.cn.md) | 基于模型的生成器、输出布局和包选择 |
| [Next 迁移状态](next-generator-prototype.cn.md) | 已验证的能力和尚未迁移的旧版功能 |
| [Next 格式化器契约](next-formatters.cn.md) | 格式化器模型和元素契约 |
| [格式化器移植](generator-formatter-porting.cn.md) | 注册和移植格式化器族 |
| [JSON 属性名查找](json-name-lookup.cn.md) | 生成的属性名表和 XXH3 分组 |
| [池化](pooling.cn.md) | 复用缓存与内存保留 |
| [Burst 钩子](burst-hooks.cn.md) | `netstandard2.1` 原生内核表 |
| [Fuzz 测试](fuzzing.cn.md) | Fuzz 测试框架与测试任务 |
| [发布](releasing.cn.md) | 发布候选、API 治理和 SourceLink |
| [持续集成](ci.cn.md) | 工作流、运行器和基准测试任务 |

## 提交规范

- 每条提交标题使用 `type(scope): description`。类型和组件范围用小写，描述用简洁的英文祈使句，末尾不加句号。
- 类型：`feat`（新功能）、`fix`（修复）、`perf`（性能优化）、`refactor`（结构调整）、`test`（测试）、`docs`（文档）、`ci`（工作流）、`build`（构建工具）、`chore`（维护）。性能前缀是 `perf`，不能写成 `pref`。
- 范围选择受影响的组件，例如 `core`、`generator`、`benchmarks`、`unity`、`burst` 或 `repo`。基准夹具改动按目的分类：`fix(benchmarks)` 修正夹具，`ci(benchmarks)` 修改其 CI 执行或采集方式。
- 每次提交都必须包含有实质内容的英文正文，与标题之间空一行。说明为什么需要修改、具体改了什么，以及相关验证及其结果。有重要限制、检查失败或未验证项时也必须说明。正文详略与改动规模相称；只有标题、重复标题或通用套话均不符合要求。
- 独立改动分别提交。破坏性变更在范围后加 `!`，并通过 `BREAKING CHANGE:` 页脚说明。
- 示例：`perf(core): reduce fixed record cursor writes`、`fix(generator): reserve large union payloads`、`ci(benchmarks): capture polymorphism JIT assembly on all hosts`。

## Pull Request

- 说明问题、改动和你运行过的检查及其结果。性能结论需要与对照库在同一次运行中的比值。
- 修改数据格式字节或既有机制需要先获得维护者批准；请先开一个 Issue，说明方案和证据。
