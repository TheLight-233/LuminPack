param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Config,
    [Parameter(Mandatory = $true)][string]$Cache
)
$ErrorActionPreference = 'Stop'
$consumer = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) '.tmp/static-package-consumer'
New-Item -ItemType Directory -Force -Path $consumer | Out-Null
'<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.targets')
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType><AllowUnsafeBlocks>true</AllowUnsafeBlocks><LangVersion>preview</LangVersion></PropertyGroup>
  <ItemGroup><PackageReference Include="LuminPack" Version="$Version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj')
@'
using System;
using LuminPack;
using LuminPack.Attribute;
[assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
[LuminPackable] public class Model { public int Value; }
public static class Program
{
    private static byte[] Save<T>(T value) => LuminPackSerializer.Serialize(value);
    public static void Main()
    {
        var value = new Model { Value = 91 };
        if (LuminPackSerializer.Deserialize<Model>(Save(value)).Value != 91) throw new Exception("Closed wrapper was not bound statically.");
        if (LuminPackSerializer.Deserialize<Model>(LuminPackSerializer.Serialize(value)).Value != 91) throw new Exception("Direct call was not bound statically.");
        Console.WriteLine("Packaged static entries passed without registration.");
    }
}
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs')
$project = Join-Path $consumer 'Consumer.csproj'
dotnet restore $project --configfile $Config --packages $Cache
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($interceptors in @('true', 'false')) {
    dotnet run --project $project -c Release --no-restore "-p:LuminPackUseInterceptors=$interceptors"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
