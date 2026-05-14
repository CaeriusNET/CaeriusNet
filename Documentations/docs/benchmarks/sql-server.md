---
title: SQL Server benchmarks
description: CaeriusNet SQL Server end-to-end benchmarks for stored procedure execution, batched inserts, TVPs, OUTPUT parameters, and connection pool reuse.
---

# SQL Server benchmarks

These benchmarks measure **real end-to-end latency** of CaeriusNet operations against a live
SQL Server 2022 instance running inside a Docker service container on Ubuntu.
Metrics include: TCP connection setup (pooled), TDS framing, SQL Server execution plan evaluation,
data serialization over the wire, and `SqlDataReader` deserialization on the .NET side.

> ⚠️ **These benchmarks require SQL Server.**
> They are skipped automatically when `BENCHMARK_SQL_CONNECTION` is not set (the guard `if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return;` executes at the top of every benchmark method).
>
> The seed table `BenchmarkItems` is pre-populated with **100 000 rows** during `[GlobalSetup]` using a
> `SELECT TOP 100000 ... FROM sys.all_objects a CROSS JOIN sys.all_objects b` cross-join seed, ensuring
> a realistic cardinality for all read benchmarks.

> This page explains what the SQL Server benchmark suite measures and how to interpret generated tables.
> If no result table is shown, run the benchmark workflow or the local commands in the overview to produce fresh BenchmarkDotNet artifacts.

::: warning Environment-specific results
SQL Server benchmark results vary with server edition, CPU, memory, storage, indexes, query plans, network latency, container settings, and connection-pool state. Use these pages to understand methodology and trends, then measure your deployment scenario.
:::

---

## Stored procedure execution round trip

**Benchmark class: `SpExecutionBench`**

### What is measured

Full stored procedure round trip: `SqlConnection.Open()` from the pool, `SqlCommand.ExecuteReaderAsync()`,
`while (reader.ReadAsync())`, and the final row count.

`RowCount` controls the `TOP N` in the stored procedure query. The benchmark measures how round trip latency
scales from 0 rows (no data, pure call overhead) to 50 000 rows (large result set streaming).

`[Params(0, 10, 100, 1_000, 5_000, 10_000, 50_000)]`

### Key insights

- **At RowCount = 0**, the measurement isolates pure call overhead: TDS command framing + SQL Server parse/compile +
  empty result set return. This is the irreducible floor for any stored procedure call.
- **At small row counts (10–100)**, connection establishment and command preparation dominate over data transfer.
  Connection pooling amortizes the TCP handshake across calls. The warm-pool cost is lower
  than a cold-start `SqlConnection`.
- **At large row counts (5 000–50 000)**, streaming throughput (rows per microsecond) becomes the binding factor.
  CaeriusNet's `SqlDataReader` iteration cost grows linearly with rows.
- The **Ratio** column between `RowCount = 0` and larger row counts helps estimate how measured time shifts from call overhead to data transfer and deserialization.

---

## Batched inserts vs single inserts

**Benchmark class: `BatchedVsSingleBench`**

### What is measured

This benchmark compares per-row stored procedure calls with a single TVP-based stored procedure call.

Two insertion strategies are compared at `[Params(10, 100, 500, 1_000, 5_000)]` items:

| Method | Strategy | Round trips |
|---|---|---|
| `Insert_SingleCalls` *(Baseline)* | One `INSERT INTO ... VALUES (...)` stored procedure call per item | N round trips |
| `Insert_BatchedTvp` | One `INSERT INTO ... SELECT * FROM @tvp` stored procedure call with a TVP | 1 round trip |

### Key insights

- **Single-call strategy costs O(N) round trips.** Each round trip includes:
  TCP segment send + SQL Server parse + plan lookup + lock acquisition + row write + result return.
  At N = 1 000, this is 1 000 independent TCP exchanges.

- **TVP batch strategy costs O(1) round trips.** The entire dataset is serialized into the TVP stream,
  transmitted in a single TDS message batch, and processed in one server-side INSERT.

- The performance gap between the two strategies widens with item count:
  - At N = 10: the difference exists but is modest (TVP has a fixed setup overhead).
- At N = 1 000: TVP avoids per-row round trip cost; confirm the measured ratio from your benchmark table.
- At N = 5 000: per-row round trip cost often dominates; confirm suitability from your benchmark table and latency budget.

- In production environments, network latency increases the cost of every round trip.
  Local Docker numbers should not be treated as remote database latency estimates.

---

## Multiple result sets vs separate calls

**Benchmark class: `MultiResultSetBench`**

### What is measured

CaeriusNet supports typed tuple multi-result-set calls: a single stored procedure call that returns
multiple independent result sets, materialized via successive `reader.NextResultAsync()` calls.

This benchmark compares:
- **Single stored procedure, multiple result sets**: one round trip, one connection checkout from the pool, and N result set reads.
- **N separate stored procedure calls**: N independent round trips, N connection checkouts, each with parse/compile overhead.

### Key insights

- Every SQL Server round trip adds a fixed overhead for command framing, plan cache lookup, and lock manager interaction.
  Combining multiple result sets into one stored procedure call eliminates all per-call fixed overhead after the first.
- The savings are most pronounced when each individual result set is small (< 100 rows) — in that regime,
  per-call overhead dominates over data transfer.
- When individual result sets are large (> 10 000 rows), the gains from combining are proportionally smaller
  since data transfer time dominates over fixed overhead.
- Multi-result-set stored procedures also reduce connection pool contention under concurrent load by shortening total
  connection hold time.

---

## TVP full round trip

**Benchmark class: `TvpFullRoundtripBench`**

### What is measured

The complete CaeriusNet TVP pipeline, end-to-end:

1. `Randomizer.Seed = new Random(42)` → generate `RowCount` items via Bogus
2. `StoredProcedureParametersBuilder.AddTvpParameter<T>(items)` → serialize to `SqlDataRecord` stream
3. `.Build()` → produce `SqlParameter[]`
4. `SqlCommand.ExecuteReaderAsync()` to execute the stored procedure with `OUTPUT INSERTED.*`
5. `while (reader.ReadAsync())` to stream back the inserted rows

Compared against a **manual ADO.NET TVP setup** without the builder: raw `SqlParameter(Structured)` assembly.

`[Params(10, 100, 1_000, 5_000, 10_000)]`

### Key insights

- Compare the CaeriusNet builder against raw ADO.NET assembly by using the **Ratio**, **Error**, and **StdDev** columns together. Small differences may be measurement noise.
- The dominant cost at any row count greater than 100 is **network I/O + SQL Server execution**, not .NET-side work.
- At RowCount = 10 000 the TVP pipeline throughput demonstrates that memory allocation stays constant
  (O(1) `SqlDataRecord` streaming) even as the SQL-side work grows linearly.
- The `OUTPUT INSERTED.*` pattern (streaming back inserted rows) proves that CaeriusNet's round trip
  cost is dominated by the server-side work, not the client-side builder or serializer.

---

## OUTPUT parameter vs SCOPE_IDENTITY()

**Benchmark class: `SpOutputParameterBench`**

### What is measured

Two common patterns for retrieving a newly-inserted identity value:

| Method | SQL strategy | Round trips | .NET cost |
|---|---|---|---|
| `Insert_OutputParameter` *(Baseline)* | `@NewId INT OUTPUT`, populated server-side inside the stored procedure | 1 | One `SqlParameter` direction change |
| `Insert_ScopeIdentity` | `INSERT ...; SELECT SCOPE_IDENTITY()`, second result set after insert | 1 (same round trip, extra result) | `reader.NextResult()` + `reader.Read()` |
| `Insert_SeparateSelect` | `INSERT ...; SELECT MAX(Id) FROM ...`, separate query after the stored procedure call | 2 | Full second round trip |

### Key insights

- `@NewId INT OUTPUT` is the most efficient pattern: the identity value is written to the output parameter
  slot by SQL Server during stored procedure execution, retrieved by `SqlClient` as part of the response packet.
  No second result set, no second round trip.
- `SELECT SCOPE_IDENTITY()` requires `reader.NextResultAsync()` to advance past the INSERT's empty result
  set. This creates a small but measurable overhead compared with a pure OUTPUT parameter.
- The two-round-trip `SELECT MAX(Id)` anti-pattern is significantly slower because it pays the full
  per-round-trip fixed overhead twice, plus it is unsafe under concurrent inserts.
- **CaeriusNet best practice:** use `OUTPUT INSERTED.Id` (for multi-row TVP inserts) or `@NewId INT OUTPUT`
  (for single-row inserts). Never use a separate `SCOPE_IDENTITY()` query or `SELECT MAX(Id)`.

---

## Connection pool reuse vs cold start

**Benchmark class: `ConnectionPoolBench`**

### What is measured

ADO.NET maintains a `SqlConnection` pool per connection string, reusing physical TCP connections across
logical `Open/Close` calls. This benchmark quantifies the performance difference between:

| Method | Strategy |
|---|---|
| `Connect_WarmPool` *(Baseline)* | `Open()` returns a pooled physical connection; no TCP handshake |
| `Connect_ColdStart` | `ClearPool(connection)` then `Open()`; forces a new TCP handshake and login |
| `Connect_Persistent` | Hold one physical connection open for the entire benchmark iteration; bypasses pool checkout overhead |

### Key insights

- **Warm pool** is the normal production scenario. `SqlConnection.Open()` dequeues an idle physical
  connection and resets its state (`sp_reset_connection`). Measure the expected cost in your environment.
- **Cold start** forces a full TCP three-way handshake + TDS pre-login + TDS login sequence.
  This is typically much more expensive than a warm pool checkout; use measured results for your environment.
  `ClearPool()` should **never be called in production** unless connection credentials change.
- **Persistent connection** bypasses checkout overhead. This is useful for understanding the irreducible
  command-execution cost (no pool interaction at all), but not suitable for concurrent workloads.
- The Ratio between Warm Pool and Cold Start quantifies the value of the connection pool.
  In a local Docker environment, the cold-start cost is often dominated by the TDS handshake.
  Remote database latency and authentication configuration can change the result substantially.
