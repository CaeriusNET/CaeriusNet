# CaeriusNet Analyzer

Roslyn analyzer project that owns the user-facing `CAERIUS` diagnostics for `[GenerateDto]`, `[GenerateTvp]`, and SQL
Server AutoContracts manifests.

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
| CAERIUS005     | Error           | A constructor parameter has no faithful SQL Server mapping     |
| CAERIUS006     | Error           | Generator target must be a supported top-level type            |
| CAERIUS007     | Error           | Manual TVP column metadata must be explicit and faithful       |
| CAERIUS008     | Error           | DTO emission stopped before an internal generator exception    |
| CAERIUS200-210 | Error / Warning | AutoContracts v3 manifest and SQL contract diagnostics         |

`CAERIUS005` blocks source emission; it is not suppressible as a `sql_variant` fallback. `CAERIUS206` accepts valid
`Input`, `Output`, `InputOutput`, and `ReturnValue` directions (TVPs remain input-only). `CAERIUS208` is a warning that
the generated CLR member is nullable. `CAERIUS210` is a warning for dynamic SQL, temporary tables, ambiguous
projections, or another shape that requires an explicit override or exclusion.

## Tests

`CaeriusNet.Analyzer.Tests` runs the analyzer in-memory against small source snippets so rule behavior stays stable
independently from the generator tests.
