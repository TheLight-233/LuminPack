# IDE 代码修复

[English](code-fixes.md)

`LuminPack.Generator` NuGet 包自带诊断对应的 C# 快速修复。消费者仍然只安装 `LuminPack`，它会自动依赖生成器包：

```xml
<PackageReference Include="LuminPack" Version="2.0.0" />
```

在支持 Roslyn 代码修复的 IDE 中，将光标放到诊断处并打开快速操作（例如 Visual Studio 的 `Ctrl+.`），查看预览后应用修复。消费项目不需要另装 CodeFix 包或引用 Workspaces。命令行构建仍然报告诊断，不会自动修改源码。

## 支持的诊断

| 诊断 | 快速操作 |
| --- | --- |
| `LuminPack001` | 移除静态类型上的 `[LuminPackable]`。 |
| `LuminPack002`、`LuminPack014` | 给缺少标记的源码类型添加 `[LuminPackable]`，并将它及外层类型设为 partial。集合元素类型通过诊断元数据定位。 |
| `LuminPack005`、`LuminPack103` | 将不可访问的回调或 Custom 格式化方法设为 internal。 |
| `LuminPack006` | 移除非托管结构体方法上的序列化回调特性。 |
| `LuminPack007` | 移除重写属性上的 `[LuminPackInclude]` 和 `[LuminPackIgnore]`。 |
| `LuminPack008`、`LuminPack009` | 将类 Union 根设为 abstract，并移除 sealed。 |
| `LuminPack021` | 移除目标框架不支持的 `[LuminPackInclude]`。该诊断为旧目标契约保留，当前生成器不再发出。 |
| `LuminPack025` | 选择一个公共构造函数添加 `[LuminPackConstructor]`，并移除其他构造函数的选择标记。 |
| `LuminPack027` | 选择一个已有的非公共构造函数，将其设为 public。 |
| `LuminPack028`、`LuminPack029` | 将嵌套类型的各个 partial 声明设为 internal。 |
| `LuminPack033`、`LuminPack101` | 为池租用方法或 Custom 格式化方法添加 static。排除 abstract、virtual、override 和显式接口实现方法。 |
| `LuminPack034` | 移除托管集合成员上的 `[LuminPackCompress]`。 |
| `LuminPack035` | 跨源码文件将 Union 参与者和所有外层类型设为 partial。 |

常规操作支持文档、项目和解决方案范围的 Fix All。构造函数选择不提供 Fix All，每个类型都需要明确选择构造方式。添加 static、修改可访问性或将 Union 设为 abstract 后，可能还需调整应用代码；请查看预览并重新构建。

修复会保留其他特性和声明注释，识别特性别名，并使用完全限定名称添加特性。修改方法修饰符时，会一起更新 partial 方法的声明和实现，包括分布在不同文件中的情况。引用程序集的元数据不可编辑；缺少序列化标记的类型需要在当前项目中有可编辑声明。未支持的诊断继续保留原有提示，不自动推断成员顺序、Union 标签、序列化方法实现或模型设计。

## 项目与包结构

`src/LuminPack.CodeFixes` 的目标框架是 `netstandard2.0`，使用 `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.3.0，与生成器的 Roslyn 基线一致。`CodeFixProvider` 位于 Workspaces，C# 包还提供 C# 工作区服务。依赖均设置 `PrivateAssets="all"`。

生成器包在 `analyzers/dotnet/cs` 中同时包含 `LuminPack.Generator.dll`、`LuminPack.CodeFixes.dll` 及其 XML 文档。CodeFix 项目不单独打包；生成器中的项目引用只用于构建，不产生程序集引用或 NuGet 依赖。包内不携带 Roslyn 和组合服务 DLL，它们由 IDE 提供。运行时、生成器均不引用修复程序集或 Workspaces，修复程序集也不引用这两个产品程序集，因此编译器和 Unity 加载生成器不依赖 IDE 服务。Unity 需要能够加载 Roslyn CodeFix 程序集的编辑器集成；仅复制源生成器不会启用快速修复。

运行聚焦的工作区检查：

```text
dotnet run --project tests/LuminPack.Tests.CodeFixes/LuminPack.Tests.CodeFixes.csproj -c Release
```

检查覆盖修复前后的真实生成器诊断、源码编译、跨文档修改、别名、注释、取消操作和 Roslyn Fix All。旧诊断 `LuminPack021` 使用合成诊断测试，因为生成器已不再发出它。这些检查不会启动 IDE。
