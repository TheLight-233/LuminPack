param(
    [ValidateSet('Smoke', 'Coverage')]
    [string]$Mode = 'Smoke',
    [ValidateSet('binary', 'binary-utf8-varint', 'json-utf8', 'json-utf16')]
    [string]$Target = 'binary',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateRange(1, 10000000)]
    [int]$Iterations = 1000,
    [int]$Seed = 233,
    [ValidateRange(1, 86400)]
    [int]$Seconds = 60,
    [string]$LibFuzzer,
    [string]$Output = '.tmp/fuzz',
    [string]$Corpus = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    $outputRoot = [IO.Path]::GetFullPath($Output)
    if (!$Corpus) { $Corpus = Join-Path $outputRoot 'corpus' }
    $corpusRoot = [IO.Path]::GetFullPath($Corpus)
    $project = Join-Path $repo 'tests/LuminPack.Fuzzing/LuminPack.Fuzzing.csproj'
    # Every run gets a clean publication; instrumented DLLs never replace normal build output.
    $publish = Join-Path $outputRoot ('publish-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $publish | Out-Null
    & dotnet publish $project -c $Configuration --no-self-contained -o $publish -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw 'The fuzz harness failed to publish.' }
    $harness = Join-Path $publish 'LuminPack.Fuzzing.dll'

    # Keep checked-in regression inputs and add the current build's verified positive seeds.
    $savedCorpus = Join-Path $repo 'tests/LuminPack.Fuzzing/Corpus'
    if (Test-Path -LiteralPath $savedCorpus) {
        foreach ($directory in Get-ChildItem -LiteralPath $savedCorpus -Directory) {
            $destination = Join-Path $corpusRoot $directory.Name
            New-Item -ItemType Directory -Force $destination | Out-Null
            foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -File) {
                $destinationFile = Join-Path $destination $file.Name
                if (!(Test-Path -LiteralPath $destinationFile)) { Copy-Item -LiteralPath $file.FullName -Destination $destinationFile }
            }
        }
    }
    & dotnet $harness --seed $corpusRoot
    if ($LASTEXITCODE -ne 0) { throw 'Positive seed verification failed.' }

    if ($Mode -eq 'Smoke') {
        & dotnet $harness --smoke $corpusRoot (Join-Path $outputRoot 'repro') $Iterations $Seed
        if ($LASTEXITCODE -ne 0) { throw 'Fuzz smoke testing failed; replay the saved input under the output directory.' }
    }
    else {
        if (!$LibFuzzer) { throw 'Coverage mode requires -LibFuzzer pointing to a libfuzzer-dotnet executable.' }
        $driver = (Resolve-Path -LiteralPath $LibFuzzer).Path
        $toolDirectory = Join-Path $outputRoot 'tools-2.3.0'
        $tool = Join-Path $toolDirectory $(if ($IsWindows) { 'sharpfuzz.exe' } else { 'sharpfuzz' })
        if (!(Test-Path -LiteralPath $tool)) {
            & dotnet tool install SharpFuzz.CommandLine --version 2.3.0 --tool-path $toolDirectory
            if ($LASTEXITCODE -ne 0) { throw 'SharpFuzz.CommandLine installation failed.' }
        }
        & $tool (Join-Path $publish 'LuminPack.Core.dll')
        if ($LASTEXITCODE -ne 0) { throw 'Core instrumentation failed.' }
        # Generated readers live in the consuming executable. The Fuzzing runner namespace is excluded.
        & $tool $harness 'LuminPack.'
        if ($LASTEXITCODE -ne 0) { throw 'Generated formatter instrumentation failed.' }
        $failures = Join-Path $outputRoot ('failures/' + $Target)
        New-Item -ItemType Directory -Force $failures | Out-Null
        $arguments = @(
            "-max_total_time=$Seconds", '-timeout=10', '-rss_limit_mb=1024', '-max_len=16384',
            "-seed=$Seed", "-artifact_prefix=$failures$([IO.Path]::DirectorySeparatorChar)",
            '--target_path=dotnet', $(if ($IsWindows) { '--target_arg="' + $harness + '"' } else { "--target_arg=$harness" }),
            (Join-Path $corpusRoot $Target)
        )
        $previousTarget = $env:LUMINPACK_FUZZ_TARGET
        try {
            $env:LUMINPACK_FUZZ_TARGET = $Target
            & $driver @arguments
            if ($LASTEXITCODE -ne 0) { throw "Coverage fuzzing failed; replay the raw artifact in $failures with --replay $Target." }
        }
        finally {
            $env:LUMINPACK_FUZZ_TARGET = $previousTarget
        }
    }
}
finally {
    Pop-Location
}
