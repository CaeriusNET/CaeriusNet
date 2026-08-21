# Advanced usage

## Named databases

Use keyed DI for isolation rather than mutable global configuration:

```csharp
services.AddCaeriusNet("orders", options => options.UseSqlServer(ordersConnectionString));
services.AddCaeriusNet("reporting", options => options.UseSqlServer(reportingConnectionString));
```

Resolve the intended `ICaeriusNetDbContext` with `[FromKeyedServices("reporting")]`. Each host has its own options, cache managers and diagnostics pipeline.

Production connection strings require `Encrypt=Strict;TrustServerCertificate=False` and a certificate trusted by the
application host. Do not relax certificate validation merely to make a deployment connect.

When a named database is configured through a connection or lease factory and uses Redis, make its cache isolation explicit. `UseSqlServer(string)` derives this automatically, but a factory does not expose a safe stable endpoint until the connection is opened:

```csharp
services.AddCaeriusNet("orders", options =>
{
    options.CacheDatabaseIdentity = "orders-production-primary";
    options.UseSqlServer(CreateOrdersConnection);
});
```

The identity must be stable and non-secret. It is hashed in generated cache keys, so it is safe for host/database isolation but must never be a connection string, credential or tenant data.

## Procedure output contracts

Use `AddOutputParameter`, `AddInputOutputParameter` and `AddReturnValue`, then execute an output-aware method. Reader consumption precedes output extraction, which matters for procedures returning both rows and outputs.

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Work_Queue")
    .AddInputOutputParameter("Cursor", cursor, SqlDbType.BigInt)
    .AddOutputParameter("Remaining", SqlDbType.Int)
    .Build();

var result = await database.ExecuteWithOutputsAsync(command, ct);
var remaining = result.Output.Get<int>("Remaining");
```

## Multi-result policy

Default multi-result APIs reject missing or surplus sets. Supply `CaeriusResultSetCountPolicy` only for an explicitly variable procedure and keep its documented range small. Set capacity values per set instead of relying on one oversized buffer.

## Development diagnostics

Use an allowlist/redactor pair when parameter capture is genuinely necessary. Do not record connection strings, cache keys or TVP values. For SQL-version checks, enable the optional Aspire health check rather than testing the server on each command path.

## Performance work

Benchmark complete scenarios before retaining optimization attributes: mapper cardinality, scalar conversion, empty/large TVPs, 1/2/5/10 result sets, collection forms, cache hit/miss and Redis codecs. A change that regresses a hot path by more than five percent or adds allocations needs an explanation or correction before release.
