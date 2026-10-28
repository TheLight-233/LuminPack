<div align="center">

# 📱 LuminPack.Maui

**MAUI Graphics 几何与颜色的二进制编解码器**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-512BD4?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [全部平台模块](../LuminPack.Platforms/README.cn.md)

[支持的类型](#-支持的类型) · [快速开始](#-快速开始) · [保证](#-保证) · [构建](#-构建)

</div>

---

基础生成器无法处理 MAUI Graphics 的构造函数和嵌套的 `Color`。这些编解码器覆盖可移植的图形数据类型，且不会引入移动端 UI 控件或工作负载。

## 📦 支持的类型

| 类别 | 类型 |
| --- | --- |
| 几何 | `Point` · `PointF` · `Size` · `SizeF` · `Rect` · `RectF` |
| 颜色 | `Color`（可为 null） |

## 🚀 快速开始

### 直接调用编解码器

```csharp
using LuminPack.Maui;

LuminMaui.Register(); // 在并发序列化之前初始化一次。
var value = new Microsoft.Maui.Graphics.Point(1.25, 2.25);
byte[] bytes = LuminMaui.PointCodec.Serialize(value);
var restored = LuminMaui.PointCodec.Deserialize(bytes);
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
    public LuminPackValue<Microsoft.Maui.Graphics.Point> Position;
}
```

- `LuminPackValue<T>` 可与平台值隐式互相转换，不添加类型标识或对象帧。
- 在生成的入口使用它之前调用 `LuminMaui.Register()`。
- 这些包装器的 JSON 回调会显式拒绝调用——平台模块只支持二进制。

### 颜色

`Color` 可为 null，写入一个标记以及四个公开的 float 分量。读取时构造新值；外部引用身份不属于协议保证。

## ✅ 保证

- **读取前校验**——每个定长读取器都会检查完整范围，并在发布目标值或游标之前先构造候选值。完整记录的便捷读取器还会拒绝多余的尾部字节。
- **沿用 Core 的标量编码**——标量表示与字节序均遵循 Core。
- **不改变 Core 路径**——原始 unmanaged API 与普通未包装的生成字段保持 Core 原有行为；注册不会改写它们的契约。
- **只是值记录**——不涉及外部对象身份、引用环、原生资源、UI 控件、调度器、绑定或渲染器状态。框架构造函数仍保留各自的领域约束。

> [!WARNING]
> 这些公开分量记录是一套独立的 schema。不要把它与同一类型已有的原始/原生布局记录悄悄混用。

## 🏭 代码生成

`Codecs.json` 负责审阅过的分量顺序。使用 `python src/LuminPack.Platforms/CodeGen~/generate_codecs.py` 重新生成确定性的标量源码，或加 `--verify` 进行检查。该生成器既不读取也不修改 Core；模块特有的记录保留在专门的 partial 文件中。

## 🔨 构建

先构建 Core，再基于现有的 **net8.0** Core 程序集构建这个可选项目：

```powershell
dotnet build src/LuminPack.Maui/LuminPack.Maui.csproj -c Release `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

该项目使用只读的程序集引用，绝不会构建 Core 或其生成器。Core 既不引用本模块也不引用共享适配器，这些源码项目也不会作为 NuGet 包发布。

关于针对性验证与支持边界，请参阅[共享平台适配器](../LuminPack.Platforms/README.cn.md)。
