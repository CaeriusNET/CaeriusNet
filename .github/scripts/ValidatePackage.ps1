[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ".work\package-validation",
    [string]$PackageDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$packageIds = @(
    "CaeriusNet",
    "CaeriusNet.Redis",
    "CaeriusNet.Aspire",
    "CaeriusNet.Contracts"
)

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Normalize-DirectoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return ([System.IO.Path]::GetFullPath($Path)).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-SafeOutputDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = Normalize-DirectoryPath $Path
    $root = Normalize-DirectoryPath ([System.IO.Path]::GetPathRoot($normalized))
    $repo = Normalize-DirectoryPath $repoRoot

    if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -eq $root -or $normalized -eq $repo) {
        throw "Refusing to clear unsafe output directory '$Path'."
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

function Get-RequiredPackage {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    $escapedPackageId = [regex]::Escape($PackageId)
    $packageFilePattern = "^$escapedPackageId\." +
        '(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)\.nupkg$'
    $packages = @(
        Get-ChildItem -LiteralPath $Directory -Filter "$PackageId.*.nupkg" -File |
            Where-Object { $_.Name -match $packageFilePattern -and $_.Name -notlike "*.symbols.nupkg" }
    )

    if ($packages.Count -ne 1) {
        throw "Expected exactly one $PackageId .nupkg in '$Directory'; found $($packages.Count)."
    }

    return $packages[0]
}

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    $match = [regex]::Match(
        $Package.Name,
        "^$([regex]::Escape($PackageId))\." +
        '(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)\.nupkg$')
    if (-not $match.Success) {
        throw "Could not infer the version of '$($Package.Name)'."
    }

    return $match.Groups["version"].Value
}

function Get-PackageEntries {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Package)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Package.FullName)
    try {
        $entries = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\')) {
                throw "Package '$($Package.Name)' contains a non-portable ZIP entry '$($entry.FullName)'."
            }

            [void]$entries.Add($entry.FullName)
        }

        return ,$entries
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-PackageEntries {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string[]]$RequiredEntries
    )

    $entries = Get-PackageEntries -Package $Package
    foreach ($requiredEntry in $RequiredEntries) {
        if (-not $entries.Contains($requiredEntry)) {
            throw "Package '$($Package.Name)' is missing required entry '$requiredEntry'."
        }
    }
}

function Assert-PackageExcludesEntries {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string[]]$ForbiddenEntries
    )

    $entries = Get-PackageEntries -Package $Package
    foreach ($forbiddenEntry in $ForbiddenEntries) {
        if ($entries.Contains($forbiddenEntry)) {
            throw "Package '$($Package.Name)' must not contain '$forbiddenEntry'."
        }
    }
}

function Get-PackageEntryText {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$EntryName
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Package.FullName)
    try {
        $entry = $archive.GetEntry($EntryName)
        if ($null -eq $entry) {
            throw "Package '$($Package.Name)' is missing required entry '$EntryName'."
        }

        $stream = $entry.Open()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-Nuspec {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    $nuspec = Get-PackageEntryText -Package $Package -EntryName "$PackageId.nuspec"
    foreach ($expected in @(
            "<id>$PackageId</id>",
            '<license type="expression">MIT</license>',
            '<readme>README.md</readme>',
            '<repository type="git" url="https://github.com/CaeriusNET/CaeriusNet"')) {
        if (-not $nuspec.Contains($expected)) {
            throw "Package '$($Package.Name)' nuspec does not contain '$expected'."
        }
    }
}

function Assert-SymbolPackage {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Package)

    $symbolPackage = Join-Path $Package.DirectoryName ($Package.BaseName + ".snupkg")
    if (-not (Test-Path -LiteralPath $symbolPackage -PathType Leaf)) {
        throw "Symbol package '$symbolPackage' is missing."
    }
}

function Validate-CaeriusNetPackage {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Package)

    Assert-PackageEntries -Package $Package -RequiredEntries @(
        "CaeriusNet.nuspec",
        "README.md",
        "LICENSE",
        "icon.png",
        "lib/net10.0/CaeriusNet.dll",
        "lib/net10.0/CaeriusNet.xml",
        "analyzers/dotnet/cs/CaeriusNet.Generator.dll",
        "analyzers/dotnet/cs/CaeriusNet.Generator.pdb",
        "analyzers/dotnet/cs/CaeriusNet.Analyzer.dll",
        "analyzers/dotnet/cs/CaeriusNet.Analyzer.pdb"
    )
    Assert-Nuspec -Package $Package -PackageId "CaeriusNet"
    Assert-PackageExcludesEntries -Package $Package -ForbiddenEntries @(
        "analyzers/dotnet/cs/CaeriusNet.MultiResults.Generator.dll",
        "analyzers/dotnet/cs/CaeriusNet.MultiResults.Generator.pdb"
    )
    Assert-SymbolPackage -Package $Package
}

function Validate-CaeriusNetRedisPackage {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Package)

    Assert-PackageEntries -Package $Package -RequiredEntries @(
        "CaeriusNet.Redis.nuspec",
        "README.md",
        "LICENSE",
        "icon.png",
        "lib/net10.0/CaeriusNet.Redis.dll",
        "lib/net10.0/CaeriusNet.Redis.xml"
    )
    Assert-Nuspec -Package $Package -PackageId "CaeriusNet.Redis"
    Assert-SymbolPackage -Package $Package
}

function Validate-CaeriusNetAspirePackage {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$Package)

    Assert-PackageEntries -Package $Package -RequiredEntries @(
        "CaeriusNet.Aspire.nuspec",
        "README.md",
        "LICENSE",
        "icon.png",
        "lib/net10.0/CaeriusNet.Aspire.dll",
        "lib/net10.0/CaeriusNet.Aspire.xml"
    )
    Assert-Nuspec -Package $Package -PackageId "CaeriusNet.Aspire"
    Assert-SymbolPackage -Package $Package
}

function Validate-CaeriusNetContractsPackage {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$ExtractDirectory
    )

    Assert-PackageEntries -Package $Package -RequiredEntries @(
        "CaeriusNet.Contracts.nuspec",
        "README.md",
        "LICENSE",
        "icon.png",
        "lib/net10.0/CaeriusNet.Contracts.dll",
        "lib/net10.0/CaeriusNet.Contracts.xml",
        "buildTransitive/CaeriusNet.Contracts.props",
        "buildTransitive/CaeriusNet.Contracts.targets",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.deps.json",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.runtimeconfig.json"
    )
    Assert-Nuspec -Package $Package -PackageId "CaeriusNet.Contracts"
    Assert-PackageExcludesEntries -Package $Package -ForbiddenEntries @(
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.exe",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.pdb"
    )
    Assert-SymbolPackage -Package $Package

    $contractsProps = Get-PackageEntryText -Package $Package -EntryName "buildTransitive/CaeriusNet.Contracts.props"
    if ($contractsProps.Contains("..\tools\net10.0")) {
        throw "Contracts package buildTransitive props contain a Windows-only tool path."
    }
    if (-not $contractsProps.Contains("../tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll")) {
        throw "Contracts package buildTransitive props do not resolve the packaged discovery tool through a portable relative path."
    }

    if (Test-Path -LiteralPath $ExtractDirectory) {
        Remove-Item -LiteralPath $ExtractDirectory -Recurse -Force
    }

    [System.IO.Compression.ZipFile]::ExtractToDirectory($Package.FullName, $ExtractDirectory)
    $toolPath = Join-Path $ExtractDirectory "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll"
    Invoke-DotNet -Arguments @($toolPath, "--help") -FailureMessage "Contracts tool smoke test failed."
}

function Write-PackageHashes {
    param([Parameter(Mandatory = $true)][string]$Directory)

    Write-Host "Package SHA256 hashes:"
    Get-ChildItem -LiteralPath $Directory -File |
        Where-Object { $_.Extension -in ".nupkg", ".snupkg" } |
        Sort-Object Name |
        ForEach-Object {
            $hash = Get-FileHash -Algorithm SHA256 -Path $_.FullName
            Write-Host ("  {0}  {1}" -f $hash.Hash.ToLowerInvariant(), $_.Name)
        }
}

$outputRoot = Resolve-RepositoryPath $OutputDirectory
Assert-SafeOutputDirectory $outputRoot

$useExistingPackages = -not [string]::IsNullOrWhiteSpace($PackageDirectory)
$resolvedPackageDirectory = if ($useExistingPackages) {
    Resolve-RepositoryPath $PackageDirectory
}
else {
    Join-Path $outputRoot "packages"
}

if ($useExistingPackages) {
    $normalizedOutputRoot = Normalize-DirectoryPath $outputRoot
    $normalizedPackageDirectory = Normalize-DirectoryPath $resolvedPackageDirectory
    if ($normalizedPackageDirectory -eq $normalizedOutputRoot -or
        $normalizedPackageDirectory.StartsWith($normalizedOutputRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "PackageDirectory must be outside OutputDirectory when validating existing artifacts."
    }
}

$consumerDirectory = Join-Path $outputRoot "consumer"
$contractsExtractDirectory = Join-Path $outputRoot "contracts-package"
$previousNuGetPackages = $env:NUGET_PACKAGES

Push-Location $repoRoot
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $outputRoot) {
        Remove-Item -LiteralPath $outputRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

    if ($useExistingPackages) {
        if (-not (Test-Path -LiteralPath $resolvedPackageDirectory -PathType Container)) {
            throw "Package directory '$resolvedPackageDirectory' does not exist."
        }
    }
    else {
        New-Item -ItemType Directory -Path $resolvedPackageDirectory -Force | Out-Null

        foreach ($project in @(
                "tools/CaeriusNet.SqlServer.Contracts/CaeriusNet.SqlServer.Contracts.csproj",
                "src/CaeriusNet/CaeriusNet.csproj",
                "src/CaeriusNet.Redis/CaeriusNet.Redis.csproj",
                "src/CaeriusNet.Aspire/CaeriusNet.Aspire.csproj",
                "src/CaeriusNet.Contracts/CaeriusNet.Contracts.csproj")) {
            Invoke-DotNet -Arguments @("build", $project, "--configuration", $Configuration, "--no-incremental") `
                -FailureMessage "dotnet build failed for $project."
        }

        foreach ($project in @(
                "src/CaeriusNet/CaeriusNet.csproj",
                "src/CaeriusNet.Redis/CaeriusNet.Redis.csproj",
                "src/CaeriusNet.Aspire/CaeriusNet.Aspire.csproj",
                "src/CaeriusNet.Contracts/CaeriusNet.Contracts.csproj")) {
            Invoke-DotNet -Arguments @("pack", $project, "--configuration", $Configuration, "--no-build", "--output", $resolvedPackageDirectory) `
                -FailureMessage "dotnet pack failed for $project."
        }
    }

    $packages = @{}
    foreach ($packageId in $packageIds) {
        $packages[$packageId] = Get-RequiredPackage -Directory $resolvedPackageDirectory -PackageId $packageId
    }

    $version = Get-PackageVersion -Package $packages["CaeriusNet"] -PackageId "CaeriusNet"
    foreach ($packageId in $packageIds) {
        $packageVersion = Get-PackageVersion -Package $packages[$packageId] -PackageId $packageId
        if ($packageVersion -ne $version) {
            throw "All release packages must share one version. $packageId has '$packageVersion' while CaeriusNet has '$version'."
        }
    }

    Validate-CaeriusNetPackage -Package $packages["CaeriusNet"]
    Validate-CaeriusNetRedisPackage -Package $packages["CaeriusNet.Redis"]
    Validate-CaeriusNetAspirePackage -Package $packages["CaeriusNet.Aspire"]
    Validate-CaeriusNetContractsPackage -Package $packages["CaeriusNet.Contracts"] -ExtractDirectory $contractsExtractDirectory
    Write-PackageHashes -Directory $resolvedPackageDirectory

    New-Item -ItemType Directory -Path $consumerDirectory -Force | Out-Null
    $env:NUGET_PACKAGES = Join-Path $outputRoot "nuget-packages"

    # Keep the smoke consumer independent even when OutputDirectory is located under the repository.
    # These nearest, empty files prevent the repository's build and central-package settings from leaking in.
    # The copied SDK policy makes this a .NET 10.0.400 package-consumer check, rather than a check against
    # whichever SDK happens to be available on the validation machine.
    Copy-Item -LiteralPath (Join-Path $repoRoot "global.json") -Destination (Join-Path $consumerDirectory "global.json")
    '<Project />' | Set-Content -LiteralPath (Join-Path $consumerDirectory "Directory.Build.props") -Encoding UTF8
    '<Project />' | Set-Content -LiteralPath (Join-Path $consumerDirectory "Directory.Build.targets") -Encoding UTF8
    '<Project />' | Set-Content -LiteralPath (Join-Path $consumerDirectory "Directory.Packages.props") -Encoding UTF8

    $packageSource = [System.Security.SecurityElement]::Escape($resolvedPackageDirectory)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-caeriusnet" value="$packageSource" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-caeriusnet">
      <package pattern="CaeriusNet*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumerDirectory "NuGet.config") -Encoding UTF8

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CaeriusNet" Version="$version" />
    <PackageReference Include="CaeriusNet.Redis" Version="$version" />
    <PackageReference Include="CaeriusNet.Aspire" Version="$version" />
    <PackageReference Include="CaeriusNet.Contracts" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumerDirectory "CaeriusNet.PackageSmoke.csproj") -Encoding UTF8

    @"
{
  "version": 3,
  "serverMajor": 16,
  "features": [],
  "namespace": "PackageSmoke.AutoContracts",
  "tableTypes": [],
  "procedures": [
    {
      "schema": "dbo",
      "name": "PackageLookup",
      "clrName": "PackageLookupProcedure",
      "parametersClrName": "PackageLookupParameters",
      "parameters": [
        { "ordinal": 1, "name": "id", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "Input", "nullable": false }
      ],
      "resultSets": [
        {
          "index": 0,
          "clrName": "PackageLookupRow",
          "status": "Available",
          "columns": [
            { "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }
          ]
        },
        {
          "index": 1,
          "clrName": "PackageLookupSummary",
          "status": "Available",
          "columns": [
            { "ordinal": 0, "name": "Total", "sqlType": "bigint", "clrType": "long", "nullable": false }
          ]
        }
      ]
    }
  ]
}
"@ | Set-Content -LiteralPath (Join-Path $consumerDirectory "caerius.contracts.json") -Encoding UTF8

    @"
using CaeriusNet;
using CaeriusNet.Commands;
using CaeriusNet.Contracts;
using CaeriusNet.Redis;
using PackageSmoke;
using System.Data;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCaeriusNet(options => options.UseSqlServer("Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true"));
services.AddCaeriusNet("reporting", options => options.UseSqlServer("Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true"));
services.AddCaeriusNetRedis();

var command = new StoredProcedureCommandBuilder("dbo", "HealthProbe")
    .AddReturnValue()
    .AddParameter("id", 42, SqlDbType.Int)
    .AddOutputParameter("rows", SqlDbType.Int)
    .WithResultSetCapacities(8, 4)
    .Build();

var dto = new PackageDto(42, "package-only");
var dtoCodec = new PackageDtoCaeriusCacheCodec();
var tvpRows = (IReadOnlyCollection<PackageTvpRow>)[new PackageTvpRow(42)];
var tvpCommand = new StoredProcedureCommandBuilder("dbo", "WriteRows")
    .AddTvpParameter<PackageTvpRow>("rows", tvpRows)
    .Build();

if (command.FullName != "dbo.HealthProbe" || command.Parameters.Length != 3 ||
    dtoCodec.CodecId.Length == 0 || PackageTvpRow.SqlTypeName != "dbo.PackageRows" ||
    tvpCommand.Parameters.Length != 1 || ContractsPackage.ManifestVersion != 3)
{
    return 1;
}

return 0;
"@ | Set-Content -LiteralPath (Join-Path $consumerDirectory "Program.cs") -Encoding UTF8

    @"
using CaeriusNet;
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using PackageSmoke.AutoContracts;
using System.Collections.Immutable;

namespace PackageSmoke;

[GenerateDto]
public sealed partial record PackageDto(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "PackageRows")]
public sealed partial record PackageTvpRow(
    [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);

internal static class PackageOnlyCompileSurface
{
    // These bodies are intentionally never executed. Their compilation verifies that the analyzer/generator
    // assemblies from CaeriusNet.nupkg and the buildTransitive AutoContracts manifest both supply usable APIs.
    internal static ValueTask<(ImmutableArray<PackageDto> Set1, ImmutableArray<PackageDto> Set2)> UseCoreMultiResult(
        ICaeriusNetDbContext context,
        StoredProcedureCommand command) =>
        CaeriusNetMultiResultSetExtensions.QueryMultipleImmutableArrayAsync<PackageDto, PackageDto>(context, command);

    internal static ValueTask<(ImmutableArray<PackageLookupRow> Set1, ImmutableArray<PackageLookupSummary> Set2)> UseAutoContracts(
        ICaeriusNetDbContext context)
    {
        var command = PackageLookupProcedureBuilderExtensions
            .CreateBuilder(new PackageLookupParameters(42))
            .Build();
        return PackageLookupProcedureExecutionExtensions.QueryAsync(context, command);
    }
}
"@ | Set-Content -LiteralPath (Join-Path $consumerDirectory "Contracts.cs") -Encoding UTF8

    if (Get-ChildItem -LiteralPath $consumerDirectory -Recurse -Filter "*.csproj" |
            Select-String -Quiet -Pattern "<ProjectReference") {
        throw "The package smoke consumer must not use ProjectReference."
    }

    Invoke-DotNet -Arguments @("run", "--project", (Join-Path $consumerDirectory "CaeriusNet.PackageSmoke.csproj"), "--configuration", $Configuration) `
        -FailureMessage "Package smoke consumer failed."

    Write-Host "Package validation succeeded for CaeriusNet v$version (core, Redis, Aspire, Contracts)."
}
finally {
    if ($null -eq $previousNuGetPackages) {
        Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
    }
    else {
        $env:NUGET_PACKAGES = $previousNuGetPackages
    }

    Pop-Location
}
