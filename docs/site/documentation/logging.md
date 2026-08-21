# Logging and observability

CaeriusNet 12 resolves `ILogger<T>` from application DI and owns no static logger provider. Configure logging with the standard host builder, then register listeners for the public `CaeriusTelemetry` names where required.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(CaeriusTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics.AddMeter(CaeriusTelemetry.MeterName));
```

The core library has no OpenTelemetry SDK dependency; `CaeriusNet.Aspire` performs this registration for its host integration.

## Stable SQL telemetry

Each database execution produces a client activity and duration metric with stable semantic-convention values:

| Attribute/instrument | Value |
| --- | --- |
| `db.system.name` | `microsoft.sql_server` |
| `db.operation.name` | `EXECUTE` |
| `db.stored_procedure.name` | Stored procedure name |
| `db.namespace` / `server.address` | Database namespace / server address |
| `db.response.status_code` | SQL response code when available |
| `error.type` | Exception type on failure |
| `db.client.operation.duration` | Histogram in seconds |

Cache metrics use low-cardinality tier/outcome dimensions. They never include the cache key or stored values.

## Sensitive values

Parameter values are disabled by default. To enable development-only capture, configure both a non-empty `ParameterCaptureAllowList` and a `ParameterRedactor`; validation rejects partial configuration. Connection strings, cache keys and TVP values are never exported.

Use the exception types to differentiate failures: `CaeriusNetSqlException` carries SQL details, `CaeriusContractException` describes contract/cardinality failures, `CaeriusMappingException` locates row mapping failures and `CaeriusCacheException` represents strict cache-mutation errors.
