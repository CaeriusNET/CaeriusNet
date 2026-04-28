# Usage

This section covers all runtime usage of CaeriusNet. Content is organized by topic — use the navigation below or the sidebar to jump directly to what you need.

## Topics

### Repository setup

All data access goes through repositories that inject `ICaeriusNetDbContext`. See the [Installation & Setup](/quickstart/getting-started) guide for DI registration.

```csharp
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository { }
```

### Reading data

| Guide | Description |
|---|---|
| [DTO Mapping](/documentation/dto-mapping) | Ordinal-based mapping, nullability, special types |
| [Source Generators](/documentation/source-generators) | `[GenerateDto]` and `[GenerateTvp]` |
| [Reading Data](/documentation/reading-data) | `QueryAsIEnumerableAsync`, `FirstQueryAsync`, capacity |
| [Multiple Result Sets](/documentation/multi-results) | Fetch 2–5 result sets in one round-trip |

### Writing data

| Guide | Description |
|---|---|
| [Writing Data](/documentation/writing-data) | `ExecuteNonQueryAsync`, `ExecuteAsync`, `ExecuteScalarAsync` |
| [Table-Valued Parameters](/documentation/tvp) | Bulk inputs via `ITvpMapper<T>` |

### Infrastructure

| Guide | Description |
|---|---|
| [Caching](/documentation/cache) | Frozen, InMemory, Redis — per-call opt-in |
| [Aspire Integration](/documentation/aspire) | `WithAspireSqlServer`, `WithAspireRedis` |

### Reference

| Guide | Description |
|---|---|
| [API Reference](/documentation/api) | Full public surface |
| [Best Practices](/documentation/best-practices) | Architecture, SQL, performance, security |

## Quick example

A full read + write repository:

```csharp
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(
        byte age, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 450)
            .AddParameter("Age", age, SqlDbType.TinyInt)
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
    }

    public async Task<int> UpdateUserAgeAsync(
        Guid guid, byte age, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
            .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
            .AddParameter("Age", age, SqlDbType.TinyInt)
            .Build();

        return await DbContext.ExecuteNonQueryAsync(sp, ct);
    }
}
```
