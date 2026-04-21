---
title: SQL Server Benchmarks
description: CaeriusNet SQL Server end-to-end benchmarks — stored procedure execution, batched vs single inserts, TVP full roundtrip, OUTPUT parameters, and connection pool reuse.
---

# SQL Server Benchmarks

These benchmarks measure **real end-to-end latency** of CaeriusNet operations against a live
SQL Server 2022 instance running inside a Docker service container on Ubuntu.
All metrics include: TCP connection setup (pooled), TDS framing, SQL Server execution plan evaluation,
data serialization over the wire, and `SqlDataReader` deserialization on the .NET side.

> ⚠️ **These benchmarks require SQL Server.**
> They are skipped automatically when `BENCHMARK_SQL_CONNECTION` is not set (the guard `if (!SqlBenchmarkGlobalSetup.IsSqlAvailable) return;` executes at the top of every benchmark method).
>
> The seed table `BenchmarkItems` is pre-populated with **100 000 rows** during `[GlobalSetup]` using a
> `SELECT TOP 100000 ... FROM sys.all_objects a CROSS JOIN sys.all_objects b` cross-join seed, ensuring
> a realistic cardinality for all read benchmarks.

> All benchmark results displayed on this page are **real measured values** produced by the CI benchmark workflow.
> Tables are populated automatically after each [GitHub Release](https://github.com/CaeriusNET/CaeriusNet/releases).

---

## Stored Procedure Execution Roundtrip

**Benchmark class: `SpExecutionBench`**

### What is measured

Full stored procedure roundtrip: `SqlConnection.Open()` (from pool) → `SqlCommand.ExecuteReaderAsync()`
→ `while (reader.ReadAsync())` → return row count.

`RowCount` controls the `TOP N` in the SP query — the benchmark measures how roundtrip latency
scales from 0 rows (no data, pure call overhead) to 50 000 rows (large result set streaming).

`[Params(0, 10, 100, 1_000, 5_000, 10_000, 50_000)]`

### Key insights

- **At RowCount = 0**, the measurement isolates pure call overhead: TDS command framing + SQL Server parse/compile +
  empty result set return. This is the irreducible floor for any SP call.
- **At small row counts (10–100)**, connection establishment and command preparation dominate over data transfer.
  Connection pooling amortises the TCP handshake across calls — the warm-pool cost is dramatically lower
  than a cold-start `SqlConnection`.
- **At large row counts (5 000–50 000)**, streaming throughput (rows per microsecond) becomes the binding factor.
  CaeriusNet's `SqlDataReader` iteration cost grows linearly with rows.
- The **Ratio** column between `RowCount = 0` and large row counts quantifies how much of the measured time
  is pure SQL Server work vs .NET deserialization overhead.

---

## Batched vs Single Inserts — The TVP Advantage

**Benchmark class: `BatchedVsSingleBench`**

### What is measured

This benchmark is the **core value proposition** of CaeriusNet's TVP support.

Two insertion strategies are compared at `[Params(10, 100, 500, 1_000, 5_000)]` items:

| Method | Strategy | Roundtrips |
|---|---|---|
| `Insert_SingleCalls` *(Baseline)* | One `INSERT INTO ... VALUES (...)` SP call per item | N roundtrips |
| `Insert_BatchedTvp` | One `INSERT INTO ... SELECT * FROM @tvp` SP call with a TVP | 1 roundtrip |

### Key insights

- **Single-call strategy costs O(N) roundtrips.** Each roundtrip includes:
  TCP segment send + SQL Server parse + plan lookup + lock acquisition + row write + result return.
  At N = 1 000, this is 1 000 independent TCP exchanges.

- **TVP batch strategy costs O(1) roundtrips.** The entire dataset is serialized into the TVP stream,
  transmitted in a single TDS message batch, and processed in one server-side INSERT.

- The performance gap between the two strategies widens dramatically with item count:
  - At N = 10: the difference exists but is modest (TVP has a fixed setup overhead).
  - At N = 1 000: TVP is multiple orders of magnitude faster.
  - At N = 5 000: TVP's advantage is so large that single-call strategy is effectively unusable.

- In **production environments with network latency > 1 ms**, the per-roundtrip cost is even higher —
  the numbers on this page represent a local Docker loop with sub-millisecond latency.
  Real-world latency multiplies every row's overhead for the single-call strategy.

---

## Multi-Result Set vs Separate Calls

**Benchmark class: `MultiResultSetBench`**

### What is measured

CaeriusNet supports `IAsyncEnumerable<(T1, T2, ...)>` multi-result-set SPs — a single SP call that returns
multiple independent result sets, read via successive `reader.NextResultAsync()` calls.

This benchmark compares:
- **Single SP, multiple result sets**: one TDS roundtrip, one connection checkout from pool, N result set reads.
- **N separate SP calls**: N independent roundtrips, N connection checkouts, each with parse/compile overhead.

### Key insights

- Every SQL Server roundtrip adds a fixed overhead for TDS framing, plan cache lookup, and lock manager interaction.
  Combining multiple result sets into one SP call eliminates all per-call fixed overhead after the first.
- The savings are most pronounced when each individual result set is small (< 100 rows) — in that regime,
  per-call overhead dominates over data transfer.
- When individual result sets are large (> 10 000 rows), the gains from combining are proportionally smaller
  since data transfer time dominates over fixed overhead.
- Multi-result-set SPs also reduce connection pool contention under concurrent load by shortening total
  connection hold time.

---

## TVP Full Roundtrip

**Benchmark class: `TvpFullRoundtripBench`**

### What is measured

The complete CaeriusNet TVP pipeline, end-to-end:

1. `Randomizer.Seed = new Random(42)` → generate `RowCount` items via Bogus
2. `StoredProcedureParametersBuilder.AddTvpParameter<T>(items)` → serialize to `SqlDataRecord` stream
3. `.Build()` → produce `SqlParameter[]`
4. `SqlCommand.ExecuteReaderAsync()` → execute SP with `OUTPUT INSERTED.*`
5. `while (reader.ReadAsync())` → stream back the inserted rows

Compared against a **manual ADO.NET TVP setup** without the builder — raw `SqlParameter(Structured)` assembly.

`[Params(10, 100, 1_000, 5_000, 10_000)]`

### Key insights

- The CaeriusNet builder adds **negligible overhead** vs raw ADO.NET assembly — the Ratio should be
  within noise margin (< 1 % difference in mean) because the builder is a thin wrapper around
  `List<SqlParameter>.Add()` + a type-check for TVP items.
- The dominant cost at any row count > 100 is **network I/O + SQL Server execution** — not .NET-side work.
- At RowCount = 10 000 the TVP pipeline throughput demonstrates that memory allocation stays constant
  (O(1) `SqlDataRecord` streaming) even as the SQL-side work grows linearly.
- The `OUTPUT INSERTED.*` pattern (streaming back inserted rows) proves that CaeriusNet's round-trip
  cost is dominated by the server-side work, not the client-side builder or serializer.

---

## OUTPUT Parameter vs SCOPE_IDENTITY()

**Benchmark class: `SpOutputParameterBench`**

### What is measured

Two common patterns for retrieving a newly-inserted identity value:

| Method | SQL strategy | Roundtrips | .NET cost |
|---|---|---|---|
| `Insert_OutputParameter` *(Baseline)* | `@NewId INT OUTPUT` — value populated server-side inside the SP | 1 | One `SqlParameter` direction change |
| `Insert_ScopeIdentity` | `INSERT ...; SELECT SCOPE_IDENTITY()` — second result set after insert | 1 (same roundtrip, extra result) | `reader.NextResult()` + `reader.Read()` |
| `Insert_SeparateSelect` | `INSERT ...; SELECT MAX(Id) FROM ...` — separate query after SP call | 2 | Full second roundtrip |

### Key insights

- `@NewId INT OUTPUT` is the most efficient pattern: the identity value is written to the output parameter
  slot by SQL Server during SP execution, retrieved by `SqlClient` as part of the SP's response packet.
  No second result set, no second roundtrip.
- `SELECT SCOPE_IDENTITY()` requires `reader.NextResultAsync()` to advance past the INSERT's empty result
  set — a small but measurable overhead vs a pure OUTPUT parameter.
- The two-roundtrip `SELECT MAX(Id)` anti-pattern is significantly slower because it pays the full
  per-roundtrip fixed overhead twice, plus it is unsafe under concurrent inserts.
- **CaeriusNet best practice:** use `OUTPUT INSERTED.Id` (for multi-row TVP inserts) or `@NewId INT OUTPUT`
  (for single-row inserts). Never use a separate `SCOPE_IDENTITY()` query or `SELECT MAX(Id)`.

---

## Connection Pool Reuse vs Cold Start

**Benchmark class: `ConnectionPoolBench`**

### What is measured

ADO.NET maintains a `SqlConnection` pool per connection string, reusing physical TCP connections across
logical `Open/Close` calls. This benchmark quantifies the performance difference between:

| Method | Strategy |
|---|---|
| `Connect_WarmPool` *(Baseline)* | `Open()` returns a pooled physical connection — no TCP handshake |
| `Connect_ColdStart` | `ClearPool(connection)` then `Open()` — forces a new TCP handshake + TDS login |
| `Connect_Persistent` | Hold one physical connection open for the entire benchmark iteration — zero pool overhead |

### Key insights

- **Warm pool** is the normal production scenario. `SqlConnection.Open()` dequeues an idle physical
  connection, resetting its state (`sp_reset_connection`) — typically < 100 μs.
- **Cold start** forces a full TCP three-way handshake + TDS pre-login + TDS login sequence.
  This is orders of magnitude more expensive than a warm pool checkout.
  `ClearPool()` should **never be called in production** unless connection credentials change.
- **Persistent connection** has zero checkout overhead — useful for understanding the irreducible
  command-execution cost (no pool interaction at all), but not suitable for concurrent workloads.
- The Ratio between Warm Pool and Cold Start quantifies the value of the connection pool.
  In a typical Docker environment (sub-ms loopback), the cold-start cost is dominated by the TDS
  handshake. Over a real network, cold-start latency is 10–100× higher.
