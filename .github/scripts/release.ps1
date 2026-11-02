param(
    [string]$Version = '',
    [string]$BaselineVersion = '',
    [string]$ApiBaseRef = '',
    [switch]$RequireUnchangedApi,
    [switch]$SkipInstallationCheck,
    [string]$Output = '.tmp/release-validation'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    $settings = [xml](Get-Content -Raw (Join-Path $repo 'eng/Versions.props'))
    $baseVersion = [string]$settings.Project.PropertyGroup.LuminPackReleaseVersion
    if ([string]$settings.Project.PropertyGroup.LuminPackPublishingEnabled -ne 'false') {
        throw 'Publishing is not implemented in this build-only workflow; keep LuminPackPublishingEnabled=false.'
    }
    if (!$BaselineVersion) { $BaselineVersion = [string]$settings.Project.PropertyGroup.LuminPackReleaseBaselineVersion }
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the candidate commit.' }
    if (!$Version) { $Version = "$baseVersion-ci.g$($revision.Substring(0, 12))" }
    $versionPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
    foreach ($candidate in @($Version, $BaselineVersion) | Where-Object { $_ }) {
        if ($candidate -notmatch $versionPattern) { throw "Invalid release version: $candidate" }
        if ($candidate.Contains('-')) {
            foreach ($identifier in $candidate.Substring($candidate.IndexOf('-') + 1).Split('.')) {
                if ($identifier -match '^0\d+$') { throw 'Numeric prerelease identifiers cannot have leading zeroes.' }
            }
        }
    }
    if ($BaselineVersion -eq $Version) { throw 'The baseline must be a different package version.' }
    if ($Version.Split('-')[0] -ne $baseVersion) { throw 'The candidate version must use the release version from eng/Versions.props.' }
    $dirty = @(& git status --porcelain).Count -ne 0
    $outputRoot = [IO.Path]::GetFullPath($Output)
    # New run directories prevent stale packages or previous instrumented outputs from entering an artifact.
    $run = Join-Path $outputRoot ('run-' + [Guid]::NewGuid().ToString('N'))
    $packages = Join-Path $run 'packages'
    New-Item -ItemType Directory -Force $packages | Out-Null
    Write-Output "Build-only release candidate: $Version at $revision; dirty=$dirty; output=$run"
    $common = @('-c', 'Release', '-p:GeneratePackageOnBuild=false', "-p:Version=$Version", "-p:SourceRevisionId=$revision", '-p:ContinuousIntegrationBuild=true')
    & dotnet pack src/LuminPack.Generator/LuminPack.Generator.csproj @common -o $packages
    if ($LASTEXITCODE -ne 0) { throw 'Generator packaging failed.' }
    $validation = @('-p:LuminPackReleaseBuild=true')
    if ($BaselineVersion) { $validation += "-p:PackageValidationBaselineVersion=$BaselineVersion" }
    & dotnet pack src/LuminPack.Core/LuminPack.Core.csproj @common @validation -o $packages
    if ($LASTEXITCODE -ne 0) { throw 'Core packaging or PackageValidation failed.' }
    & dotnet build tools/LuminPack.RepositoryChecks -c Release -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Release inspection tooling failed to build.' }
    $checks = Join-Path $repo 'tools/LuminPack.RepositoryChecks/bin/Release/net11.0/LuminPack.RepositoryChecks.dll'
    & dotnet $checks --verify-release-checks
    if ($LASTEXITCODE -ne 0) { throw 'Release inspection regression checks failed.' }
    & dotnet $checks --verify-release $packages $Version $revision
    if ($LASTEXITCODE -ne 0) { throw 'Package layout or SourceLink verification failed.' }
    $api = Join-Path $run 'api'
    & dotnet $checks --package-api $packages $api
    if ($LASTEXITCODE -ne 0) { throw 'Public API inventory failed.' }

    $apiChanged = $null
    $baseCommit = ''
    if ($ApiBaseRef) {
        $baseCommit = (& git rev-parse --verify --end-of-options "${ApiBaseRef}^{commit}").Trim()
        if ($LASTEXITCODE -ne 0) { throw 'The API base must resolve to a commit in the fetched history.' }
        $baseRoot = Join-Path $run 'api-base-source'
        $archive = Join-Path $run 'api-base.zip'
        & git archive --format=zip "--output=$archive" $baseCommit
        if ($LASTEXITCODE -ne 0) { throw 'Could not export the API base commit.' }
        Expand-Archive -LiteralPath $archive -DestinationPath $baseRoot
        $basePackages = Join-Path $run 'api-base-packages'
        foreach ($project in @('LuminPack.Generator', 'LuminPack.Core')) {
            & dotnet pack (Join-Path $baseRoot "src/$project/$project.csproj") -c Release -p:GeneratePackageOnBuild=false -p:Version=0.0.0-api-base -o $basePackages
            if ($LASTEXITCODE -ne 0) { throw "API base packaging failed for $project." }
        }
        $baseApi = Join-Path $run 'api-base'
        & dotnet $checks --package-api $basePackages $baseApi
        if ($LASTEXITCODE -ne 0) { throw 'API base inventory failed.' }
        & git diff --no-index -- $baseApi $api > (Join-Path $run 'public-api.diff')
        $diffExit = $LASTEXITCODE
        if ($diffExit -gt 1) { throw 'Public API diff failed.' }
        $apiChanged = $diffExit -eq 1
    }
    elseif ($RequireUnchangedApi) { throw 'Strict API checking requires -ApiBaseRef.' }

    if (!$SkipInstallationCheck) {
        & dotnet run --project tests/LuminPack.Tests.CodeFixes -c Release -- --verify-package (Join-Path $packages "LuminPack.Generator.$Version.nupkg")
        if ($LASTEXITCODE -ne 0) { throw 'The installed analyzer package contract failed.' }
        $feedConfig = Join-Path $run 'NuGet.Config'
        $config = [xml]'<configuration><packageSources><clear /><add key="local" value="" /><add key="nuget" value="https://api.nuget.org/v3/index.json" /></packageSources><packageSourceMapping><packageSource key="local"><package pattern="LuminPack*" /></packageSource><packageSource key="nuget"><package pattern="*" /></packageSource></packageSourceMapping></configuration>'
        $config.configuration.packageSources.add[0].SetAttribute('value', $packages)
        $config.Save($feedConfig)
        & dotnet restore tests/LuminPack.Tests.Aot/LuminPack.Tests.Aot.csproj "-p:LuminPackPackageVersion=$Version" --configfile $feedConfig --packages (Join-Path $run 'package-cache')
        if ($LASTEXITCODE -ne 0) { throw 'The local package consumer failed to restore.' }
        & dotnet run --project tests/LuminPack.Tests.Aot -c Release --no-restore "-p:LuminPackPackageVersion=$Version" -- --verify-installation
        if ($LASTEXITCODE -ne 0) { throw 'The local package consumer failed.' }
    }
    $range = if ($baseCommit) { "$baseCommit..$revision" } else { $revision }
    $changes = @(& git log --format='%h %s' $range)
    if ($LASTEXITCODE -ne 0) { throw 'Change record generation failed.' }
    $notes = @("# $Version - build-only candidate", '', "Source: $revision", "Working tree has uncommitted changes: $dirty", '',
        'This artifact has not been published. Uncommitted changes are not represented by the commit list.', '', '## Changes', '')
    $notes += $changes | ForEach-Object { "- $_" }
    $notes | Set-Content -LiteralPath (Join-Path $run 'CHANGELOG.md') -Encoding utf8
    Get-ChildItem -LiteralPath $packages -File | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    } | Set-Content -LiteralPath (Join-Path $run 'SHA256SUMS') -Encoding utf8
    [ordered]@{
        version = $Version; revision = $revision; dirty = $dirty; publishingEnabled = $false
        baselineVersion = $BaselineVersion; apiBaseCommit = $baseCommit; apiChanged = $apiChanged
        packageValidation = 'passed'; sourceLink = 'structurally verified'; installationChecked = !$SkipInstallationCheck
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'release-manifest.json') -Encoding utf8
    if ($RequireUnchangedApi -and $apiChanged) { throw "Public API changes require review: $run/public-api.diff" }
    Write-Output "Build-only release validation passed. Artifacts: $run"
}
finally {
    Pop-Location
}
