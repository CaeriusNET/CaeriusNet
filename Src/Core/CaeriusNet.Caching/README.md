# CaeriusNet.Caching

A SQL Server–first, application-level caching component for .NET 9. CaeriusNet.Caching accelerates read paths for stored
procedure–based data access while remaining cache-agnostic at the orchestration layer. The database remains the source
of truth; caching is an optional optimization applied per operation.

This package is authored in C# 13 and is part of the CaeriusNet platform.

## Overview

CaeriusNet.Caching provides a unified, pluggable caching model to serve materialized results for SQL Server workloads.
It enables per-call cache selection, deterministic keys, and explicit expiration semantics—without binding application
logic to a single cache technology.

Key attributes:

- SQL Server–only data access design, with an application cache layered above it
- Cache-agnostic orchestration (supports multiple cache strategies)
- Opt-in, per-operation caching with graceful degradation

## Design goals

- Predictable: Caching must never compromise correctness; the database is authoritative.
- Explicit: Cache type, key, and expiration are chosen per operation.
- Flexible: Multiple cache strategies can be used without changing calling code.
- Resilient: Failures in cache read/write never fail the request path.

## Cache strategies

- InMemory
    - Process-local, ultra-low latency
    - Best for hot-path reads within a single node
    - Time-bound with explicit expiration

- Frozen
    - Immutable, read-only snapshots optimized for thread-safe, high-throughput reads
    - Ideal for stable reference/lookup data
    - Updated via controlled rebuilds (no per-item expiration)

- Redis (optional)
    - Distributed, cross-node cache for scale-out scenarios
    - Time-bound with TTL; suitable when multiple processes must share cached results
    - Optional to the core SQL Server design

## How it works

- Per-operation policy: Each database operation can specify a cache strategy, a deterministic key, and an optional
  expiration (when applicable).
- Orchestration layer: A single provider routes TryGet/Store to the selected cache strategy. If configuration is
  incomplete or a cache is unavailable, the request proceeds without caching.
- Non-authoritative: Cache misses or cache errors do not surface to callers; the database read continues normally.

## Configuration and usage guidance

- Key design
    - Make keys deterministic and stable; derive them from the stored procedure identity and its inputs.
    - Include context dimensions as needed (e.g., tenant, locale, version).
    - Use namespacing to avoid collisions across domains.

- Expiration policy
    - InMemory: Tune TTLs to balance hit rate and memory usage.
    - Frozen: No expiration; rebuild the snapshot when data changes.
    - Redis: Set TTLs that align with data freshness SLAs and cross-node coherence needs.

- Redis considerations (optional)
    - Use only when cross-process/node reuse is required.
    - Secure with authentication/TLS and apply key prefixes or isolated databases.
    - Monitor latency and serialization overhead.

## Common scenarios

- Query result caching
    - Cache materialized DTOs from stored procedures using deterministic keys.
    - Use InMemory for single-node hot paths; use Redis to share across instances.

- Reference data
    - Store stable lookup tables in Frozen for lock-free, read-optimized access.
    - Rebuild on change to maintain consistency.

- API read short-circuit
    - Serve responses directly from cache when present and within SLA.
    - Fall through to SQL Server for cache misses or expired items.

## Performance

- InMemory
    - Sub-millisecond access, minimal overhead
    - Limited by process memory and lifecycle

- Frozen
    - Excellent read throughput and concurrency with immutable structures
    - Full-structure rebuild on updates; plan refresh windows accordingly

- Redis
    - Network-bound latency plus serialization cost
    - Enables horizontal scale with shared cache state

## Reliability and resilience

- Graceful degradation: Cache failures never break the request path.
- Source of truth: SQL Server remains the authoritative data store.
- Fault isolation: Cache exceptions are contained within the caching layer.

## Security

- Data classification: Avoid caching secrets or sensitive PII unless strictly necessary and protected.
- Access control: Restrict distributed cache access; enforce TLS/auth where applicable.
- Key hygiene: Validate inputs used for key composition to prevent abuse.

## Observability

- Track cache hit/miss ratio, item sizes, and cardinality
- Monitor latency for cache reads/writes, especially for distributed cache
- Observe rebuild timings and impact for Frozen snapshots

## Best practices

- Treat caching as an optimization, not persistence
- Keep cached payloads small and focused
- Prefer the simplest strategy that meets the requirement (InMemory → Frozen → Redis)
- Version keys when result shapes or semantics change
- Measure before tuning TTLs and cache selection

## FAQ

- Is this component database-agnostic?
    - No. The data access model is designed exclusively for SQL Server. The cache orchestration itself is
      cache-agnostic.

- Is Redis required?
    - No. Redis is optional and provided for flexibility in distributed scenarios.

- Does caching change consistency?
    - Caching can introduce controlled staleness (TTL or snapshot lifetime). Configure per SLA.

- Can I mix strategies?
    - Yes. Select the cache strategy per operation without altering core business logic.