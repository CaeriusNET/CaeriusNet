# CaeriusNet.Redis

`CaeriusNet.Redis` adds typed, fully asynchronous Redis caching to CaeriusNet 12. It reuses one
`IConnectionMultiplexer`, uses asynchronous `StringGetLeaseAsync` reads, and writes a versioned binary envelope with the
codec identifier and contract hash. A changed DTO or incompatible payload is therefore a safe cache miss, never an
unsafe deserialization.

## Prerequisites

- .NET 10 and C# 14
- `CaeriusNet` 12.0.0
- A Redis endpoint supported by StackExchange.Redis
- SQL Server 2022 or 2025 for the CaeriusNet database runtime

```bash
dotnet add package CaeriusNet.Redis --version 12.0.0
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
services.AddCaeriusNetRedis();
services.AddCaeriusCacheCodec<Customer, CustomerCacheCodec>();
```

Use a generated DTO codec through its generated registration extension, or register a manual `ICaeriusCacheCodec<T>` as
above. Redis caching is deliberately unavailable for a result shape with no registered codec.

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Customer_List")
    .UseRedisCache(TimeSpan.FromMinutes(5))
    .Build();

var customers = await database.QueryImmutableArrayAsync<Customer>(command, cancellationToken: ct);
```

Read-through lookup and population failures are logged and fall through to SQL. Explicit writes, removes and
invalidations are strict and surface `CaeriusCacheException`. Corrupt payloads are removed best-effort and counted as
misses. A multi-result execution is cached as one aggregate value under one key, never set-by-set.

[Documentation](https://caerius.net) · [Migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/MIGRATION-v11-v12.md) · [Issues](https://github.com/CaeriusNET/CaeriusNet/issues)
