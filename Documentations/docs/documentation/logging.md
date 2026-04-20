# Logging & Observability

CaeriusNet uses `[LoggerMessage]` source-generated logging for zero-allocation structured log output. Every database operation emits timing, procedure name, and result metadata — enabling diagnostics, performance monitoring, and alerting without custom instrumentation.

## Overview

| Feature | Implementation |
|---|---|
| **Zero-allocation** | `[LoggerMessage]` source generators — no string interpolation at runtime |
| **Structured parameters** | Named placeholders (`{ProcedureName}`, `{Duration}`, `{RowCount}`) |
| **Execution timing** | `Stopwatch.GetElapsedTime` for high-resolution elapsed measurement |
| **Event ID convention** | Categorized by subsystem (see table below) |
| **Provider-agnostic** | Works with any `ILogger` implementation |

## Configuration

### Setting the logger

CaeriusNet uses a static `LoggerProvider` to obtain the logger instance. Configure it during application startup:

```csharp
using CaeriusNet.Logging;

var builder = WebApplication.CreateBuilder(args);

// After building the service provider, set the logger
var app = builder.Build();
LoggerProvider.SetLogger(app.Services.GetRequiredService<ILoggerFactory>());
```

### Integration with dependency injection

When using `CaeriusNetBuilder`, the logger is configured automatically if `ILoggerFactory` is registered in the DI container:

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

### Filtering by event ID range

Use `ILoggerFactory` configuration to filter CaeriusNet logs by event ID ranges:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("CaeriusNet", LogLevel.Information);

    // Show only command execution events (5xxx)
    logging.AddFilter("CaeriusNet.Commands", LogLevel.Debug);

    // Suppress cache hit/miss noise in production
    logging.AddFilter("CaeriusNet.Cache", LogLevel.Warning);
});
```

## Event ID categories

CaeriusNet organizes event IDs by subsystem. Use these ranges to filter, route, or alert on specific categories:

| Range | Category | Description |
|---|---|---|
| **1000–1999** | In-Memory Cache | Cache hit, miss, set, eviction |
| **2000–2999** | Frozen Cache | Cache hit, miss, freeze operations |
| **3000–3999** | Redis Cache | Redis get, set, connection events |
| **4000–4999** | Database / Connection | Connection open, close, pool events |
| **5000–5999** | Command Execution | Start, complete, duration, row count |

### Detailed event reference

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

CaeriusNet logs use semantic (structured) parameters. These are preserved as key-value pairs by structured logging sinks:

| Parameter | Type | Description |
|---|---|---|
| `{ProcedureName}` | `string` | Fully qualified stored procedure name |
| `{Duration}` | `TimeSpan` | Elapsed execution time |
| `{RowCount}` | `int` | Number of rows returned or affected |
| `{CacheKey}` | `string` | Cache key used for lookup/store |
| `{Expiration}` | `TimeSpan` | Cache entry TTL |
| `{ErrorMessage}` | `string` | Exception message on failure |
| `{IsolationLevel}` | `string` | Transaction isolation level |

::: tip Structured logging benefits
Structured parameters enable powerful queries: "Show all executions of `sp_GetUsers` that took longer than 500ms" or "Count cache misses per key in the last hour."
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
        "CaeriusNet.Cache": "Warning",
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
    .WithLogging(logging =>
    {
        logging.AddOtlpExporter();
    });

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

::: details Custom telemetry from CaeriusNet events
Use `ILogger` event subscriptions or middleware to convert CaeriusNet log events into custom Application Insights metrics:

```csharp
// Example: track execution duration as a custom metric
services.AddSingleton<ILoggerProvider, CustomMetricsLoggerProvider>();
```
:::

## Performance considerations

| Aspect | Guidance |
|---|---|
| **Log level filtering** | Set `CaeriusNet.Cache` to `Warning` in production to suppress per-request cache hit/miss noise |
| **High-throughput paths** | `Debug` level events are compiled out when the level is not enabled (source-generator check) |
| **Structured sinks** | Prefer Seq, Elasticsearch, or OTLP over flat-file for queryability |
| **Sampling** | For very high RPS, configure sampling in your telemetry pipeline |

::: warning Avoid excessive logging in hot paths
`Debug`-level cache events fire on every database call. In production, ensure your minimum level is `Information` or higher for CaeriusNet categories to avoid log volume overwhelming sinks.
:::

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No CaeriusNet logs appear | Logger not configured | Call `LoggerProvider.SetLogger(...)` at startup |
| Missing structured parameters | Using flat-text sink | Switch to a structured sink (Seq, OTLP, JSON console) |
| High log volume | Debug level in production | Raise minimum level to Information |
| Missing timing data | Logs filtered too aggressively | Ensure event ID 5002 (command complete) is not filtered |

---

**Next:** [Best Practices](/documentation/best-practices) — recommendations for production-ready CaeriusNet usage.
