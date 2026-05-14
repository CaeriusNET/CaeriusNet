# What is CaeriusNet?

CaeriusNet is a focused data-access package for **C# 14**, **.NET 10**, and **SQL Server stored procedures**. It helps you call stored procedures, map result sets to strongly typed DTOs, pass table-valued parameters, cache read results, and observe database calls with tracing and metrics.

CaeriusNet is intentionally narrow. It does not translate LINQ, track entities, create migrations, or generate SQL text. You keep SQL Server and stored procedures as the contract. CaeriusNet provides the .NET API around that contract.

::: info Package scope
Use CaeriusNet when your application owns or consumes SQL Server stored procedures. Use a full ORM when you need change tracking, LINQ query translation, migrations, or database-provider portability.
:::

## Why use CaeriusNet?

CaeriusNet is designed for teams that want stable SQL contracts and low-overhead .NET call sites.

| Capability | Benefit |
|---|---|
| Stored procedure calls | Keeps SQL logic in SQL Server and makes the .NET call site explicit. |
| DTO mapping | Converts each row into a typed C# DTO with ordinal column reads. |
| Source generators | Removes repetitive DTO and TVP mapper code while preserving compile-time checks. |
| Table-valued parameters | Sends large sets of IDs, GUIDs, or composite values without building `DataTable` objects. |
| Multiple result sets | Reads related result sets from a single database round trip. |
| Per-call caching | Applies Frozen, in-memory, or Redis caching only where the call is safe to cache. |
| Transactions | Provides explicit async transaction scopes with commit, rollback, and automatic rollback on dispose. |
| Observability | Emits tracing, metrics, and structured logs for stored procedure execution. |

## What CaeriusNet does

Use CaeriusNet to:

- Execute stored procedures through `ICaeriusNetDbContext`.
- Build stored procedure inputs with `StoredProcedureParametersBuilder`.
- Read one row with `FirstQueryAsync<T>()`.
- Materialize result sets as `IEnumerable<T>`, `ReadOnlyCollection<T>`, or `ImmutableArray<T>`.
- Execute writes with `ExecuteNonQueryAsync`, `ExecuteScalarAsync<T>`, or `ExecuteAsync`.
- Pass TVPs through `AddTvpParameter`.
- Read two to five result sets with `QueryMultiple*Async`.
- Configure caching on individual read calls.
- Run multiple commands in an `ICaeriusNetTransaction`.

## What CaeriusNet does not do

CaeriusNet does not:

- Translate LINQ expressions to SQL.
- Track entities or detect changes.
- Generate database migrations.
- Build ad hoc SQL strings.
- Target multiple database providers.
- Replace SQL Server schema design, indexing, or query tuning.

This boundary is deliberate. It keeps the package small and predictable for applications that already use stored procedures as their data-access boundary.

## How a read call works

A typical read has four parts:

1. Create a DTO that implements `ISpMapper<T>` or use `[GenerateDto]`.
2. Build the stored procedure call with `StoredProcedureParametersBuilder`.
3. Call a read method such as `QueryAsReadOnlyCollectionAsync<T>()`.
4. Receive a typed result collection.

```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);

var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_By_Age", 128)
    .AddParameter("Age", 18, SqlDbType.Int)
    .Build();

ReadOnlyCollection<UserDto> users =
    await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

## When to use each result shape

| Method | Use when |
|---|---|
| `FirstQueryAsync<T>` | The procedure returns zero or one row. |
| `QueryAsIEnumerableAsync<T>` | You want a simple materialized sequence for LINQ operations. |
| `QueryAsReadOnlyCollectionAsync<T>` | You expose results through public APIs that should not be mutated. |
| `QueryAsImmutableArrayAsync<T>` | You want a compact immutable value for hot paths or cached data. |
| `QueryMultiple*Async` | The procedure returns related result sets that should be loaded together. |

## How it compares

| Feature | CaeriusNet | EF Core | Dapper |
|---|---|---|---|
| Primary model | SQL Server stored procedures | LINQ and entity model | SQL text and ADO.NET commands |
| Change tracking | No | Yes | No |
| Database providers | SQL Server | Multiple providers | Multiple ADO.NET providers |
| DTO mapping | Static mapper contract or source generation | Entity materialization | Runtime mapping or manual mapping |
| TVP support | Built in | Manual setup | Manual setup |
| Multiple result sets | Typed tuple APIs | Manual reader handling | `QueryMultiple` |
| Built-in caching | Frozen, in-memory, Redis | Extension-based | No |
| Tracing and metrics | Built in | External instrumentation | External instrumentation |

## Recommended next steps

- [Install and configure](/quickstart/getting-started)
- [Usage overview](/documentation/usage)
- [Reading data](/documentation/reading-data)
- [Table-valued parameters](/documentation/tvp)
- [Multiple result sets](/documentation/multi-results)
- [API reference](/documentation/api)
