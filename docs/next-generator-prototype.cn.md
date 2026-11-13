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
安全编译及默认 JSON 回退的输入策略。六个应用语料覆盖编译器拦截、带可移植符号的 IL 重写、关闭两种静态绑定后的注册回退，每种模式均覆盖有无
友元程序集声明。`--verify-external` 编译一个库和一个用 `[assembly: LuminPackable(typeof(T))]` 标记该库类型的使用方，
双向比较其字节与库自身操作的结果，并在引用程序集上期望 LuminPack112。

## 调用如何绑定

泛型二进制与 JSON 门面（包括 `Register` 和仅用于注册的 `FormatterDispatch`）作为 post-initialization 源码发布，调用点在生成器运行期间即可绑定。
语法提供器把每个封闭调用记录为值相等的事实：重载、载荷、封闭或声明的 profile（或开放的 profile 类型参数）以及编译器给出的拦截位置。拦截器处理
文档边界并直接调用已发布的静态根。载荷开放或缺少已发布根的调用保留在泛型门面上，使用注册派发。

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

- [x] **门面 XML 文档。** 公开门面包含形参、类型形参、返回值、缓冲区归属、复用和流生命周期说明。载荷专属便捷重载及其 XML 已从迁移范围中移除。
- [x] **程序集注册策略。** `LuminPackGeneratorOptions` 的两个构造函数采用相同的 `Auto`/`Disabled` 语义。禁用注册时不发布门面注册 API，
  不查询泛型或 Union 注册，也不向注册表预留 Union 标签；已知 Union case 沿用原有派发。
- [x] **程序集操作保留设置。** 聚焦矩阵覆盖全部十六种 `ForceGenerate*` 组合、被忽略的旧模式值、库与友元保留，以及禁用 unsafe 时的注册能力。
- [x] **程序集回退文档边界。** 默认泛型 JSON 读取采用程序集的 JSON 输入 profile；UTF-8 与 UTF-16 的严格数值语法和编码输入上限均已验证，
  包括没有任何特性载荷的编译。
- [x] **有上限普通对象的受检副本。** 已支持的副本将根策略递归传入可信内联记录及 List、数组、Dictionary 的载荷元素。六种 profile 的语料覆盖
  标记、Boolean 域、配额、空值、普通线格式与复用；完整目标预热后的复用读取不分配内存。其余载荷种类仍列在下方。
- [x] **规范化增量发布。** 收集事实按值比较，重复封闭根共享编译内分析，Formatter 需求不变时复用 driver 所有的目录，owner 聚合保留首次声明且不使用
  LINQ 分组。
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
- **源泛型方法。** 源泛型方法中的开放门面调用走注册回退。
- [x] **程序集标记的外部根。** 用 `[assembly: LuminPackable(typeof(T))]` 标记的被引用类型（以及被标记 Union 声明的 case）成为使用方
  程序集的载荷，与旧版 `CompilationTypeAnalysis` 和 `ReferencedTypeMetadata` 一致。声明了标记的编译通过导入全部元数据的视图分析，因此
  被引用类型的私有、internal、Include 和自动属性存储都通过布局镜像访问；其他程序集的 internal 存储走镜像而不直接访问。缺少后备字段的
  引用程序集报告 LuminPack112。导入的 Union 根通过其自身程序集生成的已编译派发写入，在本程序集读取各 case，且不在导入类型内部声明任何
  内容。与旧版不同，被标记类型的 internal 字段可以编译，被标记类型也可作为本地载荷的成员。`--verify-external` 通过二进制与 JSON
  双向覆盖记录、结构体、导入 Union 和未加特性的类型。
  对于普通外部记录，`typeof(Box<>)` 等开放泛型标记允许为调用和成员中发现的闭合构造生成 Formatter owner。`typeof(Box<int>)` 等闭合标记
  只选择对应构造。语料也在启用操作裁剪的应用中覆盖了两种标记。
  定义库也使用 Next 时，显式声明闭合 case 的泛型 Union 支持两种根标记。写入调用定义程序集的编译期槽，读取使用消费程序集生成的 case owner。语料覆盖二进制、
  JSON、集合成员，以及实现闭合泛型接口的非泛型 case。它还验证开放标记发现闭合记录时的 LuminPack112，以及消费程序集选项与导入类型选项的叠加。
- [x] **LuminPack021。** 无需迁移：自布局镜像使 `[LuminPackInclude]` 存储在所有目标上可达以来，旧生成器只声明而不再报告该警告，Next
  同样不报告。

## 产物统计

仓库内构建可传入 `-p:LuminPackReportGeneratorOutput=true -p:EmitCompilerGeneratedFiles=true`。Next 在编译器的生成文件目录下写入
`Formatters/LuminPackGen.OutputReport.g.cs`，按类别统计最终文件数和源码大小，报告本身不计入，并统计完整载荷与名称无冲突的共享根所保留的操作方向。

## 尚未迁移

### 静态绑定与发布

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 跨程序集载荷 | 仅从定义程序集源码发现的 Union case、开放泛型 case，以及更广泛的选项和后端组合仍需覆盖。旧版生成的 Union 槽签名与当前导入器检查的 Next 编译期槽签名不同。未标记的引用载荷使用注册回退。 |
| P2 | 其余后端组合 | 现代应用矩阵覆盖三种绑定模式及有无友元声明。独立友元程序集中的调用、较旧的编译器与目标框架组合，以及安装包集成仍需针对 Next 验证。在 .NET 8 及更高版本上，生成代码需要 C# 13（ref 字段、`ref readonly` 参数、ref struct 接口）。 |

### 类型形状与协议

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | 泛型 CircleReference | 直接使用或作为成员的封闭构造会得到 owner 并保持引用身份，包括环和重复的列表元素（已通过拦截器验证 `Node<string>`、`Node<int>` 与 `Node<double>`）。开放调用走注册回退。值类型形状、复杂构造、ref 返回租借、槽位演进和失败组合均未验证。 |
| P2 | 版本容忍组合 | 泛型与继承组合、压缩字段、旧名称、Schema 演进和完整的旧版交叉读取，只覆盖到测试所及范围。 |
| P2 | 公开注解 | `LuminPackableObject` 为生成或注册的引用载荷选择完整成员 Formatter。注解语料覆盖手动注册的 `NonGenerator` 成员和未加类型注解的接口。与 `LuminPackCompress` 同用时，已知值类型的交错数组及包含这些数组的列表沿用旧版元素编码和版本表分类。`--verify-alias-diagnostics` 用已构建的旧版分析器对照七种非法名称。更广泛的诊断位置和优先级仍待验证。 |
| P2 | JSON 兼容性 | 各种封装、转义、旧名称、重复与未知属性、省略成员和失败发布，只覆盖到测试所及范围。 |
| P2 | Formatter 行为矩阵 | 各家族的比较器、重复项、并发观察、失败前缀、清理和旧运行时回退，只覆盖到测试所及范围。 |

### 输入限制

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 受检副本 | 声明上限或在模型中依赖可信读取的根可以将已知嵌套读取重定向到根持有的受检副本。泛型声明可将该策略传给静态已知的子成员，已发布的闭合泛型类和托管结构体 owner 则在代入类型参数后应用该策略。语料通过拦截和生成注册表覆盖闭合根，也验证了显式配置下的字符串上限。转发型数组和列表 owner 将根限制传给内层数组、合并复制与校验的 Boolean 数组读取，以及字符串列表，并保留复用。开放类型参数派发、不透明回退及更多容器组合仍待处理。 |
| P1 | 未覆盖的载荷种类 | 读取点检查覆盖版本容忍记录、镜像字符串存储、引用跟踪记录和托管结构体。显式不可信的结构体和 Union 根拒绝由输入选择的类型。无上限 Union 将值域检查应用于可信的定长 case 和受支持的独立读取入口。引用跟踪 Union case 继承 Union 限制并保留槽表格式。直接成员及数组、列表和字典中的本地非泛型 Union 继承外层记录限制，覆盖首次读入列表和字典的路径。本地闭合泛型 Union 成员和列表元素在代入 case 类型参数后应用同样的限制，并保留泛型定义的标签宽度。具有显式 case 元数据和完整存储模型的跨程序集 Union 成员也继承外层限制，包括通过开放泛型程序集标记访问的闭合成员。开放泛型成员读取、仅在源程序集源码中发现的导入 case，以及更广泛的 profile 组合仍待覆盖。 |
| P2 | 流、异步与压缩 | 二进制异步流语料已检查编码字节上限及单字节预读。输入限制语料检查压缩数组、压缩交错数组和列表的各层，以及跨版本读取交错字段时声明的元素个数上限。其余协议和复用组合仍需覆盖。 |

### 性能机制

以下各项保持线格式不变，但生成的代码与旧版不同。

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P1 | 嵌套记录展开 | 旧版内联展开嵌套记录，包括含镜像成员、回调、版本容忍或池化构造的记录；Next 调用其 owner。 |
| P1 | 记录正文中的集合成员 | 记录正文对集合成员（如 Union 值的 List 或 Dictionary）调用其 owner，旧版在原位展开这些写入。 |
| P1 | 相邻定长字段整块复制 | 普通记录的定长字段组和符合条件的 CircleReference 字段组采用 64 位字段地址证明及逐字段回退。CircleReference 覆盖预留写入、顺序写入和同表读取。其余布局、构造和协议组合仍需对照。 |
| P1 | 非托管根共享 | 旧版通过替换证明和绑定证明共享封闭泛型与数组根；Next 为每个类型单独发布根。 |
| P2 | 根内联策略 | 返回新数组的静态入口会将 UTF-16 长度前缀的时区快照，以及标量 `IList<T>`、`ISet<T>` 和 `IReadOnlySet<T>` 的匹配实现直接写入结果数组。其他配置或实现继续使用池化路径。二进制 `List<string>` 读写与 `SortedSet<string>` 读取沿用旧版的 `NoInlining` 边界。标量枚举、集合和列表接口使用 JIT 默认读取策略，并保留独立的写入入口。其余集合及元素组合仍需对照。 |
| P2 | JSON 读取分配 | Benchmark 对象图的 JSON 读取比旧版多分配约 2 KB，来源尚未定位。 |

### 平台与目标框架

| 优先级 | 缺口 | 当前状态 |
| --- | --- | --- |
| P2 | Unity、IL2CPP、32 位、ARM64、AOT | Next 在 Windows .NET 10 的 x64 JIT、裁剪、NativeAOT 构建及独立的 x86 JIT 进程中通过了冒烟测试与保留的线格式样本。构建分析器仍报告动态 `System.Type` 解析警告。兼容目标检查覆盖 Unity 所用的 netstandard2.1 与 C# 9 编译路径。Unity 实际运行和其余运行时及架构组合仍待验证。 |
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

AOT 冒烟项目通过 `-p:LuminPackGenerator=Next` 选择 Next，构建产物放在独立目录。保留的 18 个旧版二进制样本均通过读取和逐字节写入比对，
新构建的旧版和 Next 测试程序也通过了对方写出样本的验证。更广泛的线格式与诊断语料、其余平台和语言组合，以及模糊测试仍待验证。安装包测试继续使用包内的旧版生成器。

```text
dotnet run --project tests/LuminPack.Tests.Aot -c Release -p:LuminPackGenerator=Next -- --wire verify tests/LuminPack.Tests.Aot/Fixtures
```

## 执行约束

先阅读仓库 AGENTS、机制契约、架构和贡献指南。沿用 Analysis → 不可变值模型 → Render，Roslyn 符号保留在分析边界与相应生命周期的缓存中。

复用已迁入的 Formatter 和渲染器。按开放类型定义、存储事实及能力选择算法，默认元素操作递归调用具体静态 owner。保留用户选定的家族目录组织与特殊化
扩展位置。

每个已迁移功能继续遵守机制契约，包括协议、Local + As、引用表、构造与复用、回调、typed delegates、布局、Release 检查和状态归属。判断性能是否持平时，
先对照生成代码和 JIT 反汇编，再运行 Benchmark。

生成代码的空格与换行可以不同。按仓库约定提交完成的改动，并保留工作区中其他人的修改。
