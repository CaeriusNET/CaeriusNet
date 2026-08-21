# Caching

Caching is opt-in on a `StoredProcedureCommand`. The runtime has no global cache state and derives keys centrally from the application namespace, a database identity, stored procedure contract, result shape and canonical parameter values. The database identity is hashed before it appears in a key. The binary, length-prefixed identity is SHA-256 hashed into the external form `caerius:v2:<sha256>`; applications must never manufacture a cache key themselves.

| Tier | Package | Behavior |
| --- | --- | --- |
| Memory | `CaeriusNet` | Dynamic TTL/sliding entries, size limit and per-process single-flight. |
| Frozen | `CaeriusNet` | Atomically replaced read snapshot; a miss never rebuilds it. |
| Redis | `CaeriusNet.Redis` | Fully async typed payload, binary version envelope, cross-process cache. |

## Local policies

```csharp
var memory = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .DependsOn("product", "catalog")
    .UseInMemoryCache(TimeSpan.FromMinutes(2), slidingExpiration: true)
    .Build();

var snapshot = new StoredProcedureCommandBuilder("dbo", "usp_Country_List")
    .UseFrozenCache()
    .Build();
```

Use Frozen only for data supplied as a complete snapshot. It is not a lazy cache: `TryGet` never calls SQL or constructs a new dictionary on a miss.

`DependsOn(...)` names logical data dependencies, not literal cache keys. A durable write uses the matching
`Invalidates(...)` tags. This replaces the old `Invalidate(cacheKeys)` contract, which could not address the generated
keys for all result shapes.

```csharp
var rename = new StoredProcedureCommandBuilder("dbo", "usp_Product_Rename")
    .AddParameter("Id", id, SqlDbType.Int)
    .AddParameter("Name", name, SqlDbType.NVarChar, size: 200)
    .Invalidates("product", "catalog")
    .Build();
```

Configure `CaeriusMemoryCacheOptions` with a total `SizeLimit`, a per-entry `MaxEntryWeight`, and
`MaxConcurrentPopulations` (256 by default). A single-flight population owns a cancellation source: it is cancelled
when its final waiter leaves, so abandoned work does not retain a database operation indefinitely.

## Redis policy and codec

```csharp
var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
services.AddSingleton<IConnectionMultiplexer>(multiplexer);
services.AddCaeriusNetRedis(options => options.MaxPayloadBytes = 1024 * 1024);
services.AddCaeriusCacheCodec<Product, ProductCacheCodec>();

var command = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .UseRedisCache(TimeSpan.FromMinutes(5))
    .Build();
```

DTO and AutoContracts generators emit AOT-safe binary codecs and their registration helpers. A manually mapped result must register `ICaeriusCacheCodec<T>` before it can use Redis. The payload includes magic/version, codec ID, contract hash, a null bitmap and deterministic values; an incompatible or corrupt payload is a controlled miss and is deleted best-effort. `MaxPayloadBytes` defaults to 1 MiB; larger values are refused before they are published.

`UseSqlServer(connectionString)` derives the cache database identity from only the SQL Server endpoint and catalog; credentials and other connection-string settings are excluded. A connection or lease factory must provide a stable non-secret identity before Redis can be used:

```csharp
services.AddCaeriusNet(options =>
{
    options.CacheDatabaseIdentity = "orders-production-primary";
    options.UseSqlServer(provider => ResolveOrdersConnection(provider));
});
```

Do not assign a connection string, credential, token, customer value or cache key to `CacheDatabaseIdentity`; connection-string-shaped values are rejected. Redis rejects a missing identity rather than allowing two hosts or databases to share keys accidentally.

Within one host, a Redis read-through flight covers lookup, SQL fallback and Redis population for the canonical key. Concurrent requests therefore issue one SQL fallback and publish one typed payload. The final departing waiter cancels an otherwise unused flight. Read-through Redis lookup/population failures are logged and fall through to SQL.

Redis stores the payload and its logical-tag generation snapshot together. Redis 7.4-compatible Lua scripts validate that
snapshot before publishing, increment tags atomically on invalidation, and compare-delete corrupt payloads. A slow read
that started before a successful write therefore cannot republish its old payload after that write's invalidation.

## Transactions and result sets

Transactions bypass read-through cache. Command invalidations run with `CancellationToken.None` after a successful standalone write or are deferred until transaction commit; rollback discards them. A multi-result value uses a single aggregate cache entry so its result sets remain consistent with each other.

If SQL Server has completed a write or commit and cache invalidation then fails, CaeriusNet throws
`CaeriusCacheInvalidationException`, whose `SqlWasCommitted` property is `true`. The database outcome is durable;
do not blindly retry a non-idempotent procedure. Alert/reconcile the cache or use an idempotency protocol in the
application.

Cache telemetry has low-cardinality tier/outcome dimensions only. It never exports parameter values, cache keys, payloads or TVP data.
