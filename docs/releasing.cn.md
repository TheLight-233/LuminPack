# 发布构建与 API 治理

[English](releasing.md)

当前发布工作流**只构建和验证候选产物**，不会上传 NuGet、创建 GitHub Release 或创建标签。工作流只具有仓库读取权限，不需要 NuGet API Key。项目仍在重构，候选构建成功不代表 API 已冻结或库已经发布。

## 包范围

目前维护的 NuGet 候选包为 `LuminPack`（Core）和 `LuminPack.Generator`。Core 依赖同版本 Generator；Generator 包含生成器和 CodeFixes 程序集。可选平台、Unity 和 Burst 项目目前保持不可打包状态。平台包需要先确定依赖与目标框架契约，再加入工作流；Unity 分发单独处理。

`eng/Versions.props` 统一管理开发目标版本、可选的已发布 Core 基线，以及禁用发布标记。现有目标版本仍为 `2.0.0`，该文件不表示已经实际发布。

- 使用 `major.minor.patch`，可附加 SemVer 预发布标识。候选的数字版本必须与版本文件一致，纯数字预发布标识不能有前导零。
- 不兼容的公共 API 或数据格式契约变更应提升主版本。提升主版本不能替代机制变更所需的批准。
- 未指定候选版本时生成 `<version>-ci.g<commit>`。手动输入可选择已审查的预览版本。推送 `v*` 标签会采用其版本，但仍然只构建产物。
- 确定兼容基线后，再把 `LuminPackReleaseBaselineVersion` 设为真实发布过的 Core 版本。当前留空，因为本次重构尚不建立新的已发布基线。
- 保持 `LuminPackPublishingEnabled=false`。修改该值不能实现发布；仅构建脚本会拒绝运行。

## 本地构建候选包

安装 PowerShell 7 和仓库要求的 SDK（包括 .NET 11 预览版），在仓库根目录执行：

```powershell
pwsh -File .github/scripts/release.ps1 -Version 2.0.0-preview.1 -ApiBaseRef HEAD
```

该命令打包两个候选包，运行 Core PackageValidation，核验包身份和依赖版本，核验可移植 SourceLink 符号，导出公共 API，并与指定 Git 提交构建的包比较；随后检查分析器包，并通过隔离的本地源运行包消费程序的定向入口 `--verify-installation`，检查二进制/JSON 值、复用和截断输入拒绝。完整裁剪/AOT 契约仍是独立的 CI 门禁。不使用密钥或上传权限。

API 基线导出到全新的临时目录，不覆盖检出、分支或工作文件。每次运行在 `.tmp/release-validation` 下创建新的 `run-*` 目录，`-Output` 可修改父目录。`-SkipInstallationCheck` 用于针对性打包诊断；托管工作流不使用它，清单会记录是否跳过。

允许开发检查使用含未提交改动的工作区，清单及变更记录会明确标注。SourceLink 检查核验元数据结构和提交映射；存在未提交改动时，它不能证明远端源码与本地字节完全一致。未来正式发布必须来自干净、已审查的提交。

## 公共 API 审查

仓库检查工具读取包内 DLL 元数据，不执行这些程序集。工具分别导出每个 Core 框架，以及 Generator、CodeFixes 的 public/protected 声明，包含访问器可见性、参数默认值、常量、泛型约束、可空标注和特性。回归检查覆盖可见性和具有代表性的契约变化。

`-ApiBaseRef <提交或标签>` 构建旧包并生成 `public-api.diff`。PR 工作流采用 PR 基线提交，手动验证默认为 `HEAD^`；需要完整 Git 历史。比较同时报告增加和删除。重构期间先作为审查产物；使用 `-RequireUnchangedApi` 后，任意 API 清单变化都会使运行失败，同时必须指定 API 基线，不能静默跳过比较。

API 清单用于审查，不是完整的二进制/源码兼容证明。SDK PackageValidation 提供 Core 包兼容检查。使用方生成的 facade 并不位于这些包的程序集内，其 API 和机制仍由 CI 已有生成代码与安装检查覆盖。分析器行为和序列化数据兼容仍需专门的现有测试。

## PackageValidation

发布构建设置 `LuminPackReleaseBuild=true`，对 Core 启用 SDK [PackageValidation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview)，普通构建保持既有行为。即使没有配置已发布基线，也运行跨框架一致性检查。`-BaselineVersion <已发布版本>` 额外启用 SDK 对 NuGet 包的基线比较，不兼容会使打包失败。

`eng/CompatibilitySuppressions.xml` 列出八项精确的既有跨框架差异：仅 Standard 存在的 Burst 钩子、现代目标改用托管引用的四个指针游标字段、.NET 10 BitArray 存储视图，以及 ObjectPool 实例/静态策略对应的构造函数和约束。每项例外都限定诊断、API 和左右产物。它们记录既有条件契约，不改变机制，也不授权新破坏性变更或大范围屏蔽基线错误。

Generator 包包含分析器，而不是普通 `lib` 产物，因此显式验证其包布局、依赖使用和安装行为，不把它描述成已完成 SDK 运行时产物验证。

## SourceLink 与产物

采用 SDK 内置的 [SourceLink 集成](https://github.com/dotnet/sourcelink)记录 GitHub 源码映射。共享构建属性启用仓库元数据、可移植符号、确定性编译、嵌入未跟踪源码及 CI 路径归一化。Core 生成本地 `.snupkg` 候选；Generator 在 `.nupkg` 内把可移植 PDB 放在两个分析器 DLL 旁边。

检查工具要求五个 Core 框架产物、两个分析器程序集及其符号，匹配的版本/依赖/仓库元数据，以及七份映射到候选提交的 SourceLink PDB。检查不会下载源码文档或发布符号。

每次成功验证保留：

- `packages/`：候选 `.nupkg`、`.snupkg`。
- `api/`、`api-base/`、`public-api.diff`：可审查的逐程序集、逐框架 API。
- `release-manifest.json`：版本、提交、工作区改动状态、配置的基线及执行的检查，始终标记禁用发布。
- `CHANGELOG.md`：自动生成、明确标记未发布的变更记录，范围为 API 基线至候选提交。提交列表不包含尚未提交的修改。
- `SHA256SUMS`：候选包文件哈希。

按仓库规则，历史记录生成到构建产物，不维护在 README 或 `docs` 中。

## 工作流与未来发布

`.github/workflows/release.yml` 在相关 PR 改动、`v*` 标签推送及手动触发时运行。它拉取完整历史、安装 SDK、执行包/API/安装检查并上传产物；失败时也保留已有证据。创建标签仍需所有者明确授权，工作流本身不创建标签或 Release。

重构完成后，再确定 API/数据格式基线和版本，审查 API 差异与变更记录，运行必要的平台/兼容测试，然后单独实现并授权发布。未来上传任务可以从受保护的 GitHub Environment 读取 `NUGET_API_KEY`，或采用 NuGet 可信发布。凭据不能放进 Git、工作流输入、命令示例或聊天。仅添加 Secret 不会让当前仅构建工作流开始上传。
