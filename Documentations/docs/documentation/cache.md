# Caching

CaeriusNet supports three caching strategies to reduce database load and improve latency. Caching is opt-in per call via `StoredProcedureParametersBuilder`. When a cache hit occurs, CaeriusNet returns the cached result without executing the Stored Procedure.

## Strategy comparison

| Strategy | Scope | Expiration | Requires setup | Best for |
|---|---|---|---|---|
| **Frozen** | In-process | None — lives until process restart | None | Static reference data (e.g., lookup tables, enums) |
| **InMemory** | In-process | Required (`TimeSpan`) | None | Frequently read data with acceptable staleness |
| **Redis** | Distributed | Optional | `WithRedis` / `WithAspireRedis` | Multi-instance deployments, shared cache |

## Frozen cache

Immutable, in-process cache. Entries persist until the process restarts. No expiration configuration.

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("all_users_frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```

::: tip When to use Frozen
Use Frozen for data that truly never changes during the application lifetime: country lists, currency codes, permission definitions, static lookup tables.
:::

## In-memory cache

In-process cache with a mandatory expiration `TimeSpan`. Entries are automatically evicted after the specified duration.

```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddInMemoryCache("all_users_memory", TimeSpan.FromMinutes(1))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```

::: tip When to use InMemory
Use InMemory for data that is read frequently but can tolerate slight staleness: user profiles, product catalogues, configuration records that change infrequently.
:::

## Redis cache (distributed)

Distributed cache backed by Redis. Expiration is optional — if omitted, the entry persists until Redis evicts it. Requires `WithRedis` or `WithAspireRedis` in the builder.

```csharp
// Required: configure Redis in your DI setup
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(connectionString)
    .WithRedis("localhost:6379")
    .Build();
```

```csharp
// Per-call: with expiration
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddRedisCache("all_users_redis", TimeSpan.FromMinutes(2))
    .Build();

// Per-call: no expiration (entry persists until Redis eviction)
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddRedisCache("all_users_redis")
    .Build();
```

::: tip When to use Redis
Use Redis when multiple instances of your service share the same cache, or when you need cache to survive application restarts. Combine with Aspire for automatic connection management.
:::

## Cache key design

- **Deterministic**: keys must be stable across calls with the same logical input
- **Scoped**: include parameters that affect the result
- **Compact**: avoid very long keys
- **Readable**: use lowercase with `:` separators

```csharp
// Good examples
"users:all"
$"users:age:{age}"
$"orders:user:{userId}:page:{page}"

// Avoid
$"result_{DateTime.UtcNow}"     // non-deterministic
"very_long_key_with_lots_of_data_that_is_hard_to_read"
```

## Cache invalidation

CaeriusNet does not provide explicit cache invalidation APIs — caches are TTL-based. For Frozen cache, restart the process or deploy a new version. For InMemory, tune the `TimeSpan`. For Redis, use `IDatabase.KeyDelete` directly if needed between deployments.

## Security considerations

- Do not cache data that mixes users without a user-scoped key (e.g., `$"users:profile:{userId}"`)
- For Redis in production: enable TLS, use authentication, and restrict network access
- Avoid caching sensitive data (passwords, tokens, PII) unless the risk is acceptable and the cache is properly secured

## Troubleshooting

| Issue | Likely cause | Fix |
|---|---|---|
| Cache miss when hit expected | Different key on each call | Use a deterministic key construction |
| Stale data returned | Expiration too long | Lower the `TimeSpan` or use a scoped key |
| Redis not used | Not configured in builder | Add `.WithRedis(...)` or `.WithAspireRedis(...)` |
| Memory growth | Frozen cache overused | Reserve Frozen for truly static data |

---

**Next:** [Aspire Integration](/documentation/aspire) — configure SQL Server and Redis via .NET Aspire.

