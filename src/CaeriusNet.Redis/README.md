# CaeriusNet.Redis

`CaeriusNet.Redis` adds typed, fully asynchronous Redis caching to CaeriusNet 12. It reuses one
`IConnectionMultiplexer`, uses asynchronous `StringGetLeaseAsync` reads, and writes a versioned binary envelope with the
codec identifier, contract hash and logical-tag generation snapshot. A changed DTO or incompatible payload is therefore
a safe cache miss, never an unsafe deserialization.

## Prerequisites

- .NET 10 and C# 14
- `CaeriusNet` 11.2.0
- A Redis endpoint supported by StackExchange.Redis
- SQL Server 2022 or 2025 for the CaeriusNet database runtime

```bash
dotnet add package CaeriusNet.Redis --version 11.2.0
```

## Configure

```csharp
using CaeriusNet;
using CaeriusNet.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

services.AddCaeriusNet(options => options.UseSqlServer(sqlConnectionString));
// Aspire's AddRedisClient registers this singleton for you. In a plain host, create it
// asynchronously during composition and reuse it for the lifetime of the application.
var multiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
services.AddSingleton<IConnectionMultiplexer>(multiplexer);
services.AddCaeriusNetRedis(options => options.MaxPayloadBytes = 1024 * 1024);
services.AddCaeriusCacheCodec<Customer, CustomerCacheCodec>();
```

Use a generated DTO codec through its generated registration extension, or register a manual `ICaeriusCacheCodec<T>` as
above. Redis caching is deliberately unavailable for a result shape with no registered codec.

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Customer_List")
    .DependsOn("customer")
    .UseRedisCache(TimeSpan.FromMinutes(5))
    .Build();

var customers = await database.QueryImmutableArrayAsync<Customer>(command, cancellationToken: ct);
```

Keys use `caerius:v2:<sha256>`, generated from length-prefixed binary contract fields. Tag generations are stored and
validated atomically with each payload by Redis 7.4-compatible Lua scripts: a slow SQL fallback begun before a write
cannot publish a stale value after that write invalidates a matching tag. Use `Invalidates("customer")` on the durable
write, not a literal cache key.

Read-through lookup and population failures are logged and fall through to SQL. A flight is cancelled when its final
waiter leaves. Explicit writes, removes and invalidations are strict. If SQL has committed but invalidation fails,
`CaeriusCacheInvalidationException.SqlWasCommitted` is `true`; do not retry a non-idempotent procedure blindly. Corrupt
payloads are compare-deleted best-effort and counted as misses. Payloads above `MaxPayloadBytes` (1 MiB by default) are
not published. A multi-result execution is cached as one aggregate value under one key, never set-by-set.

[Documentation](https://caerius.net) · [Migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md) · [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
