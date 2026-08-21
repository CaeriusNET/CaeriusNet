[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$PackageDirectory = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectDirectories = @(
    "src/CaeriusNet",
    "src/CaeriusNet.Redis",
    "src/CaeriusNet.Aspire",
    "src/CaeriusNet.Contracts",
    "src/Compiler/CaeriusNet.Analyzer",
    "src/Compiler/CaeriusNet.Generator",
    "src/Compiler/CaeriusNet.MultiResultApiGenerator",
    "tools/CaeriusNet.SqlServer.Contracts"
)
$temporaryExtractDirectory = $null

Push-Location $repositoryRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to restore the pinned Source Link tool. Exit code: $LASTEXITCODE."
    }

    $pdbs = @(foreach ($projectDirectory in $projectDirectories) {
        $directory = Join-Path $repositoryRoot $projectDirectory
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Source Link project directory is missing: '$projectDirectory'."
        }

        Get-ChildItem -LiteralPath (Join-Path $directory "bin/$Configuration") -Filter "*.pdb" -File -Recurse -ErrorAction SilentlyContinue
    })

    if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $resolvedPackageDirectory = if ([System.IO.Path]::IsPathRooted($PackageDirectory)) {
            [System.IO.Path]::GetFullPath($PackageDirectory)
        }
        else {
            [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageDirectory))
        }
        $corePackages = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter "CaeriusNet.*.nupkg" -File |
            Where-Object { $_.Name -notlike "*.symbols.nupkg" -and $_.Name -notlike "CaeriusNet.Redis.*" -and $_.Name -notlike "CaeriusNet.Aspire.*" -and $_.Name -notlike "CaeriusNet.Contracts.*" })
        if ($corePackages.Count -ne 1) {
            throw "Expected exactly one CaeriusNet nupkg in '$resolvedPackageDirectory'; found $($corePackages.Count)."
        }

        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
        $temporaryExtractDirectory = Join-Path $temporaryRoot ("caeriusnet-sourcelink-" + [Guid]::NewGuid().ToString("N"))
        [System.IO.Compression.ZipFile]::ExtractToDirectory($corePackages[0].FullName, $temporaryExtractDirectory)
        foreach ($relativePdb in @(
                "analyzers/dotnet/cs/CaeriusNet.Generator.pdb",
                "analyzers/dotnet/cs/CaeriusNet.Analyzer.pdb")) {
            $extractedPdb = Join-Path $temporaryExtractDirectory $relativePdb
            if (-not (Test-Path -LiteralPath $extractedPdb -PathType Leaf)) {
                throw "Packed Source Link PDB is missing: '$relativePdb'."
            }
            $pdbs += Get-Item -LiteralPath $extractedPdb
        }
    }

    if (@($pdbs).Count -eq 0) {
        throw "No release PDBs were found for Source Link validation. Build the shipped projects before invoking this script."
    }

    foreach ($pdb in $pdbs | Sort-Object FullName) {
        Write-Host "Verifying Source Link: $($pdb.FullName)"
        & dotnet tool run sourcelink -- test $pdb.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "Source Link validation failed for '$($pdb.FullName)'. Exit code: $LASTEXITCODE."
        }
    }
}
finally {
    if ($null -ne $temporaryExtractDirectory -and (Test-Path -LiteralPath $temporaryExtractDirectory)) {
        $resolvedTemporary = [System.IO.Path]::GetFullPath($temporaryExtractDirectory)
        $resolvedRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTemporary.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected Source Link temporary directory '$resolvedTemporary'."
        }
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
    }
    Pop-Location
}
