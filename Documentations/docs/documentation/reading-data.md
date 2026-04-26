# Reading Data

CaeriusNet exposes four read methods on `ICaeriusNetDbContext`, each returning a different collection type so you can match your performance, allocation, and API-shape requirements. All methods are `async`, accept a `CancellationToken`, and use `CommandBehavior.SequentialAccess` internally for efficient TDS streaming.

## Prerequisites

- A registered `ICaeriusNetDbContext` (via `CaeriusNetBuilder` — see [Installation & Setup](/quickstart/getting-started))
- A `StoredProcedureParameters` value built with `StoredProcedureParametersBuilder`
- A DTO implementing `ISpMapper<T>` (typically generated with `[GenerateDto]`)

## Choosing the right method

| Method | Return type | When to use |
|---|---|---|
| `QueryAsIEnumerableAsync<T>` | `IEnumerable<T>?` | Deferred enumeration, LINQ pipelines |
| `QueryAsReadOnlyCollectionAsync<T>` | `ReadOnlyCollection<T>` | Public APIs that expose an immutable contract |
| `QueryAsImmutableArrayAsync<T>` | `ImmutableArray<T>` | Frozen, struct-backed, allocation-efficient data |
| `FirstQueryAsync<T>` | `T?` | Single-row lookups (returns `null` when empty) |

::: tip Allocation vs. ergonomics
- `IEnumerable<T>` keeps the door open for downstream LINQ but exposes an enumerator allocation.
- `ReadOnlyCollection<T>` materializes a `List<T>` and wraps it — predictable, indexable, immutable.
- `ImmutableArray<T>` is a struct over an array — best for cached, hot-path data passed by value.
:::

## Repository setup

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;
using System.Data;

public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    // ... method implementations below
}
```

## `QueryAsIEnumerableAsync`

Returns `IEnumerable<T>?` (`null` on empty result set). Best for LINQ-pipeline scenarios or when downstream code materializes the collection later.

```csharp
public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(
    byte age, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", capacity: 450)
        .AddParameter("Age", age, SqlDbType.TinyInt)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
}
```

## `QueryAsReadOnlyCollectionAsync`

Returns a `ReadOnlyCollection<T>` — empty when the SP returns no rows. Ideal for public APIs where the caller should see an immutable contract.

```csharp
public async Task<ReadOnlyCollection<UserDto>> GetAllUsersAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
        .AddFrozenCache("users:all:frozen")
        .Build();

    return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
}
```

## `QueryAsImmutableArrayAsync`

Returns `ImmutableArray<T>` — a struct wrapper over an array. Ideal for frozen data sets that will be cached or passed around without mutation.

```csharp
public async Task<ImmutableArray<UserDto>> GetUsersImmutableAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
        .Build();

    return await DbContext.QueryAsImmutableArrayAsync<UserDto>(sp, ct);
}
```

## `FirstQueryAsync`

Returns `T?` — reads only the first row and returns `null` if the result set is empty. Use it for single-entity lookups.

```csharp
public async Task<UserDto?> GetUserByGuidAsync(Guid guid, CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .Build();

    return await DbContext.FirstQueryAsync<UserDto>(sp, ct);
}
```

## Result-set capacity

The third constructor argument of `StoredProcedureParametersBuilder` is `resultSetCapacity`. It pre-allocates the internal `List<T>` to the expected row count, avoiding reallocations as rows are added:

```csharp
// Expecting ~250 rows — pre-allocate to skip List<T> resizing
new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", capacity: 250);
```

::: tip Capacity tuning
Pick a reasonable upper bound. Too low triggers extra allocations; too high wastes memory. For write operations (no result set), the default `1` is fine — capacity is ignored.
:::

## `CancellationToken`

Every read method takes a `CancellationToken`. Propagate it from your controller, hosted service, or caller so the SQL command is cancelled if the request is aborted:

```csharp
public async Task<IEnumerable<UserDto>> GetAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users").Build();
    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
}
```

## Caching reads

Add per-call caching to any read by chaining a cache method on the builder. See [Caching](/documentation/cache) for the full guide.

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
    .AddInMemoryCache("users:all", TimeSpan.FromMinutes(2))
    .Build();
```

On a cache hit, **no SQL command is executed and no DB span is created** — only the `caerius.cache.lookups{hit=true}` counter ticks.

---

**Next:** [Writing Data](/documentation/writing-data) — execute INSERT, UPDATE, DELETE, and scalar returns.
