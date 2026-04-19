# Reading Data

CaeriusNet provides four read methods, each returning a different collection type to match your performance and API requirements. All methods are `async`, accept a `CancellationToken`, and use `CommandBehavior.SequentialAccess` internally for efficient TDS streaming.

## Prerequisites

- A registered `ICaeriusNetDbContext` (via `CaeriusNetBuilder`)
- A `StoredProcedureParameters` built with `StoredProcedureParametersBuilder`
- A DTO implementing `ISpMapper<T>` (or decorated with `[GenerateDto]`)

## Choosing the right method

| Method | Return type | When to use |
|---|---|---|
| `QueryAsIEnumerableAsync` | `IEnumerable<T>?` | Deferred access, LINQ pipelines |
| `QueryAsReadOnlyCollectionAsync` | `ReadOnlyCollection<T>` | Public API surface, immutable contract |
| `QueryAsImmutableArrayAsync` | `ImmutableArray<T>` | Struct-backed, allocation-efficient, frozen data |
| `FirstQueryAsync` | `T?` | Single-row lookups |

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
    // ... implementations below
}
```

## `QueryAsIEnumerableAsync`

Returns `IEnumerable<T>?` (null on empty result set). Best for LINQ-pipeline scenarios or when downstream code materializes the collection later.

```csharp
public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(
    byte age, CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 450)
        .AddParameter("Age", age, SqlDbType.TinyInt)
        .Build();

    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
}
```

## `QueryAsReadOnlyCollectionAsync`

Returns a `ReadOnlyCollection<T>`. Ideal for public APIs where you want to expose an immutable-contract collection.

```csharp
public async Task<ReadOnlyCollection<UserDto>> GetAllUsersAsync(
    CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
        .AddFrozenCache("users:all:frozen")
        .Build();

    return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
}
```

## `QueryAsImmutableArrayAsync`

Returns `ImmutableArray<T>` — a struct wrapper over an array, ideal for frozen data sets that will be cached or passed around without mutation risk.

```csharp
public async Task<ImmutableArray<UserDto>> GetUsersImmutableAsync(
    CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
        .Build();

    return await DbContext.QueryAsImmutableArrayAsync<UserDto>(sp, cancellationToken);
}
```

## `FirstQueryAsync`

Returns `T?` — reads only the first row and returns `null` if no rows are returned. Use for single-entity lookups.

```csharp
public async Task<UserDto?> GetUserByGuidAsync(
    Guid guid, CancellationToken cancellationToken)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Guid")
        .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
        .Build();

    return await DbContext.FirstQueryAsync<UserDto>(sp, cancellationToken);
}
```

## Result set capacity

The third argument to `StoredProcedureParametersBuilder` is `resultSetCapacity`. This pre-allocates the internal `List<T>` to the expected row count, avoiding resizing:

```csharp
// Expecting ~250 rows — pre-allocate to avoid List<T> resizing
new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
```

::: tip Capacity tuning
Set capacity to a reasonable upper-bound estimate. Too low causes extra allocations; too high wastes memory. For write operations (no result set), the default `16` is fine.
:::

## CancellationToken

All read methods accept a `CancellationToken`. Pass it from your controller or caller to support request cancellation:

```csharp
public async Task<IEnumerable<UserDto>> GetAsync(CancellationToken ct)
{
    var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users").Build();
    return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
}
```

## Caching reads

Add per-call caching to any read operation via the builder. See [Caching](/documentation/cache) for full details.

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
    .AddInMemoryCache("users:all", TimeSpan.FromMinutes(2))
    .Build();
```

---

**Next:** [Writing Data](/documentation/writing-data) — execute INSERT, UPDATE, DELETE, and scalar returns.
