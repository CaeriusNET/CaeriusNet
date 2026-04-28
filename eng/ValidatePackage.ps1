[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts\package-validation"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
}
else {
    Join-Path $repoRoot $OutputDirectory
}

$packageDirectory = Join-Path $outputRoot "packages"
$consumerDirectory = Join-Path $outputRoot "consumer"

Push-Location $repoRoot
try {
    if (Test-Path $outputRoot) {
        Remove-Item $outputRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

    dotnet build "Src\CaeriusNet.csproj" --configuration $Configuration --no-incremental
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    dotnet pack "Src\CaeriusNet.csproj" --configuration $Configuration --no-build --output $packageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed with exit code $LASTEXITCODE."
    }

    $package = Get-ChildItem $packageDirectory -Filter "CaeriusNet.*.nupkg" |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $package) {
        throw "No CaeriusNet .nupkg was produced in '$packageDirectory'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entries = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            [void]$entries.Add($entry.FullName)
        }

        $requiredEntries = @(
            "lib/net10.0/CaeriusNet.dll",
            "lib/net10.0/CaeriusNet.xml",
            "analyzers/dotnet/cs/CaeriusNet.Generator.dll",
            "analyzers/dotnet/cs/CaeriusNet.Analyzer.dll"
        )

        foreach ($requiredEntry in $requiredEntries) {
            if (-not $entries.Contains($requiredEntry)) {
                throw "Package '$($package.Name)' is missing required entry '$requiredEntry'."
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $version = [regex]::Match($package.Name, "^CaeriusNet\.(?<version>.+)\.nupkg$").Groups["version"].Value
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Could not infer package version from '$($package.Name)'."
    }

    New-Item -ItemType Directory -Path $consumerDirectory -Force | Out-Null

    $packageSource = $packageDirectory
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-caeriusnet" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Path (Join-Path $consumerDirectory "NuGet.config") -Encoding UTF8

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CaeriusNet" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $consumerDirectory "PackageSmoke.Consumer.csproj") -Encoding UTF8

    @"
using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;
using CaeriusNet.Mappers;

namespace PackageSmoke;

[GenerateDto]
public sealed partial record CustomerDto(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "CustomerRows")]
public sealed partial record CustomerRow(int Id, string Name);

internal static class Program
{
    private static int Main()
    {
        EnsureDto<CustomerDto>();
        EnsureTvp<CustomerRow>();

        if (CustomerRow.TvpTypeName != "dbo.CustomerRows")
        {
            return 1;
        }

        var mapper = new CustomerRow(1, "Ada");
        using var records = mapper.MapAsSqlDataRecords([mapper]).GetEnumerator();

        return records.MoveNext() ? 0 : 2;
    }

    private static void EnsureDto<T>() where T : class, ISpMapper<T>
    {
    }

    private static void EnsureTvp<T>() where T : class, ITvpMapper<T>
    {
    }
}
"@ | Set-Content -Path (Join-Path $consumerDirectory "Program.cs") -Encoding UTF8

    dotnet run --project (Join-Path $consumerDirectory "PackageSmoke.Consumer.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Package smoke consumer failed with exit code $LASTEXITCODE."
    }

    Write-Host "Package validation succeeded for $($package.Name)."
}
finally {
    Pop-Location
}
