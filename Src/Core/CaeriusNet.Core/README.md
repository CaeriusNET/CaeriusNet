# CaeriusNet.Co# CaeriusNet.Core — Architectural Overview

Applies to: .NET 9 | C# | SQL Server
CaeriusNet.Core is the foundational layer of the CaeriusNET stack. It defines the core abstractions, DI extensions, and
data-access contracts necessary to build high-performance, SQL Server–centric applications on .NET 9. The Core package
is optimized for SQL Server and intentionally does not target cross-database compatibility. It embraces modern .NET
practices including dependency injection, configuration, and source generation to produce predictable, maintainable, and
efficient data-access patterns.

## Key scenarios

- Register SQL Server infrastructure in a .NET 9 application via a single, opinionated extension method.
- Standardize data-access contracts through a core application interface to align with Microsoft.Extensions.*.
- Enable high-throughput database operations leveraging Microsoft.Data.SqlClient and Table-Valued Parameters (TVPs).
- Integrate optional caching (in-memory, Redis) to reduce load and improve read performance.
- Employ source generators to streamline DTO shaping and SQL typing consistency from C# symbols.

## Design goals

- SQL Server first: All data operations, typing, and mappers are designed for SQL Server.
- Predictable composition: Use a single entrypoint to wire up the CaeriusNET infrastructure into IServiceCollection.
- Performance-centric: Prefer zero-alloc patterns, explicit typing, TVPs, and async I/O via Microsoft.Data.SqlClient.
- Developer productivity: Provide opinionated utilities and source generation to reduce boilerplate and runtime errors.

## Architecture

- Dependency Injection Extension
    - A single extension method on IServiceCollection provides a guided, consistent registration experience for the
      CaeriusNET runtime and database context. This method returns a specialized application contract that aligns to the
      DI collection.

- Application Contract
    - A minimal ICaeriusNetApplication interface models the application composition surface, deferring implementation
      details while maintaining DI semantics.

- SQL Utilities
    - Utilities encapsulate SQL Server–specific conventions, including safe SqlCommand creation and async execution
      helpers tuned for stored procedures and parameterization.

- TVP Mapping
    - An ITvpMappercontract defines how to transform object graphs into DataTable instances to be consumed as SQL Server
      Table-Valued Parameters.

- Source Generation
    - Generators support DTO projection and SQL type inference from Roslyn symbols, enabling compile-time validation and
      eliminating a class of runtime mismatches.

- Caching and Configuration
    - Integration with Microsoft.Extensions.Configuration, MemoryCache, and Redis (StackExchange.Redis) allows
      architects to layer read caching strategies without leaking implementation details into the core domain.

## Namespaces and components

- CaeriusNet.Core.Extensions
    - ICaeriusNetApplication: Core application contract that follows IServiceCollection semantics for fluent service
      registration.
    - CaeriusNetApplication: DI extension surface to register the CaeriusNET infrastructure and concrete database
      context using a SQL Server connection string.

- CaeriusNet.Core.Mappers
    - ITvpMapper: Abstraction for materializing collections into DataTable instances for TVP scenarios.

- CaeriusNet.Utilities
    - SqlCommandUtility: Helper for building and executing SqlCommand instances against SQL Server with async patterns
      and strong parameterization.

- CaeriusNet.SourceGenerator
    - DtoSourceGenerator: Source generator that emits DTO-related code from Roslyn syntax and semantic analysis.
    - Utils.TypeDetector: Utility to map C# types to SQL Server types based on Roslyn symbols to maintain typing
      consistency.

## Package dependencies

- Microsoft.Data.SqlClient — SQL Server provider used for all database operations.
- System.Text.Json — High-performance serialization for payloads and DTO interactions.
- Microsoft.Extensions.Configuration.Json — JSON-based configuration for environment-based connection strings and
  options.
- Microsoft.Extensions.DependencyInjection — DI backbone for composition and lifetime management.
- Microsoft.Extensions.Caching.Memory — In-memory caching for low-latency lookups and throttling database access.
- StackExchange.Redis — Distributed caching for scale-out scenarios.

## Getting started

1. Add the Core package to your .NET 9 project.
2. Configure your SQL Server connection string via configuration (e.g., appsettings.json or environment variables).
3. Register CaeriusNET services during application startup using the registration extension.

``` csharp
// In Program.cs or composition root
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SqlServer");
// Registers CaeriusNET Core services and the SQL Server DbContext
builder.Services.AddCaeriusNet(connectionString);
```

After registration, your application can resolve the configured database context and any CaeriusNET services that rely
on SQL Server.

## Core concepts

- Application registration
    - The AddCaeriusNet(IServiceCollection, string) extension method centralizes SQL Server–specific initialization and
      returns ICaeriusNetApplication for fluent configuration patterns.

- Strongly typed SQL execution
    - SqlCommandUtility encourages parameterized, async-first execution, consistent with Microsoft guidelines for secure
      and performant data access.

- Table-Valued Parameters (TVPs)
    - Implement ITvpMapperto convert domain collections to DataTable instances for bulk operations via stored
      procedures.

- Compile-time safety with source generators
    - DTO generation and SQL type inference reduce runtime errors and maintain consistent contracts between C# and SQL
      Server schemas.

## Configuration and hosting

- Connection strings
    - Use Microsoft.Extensions.Configuration.Json to externalize connection strings and environment-specific overrides.

- Caching
    - Choose between MemoryCache for single-instance deployments or Redis via StackExchange.Redis for distributed
      scenarios.

- Telemetry and diagnostics
    - Integrate with your preferred logging and tracing infrastructure using Microsoft.Extensions.Logging for consistent
      observability.

## Performance guidance

- Prefer TVPs for bulk operations to minimize round-trips and reduce lock contention.
- Use async I/O for all database calls to improve scalability under load.
- Reuse parameter definitions and avoid dynamic SQL whenever possible; favor stored procedures with explicit types.
- Apply caching to shield hot paths and reduce repeat reads against the same keys.

## Security considerations

- Store connection strings securely (e.g., user secrets, Azure Key Vault, environment variables).
- Always use parameterized commands; do not concatenate user input into SQL.
- Restrict database permissions to least privilege required by the application.
- Validate and sanitize data before mapping to TVPs.

## Versioning and compatibility

- Target framework: net9.0
- Language version: C# (latest available in .NET 9 toolchain)
- Database provider: SQL Server only (via Microsoft.Data.SqlClient)
- Backward compatibility: The Core package maintains semantic versioning; breaking changes are documented in release
  notes.

## Thread safety

- DI registrations are designed to be safe for concurrent resolution when scoped or singleton lifetimes are used
  appropriately.
- SqlClient usage follows standard async patterns; ensure you do not share SqlCommand instances across threads and scope
  connections per operation or per scope as appropriate.

## Extensibility points

- Implement ITvpMapperfor custom collection-to-DataTable mappings.
- Extend source generation by participating in the DTO pipeline if your solution adds project-specific analyzers.
- Compose additional services by chaining ICaeriusNetApplication (which maintains IServiceCollection semantics) during
  startup.

## When to use CaeriusNet.Core

Use CaeriusNet.Core when:

- Your application targets .NET 9 and SQL Server exclusively.
- You need a streamlined, DI-first experience for data access with minimal boilerplate.
- You want predictable performance characteristics, with first-class support for TVPs, parameterized commands, and async
  patterns.
- You plan to leverage source generators for safer, faster DTO and type handling.