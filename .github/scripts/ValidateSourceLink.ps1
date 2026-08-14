[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
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

Push-Location $repositoryRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to restore the pinned Source Link tool. Exit code: $LASTEXITCODE."
    }

    $pdbs = foreach ($projectDirectory in $projectDirectories) {
        $directory = Join-Path $repositoryRoot $projectDirectory
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Source Link project directory is missing: '$projectDirectory'."
        }

        Get-ChildItem -LiteralPath (Join-Path $directory "bin/$Configuration") -Filter "*.pdb" -File -Recurse -ErrorAction SilentlyContinue
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
    Pop-Location
}
