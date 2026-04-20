# CaeriusNet

<p align="center">
  <a href="https://www.nuget.org/packages/CaeriusNet"><img src="https://img.shields.io/nuget/v/CaeriusNet?style=flat&logo=nuget" alt="NuGet version"></a>
  <a href="https://www.nuget.org/packages/CaeriusNet"><img src="https://img.shields.io/nuget/dt/CaeriusNet?style=flat" alt="NuGet downloads"></a>
  <img src="https://img.shields.io/badge/.NET%2010-512BD4.svg?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/C%23%2014-%23239120.svg?style=flat&logo=csharp&logoColor=white" alt="C# 14">
  <img src="https://img.shields.io/badge/SQL%20Server-CC2927.svg?style=flat&logo=microsoftsqlserver&logoColor=white" alt="SQL Server 2019+">
  <img src="https://img.shields.io/badge/license-MIT-blue.svg?style=flat" alt="MIT License">
</p>

## Overview

**CaeriusNet** is a high-performance micro-ORM for C# 14 and .NET 10, built exclusively around SQL Server stored procedures. It eliminates boilerplate data-access code through compile-time source generators, delivers multiple caching strategies out of the box, and supports multi-result-set queries — all in a single NuGet package.

---

## Why CaeriusNet?

- **Zero reflection at runtime** — DTO and TVP mappers are generated at compile time via Roslyn source generators.
- **Stored procedures first** — every query is a stored procedure call; no inline SQL, no query builders.
- **Three caching layers** — Frozen (immutable in-process), In-Memory (TTL), and Redis (distributed).
- **Multi-result sets** — retrieve up to 5 strongly-typed result sets in a single database round-trip.
- **TVP support** — pass structured data to SQL Server efficiently without any DataTable overhead.
- **Aspire-ready** — first-class integration with .NET Aspire for cloud-native scenarios.

---

## Installation

```bash
dotnet add package CaeriusNet
```

The source generator is bundled inside the package — no separate analyzer package is required.

---

## Quick Start

**1. Register the services**

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(connectionString)
    .Build();
```

**2. Define a DTO**

```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

**3. Execute a stored procedure**

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_GetUsers_By_Age", 250)
    .AddParameter("Age", age, SqlDbType.Int)
    .AddInMemoryCache("users:age:" + age, TimeSpan.FromMinutes(5))
    .Build();

var users = await dbContext.QueryAsReadOnlyCollectionAsync<UserDto>(sp, cancellationToken);
```

---

## Core Concepts

| Concept | Description |
|---------|-------------|
| **Stored procedures first** | All queries target SQL Server stored procedures. Inline SQL is not supported. |
| **Compile-time mapping** | `[GenerateDto]` and `[GenerateTvp]` generate `ISpMapper<T>` and `ITvpMapper<T>` at build time. |
| **TVP support** | Pass structured data as Table-Valued Parameters using `.AddTvpParameter()`. |
| **Caching layers** | Choose Frozen, In-Memory, or Redis caching per query — or combine them. |
| **Multi-result sets** | Call `QueryMultipleIEnumerableAsync<T1, T2, …>` to retrieve up to 5 result sets at once. |

---

## DTO Mapping

You can map result sets either automatically with source generation or manually by implementing the interface.

### Source-generated (recommended)

```csharp
[GenerateDto]
public sealed partial record UserDto(int Id, string Name, byte Age);
```

The `[GenerateDto]` attribute instructs the compiler to generate the `ISpMapper<UserDto>` implementation automatically.

### Manual implementation

```csharp
public sealed record UserDto(int Id, string Name, byte Age) : ISpMapper<UserDto>
{
    public static UserDto MapFromDataReader(SqlDataReader reader)
        => new(reader.GetInt32(0), reader.GetString(1), reader.GetByte(2));
}
```

---

## Table-Valued Parameters (TVP)

Use `[GenerateTvp]` to generate a TVP mapper that matches an existing SQL Server user-defined table type.

**1. Define the SQL type**

```sql
CREATE TYPE dbo.tvp_int AS TABLE (Id INT NOT NULL);
```

**2. Generate the C# mapper**

```csharp
[GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
public sealed partial record UsersIdsTvp(int Id);
```

**3. Use it in a query**

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers_By_Ids", 1024)
    .AddTvpParameter("Ids", tvpItems)
    .Build();

var users = await dbContext.QueryAsIEnumerableAsync<UserDto>(sp, cancellationToken);
```

---

## Multi-Result Sets

Retrieve multiple strongly-typed result sets in a single round-trip using `QueryMultipleIEnumerableAsync`.

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetDashboard", 512).Build();

var (users, orders) = await dbContext.QueryMultipleIEnumerableAsync<UserDto, OrderDto>(
    sp, cancellationToken);
```

You can query up to five result sets: `QueryMultipleIEnumerableAsync<T1, T2, T3, T4, T5>`.

---

## Caching

Add a cache policy directly on the `StoredProcedureParametersBuilder`. All three strategies are opt-in per query.

### Frozen cache — in-process, immutable

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_GetConfig", 64)
    .AddFrozenCache("config:all")
    .Build();
```

### In-Memory cache — in-process, TTL

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_GetUsers_By_Age", 250)
    .AddParameter("Age", age, SqlDbType.Int)
    .AddInMemoryCache("users:age:" + age, TimeSpan.FromMinutes(5))
    .Build();
```

### Redis cache — distributed, optional

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "usp_GetProducts", 512)
    .AddRedisCache("products:all", TimeSpan.FromMinutes(10))
    .Build();
```

To enable Redis, register it during setup:

```csharp
CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(connectionString)
    .WithRedis("localhost:6379")
    .Build();
```

---

## Write Operations

Use the execute methods for INSERT, UPDATE, DELETE, and scalar return values.

### Execute non-query

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_UpdateUser")
    .AddParameter("Guid", guid, SqlDbType.UniqueIdentifier)
    .AddParameter("Age", age, SqlDbType.TinyInt)
    .Build();

int rows = await dbContext.ExecuteNonQueryAsync(sp, cancellationToken);
```

### Execute scalar

```csharp
var sp = new StoredProcedureParametersBuilder("dbo", "sp_CreateUser")
    .AddParameter("Name", name, SqlDbType.NVarChar)
    .Build();

int newId = await dbContext.ExecuteScalarAsync<int>(sp, cancellationToken);
```

### Available methods

| Category | Method |
|----------|--------|
| **Read** | `FirstQueryAsync`, `QueryAsReadOnlyCollectionAsync`, `QueryAsIEnumerableAsync`, `QueryAsImmutableArrayAsync` |
| **Write** | `ExecuteNonQueryAsync`, `ExecuteAsync`, `ExecuteScalarAsync<T>` |
| **Multi-result** | `QueryMultipleIEnumerableAsync<T1,T2>` … `<T1,T2,T3,T4,T5>` |

---

## Aspire Integration

Use `WithAspireSqlServer` and `WithAspireRedis` when running under .NET Aspire.

```csharp
CaeriusNetBuilder.Create(builder)
    .WithAspireSqlServer("CaeriusNet")
    .WithAspireRedis()
    .Build();
```

---

## Prerequisites

| Requirement | Minimum version |
|-------------|----------------|
| .NET | 10 |
| C# | 14 |
| SQL Server | 2019 |

---

## Contributing

Contributions are welcome. Read these first:

- [`CONTRIBUTING.md`](CONTRIBUTING.md) — coding standards, branch model, conventional commits, build/test commands.
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) — Contributor Covenant 2.1.
- [`SECURITY.md`](SECURITY.md) — how to responsibly report vulnerabilities (private GitHub advisory).
- [`SUPPORT.md`](SUPPORT.md) — where to ask questions and get help.

Quick path:

1. Open an [issue](https://github.com/CaeriusNET/CaeriusNet/issues) to report a bug or propose a feature.
2. Fork the repository and create a branch from `main`.
3. Submit a [pull request](https://github.com/CaeriusNET/CaeriusNet/pulls) with a clear description of your change.

---

## License

CaeriusNet is licensed under the [MIT License](LICENSE).
