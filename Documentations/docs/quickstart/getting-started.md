---
prev:
  text: 'What is CaeriusNet?'
  link: '/quickstart/what-is-caeriusnet'
next:
  text: 'DTO Mapping'
  link: '/documentation/dto-mapping'
---

# Installation & Setup

## Prerequisites

- .NET 10.0 SDK or higher
- SQL Server 2019 or higher
- C# 14 language version

## Install the package

```bash
dotnet add package CaeriusNet
```

## Configure the connection string

Add your SQL Server connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=MyAppDb;User Id=sa;Password=YourPassword!;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;"
  }
}
```

::: details Connection string parameters
The parameters `Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;` are required by `Microsoft.Data.SqlClient`.

Visit [connectionstrings.com/sql-server](https://www.connectionstrings.com/sql-server/) to generate a connection string for your environment.
:::

## Register CaeriusNet

::: code-group
```csharp [ASP.NET Core / Generic Host]
using CaeriusNet.Builders;

var builder = WebApplication.CreateBuilder(args);

CaeriusNetBuilder
    .Create(builder)
    .WithSqlServer(builder.Configuration.GetConnectionString("Default")!)
    // .WithRedis("localhost:6379")  // optional distributed cache
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
    .WithAspireRedis()   // optional — defaults to "redis"
    .Build();

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```
```csharp [Console / Worker Service]
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

## Register your repositories

Register your repositories in the DI container:

```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

## Your first query

### 1. Create a Stored Procedure

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Age
    @Age INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Age
    FROM dbo.Users
    WHERE Age >= @Age;
END
```

### 2. Define a DTO

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

### 3. Implement a repository

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using System.Data;

public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(
        int age, CancellationToken cancellationToken)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", 128)
            .AddParameter("Age", age, SqlDbType.Int)
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken) ?? [];
    }
}
```

### 4. Inject and call

```csharp
public sealed class UserService(IUserRepository repository)
{
    public Task<IEnumerable<UserDto>> GetAdultsAsync(CancellationToken ct)
        => repository.GetUsersOlderThanAsync(18, ct);
}
```

---

**Next:** [DTO Mapping](/documentation/dto-mapping) — learn how ordinal-based mapping works.