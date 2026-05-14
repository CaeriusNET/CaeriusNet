# Usage overview

This section shows where to go for each CaeriusNet task. Start here when you know what you want to do, but you are not sure which API or guide applies.

## Typical workflow

Most CaeriusNet code follows the same pattern:

1. Define or generate DTO and TVP types.
2. Build stored procedure settings with `StoredProcedureParametersBuilder`.
3. Call a read, write, multi-result, cache, or transaction API.
4. Let CaeriusNet handle connection lifetime, command execution, telemetry, and exception wrapping.

```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);

var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_By_Age", 128)
    .AddParameter("Age", 18, SqlDbType.Int)
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, ct);
```

## Choose a guide

| Task | Start with |
|---|---|
| Install the package and register services | [Install and configure](/quickstart/getting-started) |
| Map stored procedure result rows to DTOs | [DTO mapping](/documentation/dto-mapping) |
| Generate DTO or TVP mapper code | [Source generators](/documentation/source-generators) |
| Read a result set | [Reading data](/documentation/reading-data) |
| Execute writes, deletes, updates, or scalar commands | [Writing data](/documentation/writing-data) |
| Send a list of values to SQL Server | [Table-valued parameters](/documentation/tvp) |
| Read related result sets in one round trip | [Multiple result sets](/documentation/multi-results) |
| Cache a read call | [Caching](/documentation/cache) |
| Run several commands atomically | [Transactions](/documentation/transactions) |
| Connect to Aspire resources | [Aspire integration](/documentation/aspire) |
| Capture traces, metrics, and logs | [Logging and observability](/documentation/logging) |
| Review production recommendations | [Best practices](/documentation/best-practices) |
| Look up method signatures | [API reference](/documentation/api) |

## Runtime API groups

### Read APIs

Use read APIs when the stored procedure returns one or more rows.

| Method | Return shape |
|---|---|
| `FirstQueryAsync<T>` | `T?` |
| `QueryAsIEnumerableAsync<T>` | `IEnumerable<T>` |
| `QueryAsReadOnlyCollectionAsync<T>` | `ReadOnlyCollection<T>` |
| `QueryAsImmutableArrayAsync<T>` | `ImmutableArray<T>` |

### Write APIs

Use write APIs when the stored procedure changes data or returns a scalar value.

| Method | Return shape |
|---|---|
| `ExecuteNonQueryAsync` | Rows affected |
| `ExecuteScalarAsync<T>` | First column of the first row |
| `ExecuteAsync` | No result |

### Multi-result APIs

Use multi-result APIs when one stored procedure returns related data sets.

| Method family | Return shape |
|---|---|
| `QueryMultipleIEnumerableAsync` | Tuple of `IEnumerable<T>` values |
| `QueryMultipleReadOnlyCollectionAsync` | Tuple of `ReadOnlyCollection<T>` values |
| `QueryMultipleImmutableArrayAsync` | Tuple of `ImmutableArray<T>` values |

### Infrastructure APIs

Use infrastructure APIs when you need caching, transactions, or observability.

| Area | API |
|---|---|
| Caching | `AddFrozenCache`, `AddInMemoryCache`, `AddRedisCache`, `ICaeriusNetCache` |
| Transactions | `BeginTransactionAsync`, `ICaeriusNetTransaction` |
| Telemetry | `CaeriusDiagnostics`, `CaeriusTelemetryOptions` |
| Setup | `CaeriusNetBuilder` |

## Example repository

```csharp
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
{
    public async ValueTask<IEnumerable<UserDto>> GetUsersOlderThanAsync(
        byte age,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 450)
            .AddParameter("Age", age, SqlDbType.TinyInt)
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
    }

    public async ValueTask<int> UpdateUserAgeAsync(
        Guid guid,
        byte age,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
            .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
            .AddParameter("Age", age, SqlDbType.TinyInt)
            .Build();

        return await DbContext.ExecuteNonQueryAsync(sp, ct);
    }
}
```

## Documentation conventions

- C# examples use `ValueTask` when they directly expose CaeriusNet hot-path calls.
- SQL examples use `SET NOCOUNT ON` to prevent extra result sets.
- Builder parameter names omit the SQL `@` prefix.
- DTO examples use `[GenerateDto]` unless a manual mapper is being explained.
- TVP examples use `[GenerateTvp]` unless a manual mapper is being explained.
