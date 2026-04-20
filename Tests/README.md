# CaeriusNet Tests

## Overview

Three test projects cover unit behavior, source generator verification, and end-to-end integration tests against a real
SQL Server instance.

## Test Projects

| Project                     | Type        | Count | Framework              |
|-----------------------------|-------------|-------|------------------------|
| CaeriusNet.Tests            | Unit        | ~142  | xUnit                  |
| CaeriusNet.Generator.Tests  | Generator   | ~135  | xUnit + Roslyn Testing |
| CaeriusNet.IntegrationTests | Integration | ~48   | xUnit + Testcontainers |

## Prerequisites

### Unit and generator tests

- .NET 10 SDK
- No external dependencies

### Integration tests

- .NET 10 SDK
- **Docker Desktop** running
- First run downloads `mcr.microsoft.com/mssql/server:2022-latest` (~1.5 GB)

Testcontainers starts a SQL Server 2022 container automatically.

#### Container reuse

`SqlServerFixture` uses `.WithReuse(true)` so the SQL Server container can stay warm between local test runs.

To enable reuse:

1. Set `TESTCONTAINERS_REUSE_ENABLE=true`
2. Or create `~/.testcontainers.properties` with `testcontainers.reuse.enable=true`

The `.devcontainer/` setup configures this automatically.

## Running Tests

### All tests except integration

```bash
dotnet test CaeriusNet.slnx --configuration Release --filter "FullyQualifiedName!~CaeriusNet.IntegrationTests"
```

### Unit tests only

```bash
dotnet test Tests/CaeriusNet.Tests --configuration Release
```

### Generator tests only

```bash
dotnet test Tests/CaeriusNet.Generator.Tests --configuration Release
```

### Integration tests

```bash
dotnet test Tests/CaeriusNet.IntegrationTests --configuration Release
```

### All tests

```bash
dotnet test CaeriusNet.slnx --configuration Release
```

## Test Categories

### Unit tests (`CaeriusNet.Tests`)

| Category  | What is tested                                                                    |
|-----------|-----------------------------------------------------------------------------------|
| Builders  | `StoredProcedureParametersBuilder` fluent API, validation, and cache policy setup |
| Caches    | `FrozenCacheManager`, `InMemoryCacheManager`, and `RedisCacheManager` behavior    |
| Factories | `CaeriusNetDbContext` creation and `CaeriusNetTransaction` state transitions      |
| Logging   | `LogMessages` smoke tests and `LoggerProvider` integration                        |
| Helpers   | `EmptyCollections` and `SearchValues` validation helpers                          |

### Generator tests (`CaeriusNet.Generator.Tests`)

| Category    | What is tested                                                                                  |
|-------------|-------------------------------------------------------------------------------------------------|
| DTO         | Type mappings, nullable members, special conversions, `Half`, ordinal constants, and attributes |
| TVP         | Supported SQL types, multi-column generation, nullable members, and `class` generation          |
| Diagnostics | `CAERIUS001` through `CAERIUS005` diagnostics                                                   |
| Helpers     | `TypeDetector` and `ColumnExtractor` helper logic                                               |

These tests use `CSharpSourceGeneratorTest` to compile source in memory and verify generated output.

### Integration tests (`CaeriusNet.IntegrationTests`)

| Category     | What is tested                                                                   |
|--------------|----------------------------------------------------------------------------------|
| Reads        | Single-row, collection, multi-result-set (2 to 4), and concurrent read scenarios |
| Writes       | Non-query, scalar, and fire-and-forget execution                                 |
| TVP          | Table-valued parameter round-trips and TVP usage inside transactions             |
| Transactions | Commit, rollback, multi-command flows, and poison-state handling                 |

The SQL schema is created from `Tests/CaeriusNet.IntegrationTests/Sql/schema.sql`.

## CI/CD

Unit and generator tests run in the main `CI` workflow on every push and pull request. Integration tests run in the
separate `Integration Tests` workflow with Docker support.
