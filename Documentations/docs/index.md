---
layout: home

hero:
  name: "CaeriusNet"
  text: "SQL Server stored procedure data access for .NET"
  tagline: Build typed, observable, allocation-aware data access around SQL Server stored procedures, DTO mapping, table-valued parameters, caching, and transactions.
  image:
    alt: CaeriusNet logo
  actions:
    - theme: brand
      text: Get started
      link: /quickstart/getting-started
    - theme: alt
      text: Learn the concepts
      link: /quickstart/what-is-caeriusnet
    - theme: alt
      text: View examples
      link: /examples/
    - theme: alt
      text: API reference
      link: /documentation/api

features:
  - title: Stored procedures by design
    details: CaeriusNet targets SQL Server stored procedures. It does not translate LINQ, generate ad hoc SQL, track entities, or abstract multiple database providers.
  - title: Typed result mapping
    details: Map result sets to DTOs with the <code>ISpMapper&lt;T&gt;</code> contract. Use <code>[GenerateDto]</code> to remove repetitive mapper code while keeping ordinal mapping explicit.
  - title: Table-valued parameters
    details: Send large sets of IDs, GUIDs, and composite rows through SQL Server TVPs. Use <code>[GenerateTvp]</code> for strongly typed TVP rows.
  - title: Multiple result sets
    details: Read two to five typed result sets from one stored procedure call. Choose tuple return shapes based on <code>IEnumerable&lt;T&gt;</code>, <code>ReadOnlyCollection&lt;T&gt;</code>, or <code>ImmutableArray&lt;T&gt;</code>.
  - title: Per-call caching
    details: Add Frozen, in-memory, or Redis caching to individual read calls from the same parameters builder you use for stored procedure inputs.
  - title: Transactions
    details: Use explicit async transaction scopes with commit, rollback, automatic rollback on dispose, poisoned-state handling, and transaction-level tracing.
  - title: Observability
    details: Built-in tracing, metrics, and structured logging expose stored procedure names, durations, row counts, failures, cache hits, and transaction context.
  - title: Aspire-ready setup
    details: Register SQL Server, Redis, tracing, and metrics with .NET Aspire-friendly builder methods while keeping the same runtime API.
---

::: info Supported data-access model
CaeriusNet is for SQL Server stored procedures. It is not a LINQ provider, entity tracker, migration tool, or multi-provider ORM.
:::

## Start here

Use this documentation to learn and apply the NuGet package in production applications.

| Goal | Article |
|---|---|
| Understand the package and decide whether it fits your architecture | [What is CaeriusNet?](/quickstart/what-is-caeriusnet) |
| Install the package and run the first stored procedure | [Install and configure](/quickstart/getting-started) |
| Find the right guide for a task | [Usage overview](/documentation/usage) |
| Read rows from stored procedures | [Reading data](/documentation/reading-data) |
| Execute writes and scalar commands | [Writing data](/documentation/writing-data) |
| Send TVPs to SQL Server | [Table-valued parameters](/documentation/tvp) |
| Read multiple result sets from one call | [Multiple result sets](/documentation/multi-results) |
| Look up the public API | [API reference](/documentation/api) |

## Documentation structure

The documentation is organized by how you use the package:

1. **Get started**: Learn what CaeriusNet is, install the package, configure dependency injection, and run a query.
2. **Use CaeriusNet**: Read data, write data, map DTOs, pass TVPs, use multiple result sets, cache reads, and run transactions.
3. **Advanced scenarios**: Use source generators, AutoContracts, logging, OpenTelemetry, Aspire integration, and combined patterns.
4. **Reference**: Look up public APIs, diagnostics, best practices, and benchmark methodology.

## Recommended learning path

1. Read [What is CaeriusNet?](/quickstart/what-is-caeriusnet) to understand the data-access model.
2. Complete [Install and configure](/quickstart/getting-started) to register the package and execute your first stored procedure.
3. Choose a read shape in [Reading data](/documentation/reading-data).
4. Add DTO or TVP generation with [Source generators](/documentation/source-generators).
5. Review [Best practices](/documentation/best-practices) before you design production stored procedures.
6. Use [Examples](/examples/) for end-to-end scenarios.

<style>
:root {
  --vp-home-hero-name-color: transparent;
  --vp-home-hero-name-background: -webkit-linear-gradient(120deg, #0078d4 30%, #50e6ff);

  --vp-home-hero-image-background-image: linear-gradient(-45deg, #0078d4 50%, #50e6ff 50%);
  --vp-home-hero-image-filter: blur(44px);
}

@media (min-width: 640px) {
  :root {
    --vp-home-hero-image-filter: blur(56px);
  }
}

@media (min-width: 960px) {
  :root {
    --vp-home-hero-image-filter: blur(68px);
  }
}
</style>
