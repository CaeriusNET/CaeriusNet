# Compiler diagnostics

CaeriusNet's Roslyn analyzer emits **compile-time diagnostics** for `[GenerateDto]`, `[GenerateTvp]`, and AutoContracts manifest issues. The analyzer ships inside the `CaeriusNet` NuGet package; no extra reference is required.

Use this page to decide whether a diagnostic must be fixed, can be suppressed, or should be escalated in CI.

## Diagnostic reference

| ID | Severity | Title | Triggered when |
|---|---|---|---|
| [CAERIUS001](/diagnostics/CAERIUS001) | Error | Type must be `sealed` | A DTO or class TVP target is missing `sealed`; record-struct TVPs must instead be `readonly` |
| [CAERIUS002](/diagnostics/CAERIUS002) | Error | Type must be `partial` | A `[GenerateDto]` or `[GenerateTvp]` type is missing the `partial` modifier |
| [CAERIUS003](/diagnostics/CAERIUS003) | Error | Primary constructor required | The type does not declare a primary constructor (or the constructor has no parameters) |
| [CAERIUS004](/diagnostics/CAERIUS004) | Error | `[GenerateTvp]` requires a non-empty `TvpName` | The attribute sets `TvpName` to an empty or whitespace-only string |
| [CAERIUS005](/diagnostics/CAERIUS005) | Error | CLR type has no faithful SQL Server mapping | A constructor parameter has no faithful SQL Server mapping; source emission stops |
| [CAERIUS006](/diagnostics/CAERIUS006) | Error | Generator target shape is not supported | A generator target cannot be represented safely |
| [CAERIUS007](/diagnostics/CAERIUS007) | Error | Manual TVP metadata is invalid | A positional TVP parameter lacks exact `SqlServerTvpColumnAttribute` metadata |
| [CAERIUS008](/diagnostics/CAERIUS008) | Error | DTO source emission failed | A containment boundary prevented an internal generator exception/`CS8785` and emitted no source |
| [CAERIUS200](/diagnostics/CAERIUS200) | Error | AutoContracts manifest is missing | Exactly one canonical v3 `caerius.contracts.json` is not available |
| [CAERIUS201](/diagnostics/CAERIUS201) | Error | Stored procedure could not be found | The manifest references a procedure that is missing from the inspected SQL Server database |
| [CAERIUS202](/diagnostics/CAERIUS202) | Error | Referenced TVP type is missing | A procedure parameter references a TVP type that is missing from SQL Server metadata |
| [CAERIUS203](/diagnostics/CAERIUS203) | Error | TVP SQL type is not supported | A referenced TVP column uses a SQL Server type AutoContracts cannot generate safely |
| [CAERIUS204](/diagnostics/CAERIUS204) | Error | First projection cannot be determined | SQL Server cannot expose a stable first result set for the procedure |
| [CAERIUS205](/diagnostics/CAERIUS205) | Warning | Stored procedure has no result set | The procedure is included for read-contract generation but SQL Server reports no result set |
| [CAERIUS206](/diagnostics/CAERIUS206) | Error | Parameter direction is not supported | A direction/type pairing is invalid; v12 supports outputs and return values |
| [CAERIUS207](/diagnostics/CAERIUS207) | Error | SQL type cannot be mapped | A procedure parameter or projected column uses an unmappable SQL Server type |
| [CAERIUS208](/diagnostics/CAERIUS208) | Warning | Nullable result column | SQL Server metadata marks a projected column nullable and the generated CLR member is nullable |
| [CAERIUS209](/diagnostics/CAERIUS209) | Error | Contract hash differs during Verify | `Verify` found drift between SQL Server metadata and `caerius.contracts.json` |
| [CAERIUS210](/diagnostics/CAERIUS210) | Warning | Procedure may be incompatible with generated contracts | Dynamic SQL, `#temp`, ambiguous metadata or another shape needs an override/exclusion |

## Severity levels

| Severity | Build impact | Action required |
|---|---|---|
| **Error** | Fails compilation | Must be fixed before the project builds |
| **Warning** | Compilation succeeds | Review and fix, or suppress intentionally |

::: tip Full rule pages
For examples and rule-specific guidance, open the individual pages under [Diagnostic rules](/diagnostics/).
:::

## Suppressing diagnostics

### Per declaration with `#pragma`

`CAERIUS005` is a blocking error with no dynamic fallback; do not suppress it. Replace the unsupported type with a
faithful SQL/CLR boundary type or write an explicit mapper outside generator input.

### Project-wide with `.editorconfig`

```ini
[*.cs]
# Promote review warnings when your database contract requires it.
dotnet_diagnostic.CAERIUS208.severity = error
```

### Via `<NoWarn>` in `.csproj`

```xml
<PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);CAERIUS208;CAERIUS210</WarningsAsErrors>
</PropertyGroup>
```

## Escalating severity

Promote warnings to errors to enforce stricter rules in CI:

### Via `.editorconfig`

```ini
[*.cs]
# Require nullable result columns to be reviewed
dotnet_diagnostic.CAERIUS208.severity = error
```

### Via `<WarningsAsErrors>` in `.csproj`

```xml
<PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);CAERIUS208;CAERIUS210</WarningsAsErrors>
</PropertyGroup>
```

::: tip CI enforcement
Escalating AutoContracts warnings such as `CAERIUS205`, `CAERIUS208`, or
`CAERIUS210` in your CI `.editorconfig` prevents accidental contract drift or ambiguous metadata
from reaching production.
:::

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No diagnostics emitted | Analyzer not loaded | Ensure the `CaeriusNet` package is referenced (the analyzer ships in the same package) |
| Diagnostics not visible in IDE | IDE cache stale | Restart the IDE or run `dotnet build` from the CLI |
| `#pragma` suppression has no effect | Wrong scope | Wrap the **type declaration**, not the file or a member |

---

**See also:** [source generators](/documentation/source-generators) - the full guide to `[GenerateDto]` and `[GenerateTvp]`.
