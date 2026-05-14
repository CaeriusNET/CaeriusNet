---
prev:
  text: 'What is CaeriusNet?'
  link: '/quickstart/what-is-caeriusnet'
next:
  text: 'Usage overview'
  link: '/documentation/usage'
---

# Install and configure CaeriusNet

This quickstart shows how to install CaeriusNet, register it with dependency injection, and execute a SQL Server stored procedure.

## Prerequisites

- .NET 10 SDK or later.
- C# 14 or later.
- SQL Server 2019 or later, Azure SQL, or a compatible local development container.
- A SQL Server database that contains the stored procedure you want to call.
- Redis, if you want distributed caching.

## Install the NuGet package

Run this command from your project directory:

```bash
dotnet add package CaeriusNet
```

The package includes the runtime library, mapper source generators, and analyzer rules.

## Configure a connection string

Add a SQL Server connection string to `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=MyAppDb;User Id=sa;Password=Your_password123;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

::: tip Development certificates
For local Docker or developer SQL Server instances, `TrustServerCertificate=True` is common. For production, use a trusted certificate chain and remove this option.
:::

## Register CaeriusNet

Choose the registration style that matches your application.

::: code-group
```csharp [ASP.NET Core]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);

CaeriusNetBuilder
    .Create(builder)
    .WithSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .Build();

var app = builder.Build();
app.Run();
```

```csharp [.NET Aspire]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")
    .WithAspireRedis()
    .Build();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

```csharp [Worker or console app]
using CaeriusNet.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(configuration.GetConnectionString("Default")!)
    .Build();
```
:::

To enable Redis caching outside Aspire, add `WithRedis(...)` before `Build()`.

```csharp
CaeriusNetBuilder
    .Create(builder)
    .WithSqlServer(builder.Configuration.GetConnectionString("Default")!)
    .WithRedis(builder.Configuration.GetConnectionString("Redis"))
    .Build();
```

## Create a stored procedure

The example uses a stored procedure that returns users filtered by age.

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Age
    @Age INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Age
    FROM dbo.Users
    WHERE Age >= @Age
    ORDER BY Id;
END
GO
```

## Create a DTO

Use `[GenerateDto]` to let CaeriusNet create the mapper.

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

The DTO constructor parameters must match the stored procedure result columns by order and type.

## Call the stored procedure

Inject `ICaeriusNetDbContext` into a repository. Build the stored procedure parameters, then call a read method.

::: tip Parameter names
When you add parameters in C#, pass the parameter identifier without the SQL `@` prefix. Use `"Age"`, not `"@Age"`.
:::

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using System.Collections.ObjectModel;
using System.Data;

public sealed record UserRepository(ICaeriusNetDbContext DbContext)
{
    public async ValueTask<ReadOnlyCollection<UserDto>> GetUsersByAgeAsync(
        int age,
        CancellationToken cancellationToken)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", resultSetCapacity: 128)
            .AddParameter("Age", age, SqlDbType.Int)
            .Build();

        return await DbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
    }
}
```

Register the repository in dependency injection.

```csharp
builder.Services.AddScoped<UserRepository>();
```

## Add caching to a read

Caching is opt-in per call. Add a cache policy before you call `Build()`.

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 128)
    .AddParameter("Age", age, SqlDbType.Int)
    .AddInMemoryCache($"users:age:{age}", TimeSpan.FromMinutes(2))
    .Build();
```

On a cache hit, CaeriusNet returns the cached value and does not execute the stored procedure.

## Next steps

- [Usage overview](/documentation/usage)
- [Reading data](/documentation/reading-data)
- [Writing data](/documentation/writing-data)
- [DTO mapping](/documentation/dto-mapping)
- [Table-valued parameters](/documentation/tvp)
- [Multiple result sets](/documentation/multi-results)
