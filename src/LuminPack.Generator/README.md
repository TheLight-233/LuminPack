# LuminPack.Generator

Source generator and analyzer project with the dotted assembly/package name `LuminPack.Generator`.

```xml
<ProjectReference Include="path/to/src/LuminPack.Generator/LuminPack.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

This directory owns the generator implementation and its analyzer package. Public generator options use the `LuminPack.Attribute` namespace.

Use one generator package or analyzer reference per consumer. The `LuminPack.Core` package brings this analyzer transitively; a direct analyzer reference is useful for project-based builds.
