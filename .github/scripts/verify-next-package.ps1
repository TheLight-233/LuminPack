param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Config,
    [Parameter(Mandatory = $true)][string]$Cache,
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$consumer = if ($Output) { $Output } else { Join-Path $root '.tmp/next-package-consumer' }
New-Item -ItemType Directory -Force -Path $consumer | Out-Null
'<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props')
'<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.targets')
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <LangVersion>13</LangVersion>
    <LuminPackUseInterceptors Condition="'`$(BindingMode)' != 'compiler'">false</LuminPackUseInterceptors>
    <LuminPackUseIlRewriting Condition="'`$(BindingMode)' == 'registration'">false</LuminPackUseIlRewriting>
    <DefineConstants Condition="'`$(BindingMode)' != 'registration'">`$(DefineConstants);STATIC_BINDING</DefineConstants>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="LuminPack" Version="$Version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj')
@'
using System;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

#if STATIC_BINDING
[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
#endif
[LuminPackable] public sealed class Model { public int Value; public string Text; }

public static class Program
{
    public static void Main()
    {
        var value = new Model { Value = 91, Text = "\u4E2D" };
        byte[] bytes = LuminPackSerializer.Serialize<Model, LuminPackUtf8VarIntOptions>(value);
        var binary = LuminPackSerializer.Deserialize<Model, LuminPackUtf8VarIntOptions>(bytes);
        string text = LuminPackSerializer.SerializeJson<Model, LuminPackUtf8LengthOptions>(value);
        var json = LuminPackSerializer.DeserializeJson<Model, LuminPackUtf8LengthOptions>(text);
        if (binary.Value != 91 || binary.Text != value.Text || json.Value != 91 || json.Text != value.Text)
            throw new InvalidOperationException("The installed generator changed the binary or JSON payload.");
    }
}
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs')

$project = Join-Path $consumer 'Consumer.csproj'
dotnet restore $project --configfile $Config --packages $Cache
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($mode in @('compiler', 'il', 'registration')) {
    $output = Join-Path $consumer "bin/$mode"
    dotnet build $project -c Release --no-restore -t:Rebuild -o $output "-p:BindingMode=$mode"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet (Join-Path $output 'Consumer.dll')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    foreach ($assembly in @('LuminPack.SourceGenerator', 'LuminPack.CodeFixes', 'LuminPack.ILPostProcessor', 'Mono.Cecil')) {
        if (Test-Path -LiteralPath (Join-Path $output "$assembly.dll")) {
            throw "Build-only assembly leaked into the consumer output: $assembly."
        }
    }
    Write-Host "Installed Next package passed $mode binding."
}
