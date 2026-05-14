# CaeriusNet Analyzer

Roslyn analyzer project that owns the user-facing `CAERIUS` diagnostics for `[GenerateDto]`, `[GenerateTvp]`, and
SQL Server AutoContracts manifests.

## Purpose

The analyzer validates generator-target shapes and `caerius.contracts.json` content during IDE/CLI compilation, while
`CaeriusNet.Generator` stays focused on incremental discovery, extraction, and source emission. This keeps Roslyn
incremental generators deterministic and keeps all user-facing diagnostics in one analyzer compartment.

## Rules

| ID             | Severity        | Description                                                    |
|----------------|-----------------|----------------------------------------------------------------|
| CAERIUS001     | Error           | Target type must be `sealed`                                   |
| CAERIUS002     | Error           | Target type must be `partial`                                  |
| CAERIUS003     | Error           | Target type must declare a primary constructor with parameters |
| CAERIUS004     | Error           | `[GenerateTvp]` requires a non-empty `TvpName`                 |
| CAERIUS005     | Warning         | A constructor parameter falls back to `sql_variant`            |
| CAERIUS006     | Error           | Generator target must be a supported top-level type            |
| CAERIUS200-210 | Error / Warning | AutoContracts manifest and SQL contract diagnostics            |

## Tests

`CaeriusNet.Analyzer.Tests` runs the analyzer in-memory against small source snippets so rule behavior stays stable
independently from the generator tests.
