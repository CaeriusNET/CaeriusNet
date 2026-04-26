# What is CaeriusNet?

CaeriusNet is a focused, high-performance **micro-ORM** for **C# 14 / .NET 10** that turns SQL Server Stored Procedure result sets into strongly typed C# DTOs — in microseconds, with no reflection.

It is unapologetically specialized: CaeriusNet targets **Microsoft SQL Server** and **Stored Procedures**. That clarity lets it optimize deeply for T-SQL, `SqlDataReader`, and SQL Server features such as Table-Valued Parameters and multi-result-sets.

## Why CaeriusNet?

### 1. Performance you can feel

CaeriusNet is engineered to keep the hot path hot:

- **Ordinal, allocation-aware mapping.** DTOs implement `ISpMapper<T>` (manual or source-generated) and read columns by index — zero name lookups, fewer string allocations.
- **Compiler-guided optimizations.** Hot mappers carry `[MethodImpl(AggressiveInlining)]`; TVP iterators carry `[MethodImpl(AggressiveOptimization)]`.
- **Low-level buffers and spans.** `CollectionsMarshal.SetCount` + `AsSpan(list)` populate result lists in place; `ArrayPool<T>.Shared` rents/returns buffers for `ImmutableArray<T>` without extra copies.
- **Streaming reads.** Every `SqlCommand` runs with `CommandBehavior.SequentialAccess` to consume rows directly off the TDS stream.
- **Right-sized collections.** You declare an expected capacity per call; CaeriusNet pre-allocates so `List<T>` never resizes mid-fill.
- **Caching tiers built in.** Frozen (immutable in-process), InMemory (TTL), and Redis (distributed) — opt in per call via the builder.

In short: no runtime reflection, no expression-tree compilation on the hot path, and minimal allocations.

### 2. Source-generated mappers

- **`[GenerateDto]`** emits a compile-time `ISpMapper<T>` for sealed partial records or classes. You get static, ordinal mapping with correct nullability and special-type conversions.
- **`[GenerateTvp]`** emits an `ITvpMapper<T>` so you can pass large sets as Table-Valued Parameters without writing boilerplate.
- A dedicated **Roslyn analyzer** enforces the contract (`sealed partial`, primary constructor) — drift surfaces in your IDE, not at runtime.

Prefer manual control? You can still implement `ISpMapper<T>` / `ITvpMapper<T>` by hand.

### 3. SQL Server expertise, not a kitchen sink

- **Stored Procedures first.** TVP, multi-result-sets, scalar reads, and write commands (`ExecuteNonQueryAsync`, `ExecuteScalarAsync<T>`, fire-and-forget `ExecuteAsync`).
- **Fluent builder.** `StoredProcedureParametersBuilder` composes parameters, TVPs, and caching for each call.
- **Atomic transactions.** `BeginTransactionAsync` provides a thread-safe scope with a strict state machine, automatic rollback on dispose, and a parent `TX` activity for cohesive tracing.
- **Works with the schema you already have.** No migrations, no shadow tables, no surprise columns.

### 4. Developer experience

- Small API surface: one builder, one `ICaeriusNetDbContext` abstraction, a handful of query and command methods.
- **Async-only I/O** by design — every call is `async` and `CancellationToken`-aware.
- **DI-first.** `CaeriusNetBuilder` integrates cleanly with `IServiceCollection` and `IHostApplicationBuilder`.
- **Aspire-native.** `WithAspireSqlServer` / `WithAspireRedis` resolve connection strings from the AppHost in two lines.

### 5. Reliability and observability

- **Clear exception boundaries.** SQL errors are wrapped in `CaeriusNetSqlException`; the original `SqlException` stays available via `InnerException`.
- **Built-in OpenTelemetry.** An `ActivitySource` and a `Meter` emit OTel-compliant spans (`db.system`, `db.operation`, `db.statement`, plus rich `caerius.*` tags) and metrics (duration histogram, executions / errors / cache-lookup counters).
- **Structured logging** via source-generated `[LoggerMessage]` — zero allocations on disabled levels, named placeholders for queryable backends.
- **AOT- and trim-compatible** (`IsAotCompatible=true`, `IsTrimmable=true`) — the library is ready for AOT-published deployments.

## Core capabilities at a glance

- Stored Procedure → DTO mapping with compile-time safety (`ISpMapper<T>` / `[GenerateDto]`).
- High-throughput reads into `IEnumerable<T>`, `ReadOnlyCollection<T>`, or `ImmutableArray<T>` — pick what fits.
- Up to **five result sets** in a single round-trip.
- **TVPs** with `[GenerateTvp]` for bulk-style inputs.
- Per-call caching: **Frozen**, **InMemory** (TTL), **Redis** distributed.
- Write commands: `ExecuteNonQueryAsync`, `ExecuteScalarAsync<T>`, `ExecuteAsync`.
- Atomic transactions with a parent `TX` span and a strict state machine.
- DI / Aspire-friendly setup via `CaeriusNetBuilder`.

## When should you use CaeriusNet?

Choose CaeriusNet when:

- Your team owns the SQL schema and embraces Stored Procedures for reads and writes.
- Latency and allocation budget matter — you want predictable, fast mapping with minimal runtime overhead.
- You need to push large parameter sets (IDs, GUIDs, composite keys) via TVPs.
- You operate in services where per-call caching can offload the database.
- You prefer versioned SQL and stable DTOs over runtime query generation.

It may not be the best fit when you require ORM features such as change tracking, LINQ-to-SQL translation, or multi-database portability.

## How does it compare?

| | CaeriusNet | EF Core | Dapper |
|---|---|---|---|
| Target database | SQL Server only | SQL Server, PostgreSQL, MySQL, … | Any ADO.NET provider |
| Query model | Stored Procedures | LINQ → SQL translation | Raw SQL strings |
| Mapping | Compile-time, ordinal, no reflection | Runtime, expression-tree compilation | Runtime reflection or hand-rolled |
| Change tracking | None | Full | None |
| TVP support | Source-generated, zero-copy | Manual `DataTable` | Manual `DataTable` |
| Multi-result-set | Up to 5, typed tuple | Manual `ExecuteReader` + `NextResult` | `QueryMultiple` (typed by call) |
| Built-in caching | Frozen / InMemory / Redis | Second-level cache via extensions | None |
| OpenTelemetry | Built-in source + meter | Via instrumentation package | None |

Pick the tool that matches your architecture — CaeriusNet excels when SPs, TVPs, and throughput are central.

## Architecture and hot path (simplified)

1. You build call settings with `StoredProcedureParametersBuilder` (schema, SP name, capacity, parameters, optional cache).
2. Read commands open a connection via `ICaeriusNetDbContext` and execute with `SqlCommand` using `SequentialAccess`.
3. Rows are mapped by `ISpMapper<T>` (manual or generated) with ordinal reads (e.g., `GetInt32(0)`).
4. Results are materialized using pre-sized collections and pooling helpers; the optional cache is read or written via the chosen tier.
5. Throughout, an `Activity` records the SP execution and the `Meter` records duration, executions, errors, and cache lookups.

## A taste — one SP, one cache, zero ceremony

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("users:all:frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

Same call, with a TVP filter and Redis caching:

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 1024)
    .AddTvpParameter("Ids", tvpItems)
    .AddParameter("Age", age, SqlDbType.Int)
    .AddRedisCache($"users:age:{age}", TimeSpan.FromMinutes(2))
    .Build();

var (adults, seniors) = await dbContext
    .QueryMultipleIEnumerableAsync<UserDto, UserDto>(sp, ct);
```

## Benchmarks and realism

CaeriusNet publishes BenchmarkDotNet suites covering the mapping path, collection construction, TVP serialization, cache layers, and full SQL Server round-trips — all reproducible with a fixed seed (`Random(42)`) and run on every release. As always, **measure in your own environment**: network, SQL plans, and payload size dominate end-to-end latency. CaeriusNet minimizes client-side cost so your time is spent where it matters — on the database.

## Next steps

- [Installation & Setup](/quickstart/getting-started) — drop the package in, register DI, run your first query.
- [Source Generators](/documentation/source-generators) — let the compiler write your mappers.
- [Examples](/examples/) — end-to-end walkthroughs for SPs, TVPs, multi-result-sets, and transactions.
- [Aspire Integration](/documentation/aspire) — wire CaeriusNet into the Aspire dashboard.
- [Best Practices](/documentation/best-practices) — recommendations for production-ready usage.
- [API Reference](/documentation/api) — full public surface.
