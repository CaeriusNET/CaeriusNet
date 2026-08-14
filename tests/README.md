# CaeriusNet Tests

## Overview

Test projects cover unit behavior, analyzer diagnostics, source generator verification, packaging, and end-to-end
integration tests against a real SQL Server instance.

## Test Projects

| Project                     | Type        | Framework                 |
|-----------------------------|-------------|---------------------------|
| CaeriusNet.Tests            | Unit        | xUnit v3                  |
| CaeriusNet.Analyzer.Tests   | Analyzer    | xUnit v3 + Roslyn         |
| CaeriusNet.Generator.Tests  | Generator   | xUnit v3 + Roslyn         |
| CaeriusNet.IntegrationTests | Integration | xUnit v3 + Testcontainers |
| CaeriusNet.Packaging.Tests  | Packaging   | xUnit v3 + local NuGet    |

## Prerequisites

### Unit and generator tests

- .NET 10 SDK
- No external dependencies

### Integration tests

- .NET 10 SDK
- **Docker Desktop** running
- First run downloads `2022-CU26-ubuntu-22.04`,
  `2025-CU7-ubuntu-24.04` and the pinned Redis image.

The container suites are explicit. Set `CAERIUS_RUN_CONTAINERS=true`; when the variable is absent, those tests are
reported as skipped rather than silently starting infrastructure.

## Running Tests

### All tests except integration

```bash
dotnet test CaeriusNet.slnx --configuration Release --filter "FullyQualifiedName!~CaeriusNet.IntegrationTests"
```

### Unit tests only

```bash
dotnet test tests/CaeriusNet.Tests/CaeriusNet.Tests.csproj --configuration Release
```

### Generator tests only

```bash
dotnet test tests/CaeriusNet.Generator.Tests/CaeriusNet.Generator.Tests.csproj --configuration Release
```

### Analyzer tests only

```bash
dotnet test tests/CaeriusNet.Analyzer.Tests/CaeriusNet.Analyzer.Tests.csproj --configuration Release
```

### Integration tests

```bash
$env:CAERIUS_RUN_CONTAINERS = 'true'
dotnet test tests/CaeriusNet.IntegrationTests/CaeriusNet.IntegrationTests.csproj --configuration Release
```

### All tests

```bash
dotnet test CaeriusNet.slnx --configuration Release
```

## Test Categories

### Unit tests (`CaeriusNet.Tests`)

| Category          | What is tested                                                                                 |
|-------------------|------------------------------------------------------------------------------------------------|
| Runtime contracts | DI registration, command validation, cardinality, exceptions, transactions, and cache behavior |
| Cache acceptance  | Memory/Frozen cache policies and typed cache codec behavior                                    |

### Generator tests (`CaeriusNet.Generator.Tests`)

| Category             | What is tested                                                       |
|----------------------|----------------------------------------------------------------------|
| Contract generation  | DTO, TVP, AutoContracts emission and deterministic contract metadata |
| Incremental behavior | Source-generator caching and compilation step tracking               |

These tests use `CSharpSourceGeneratorTest` to compile source in memory and verify generated output.

### Analyzer tests (`CaeriusNet.Analyzer.Tests`)

| Category      | What is tested                                         |
|---------------|--------------------------------------------------------|
| Attributes    | `CAERIUS001` through `CAERIUS006` target diagnostics   |
| AutoContracts | `CAERIUS200` through `CAERIUS210` manifest diagnostics |

### Packaging tests (`CaeriusNet.Packaging.Tests`)

| Category      | What is tested                                                       |
|---------------|----------------------------------------------------------------------|
| AutoContracts | NuGet `buildTransitive`, `AdditionalFiles`, and non-versioned naming |

### Integration tests (`CaeriusNet.IntegrationTests`)

| Category   | What is tested                                                                              |
|------------|---------------------------------------------------------------------------------------------|
| SQL Server | Stored-procedure cardinality, outputs, TVPs, result-set contracts, and transaction behavior |
| Redis      | Read-through, corruption handling, versioned envelopes, and strict mutation behavior        |
| Aspire     | DI registration, keyed connections, health checks, and telemetry configuration              |
| Contracts  | `Pull`/`Verify` package behavior and manifest drift detection                               |

The active acceptance fixtures create their schema programmatically for each supported SQL Server image; there is no
separately maintained static schema file.

## CI/CD

Unit and generator tests run in the main `CI` workflow on every push and pull request. Integration tests run in the
separate `Integration Tests` workflow with Docker support.
