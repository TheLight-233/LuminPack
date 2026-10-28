<div align="center">

# 🎮 LuminPack.Unity

**Unity 值类型、曲线与渐变的编解码器**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![netstandard2.1](https://img.shields.io/badge/target-netstandard2.1-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Unity](https://img.shields.io/badge/Unity-Mono%20%7C%20IL2CPP-000000?style=flat-square&logo=unity&logoColor=white) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [Unity 指南](../../docs/unity.cn.md) · [Burst 后端](../LuminPack.Burst/README.cn.md)

[支持的类型](#-支持的类型) · [快速开始](#-快速开始) · [工作方式](#-工作方式) · [安全性](#-安全性) · [安装与构建](#-安装与构建) · [测试](#-测试)

</div>

---

可选的 Unity 值类型编解码器，独立于 `LuminPack.Burst`。Core 不引用任何一个扩展。运行时程序集面向 netstandard2.1，并使用 `UnityEngine.CoreModule`。

## 📦 支持的类型

| 类别 | 类型 |
| --- | --- |
| 向量与旋转 | `Vector2` · `Vector3` · `Vector4` · `Vector2Int` · `Vector3Int` · `Quaternion` |
| 颜色 | `Color` · `Color32` |
| 包围盒与矩形 | `Rect` · `RectInt` · `Bounds` · `BoundsInt` · `RangeInt` |
| 几何 | `Ray` · `Ray2D` · `Plane` · `Matrix4x4` · `LayerMask` |
| 动画关键帧 | `Keyframe` · `GradientColorKey` · `GradientAlphaKey` |
| 原生所有者对象 | `AnimationCurve` · `Gradient` · `RectOffset`（通过公开 API 值实现；绝不序列化原生句柄） |

<details>
<summary><b>分量布局</b></summary>

- 值以具名标量分量写入，不带对象帧。
- `Matrix4x4` 使用列主序分量顺序。
- `Bounds` 存储中心和半尺寸；`BoundsInt` 存储位置和尺寸。
- 射线通过两个连续的向量存储视图保留方向的原始位，而不是调用会归一化的 setter。
- `AnimationCurve` 写入对象标记、前/后循环模式以及带计数的 32 字节 `Keyframe` 记录。`Keyframe` 通过 Unity 公开属性保留时间、值、入/出切线、编辑器切线模式、加权模式以及入/出权重；编辑器切线元数据是否可用以及其含义由 Unity Player 的语义决定。
- `Gradient` 包含模式、`colorSpace`、带计数的颜色键和透明度键。`RectOffset` 包含 left/right/top/bottom。

这些记录遵循 LuminPack 线格式。面向字段的编解码器是一种显式表示：两端必须一致地选择它们。

</details>

## 🚀 快速开始

```csharp
using LuminPack.Unity;

LuminUnity.Register(); // 幂等的手动入口；在场景加载前也会自动调用。
LuminUnity.Write(ref writer, position);
LuminUnity.Read(ref reader, ref position);
```

当某个 schema 需要这些精确的字段编解码器时，可以在自定义格式化器中使用 `LuminUnity.Write/Read`。

## 🔧 工作方式

- **自动注册**——静态类带有 IL2CPP 预先构造特性，`Register` 在 `BeforeSceneLoad` 时运行。注册会启用现有的泛型格式化器回退，并保留应用已注册的格式化器。
- **生成器转发**——生成器会把 `AnimationCurve`、`Gradient` 和 `RectOffset` 的操作转发给本模块，普通字段和集合元素都是如此，且不会检查原生句柄。生成包含这些类型的契约时请引用本模块。
- **仍由 Core 主导**——其他值类型契约保留 Core 的生成与 unmanaged 路径；注册绝不会强迫原始 unmanaged 实例调用改走格式化器。Writer/Reader 与源码生成器仍归 Core 所有——不存在替代的序列化器、类型 ID 映射或分派机制。
- **只支持二进制**——编解码器安装的是二进制和精确大小回调；生成的原生所有者 JSON 操作会显式拒绝调用。

> [!NOTE]
> 资源对象、场景引用、GameObject、纹理和原生句柄都不会被序列化。

## 🔒 安全性

- 每个编解码器都会预留完整的输出空间，并在访问任何定长字段之前检查完整输入。读取时先在本地暂存值，只有全部成功后才发布。
- 原生所有者读取会在触碰目标之前暂存本地游标和全部负载值。在分配键数组之前会先检查计数和完整负载。完整读取会复用已有的原生所有者；null 记录则赋值为 null。
- 编解码器不会给 Core 的原生标量表示强加可移植字节序。

> [!WARNING]
> - 底层 netstandard Reader/Writer 的存储在整个生命周期内（包括分配与回调期间）都必须保持固定。生成的便捷入口仍承担其原有的固定职责。
> - 记录校验通过之后，原生 API 抛出的异常仍可能影响已有的所有者——Unity 原生对象没有通用的事务机制。
> - 计算大小和写入操作会读取公开的原生状态，因此不要并发修改所有者。

## 📥 安装与构建

以源码方式集成到 Unity 时，把 `Runtime` 及其 asmdef 与 netstandard Core DLL 及其必需依赖一起导入。不要再同时导入 LuminPack.Unity 的预编译副本。

该项目有意使用只读程序集引用，因此构建它绝不会重新构建 Core，也不会对 Core 运行生成器：

```powershell
dotnet build src/LuminPack.Unity/LuminPack.Unity.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

- `UnityManagedPath` 来自已安装的编辑器；其引擎 DLL 不会被复制到扩展的分发内容中。
- `LuminPackCorePath` 默认指向现有的 Release/Debug netstandard2.1 Core 输出。
- 由于 Unity 引擎引用必须由宿主提供，该 .NET 项目不是独立的 NuGet 分发包。

## 🧪 测试

| 测试夹具 | 检查内容 |
| --- | --- |
| `Tests~` 控制台项目（Unity 会忽略） | 真实的 Unity 引擎值类型、每一个被截短的输入窗口、完成后的游标位置、精确大小、二进制往返稳定性、哨兵字节、重复注册，以及 21 个定长值编解码器。需要相同的构建属性。 |
| `Tests~/Editor/UnityObjectContract.cs` | 在 Unity 中真实构造曲线/渐变/偏移、复用与全新读取、每一个截断窗口、null 帧、伪造计数以及曲线求值 |
| `Tests~/Consumer` | 构建一个包含原生所有者字段、数组和列表的真实生成契约，然后在 Unity 中运行该程序集 |

手动触发的扩展工作流会把这些测试夹具与原生 Burst 内核一起运行。

> [!IMPORTANT]
> 控制台宿主只能证明托管宿主上的行为，不能证明 Unity Player 或 IL2CPP 上的执行。在已部署的协议中启用这些编解码器之前，请补充真实引擎版本与运行时的测试——尤其是 `Ray` 的存储假设。
