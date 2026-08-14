# Diagnostic rules

CaeriusNet analyzers validate `[GenerateDto]`, `[GenerateTvp]`, and AutoContracts manifest inputs at compile time. Diagnostics are emitted by the analyzer included with the `CaeriusNet` package and appear in IDE builds and `dotnet build`.

Use these pages as the rule reference for generated DTO, TVP mapper, and AutoContracts manifest issues.

| ID | Severity | Summary |
|---|---|---|
| [CAERIUS001](./CAERIUS001) | Error | Type must be `sealed` |
| [CAERIUS002](./CAERIUS002) | Error | Type must be `partial` |
| [CAERIUS003](./CAERIUS003) | Error | Type must declare a primary constructor |
| [CAERIUS004](./CAERIUS004) | Error | `TvpName` must not be empty |
| [CAERIUS005](./CAERIUS005) | Warning | Unmapped CLR type falls back to `sql_variant` |
| [CAERIUS006](./CAERIUS006) | Error | Generator target shape is not supported |
| [CAERIUS200](./CAERIUS200) | Error | AutoContracts manifest is missing |
| [CAERIUS201](./CAERIUS201) | Error | Stored procedure could not be found |
| [CAERIUS202](./CAERIUS202) | Error | Referenced TVP type is missing |
| [CAERIUS203](./CAERIUS203) | Error | TVP SQL type is not supported |
| [CAERIUS204](./CAERIUS204) | Error | First projection cannot be determined |
| [CAERIUS205](./CAERIUS205) | Warning | Stored procedure has no result set |
| [CAERIUS206](./CAERIUS206) | Error | `OUTPUT` parameters are not supported |
| [CAERIUS207](./CAERIUS207) | Error | SQL type cannot be mapped |
| [CAERIUS208](./CAERIUS208) | Policy-dependent | Nullable column violates policy |
| [CAERIUS209](./CAERIUS209) | Error | Contract hash differs during Verify |
| [CAERIUS210](./CAERIUS210) | Warning | Stored procedure is probably incompatible |

::: tip Build policy
Keep error diagnostics fixed in source. Treat warning suppressions as design decisions and prefer documenting them near the affected type or AutoContracts manifest entry.
:::

See also the higher-level [Compiler Diagnostics](/documentation/diagnostics) guide for suppression and severity configuration.
