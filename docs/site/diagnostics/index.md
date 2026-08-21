# Diagnostic rules

CaeriusNet analyzers validate `[GenerateDto]`, `[GenerateTvp]`, and AutoContracts manifest inputs at compile time. Diagnostics are emitted by the analyzer included with the `CaeriusNet` package and appear in IDE builds and `dotnet build`.

Use these pages as the rule reference for generated DTO, TVP mapper, and AutoContracts manifest issues.

| ID | Severity | Summary |
|---|---|---|
| [CAERIUS001](./CAERIUS001) | Error | Type must be `sealed` |
| [CAERIUS002](./CAERIUS002) | Error | Type must be `partial` |
| [CAERIUS003](./CAERIUS003) | Error | Type must declare a primary constructor |
| [CAERIUS004](./CAERIUS004) | Error | `TvpName` must not be empty |
| [CAERIUS005](./CAERIUS005) | Error | CLR type has no faithful SQL Server mapping |
| [CAERIUS006](./CAERIUS006) | Error | Generator target shape is not supported |
| [CAERIUS007](./CAERIUS007) | Error | Manual TVP column metadata is invalid |
| [CAERIUS008](./CAERIUS008) | Error | DTO source emission failed without a generator exception |
| [CAERIUS200](./CAERIUS200) | Error | AutoContracts manifest is missing |
| [CAERIUS201](./CAERIUS201) | Error | Stored procedure could not be found |
| [CAERIUS202](./CAERIUS202) | Error | Referenced TVP type is missing |
| [CAERIUS203](./CAERIUS203) | Error | TVP SQL type is not supported |
| [CAERIUS204](./CAERIUS204) | Error | First projection cannot be determined |
| [CAERIUS205](./CAERIUS205) | Warning | Stored procedure has no result set |
| [CAERIUS206](./CAERIUS206) | Error | Parameter direction is not supported |
| [CAERIUS207](./CAERIUS207) | Error | SQL type cannot be mapped |
| [CAERIUS208](./CAERIUS208) | Warning | Nullable result column |
| [CAERIUS209](./CAERIUS209) | Error | Contract hash differs during Verify |
| [CAERIUS210](./CAERIUS210) | Warning | Procedure may be incompatible with generated contracts |

::: tip Build policy
Keep error diagnostics fixed in source. `CAERIUS005` has no `sql_variant` fallback, so suppressing it cannot enable
generation. Treat warning suppressions as design decisions and prefer documenting them near the affected contract.
:::

See also the higher-level [Compiler Diagnostics](/documentation/diagnostics) guide for suppression and severity configuration.
