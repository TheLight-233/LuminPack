<div align="center">

# ⚙ LuminPack.Generator

**The source generator and analyzer behind `[LuminPackable]`**

![Roslyn](https://img.shields.io/badge/Roslyn-source%20generator-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![Analyzer](https://img.shields.io/badge/analyzer-included-2EA043?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [Generation modes](../../docs/generation-modes.md) · [Compile-time options](../../docs/options.md)

</div>

---

Source generator and analyzer project, with the dotted assembly and package name `LuminPack.Generator`. This directory owns the generator implementation and its analyzer package. Public generator options live in the `LuminPack.Attribute` namespace.

## 📥 Referencing

The `LuminPack.Core` package brings this analyzer in transitively, so most projects need nothing else. A direct analyzer reference is useful for project-based builds:

```xml
<ProjectReference Include="path/to/src/LuminPack.Generator/LuminPack.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

> [!IMPORTANT]
> Use exactly one generator package or analyzer reference per consumer.
