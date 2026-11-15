# Next 生成器

[English](generator-next.md)

`src/LuminPack.Generator.Next` 是基于模型的新生成器。Benchmark 和运行时测试项目通过 `-p:LuminPackGenerator=Next` 选择它，也保留了旧生成器选项以便对照。发布包中的生成器仍是独立项目。

测试 NuGet 安装时，为 `src/LuminPack.Generator` 打包加上 `-p:LuminPackPackNext=true`，并以相同候选版本打包 Core。从本地源安装生成的 `LuminPack` 包，在消费项目中设置 `<LuminPackGenerator>Next</LuminPackGenerator>` 即可选择包内的 Next 分析器。省略该属性时，候选包默认使用旧生成器。可用 `.github/scripts/verify-next-package.ps1` 对候选源验证编译器拦截、IL 重写和注册回退。

## 输出结构

分析器使用 `LuminPack.SourceGenerator.dll`，以便 IDE 在同一个解决方案中同时加载新版和旧版的 `LuminPack.Generator.dll`。入口类型为 `LuminPack.SourceGenerator.LuminPackSourceGenerator`。一个增量生成器共用分析流程，并在 Roslyn 4.6 及以上输出三个目录：

| 目录 | 内容 |
| --- | --- |
| `LuminPack.SourceGenerator.LuminPackSourceGenerator` | 二进制入口、类型算法、Union 分派和拦截入口。 |
| `LuminPack.SourceGenerator.LuminPackJsonGenerator` | JSON 入口、类型算法和 JSON Union 分派。 |
| `LuminPack.SourceGenerator.LuminPackFormatterGenerator` | 共享框架 Formatter、编译期配置和注册支持。 |

较旧的 Roslyn 宿主使用带分类前缀的平铺文件名，例如 `LuminPack.SourceGenerator.LuminPackJsonGenerator.LuminPackGen.Model.g.cs`。Rider 可以把三类文件直接平铺在同一个生成器节点下。要查看磁盘上的目录结构，在消费项目运行 `dotnet build -p:EmitCompilerGeneratedFiles=true`，然后打开 `obj/<配置>/<目标框架>/generated` 下 Next 生成器的输出。

文件名沿用旧生成器的 `LuminPackGen.<命名空间>.<类型名>.g.cs` 规则，以声明的泛型参数个数替代具体类型参数。基础标量合并至 `LuminPackGen.System.g.cs`，数组合并至 `LuminPackGen.Array.g.cs`，同一框架泛型定义的不同构造合并到一个文件。

核心算法位于必要适配方法之前。保留的序列化便捷 API 位于各类型文件末尾；空的生成扩展类及其空命名空间片段不会输出。文件内使用别名标识静态读写容器，JSON 操作使用 `Json` 后缀。调用方重定向后移除纯转发方法，包括 `WritePolymorphismValue` 和 `DeserializeUnion`。编译器拦截和类型化委托适配保留必要签名。生成的实现名称不是应用程序入口。

普通对象与其 Union 载荷通过无状态 framing 类型参数共享算法。`LuminPackObjectFraming` 与 `LuminPackPayloadFraming` 位于 Core。不同封闭实例保留各自的 null／对象头处理、容量预留、读取检查和发布边界。异常时必须保留调用方游标的路径仍使用独立的 Reader／Writer 状态复制包装。

Union 写入为每种格式保留一个泛型虚方法，向下传递编译期选项。二进制额外保留一个非泛型默认配置虚槽，使常用路径不承担泛型虚调用的额外分派成本。封闭泛型子类型继续使用缓存的分派槽。Union 读取使用一套 case 分支处理新建和复用目标，每个分支判断现有实例是否正好匹配所需类型。

## Owner 结构体与共享泛型 Formatter

生成的 Formatter owner 是只含静态操作、无字段的 `readonly` 结构体。Core 为每种操作声明一个接口：`ILuminPackWriter<T>`、`ILuminPackReader<T>`、`ILuminPackJsonWriter<T>` 和 `ILuminPackJsonReader<T>`。这些成员在所有目标框架上都是实例成员，共享代码通过 `default(TFormatter).Write<TOptions>(...)` 调用。调用在每个实例化中绑定到确切的 owner，既不装箱也不分派，转发方法内联后空接收者也随之消失。不使用 static abstract 成员：NativeAOT 无法编译在引用类型元素上共享的泛型代码中的静态虚调用。

所有已注册序列与键值家族的封闭实例（例如 `List<T>`、`HashSet<T>`、`Queue<T>`、`Stack<T>`、`SortedSet<T>`、`ImmutableArray<T>`、`IList<T>`、`Dictionary<TKey, TValue>`、`SortedDictionary<TKey, TValue>`、`ConcurrentDictionary<TKey, TValue>`）以及数组，按家族、数组秩与元素类别组合共享一个泛型 owner。与封闭发布一致，原始元素使用家族的标量模板，其他元素使用其通用元素模板。每个泛型参数分类如下：

| 类别 | 规则 | 泛型形式 |
| --- | --- | --- |
| Unmanaged | 封闭 owner 直接复制其存储（原始标量、普通 unmanaged 结构体、`Native` 布局） | 带 `unmanaged` 约束的类型参数；JSON 操作额外带策略参数 |
| String | `string` | 保持字面类型，字符串内核不变 |
| Element | 其他拥有自身 Formatter 的非多态元素，包括 `Tagged`、`Packed` 结构体与数组 | 类型参数加由元素 owner 实现的 `struct` 策略参数 |

`Dictionary<int, int>` 与 `Dictionary<long, double>` 共享 `DictionaryOfUnmanagedAndUnmanagedFormatter<TKey, TValue, ...>`，`Dictionary<int, string>` 使用 `DictionaryOfUnmanagedAndStringFormatter<TKey, ...>`，`Dictionary<string, SimpleClass>` 使用 `DictionaryOfStringAndElementFormatter<TValue, TValueFormatter>`。嵌套集合属于 Element：`Dictionary<int, List<string>>` 以 `List<string>` 的 owner 作为策略。

集合只负责容器结构，元素的布局与编码留在元素自身的 owner 中：

- 连续集合中的 `Tagged` 或 `Packed` 元素保留块传输。Core 的 `ILuminPackBlockWriter<T>` 与 `ILuminPackBlockReader<T>` 提供块宽度、校验、写入和读取，元素 owner 用封闭传输实现它们：布局描述、向量化前缀和标量尾部。因此 `List<TagA>` 与 `TagB[]` 分别共享 `ListOfLayoutFormatter<T, TFormatter>` 和 `ArrayOfLayoutFormatter<T, TFormatter>`。模板中写出的布局打包宽度保留在共享渲染中，宽度不同的元素使用不同的共享 owner。
- 嵌套数组是元素，其策略是内层数组的 owner，而内层 owner 本身也是共享的。`int[][]` 与 `long[][]` 共享 `ArrayOfElement2Formatter<T, TFormatter, TCompressedFormatter>`。数组或列表的元素为数组时，其压缩根方法调用第二个策略 `IntArrayCompressedFormatter`，后者转发到内层数组的压缩入口。
- 矩形数组按秩共享行优先模板：`int[,]` 与 `long[,]` 使用 `Array2DOfUnmanagedFormatter<T, TFormatter>`，`SimpleClass[,,]` 使用 `Array3DOfElementFormatter<T, TFormatter>`。原始块、布局块与逐元素循环沿用封闭规则。

Union、接口、`object`、布尔与可空元素保留各自的专用封闭 owner，因此多态分派和布尔校验保持不变。只有 Core 声明了块接口时，带布局的元素才会共享。

泛型 owner 由该家族自身的模板以占位类型名渲染，算法与封闭版本一致。只有占位渲染结果和操作集合完全相同的封闭 owner 才会共享；渲染不同时使用带编号的名称。封闭 owner 保留其状态扩展方法，其根方法变为转发，发布阶段将其解析为文件别名，例如 `using DictionaryOfIntAndIntWriter = ...DictionaryOfUnmanagedAndUnmanagedFormatter<int, int, IntFormatter, IntFormatter>;`，调用点写法不变。策略成员发布在 `LuminPackGen.FormatterStrategies.g.cs`，执行的元素调用与封闭 owner 完全相同。若封闭 owner 的入口或扩展需要共享 owner 不具备的根方法，或家族转发到另一个封闭 owner（例如 `IDictionary<TKey, TValue>`），则保留封闭 owner。只被一个封闭 owner 使用的形态同样保留封闭 owner。

共享 owner 的所有根方法均为 `NoInlining`。以一次集合调用换取循环内元素调用的内联预算：若集合根方法被内联，循环会复制到每个调用方，循环内的元素调用反而无法内联。策略转发方法和载荷记录写入仍可内联。

## 导入引用程序集中的载荷

在消费程序集声明 `[assembly: LuminPackable(typeof(Box<>))]`，可为序列化调用或成员用到的闭合 `Box<T>` 生成 owner。使用 `typeof(Box<int>)` 选择单个构造。标记的记录继承消费程序集选项，并应用类型和成员自身的选项。引用程序集元数据缺少所需的自动属性存储时，生成器报告 `LuminPack112`。此时请引用实现程序集。

对于由 Next 生成且显式声明闭合 case 的库中泛型 Union，消费程序集按声明的标签读取，定义程序集的编译期 Union 槽负责写入。这类 Union 支持开放和精确根标记。

引用成员标记 `[LuminPackableObject]` 后使用完整的生成或注册 Formatter。接口即使没有声明 `[LuminPackable]`，其成员也可使用手动注册的 Formatter。

## 注册外部 Union 子类型

通过序列化入口注册：

```csharp
LuminPackSerializer.Register<IMessage, ExternalMessage>(
    77, WriteMessage, ReadMessage, WriteMessageJson, ReadMessageJson);
```

二进制写委托接收 `ref LuminPackWriter, ref IMessage`，读委托接收 `ref LuminPackReader, ref IMessage`；JSON 委托使用对应 JSON 状态。四个操作均必需。写委托负责完整的 Union 标签／JSON 外层结构，读委托从既有分派边界读取子类型载荷。

子类型必须实现或继承根类型。标签必须在 `UInt16` 范围内；生成器已占用的标签、重复标签和重复类型注册都会被拒绝。Core 中隐藏的 `LuminPackUnionFormatter<TUnion>` 统一持有表和生成标签预留信息，各根类型不再生成独立注册 API。普通自定义 Formatter 仍使用原有类型化 `LuminPackSerializer.Register<T>` 重载。

`LuminPackSerializer` 上的闭合负载调用直接绑定到生成的静态 Root。编译器或已安装的 IL 后处理器按实际使用的负载与门面签名共用拦截器，同一签名的显式配置共用泛型拦截器。文档拦截器负责输入检查、缓冲和状态的生命周期。生成的 XML 文档位于公开门面上。

源码泛型方法中的序列化调用若使用开放负载类型，仍执行原方法体。启用注册时，首次使用 `LuminPackSerializer` 会初始化 Core 中内置的标量和字符串 Formatter、本程序集的 Custom Formatter，以及 Next 为当前程序集生成的闭合类型操作。这包括已发现的 `Dictionary<int, string>` 等集合。显式注册优先。生成范围之外的类型或操作仍需显式注册。指定显式配置时传入两个类型实参。

## 增量行为与验证

声明使用 Roslyn 按特性索引的增量提供器。程序集设置与已绑定的序列化调用作为独立、按值比较的输入。驱动器快照保留不含编译器对象的模型事实、声明和最终文件；未变更的文件复用已解析的方法引用及精确操作依赖。发布阶段直接使用写入器记录的声明位置，不建立第二个编译，也不重新绑定生成代码。

临时源码构建使用独占、有大小上限的数组池租约。Span 操作填充和复制已验证的字符范围，语句对齐的临时整数表也从池中租用。成功、失败和取消都会释放租约，超大缓冲区不回池保留。发布字符串独立拥有字符存储。最终字符串、规范化模型和 Roslyn 语法仍会分配，因此整个生成器不是零分配。发布热循环使用显式迭代，减少 LINQ 管道。

```text
dotnet run --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -c Release -- --verify-publication
dotnet run --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -c Release -- --types 256 --runs 9 --warmups 8 --old-dll <reference-generator.dll> --old-mode off
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-roundtrip
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
```

对照 DLL 可以是修改前的 Next，也可以是旧生成器。比较时会预热两个生成器，交替执行，并同时打印每次耗时、中位数、完整极差和分配量中位数。使用 --reference-first 反转首次执行顺序。对相同输出架构的冻结版本使用 --verify-equivalence，可比较全量生成、修改注释／成员、删除和恢复声明后的文件名、源码及诊断。应同时比较完整生成、两类增量编辑、生成文本、分配量、线格式样本和运行时测量。源代码体积缩小本身不能证明运行时或生成器性能。
