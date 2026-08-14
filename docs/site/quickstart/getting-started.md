---
prev:
  text: 'What is CaeriusNet?'
  link: '/quickstart/what-is-caeriusnet'
next:
  text: 'Usage overview'
  link: '/documentation/usage'
---

# Install and configure CaeriusNet

This quickstart creates a .NET 10 / C# 14 client for a SQL Server 2022 or 2025 stored procedure. CaeriusNet calls only stored procedures; inline SQL is not a runtime feature.

## Install

```bash
dotnet add package CaeriusNet --version 12.0.0
```

`CaeriusNet.Redis`, `CaeriusNet.Aspire` and `CaeriusNet.Contracts` are optional packages. Install them only for their corresponding integration.

## Register the database

```csharp
using CaeriusNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCaeriusNet(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Orders")!));
```

For more than one database, give each additional registration a key and request it with the standard keyed-service mechanism:

```csharp
builder.Services.AddCaeriusNet("reporting", options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Reporting")!));

public sealed class Reports(
    [FromKeyedServices("reporting")] ICaeriusNetDbContext reporting)
{
}
```

## Create the stored procedure

```sql
CREATE PROCEDURE dbo.usp_User_ByMinimumAge
    @Age int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Name, Age
    FROM dbo.Users
    WHERE Age >= @Age
    ORDER BY Id;
END
```

`SET NOCOUNT ON` prevents unintended row-count messages from affecting a multiple-result-set contract.

## Map a row and call the procedure

Use the generator with `[GenerateDto]`, or implement the static mapper contract explicitly:

```csharp
using CaeriusNet;
using CaeriusNet.Abstractions;
using CaeriusNet.Commands;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.ObjectModel;

public sealed record User(int Id, string Name, byte Age) : ISpMapper<User>
{
    public static User MapFromDataReader(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetString(1), reader.GetByte(2));
}

public sealed class UserRepository(ICaeriusNetDbContext database)
{
    public ValueTask<ReadOnlyCollection<User>> GetAsync(byte age, CancellationToken ct)
    {
        var command = new StoredProcedureCommandBuilder("dbo", "usp_User_ByMinimumAge")
            .AddParameter("Age", age, SqlDbType.TinyInt)
            .Build();

        return database.QueryReadOnlyCollectionAsync<User>(command, cancellationToken: ct);
    }
}
```

Parameter names may be passed with or without one `@`; use the same declared `SqlDbType` and supply size, precision or scale where the SQL contract needs it. Command validation happens before the connection opens.

## Add a local cache

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_User_ByMinimumAge")
    .AddParameter("Age", age, SqlDbType.TinyInt)
    .UseInMemoryCache(TimeSpan.FromMinutes(2), slidingExpiration: true)
    .Build();
```

Cache identity comes from the stored procedure contract, result shape and canonical input values. Do not invent a cache-key string at each call site.

## Next steps

- [Choose a result contract](/documentation/reading-data)
- [Pass table-valued parameters](/documentation/tvp)
- [Read multiple result sets](/documentation/multi-results)
- [Use Redis caching](/documentation/cache)
- [Configure Aspire](/documentation/aspire)
- [Migrate an existing v11 application](https://github.com/CaeriusNET/CaeriusNet/blob/main/MIGRATION-v11-v12.md)
