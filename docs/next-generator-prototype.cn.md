# Next 生成器快速原型与迁移优先级

[English](next-generator-prototype.md)

先交付能够通过常规序列化 API 使用、覆盖常见对象图、可以开展运行时与生成器 Benchmark 的 Next 原型。根据实际使用和测量结果完善性能与行为，完整迁移继续按后续清单推进。

P0 为当前高优先级，每跑通一条完整调用链即可开始对应的技术验证。P1 为原型可运行后的优先完善项。P2 为完整迁移阶段的低优先级工作。实际使用或选定基准依赖后续功能时，将对应的最小实现提升到 P0。

## 接手状态

目标仓库为 `C:\Users\28027\Desktop\C#\LuminPack-direct`，生成器项目为 `C:\Users\28027\Desktop\C#\LuminPack-direct\src\LuminPack.Generator.Next`，接手基线为提交 `aa24595`。

已有代码包括 Formatter 家族入口、默认递归元素契约、48 个旧诊断描述符及校验入口，以及部分构造、复用、租借、回调、布局、版本容错和引用跟踪路径。应复用这些实现，按具体阻塞项修补。

基线验证范围为 175 个生成器源码文件的静态语义检查，错误数为零。生成消费者编译、完整诊断语料与序列化回归尚待执行。现有 design 文档中的历史语料结果需结合对应版本解读。

旧生成器的 `DirectJsonEmitter.cs` 存在接手前的未提交修改，应保留其工作区状态。

## 原型目标范围

初始目标选择现有运行时 Benchmark 使用的 .NET 10，并使用当前现代游标路径。原型至少覆盖以下形状。

- 普通 class、struct、枚举、可空值、框架常用标量和 string。
- 字段、自动属性、readonly 与 Include 存储，以及常见继承关系。
- 数组、List、Dictionary、HashSet、已迁入的常用集合及递归嵌套元素。
- 闭合泛型载荷、Tuple、ValueTuple 和 Rest。
- 非泛型抽象类 Union、接口 Union，以及数组、List、Dictionary 中的 Union 元素。
- 二进制和 JSON 的常规写入、分配返回、已有目标读取与复用缓冲区入口。
- 默认选项及基准实际使用的显式 profile。用到的 unmanaged 存储继续遵守 Native、Packed、Tagged 契约。

初始范围属于本次优先级筛选。扩展到其他运行时或协议组合时，补齐对应实现和验证。

## 实施顺序

1. 先打通常规二进制 API、常见对象图和一个代表性消费者，按相关功能批量编写后集中联调。
2. 第一条完整调用链可运行后，立即开始对应的运行时 Benchmark 和生成器 Benchmark，取得首轮时间与分配数据。
3. 继续补入 JSON、其余常用入口和现有基准依赖的非泛型 Union，分批扩展代表性消费者。
4. 用实际使用和测量结果决定 P1 的顺序，P2 继续保留完整迁移范围。

已经跑通的场景持续用于测量，其余原型场景按批次补齐。

## 原型状态

P0 调用入口层已经完成。引用 `LuminPack.Generator.Next` 的消费者可以直接调用常规 `LuminPackSerializer` API，每个闭合调用都绑定到
Next 生成的正文。

- 二进制与 JSON 泛型门面（包括 `Register` 和仅注册的 `FormatterDispatch`）作为 post-initialization 源码发布，调用点在生成器运行时
  即可完成绑定。
- 语法 provider 把每个闭合调用记录为值相等的事实：重载、载荷、闭合或声明 profile（或开放的 profile 类型参数），以及编译器提供的拦截位置。
- 拦截器转发到每个载荷的 `LuminEntry_` owner，其正文沿用既有具体入口的形态。可打包载荷调用自身的 `Serialize_`/`Deserialize_`
  根方法；其他闭合根按同类型字段进行分类，加入共享 owner 需求，并调用 owner 的静态 `Write`/`Read`。没有已发布根方法的调用继续走泛型门面。
- 目前静态绑定后端只有 compiler interception。IL 改写、旧编译器及其他后端组合仍属于 P2。

### 运行原型

运行时 Benchmark 项目默认选择 Next，并在启动时打印实际使用的生成器。BenchmarkDotNet 的子项目构建沿用同一选择。

```bash
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-fixtures
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --verify-roundtrip
dotnet run -c Release --project benchmarks/LuminPack.Benchmark -- --filter "*"
```

旧生成器基线使用 `-p:LuminPackGenerator=Legacy`。当前分支上旧生成器项目无法编译，因此需要通过
`-p:LuminPackLegacyAnalyzer=<LuminPack.Generator.dll 路径>` 传入预先构建的旧分析器。Next 及预构建分析器的构建会向 `LuminPack.Core`
传递 `LuminPackLegacyGeneratorReference=false`，跳过其用于打包的旧生成器项目引用。

单元测试通过 `-p:LuminPackGenerator=Next` 选择 Next，默认仍为 Legacy。可与测试区域组合使用，例如
`-p:LuminPackGenerator=Next -p:LuminPackTestArea=Safety -f net10.0`。

生成器 Benchmark 支持真实语料及其引用：
`GeneratorBenchmarks --corpus benchmarks/LuminPack.Benchmark --refs <Benchmark 输出目录> --old-dll <旧分析器>`。

### 已验证状态

- Benchmark 语料和单元测试 Safety 语料仅使用 Next 编译，生成代码错误为零。
- 四个 Benchmark 夹具在 Next 与旧生成器下产生逐字节一致的 LuminPack 二进制和 JSON 载荷。
- `--verify-roundtrip` 在没有任何注册的情况下通过全新读取、已有目标读取、声明与显式 profile、`LuminBufferWriter`、
  `IBufferWriter<byte>` 和 JSON 检查。
- Safety 测试区域在 Next 下通过 94 项中的 87 项。

### 已记录缺口

- 声明的输入选项已迁移，但尚未编译，也未与旧输出比对。注册分发跟随程序集声明；每个具体根冻结自身的输入上限，受信根不做输入检查；显式不受信根
  以严格 profile 读取 JSON，并对由输入选择类型的形状直接拒绝；声明的集合、字符串和深度上限在根正文的读点检查，受限根下的嵌套读取改走其入口的
  受检副本。已知差异：只有声明了集合、字符串或深度上限的根才使用受检副本；受检副本中受信载荷的内联子对象仍按受信方式读取标记；版本容忍、镜像、
  引用跟踪和结构体载荷、被拒绝的结构体和 Union 根尚未覆盖；压缩计数未检查；流和异步入口尚未拦截，仍使用程序集上限；异常映射尚未迁移。
- 独立的 `List<string>` 根在失败前会推进调用方 reader；旧根只在成功时发布游标（一项 Safety 测试）。
- 记录正文对集合成员（如 Union 值的 List 或 Dictionary）调用其 owner，而旧管线会在原位展开这些写入，因此写入此类记录慢于旧管线（P1 快速路径）。
- Benchmark 对象图的 JSON 读取比旧管线多分配约 2 KB，来源尚未定位。
- 根方法名称来自可读类型名，两个读法相同的载荷只有第一个完成绑定，另一个继续走泛型门面。
- 调用列表是 `ImmutableArray`，每次编辑都会重新计算共享 owner 集合，再由值相等的输出命中缓存。
- 完整单元测试区域、其他目标框架以及旧生成器的编译错误尚未处理。

## P0 高优先级

### 常规调用入口和静态绑定

- [x] 打通 `LuminPackSerializer` 的常规二进制与 JSON API，使调用到达 Next 生成的具体根方法。
- [x] 覆盖隐式类型调用、显式载荷类型调用，以及显式载荷加 profile 调用。
- [x] 接通分配返回、调用者持有的 `LuminBufferWriter` 和已有目标反序列化入口。
- [x] 优先接入覆盖这些调用的现代 compiler interceptor 或已有具体静态入口，完整后端组合按 P2 推进。
- [x] 补齐这些入口实际需要的类型发现、闭合操作发现、owner 发布和支持声明。

完成条件是代表性消费者可以直接调用常规 API，并能确认调用绑定到 Next 生成正文。

### 常见对象图和递归依赖

- [ ] 修复普通对象、继承对象、自动属性、readonly 与 Include 成员在模型到渲染之间的缺口。
- [ ] 打通常见闭合泛型、数组、集合、字典键值和 Tuple/Rest 的依赖发布。
- [ ] 通过默认递归契约处理 `ConcurrentQueue<List<int[]>>`、`Dictionary<string, List<MyClass>>` 等嵌套组合。
- [ ] 处理代表性对象图触发的递归和深度限制，复杂泛型递归按 P1 推进。
- [ ] 完整发布模型、镜像、根方法和子 owner，消除原型范围内的空渲染及缺失调用目标。

完成条件是新增一个已支持元素组合时，可以沿用现有容器实现和静态转发。

### 非泛型 Union 和基准实际模型

现有基准依赖继承和接口多态。`SimpleBenchmark` 使用继承、Packed 的 `Transform` 和含 Union 值的字典。`SerializeBenchmark` 与 `JsonBenchmark` 使用 `CharacterSaveData` 对象图。`PolymorphismBenchmark` 使用抽象类根、接口根及 Union 集合。

- [x] 完成这些非泛型 Union 的自动发现、标签、虚分发、分支读写和依赖发布。
- [x] 覆盖 `List<IFoo>`、`Dictionary<int, IFoo>` 和继承成员。
- [ ] 修复实际分支所需的构造、Local 镜像、固定游标和 profile 连接。
- [x] 保留现有窄/宽标签、分层写分发及字段布局契约。

完成条件是上述基准模型能以原有数据结构运行。基准中用到的 Union 与 Packed 存储属于 P0。

### 原型范围内的行为正确性

- [ ] 对齐 null、空集合、成员顺序、数组形状和基础 JSON 表示。
- [x] 对齐常规构造、已有目标复用、缓冲区增长及状态发布。
- [ ] 保留默认 Reader 检查、Boolean 检查、已有容量检查和栈保护。
- [x] 对齐基准使用的 `LuminPackUtf16LengthOptions` 与 `LuminPackDefaultOptions` 调用。
- [ ] 对用到的 Native、Packed、Tagged 类型核对单值与集合表示。
- [x] 保持普通调用的具体静态路径，显式和 Custom 注册继续使用原有类型化委托边界。

完整诊断边界与协议组合按 P2 推进。原型遇到的生成错误和行为错误在此修复。

### Next 运行时基准接入

运行时 Benchmark 项目当前引用旧生成器 Analyzer。提供可识别的 Next 选择方式，并保留旧版基线的运行方式。

- [x] 明确基准实际加载的 Analyzer 和生成正文身份。
- [x] 先跑通 `SimpleBenchmark`，随后接通 `SerializeBenchmark`、`JsonBenchmark` 和 `PolymorphismBenchmark`。
- [x] 复用原有模型、随机种子、数据、profile 和基准配置。
- [x] 使常规应用与基准共用正常调用链。
- [x] 在同机、同输入、同次运行条件下记录相对参考实现的耗时比、重复样本离散程度、分配和有效载荷大小。

完成条件是可以重复测量 Next 的序列化与反序列化，并能解释调用链和输入条件。

### 生成器基准和集中联调

- [x] 接通已有 GeneratorBenchmarks，分别记录完整生成、无关编辑、成员编辑的时间与分配。
- [ ] 记录生成文件数、源码体积和实际保留的操作集合。
- [x] 批量完成一组相关 P0 代码后，集中构建 Next 和代表性消费者，修复阻塞编译及基本读写的问题。
- [x] 对代表性模型做有限的基本读写、复用和旧格式互读检查，然后开展 Benchmark。
- [x] 将每次测量涉及的目标、载荷、profile 和生成器身份保留在结果中。

分别记录源码静态检查、消费者运行、运行时 Benchmark 和生成器 Benchmark 的结果。完整迁移和全平台回归继续推进期间，原型可先用于技术验证。

## P1 原型可运行后的优先完善

| 工作 | 具体范围 | 完成条件 |
| --- | --- | --- |
| 生成器分配与增量性能 | 减少重复模型投影、语义查询、集合复制和排序，核对无关修改及子类型修改的失效范围 | 有重复测量，缓存输出与 fresh 输出一致 |
| 旧 unmanaged 根方法共享 | 迁移闭合泛型和数组根的共享、替换证明、绑定证明及保留入口 | 恢复旧共享机制，失败候选保留独立根 |
| 按调用保留操作 | 接入四个独立操作掩码、依赖和 ForceGenerate 选项 | 应用按调用保留，库保留完整操作 |
| 复杂泛型与递归 | 泛型外层中的嵌套载荷、更深对象图和相互递归 | 按完整契约扩展支持面，并正确终止分析 |
| 声明选项传递 | 特殊化内联集合中的成员 profile，以及常用非默认字符串选项 | 不同入口保留同一声明选项 |
| 显式输入限制 | 迁移旧策略实际执行的限制和递归深度检查，将根常量传到读点 | 保留旧检查位置、默认行为和状态归属 |
| 已迁入协议的实用覆盖 | 常见 VersionTolerant、CircleReference、构造、租借及回调组合 | 代表性使用能够运行，针对性核对异常与发布行为 |
| 常见 Custom 使用 | 完成所需注册 API、自动注册和缺失操作异常 | 用户类型能按原有签名注册和调用 |
| 已有 Formatter 优化路径 | 核对原型或基准实际触发的遍历、复用、快照、builder 和池路径 | 保留旧算法、资源归还和可见行为 |

P1 的执行顺序由实用性和基准结果决定。直接阻塞实际使用的项目优先修复。

## P2 完整迁移阶段的低优先级

| 工作 | 仍需完成的范围 |
| --- | --- |
| 泛型 Union | 泛型根、闭合分支、参数映射、约束匹配和虚调用后的缓存槽位 |
| 跨程序集载荷 | 完整引用元数据导入、重绑定、外部 owner、外部 Union 和跨程序集选项 |
| CircleReference 全部形状 | 泛型、值类型、复杂构造和 ref-return 租借，槽位演进及异常组合 |
| 版本容错全部组合 | 泛型与继承组合、压缩字段、旧名称、schema 演进和完整旧版本互读 |
| 全部 facade 重载 | Stream、异步、压缩和其余公开 API，精确长度及异常生命周期 |
| 全部静态绑定后端 | IL 后处理、旧编译器、源码泛型包装特化、友元程序集和关闭后端组合 |
| 全平台适配 | netstandard2.1 指针游标、Unity、IL2CPP、32 位、ARM64、AOT 和可选模块 |
| 全部诊断语义 | 48 个诊断的触发、优先级、数量、位置、关联位置、代码修复和 IDE 不完整源码 |
| 全部公开注解和工程选项 | NonGenerator、LuminPackableObject、别名冲突及剩余程序集配置 |
| 全 Formatter 行为矩阵 | 所有家族和组合的比较器、重复元素、并发观察、失败前缀、资源清理与旧运行时回退 |
| 完整 JSON 兼容 | 全部 envelope、转义、旧名称、重复和未知属性、省略成员及失败发布行为 |
| 完整输入和压缩组合 | 各协议、嵌套作用域、计数、资源限制、异常与复用的组合 |
| 发行接入和文档 | analyzer/package、构建与 IDE 接入，中英文文档和受影响的使用 skill |
| 完整验收 | 旧格式夹具、诊断语料、平台与语言矩阵、增量一致性、必要的模糊测试 |

新增跨语言生成、额外格式、ASP.NET 产品扩展及下一轮标量/string/LuminPackable 泛型设计可进入后续产品规划。

## 原型完成条件

- [x] 一个常规消费者可以调用 Next 的公共 API。
- [ ] 常见 class/struct、继承、自动属性、闭合泛型、集合和嵌套元素可以生成完整正文并运行。
- [x] 现有基准依赖的非泛型 Union 和 Packed 类型可以运行。
- [x] 常规二进制和 JSON 写入、读取、已有目标和复用缓冲区入口可以运行。
- [x] 运行时 Benchmark 与生成器 Benchmark 均可执行，结果能够确认使用 Next。
- [x] 基本有效性检查已完成，并列明实际覆盖范围与剩余缺口。

达到这些条件后，优先利用原型开展测量和改进，继续按 P1、P2 扩展覆盖。

## Claude 执行约束

先阅读仓库 AGENTS、机制契约、架构和贡献指南。沿用 Analysis → 不可变值模型 → Render，Roslyn 符号保留在分析边界与相应生命周期的缓存中。

复用已迁入的 Formatter 和渲染器。按开放类型定义、存储事实及能力选择算法，默认元素操作递归调用具体静态 owner。保留用户选定的家族目录组织与特殊化扩展位置。

先批量实现一组相关的 P0 缺口，再集中联调。将编译失败、行为错误和测量结果转化为具体修复项。完成一组常规使用场景后，及时使其可运行并用于基准。

优先级决定实施顺序。已进入原型支持范围的功能继续遵守机制契约，包括协议、Local + As、引用表、构造与复用、回调、typed delegates、布局、Release 检查和状态归属。

生成代码的空格与换行可以不同。按仓库约定提交完成的改动，并保留工作区中其他人的修改。
