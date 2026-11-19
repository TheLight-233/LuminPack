# Release builds and API governance

[Chinese](releasing.cn.md)

The release workflow builds and validates a candidate for review. In the upstream repository, pushing the exact release tag configured in `eng/Versions.props` also starts the NuGet publish job after validation succeeds.

## Package scope

Consumers install one `LuminPack` NuGet package. It contains the Core runtime in `lib/`, the Next generator and CodeFixes in `analyzers/dotnet/cs/`, and the IL postprocessor with Mono.Cecil in `tools/`. The `buildTransitive/` assets enable compiler interception and IL rewriting. Core's portable symbols also ship as a separate `.snupkg` for the NuGet symbol server. Platform adapters, Unity and Burst use their source distributions.

`eng/Versions.props` configures the development target `2.0.0`, the optional previously published Core baseline and the version eligible for NuGet publishing. `LuminPackNuGetPublishVersion` is set to `2.0.0-rc.2`. An empty value leaves the workflow in candidate validation mode.

- Use `major.minor.patch` and optional SemVer prerelease identifiers. A candidate's numeric version must match the version file. Numeric prerelease identifiers cannot have leading zeroes.
- Raise the major version for incompatible public API or wire-contract changes. A major bump never substitutes for approval of a mechanism change.
- A blank candidate version produces `<version>-ci.g<commit>`. Manual inputs and pushed `v*` tags can select a version for validation.
- Set `LuminPackReleaseBaselineVersion` when a previously published Core version must remain API-compatible. For the 2.0 major release, the baseline stays empty and cross-framework PackageValidation still runs. Use `-BaselineVersion 1.1.9` to investigate SDK compatibility with 1.x separately.

## Build a candidate locally

Install PowerShell 7 and the repository's SDKs, including the .NET 11 SDK, then run from the repository root.

```powershell
pwsh -File .github/scripts/release.ps1 -Version 2.0.0-rc.2 -ApiBaseRef 'HEAD^'
```

This builds the package and symbols, runs Core PackageValidation, verifies the package layout and SourceLink, and compares the public APIs with the requested Git commit. The single-package consumers restore from an isolated local feed. They check binary/JSON values, reuse, truncated-input rejection, compiler interception, IL rewriting and registered fallback. The full trimmed/AOT contracts run in the runtime compatibility workflow.

The API base is exported into a fresh scratch directory; no checkout, branch or working file is overwritten. Every invocation writes to a new `run-*` directory below `.tmp/release-validation`. `-Output` changes the parent directory. `-SkipInstallationCheck` is available for focused packaging diagnostics; it is not used by the hosted workflow and is recorded in the manifest.

Dirty local trees are allowed for development inspection. Their manifests and change records explicitly say they are dirty. SourceLink checks verify metadata structure and commit mappings; with uncommitted changes, they do not prove that the remote source matches the local source bytes. An eventual published build must come from a clean, reviewed commit.

## Public API review

The repository inspector reads packaged DLL metadata without executing those assemblies. It exports public and protected declarations separately for every Core framework and for the Generator and CodeFixes assemblies. Inventories include accessor visibility, parameter defaults, constants, generic constraints, nullable annotations and attributes. Regression checks exercise visibility and representative contract changes.

`-ApiBaseRef <commit-or-tag>` builds the previous package shapes and writes `public-api.diff`. The PR workflow uses the PR base commit; manual validation defaults to `HEAD^`. Full Git history must be available. The comparison reports additions as well as removals. During refactoring it is a review artifact; `-RequireUnchangedApi` makes any inventory change fail the run. It also requires an API base instead of silently omitting comparison.

Inventories are review aids, not a complete binary/source compatibility proof. SDK PackageValidation supplies Core package compatibility checks. Generated consumer facades are not assemblies inside these packages; their API and mechanism contracts remain covered by the existing codegen and installation checks in CI. Analyzer behavior and serializer wire compatibility need their existing dedicated tests.

## PackageValidation

Release builds set `LuminPackReleaseBuild=true`, enabling the SDK's [PackageValidation](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/package-validation/overview) for Core. Ordinary builds retain their existing behavior. Cross-framework consistency checks run even when no published baseline is configured. `-BaselineVersion <published-version>` additionally enables the SDK baseline comparison against NuGet; mismatches fail packaging.

`eng/CompatibilitySuppressions.xml` lists six exact, established cross-framework differences covering Standard-only Burst hooks, four cursor fields represented by spans on Standard and by references on modern targets, and the .NET 10 BitArray storage view. Each exception identifies the diagnostic, API and left/right assets. These entries document existing conditional contracts, do not change them, and do not authorize new breaks or blanket suppression of baseline errors.

The repository checks the bundled analyzers and build tools through package layout, dependency and installation checks. SDK PackageValidation covers the Core runtime assets.

## SourceLink and artifacts

The SDK's built-in [SourceLink integration](https://github.com/dotnet/sourcelink) records GitHub source mappings. Shared build properties enable repository metadata, portable symbols, deterministic compilation, embedded untracked sources and CI path normalization. The `.nupkg` carries portable PDBs beside both analyzer DLLs, and the `.snupkg` provides the runtime symbols.

The inspector requires all five Core framework assets, both analyzer assemblies and their symbols, the MSBuild assets, and the candidate version and commit metadata. Each packaged portable PDB must map to the candidate commit.

Each successful validation retains:

- `packages/`: one candidate `.nupkg` and its `.snupkg`.
- `api/`, `api-base/`, `public-api.diff`: reviewable per-assembly and per-framework APIs.
- `release-manifest.json`: candidate version, commit, dirty state, configured baseline and performed checks.
- `CHANGELOG.md`: a generated, explicitly unpublished change record from the API-base commit through the candidate commit. Uncommitted edits are not represented by that commit list.
- `SHA256SUMS`: hashes of the candidate package files.

Change history is generated into build artifacts rather than maintained in README or `docs`, as required by the repository rules.

## Publish to NuGet.org

`.github/workflows/release.yml` runs for relevant PR changes, `v*` tag pushes and manual dispatch. It retains the packages and API comparison as workflow artifacts, including available evidence on failure. GitHub requires the workflow on the default branch to offer manual dispatch.

1. In GitHub, create the `nuget` environment and set its variable `NUGET_USER` to the NuGet.org account name that owns `LuminPack` (`TheLight233`). The environment can restrict deployment to release tags and require a reviewer.
2. In the NuGet.org account, add a [Trusted Publishing policy](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for repository owner `TheLight-233`, repository `LuminPack`, workflow file `release.yml`, and environment `nuget`. Scope it to pushing new versions of the existing package `LuminPack`.
3. Review the candidate API diff, the change record and the platform checks for the selected commit. Push the authorized `v2.0.0-rc.2` tag pointing at that commit, which must include the workflow. The tag's version must equal `LuminPackNuGetPublishVersion`.
4. The publish job checks the candidate manifest and hashes, obtains a short-lived NuGet key via OIDC, and uploads the validated package and symbols. If publication is interrupted, rerun the failed job on the same run to reuse the same artifact. Use a new version for a different build.

PR builds, manual validation and all other version tags produce candidate artifacts. The workflow leaves Git tags and GitHub Releases to the maintainer.
