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
- `runtime-compatibility.yml`：在 Windows、Linux、macOS（x64 和 ARM64）上运行裁剪版和 NativeAOT 程序，标量/SSSE3/AVX2 回退以及 x86。按 CPU 厂商、ARM32 和 AVX-512 的运行需要手动触发，并需要带对应标签的自托管 runner。
- Unity 工作流构建并运行 Mono 和 IL2CPP Player，需要配置 Unity 许可证密钥。
- `test-areas.yml` 在 Windows、Linux 和 macOS 上以 Debug、Release 运行 Safety 测试区；三个系统均执行 AspNetCore、Avalonia、Maui、MonoGame、SkiaSharp 适配契约，Windows 额外执行 Drawing 和 Wpf。适配模块使用已有 Core 二进制，因此先构建 Core。
- 推送到 `main`、`refactor` 或创建拉取请求均会触发托管检查。Unity 执行仍需要已配置的许可证或原生运行器；`.github/scripts` 下的辅助脚本随工作流纳入版本控制。
- 基准测试只能通过手动的 `run_benchmarks` 输入触发，明确限定为现有四类：`SimpleBenchmark`、`SerializeBenchmark`、`JsonBenchmark`、`PolymorphismBenchmark`，不作为正确性门禁。
- CI 按事件区分并发组，推送不会取消手动启动的基准测试。注册表子进程测试启动测试程序集旁的 apphost，以保持与测试进程一致的架构，包括自包含 x86。

## 基准测试

```text
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --filter *SimpleBenchmark*
```

用 LuminPack 耗时除以参照库耗时来评判结果，数据必须来自同一台机器的同一次运行；见契约第 5 节。

## 源码规范

- 遵循 `.editorconfig`：4 空格缩进，UTF-8。
- 代码、注释、XML 文档、诊断、测试和维护文档使用英文。README 和 `*.cn.md` 中文版是例外。测试中的多语言字符串用 Unicode 转义书写。
- 每个方法、构造函数、运算符和转换都要有 XML 文档，包括摘要、参数、类型参数和返回值。注释写在特性之前。
- emitter 分文件存放；大型生成器按职责拆分为 partial 文件，保持类型和成员名称不变。
- 通过 `LuminPackExceptionHelper` 抛出异常，保留异常类型、参数名和清理逻辑。需要保留原始异常身份时使用 `Rethrow`。
- 修改名称、成员顺序、枚举值、null 标记、字符串模式、帧格式或存储视图都可能破坏已持久化的数据。本仓库中没有调用点的公共成员仍可能被使用者或生成代码使用。
- 修改移植的格式化代码时，保留第三方声明（`docs/xjb-LICENSE.txt`、`docs/xjb-NOTICE.md`）。

## 文档

- README 是项目封面：介绍、安装、使用和链接。README 和 `docs` 中不放发布说明、版本历史、迁移通知或实验报告。
- 每个 README、AGENTS、CONTRIBUTING 以及 `docs` 页面都有英文 `name.md` 和中文 `name.cn.md`，两者一起更新。
- 公共用法改变时更新 `skills/luminpack`。
