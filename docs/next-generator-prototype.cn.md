# Next 生成器迁移状态

[English](next-generator-prototype.md)

`src/LuminPack.Generator.Next` 是 `src/LuminPack.Generator` 旧生成器基于模型的后继实现。本页记录 Next 的运行方式、已验证范围，以及所有尚未迁移的
旧生成器能力。通过单元测试只能证明测试语料覆盖到的形状；下方清单是剩余工作的权威列表。

优先级：P1 会阻碍部分项目的常规使用，或造成可测量的性能退化；P2 用于完成完整迁移。实际使用依赖某个 P2 项时，将其最小必要部分提前。

## 运行 Next

单元测试和运行时 Benchmark 项目用 `-p:LuminPackGenerator=Next` 或 `-p:LuminPackGenerator=Legacy` 选择生成器。测试需同时指定测试区域，例如
`-p:LuminPackGenerator=Next -p:LuminPackTestArea=All -f net10.0`。预编译的旧分析器可通过
`-p:LuminPackLegacyAnalyzer=<LuminPack.Generator.dll 路径>` 传入。

```bash
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-fixtures
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-roundtrip
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --filter "*"
```

生成器基准接受真实语料及其引用：
`GeneratorBenchmarks --corpus benchmarks/LuminPack.Benchmark --refs <基准输出目录> --old-dll <旧分析器>`。

## 调用如何绑定

泛型二进制与 JSON 门面（包括 `Register` 和仅用于注册的 `FormatterDispatch`）作为 post-initialization 源码发布，调用点在生成器运行期间即可绑定。
语法提供器把每个封闭调用记录为值相等的事实：重载、载荷、封闭或声明的 profile（或开放的 profile 类型参数）以及编译器给出的拦截位置。拦截器转发到
每个载荷的 `LuminEntry_` owner。可打包载荷调用其 `Serialize_`/`Deserialize_` 根；其他封闭根调用共享 owner 的静态 `Write`/`Read`。没有发布根的
调用保留在泛型门面上。

没有编译器拦截时，定义了 `LUMINPACK_IL_REWRITE`（即 `LuminPackUseIlRewriting` 构建集成）的构建得到相同的入口：每个拦截器改为带有该门面
确切构造的仅构建期映射 `LuminPack.Generated.StaticCall`，由 `LuminPack.ILPostProcessor` 在编译后替换已编译的调用。Next 生成的代码需要
C# 13，因此该后端暂时无法服务较旧的语言版本。

## 操作如何保留

没有友元程序集的应用程序或 Unity 预定义程序集只保留代码实际调用的操作，规则见[生成模式](generation-modes.cn.md)；其他项目保留全部四种
操作。每个封闭调用把它的操作保留在载荷类型经由成员类型、泛型参数、基类型、接口和特性类型所到达的载荷上，每个被到达的声明再把自己的操作
继续传递下去。基类型或接口也会到达从它派生的声明，因为 Union 根要分派到这些 case。共享 Formatter owner 保留需要它的载荷和根所具有的每种
操作。没有具体根的门面调用、注册、直接的 writer/reader 状态调用、门面方法组、生成的扩展方法名以及
`LuminPackGeneratorOptions.ForceGenerate*` 会为所有载荷保留对应操作。不保留任何操作的载荷不发布文件，没有 JSON 操作的载荷不发布 JSON
文件。在任何项目中，拦截器入口只保留其调用用到的方向。

## 已验证状态

- 旧生成器基准为提交 8af1579（最后一个通过完整测试区域的旧提交）。使用 Next 时，net10.0 完整测试区域 1135 项全部通过。net9.0 与 net11.0 测试
  目标尚未用 Next 运行。
- 四个 Benchmark 固定数据在 Next 与旧生成器下产生逐字节相同的二进制和 JSON 载荷；`--verify-roundtrip` 在无注册的情况下通过新建读取、已有目标读取、
  声明与显式 profile、`LuminBufferWriter`、`IBufferWriter<byte>` 和 JSON。
- 单元测试语料中的每个载荷都已完整建模；只有 Custom 载荷由载荷渲染器之外的部分负责。
- 生成的公开方法名称与旧版一致，泛型门面的重载（含异步入口）也一致。
- 汇编对比（两个生成器各跑完整测试，FullOpts JIT 反汇编）：在 4344 个可对应方法上，Next 的代码字节为旧版的 0.992 倍，call 指令略少。系统性差异
  见下方性能机制一节。

## 尚未迁移

### 静态绑定与发布

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 跨程序集载荷 | Next 只发现当前编译中声明在类型上的 `[LuminPackable]`。程序集级 `[LuminPackable(typeof(T))]` 外部根、引用元数据导入（旧版 `ReferencedTypeMetadata`，含诊断 LuminPack112）、外部 owner、外部 Union 和跨程序集选项均缺失。 |
| P2 | 源泛型方法的包装特化 | 旧版通过生成的包装（`StaticSerializerBinding.Wrappers`）特化用户泛型方法内的调用；Next 通过泛型方法展开封闭这些载荷，但没有重现包装入口。 |
| P2 | 其余后端组合 | 友元程序集与禁用后端的组合未验证。生成代码与门面使用 C# 13 特性（ref 字段、`ref readonly` 参数、ref struct 接口），旧版支持的 C# 9–12 使用方无法编译。 |

### 生成源码

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | XML 文档 | 旧版为生成成员生成文档（`GeneratedSourceDocumentation`、`LuminPackSerializerDocumentation`）。对单元测试语料，旧版约 54,000 行文档注释，Next 约 860 行。 |
| P2 | 产物统计 | 尚未按构建记录生成文件数、源码体积和保留的操作。 |

### 类型形状与协议

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 泛型外层类型中的载荷 | 声明在泛型类型内部的可打包类型（如 `Outer<T>.Inner`）被拒绝，原因为 "a payload nested in a generic definition is not modeled yet"。旧版生成器同样不支持（报告 LuminPack099 并生成无法编译的代码），因此这是新能力而非迁移项。 |
| P1 | 泛型 CircleReference | 只为编译中实际出现的封闭构造生成 owner，缺少旧版发布的开放定义，其他封闭构造没有生成路径。值类型形状、复杂构造、ref 返回租借、槽位演进和失败组合均未验证。 |
| P2 | 结构体内联顺序 | 旧版内联展开嵌套成员时，每一层先自动属性、后字段。Next 已通过内联顺序根为类重现该顺序；嵌套结构体仍按声明顺序。 |
| P2 | 版本容忍组合 | 泛型与继承组合、压缩字段、旧名称、Schema 演进和完整的旧版交叉读取，只覆盖到测试所及范围。 |
| P2 | 公开注解 | `NonGenerator`、`LuminPackableObject`、别名冲突和其余程序集选项已被读取，但完整语义尚未与旧版对照。 |
| P2 | JSON 兼容性 | 各种封装、转义、旧名称、重复与未知属性、省略成员和失败发布，只覆盖到测试所及范围。 |
| P2 | Formatter 行为矩阵 | 各家族的比较器、重复项、并发观察、失败前缀、清理和旧运行时回退，只覆盖到测试所及范围。 |

### 输入限制

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 受检副本 | 声明的集合、字符串和深度上限在根正文的读取点检查，有上限根下的嵌套读取走受检副本。只有声明了上限的根才使用受检副本；受检副本内可信载荷的内联子对象仍使用可信的标记读取。 |
| P1 | 未覆盖的载荷种类 | 版本容忍、镜像、引用跟踪和结构体载荷、被拒绝的结构体以及 Union 根不在限制路径覆盖范围内。 |
| P2 | 流、异步与压缩 | 流和异步调用按载荷上限暂存，压缩成员使用压缩内核；两者都没有专门测试验证，其余协议、嵌套作用域、计数与复用组合也未验证。 |

### 性能机制

以下各项保持线格式不变，但生成的代码与旧版不同。

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 嵌套记录展开 | 旧版内联展开嵌套记录，包括含镜像成员、回调、版本容忍或池化构造的记录；Next 调用其 owner。 |
| P1 | 记录正文中的集合成员 | 记录正文对集合成员（如 Union 值的 List 或 Dictionary）调用其 owner，旧版在原位展开这些写入。 |
| P1 | 相邻定长字段整块复制 | 旧版在字段地址证明后整块复制相邻定长字段；Next 逐字段写入。 |
| P1 | 非托管根共享 | 旧版通过替换证明和绑定证明共享封闭泛型与数组根；Next 为每个类型单独发布根。 |
| P2 | 根内联策略 | 若干在旧版为 `NoInlining` 的接口与集合根被内联进 Next 入口；时区和精确接口入口形式没有特化。 |
| P2 | JSON 读取分配 | Benchmark 对象图的 JSON 读取比旧版多分配约 2 KB，来源尚未定位。 |

### 平台与目标框架

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 兼容目标 | 旧版为 netstandard2.1 等兼容目标的指针读取器固定托管输入（`PinnedReaderProjection`）；Next 没有对应实现，且只在 .NET 10 上运行过。 |
| P2 | Unity、IL2CPP、32 位、ARM64、AOT | 尚未用 Next 验证。Unity 还需要上文所列的较旧语言版本。 |
| P2 | 可选模块 | 各平台与集成模块尚未针对 Next 构建或测试。 |

### 诊断

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | LuminPack021 | 目标框架没有运行时访问器时 `[LuminPackInclude]` 的警告未报告。 |
| P2 | 诊断语义 | 其余 48 个描述符均已存在；触发条件、优先级、数量、位置、相关声明、代码修复和 IDE 中不完整源码的行为尚未与旧版对照。 |

### 生成器性能

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 增量工作 | 调用列表是 `ImmutableArray`，每次编辑都会重新计算共享 owner 集合，之后才由值相等的输出命中缓存。重复的模型投影、语义查询、集合复制和排序尚未削减。 |

### 验收

尚未对 Next 运行 Benchmark 固定数据之外的旧版线格式固定数据、诊断语料、平台与语言矩阵、增量等价性检查和模糊测试。打包、构建与 IDE 集成以及受影响的
使用技能仍以旧生成器为准。

## 执行约束

先阅读仓库 AGENTS、机制契约、架构和贡献指南。沿用 Analysis → 不可变值模型 → Render，Roslyn 符号保留在分析边界与相应生命周期的缓存中。

复用已迁入的 Formatter 和渲染器。按开放类型定义、存储事实及能力选择算法，默认元素操作递归调用具体静态 owner。保留用户选定的家族目录组织与特殊化
扩展位置。

每个已迁移功能继续遵守机制契约，包括协议、Local + As、引用表、构造与复用、回调、typed delegates、布局、Release 检查和状态归属。判断性能是否持平时，
先对照生成代码和 JIT 反汇编，再运行 Benchmark。

生成代码的空格与换行可以不同。按仓库约定提交完成的改动，并保留工作区中其他人的修改。
