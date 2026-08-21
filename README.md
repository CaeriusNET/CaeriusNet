# CaeriusNet

<p align="center">
  <a href="https://www.nuget.org/packages/CaeriusNet"><img src="https://img.shields.io/nuget/v/CaeriusNet?style=flat&logo=nuget" alt="NuGet CaeriusNet"></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/C%23-14-239120?style=flat&logo=csharp&logoColor=white" alt="C# 14">
  <img src="https://img.shields.io/badge/SQL%20Server-2022%20%7C%202025-CC2927?style=flat&logo=microsoftsqlserver&logoColor=white" alt="SQL Server 2022 or 2025">
  <img src="https://img.shields.io/badge/license-MIT-blue?style=flat" alt="MIT">
</p>

CaeriusNet 12 is a stored-procedure-only data-access runtime for .NET 10 and C# 14. It maps result rows without reflection, supports table-valued parameters (TVPs), cardinality-aware reads, transactions, typed cache codecs, and SQL Server 2022/2025. Building v12 requires the .NET SDK `10.0.400` (patch roll-forward) and Roslyn `5.9` or later.

The v12 architecture is distributed as package version 11.2.0. It replaces the earlier v11 builder and its global state with dependency injection; read the [v11 to v12 migration guide](docs/MIGRATION-v11-v12.md) before upgrading. Maintainers can follow the complete invariants, evidence and release gates in the [11.2.0 engineering contract](docs/engineering/caeriusnet-v12-release-engineering-contract.md).

## Packages

| Package | Purpose |
| --- | --- |
| [`CaeriusNet`](src/CaeriusNet/README.md) | Runtime, DI, transactions, mappers, local caches, telemetry, DTO/TVP generator and analyzer. |
| [`CaeriusNet.Redis`](src/CaeriusNet.Redis/README.md) | Fully asynchronous typed Redis cache manager and binary envelope. |
| [`CaeriusNet.Aspire`](src/CaeriusNet.Aspire/README.md) | Aspire SQL Server/Redis clients, health checks, configuration and telemetry wiring. |
| [`CaeriusNet.Contracts`](src/CaeriusNet.Contracts/README.md) | Opt-in AutoContracts discovery tool and `buildTransitive` integration. |

The `CaeriusNet` package ships only its Analyzer and Generator under `analyzers/dotnet/cs`, alongside their Source Link
PDBs. The multi-result API is already compiled into `CaeriusNet.dll`; no consumer-facing MultiResultApiGenerator is
packaged. Release packaging validates the extracted PDBs and requires two consecutive packs to be bit-for-bit identical.

## Install and configure

```bash
dotnet add package CaeriusNet --version 11.2.0
```

```csharp
using CaeriusNet;
using CaeriusNet.Abstractions;
using Microsoft.Extensions.DependencyInjection;

services.AddCaeriusNet(options =>
    options.UseSqlServer("Server=.;Database=Orders;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"));
```

For a second database, add a keyed context and request it through normal .NET keyed DI:

```csharp
services.AddCaeriusNet("reporting", options => options.UseSqlServer(reportingConnectionString));

public sealed class ReportingService(
    [FromKeyedServices("reporting")] ICaeriusNetDbContext database)
{
}
```

## First stored-procedure call

Every runtime command is a `CommandType.StoredProcedure` call. CaeriusNet neither accepts inline SQL nor offers a query builder.

```csharp
using CaeriusNet.Commands;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;
using System.Data;

public sealed record Customer(int Id, string Name) : ISpMapper<Customer>
{
    public static Customer MapFromDataReader(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetString(1));
}

var command = new StoredProcedureCommandBuilder("dbo", "usp_Customers_ByCountry")
    .AddParameter("CountryCode", "FR", SqlDbType.Char, size: 2)
    .Build();

var customers = await database.QueryReadOnlyCollectionAsync<Customer>(command, cancellationToken: ct);
```

`QueryAsync<T>` materializes before returning its `IEnumerable<T>`. Use `StreamAsync<T>` only when streaming one result set and the reader lifetime is intentionally part of the caller's async enumeration.

## Choose the result contract

| Intent | API |
| --- | --- |
| One required row / optional first row | `QueryFirstAsync<T>` / `QueryFirstOrDefaultAsync<T>` |
| Exactly one row / zero or one row | `QuerySingleAsync<T>` / `QuerySingleOrDefaultAsync<T>` |
| Materialized rows | `QueryAsync<T>`, `QueryReadOnlyCollectionAsync<T>`, `QueryImmutableArrayAsync<T>` |
| One result set streamed | `StreamAsync<T>` |
| Required scalar / default only when the row is absent | `ExecuteScalarAsync<T>` / `ExecuteScalarOrDefaultAsync<T>` |
| No business result / affected-row count | `ExecuteAsync` / `ExecuteNonQueryAsync` |

Cardinality, mapping, result-set shape and SQL failures are reported as `CaeriusContractException`, `CaeriusMappingException`, and `CaeriusNetSqlException`; cancellation remains `OperationCanceledException`.
Both scalar APIs keep an existing SQL `NULL` distinct from an absent row: nullable/reference `T` receives `null`, while a non-nullable `T` raises `CaeriusContractException`.

## TVPs, result sets and outputs

`[GenerateDto]` and `[GenerateTvp]` generate source-only mappers. A generated TVP writes rows into a reused `SqlDataRecord`, including for empty input, so it avoids `DataTable` allocation.

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "OrderLineType")]
public sealed partial record OrderLine(int ProductId, int Quantity);

IReadOnlyCollection<OrderLine> lines = [new(42, 2)];

var command = new StoredProcedureCommandBuilder("dbo", "usp_Order_AddLines")
    .AddTvpParameter("Lines", lines)      // replayable and eligible for caching
    .AddOutputParameter("OrderId", SqlDbType.Int)
    .Build();

var result = await database.ExecuteWithOutputsAsync(command, ct);
var orderId = result.Output.Get<int>("OrderId");
```

Use the other TVP overload for a truly streaming producer:

```csharp
.AddTvpParameter("Lines", static cancellationToken => ReadLines(cancellationToken))
```

The factory is never materialized by CaeriusNet and a command containing it cannot use a read-through cache. Manual TVP contracts must place `SqlServerTvpColumnAttribute` on every positional parameter so its SQL name, ordinal, type, nullability and required facets remain explicit.

Multi-result calls are generated for arities 2 through 10 and for `IEnumerable<T>`, `ReadOnlyCollection<T>` and `ImmutableArray<T>`. They require the announced result-set count by default; opt into `CaeriusResultSetCountPolicy.AllowMissing` or `AllowAdditional` only for deliberately variable procedures.

## Caching and transactions

Local memory and Frozen snapshot caches are part of `CaeriusNet`; Redis requires the separate `CaeriusNet.Redis` package and an `ICaeriusCacheCodec<T>`. Cache keys are centrally derived from the application namespace, procedure contract, result shape and canonical parameters—do not construct keys per procedure. The external key is a length-prefixed binary identity hashed as `caerius:v2:<sha256>`.

```csharp
var cached = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .DependsOn("product", "catalog")
    .UseInMemoryCache(TimeSpan.FromMinutes(2), slidingExpiration: true)
    .Build();

var write = new StoredProcedureCommandBuilder("dbo", "usp_Product_Rename")
    .AddParameter("Id", productId, SqlDbType.Int)
    .AddParameter("Name", name, SqlDbType.NVarChar, size: 200)
    .Invalidates("product", "catalog")
    .Build();
```

Transactions hold one connection and one SQL transaction, forbid concurrent commands, bypass read-through cache, and defer declared invalidations until commit:

```csharp
await using var transaction = await database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
await transaction.ExecuteNonQueryAsync(command, ct);
await transaction.CommitAsync(ct);
```

The same materialized single- and multi-result APIs (all arities 2–10 and collection families) are available on a transaction, including `ExecuteWithOutputsAsync`, `ExecuteNonQueryWithOutputsAsync`, and `Query…WithOutputsAsync`. Outputs are read only after every result set has been consumed and its reader disposed; invalidations run only after `CommitAsync` succeeds. `transaction.StreamAsync<T>` retains the command slot for the lifetime of its enumeration—consume it fully before committing, or roll the transaction back. `CommitAsync` and `RollbackAsync` observe cancellation before their terminal SQL operation begins; after that cutoff, they finish without the caller token so a cancelled request cannot turn an already-sent commit into an ambiguous cache state. If the durable SQL write succeeds but an invalidation fails, `CaeriusCacheInvalidationException` has `SqlWasCommitted == true`: investigate/reconcile the cache; do not blindly retry a non-idempotent procedure.

## Documentation and support

- [Documentation site](https://caerius.net)
- [Getting started](https://caerius.net/quickstart/getting-started)
- [Migration from v11](docs/MIGRATION-v11-v12.md)
- [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
- [Contributing](CONTRIBUTING.md)

CaeriusNet is released under the [MIT License](LICENSE).
