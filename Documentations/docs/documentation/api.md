# API Reference

This section provides a practical reference for the main public APIs exposed by the CaeriusNet package. All examples use C# 12/.NET 10 and Microsoft.Data.SqlClient.

- Namespaces shown below are abbreviated; use your project’s using directives accordingly.
- Code examples assume Dependency Injection is configured via CaeriusNetBuilder.

## Builders

### CaeriusNet.Builders.CaeriusNetBuilder
Configures CaeriusNet services for DI.
```csharp
static CaeriusNetBuilder Create(IServiceCollection services);
static CaeriusNetBuilder Create(IHostApplicationBuilder builder);
CaeriusNetBuilder WithSqlServer(string connectionString);
CaeriusNetBuilder WithRedis(string? connectionString);
CaeriusNetBuilder WithAspireSqlServer(string connectionName = "sqlserver");
CaeriusNetBuilder WithAspireRedis(string connectionName = "redis");
IServiceCollection Build();
```
Example:
```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    // .WithRedis("localhost:6379")
    .Build();
```

### CaeriusNet.Builders.StoredProcedureParametersBuilder
Fluent builder for stored-procedure execution settings, parameters, and caching.

Constructor:
```csharp
StoredProcedureParametersBuilder(string schemaName, string procedureName, int resultSetCapacity = 1);
```

Parameter methods:
```csharp
StoredProcedureParametersBuilder AddParameter(string parameter, object value, SqlDbType dbType);
StoredProcedureParametersBuilder AddTvpParameter<T>(string parameter, IEnumerable<T> items) where T : class, ITvpMapper<T>;
```

Caching methods:
```csharp
StoredProcedureParametersBuilder AddInMemoryCache(string cacheKey, TimeSpan expiration);
StoredProcedureParametersBuilder AddFrozenCache(string cacheKey);
StoredProcedureParametersBuilder AddRedisCache(string cacheKey, TimeSpan? expiration = null);
```

Build:
```csharp
StoredProcedureParameters Build()
```

Example (read with capacity):
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", resultSetCapacity: 250)
    .Build();
```

Example (parameters + TVP + cache):
```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Tvp_Ids_And_Age", 1024)
    .AddTvpParameter("Ids", tvpItems)                    // T implements ITvpMapper<T>
    .AddParameter("Age", age, SqlDbType.Int)
    .AddRedisCache("users:age:" + age, TimeSpan.FromMinutes(2))
    .Build();
```

## Abstractions

### CaeriusNet.Abstractions.ICaeriusNetDbContext
Represents a factory for opening SQL connections and exposes an optional Redis cache manager.

Properties:
```csharp
IRedisCacheManager? RedisCacheManager { get; }
```

Methods:
```csharp
SqlConnection DbConnection()
```

### CaeriusNet.Abstractions.IRedisCacheManager
Distributed cache adapter used when Redis is configured.
```csharp
bool TryGet<T>(string cacheKey, out T? value);
void Store<T>(string cacheKey, T value, TimeSpan? expiration) where T : notnull
```

## Mappers

### CaeriusNet.Mappers.ISpMapper
Compile-time friendly contract for mapping a row from SqlDataReader.
```csharp
static abstract T MapFromDataReader(SqlDataReader reader)
```

Usage (manual):
```csharp
public sealed record UserDto(int Id, string Name) : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1));
}
```

### CaeriusNet.Mappers.ITvpMapper
Defines how to convert items to a DataTable for TVP.
```csharp
static abstract string TvpTypeName { get; }
DataTable MapAsDataTable(IEnumerable<T> items)
```

Usage (manual):
```csharp
public sealed record UsersIdsTvp(int Id) : ITvpMapper<UsersIdsTvp>
{
    public static string TvpTypeName => "dbo.tvp_int";
    public DataTable MapAsDataTable(IEnumerable<UsersIdsTvp> items)
    {
        var table = new DataTable("dbo.tvp_int");
        table.Columns.Add("Id", typeof(int));
        foreach (var it in items) table.Rows.Add(it.Id);
        return table;
    }
}
```

## Attributes (Source Generators)

### CaeriusNet.Attributes.Dto.GenerateDtoAttribute
Annotate sealed partial records/classes to generate `ISpMapper<T>` at compile time.
```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte? Age);
```

### CaeriusNet.Attributes.Tvp.GenerateTvpAttribute
Annotate sealed partial records/classes to generate `ITvpMapper<T>`.

Required init properties:
```csharp
string Schema { get; init; } = "dbo"
string TvpName { get; init; }
```
Exemple:
```csharp
[GenerateTvp(Schema = "Types", TvpName = "tvp_Int")]
public sealed partial record UsersIntTvp(int UserId);
```

## Read Commands
Extension methods on ICaeriusNetDbContext for reading result sets.

Namespace: CaeriusNet.Commands.Reads.SimpleReadSqlAsyncCommands
```csharp
ValueTask<TResultSet?> FirstQueryAsync<TResultSet>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask<ReadOnlyCollection<TResultSet>> QueryAsReadOnlyCollectionAsync<TResultSet>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask<IEnumerable<TResultSet>?> QueryAsIEnumerableAsync<TResultSet>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask<ImmutableArray<TResultSet>> QueryAsImmutableArrayAsync<TResultSet>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
```

Each TResultSet must be a class implementing `ISpMapper<TResultSet>`.

Example:
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250).Build();
var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp);
```

## Write Commands
Extension methods on ICaeriusNetDbContext for non-query operations.

Namespace: CaeriusNet.Commands.Writes.WriteSqlAsyncCommands
```csharp
ValueTask<T?> ExecuteScalarAsync<T>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask<int> ExecuteNonQueryAsync(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
ValueTask ExecuteAsync(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
```

Examples:
```csharp
// Get affected rows
var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUserAge_By_Guid")
    .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
    .AddParameter("Age", age, SqlDbType.TinyInt)
    .Build();
var affected = await dbContext.ExecuteNonQueryAsync(sp);

// Fire-and-forget (no count)
await dbContext.ExecuteAsync(sp);
```

## Multiple Result Sets
Namespace: CaeriusNet.Commands.Reads.MultiIEnumerableReadSqlAsyncCommands
```csharp
Task<(IEnumerable<T1>, IEnumerable<T2>)> QueryMultipleIEnumerableAsync<T1, T2>(ICaeriusNetDbContext, StoredProcedureParameters, CancellationToken);
Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>)> QueryMultipleIEnumerableAsync<T1, T2, T3>(...);
Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>)> QueryMultipleIEnumerableAsync<T1, T2, T3, T4>(...);
Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>, IEnumerable<T5>)> QueryMultipleIEnumerableAsync<T1, T2, T3, T4, T5>(...);
```

Example:
```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_Get_Dashboard_Data", 128).Build();
var (users, orders, products) = await dbContext
    .QueryMultipleIEnumerableAsync<UserDto, OrderDto, ProductDto>(sp);
```

## Caching
Caching is configured per-call via StoredProcedureParametersBuilder and resolved using:
- Frozen (in-process, immutable)
- InMemory (in-process, expirable)
- Redis (distributed, optional)

Example:
```csharp
var sp = new StoredProcedureParametersBuilder("Users", "usp_Get_All_Users", 250)
    .AddFrozenCache("all_users_frozen")
    .Build();
var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp);
```

For Redis, configure CaeriusNetBuilder.WithRedis(...) or WithAspireRedis(...).
