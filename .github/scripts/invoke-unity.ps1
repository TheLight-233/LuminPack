# Builds and executes one native desktop Player on a prelicensed Unity runner.
param(
    [string]$Project = 'tests/Unity',
    [string]$Artifacts = '.tmp/unity-native'
)
$ErrorActionPreference = 'Stop'
if (-not $env:UNITY_EDITOR_PATH -or -not (Test-Path -LiteralPath $env:UNITY_EDITOR_PATH)) {
    throw 'Configure UNITY_EDITOR_PATH with the licensed Editor matching tests/Unity/ProjectSettings/ProjectVersion.txt.'
}
$projectPath = (Resolve-Path $Project).Path
$logPath = [IO.Path]::GetFullPath($Artifacts)
New-Item -ItemType Directory -Path $logPath -Force | Out-Null
dotnet build src/LuminPack.Core/LuminPack.Core.csproj -c Release -f netstandard2.1 -p:CopyLocalLockFileAssemblies=true -p:GeneratePackageOnBuild=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($assembly in @('LuminPack.Core.dll', 'System.Collections.Immutable.dll', 'System.Runtime.CompilerServices.Unsafe.dll')) {
    Copy-Item -LiteralPath (Join-Path 'src/LuminPack.Core/bin/Release/netstandard2.1' $assembly) -Destination (Join-Path $projectPath 'Assets/Plugins')
}
Copy-Item -LiteralPath 'src/LuminPack.Generator/bin/Release/netstandard2.0/LuminPack.Generator.dll' -Destination (Join-Path $projectPath 'Assets/Plugins')
$editorInfo = [Diagnostics.ProcessStartInfo]::new($env:UNITY_EDITOR_PATH)
$editorInfo.UseShellExecute = $false
$editorInfo.CreateNoWindow = $true
foreach ($argument in @('-batchmode', '-nographics', '-quit', '-projectPath', $projectPath,
    '-executeMethod', 'LuminPackSmokeBuild.BuildConfigured', '-logFile', (Join-Path $logPath 'editor.log'))) {
    $editorInfo.ArgumentList.Add($argument)
}
$editor = [Diagnostics.Process]::Start($editorInfo)
if (-not $editor.WaitForExit(5400000)) { $editor.Kill($true); throw 'Unity Editor timed out.' }
if ($editor.ExitCode -ne 0) { throw 'Unity build failed; inspect editor.log.' }
$output = Join-Path $projectPath ('build/' + $env:LUMINPACK_UNITY_TARGET + '_' + $env:LUMINPACK_UNITY_BACKEND)
$relativePlayer = if ($IsMacOS) { 'LuminPackSmoke.app/Contents/MacOS/LuminPackSmoke' } else { 'LuminPackSmoke.exe' }
$playerInfo = [Diagnostics.ProcessStartInfo]::new((Join-Path $output $relativePlayer))
$playerInfo.UseShellExecute = $false
$playerInfo.CreateNoWindow = $true
foreach ($argument in @('-batchmode', '-nographics', '-logFile', (Join-Path $logPath 'player.log'))) {
    $playerInfo.ArgumentList.Add($argument)
}
$player = [Diagnostics.Process]::Start($playerInfo)
if (-not $player.WaitForExit(120000)) { $player.Kill($true); throw 'Unity Player timed out.' }
if ($player.ExitCode -ne 0 -or -not (Select-String -LiteralPath (Join-Path $logPath 'player.log') -SimpleMatch 'LUMINPACK_UNITY_SMOKE_PASSED')) {
    throw 'Unity Player failed its smoke checks; inspect player.log.'
}
