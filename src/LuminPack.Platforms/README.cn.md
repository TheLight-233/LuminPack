<div align="center">

# 🧩 LuminPack.Platforms

**所有可选平台模块共用的类型化适配器**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md)

[模块](#-模块) · [工作方式](#-工作方式) · [保证](#-保证) · [构建与测试](#-构建与测试)

</div>

---

为可选平台模块提供共享的类型化适配器。Core 不依赖其中任何项目。适配器调用 Core 现有的 Reader/Writer/Evaluator 操作，不引入反射序列化器、运行时类型映射、SIMD 算法或替代的布局机制。

## 📦 模块

| 模块 | 序列化内容 |
| --- | --- |
| 🌐 [LuminPack.AspNetCore](../LuminPack.AspNetCore/README.cn.md) | MVC/Minimal API 的二进制请求体，以及逻辑 HTTP 值类型（含多值与 null 元素的请求头） |
| 🖼️ [LuminPack.Avalonia](../LuminPack.Avalonia/README.cn.md) | 公开的不可变几何、相对坐标、颜色与完整矩阵 |
| 🎨 [LuminPack.Drawing](../LuminPack.Drawing/README.cn.md) | WinForms/System.Drawing 数据，包括命名/系统/空颜色的身份信息 |
| 🪟 [LuminPack.Wpf](../LuminPack.Wpf/README.cn.md) | 公开几何、Empty 哨兵值、变换、网格尺寸与线性 scRGB 颜色 |
| 📱 [LuminPack.Maui](../LuminPack.Maui/README.cn.md) | MAUI Graphics 可移植几何与可为 null 的托管颜色，不依赖移动端 UI |
| 🕹️ [LuminPack.MonoGame](../LuminPack.MonoGame/README.cn.md) | 存档中的数学与游戏数据值，无需 Game 或图形上下文 |
| 🖌️ [LuminPack.SkiaSharp](../LuminPack.SkiaSharp/README.cn.md) | 几何、颜色与透视矩阵值，不含原生渲染句柄 |

Unity 有独立的模块：[LuminPack.Unity](../LuminPack.Unity/README.cn.md) 与 [LuminPack.Burst](../LuminPack.Burst/README.cn.md)。

## 🔧 工作方式

**直接调用编解码器。** 每个模块都提供类型化的 `...Codec.Serialize/Deserialize` 入口，可以直接调用。

**生成类型中的字段。** 对于生成 DTO 中的字段和集合元素，显式使用 `LuminPackValue<T>`，并调用对应模块的 `Register()` 进行初始化：

- 它基于现有的不透明 Custom 格式化器机制，并保留具体泛型注册。
- 包装器不添加任何线格式帧，应用显式注册的入口会被保留。
- 初始化必须在并发序列化之前完成；已注册的代码和模块编解码器必须保持加载。

**`LuminPackCodec<T>`** 把一次完整的二进制写入/读取/计算大小操作组合在一起。根级便捷方法会检查实际写入长度是否精确，并在赋值给目标之前拒绝多余的输入。

## ✅ 保证

- **分阶段发布**——模块读取器检查完整的定长范围，并分阶段发布游标和目标值。标量宽度、特定架构的访问方式和字节序仍由 Core 负责。
- **没有隐式拦截**——注册不会拦截普通的静态生成操作或原始 unmanaged 操作，也不会让所有平台对象自动参与序列化。
- **schema 相互独立**——已有的原生布局记录与这些显式分量记录是不同的 schema。
- **只支持二进制**——平台 Custom 包装器有意不支持 JSON。
- **不做任何推断**——不推断原生对象句柄、UI 控件、资源所有权或引用身份。

> [!IMPORTANT]
> 你自己编写的应用编解码器也必须遵守相同的值发布与范围契约——适配器不会让用户回调具备事务性。

### 何时添加模块

Core 现有的 unmanaged 路径已经覆盖了许多普通的平台值。只有在存在真正的序列化缺口、构造函数/表示问题或框架传输边界时，才添加模块或类型。绝不要通过复制内部指针来持久化原生资源；对于已经可以正常工作的外部类型，也不需要再加一个包。

## 🔨 构建与测试

这些项目以只读方式引用现有的 net8.0 `LuminPack.Core.dll`，绝不会构建 Core 或其生成器。输出位于其他位置时请传入 `LuminPackCorePath`。只有生成消费者测试需要生成器；请通过 `LuminPackGeneratorPath` 传入与之匹配的程序集。这些项目不发布任何包。

只使用匹配的预构建依赖运行模块契约：

```powershell
& 'src/LuminPack.Platforms/Tests~/Run.ps1' `
  -CorePath 'C:/Artifacts/LuminPack.Core.dll' `
  -GeneratorPath 'C:/Artifacts/LuminPack.Generator.dll'
```

脚本会对这些程序集做快照，只构建这些可选项目并执行它们的控制台契约。Windows 上还会运行 WPF；其他主机跳过这个仅限 Windows 的模块。它不会运行整个解决方案、现有单元测试套件或基准测试。

| 已覆盖 | 未覆盖 |
| --- | --- |
| 公开值与字节级往返、完整大小 | 完整的 Avalonia/MAUI/MonoGame/Skia 应用 |
| 每一个截断的输入/输出窗口、哨兵字节 | 移动设备 |
| 格式错误的标记、保留的注册 | 裁剪或 NativeAOT |
| 真实生成的字段、数组和列表 | 应用部署与目标平台上的执行 |
| 通过真实 ASP.NET Core 管道的 HTTP | |

通用项目面向 net8.0；WPF 面向 net8.0-windows。平台包版本是显式指定的，只有在完成相应的契约验证后才能更改。
