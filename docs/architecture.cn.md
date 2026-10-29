# 架构

[English](architecture.md)

所有改动都受[机制契约](mechanism-contract.cn.md)约束。

## 仓库结构

| 路径 | 内容 |
| --- | --- |
| `src/LuminPack.Core` | 运行时（NuGet 包 `LuminPack`，程序集 `LuminPack.Core`）：二进制和 JSON 读写器、直接分配写入使用的精确长度计算器、缓冲区、集合、布局转换、文本内核、压缩、ChaCha20-Poly1305、安全策略。目标框架 `netstandard2.1`、`net8.0` 到 `net11.0`。 |
| `src/LuminPack.Generator` | 增量源代码生成器和分析器（`netstandard2.0`，Roslyn 4.3）。单独发布为一个包，运行时包 `LuminPack` 依赖它。 |
| `src/LuminPack.Unity`、`src/LuminPack.Burst` | 可选的 Unity 值类型编解码，以及原生 Hook 表的 Burst 后端。 |
| `src/LuminPack.Platforms` 及各适配包 | 共享适配层，以及 ASP.NET Core、Avalonia、Drawing、MAUI、MonoGame、SkiaSharp、WPF 的编解码。 |
| `tests/` | 见[测试](#tests)。 |
| `benchmarks/LuminPack.Benchmark` | BenchmarkDotNet 对比：`SimpleBenchmark`、`SerializeBenchmark`、`JsonBenchmark`、`PolymorphismBenchmark`。 |
| `tools/` | `LuminPack.RepositoryChecks`（源码规范检查）、性能探针、xjb 表生成。 |
| `skills/luminpack` | 给 AI 助手使用的 Skill。 |
| `experiments/` | 本地实验，不属于产品。 |

### Core 源码布局

运行时入口和游标状态位于 `src/LuminPack.Core/Core`；可选的 `netstandard2.1` Hook 派发位于 `Core/Acceleration`。工具代码按职责放在 `Utility` 下：

| 目录 | 职责 |
| --- | --- |
| `Buffers` | 自有和分段输出、序列构造。 |
| `Collections`、`Collections/Hashing`、`Collections/Trees` | 集合存储视图、哈希表、树辅助。 |
| `References` | 引用身份和 Union 映射。 |
| `Memory`、`Memory/Views`、`Memory/Simd` | Marshal 操作、原始数组、非托管视图、复制内核。 |
| `Layout`、`Layout/Simd` | 字段偏移、布局标签、转换与字节重排。 |
| `Text` | `StringSerializer` 和文本内核。 |
| `Compression`、`Pool` | LZ 压缩和池化存储。 |
| `Configuration`、`Diagnostics`、`Protocol` | 选项作用域、异常、协议常量。 |
| `Compatibility` | 生成代码使用的公共转发入口。 |

文件的物理位置不改变命名空间和数据表示。

## 运行时执行模型

- **游标。** 每个读写器只前进一个游标：新版 .NET 上是 `ref byte`，`netstandard2.1` 上是 `byte*`。保留的 `Buffer` 窗口提供长度信息和回填位置。
- **双寄存器。** 每个状态在游标旁缓存窗口的结束位置（`_end`，写入器为扩容阈值 `_recordLimit`）。范围检查只需一次减法和一次有符号比较。设置 `Buffer`、`BufferCheckLimit` 或 `Owner`（internal setter）会刷新缓存。
- **生成代码的局部游标写入。** 生成的对象写入代码把游标放在局部变量 `ref byte __cursor` 中（JIT 会把它放在寄存器里），并调用返回 `ref` 的写入操作。连续的有界成员共享一次 `ReserveRecord` 检查，之后不再检查。在任何需要使用写入器本身的操作之前，用 `CommitRecords` 发布游标。
- **Reader 检查。** `EnsureReadable(count)` 在每次 unsafe 加载前与缓存的结束位置比较；Debug 和 Release 下都启用。
- **选项**是无状态的值类型配置（`ILuminPackOptions`），作为泛型参数传递，配置判断会折叠为常量。
- **缓冲区。** `LuminBufferWriter` 是基于一个 `ArrayPool<byte>.Shared` 数组的 `IBufferWriter<byte>`，同时持有一个追加会话的二进制/JSON 引用状态。可用区间从租用时观察到的 64 字节边界开始。逻辑容量和扩容阈值不受数组池桶大小影响；扩容仍能放进已租数组时原地扩展。writer 绑定存储或扩容后刷新时取得 `ref byte`；netstandard2.1 上取得 `byte*`，缓冲区用 `GCHandle` 从租用到归还一直固定该数组。

从缓冲区构造的二进制和 JSON Reader 绑定其读取引用上下文。Writer 优先使用显式引用上下文，其次使用关联缓冲区的上下文。`OptionState` 提供已初始化的上下文。

## 生成器

每个使用 LuminPack 的编译中运行两个增量生成器：

| 生成器 | 输出 |
| --- | --- |
| `LuminPackSourceGenerator` | 每个类型的格式化扩展、布局镜像和私有访问器、Union 槽位与 override。 |
| `LuminPackAggregateGenerator` | 每个程序集的 `LuminPackSerializer` 外观（共享泛型 API 加上每个类型的具体重载 partial 文件）、泛型派发、选项配置声明以及 `Custom` 格式化器注册表。 |

主要组件：

| 组件 | 职责 |
| --- | --- |
| `LuminPackSourceGenerator.*` | 模型分析：元数据、字段与继承存储、构造函数、类型分类、Union 发现。 |
| `LuminPackCodeGenerator.*` | 二进制读写、直接记录的长度计算、非托管分组、局部游标写入（`CursorWrite`）。 |
| `LuminPackExtensionGenerator.*` | 操作模板、JSON 辅助、派发片段。 |
| `LuminPackSerializerGenerator.*` | 具体和泛型的 buffer、span、流、异步、JSON 和压缩 API。 |
| `CodeEmitters/` | 每个标量或集合族一个 emitter。 |
| `Api/` | `Light` 和 `Minimal` 使用的外观调用分析。 |
| 各类投影（`SerializerCoreProjection`、`BinaryCursorProjection`、`CompiledOperationProjection`、`RecursionGuardProjection`、`GeneratedOperationScopes` 等） | 改写完成的操作：状态归属、带检查的游标运算、配置传递、递归保护、可读的局部变量名。 |
| `PortableStructPlan`、`PortableStructEmitter` | Packed 和 Tagged 结构体存储。 |
| `Incremental/` | 冻结值、依赖守卫和缓存（`TypeEmissionCache`、`AggregateEmissionCache`、`SerializerFacadeCache`、`RegistrySourceCache`、`PartitionedSourceCache` 等）。 |

### 操作流水线

1. 分析类型的规范契约，冻结操作模板。
2. 绑定根方法并组合嵌套操作，保留递归、`Custom`、虚调用和专用边界。
3. 把调用绑定到序列化器的状态方法体，并传递编译期配置。
4. 规整读取器存储的生命周期和游标槽位。
5. 简化语句块并选择可读的局部变量名（仅影响呈现）。
6. 格式化、添加 XML 文档，并通过缓存边界发布文本。

### 增量边界

生成过程把当前的语义分析与冻结的输出值分开。每个根类型的源码、布局和诊断在产出源码前都会比较；值没变就不会触发输出回调。跨编译缓存只保存描述、语法树标识、复制的设置和生成的字符串，绝不保存 `Compilation`、`SemanticModel` 或符号。失效判断覆盖项目级事实（引用、语言设置、别名、扩展方法签名）和源码闭包（观察到的类型、成员、特性、partial 声明）。`IncrementalGenerationMetrics` 为增量测试提供内部计数。

类型发现会在语义绑定前过滤没有特性的声明，并选择实际携带可序列化特性的 partial 声明。多个输出缓存共享所属编译内的依赖闭包。项目守卫仅弱引用元数据引用的标识，避免编译引用保留旧项目。完成输出批次后丢弃不再使用的文件包装缓存。编辑后，编译级守卫和聚合规划仍会检查当前项目事实；增量生成不代表每个阶段在无关编辑后都完全不执行。

在同一次编译中，类型管线与聚合管线会为相同类型、完整生成设置及流式读取选择共享冻结的操作模板。并发请求只发布一个延迟构造的模板，语义构造期间不持有发布锁。这些上下文以编译为弱键，对已完成模板也使用弱引用，并丢弃失败的构造任务；只有不含符号的模板进入保留驱动的输出缓存。未改变值的声明配置叠加会复用不可变值，仅显式叠加输入设置时才复制输入策略。临时操作语法跳过 XML 解析，最终供调用者使用的 API 文档仍保持完整。最终投影语法在丢弃前提供延迟格式器的转发信息，避免再次完整解析；含条件指令的源码继续使用文本解析回退。

生成器会在变更类型分析前，以及聚合格式器和门面贡献之间检查 Roslyn 取消请求。过期工作被取消时会传播取消状态，不会报告为生成器故障。已完成的贡献仍可复用，被取消的聚合批次不会发布。正在处理的单个贡献或项目级语义守卫可能需要完成后才会到达下一个取消边界。

### 生成的文件

`LuminPackSerializer.g.cs` 存放共享泛型 API；每个类型的具体重载放在以哈希命名的 partial 文件中，其他类型变化时保持稳定。每个生成的程序集都拥有一个 public 的 `LuminPackSerializer`；生成的文件屏蔽 CS0436，使引用程序集中的同名类型被遮蔽。每个负载的根方法放在 `__LuminRoots_<hash>` 容器中，这样每次调用的重载解析只需考察一个负载。

### 生成代码的文档

`GeneratedSourceDocumentation` 和 `LuminPackSerializerDocumentation` 只为可直接调用的 `LuminPackSerializer` API 和门面类型添加紧凑 XML 文档。API 注释来自同一份类型分析，包含类型、成员顺序与筛选、CLR 布局与线上布局、缓冲区语义和返回值单位。内部状态转发方法、执行根方法、Union 槽、布局辅助方法和私有门面适配器省略 XML，可执行代码保持不变。生成文件屏蔽 CS1591，启用文档警告时也无需为这些实现细节补 XML。

<a id="tests"></a>
## 测试

| 项目 | 范围 |
| --- | --- |
| `tests/LuminPack.Tests` | xUnit 运行时测试。`LuminPackTestArea` 选择 `All`（默认）、`Safety`、`Simd` 或 `Hooks`（带原生 Hook 探针的 `netstandard2.1` 资产）。 |
| `tests/LuminPack.Tests.Codegen` | 使用 SDK 自带 Roslyn 的生成代码契约：XML 文档、机制、内联、Union 游标、复用、分片、外部 Union、选项。 |
| `tests/LuminPack.Tests.Generator` | 使用 Roslyn 4.3 的生成器流水线：并发、emitter 形态、裁剪模式、增量失效（`--verify-incremental`）。 |
| `tests/LuminPack.Tests.Aot` | 裁剪/NativeAOT 冒烟程序和数据格式夹具（`--wire verify`）；加 `-p:LuminPackPackageVersion=<v>` 时改用打包好的 NuGet 包运行。 |
| `tests/LuminPack.Tests.Platforms` | 平台适配契约，用 `LuminPackPlatform` 选择平台。 |
| `tests/Unity` | Unity Mono/IL2CPP 冒烟工程。 |

## 异常

生产代码中的异常在 `LuminPackExceptionHelper` 中构造。生成的写缓冲区 catch 路径会重置缓冲区，并通过 `ExceptionDispatchInfo` 重新抛出，保留原始调用栈。
