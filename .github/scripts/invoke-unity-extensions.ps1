param(
    [string]$Project = '.tmp/unity-extensions-native',
    [string]$Artifacts = '.tmp/unity-extensions-results'
)
$ErrorActionPreference = 'Stop'
if (-not $env:UNITY_EDITOR_PATH -or -not (Test-Path -LiteralPath $env:UNITY_EDITOR_PATH)) {
    throw 'Configure UNITY_EDITOR_PATH with a prelicensed Unity Editor 2022.3 or later.'
}
$repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = [IO.Path]::GetFullPath((Join-Path $repositoryPath $Project))
$artifactPath = [IO.Path]::GetFullPath((Join-Path $repositoryPath $Artifacts))
if (Test-Path -LiteralPath (Join-Path $projectPath 'Assets')) { throw 'Use a new isolated validation project directory.' }
foreach ($folder in @('Assets/Plugins', 'Assets/Editor', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $projectPath $folder) -Force | Out-Null
}
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
'{"dependencies":{"com.unity.burst":"1.8.18","com.unity.modules.jsonserialize":"1.0.0"}}' | Set-Content (Join-Path $projectPath 'Packages/manifest.json')
'-unsafe' | Set-Content (Join-Path $projectPath 'Assets/csc.rsp')
foreach ($assembly in @('LuminPack.Core.dll', 'System.Collections.Immutable.dll', 'System.Runtime.CompilerServices.Unsafe.dll')) {
    Copy-Item -LiteralPath (Join-Path $repositoryPath "src/LuminPack.Core/bin/Release/netstandard2.1/$assembly") -Destination (Join-Path $projectPath 'Assets/Plugins')
}
$editorDirectory = Split-Path -Parent $env:UNITY_EDITOR_PATH
$managedCandidates = @((Join-Path $editorDirectory 'Data/Managed/UnityEngine'), (Join-Path $editorDirectory '../Managed/UnityEngine'))
$unityManagedPath = $managedCandidates | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'UnityEngine.CoreModule.dll') } | Select-Object -First 1
if (-not $unityManagedPath) { throw 'Cannot locate the installed Unity managed engine assemblies.' }
dotnet build (Join-Path $repositoryPath 'src/LuminPack.Unity/LuminPack.Unity.csproj') -c Release "-p:UnityManagedPath=$unityManagedPath"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build (Join-Path $repositoryPath 'src/LuminPack.Unity/Tests~/Consumer/LuminPack.Unity.Consumer.Tests.csproj') -c Release "-p:UnityManagedPath=$unityManagedPath"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($source in @('src/LuminPack.Unity/bin/Release/netstandard2.1/LuminPack.Unity.dll', 'src/LuminPack.Unity/Tests~/Consumer/bin/Release/netstandard2.1/LuminPack.Unity.Consumer.Tests.dll')) {
    Copy-Item -LiteralPath (Join-Path $repositoryPath $source) -Destination (Join-Path $projectPath 'Assets/Plugins')
}
Copy-Item -LiteralPath (Join-Path $repositoryPath 'src/LuminPack.Burst/Runtime') -Destination (Join-Path $projectPath 'Assets/LuminPack.Burst') -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryPath 'src/LuminPack.Unity/Tests~/Editor/UnityObjectContract.cs') -Destination (Join-Path $projectPath 'Assets/Editor')
Copy-Item -LiteralPath (Join-Path $repositoryPath 'src/LuminPack.Burst/Tests~/KernelContract.cs') -Destination (Join-Path $projectPath 'Assets/Editor')
Get-ChildItem -LiteralPath (Join-Path $repositoryPath 'src/LuminPack.Burst/Tests~/Shared') -Filter '*.cs' | Copy-Item -Destination (Join-Path $projectPath 'Assets/Editor')
@'
using System;
using LuminPack.Burst;
using LuminPack.Core;
using Unity.Burst;
using UnityEditor;
using UnityEngine;
public static unsafe class LuminPackExtensionValidation
{
    public static void Run()
    {
        try
        {
            UnityObjectContract.RunAll();
            LuminPack.Unity.ContractFixture.ContractEntry.Run();
            if (!BurstBackend.TryRegister()) LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Compiled SIMD backend is required.");
            LuminPack.Burst.Tests.PortableLayoutContract.Verify((BurstBackend.Capabilities & 6) != 0);
            KernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyFunction>(BurstKernels.Copy).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CountMatchFunction>(BurstKernels.CountMatch).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.XorFunction>(BurstKernels.Xor).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.CopyMatchFunction>(BurstKernels.CopyMatch).Invoke);
            LuminPack.Burst.Tests.TextKernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8EncodeFunction>(BurstKernels.Utf8Encode).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8DecodeFunction>(BurstKernels.Utf8Decode).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Utf8EqualFunction>(BurstKernels.Utf8Equal).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.AsciiEncodeFunction>(BurstKernels.AsciiEncode).Invoke);
            LuminPack.Burst.Tests.TextKernelContract.VerifyBytes(
                BurstCompiler.CompileFunctionPointer<BurstBackend.EqualFunction>(BurstKernels.Equal).Invoke);
            LuminPack.Burst.Tests.Latin1KernelContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.IsLatin1Function>(BurstKernels.IsLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.NarrowLatin1Function>(BurstKernels.NarrowLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.WidenLatin1Function>(BurstKernels.WidenLatin1).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.Latin1EqualFunction>(BurstKernels.Latin1Equal).Invoke);
            LuminPack.Burst.Tests.LayoutCryptoContract.Verify(
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutFunction>(BurstKernels.Layout).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.ChaCha20Function>(BurstKernels.ChaCha20).Invoke);
            LuminPack.Burst.Tests.LayoutCryptoContract.VerifyPlanned(
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutFunction>(BurstKernels.Layout).Invoke,
                BurstCompiler.CompileFunctionPointer<BurstBackend.LayoutPlannedFunction>(BurstKernels.LayoutPlanned).Invoke);
            Debug.Log("LUMINPACK_EXTENSIONS_NATIVE_OK capabilities=" + BurstBackend.Capabilities);
            LuminPackBurst.Clear();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
'@ | Set-Content (Join-Path $projectPath 'Assets/Editor/LuminPackExtensionValidation.cs')
$logFile = Join-Path $artifactPath 'editor.log'
$processInfo = [Diagnostics.ProcessStartInfo]::new($env:UNITY_EDITOR_PATH)
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
foreach ($argument in @('-batchmode', '-nographics', '-quit', '-projectPath', $projectPath, '-executeMethod', 'LuminPackExtensionValidation.Run', '-logFile', $logFile)) {
    $processInfo.ArgumentList.Add($argument)
}
$process = [Diagnostics.Process]::Start($processInfo)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity validation failed with exit code $($process.ExitCode); see $logFile." }
if (-not (Select-String -LiteralPath $logFile -Pattern 'LUMINPACK_EXTENSIONS_NATIVE_OK' -Quiet)) { throw 'Unity exited without the native validation marker.' }
Write-Output "Native Unity objects and Burst algorithms passed: $logFile"
