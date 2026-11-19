# 发布构建与 API 治理

[English](releasing.md)

发布工作流先构建和验证候选包，供维护者审查。在上游仓库中，推送与 `eng/Versions.props` 配置完全匹配的发布标签，会在验证通过后启动 NuGet 发布任务。

## 包范围

使用方只需安装一个 `LuminPack` NuGet 包。包内的 `lib/` 存放 Core 运行时，`analyzers/dotnet/cs/` 存放 Next 生成器和 CodeFixes，`tools/` 存放 IL 后处理器和 Mono.Cecil。`buildTransitive/` 提供编译器拦截和 IL 重写所需的构建配置。Core 的可移植符号另以 `.snupkg` 上传到 NuGet 符号服务。平台适配器、Unity 和 Burst 仍通过源码分发。

`eng/Versions.props` 配置开发目标版本 `2.0.0`、可选的已发布 Core 基线，以及允许上传 NuGet 的版本。`LuminPackNuGetPublishVersion` 当前设为 `2.0.0-rc.2`，留空时工作流只验证候选包。

- 使用 `major.minor.patch`，可附加 SemVer 预发布标识。候选的数字版本必须与版本文件一致，纯数字预发布标识不能有前导零。
- 不兼容的公共 API 或数据格式契约变更应提升主版本。提升主版本不能替代机制变更所需的批准。
- 未指定候选版本时生成 `<version>-ci.g<commit>`。手动触发和推送 `v*` 标签都可以指定待验证版本。
- 需要保持与某个已发布版本的 API 兼容性时，设置 `LuminPackReleaseBaselineVersion`。2.0 是主版本升级，该值留空，仍执行跨框架 PackageValidation。可通过 `-BaselineVersion 1.1.9` 单独调查与 1.x 的 SDK 兼容性。

## 本地构建候选包

安装 PowerShell 7 和仓库要求的 SDK（包括 .NET 11 SDK），在仓库根目录执行以下命令。

```powershell
pwsh -File .github/scripts/release.ps1 -Version 2.0.0-rc.2 -ApiBaseRef 'HEAD^'
```

该命令构建候选包和符号，运行 Core PackageValidation，核验包布局和 SourceLink，并与指定 Git 提交比较公共 API。安装验证使用隔离的本地源，消费项目只引用一个 `LuminPack` 包，检查二进制/JSON 值、复用、截断输入拒绝、编译器拦截、IL 重写和注册回退。完整裁剪/AOT 契约由运行时兼容性工作流检查。

API 基线导出到全新的临时目录，不覆盖检出、分支或工作文件。每次运行在 `.tmp/release-validation` 下创建新的 `run-*` 目录，`-Output` 可修改父目录。`-SkipInstallationCheck` 用于针对性打包诊断；托管工作流不使用它，清单会记录是否跳过。

允许开发检查使用含未提交改动的工作区，清单及变更记录会明确标注。SourceLink 检查核验元数据结构和提交映射；存在未提交改动时，它不能证明远端源码与本地字节完全一致。未来正式发布必须来自干净、已审查的提交。

## 公共 API 审查

仓库检查工具读取包内 DLL 元数据，不执行这些程序集。工具分别导出每个 Core 框架，以及 Generator、CodeFixes 的 public/protected 声明，包含访问器可见性、参数默认值、常量、泛型约束、可空标注和特性。回归检查覆盖可见性和具有代表性的契约变化。

`-ApiBaseRef <提交或标签>` 构建旧包并生成 `public-api.diff`。PR 工作流采用 PR 基线提交，手动验证默认为 `HEAD^`；需要完整 Git 历史。比较同时报告增加和删除。重构期间先作为审查产物；使用 `-RequireUnchangedApi` 后，任意 API 清单变化都会使运行失败，同时必须指定 API 基线，不能静默跳过比较。

API 清单用于审查，不是完整的二进制/源码兼容证明。SDK PackageValidation 提供 Core 包兼容检查。使用方生成的 facade 并不位于这些包的程序集内，其 API 和机制仍由 CI 已有生成代码与安装检查覆盖。分析器行为和序列化数据兼容仍需专门的现有测试。

## PackageValidation

发布构建设置 `LuminPackReleaseBuild=true`，对 Core 启用 SDK [PackageValidation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview)，普通构建保持既有行为。即使没有配置已发布基线，也运行跨框架一致性检查。`-BaselineVersion <已发布版本>` 额外启用 SDK 对 NuGet 包的基线比较，不兼容会使打包失败。

`eng/CompatibilitySuppressions.xml` 列出六项精确的既有跨框架差异，涵盖仅 Standard 存在的 Burst 钩子、在 Standard 上使用 Span 且在新版目标上使用托管引用的四个游标字段，以及 .NET 10 BitArray 存储视图。每项例外都限定诊断、API 和左右产物。它们记录既有条件契约，不改变机制，也不授权新破坏性变更或大范围屏蔽基线错误。

仓库通过包布局、依赖和安装检查验证内置分析器与构建工具。SDK PackageValidation 覆盖 Core 运行时产物。

## SourceLink 与产物

采用 SDK 内置的 [SourceLink 集成](https://github.com/dotnet/sourcelink)记录 GitHub 源码映射。共享构建属性启用仓库元数据、可移植符号、确定性编译、嵌入未跟踪源码及 CI 路径归一化。`.nupkg` 在两个分析器 DLL 旁包含可移植 PDB，`.snupkg` 提供运行时符号。

检查工具要求五个 Core 框架产物、两个分析器程序集及其符号、MSBuild 文件，以及匹配的候选版本和提交元数据。每份打包的可移植 PDB 都必须映射到候选提交。

每次成功验证保留：

- `packages/`：一个候选 `.nupkg` 和对应的 `.snupkg`。
- `api/`、`api-base/`、`public-api.diff`：可审查的逐程序集、逐框架 API。
- `release-manifest.json`：候选版本、提交、工作区改动状态、配置的基线及执行的检查。
- `CHANGELOG.md`：自动生成、明确标记未发布的变更记录，范围为 API 基线至候选提交。提交列表不包含尚未提交的修改。
- `SHA256SUMS`：候选包文件哈希。

按仓库规则，历史记录生成到构建产物，不维护在 README 或 `docs` 中。

## 发布到 NuGet.org

`.github/workflows/release.yml` 在相关 PR 改动、`v*` 标签推送及手动触发时运行。候选包和 API 差异会保存在工作流产物中，失败时也会保留已有证据。GitHub 要求工作流出现在默认分支上，才能手动触发。

1. 在 GitHub 创建 `nuget` 环境，把环境变量 `NUGET_USER` 设为拥有 `LuminPack` 的 NuGet.org 账号名（`TheLight233`）。可通过环境设置限制发布标签并启用审查人。
2. 在 NuGet.org 创建[可信发布策略](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)。填写仓库所有者 `TheLight-233`、仓库 `LuminPack`、工作流文件 `release.yml` 和环境 `nuget`。权限限定为为现有 `LuminPack` 包上传新版本。
3. 审查目标提交的候选 API 差异、变更记录及平台检查结果。将已授权的 `v2.0.0-rc.2` 标签推送到该提交，提交须包含发布工作流。标签版本必须与 `LuminPackNuGetPublishVersion` 一致。
4. 发布任务检查候选清单和哈希，通过 OIDC 获取短期 NuGet 密钥，然后上传验证过的包和符号。发布中断时，在同一次运行中重试失败的任务，复用同一份产物。需要发布不同构建时使用新版本。

PR 构建、手动验证和其他版本标签只生成候选产物。Git 标签和 GitHub Release 由维护者管理。
