---
title: Performance & Benchmarks
description: Redirect page for CaeriusNet benchmark documentation.
---

# Performance & Benchmarks

The benchmark documentation is organized into focused pages so results and methodology are easier to review.

| Page | Scope |
|---|---|
| [Overview & Methodology](./index) | BenchmarkDotNet configuration, local commands, CI mode, and table interpretation. |
| [In-Memory Benchmarks](./in-memory) | DTO mapping, TVP serialization, and parameter-builder CPU/allocation measurements. |
| [Collection Benchmarks](./collections) | Read, create, and capacity behavior for supported collection shapes. |
| [SQL Server Benchmarks](./sql-server) | Stored-procedure execution, batched inserts, TVP roundtrips, output parameters, and connection pooling. |
| [Cache Benchmarks](./cache) | Frozen and in-memory cache read/write behavior. |

::: warning Interpret benchmark numbers carefully
Benchmark results are not product guarantees. Use them to understand trends, then measure your own workload with your schema, SQL plans, network path, database edition, and deployment hardware.
:::
