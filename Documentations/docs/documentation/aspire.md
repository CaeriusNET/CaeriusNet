# Aspire Integration

CaeriusNet provides first-class integration with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) through `WithAspireSqlServer` and `WithAspireRedis` builder methods. Aspire manages connection strings via its resource abstraction — CaeriusNet resolves them automatically.

## Prerequisites

- .NET 10 and a .NET Aspire AppHost project
- `CaeriusNet` NuGet package in your service project
- SQL Server and (optionally) Redis resources declared in AppHost

## AppHost configuration

In your Aspire AppHost project, declare SQL Server and Redis resources and pass their references to your service:

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sqlserver")
    .AddDatabase("MyAppDb");

var redis = builder.AddRedis("redis");

builder.AddProject<Projects.MyApp_Api>("api")
    .WithReference(sql)
    .WithReference(redis);

builder.Build().Run();
```

## Service project configuration

In your service's `Program.cs` or `Startup.cs`, use `WithAspireSqlServer` and `WithAspireRedis`. These methods resolve the connection string from Aspire's named connection:

::: code-group
```csharp [With Redis]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // Aspire service defaults

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")  // matches AppHost resource name
    .WithAspireRedis("redis")          // optional distributed cache
    .Build();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```
```csharp [SQL Server only]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .Build();

var app = builder.Build();
app.Run();
```
:::

## Console app / Worker Service pattern

For console apps or background workers using Aspire:

```csharp
using CaeriusNet.Builders;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis("redis")
    .Build();

var host = builder.Build();

// Inject ICaeriusNetDbContext via DI in your hosted services
host.Run();
```

## Manual setup (without Aspire)

If you are not using Aspire, use `WithSqlServer` and `WithRedis` with explicit connection strings:

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    .WithRedis("localhost:6379")  // optional
    .Build();
```

## Resource name matching

The string passed to `WithAspireSqlServer` and `WithAspireRedis` must match the resource name declared in the AppHost:

| AppHost declaration | Service builder call |
|---|---|
| `builder.AddSqlServer("sqlserver")` | `.WithAspireSqlServer("sqlserver")` |
| `builder.AddRedis("redis")` | `.WithAspireRedis("redis")` |

::: tip Default names
If you use the conventional names `"sqlserver"` and `"redis"` you can also use the parameter-less overloads:
```csharp
.WithAspireSqlServer()  // defaults to "sqlserver"
.WithAspireRedis()      // defaults to "redis"
```
:::

## Complete example

```csharp
// AppHost/Program.cs
var sql = builder.AddSqlServer("sqlserver").AddDatabase("CaeriusDb");
var redis = builder.AddRedis("redis");
builder.AddProject<Projects.Api>("api")
    .WithReference(sql)
    .WithReference(redis);
```

```csharp
// Api/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis("redis")
    .Build();

builder.Services.AddScoped<IUserRepository, UserRepository>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

```csharp
// Api/Repositories/UserRepository.cs
public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<IEnumerable<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "usp_Get_All_Users", 250)
            .AddRedisCache("users:all", TimeSpan.FromMinutes(5))
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct) ?? [];
    }
}
```

---

**Next:** [API Reference](/documentation/api) — full surface of all public types and methods.
