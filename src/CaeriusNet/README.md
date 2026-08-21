# CaeriusNet

CaeriusNet is the .NET 10 / C# 14 runtime for SQL Server 2022 and 2025 stored procedures. Building v12 requires SDK
10.0.400 with patch roll-forward and Roslyn 5.9 or later. It provides dependency-injection registration, typed mapping,
TVPs, explicit result cardinalities, transactions, local cache managers, BCL telemetry, the DTO/TVP generator, and the
analyzer.

## Install

```bash
dotnet add package CaeriusNet --version 11.2.0
```

## Quick start

```csharp
using CaeriusNet;
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;

services.AddCaeriusNet(options => options.UseSqlServer(connectionString));

public sealed record Product(int Id, string Name) : ISpMapper<Product>
{
    public static Product MapFromDataReader(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetString(1));
}

var command = new StoredProcedureCommandBuilder("dbo", "usp_Product_Get")
    .AddParameter("Id", productId, SqlDbType.Int)
    .Build();

var product = await database.QuerySingleAsync<Product>(command, cancellationToken);
```

`StoredProcedureCommand` is immutable once built. Inputs, directions, ordinals, SQL facets and TVP metadata are
validated before a connection is opened; parameters are created directly with their declared `SqlDbType`—never with
`AddWithValue`.

## Named databases

```csharp
services.AddCaeriusNet("reporting", options => options.UseSqlServer(reportingConnectionString));

public sealed class ReportService(
    [FromKeyedServices("reporting")] ICaeriusNetDbContext reporting)
{
}
```

## Runtime model

- Stored procedures only: each command uses `CommandType.StoredProcedure`; inline SQL is unsupported.
- Cardinality APIs distinguish no row, `DBNull`, multiple rows and conversion failures.
- `QueryAsync<T>` is materialized even though it returns `IEnumerable<T>`; use `StreamAsync<T>` for an explicitly scoped
  single-result stream.
- Multi-result APIs cover 2–10 sets and all three materialized families. Result-set count is exact unless a
  `CaeriusResultSetCountPolicy` explicitly permits variation.
- `[GenerateDto]` and `[GenerateTvp]` generate mapping and typed-cache-codec support. `[GenerateTvp]` accepts a sealed
  positional record class or a readonly positional record struct; a manual TVP declares exact metadata with
  `SqlServerTvpColumnAttribute` on every positional parameter. Manual Redis-cached result types must implement and
  register `ICaeriusCacheCodec<T>`.
- Memory cache is TTL/sliding and single-flight; Frozen cache is an atomically swapped snapshot and never rebuilds on a
  miss.
- Redis cache keys are isolated by a non-secret database identity. `UseSqlServer(string)` derives it from the
  endpoint/catalog; connection or lease factories must set `CacheDatabaseIdentity` explicitly. Redis read-through
  collapses concurrent lookup, SQL fallback and population per canonical key in each host. Keys use the stable
  `caerius:v2:<sha256>` form and cache dependencies are declared with `DependsOn(...)`; durable writes target them with
  `Invalidates(...)`.
- The replayable TVP overload takes `IReadOnlyCollection<T>`; the streaming overload takes
  `Func<CancellationToken, IEnumerable<T>>`, is not materialized and makes the command non-cacheable.
- Transactions use one connection, reject concurrent commands, bypass read-through cache and run invalidations after a
  successful commit. Their materialized and multi-result APIs have output-aware counterparts; outputs are observed only
  after readers are fully consumed. `transaction.StreamAsync<T>` owns the transaction command slot until enumeration
  ends, and an incomplete enumeration requires rollback. A commit or rollback token is observed before the terminal SQL
  operation begins; after that point CaeriusNet completes the terminal operation and post-commit invalidations without
  caller cancellation, so an aborted request cannot leave a durable write behind a stale cache entry. If a completed
  write cannot invalidate cache data, `CaeriusCacheInvalidationException.SqlWasCommitted` is `true`; reconcile instead
  of blindly retrying a non-idempotent procedure.

`CaeriusNet.Redis`, `CaeriusNet.Aspire`, and `CaeriusNet.Contracts` are separate packages. See
the [repository README](https://github.com/CaeriusNET/CaeriusNet), [migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md), [documentation](https://caerius.net),
and [issue tracker](https://github.com/CaeriusNET/CaeriusNet/issues).
