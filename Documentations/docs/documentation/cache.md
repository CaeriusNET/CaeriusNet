# Caching

Caerius.NET supports three caching strategies to reduce database load and improve latency. You opt-in per call when building StoredProcedureParameters. When Redis is configured, distributed caching becomes available; otherwise, only in-process caches are used.

- Frozen cache: immutable, in-process, fastest lookups. Ideal for static reference data that rarely changes.
- In-memory cache: in-process with expiration policy. Good for frequently used results with acceptable staleness.
- Redis cache: distributed cache for multi-instance deployments. Requires Redis configuration.

## Prerequisites

- Frozen and In-memory caches require no external setup.
- Redis requires configuration via CaeriusNetBuilder:

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    .WithRedis("localhost:6379") // or .WithAspireRedis("redis") in Aspire
    .Build();
```

## How to enable caching per call

Use the appropriate Add*Cache method on StoredProcedureParametersBuilder.

### Frozen cache
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("all_users_frozen")
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```
- No expiration. Values remain until process restarts.
- Best for rarely changing data (e.g., lookup tables).

### In-memory cache
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddInMemoryCache("all_users_memory", TimeSpan.FromMinutes(1))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```
- Expiration is required. Data lives in the current process only.

### Redis cache (distributed)
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddRedisCache("all_users_redis", TimeSpan.FromMinutes(2))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```
- Requires builder configuration (WithRedis or WithAspireRedis).
- Expiration is optional; if omitted, entry persists until evicted.

## Cache key design

- Use deterministic keys, e.g., include input parameters: users:age:>=30
- Avoid very long keys.
- Prefer lowercase and : separators for readability.

## Troubleshooting

- Cache miss when expected hit:
  - Verify the same cache key is used across calls.
  - Ensure identical StoredProcedureParameters (including schema/name) are used.
  - For Redis, confirm connectivity and that your app is configured with WithRedis/WithAspireRedis.

- Memory growth:
  - Frozen cache grows monotonically in-process; use it for true constants only.
  - In-memory cache entries expire; tune expiration to your workload.

## Security considerations

- Do not cache sensitive data unless required and safe.
- For Redis, use TLS and authentication in production. Restrict network access.
