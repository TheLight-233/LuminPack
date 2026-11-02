# Release builds and API governance

[Chinese](releasing.cn.md)

The release workflow currently **builds and validates candidates only**. It never uploads to NuGet, creates a GitHub release or creates a tag. It has read-only repository permissions and requires no NuGet API key. Refactoring is still in progress; a successful candidate build does not declare the API frozen or the library released.

## Package scope

The maintained NuGet candidates are `LuminPack` (Core) and `LuminPack.Generator`. Core depends on the matching Generator version; Generator carries the generator and CodeFixes assemblies. The optional platform, Unity and Burst projects currently remain non-packable. Platform packages need their own dependency and target-framework contracts before joining this workflow; Unity distribution remains a separate concern.

`eng/Versions.props` owns the development target version, the optional previously published Core baseline and the disabled publishing marker. The existing target version remains `2.0.0`; this file is not a record of an actual release.

- Use `major.minor.patch` and optional SemVer prerelease identifiers. A candidate's numeric version must match the version file. Numeric prerelease identifiers cannot have leading zeroes.
- Raise the major version for incompatible public API or wire-contract changes. A major bump never substitutes for approval of a mechanism change.
- A blank candidate version produces `<version>-ci.g<commit>`. Manual inputs can select a reviewed preview version. A pushed `v*` tag selects its version but still only builds artifacts.
- Set `LuminPackReleaseBaselineVersion` to an actual previously published Core version when that compatibility baseline is agreed. It starts empty because this refactoring does not establish a new shipped baseline.
- Keep `LuminPackPublishingEnabled=false`. Changing that value does not implement publication; the build-only script rejects it.

## Build a candidate locally

Install PowerShell 7 and the repository's SDKs, including the .NET 11 preview SDK, then run from the repository root:

```powershell
pwsh -File .github/scripts/release.ps1 -Version 2.0.0-preview.1 -ApiBaseRef HEAD
```

This packs both candidates, runs Core PackageValidation, verifies package identities and aligned dependencies, verifies portable SourceLink symbols, exports public APIs, compares with packages built from the requested Git commit, checks the analyzer package and runs the package consumer's focused `--verify-installation` selector against an isolated local feed. It checks binary/JSON values, reuse and truncated-input rejection. The full trimmed/AOT contracts remain a separate CI gate. No key or upload permission is used.

The API base is exported into a fresh scratch directory; no checkout, branch or working file is overwritten. Every invocation writes to a new `run-*` directory below `.tmp/release-validation`. `-Output` changes the parent directory. `-SkipInstallationCheck` is available for focused packaging diagnostics; it is not used by the hosted workflow and is recorded in the manifest.

Dirty local trees are allowed for development inspection. Their manifests and change records explicitly say they are dirty. SourceLink checks verify metadata structure and commit mappings; with uncommitted changes, they do not prove that the remote source matches the local source bytes. An eventual published build must come from a clean, reviewed commit.

## Public API review

The repository inspector reads packaged DLL metadata without executing those assemblies. It exports public and protected declarations separately for every Core framework and for the Generator and CodeFixes assemblies. Inventories include accessor visibility, parameter defaults, constants, generic constraints, nullable annotations and attributes. Regression checks exercise visibility and representative contract changes.

`-ApiBaseRef <commit-or-tag>` builds the previous package shapes and writes `public-api.diff`. The PR workflow uses the PR base commit; manual validation defaults to `HEAD^`. Full Git history must be available. The comparison reports additions as well as removals. During refactoring it is a review artifact; `-RequireUnchangedApi` makes any inventory change fail the run. It also requires an API base instead of silently omitting comparison.

Inventories are review aids, not a complete binary/source compatibility proof. SDK PackageValidation supplies Core package compatibility checks. Generated consumer facades are not assemblies inside these packages; their API and mechanism contracts remain covered by the existing codegen and installation checks in CI. Analyzer behavior and serializer wire compatibility need their existing dedicated tests.

## PackageValidation

Release builds set `LuminPackReleaseBuild=true`, enabling the SDK's [PackageValidation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview) for Core. Ordinary builds retain their existing behavior. Cross-framework consistency checks run even when no published baseline is configured. `-BaselineVersion <published-version>` additionally enables the SDK baseline comparison against NuGet; mismatches fail packaging.

`eng/CompatibilitySuppressions.xml` lists eight exact, established cross-framework differences: Standard-only Burst hooks, four pointer cursor fields that become managed references on modern targets, the .NET 10 BitArray storage view, and the instance/static ObjectPool policy constructor and constraint. Each exception identifies the diagnostic, API and left/right assets. These entries document existing conditional contracts, do not change them, and do not authorize new breaks or blanket suppression of baseline errors.

The Generator package carries analyzers rather than ordinary `lib` assets, so its package layout, dependency use and installed behavior are checked explicitly rather than claiming SDK runtime-asset validation for it.

## SourceLink and artifacts

The SDK's built-in [SourceLink integration](https://github.com/dotnet/sourcelink) records GitHub source mappings. Shared build properties enable repository metadata, portable symbols, deterministic compilation, embedded untracked sources and CI path normalization. Core publishes a local `.snupkg` candidate; Generator carries portable PDBs beside both analyzer DLLs in its `.nupkg`.

The inspector requires all five Core framework assets, both analyzer assemblies and their symbols, matching version/dependency/repository metadata, and seven portable PDBs with SourceLink mappings to the candidate commit. It does not download source documents or publish symbols.

Each successful validation retains:

- `packages/`: candidate `.nupkg` and `.snupkg` files.
- `api/`, `api-base/`, `public-api.diff`: reviewable per-assembly and per-framework APIs.
- `release-manifest.json`: version, commit, dirty state, configured baseline and performed checks, always with publishing disabled.
- `CHANGELOG.md`: a generated, explicitly unpublished change record from the API-base commit through the candidate commit. Uncommitted edits are not represented by that commit list.
- `SHA256SUMS`: hashes of the candidate package files.

Change history is generated into build artifacts rather than maintained in README or `docs`, as required by the repository rules.

## Workflow and future publication

`.github/workflows/release.yml` runs for relevant PR changes, `v*` tag pushes and manual dispatch. It fetches full history, installs the SDKs, performs package/API/installation checks and uploads artifacts, including partial evidence on failure. Creating a tag still requires the owner's explicit authorization. The workflow itself creates no tags or releases.

After refactoring, agree the API/wire baseline and version, review the API diff and change record, run the required platform/compatibility tests, and then implement and authorize publication separately. A future upload job can read `NUGET_API_KEY` from a protected GitHub environment or use NuGet trusted publishing. Never put credentials in Git, workflow inputs, command examples or chat. Merely adding a Secret cannot make this build-only workflow publish.
