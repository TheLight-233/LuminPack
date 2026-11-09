# 自动生成

[English](generation-modes.md) · [返回 README](README.cn.md)

LuminPack 采用统一的自动生成规则，不需要选择生成档位，也不需要配置 `CompilerVisibleProperty`。

- 每个 `[LuminPackable]` 声明提供自己的序列化成员图。库为成员图保留四种操作，以便其他程序集使用模型。
- 序列化调用保留其所需的具体负载，包括闭合泛型包装方法、容器元素和 Union case。
- 无关代码中观察到的未标记类型，不会仅因为是 public 就生成序列化入口。
- 便捷操作按实际调用生成。运行时选择目标或不透明的边界使用注册兜底。

## 操作选择

控制台或桌面应用程序，以及没有友元访问的 Unity `Assembly-CSharp` 和 `Assembly-CSharp-Editor`，按负载独立选择二进制写、二进制读、JSON 写和 JSON 读。只调用 `Serialize(player)` 时，保留 `Player` 及其依赖的成员图和 Union 图的二进制写入。另一个模型的 JSON 调用不会让 `Player` 生成 JSON。引用 LuminPack 运行库不影响这条规则。

库或声明了 `InternalsVisibleTo` 的编译保留已发现模型图的四种操作，因为调用方可能在当前编译外。直接调用序列化状态时，会为已发现的负载保守地保留对应方向。注册、直接调用生成的格式化器扩展方法或无法解析的序列化表达式可能保留全部四种操作。

对于引用程序集中的模型，在消费程序集声明 `[assembly: LuminPackable(typeof(ExternalModel))]`，让消费方的生成器分析该类型。如果某个操作是间接调用的，无法从源码推导，可以显式保留该模块：

```csharp
using LuminPack.Attribute;

[assembly: LuminPackable(typeof(ExternalModel))]
[assembly: LuminPackGeneratorOptions(ForceGenerateDeserializeJson = true)]
```

`LuminPackGeneratorOptions` 还提供 `ForceGenerateSerialize`、`ForceGenerateDeserialize` 和 `ForceGenerateSerializeJson`。四个属性默认均为 `false`，可独立启用，每个属性为程序集已发现的负载保留对应操作。具体便捷重载仍由绑定调用决定。运行时才选定的泛型调用在无法绑定具体入口时仍需要强类型注册。

## 静态拦截

直接调用序列化器时，生成器为对应的具体类型便捷入口生成 XML 文档。仅供特化包装方法使用的具体入口保留执行方法体，文档由共享泛型门面提供。

已移除的 `Sizeof` 和 `CalculateOffset` API 不再生成。直接分配结果数组的写入路径，仅为支持的记录形态保留内部具体类型 `MeasureRecord` 辅助方法；它不是通用求长接口，也不参与注册兜底。普通复杂对象、Custom 格式化器和 Union 不再生成求长占位方法。

生成器包自动启用 `LuminPack` Interceptor 命名空间。在 C# 11 或更高版本、编译器提供带版本的 `GetInterceptableLocation` 时，公共门面使用固定数量的泛型签名。`Serialize(value)`、`Serialize<Player>(value)` 和 `Deserialize<Player>(bytes)` 等调用被拦截到既有具体实现。相同构造签名的多个源码位置共享一个 Interceptor。内部状态入口使用负载专属名称，分配数组、缓冲区和流等便捷入口位于独立的 `LuminEntry_<Type>` 容器。只保留实际使用的便捷 API 类别；格式化器保留范围遵循前述统一声明与调用图规则。

显式配置使用两个类型参数，例如 `Serialize<Player, TOptions>(value)`。配置类型参数可以保持开放：它选择编译期选项，不在运行时选择负载格式化器。生成器不再为各负载类型生成公开的配置便捷重载。每个实际使用的便捷操作保留既有的编译期配置方法体，默认调用由 Interceptor 直接传入声明配置。这里使用的是既有选项 struct，不是格式化器 Plan。

对于 `static byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value)` 这样的源码包装方法，可在闭合调用处拦截到具体方法体副本。调用图按方法和类型定义建立索引，关联的类型实参一起替换。嵌套静态包装方法和普通递归共享各自的具体方法体，并保留原来的重载选择、调用者信息常量和副作用。

特化采用保守边界。实例或虚方法、引用程序集中的方法体、不可访问的依赖、显式静态初始化、异步或迭代器方法体、lambda 或局部函数、缩减形式的扩展调用、展开的 `params`，以及绑定依赖开放负载类型的运算符，均保留原路径。特化深度有上限。这些边界需要既有的[注册兜底](custom-formatters.cn.md#runtime-registration)；类型可达不会自动注册。静态路径和兜底都不增加 Runtime Plan 或反射工厂。只有显式注册和 Custom 兜底保留强类型委托，普通静态路径不保留委托。既有 Union 虚方法派发及泛型缓存槽位保持不变。

生成器仍以 Roslyn 4.3 为依赖下限；只有编译器宿主的可选能力发现使用反射，不会把反射加入生成的序列化代码。可显式禁用编译器 Interceptor，改用已安装的 IL 后端：

```xml
<PropertyGroup>
  <LuminPackUseInterceptors>false</LuminPackUseInterceptors>
</PropertyGroup>
```

NuGet 包同时安装 IL 后处理器。编译器不支持 Interceptor 时（包括 Roslyn 4.3 和 C# 9），生成器输出相同的具体入口及仅供构建使用的签名映射。MSBuild 在编译后、复制产物前替换匹配的本程序集静态 `call` 操作数。这条路径不需要生成具体公共重载，也不依赖 PDB 行号匹配；保留参数求值顺序，写入前校验参数和返回值的栈类型。Portable PDB 会同步保留；签名程序集需要原来的 `AssemblyOriginatorKeyFile`。不改写引用程序集、虚调用或方法组函数指针。

`LuminPackUseInterceptors=false` 且 `LuminPackUseIlRewriting` 保持启用时选择 IL 后端。同时关闭这两个属性才使用重载兼容后端：普通默认调用保留具体重载，显式配置调用使用已注册的泛型兜底。这些是编译接入开关，不是生成档位。直接安装 Analyzer DLL 时，需要自行安装相应接入。Unity 通过 `ILPostProcessor` 使用同一个改写器，参见 [Unity 安装步骤](unity.cn.md)。未安装后处理器时，不要定义 `LUMINPACK_IL_REWRITE`。

## 注册与迁移

注册仍是独立的策略：

```csharp
[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
```

默认允许强类型委托注册，与项目的 unsafe 编译设置无关。此设置不改变静态格式化器的选择。

删除 MSBuild 中的 `LuminPackGenerationMode` 及对应的 `CompilerVisibleProperty`。删除程序集特性的档位参数，仅在需要时保留注册策略。旧的 `Full`、`Medium`、`Light`、`Minimal` 枚举值和构造函数作为已废弃的兼容写法保留，但全部被忽略；在旧编译器上也使用相同的自动保留规则。

消费程序集应启用生成器，以建立自己的静态绑定。缺少可用元数据的引用实现、方法组和运行时选择的目标需要显式或 Custom 注册。类型可达不等于自动注册，缺少注册或操作时抛出 `LuminPackException`。

生成策略不改变数据格式、Reader 检查、状态归属、复用或 Union 派发。被拦截的具体 Root 使用声明所属的选项；注册兜底的文档边界使用消费程序集默认配置或显式配置。
