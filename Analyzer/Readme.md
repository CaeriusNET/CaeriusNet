# CaeriusNet Analyzer

Roslyn analyzer project that owns the user-facing `CAERIUS` diagnostics for `[GenerateDto]` and `[GenerateTvp]`.

## Purpose

The analyzer validates generator-target shapes and reports diagnostics during IDE/CLI compilation, while
`CaeriusNet.Generator` stays focused on incremental discovery, extraction, and source emission.

## Rules

| ID         | Severity | Description                                                    |
|------------|----------|----------------------------------------------------------------|
| CAERIUS001 | Error    | Target type must be `sealed`                                   |
| CAERIUS002 | Error    | Target type must be `partial`                                  |
| CAERIUS003 | Error    | Target type must declare a primary constructor with parameters |
| CAERIUS004 | Error    | `[GenerateTvp]` requires a non-empty `TvpName`                 |
| CAERIUS005 | Warning  | A constructor parameter falls back to `sql_variant`            |

## Tests

`CaeriusNet.Analyzer.Tests` runs the analyzer in-memory against small source snippets so rule behavior stays stable
independently from the generator tests.
