# 🎮 Unity、IL2CPP 与 NativeAOT

[English](unity.md) · [返回 README](../README.cn.md)

## Unity

LuminPack 通过 `netstandard2.1` 资产支持 Unity Mono 和 IL2CPP。请把 API 兼容级别设为 **.NET Standard 2.1**。

可以用 NuGetForUnity 等 NuGet 工作流安装 `LuminPack` 包，或者手动导入：

1. 把包中 `netstandard2.1` 版本的 `LuminPack.Core.dll` 及其依赖放到 `Assets/Plugins`。
2. 把 `LuminPack.Generator.dll` 放到 `Assets/RoslynAnalyzers`，并在 Inspector 中为它添加 `RoslynAnalyzer` 标签。
3. 声明一个 `[LuminPackable]` 类型并确认项目能编译，以此确认 Unity 运行了生成器。

在 `netstandard2.1` 上，读写游标是原生指针。生成的入口会在整个调用期间固定（pin）托管输入；你自己在托管内存上创建的底层读写器，需要在其整个生命周期内保持内存固定。

32 位 IL2CPP 规则贯穿始终：大于 4 字节的固定值用块复制搬运，绝不使用类型化的非对齐访问，后者可能让 IL2CPP 崩溃。

发布前请在每个目标平台运行一次 IL2CPP Player；编译成功并不够。仓库中的 `tests/Unity` 工程是 Mono/IL2CPP 冒烟测试。

### 可选的 Unity 包

| 包 | 用途 |
| --- | --- |
| `LuminPack.Unity` | Unity 值类型的编解码：向量、`Quaternion`、`Color`、`Rect`、`Bounds`、`Matrix4x4`、`LayerMask`、`AnimationCurve`、`Gradient`、`RectOffset` 等。见[它的 README](../src/LuminPack.Unity/README.md)。 |
| `LuminPack.Burst` | `netstandard2.1` 原生内核表的 Burst 1.8 后端（复制、UTF-8/Latin-1 文本、布局转换、压缩和 ChaCha20）。见[它的 README](../src/LuminPack.Burst/README.md) 和 [Hook ABI](burst-hooks.cn.md)。 |

Core 不引用这两个包。它们都在场景加载前完成注册。

## NativeAOT 与裁剪

生成的代码不使用反射，所以序列化在 NativeAOT 和完全裁剪下都能工作。`tests/LuminPack.Tests.Aot` 在 CI 中发布并运行裁剪版和原生版可执行文件。

`System.Type` 是例外：在 .NET 8+ 上，如果运行时不支持动态代码（NativeAOT），读取非空的 `Type` 会抛出 `PlatformNotSupportedException`。写入 `Type` 和读取 `null` 仍然可用。支持动态代码时按名称解析类型，其元数据必须在裁剪后保留。

如果你使用 `Full` 生成模式并完全裁剪，且不需要动态代码，请设置 `<DynamicCodeSupport>false</DynamicCodeSupport>`。保持动态代码开启时，完全裁剪会对按名称解析 `Type` 报告 IL2057，需要你先决定如何保留这些元数据。
