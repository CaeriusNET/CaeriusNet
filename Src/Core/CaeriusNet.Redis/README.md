# CaeriusNet.Redis

High-performance, async-only Redis access for CaeriusNet with a single long-lived connection per connection string.

Key features:

- Strictly asynchronous API (no sync wrappers), CancellationToken accepted on all public methods.
- Single ConnectionMultiplexer per connection string to avoid costly connect/disconnect churn.
- Focused, rigorously named key-value operations for predominant GET/SET use cases.
- Optimized parameter order and minimal allocations in the hot path.

## Packages

- StackExchange.Redis (2.9.11)
- Microsoft.Extensions.DependencyInjection.Abstractions (9.0.0) for DI registration helpers

## Registration

```csharp
using CaeriusNet.Redis.Extensions;

var services = new ServiceCollection();
services.AddCaeriusRedis(
    connectionString: "your-redis:6379,password=...", // mandatory, non-empty
    defaultDatabase: 0,                                  // optional; -1 uses server default
    storeDatabase: 0                                     // optional; -1 follows defaultDatabase
);
```

This registers:

- IRedisConnectionProvider: singleton maintaining a single ConnectionMultiplexer
- IRedisKeyValueStore: singleton bound to a chosen database index

## Usage

```csharp
using CaeriusNet.Redis.Abstractions;

var provider = services.BuildServiceProvider();
var store = provider.GetRequiredService<IRedisKeyValueStore>();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

// Set a string value with a TTL
await store.SetStringAsync("user:42:name", "Ada", TimeSpan.FromMinutes(10), keepTtl: false, cts.Token);

// Get the string back
string? name = await store.GetStringAsync("user:42:name", cts.Token);

// Set and get bytes
byte[] payload = [1, 2, 3, 4];
await store.SetBytesAsync("blob:payload", payload, TimeSpan.FromHours(1), keepTtl: false, cts.Token);
byte[]? loaded = await store.GetBytesAsync("blob:payload", cts.Token);

// Key management
bool exists = await store.KeyExistsAsync("user:42:name", cts.Token);
bool ttlSet = await store.ExpireKeyAsync("user:42:name", TimeSpan.FromMinutes(1), cts.Token);
TimeSpan? ttl = await store.GetTimeToLiveAsync("user:42:name", cts.Token);
bool deleted = await store.DeleteKeyAsync("user:42:name", cts.Token);
```

## Cancellation semantics

StackExchange.Redis is Task-based and does not accept a CancellationToken for most operations.

- Connect: the provider uses Task.WaitAsync(ct) so callers can stop waiting; the underlying connect continues in the
  background.
- Commands: we pre-check the token to avoid starting work if the caller is canceled. Once dispatched, the driver
  operation cannot be forcibly canceled.

## Performance notes

- ConnectionMultiplexer is expensive; we create it once and reuse it.
- IDatabase is cheap to obtain; we cache nothing and ask the multiplexer for the database per call after ensuring
  connection.
- GET/SET methods avoid LINQ and unnecessary allocations. For byte payloads, we currently use ToArray() to map
  ReadOnlyMemory<byte> into RedisValue in a safe way across library versions.
- We avoid per-call WaitAsync to keep overhead low; use small TTLs and cancellation pre-checks for responsiveness.

## Design guarantees

- Connection string is mandatory; empty/whitespace throws an ArgumentException.
- AbortOnConnectFail=false and KeepAlive defaults applied for resilience.
- Async disposal closes the connection gracefully (allowing in-flight commands to complete).

## Extensibility

- IRedisConnectionProvider abstracts the connection lifecycle; custom providers can be implemented for advanced
  scenarios (e.g., injected ConnectionMultiplexer, instrumented provider).
- IRedisKeyValueStore focuses on key/value; additional modules can be added for hashes, sets, streams, etc., following
  the same async and CT rules.

## Versioning & Quality

- Target: .NET 9.0, nullable enabled, implicit usings enabled.
- XML docs generated; warnings for missing XML docs suppressed via NoWarn 1591 for brevity.

