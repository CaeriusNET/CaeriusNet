[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = ".work\package-validation",
    [string]$PackageDirectory = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

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
    $root = (Normalize-DirectoryPath ([System.IO.Path]::GetPathRoot($normalized)))
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
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][scriptblock]$Filter,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $packages = @(Get-ChildItem $Directory -Filter $Pattern -File |
        Where-Object $Filter |
        Sort-Object LastWriteTime -Descending)

    if ($packages.Count -eq 0) {
        throw "No $Description was found in '$Directory'."
    }

    if ($packages.Count -gt 1) {
        Write-Warning "Multiple $Description files were found in '$Directory'. Validating newest: $($packages[0].Name)."
    }

    return $packages[0]
}

function Get-PackageVersion {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$PackageId
    )

    $escapedId = [regex]::Escape($PackageId)
    $match = [regex]::Match($Package.Name, "^$escapedId\.(?<version>.+)\.nupkg$")
    if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups["version"].Value)) {
        throw "Could not infer package version from '$($Package.Name)'."
    }

    return $match.Groups["version"].Value
}

function Assert-PackageEntries {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string[]]$RequiredEntries
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Package.FullName)
    try {
        $entries = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.Contains('\')) {
                throw "Package '$($Package.Name)' contains a non-portable ZIP entry '$($entry.FullName)'."
            }

            [void]$entries.Add($entry.FullName)
        }

        foreach ($requiredEntry in $RequiredEntries) {
            if (-not $entries.Contains($requiredEntry)) {
                throw "Package '$($Package.Name)' is missing required entry '$requiredEntry'."
            }
        }
    }
    finally {
        $archive.Dispose()
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
        if (-not $entry) {
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

function Assert-PackageTextContains {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$EntryName,
        [Parameter(Mandatory = $true)][string[]]$ExpectedText
    )

    $text = Get-PackageEntryText -Package $Package -EntryName $EntryName
    foreach ($expected in $ExpectedText) {
        if (-not $text.Contains($expected)) {
            throw "Package '$($Package.Name)' entry '$EntryName' does not contain expected text '$expected'."
        }
    }
}

function Assert-PackageTextDoesNotContain {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$EntryName,
        [Parameter(Mandatory = $true)][string[]]$DisallowedText
    )

    $text = Get-PackageEntryText -Package $Package -EntryName $EntryName
    foreach ($disallowed in $DisallowedText) {
        if ($text.Contains($disallowed)) {
            throw "Package '$($Package.Name)' entry '$EntryName' contains disallowed text '$disallowed'."
        }
    }
}

function Write-PackageHashes {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $packages = @(Get-ChildItem $Directory -File |
        Where-Object { $_.Extension -in ".nupkg", ".snupkg" } |
        Sort-Object Name)

    if ($packages.Count -eq 0) {
        return
    }

    Write-Host "Package SHA256 hashes:"
    foreach ($package in $packages) {
        $hash = Get-FileHash -Algorithm SHA256 -Path $package.FullName
        Write-Host ("  {0}  {1}" -f $hash.Hash.ToLowerInvariant(), $package.Name)
    }
}

function Validate-CaeriusNetPackage {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$Package,
        [Parameter(Mandatory = $true)][string]$ExtractDirectory
    )

    Assert-PackageEntries -Package $Package -RequiredEntries @(
        "CaeriusNet.nuspec",
        "README.md",
        "LICENSE",
        "lib/net10.0/CaeriusNet.dll",
        "lib/net10.0/CaeriusNet.xml",
        "analyzers/dotnet/cs/CaeriusNet.Generator.dll",
        "analyzers/dotnet/cs/CaeriusNet.Analyzer.dll",
        "buildTransitive/CaeriusNet.props",
        "buildTransitive/CaeriusNet.targets",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.deps.json",
        "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.runtimeconfig.json",
        "tools/net10.0/any/Microsoft.Data.SqlClient.dll",
        "tools/net10.0/any/Microsoft.Extensions.Configuration.EnvironmentVariables.dll",
        "tools/net10.0/any/Microsoft.Extensions.Configuration.Json.dll"
    )

    Assert-PackageTextContains -Package $Package -EntryName "CaeriusNet.nuspec" -ExpectedText @(
        "<id>CaeriusNet</id>",
        "<license type=`"expression`">MIT</license>",
        "<readme>README.md</readme>",
        "<repository type=`"git`" url=`"https://github.com/CaeriusNET/CaeriusNet`""
    )

    Assert-PackageTextDoesNotContain -Package $Package -EntryName "CaeriusNet.nuspec" -DisallowedText @(
        "<id>CaeriusNet.SqlServer.Contracts</id>",
        "DotnetTool",
        "<packageTypes"
    )

    if (Test-Path $ExtractDirectory) {
        Remove-Item $ExtractDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $ExtractDirectory -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Package.FullName, $ExtractDirectory)
    $toolPath = Join-Path $ExtractDirectory "tools/net10.0/any/CaeriusNet.SqlServer.Contracts.dll"
    if (-not (Test-Path $toolPath)) {
        throw "Extracted CaeriusNet package is missing '$toolPath'."
    }

    Invoke-DotNet -Arguments @($toolPath, "--help") -FailureMessage "Contracts tool smoke test failed."
}

$outputRoot = Resolve-RepositoryPath $OutputDirectory
Assert-SafeOutputDirectory $outputRoot

$useExistingPackages = -not [string]::IsNullOrWhiteSpace($PackageDirectory)
$packageDirectory = if ($useExistingPackages) {
    Resolve-RepositoryPath $PackageDirectory
}
else {
    Join-Path $outputRoot "packages"
}

$consumerDirectory = Join-Path $outputRoot "consumer"
$packageExtractDirectory = Join-Path $outputRoot "caeriusnet-package"
$previousNuGetPackages = $env:NUGET_PACKAGES

Push-Location $repoRoot
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path $outputRoot) {
        Remove-Item $outputRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

    if ($useExistingPackages) {
        if (-not (Test-Path $packageDirectory)) {
            throw "Package directory '$packageDirectory' does not exist."
        }
    }
    else {
        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

        Invoke-DotNet -Arguments @("build", "Tools\CaeriusNet.SqlServer.Contracts\CaeriusNet.SqlServer.Contracts.csproj", "--configuration", $Configuration, "--no-incremental") `
            -FailureMessage "dotnet build failed for CaeriusNet.SqlServer.Contracts."

        Invoke-DotNet -Arguments @("build", "Src\CaeriusNet.csproj", "--configuration", $Configuration, "--no-incremental") `
            -FailureMessage "dotnet build failed for CaeriusNet."

        Invoke-DotNet -Arguments @("pack", "Src\CaeriusNet.csproj", "--configuration", $Configuration, "--no-build", "--output", $packageDirectory) `
            -FailureMessage "dotnet pack failed for CaeriusNet."
    }

    $mainPackage = Get-RequiredPackage `
        -Directory $packageDirectory `
        -Pattern "CaeriusNet.*.nupkg" `
        -Filter { $_.Name -match '^CaeriusNet\.[0-9]' -and $_.Name -notlike "*.symbols.nupkg" } `
        -Description "CaeriusNet .nupkg"

    $legacyContractsPackages = @(Get-ChildItem $packageDirectory -Filter "CaeriusNet.SqlServer.Contracts.*.nupkg" -File)
    if ($legacyContractsPackages.Count -gt 0) {
        throw "Found legacy standalone AutoContracts package(s) in '$packageDirectory': $($legacyContractsPackages.Name -join ', '). Publish only the CaeriusNet package."
    }

    Validate-CaeriusNetPackage -Package $mainPackage -ExtractDirectory $packageExtractDirectory
    Write-PackageHashes -Directory $packageDirectory

    $version = Get-PackageVersion -Package $mainPackage -PackageId "CaeriusNet"

    New-Item -ItemType Directory -Path $consumerDirectory -Force | Out-Null
    $env:NUGET_PACKAGES = Join-Path $outputRoot "nuget-packages"

    $packageSource = [System.Security.SecurityElement]::Escape($packageDirectory)
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
      <package pattern="CaeriusNet" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
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
{
  "version": 1,
  "namespace": "PackageSmoke.Contracts",
  "tableTypes": [
    {
      "schema": "dbo",
      "name": "CustomerIdRows",
      "clrName": "CustomerIdRowsTvp",
      "columns": [
        { "ordinal": 1, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }
      ],
      "contractHash": "sha256:table"
    }
  ],
  "procedures": [
    {
      "schema": "dbo",
      "name": "Customer_Search",
      "clrName": "CustomerSearchProcedure",
      "parametersClrName": "CustomerSearchParameters",
      "resultClrName": "CustomerSearchResult",
      "parameters": [
        {
          "ordinal": 1,
          "name": "Ids",
          "sqlType": "dbo.CustomerIdRows",
          "clrType": "ReadOnlyMemory<CustomerIdRowsTvp>",
          "isTableType": true,
          "nullable": false
        },
        {
          "ordinal": 2,
          "name": "IncludeDisabled",
          "sqlType": "bit",
          "clrType": "bool",
          "isTableType": false,
          "nullable": false
        }
      ],
      "resultSet": {
        "status": "Available",
        "columns": [
          { "ordinal": 1, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false },
          { "ordinal": 2, "name": "Name", "sqlType": "nvarchar(64)", "clrType": "string", "nullable": false, "maxLength": 128 }
        ]
      },
      "contractHash": "sha256:procedure"
    }
  ]
}
"@ | Set-Content -Path (Join-Path $consumerDirectory "caerius.contracts.json") -Encoding UTF8

    @"
using CaeriusNet.Attributes.Dto;
using CaeriusNet.Attributes.Tvp;
using CaeriusNet.Builders;
using CaeriusNet.Mappers;
using PackageSmoke.Contracts;

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

        if (!records.MoveNext())
        {
            return 2;
        }

        ReadOnlyMemory<CustomerIdRowsTvp> ids = new CustomerIdRowsTvp[] { new(1), new(2) };
        var autoContractParameters = new CustomerSearchParameters(ids, IncludeDisabled: false);
        var typed = StoredProcedureParametersBuilder<CustomerSearchProcedure>
            .Create(autoContractParameters, resultSetCapacity: ids.Length)
            .Build();

        if (typed.SchemaName != "dbo" || typed.ProcedureName != "Customer_Search")
        {
            return 3;
        }

        if (typed.GetParametersSpan().Length != 2)
        {
            return 4;
        }

        return 0;
    }

    private static void EnsureDto<T>() where T : class, ISpMapper<T>
    {
    }

    private static void EnsureTvp<T>() where T : class, ITvpMapper<T>
    {
    }
}
"@ | Set-Content -Path (Join-Path $consumerDirectory "Program.cs") -Encoding UTF8

    Invoke-DotNet -Arguments @("run", "--project", (Join-Path $consumerDirectory "PackageSmoke.Consumer.csproj"), "--configuration", "Release") `
        -FailureMessage "Package smoke consumer failed."

    Write-Host "Package validation succeeded for $($mainPackage.Name)."
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
