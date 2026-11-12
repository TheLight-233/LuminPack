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
增量 driver 的聚焦契约检查使用
`dotnet run -c Release --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -- --verify-incremental`。
检查覆盖调用位置、成员、根类型、操作方向、程序集选项、程序集身份、泛型包装、声明与 unsafe 设置的编辑，逐项对比保留 driver 与全新 driver 的
生成源码和编译器诊断。
同一命令改用 `--verify-input-limits` 可检查受检副本：可信内联后代、List/数组/Dictionary 元素、错误标记与 Boolean 值、根限制和复用，
覆盖全部六种字符串 profile。
`--verify-documentation` 检查三种编译器文档模式下的调用方 XML、编译器导出的文档、不变的可执行 token，以及只修改布局、添加或移除文档事实时的
保留 driver 失效行为。`--verify-assembly-options` 检查两个选项构造函数、注册策略、全部十六种强制操作组合、旧模式值、库与友元保留、
安全编译及默认 JSON 回退的输入策略。`--verify-external` 编译一个库和一个用 `[assembly: LuminPackable(typeof(T))]` 标记该库类型的使用方，
双向比较其字节与库自身操作的结果，并在引用程序集上期望 LuminPack112。

## 调用如何绑定

泛型二进制与 JSON 门面（包括 `Register` 和仅用于注册的 `FormatterDispatch`）作为 post-initialization 源码发布，调用点在生成器运行期间即可绑定。
语法提供器把每个封闭调用记录为值相等的事实：重载、载荷、封闭或声明的 profile（或开放的 profile 类型参数）以及编译器给出的拦截位置。拦截器转发到
每个载荷的 `LuminEntry_` owner。可打包载荷调用其 `Serialize_`/`Deserialize_` 根；其他封闭根调用共享 owner 的静态 `Write`/`Read`。没有发布根的
调用保留在泛型门面上。

没有编译器拦截时，定义了 `LUMINPACK_IL_REWRITE`（即 `LuminPackUseIlRewriting` 构建集成）的构建得到相同的入口：每个拦截器改为带有该门面
确切构造的仅构建期映射 `LuminPack.Generated.StaticCall`，由 `LuminPack.ILPostProcessor` 在编译后替换已编译的调用。编译器拦截需要
C# 12，因此较旧的语言版本通过该后端绑定。

## 兼容目标

写入器与读取器状态使用原生指针游标的运行时（例如 netstandard2.1）会得到投影到该游标上的同一套渲染操作：引用游标的存储、读取和前进改为指针运算
（禁用 unsafe 时改为位置运算），基于托管输入的读取器读取固定或池化的副本，与旧版 `PinnedReaderProjection` 一致。低于 .NET 8 的目标上，门面选用
不含 `scoped` 且固定读取输入的变体。这些使用方可以像旧生成器一样用 C# 9 编译。.NET 8 及更高版本的目标保留引用游标代码，仍需要 C# 13。

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

## 已完成的迁移范围

勾选仅表示本项明确列出的范围已完成；下方仍列出的相关组合继续保留为待办。

- [x] **调用方 XML 文档。** 门面及其载荷专属便捷入口容器包含形参、类型形参、返回值、缓冲区归属、复用和流生命周期说明。具体入口描述经过筛选的
  成员顺序、物理存储、CLR 布局、旧名称与引用槽，并链接实际 Formatter 容器。执行根、状态转发器和辅助声明不生成 XML。聚焦语料在 `None`、
  `Parse`、`Diagnose` 三种模式下检查 381 个 API、编译器 XML 输出及可执行 token 不变。
- [x] **程序集注册策略。** `LuminPackGeneratorOptions` 的两个构造函数采用相同的 `Auto`/`Disabled` 语义。禁用注册时不发布门面注册 API，
  不查询泛型或 Union 注册，也不向注册表预留 Union 标签；已知 Union case 沿用原有派发。
- [x] **程序集操作保留设置。** 聚焦矩阵覆盖全部十六种 `ForceGenerate*` 组合、被忽略的旧模式值、库与友元保留，以及禁用 unsafe 时的注册能力。
- [x] **程序集回退文档边界。** 默认泛型 JSON 读取采用程序集的 JSON 输入 profile；UTF-8 与 UTF-16 的严格数值语法和编码输入上限均已验证，
  包括没有任何特性载荷的编译。
- [x] **有上限普通对象的受检副本。** 已支持的副本将根策略递归传入可信内联记录及 List、数组、Dictionary 的载荷元素。六种 profile 的语料覆盖
  标记、Boolean 域、配额、空值、普通线格式与复用；完整目标预热后的复用读取不分配内存。其余载荷种类仍列在下方。
- [x] **规范化增量发布。** 收集事实按值比较，重复封闭根共享编译内分析，Formatter 需求不变时复用 driver 所有的目录，owner 聚合保留首次声明且不使用
  LINQ 分组。XML 缓存同时记录已存在和缺失的声明依赖，并在仅布局变化时失效。
- [x] **可信 Boolean 标量读取。** 普通和内联可信字段使用推进游标的 reader 操作，并保留 Reader 边界检查；不可信字段继续检查 Boolean 域。
- [x] **兼容目标。** netstandard2.1 等指针游标运行时得到投影后的游标代码、固定或池化的读取输入以及兼容门面。Benchmark 语料以 C# 9 针对
  netstandard2.1 运行时编译，没有生成代码错误；netstandard2.1 + C# 9 下经 IL 重写的语料得到与 .NET 10 相同的结果和字节。禁用 unsafe 的
  安全代码变体尚未运行。
- [x] **声明成员顺序。** 未声明顺序的成员在任何位置都按声明顺序序列化，包括在父级中展开的嵌套记录。旧版每一层先自动属性、后字段的规则
  被有意移除，因此混合自动属性与字段的嵌套类载荷与旧版字节不同；四个 Benchmark 固定数据不受影响。
- [x] **泛型外层类型中的载荷。** 声明在泛型类型内部的可打包类型（如 `Outer<T>.Inner` 或 `Outer<T>.Mid<U>.Leaf`）作为泛型定义处理，
  类型形参依次为外层形参与自身形参。封闭使用绑定到定义的泛型 owner，封闭成员类型的 owner 名称包含外层实参（如 `OuterOfIntInner`）。
  覆盖类、结构体、两层嵌套、成员与列表使用的语料，在拦截器与 IL 重写下二进制和 JSON 往返均正确。旧版生成器从未支持该形状（LuminPack099）。
  嵌套在泛型类型中的 Union case 仍被拒绝。`GenericContainerPayloadTest` 在 Next 生成器下覆盖这些形状。
- [x] **重复简单类型名的区分。** owner、入口和别名名称不含命名空间，因此不同命名空间中同名的两个类型会共享 owner：载荷操作合并进同一个
  partial 类，而 `List<Point>` 与 `List<Other.Point>` 等集合 owner 发生冲突，其中一个不会发布。现在由编译事实列出声明在多个命名空间中、
  或与常用框架类型同名的简单名称，这些名称保留命名空间（`OtherPointFormatter`、`ListOfOtherPointFormatter`）。其他名称不变；Benchmark
  语料的输出完全一致。
- [x] **源泛型方法的包装特化。** 到达门面的源泛型方法的封闭调用（例如基于 `Deserialize<Box<T>>` 的 `Read<double>(bytes)`）会通过拦截器
  或 IL 重写器重定向到生成的封闭副本（`LuminWrapper_Read_Double`），与旧版 `StaticSerializerBinding.Wrappers` 一致。副本中的门面调用绑定
  与源调用相同的具体入口，对其他转发方法的调用绑定各自的包装。搬移或封闭后可能改变语义的方法体（lambda、await、隐式接收者、开放运算符、
  不可访问成员、静态初始化）与旧版一样保留在泛型门面上；与旧版不同，对象初始化器中的成员名可以接受。保留全部操作的库项目现在也会为开放
  门面调用触发泛型展开。已通过两种后端验证泛型类、两个类型形参、嵌套转发、profile、JSON、ref 读取和值类型泛型 CircleReference；
  `GenericForwardingWrapperTest` 覆盖这些情况。
- [x] **程序集标记的外部根。** 用 `[assembly: LuminPackable(typeof(T))]` 标记的被引用类型（以及被标记 Union 声明的 case）成为使用方
  程序集的载荷，与旧版 `CompilationTypeAnalysis` 和 `ReferencedTypeMetadata` 一致。声明了标记的编译通过导入全部元数据的视图分析，因此
  被引用类型的私有、internal、Include 和自动属性存储都通过布局镜像访问；其他程序集的 internal 存储走镜像而不直接访问。缺少后备字段的
  引用程序集报告 LuminPack112。导入的 Union 根通过其自身程序集生成的已编译派发写入，在本程序集读取各 case，且不在导入类型内部声明任何
  内容。与旧版不同，被标记类型的 internal 字段可以编译，被标记类型也可作为本地载荷的成员。`--verify-external` 通过二进制与 JSON
  双向覆盖记录、结构体、导入 Union 和未加特性的类型。
- [x] **LuminPack021。** 无需迁移：自布局镜像使 `[LuminPackInclude]` 存储在所有目标上可达以来，旧生成器只声明而不再报告该警告，Next
  同样不报告。

## 尚未迁移

### 静态绑定与发布

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 跨程序集载荷 | 程序集标记的外部根已迁移。旧版会为观察到的构造特化的开放泛型标记（如 `typeof(Box<>)`）以及跨程序集选项继承仍缺失。与旧版相同，使用方未标记的被引用程序集 `[LuminPackable]` 类型在被序列化时仍走注册回退。 |
| P2 | 其余后端组合 | 友元程序集的操作保留与单个语料上的 IL 重写已验证，但友元访问、IL 重写和禁用集成的静态后端组合仍需验证。在 .NET 8 及更高版本上，生成代码需要 C# 13（ref 字段、`ref readonly` 参数、ref struct 接口）。 |

### 生成源码

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 产物统计 | 尚未按构建记录生成文件数、源码体积和保留的操作。 |

### 类型形状与协议

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 泛型 CircleReference | 直接使用或作为成员的封闭构造会得到 owner 并保持引用身份，包括环和重复的列表元素（已通过拦截器验证 `Node<string>`、`Node<int>` 与 `Node<double>`）。经由源泛型方法到达的构造通过包装绑定。值类型形状、复杂构造、ref 返回租借、槽位演进和失败组合均未验证。 |
| P2 | 版本容忍组合 | 泛型与继承组合、压缩字段、旧名称、Schema 演进和完整的旧版交叉读取，只覆盖到测试所及范围。 |
| P2 | 公开注解 | `NonGenerator`、`LuminPackableObject` 与别名冲突仍需完整对照旧版语义。跨程序集选项继承仍属于跨程序集缺口。 |
| P2 | JSON 兼容性 | 各种封装、转义、旧名称、重复与未知属性、省略成员和失败发布，只覆盖到测试所及范围。 |
| P2 | Formatter 行为矩阵 | 各家族的比较器、重复项、并发观察、失败前缀、清理和旧运行时回退，只覆盖到测试所及范围。 |

### 输入限制

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 受检副本 | 仍只有声明了上限的根才请求受检副本。无上限的不可信根及其余 owner/profile 组合仍需迁移和验证；已完成的有上限内联范围见上方。 |
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
| P2 | Unity、IL2CPP、32 位、ARM64、AOT | 尚未用 Next 验证。Unity 所用的 netstandard2.1 与 C# 9 编译路径已由兼容目标检查覆盖，但尚未构建 Unity 项目。 |
| P2 | 可选模块 | 各平台与集成模块尚未针对 Next 构建或测试。 |

### 诊断

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 诊断语义 | 描述符均已存在；触发条件、优先级、数量、位置、相关声明、代码修复和 IDE 中不完整源码的行为尚未与旧版对照。 |

### 生成器性能

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 增量工作 | 各操作的共享 owner 依赖遍历、其余重复模型投影和语义查询仍待削减。已完成的规范化与缓存工作见上方。 |

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
