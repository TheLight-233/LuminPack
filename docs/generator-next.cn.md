# Next 生成器

[English](generator-next.md)

`src/LuminPack.Generator.Next` 是基于模型的新生成器。Benchmark 和运行时测试项目通过 `-p:LuminPackGenerator=Next` 选择它，也保留了旧生成器选项以便对照。发布包中的生成器仍是独立项目。

## 输出结构

一个增量生成器输出三个目录：

| 目录 | 内容 |
| --- | --- |
| `Binary` | 二进制入口、类型算法、Union 分派和拦截入口。 |
| `Json` | JSON 入口、类型算法和 JSON Union 分派。 |
| `Formatters` | 共享框架 Formatter、编译期配置和注册支持。 |

文件名沿用旧生成器的 `LuminPackGen.<命名空间>.<类型名>.g.cs` 规则，以声明的泛型参数个数替代具体类型参数。基础标量合并至 `LuminPackGen.System.g.cs`，数组合并至 `LuminPackGen.Array.g.cs`，同一框架泛型定义的不同构造合并到一个文件。

核心算法位于必要适配方法之前。保留的序列化便捷 API 位于各类型文件末尾；空的生成扩展类及其空命名空间片段不会输出。文件内使用别名标识静态读写容器，JSON 操作使用 `Json` 后缀。调用方重定向后移除纯转发方法，包括 `WritePolymorphismValue` 和 `DeserializeUnion`。编译器拦截和类型化委托适配保留必要签名。生成的实现名称不是应用程序入口。

普通对象与其 Union 载荷通过无状态 framing 类型参数共享算法。`LuminPackObjectFraming` 与 `LuminPackPayloadFraming` 位于 Core。不同封闭实例保留各自的 null／对象头处理、容量预留、读取检查和发布边界。异常时必须保留调用方游标的路径仍使用独立的 Reader／Writer 状态复制包装。

Union 写入为每种格式保留一个泛型虚方法，向下传递编译期选项。二进制额外保留一个非泛型默认配置虚槽，使常用路径不承担泛型虚调用的额外分派成本。封闭泛型子类型继续使用缓存的分派槽。Union 读取使用一套 case 分支处理新建和复用目标，每个分支判断现有实例是否正好匹配所需类型。

## 注册外部 Union 子类型

通过序列化入口注册：

```csharp
LuminPackSerializer.Register<IMessage, ExternalMessage>(
    77, WriteMessage, ReadMessage, WriteMessageJson, ReadMessageJson);
```

二进制写委托接收 `ref LuminPackWriter, ref IMessage`，读委托接收 `ref LuminPackReader, ref IMessage`；JSON 委托使用对应 JSON 状态。四个操作均必需。写委托负责完整的 Union 标签／JSON 外层结构，读委托从既有分派边界读取子类型载荷。

子类型必须实现或继承根类型。标签必须在 `UInt16` 范围内；生成器已占用的标签、重复标签和重复类型注册都会被拒绝。Core 中隐藏的 `LuminPackUnionFormatter<TUnion>` 统一持有表和生成标签预留信息，各根类型不再生成独立注册 API。普通自定义 Formatter 仍使用原有类型化 `LuminPackSerializer.Register<T>` 重载。

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
