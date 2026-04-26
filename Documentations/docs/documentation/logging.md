# Logging & Observability

CaeriusNet uses **source-generated** `[LoggerMessage]` logging for zero-allocation structured output. Every database operation emits timing, procedure name, and result metadata — enabling diagnostics, performance monitoring, and alerting without custom instrumentation.

For OpenTelemetry tracing and metrics, see [Aspire Integration — Tracing & Telemetry](/documentation/aspire#tracing-telemetry).

## Overview

| Feature | Implementation |
|---|---|
| **Zero-allocation** | `[LoggerMessage]` source generators — no string interpolation at runtime |
| **Structured parameters** | Named placeholders (`{ProcedureName}`, `{Duration}`, `{RowCount}`) |
| **Execution timing** | `Stopwatch.GetElapsedTime` for high-resolution measurement |
| **Event-ID convention** | Categorized by subsystem (see table below) |
| **Provider-agnostic** | Works with any `ILogger` implementation |

## Configuration

### Setting the logger

CaeriusNet uses a static `LoggerProvider` to obtain its logger instance. Configure it once during application startup:

```csharp
using CaeriusNet.Logging;

var builder = WebApplication.CreateBuilder(args);

// ... DI registration ...

var app = builder.Build();
LoggerProvider.SetLogger(app.Services.GetRequiredService<ILoggerFactory>());
```

### Integration with DI

When using `CaeriusNetBuilder`, the logger is wired automatically as long as `ILoggerFactory` is registered in the DI container — `LoggerProvider.SetLogger` is called for you during `Build()`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

CaeriusNetBuilder
    .Create(builder.Services)
    .WithSqlServer(connectionString)
    .Build();
```

### Filtering by category

Use `ILoggerFactory` configuration to filter CaeriusNet logs by subsystem:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("CaeriusNet",          LogLevel.Information);
    logging.AddFilter("CaeriusNet.Commands", LogLevel.Debug);    // verbose for command execution
    logging.AddFilter("CaeriusNet.Cache",    LogLevel.Warning);  // suppress per-call cache noise
});
```

## Event-ID categories

CaeriusNet organizes event IDs by subsystem. Use these ranges to filter, route, or alert on specific categories:

| Range | Category | Description |
|---|---|---|
| **1000–1999** | In-Memory cache | Hit, miss, set, eviction |
| **2000–2999** | Frozen cache | Hit, miss, freeze operations |
| **3000–3999** | Redis cache | Get, set, connection events |
| **4000–4999** | Database / connection | Connection open, close, pool events |
| **5000–5999** | Command execution | Start, complete, duration, row count |

### Event reference

| Event ID | Level | Message template |
|---|---|---|
| 1001 | Debug | `In-memory cache hit for key '{CacheKey}'` |
| 1002 | Debug | `In-memory cache miss for key '{CacheKey}'` |
| 1003 | Information | `In-memory cache set for key '{CacheKey}' with expiration {Expiration}` |
| 2001 | Debug | `Frozen cache hit for key '{CacheKey}'` |
| 2002 | Debug | `Frozen cache miss for key '{CacheKey}'` |
| 2003 | Information | `Frozen cache set for key '{CacheKey}'` |
| 3001 | Debug | `Redis cache hit for key '{CacheKey}'` |
| 3002 | Debug | `Redis cache miss for key '{CacheKey}'` |
| 3003 | Information | `Redis cache set for key '{CacheKey}' with expiration {Expiration}` |
| 3004 | Warning | `Redis connection failed: {ErrorMessage}` |
| 4001 | Debug | `Opening SQL connection` |
| 4002 | Debug | `SQL connection opened in {Duration}` |
| 4003 | Debug | `SQL connection closed` |
| 5001 | Debug | `Executing stored procedure '{ProcedureName}'` |
| 5002 | Information | `Stored procedure '{ProcedureName}' completed in {Duration} ({RowCount} rows)` |
| 5003 | Warning | `Stored procedure '{ProcedureName}' exceeded threshold: {Duration}` |
| 5004 | Error | `Stored procedure '{ProcedureName}' failed: {ErrorMessage}` |

## Structured parameters

CaeriusNet log messages use semantic (structured) placeholders. Structured logging sinks preserve them as queryable key/value pairs:

| Placeholder | Type | Description |
|---|---|---|
| `{ProcedureName}` | `string` | Fully qualified Stored Procedure name (`schema.name`) |
| `{Duration}` | `TimeSpan` | Elapsed execution time |
| `{RowCount}` | `int` | Number of rows returned or affected |
| `{CacheKey}` | `string` | Cache key used for lookup or store |
| `{Expiration}` | `TimeSpan` | Cache entry TTL |
| `{ErrorMessage}` | `string` | Exception message on failure |
| `{IsolationLevel}` | `string` | Transaction isolation level |

::: tip Why this matters
Structured parameters unlock powerful queries — *"all executions of `sp_GetUsers` slower than 500 ms"*, *"cache misses per key in the last hour"*, *"failure rate by procedure name"* — without parsing log text.
:::

## Integration examples

### Serilog

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{EventId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Seq("http://localhost:5341");
});

CaeriusNetBuilder
    .Create(builder.Services)
    .WithSqlServer(connectionString)
    .Build();
```

Filter CaeriusNet events in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "CaeriusNet.Cache":    "Warning",
        "CaeriusNet.Commands": "Debug"
      }
    }
  }
}
```

### OpenTelemetry

Export CaeriusNet logs to an OTLP-compatible backend:

```csharp
builder.Services.AddOpenTelemetry()
    .WithLogging(logging => logging.AddOtlpExporter());

builder.Services.AddLogging(logging =>
{
    logging.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    });
});
```

### Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddLogging(logging =>
{
    logging.AddApplicationInsights();
    logging.AddFilter<ApplicationInsightsLoggerProvider>(
        "CaeriusNet", LogLevel.Information);
});
```

## Performance considerations

| Aspect | Guidance |
|---|---|
| **Log level filtering** | Set `CaeriusNet.Cache` to `Warning` in production to suppress per-call cache noise |
| **Hot paths** | `Debug`-level callsites are compiled out when the level is disabled (the source-generator emits an `IsEnabled` check) |
| **Structured sinks** | Prefer Seq, Elasticsearch, or OTLP over flat-file sinks for queryability |
| **Sampling** | For very high RPS, configure sampling in your telemetry pipeline |

::: warning Avoid excessive logging in hot paths
`Debug`-level cache events fire on every call. In production, ensure your minimum level is `Information` (or higher) for `CaeriusNet.Cache` to avoid log volume drowning your sinks.
:::

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No CaeriusNet logs appear | Logger not configured | Ensure `ILoggerFactory` is registered before `CaeriusNetBuilder.Build()` |
| Missing structured parameters | Flat-text sink in use | Switch to a structured sink (Seq, OTLP, JSON console) |
| High log volume | Debug level enabled in production | Raise the minimum level for `CaeriusNet.Cache` to `Warning` |
| Missing timing data | Event ID 5002 filtered out | Re-enable `CaeriusNet.Commands` at `Information` |

---

**Next:** [Aspire Integration](/documentation/aspire) — connect CaeriusNet to the Aspire dashboard via OpenTelemetry.
