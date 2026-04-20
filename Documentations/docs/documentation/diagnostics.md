# Compiler Diagnostics

CaeriusNet's Roslyn source generators emit compile-time diagnostics to catch configuration and mapping errors before runtime. Each diagnostic has a unique ID, severity, and actionable guidance.

## Diagnostic reference

| ID | Severity | Title | Description |
|---|---|---|---|
| [CAERIUS001](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS001.md) | Error | Type must be `sealed` | A `[GenerateDto]` or `[GenerateTvp]` type is missing the `sealed` modifier |
| [CAERIUS002](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS002.md) | Error | Type must be `partial` | A `[GenerateDto]` or `[GenerateTvp]` type is missing the `partial` modifier |
| [CAERIUS003](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS003.md) | Error | Primary constructor required | The type does not use a primary constructor for its parameters |
| [CAERIUS004](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS004.md) | Error | Empty primary constructor | The primary constructor has no parameters (no columns to map) |
| [CAERIUS005](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS005.md) | Warning | Unmapped CLR type falls back to `sql_variant` | A property type has no native SQL Server mapping |
| [CAERIUS006](https://github.com/CaeriusNET/CaeriusNet/blob/main/Documentations/diagnostics/CAERIUS006.md) | Warning | Unsupported type for SQL Server mapping | A property type uses `sql_variant` fallback with performance implications |

## Severity levels

| Severity | Build impact | Action required |
|---|---|---|
| **Error** | Fails compilation | Must fix before building |
| **Warning** | Compilation succeeds | Review and fix or suppress intentionally |

## How to suppress diagnostics

### Per-declaration with `#pragma`

```csharp
#pragma warning disable CAERIUS005
[GenerateDto]
public sealed partial record FlexibleDto(int Id, object DynamicValue);
#pragma warning restore CAERIUS005
```

### Project-wide with `.editorconfig`

```ini
[*.cs]
# Suppress specific diagnostic
dotnet_diagnostic.CAERIUS005.severity = none

# Downgrade to suggestion
dotnet_diagnostic.CAERIUS006.severity = suggestion
```

### Via `<NoWarn>` in `.csproj`

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);CAERIUS005;CAERIUS006</NoWarn>
</PropertyGroup>
```

## How to escalate severity

Promote warnings to errors to enforce stricter rules in CI:

### Via `.editorconfig`

```ini
[*.cs]
# Treat sql_variant fallback as a build error
dotnet_diagnostic.CAERIUS005.severity = error
dotnet_diagnostic.CAERIUS006.severity = error
```

### Via `<WarningsAsErrors>` in `.csproj`

```xml
<PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);CAERIUS005;CAERIUS006</WarningsAsErrors>
</PropertyGroup>
```

::: tip CI enforcement
Escalating CAERIUS005 and CAERIUS006 to errors in your CI `.editorconfig` prevents accidental `sql_variant` usage from reaching production.
:::

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No diagnostics emitted | Generator not running | Ensure `CaeriusNet` package is referenced and IDE recognizes analyzers |
| Diagnostics not appearing in IDE | IDE cache stale | Restart IDE or run `dotnet build` from CLI |
| Suppression not working | Wrong scope | Ensure `#pragma` wraps the type declaration, not just the file |

---

**See also:** [Source Generators](/documentation/source-generators) — full guide to `[GenerateDto]` and `[GenerateTvp]` code generation.
