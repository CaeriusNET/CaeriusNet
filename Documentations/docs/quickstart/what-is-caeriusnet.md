# What is Caerius.NET?

Caerius.NET is a focused, high‑performance Micro‑ORM for C# 13 / .NET 10 that turns SQL Server Stored Procedure results into strongly typed C# DTOs — in microseconds. It is built for teams who favor SQL Server and Stored Procedures for data access and want compile‑time safety, predictable performance, and a minimal API surface.

We are unapologetically specialized: Caerius.NET targets C# and Microsoft SQL Server. This clarity lets us optimize deeply for T/SQL, `SqlDataReader`, and SQL Server features such as Table‑Valued Parameters (TVP) and multi‑result sets.

## Why Caerius.NET?

### 1. Performance you can feel
Caerius.NET is engineered to keep the hot path hot:
- Ordinal, allocation‑aware mapping: DTOs implement `ISpMapper<T>` (or are source‑generated), reading columns by index for zero name lookups.
- Compiler‑guided optimizations: extensive use of `MethodImplOptions.AggressiveOptimization`/`AggressiveInlining` on hot methods.
- Low‑level buffers and spans:
  - `CollectionsMarshal.SetCount` + `CollectionsMarshal.AsSpan(list)` to add items with minimal overhead.
  - `ArrayPool<T>.Shared` to rent/return buffers and build `ImmutableArray<T>` without extra copies.
  - `CommandBehavior.SequentialAccess` on readers to stream rows efficiently.
- Right‑sized collections: you declare an expected capacity per call; Caerius.NET pre‑allocates to avoid list resizing.
- Caching tiers built‑in: Frozen (immutable in‑process), In‑Memory (expirable), and Redis (distributed) — enabled per call via the builder.

In short: no runtime reflection for mapping, no dynamic expression compilation on the hot path, and minimal allocations.

### 2. Source‑generated DTO and TVP mappers
- `[GenerateDto]` emits a compile‑time `ISpMapper<T>` for your sealed partial records/classes. You get static, ordinal mapping with correct nullability and types.
- `[GenerateTvp]` emits an `ITvpMapper<T>` so you can pass large sets as TVPs without writing boilerplate `DataTable` code.

Prefer manual control? You can still implement `ISpMapper<T>` / `ITvpMapper<T>` yourself.

### 3. SQL Server expertise, not a kitchen sink
- Stored Procedures first. TVP, multi‑result sets, scalar reads, and write commands (`ExecuteNonQueryAsync`, `ExecuteScalarAsync`, fire‑and‑forget `ExecuteAsync`).
- Fluent parameter builder with TVP support: `StoredProcedureParametersBuilder` composes parameters and caching for each call.
- Works great in existing databases that already standardize on Stored Procedures.

### 4. Simplicity and developer experience
- Small API surface: one builder, one `DbContext` abstraction, and a handful of query/command methods.
- Async‑only I/O by design to avoid accidental thread pool starvation and to play well with any SP duration.
- First‑class DI and Aspire integration: configure SQL Server and Redis via `CaeriusNetBuilder` (manual or Aspire‑style).

### 5. Reliability and observability
- Clear exception boundaries: SQL errors are wrapped in `CaeriusNetSqlException` with the original `SqlException` preserved.
- Structured logging hooks: plug your logger (e.g., `ILogger`) to trace connections, caching, and Redis activity.
- Unit‑tested core and deterministic mapping semantics.

## Core capabilities at a glance
- Stored Procedure to DTO mapping with compile‑time safety (`ISpMapper<T>` / `[GenerateDto]`).
- High‑throughput reads into `IEnumerable<T>`, `ReadOnlyCollection<T>`, or `ImmutableArray<T>` — pick what fits your scenario.
- Multiple result sets in a single round‑trip (2 to 5 result sets helpers).
- TVP (Table‑Valued Parameters) support with `[GenerateTvp]` for bulk‑style inputs.
- Per‑call caching: Frozen, In‑Memory (TTL), or Redis distributed.
- Write commands: `ExecuteNonQueryAsync`, `ExecuteScalarAsync<T>`, and `ExecuteAsync`.
- DI/Aspire friendly setup via `CaeriusNetBuilder`.

## When should you use Caerius.NET?
Use Caerius.NET when:
- Your team embraces SQL Server Stored Procedures for reads and writes.
- Latency and allocation budget matter — you want predictable, fast mapping and minimal runtime overhead.
- You need to push large parameter sets (IDs, GUIDs, composite keys) via TVPs.
- You operate in services where per‑call caching (including Redis) can offload the database.
- You prefer versioned SQL and stable DTOs over runtime query generation.

It may not be the best fit when you require ORM features such as change tracking, LINQ query translation, or multi‑database portability.

## How does it compare?
- EF Core: feature‑rich ORM with change tracking and LINQ translation. Caerius.NET is leaner, SP‑centric, and typically faster on read paths due to ordinal mapping and fewer allocations.
- Dapper: excellent general‑purpose micro‑ORM. Caerius.NET optimizes specifically for SQL Server Stored Procedures, adds compile‑time DTO/TVP generators, multi‑result helpers, and per‑call caching primitives.

Choose the tool that matches your architecture — Caerius.NET excels when SPs, TVPs, and throughput are central.

## Architecture and hot path (simplified)
1. You build call settings with `StoredProcedureParametersBuilder` (schema, SP name, capacity, parameters, optional cache).
2. Read commands open a connection via `ICaeriusNetDbContext.DbConnection()` and execute with `SqlCommand` using `SequentialAccess`.
3. Rows are mapped by `ISpMapper<T>` (manual or generated) with ordinal reads (e.g., `GetInt32(0)`).
4. Results are materialized using pre‑sized collections and pooling helpers; optional cache is read/written via chosen backend.

## Example: one SP, one cache, zero fuss
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("users:all:frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```

With TVP and Redis:
```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 1024)
    .AddTvpParameter("Ids", tvpItems)
    .AddParameter("Age", age, SqlDbType.Int)
    .AddRedisCache($"users:age:{age}", TimeSpan.FromMinutes(2))
    .Build();

var (adults, seniors) = await dbContext
    .QueryMultipleIEnumerableAsync<UserDto, UserDto>(sp, cancellationToken);
```

## Benchmarks and realism
We publish micro‑benchmarks that focus on the mapping and materialization path. As always, measure in your environment — network, SQL plans, and payload size dominate end‑to‑end latency. Caerius.NET minimizes client‑side costs so your time is spent where it matters: on the database.

## Learn more
- Quickstart: [Getting Started](/quickstart/getting-started)
- Deep dive: [Advanced Usage](/documentation/advanced-usage)
- Caching strategies: [Caching](/documentation/cache)
- Practices that scale: [Best Practices](/documentation/best-practices)
- Full surface: [API Reference](/documentation/api)