# Compiler Diagnostics

CaeriusNet's Roslyn analyzer emits **compile-time diagnostics** for `[GenerateDto]` and `[GenerateTvp]` contract issues. The analyzer ships inside the `CaeriusNet` NuGet package; no extra reference is required.

Use this page to decide whether a diagnostic must be fixed, can be suppressed, or should be escalated in CI.

## Diagnostic reference

| ID | Severity | Title | Triggered when |
|---|---|---|---|
| [CAERIUS001](/diagnostics/CAERIUS001) | Error | Type must be `sealed` | A `[GenerateDto]` or `[GenerateTvp]` type is missing the `sealed` modifier |
| [CAERIUS002](/diagnostics/CAERIUS002) | Error | Type must be `partial` | A `[GenerateDto]` or `[GenerateTvp]` type is missing the `partial` modifier |
| [CAERIUS003](/diagnostics/CAERIUS003) | Error | Primary constructor required | The type does not declare a primary constructor (or the constructor has no parameters) |
| [CAERIUS004](/diagnostics/CAERIUS004) | Error | `[GenerateTvp]` requires a non-empty `TvpName` | The attribute sets `TvpName` to an empty or whitespace-only string |
| [CAERIUS005](/diagnostics/CAERIUS005) | Warning | Unmapped CLR type falls back to `sql_variant` | A constructor parameter has a type with no native SQL Server mapping |

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

```csharp
#pragma warning disable CAERIUS005
[GenerateDto]
public sealed partial record FlexibleDto(int Id, object DynamicValue);
#pragma warning restore CAERIUS005
```

### Project-wide with `.editorconfig`

```ini
[*.cs]
# Suppress entirely
dotnet_diagnostic.CAERIUS005.severity = none

# Or downgrade to a suggestion
# dotnet_diagnostic.CAERIUS005.severity = suggestion
```

### Via `<NoWarn>` in `.csproj`

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);CAERIUS005</NoWarn>
</PropertyGroup>
```

## Escalating severity

Promote warnings to errors to enforce stricter rules in CI:

### Via `.editorconfig`

```ini
[*.cs]
# Treat sql_variant fallback as a build error
dotnet_diagnostic.CAERIUS005.severity = error
```

### Via `<WarningsAsErrors>` in `.csproj`

```xml
<PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);CAERIUS005</WarningsAsErrors>
</PropertyGroup>
```

::: tip CI enforcement
Escalating `CAERIUS005` to an error in your CI `.editorconfig` prevents an accidental `sql_variant` fallback from reaching production.
:::

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No diagnostics emitted | Analyzer not loaded | Ensure the `CaeriusNet` package is referenced (the analyzer ships in the same package) |
| Diagnostics not visible in IDE | IDE cache stale | Restart the IDE or run `dotnet build` from the CLI |
| `#pragma` suppression has no effect | Wrong scope | Wrap the **type declaration**, not the file or a member |

---

**See also:** [Source Generators](/documentation/source-generators) — the full guide to `[GenerateDto]` and `[GenerateTvp]`.
