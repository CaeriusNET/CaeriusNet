---
prev:
  text: 'What is CaeriusNet?'
  link: '/quickstart/what-is-caeriusnet'
next:
  text: 'DTO Mapping'
  link: '/documentation/dto-mapping'
---

# Installation & Setup

This page walks through installing the package, configuring CaeriusNet for your hosting model, and running your first query end-to-end.

::: info Scope
CaeriusNet targets Microsoft SQL Server stored procedures. The quickstart assumes the SQL object already exists or can be created in your database.
:::

## Prerequisites

- **.NET 10.0 SDK** or higher
- **C# 14** language version (default for .NET 10 projects)
- **SQL Server 2019** or higher (Developer / Standard / Enterprise / Express / Azure SQL)
- *(Optional)* **Redis** for distributed caching
- *(Optional)* **.NET Aspire 10+** for cloud-native development

## 1. Install the package

```bash
dotnet add package CaeriusNet
```

The package ships the runtime library, the Roslyn source generators, and the analyzer in one bundle — no additional packages are required.

## 2. Configure the connection string

Add your SQL Server connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=MyAppDb;User Id=sa;Password=YourPassword!;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;"
  }
}
```

::: details Connection string options
The flags `Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;` are recommended by `Microsoft.Data.SqlClient`.

For Azure SQL, dev containers, or Docker, see [connectionstrings.com/sql-server](https://www.connectionstrings.com/sql-server/) for ready-made templates.
:::

## 3. Register CaeriusNet

Pick the host model that matches your project. All three flavours produce the same registration — only the entry point differs.

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
builder.AddServiceDefaults();   // Aspire ServiceDefaults wires OpenTelemetry

CaeriusNetBuilder
    .Create(builder)
    .WithAspireSqlServer("sqlserver")  // matches AppHost AddSqlServer name
    .WithAspireRedis()                  // optional — defaults to "redis"
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

::: tip Aspire ServiceDefaults
If you use Aspire, register CaeriusNet's `ActivitySource` and `Meter` in your `ServiceDefaults` project so spans and metrics flow into the dashboard. See the [Aspire Integration](/documentation/aspire#tracing-telemetry) guide.
:::

## 4. Register your repositories

Inject `ICaeriusNetDbContext` into a repository and register the contract in DI:

```csharp
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

## 5. Your first query

### a. Define the Stored Procedure

```sql
CREATE PROCEDURE dbo.sp_GetUsers_By_Age
    @Age INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Age
    FROM   dbo.Users
    WHERE  Age >= @Age;
END
GO
```

### b. Define the DTO

```csharp
using CaeriusNet.Attributes.Dto;

[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

`[GenerateDto]` instructs the compile-time source generator to emit `ISpMapper<UserDto>.MapFromDataReader` for you. The DTO must be `sealed`, `partial`, and use a primary constructor — the analyzer enforces this at build time (see [Compiler Diagnostics](/documentation/diagnostics)).

### c. Implement the repository

::: tip Parameter naming
When adding parameters in C#, pass the stored-procedure parameter identifier without the SQL `@` prefix. For example, use `"Age"` with `AddParameter(...)`.
:::

```csharp
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using System.Data;

public sealed record UserRepository(ICaeriusNetDbContext DbContext)
    : IUserRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(
        int age, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Age", ResultSetCapacity: 128)
            .AddParameter("Age", age, SqlDbType.Int)
            .Build();

        return await DbContext.QueryAsIEnumerableAsync<UserDto>(sp, ct);
    }
}
```

### d. Inject and call

```csharp
public sealed class UserService(IUserRepository repository)
{
    public Task<IEnumerable<UserDto>> GetAdultsAsync(CancellationToken ct)
        => repository.GetUsersOlderThanAsync(18, ct);
}
```

That's it — you have a fully typed, instrumented, allocation-aware Stored Procedure call.

## What happens behind the scenes

1. The builder produces an immutable `StoredProcedureParameters` value.
2. `QueryAsIEnumerableAsync<UserDto>` opens a connection via `ICaeriusNetDbContext`, starts an OTel `Activity`, and executes the SP with `CommandBehavior.SequentialAccess`.
3. The compile-time-generated `UserDto.MapFromDataReader` reads each row by ordinal — no reflection, no name lookups.
4. Results are materialised into a pre-sized list (capacity = 128) and returned to the caller.
5. The `Activity` records duration, row count, and SP metadata; the `Meter` increments executions and observes the duration histogram.

## Next steps

- [DTO Mapping](/documentation/dto-mapping) — understand ordinal mapping and nullability.
- [Source Generators](/documentation/source-generators) — explore `[GenerateDto]` and `[GenerateTvp]` in depth.
- [Reading Data](/documentation/reading-data) — choose between `IEnumerable`, `ReadOnlyCollection`, and `ImmutableArray`.
- [Caching](/documentation/cache) — add Frozen / InMemory / Redis caching to any call.
- [Examples](/examples/) — end-to-end walkthroughs.
