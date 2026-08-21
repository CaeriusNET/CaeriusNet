---
layout: home

hero:
  name: "CaeriusNet"
  text: "Stored-procedure data access for .NET 10"
  tagline: "Typed, observable SQL Server 2022/2025 calls for C# 14—without inline SQL or runtime mapper reflection."
  actions:
    - theme: brand
      text: Get started
      link: /quickstart/getting-started
    - theme: alt
      text: Migrate from v11
      link: https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md
    - theme: alt
      text: Package guide
      link: /documentation/usage

features:
  - title: Stored procedures only
    details: Every runtime call is CommandType.StoredProcedure. CaeriusNet has no inline SQL, LINQ provider, query builder or alternative database driver.
  - title: Explicit result contracts
    details: First, single, scalar, materialized and streamed APIs distinguish empty results, SQL NULL, multiple rows, mapping errors and conversion errors.
  - title: Generated mapping and TVPs
    details: DTO and TVP generators produce static mapper contracts. TVP enumeration reuses one SqlDataRecord and supports empty input.
  - title: Multiple result sets, 2–10
    details: Generated tuple APIs materialize each announced set as IEnumerable, ReadOnlyCollection or ImmutableArray and validate its count by default.
  - title: Typed cache architecture
    details: Memory cache provides TTL, sliding expiration and single-flight; Frozen cache swaps snapshots atomically; Redis adds versioned typed binary payloads.
  - title: DI and Aspire ready
    details: Register default or keyed databases through DI, then compose Aspire SQL Server, Redis, health checks and OpenTelemetry listeners separately.
---

::: info Version 12
CaeriusNet v12 is a major release for .NET 10, C# 14 and SQL Server 2022/2025 only. Its DI-first API replaces all v11 builder and global-state APIs. See the [migration guide](https://github.com/CaeriusNET/CaeriusNet/blob/main/docs/MIGRATION-v11-v12.md).
:::

## Start here

| Goal | Article |
| --- | --- |
| Install the runtime and make the first stored-procedure call | [Install and configure](/quickstart/getting-started) |
| Decide which packages are needed | [Usage overview](/documentation/usage) |
| Choose a row cardinality or collection shape | [Reading data](/documentation/reading-data) |
| Execute writes, scalars and outputs | [Writing data](/documentation/writing-data) |
| Pass table-valued parameters | [Table-valued parameters](/documentation/tvp) |
| Read 2–10 result sets | [Multiple result sets](/documentation/multi-results) |
| Cache safely | [Caching](/documentation/cache) |
| Use .NET Aspire | [Aspire integration](/documentation/aspire) |
| Discover AutoContracts | [AutoContracts](/documentation/auto-contracts) |

CaeriusNet operates at the stored-procedure boundary. It is not an entity tracker, migration tool, SQL generator or multi-provider ORM.
