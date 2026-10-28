<div align="center">

# ⚙ LuminPack.Generator

**`[LuminPackable]` 背后的源码生成器与分析器**

![Roslyn](https://img.shields.io/badge/Roslyn-source%20generator-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Analyzer](https://img.shields.io/badge/analyzer-included-2EA043?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [生成模式](../../docs/generation-modes.cn.md) · [编译期选项](../../docs/options.cn.md)

</div>

---

源码生成器与分析器项目，程序集和包名为带点号的 `LuminPack.Generator`。本目录负责生成器的实现及其分析器包。公开的生成器选项位于 `LuminPack.Attribute` 命名空间。

## 📥 引用方式

`LuminPack.Core` 包会以传递方式引入该分析器，因此大多数项目无需额外操作。对于基于项目引用的构建，可以直接引用分析器：

```xml
<ProjectReference Include="path/to/src/LuminPack.Generator/LuminPack.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

> [!IMPORTANT]
> 每个使用方只能有一个生成器包或分析器引用。
