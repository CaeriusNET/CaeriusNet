---
layout: home

hero:
  name: "CaeriusNet"
  text: "SQL Server stored procedures to C# DTOs"
  tagline: A focused .NET data-access library for teams that standardize on SQL Server stored procedures, compile-time DTO mapping, and observable database calls.
  image:
    alt: CaeriusNet Logo
  actions:
    - theme: brand
      text: Start with the quickstart
      link: /quickstart/getting-started
    - theme: alt
      text: What is CaeriusNet?
      link: /quickstart/what-is-caeriusnet
    - theme: alt
      text: API reference
      link: /documentation/api
    - theme: alt
      text: GitHub
      link: https://github.com/CaeriusNET/CaeriusNet

features:
  - icon: 🛠️
    title: SQL Server stored procedures only
    details: CaeriusNet intentionally targets Microsoft SQL Server and stored procedures. It does not translate LINQ, generate SQL text, track entities, or abstract multiple database providers.
  - icon: ⚙️
    title: Compile-time mapper generation
    details: <code>[GenerateDto]</code> and <code>[GenerateTvp]</code> emit <code>ISpMapper&lt;T&gt;</code> and <code>ITvpMapper&lt;T&gt;</code> implementations at build time. Analyzer rules validate the <code>sealed partial</code> primary-constructor contract.
  - icon: 📖
    title: Ordinal data-reader mapping
    details: Result columns are read by index through <code>SqlDataReader</code> with <code>SequentialAccess</code>. Declared result-set capacity lets CaeriusNet pre-size collections for predictable materialization.
  - icon: 📦
    title: Table-valued parameters
    details: Pass large identifier or composite-key sets with SQL Server TVPs. Generated TVP mappers stream <code>SqlDataRecord</code> values without requiring callers to build <code>DataTable</code> instances.
  - icon: 🔀
    title: Multiple result sets
    details: Read up to five typed result sets from one stored-procedure execution. Return shapes are available for <code>IEnumerable</code>, <code>ReadOnlyCollection</code>, and <code>ImmutableArray</code>.
  - icon: 🧊
    title: Per-call caching
    details: Choose a cache tier on the parameters builder when a call is safe to cache Frozen for process-lifetime reference data, InMemory for expiring local entries, or Redis for shared distributed cache.
  - icon: 🔐
    title: Transaction scopes
    details: <code>BeginTransactionAsync</code> returns an explicit async scope with commit, rollback, poisoned-state handling, and tracing that groups child stored-procedure calls under a transaction activity.
  - icon: 📡
    title: Diagnostics and telemetry
    details: Built-in <code>ActivitySource</code>, <code>Meter</code>, and source-generated logging expose stored-procedure metadata, durations, failures, cache events, and optional parameter-value capture.
---

::: info Important scope
CaeriusNet is for SQL Server stored procedures. Parameter names passed to <code>StoredProcedureParametersBuilder</code> methods use the identifier only, without the SQL <code>@</code> prefix.
:::

::: tip Benchmarks
Benchmark pages document the suites and methodology. Treat any published numbers as environment-specific; SQL Server edition, hardware, network latency, schema, query plans, and payload shape can change results.
:::

## Documentation map

| Area | Start here | Use it for |
|---|---|---|
| Quickstart | [Installation & Setup](/quickstart/getting-started) | Install the package, register DI, and run the first stored-procedure query. |
| Guides | [Reading Data](/documentation/reading-data) | Learn DTO mapping, TVPs, result-set shapes, caching, transactions, logging, and Aspire integration. |
| Reference | [API Reference](/documentation/api) | Look up public builders, abstractions, commands, mapper contracts, and exception behavior. |
| Diagnostics | [Diagnostic rules](/diagnostics/) | Resolve analyzer errors and warnings emitted by the source generators. |
| Examples | [Examples overview](/examples/) | Follow end-to-end stored procedure, TVP, multi-result, and transaction scenarios. |
| Benchmarks | [Performance & Benchmarks](/benchmarks/) | Understand the benchmark suites, configuration, limitations, and local run commands. |

<style>
:root {
  --vp-home-hero-name-color: transparent;
  --vp-home-hero-name-background: -webkit-linear-gradient(120deg, #8f00fd 30%, #6ae8f4);

  --vp-home-hero-image-background-image: linear-gradient(-45deg, #8f00fd 50%, #6ae8f4 50%);
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
