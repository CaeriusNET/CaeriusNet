---
title: Cache benchmarks
description: CaeriusNet v12 typed binary-codec cache benchmark methodology.
---

# Cache benchmarks

The v12 benchmark suite focuses on the typed codec that backs Redis cache entries. The runtime has three injected cache managers—memory, Frozen snapshots and Redis—but cache entries are never represented by shared global state.

## Codec hit and miss

**Benchmark class:** `CacheCodecBenchmarks`

| Benchmark | What it measures |
| --- | --- |
| `EncodeDto` | Deterministic DTO binary encoding |
| `CodecCacheHit` | Decode of a previously encoded typed payload |
| `CodecCacheMissAndPopulate` | Miss, encode and population work without an external Redis round trip |

The generated codec is AOT-safe and carries a codec identity and contract hash. Redis adds a versioned envelope around this payload. An incompatible or corrupt envelope is a controlled miss, removed best-effort, and counted separately; it is never deserialized as an older DTO contract.

## What is intentionally outside this microbenchmark

An `IConnectionMultiplexer` is reused at application scope, while `IDatabase` is lightweight. Real Redis latency, retry behavior and server throughput belong to integration tests and deployment measurements rather than a CPU-only BenchmarkDotNet result.

Likewise, Frozen cache reads are snapshot lookups: a miss does not build a new dictionary. Memory cache population uses local single-flight work and expiration policy. Benchmark these operations in a host that reflects the application's expected cache size and concurrency.

Run the codec suite with:

```bash
dotnet run -c Release --project benchmarks/CaeriusNet.Benchmark.csproj -- --filter "*CacheCodecBenchmarks*"
```
