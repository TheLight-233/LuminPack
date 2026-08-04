param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [ValidateSet('Serialize', 'Deserialize')]
    [string] $Method = 'Deserialize',

    [string] $JitFilter = '',

    [ValidateSet('--character-jit-probe', '--character-concrete-jit-probe')]
    [string] $ProbeArgument = '--character-jit-probe'
)

$env:DOTNET_TieredCompilation = '1'
$env:DOTNET_TieredPGO = '1'
$env:DOTNET_TC_CallCountThreshold = '1'
$env:DOTNET_TC_CallCountingDelayMs = '0'
$env:DOTNET_JitDisasm = if ([string]::IsNullOrWhiteSpace($JitFilter)) {
    "LuminPack.Generated.LuminPackBenchmark_CharacterSaveDataParser:$Method"
} else {
    $JitFilter
}
$env:DOTNET_JitDisasmDiffable = '1'
$env:COMPlus_JitDisasm = $env:DOTNET_JitDisasm
$env:COMPlus_JitDisasmDiffable = $env:DOTNET_JitDisasmDiffable

$benchmarkAssembly = Join-Path $PSScriptRoot 'bin\Release\net10.0\LuminPackBenchmark.dll'
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputFullPath)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

& rtk proxy dotnet $benchmarkAssembly $ProbeArgument |
    Out-File -FilePath $outputFullPath -Encoding utf8
exit $LASTEXITCODE
