# Caching

CaeriusNet supports three caching strategies to reduce database load and improve latency. Caching is **opt-in per call** via `StoredProcedureParametersBuilder` — you choose the tier exactly where you need it. On a cache hit, CaeriusNet returns the cached result without executing the Stored Procedure and **without creating a DB span**.

## Strategy comparison

| Strategy | Scope | Expiration | DI prerequisite | Best for |
|---|---|---|---|---|
| **Frozen** | In-process | None — lives until process restart | None | Static reference data (lookup tables, enums, country codes) |
| **InMemory** | In-process | Required (`TimeSpan`) | None | Frequently-read data with acceptable staleness |
| **Redis** | Distributed | Optional (`TimeSpan?`) | `WithRedis` / `WithAspireRedis` | Multi-instance deployments, shared cache, surviving restarts |

## Frozen cache

Immutable, in-process cache backed by `FrozenDictionary<TKey, TValue>` for lock-free reads. Entries persist until the process restarts; there is no expiration.

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("users:all:frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

::: tip When to use Frozen
Frozen is for data that **truly never changes** for the lifetime of the process: country lists, currency codes, permission definitions, static lookup tables. If the data needs an invalidation strategy, prefer InMemory or Redis.
:::

## In-memory cache

In-process cache with a mandatory `TimeSpan` expiration. Entries are evicted automatically when their TTL elapses.

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddInMemoryCache("users:all:memory", TimeSpan.FromMinutes(1))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

::: tip When to use InMemory
InMemory is for data read frequently within a single process where some staleness is acceptable: user profiles, product catalogues, configuration records that change infrequently.
:::

## Redis cache (distributed)

Distributed cache backed by Redis through `Microsoft.Extensions.Caching.StackExchangeRedis`. Expiration is **optional** — when omitted, the entry persists until Redis evicts it under memory pressure.

```csharp
// Required: configure Redis once in your DI setup
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(connectionString)
    .WithRedis("localhost:6379")
    .Build();
```

```csharp
// Per-call: with explicit expiration
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddRedisCache("users:all:redis", TimeSpan.FromMinutes(2))
    .Build();

// Per-call: no expiration (entry persists until Redis evicts it)
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddRedisCache("users:all:redis")
    .Build();
```

::: tip When to use Redis
Redis is for **multi-instance** deployments where the cache must be shared across replicas, or where it must survive application restarts. Combine with Aspire (`WithAspireRedis`) for automatic connection-string resolution from the AppHost.
:::

## Cache key design

Good cache keys are **deterministic**, **scoped to the input**, and **readable**:

```csharp
// ✅ Good
"users:all"
$"users:age:{age}"
$"orders:user:{userId}:page:{page}"

// ❌ Avoid
$"result_{DateTime.UtcNow}"      // non-deterministic — never hits
"all_user_data_cached_safely"    // not scoped to inputs — collisions
```

Recommended conventions:

- Lowercase, colon-separated segments
- Most-stable prefix first (`entity`), then identifier, then variant
- Include all parameters that affect the result — otherwise the cache returns the wrong rows

## Cache invalidation

CaeriusNet does not provide explicit invalidation APIs — caches are TTL-based by design. Strategies:

- **Frozen** — restart the process or deploy a new version
- **InMemory** — pick a TTL that matches your acceptable staleness
- **Redis** — call `IDatabase.KeyDelete` directly (e.g., on a deployment hook) or rely on TTL

## Cache and transactions

**Caches are bypassed inside `ICaeriusNetTransaction` scopes.** This prevents uncommitted (dirty) reads from being published to the cache and served to other consumers. After `CommitAsync`, the next non-transactional call populates the cache normally.

See [Transactions — Cache bypass](/documentation/transactions#cache-bypass).

## Telemetry

Every cache lookup is recorded — both hits and misses — through the `caerius.cache.lookups` counter:

| Tag | Value |
|---|---|
| `caerius.cache.tier` | `Frozen`, `InMemory`, or `Redis` |
| `caerius.cache.hit` | `true` on hit, `false` on miss |

On a hit, **no DB span is created** — the trace accurately reflects that the database was not contacted.

## Security considerations

- **Per-user data** must include the user identifier in the key (`$"profile:{userId}"`).
- **Production Redis** should enable TLS, require authentication, and restrict network access to trusted environments.
- **Sensitive data** (passwords, tokens, PII) should not be cached unless the risk is acceptable and the cache is properly secured. Prefer short TTLs.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Cache miss when a hit was expected | Different key on each call | Construct keys deterministically from inputs |
| Stale data returned | TTL too long | Lower the `TimeSpan` or include a version segment |
| `WithRedis` not used | Redis not configured | Add `.WithRedis(...)` or `.WithAspireRedis(...)` to the builder |
| Memory growth | Frozen cache misused | Reserve Frozen for truly static data; prefer InMemory with TTL otherwise |

---

**Next:** [Transactions](/documentation/transactions) — atomic multi-statement units of work with parent `TX` spans.
