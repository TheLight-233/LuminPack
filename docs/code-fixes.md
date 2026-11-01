# IDE code fixes

[Chinese](code-fixes.cn.md)

The `LuminPack.Generator` NuGet package includes C# quick actions for its diagnostics. Consumers only install `LuminPack`, which automatically brings in the generator package:

```xml
<PackageReference Include="LuminPack" Version="2.0.0" />
```

In an IDE with Roslyn code-fix support, place the cursor on a diagnostic and open Quick Actions (for example, `Ctrl+.` in Visual Studio). Review the preview and apply the fix. No separate CodeFix package or Workspaces reference is required in the consuming project. Command-line builds still report generator diagnostics without applying edits.

## Supported diagnostics

| Diagnostic | Quick action |
| --- | --- |
| `LuminPack001` | Remove `[LuminPackable]` from a static type. |
| `LuminPack002`, `LuminPack014` | Add `[LuminPackable]` to the missing source type; make it and its containing types partial. Collection element types are resolved from diagnostic metadata. |
| `LuminPack005`, `LuminPack103` | Make an inaccessible callback or Custom formatter method internal. |
| `LuminPack006` | Remove serialization callback attributes from an unmanaged struct method. |
| `LuminPack007` | Remove `[LuminPackInclude]` and `[LuminPackIgnore]` from an overridden property. |
| `LuminPack008`, `LuminPack009` | Make a class Union root abstract and remove `sealed`. |
| `LuminPack021` | Remove an unsupported `[LuminPackInclude]` annotation. This diagnostic is retained for older target contracts and is not currently emitted. |
| `LuminPack025` | Choose a public constructor and mark it with `[LuminPackConstructor]`, removing other constructor-selection annotations. |
| `LuminPack027` | Choose an existing non-public constructor to make public. |
| `LuminPack028`, `LuminPack029` | Make a nested type internal across its partial declarations. |
| `LuminPack033`, `LuminPack101` | Add `static` to a pool-rent or Custom formatter method. Abstract, virtual, override and explicit interface methods are excluded. |
| `LuminPack034` | Remove `[LuminPackCompress]` from a managed collection member. |
| `LuminPack035` | Make a Union participant and every containing type partial across source files. |

Mechanical actions support Fix All in a document, project or solution. Constructor choices intentionally have no Fix All action: each type needs an explicit construction decision. Adding `static`, changing accessibility or making a Union abstract can require related application changes; review the preview and rebuild afterward.

Fixes preserve unrelated attributes and declaration comments, recognize attribute aliases and use fully qualified names when adding attributes. Modifier fixes update both halves of partial methods together, including declarations in separate files. Referenced metadata cannot be edited; a missing packable type needs an editable declaration in the current project. Unsupported diagnostics remain available with their existing guidance. Member order, Union tags, serialization method bodies and schema choices are not inferred.

## Project and package structure

`src/LuminPack.CodeFixes` targets `netstandard2.0` and uses `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.3.0, matching the generator's Roslyn baseline. `CodeFixProvider` comes from Workspaces; the C# package also supplies C# workspace services. Dependencies use `PrivateAssets="all"`.

The generator package contains both `LuminPack.Generator.dll` and `LuminPack.CodeFixes.dll` with their XML documentation under `analyzers/dotnet/cs`. The CodeFix project is not separately packable. Its build-only project reference in the generator does not introduce an assembly reference or a NuGet dependency. Roslyn and composition DLLs are not bundled; the IDE supplies them. Neither the runtime nor the generator references the fix assembly or Workspaces, and the fix assembly references neither product assembly. This keeps compiler-only and Unity generator loading independent of IDE services. Unity needs an editor integration that loads Roslyn code-fix assemblies; copying the source generator alone does not enable quick actions.

Run the focused workspace checks with:

```text
dotnet run --project tests/LuminPack.Tests.CodeFixes/LuminPack.Tests.CodeFixes.csproj -c Release
```

The checks exercise real generator diagnostics before and after edits, source compilation, cross-document changes, aliases, comments, cancellation and the Roslyn Fix All provider. The legacy `LuminPack021` action uses a synthetic diagnostic because the generator no longer emits it. These checks do not launch an IDE.
