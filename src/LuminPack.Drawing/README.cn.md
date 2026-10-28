<div align="center">

# 🎨 LuminPack.Drawing

**保留身份信息的 System.Drawing.Color 编解码器**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![System.Drawing](https://img.shields.io/badge/System.Drawing-0078D4?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [全部平台模块](../LuminPack.Platforms/README.cn.md)

[支持的类型](#-支持的类型) · [快速开始](#-快速开始) · [保证](#-保证) · [构建](#-构建)

</div>

---

`Color` 可以是 Empty、已知/系统颜色、命名颜色或显式 ARGB——只保存 ARGB 会丢失这些公开身份信息。基础生成的 DTO 本身没有 `Color` 格式化器。

## 📦 支持的类型

| 类别 | 类型 |
| --- | --- |
| 颜色 | `Color`——Empty、已知/系统颜色、命名颜色与显式 ARGB |

普通的绘图几何类型已由 Core 处理，本模块不再重复提供编解码器。

## 🚀 快速开始

### 直接调用编解码器

```csharp
using LuminPack.Drawing;

LuminDrawing.Register(); // 在并发序列化之前初始化一次。
var value = System.Drawing.Color.FromName("CustomColor");
byte[] bytes = LuminDrawing.ColorCodec.Serialize(value);
var restored = LuminDrawing.ColorCodec.Deserialize(bytes);
```

直接调用编解码器无需注册。

### 在生成类型中使用

对于普通生成 DTO 中的字段或集合元素，通过现有的 Custom 机制显式启用：

```csharp
using LuminPack.Attribute;
using LuminPack.Platforms;

[LuminPackable]
public partial class Snapshot
{
    public LuminPackValue<System.Drawing.Color> Color;
}
```

- `LuminPackValue<T>` 可与平台值隐式互相转换，不添加类型标识或对象帧。
- 在生成的入口使用它之前调用 `LuminDrawing.Register()`。
- 这些包装器的 JSON 回调会显式拒绝调用——平台模块只支持二进制。

### 未包装的字段

经过测试的 Core 基线已经会把普通的、未包装的 `System.Drawing.Color` 字段交给其不透明 BCL 格式化器回退路径。调用 `Register()` 后该路径可以直接工作，并有生成 DTO 的测试夹具验证。若希望由声明统一选定本模块的 schema，显式包装器依然有用。

### 线格式记录

颜色记录保留 Empty、已知/系统标识、未知名称和显式 ARGB：先写一个种类字节，再写对应的逻辑负载。该表示由专门的颜色 partial 文件负责。不会持久化任何绘图引擎、GDI 资源、`Font`、`Image` 或 `Bitmap` 句柄。

## ✅ 保证

- **读取前校验**——每个定长读取器都会检查完整范围，并在发布目标值或游标之前先构造候选值。完整记录的便捷读取器还会拒绝多余的尾部字节。
- **沿用 Core 的标量编码**——标量表示与字节序均遵循 Core。
- **不改变 Core 路径**——原始 unmanaged API 与普通未包装的生成字段保持 Core 原有行为；注册不会改写它们的契约。
- **只是值记录**——不涉及外部对象身份、引用环、原生资源、UI 控件、调度器、绑定或渲染器状态。框架构造函数仍保留各自的领域约束。

> [!WARNING]
> 这些公开分量记录是一套独立的 schema。不要把它与同一类型已有的原始/原生布局记录悄悄混用。

## 🔨 构建

先构建 Core，再基于现有的 **net8.0** Core 程序集构建这个可选项目：

```powershell
dotnet build src/LuminPack.Drawing/LuminPack.Drawing.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

该项目使用只读的程序集引用，绝不会构建 Core 或其生成器。Core 既不引用本模块也不引用共享适配器，这些源码项目也不会作为 NuGet 包发布。

关于针对性验证与支持边界，请参阅[共享平台适配器](../LuminPack.Platforms/README.cn.md)。
