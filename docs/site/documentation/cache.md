# Caching

Caching is opt-in on a `StoredProcedureCommand`. The runtime has no global cache state and derives keys centrally from the application namespace, a database identity, stored procedure contract, result shape and canonical parameter values. The database identity is hashed before it appears in a key.

| Tier | Package | Behavior |
| --- | --- | --- |
| Memory | `CaeriusNet` | Dynamic TTL/sliding entries, size limit and per-process single-flight. |
| Frozen | `CaeriusNet` | Atomically replaced read snapshot; a miss never rebuilds it. |
| Redis | `CaeriusNet.Redis` | Fully async typed payload, binary version envelope, cross-process cache. |

## Local policies

```csharp
var memory = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .UseInMemoryCache(TimeSpan.FromMinutes(2), slidingExpiration: true)
    .Build();

var snapshot = new StoredProcedureCommandBuilder("dbo", "usp_Country_List")
    .UseFrozenCache()
    .Build();
```

Use Frozen only for data supplied as a complete snapshot. It is not a lazy cache: `TryGet` never calls SQL or constructs a new dictionary on a miss.

## Redis policy and codec

```csharp
var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
services.AddSingleton<IConnectionMultiplexer>(multiplexer);
services.AddCaeriusNetRedis();
services.AddCaeriusCacheCodec<Product, ProductCacheCodec>();

var command = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .UseRedisCache(TimeSpan.FromMinutes(5))
    .Build();
```

DTO and AutoContracts generators emit AOT-safe binary codecs and their registration helpers. A manually mapped result must register `ICaeriusCacheCodec<T>` before it can use Redis. The payload includes magic/version, codec ID, contract hash, a null bitmap and deterministic values; an incompatible or corrupt payload is a controlled miss and is deleted best-effort.

`UseSqlServer(connectionString)` derives the cache database identity from only the SQL Server endpoint and catalog; credentials and other connection-string settings are excluded. A connection or lease factory must provide a stable non-secret identity before Redis can be used:

```csharp
services.AddCaeriusNet(options =>
{
    options.CacheDatabaseIdentity = "orders-production-primary";
    options.UseSqlServer(provider => ResolveOrdersConnection(provider));
});
```

Do not assign a connection string, credential, token, customer value or cache key to `CacheDatabaseIdentity`; connection-string-shaped values are rejected. Redis rejects a missing identity rather than allowing two hosts or databases to share keys accidentally.

Within one host, a Redis read-through flight covers lookup, SQL fallback and Redis population for the canonical key. Concurrent requests therefore issue one SQL fallback and publish one typed payload, while a cancelled waiter only stops waiting for its own result. Read-through Redis lookup/population failures are logged and fall through to SQL. Explicit cache mutations—including command-declared invalidation after an accepted write—are strict and surface `CaeriusCacheException`.

## Transactions and result sets

Transactions bypass read-through cache. Command invalidations run after a successful standalone write or are deferred until transaction commit; rollback discards them. A multi-result value uses a single aggregate cache entry so its result sets remain consistent with each other.

Cache telemetry has low-cardinality tier/outcome dimensions only. It never exports parameter values, cache keys, payloads or TVP data.
