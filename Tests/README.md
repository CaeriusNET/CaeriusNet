# CaeriusNet Tests

## Overview

Test projects cover unit behavior, analyzer diagnostics, source generator verification, packaging, and end-to-end
integration tests against a real SQL Server instance.

## Test Projects

| Project                     | Type        | Count | Framework              |
|-----------------------------|-------------|-------|------------------------|
| CaeriusNet.Tests            | Unit        | ~186  | xUnit                  |
| CaeriusNet.Analyzer.Tests   | Analyzer    | ~23   | xUnit + Roslyn         |
| CaeriusNet.Generator.Tests  | Generator   | ~149  | xUnit + Roslyn Testing |
| CaeriusNet.IntegrationTests | Integration | ~51   | xUnit + Testcontainers |
| CaeriusNet.Packaging.Tests  | Packaging   | ~4    | xUnit + local NuGet    |

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

### Analyzer tests only

```bash
dotnet test Tests/CaeriusNet.Analyzer.Tests --configuration Release
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
| Helpers     | `TypeDetector` and `ColumnExtractor` helper logic                                               |

These tests use `CSharpSourceGeneratorTest` to compile source in memory and verify generated output.

### Analyzer tests (`CaeriusNet.Analyzer.Tests`)

| Category      | What is tested                                      |
|---------------|-----------------------------------------------------|
| Attributes    | `CAERIUS001` through `CAERIUS006` target diagnostics |
| AutoContracts | `CAERIUS200` through `CAERIUS210` manifest diagnostics |

### Packaging tests (`CaeriusNet.Packaging.Tests`)

| Category      | What is tested                                                       |
|---------------|-----------------------------------------------------------------------|
| AutoContracts | NuGet `buildTransitive`, `AdditionalFiles`, and non-versioned naming |

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
