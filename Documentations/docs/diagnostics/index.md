# Diagnostic rules

CaeriusNet analyzers validate `[GenerateDto]` and `[GenerateTvp]` contracts at compile time. Diagnostics are emitted by the analyzer included with the `CaeriusNet` package and appear in IDE builds and `dotnet build`.

Use these pages as the rule reference for generated DTO and TVP mapper issues.

| ID | Severity | Summary |
|---|---|---|
| [CAERIUS001](./CAERIUS001) | Error | Type must be `sealed` |
| [CAERIUS002](./CAERIUS002) | Error | Type must be `partial` |
| [CAERIUS003](./CAERIUS003) | Error | Type must declare a primary constructor |
| [CAERIUS004](./CAERIUS004) | Error | `TvpName` must not be empty |
| [CAERIUS005](./CAERIUS005) | Warning | Unmapped CLR type falls back to `sql_variant` |

::: tip Build policy
Keep error diagnostics fixed in source. Treat warning suppressions as design decisions and prefer documenting them near the affected type.
:::

See also the higher-level [Compiler Diagnostics](/documentation/diagnostics) guide for suppression and severity configuration.
