param(
    [string]$BaselineRef = '807f6e83d2454eecccde53a37bc07af37c4f6966',
    [switch]$DenseBenchmark
)

# Compare actual old/new JSON writers in the same process. All generated sources,
# assemblies and build logs stay in the repository's ignored .tmp directory.
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$previousDirectory = Get-Location
$previousTiering = $env:DOTNET_TieredCompilation
try {
    Set-Location -LiteralPath $repositoryRoot
    $baselineDirectory = Join-Path $repositoryRoot '.tmp/redox-baseline'
    $comparisonDirectory = Join-Path $repositoryRoot '.tmp/redox-comparison'
    New-Item -ItemType Directory -Force -Path $baselineDirectory, $comparisonDirectory | Out-Null

    foreach ($name in @('LuminPackJsonWriter', 'LuminPackJsonReader')) {
        $snapshot = git show "${BaselineRef}:src/LuminPack/Core/Json/$name.cs"
        if ($LASTEXITCODE -ne 0) { throw "Could not read $name at $BaselineRef" }
        $snapshot | Set-Content -LiteralPath (Join-Path $baselineDirectory "$name.cs") -Encoding utf8
    }

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>LuminPackBaseline</AssemblyName>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>13</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../src/LuminPack/**/*.cs"
             Exclude="../../src/LuminPack/obj/**;../../src/LuminPack/bin/**;../../src/LuminPack/Core/Json/LuminPackJsonWriter.cs;../../src/LuminPack/Core/Json/LuminPackJsonReader.cs" />
    <Compile Include="LuminPackJsonWriter.cs" />
    <Compile Include="LuminPackJsonReader.cs" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $baselineDirectory 'Baseline.csproj') -Encoding utf8

    dotnet build src/LuminPack/LuminPack.csproj -c Release -f net10.0 --no-restore -p:GeneratePackageOnBuild=false -p:PublishAot=false --nologo -v:q *> (Join-Path $comparisonDirectory 'runtime-build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed; see .tmp/redox-comparison/runtime-build.log' }
    dotnet build (Join-Path $baselineDirectory 'Baseline.csproj') -c Release --nologo -v:q *> (Join-Path $baselineDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Baseline build failed; see .tmp/redox-baseline/build.log' }

    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Raw
    $currentStart = $source.IndexOf('private static int Current')
    $segmentedStart = $source.IndexOf('    [MethodImpl(MethodImplOptions.NoInlining)]', $currentStart)
    $segmentedEnd = $source.IndexOf('    private static byte Hex', $segmentedStart)
    if ($currentStart -lt 0 -or $segmentedStart -lt 0 -or $segmentedEnd -lt 0) {
        throw 'The study harness changed; update CompareRuntime.ps1 before running.'
    }
    $replacement = @'
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Segmented(string value, byte[] buffer)
    {
        Span<byte> destination = buffer;
        var writer = new LuminPackJsonWriter(ref destination, State);
        writer.WriteString(value);
        return writer.CurrentIndex;
    }

'@
    $source = $source.Substring(0, $segmentedStart) + $replacement + $source.Substring($segmentedEnd)
    $source = $source.Replace('private static readonly LuminPackWriterOptionalState State = new();',
        'private static readonly LuminPackWriterOptionalState State = new();' + [Environment]::NewLine +
        '    private static readonly old::LuminPack.Option.LuminPackWriterOptionalState BaselineState = new();')
    $currentStart = $source.IndexOf('private static int Current')
    $currentEnd = $source.IndexOf('    [MethodImpl(MethodImplOptions.NoInlining)]', $currentStart)
    $oldMethod = $source.Substring($currentStart, $currentEnd - $currentStart).Replace(
        'new LuminPackJsonWriter(ref destination, State)',
        'new old::LuminPack.Core.LuminPackJsonWriter(ref destination, BaselineState)')
    $source = $source.Substring(0, $currentStart) + $oldMethod + $source.Substring($currentEnd)
    $source = $source.Replace('new[] { 8, 32, 128, 4096 }', 'new[] { 8, 16, 32, 128, 4096 }')
    $source = $source.Replace('        CheckCorrectness();',
        '        if (OperatingSystem.IsWindows()) Process.GetCurrentProcess().ProcessorAffinity = (nint)1;' +
        [Environment]::NewLine + '        CheckCorrectness();')
    $source = $source.Replace('Current ns | Segmented ns', 'Baseline ns | Runtime ns').Replace(
        'Current B/op | Segmented B/op', 'Baseline B/op | Runtime B/op')
    # Give each implementation a comparable timed duration rather than running
    # the faster implementation for only a small fraction of the baseline's time.
    $calibration = @'
double baselinePilot = Measure(Current, input, output, 1000);
            double runtimePilot = Measure(Segmented, input, output, 1000);
            int baselineIterations = Math.Clamp((int)(50_000_000 / Math.Max(1, baselinePilot)), 1000, 2_000_000);
            int runtimeIterations = Math.Clamp((int)(50_000_000 / Math.Max(1, runtimePilot)), 1000, 2_000_000);
'@
    $source = [regex]::Replace($source,
        'double pilot = Math.Max\([^\r\n]*\);\s*int iterations = Math.Clamp\([^\r\n]*\);', $calibration)
    $source = $source.Replace('Measure(Current, input, output, iterations)',
        'Measure(Current, input, output, baselineIterations)').Replace(
        'Measure(Segmented, input, output, iterations)', 'Measure(Segmented, input, output, runtimeIterations)')
    $denseSource = Join-Path $comparisonDirectory 'DenseStringBenchmark.cs'
    if ($DenseBenchmark) {
        $source = $source.Replace('public static void Main()', 'public static void Main(string[] args)').Replace(
            '        CheckCorrectness();',
            '        if (args.Length > 0) { BenchmarkDotNet.Running.BenchmarkSwitcher.FromTypes(new[] { typeof(DenseStringBenchmark) }).Run(args); return; }' +
            [Environment]::NewLine + '        CheckCorrectness();')
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DenseStringBenchmark.cs.txt') -Destination $denseSource
    }
    elseif (Test-Path -LiteralPath $denseSource) {
        Remove-Item -LiteralPath $denseSource
    }
    ('extern alias old;' + [Environment]::NewLine + $source) |
        Set-Content -LiteralPath (Join-Path $comparisonDirectory 'Program.cs') -Encoding utf8

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="LuminPack"><HintPath>../../src/LuminPack/bin/Release/net10.0/LuminPack.dll</HintPath></Reference>
    <Reference Include="LuminPackBaseline"><HintPath>../redox-baseline/bin/Release/net10.0/LuminPackBaseline.dll</HintPath><Aliases>old</Aliases></Reference>
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $comparisonDirectory 'Comparison.csproj') -Encoding utf8

    if ($DenseBenchmark) {
        [xml]$project = Get-Content -LiteralPath (Join-Path $comparisonDirectory 'Comparison.csproj') -Raw
        $group = $project.CreateElement('ItemGroup')
        $package = $project.CreateElement('PackageReference')
        $package.SetAttribute('Include', 'BenchmarkDotNet')
        $package.SetAttribute('Version', '0.15.6')
        [void]$group.AppendChild($package)
        [void]$project.Project.AppendChild($group)
        $project.Save((Join-Path $comparisonDirectory 'Comparison.csproj'))
    }

    $env:DOTNET_TieredCompilation = '0'
    dotnet build (Join-Path $comparisonDirectory 'Comparison.csproj') -c Release --nologo -v:q *> (Join-Path $comparisonDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Comparison build failed; see .tmp/redox-comparison/build.log' }
    if ($DenseBenchmark) {
        dotnet (Join-Path $comparisonDirectory 'bin/Release/net10.0/Comparison.dll') --filter '*DenseStringBenchmark*' --warmupCount 5 --iterationCount 12 --iterationTime 250 --artifacts BenchmarkDotNet.Artifacts/redox-dense-final *> (Join-Path $comparisonDirectory 'dense.log')
        if ($LASTEXITCODE -ne 0) { throw 'Dense benchmark failed; see .tmp/redox-comparison/dense.log' }
        Write-Output 'Dense benchmark saved to BenchmarkDotNet.Artifacts/redox-dense-final.'
        return
    }
    dotnet (Join-Path $comparisonDirectory 'bin/Release/net10.0/Comparison.dll') |
        Set-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-comparison.md') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Runtime comparison failed' }
    dotnet (Join-Path $comparisonDirectory 'bin/Release/net10.0/Comparison.dll') |
        Set-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-comparison-confirmation.md') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Runtime confirmation failed' }
    Write-Output 'Correctness and timings saved to runtime-comparison*.md.'
}
finally {
    $env:DOTNET_TieredCompilation = $previousTiering
    Set-Location -LiteralPath $previousDirectory
}
