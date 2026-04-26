---
layout: home

hero:
  name: "CaeriusNet"
  text: "SQL Server Stored Procedures to C# DTOs"
  tagline: A focused, high-performance micro-ORM for C# 14 / .NET 10. Compile-time safe mapping, zero reflection, first-class observability.
  image:
    alt: CaeriusNet Logo
  actions:
    - theme: brand
      text: What is CaeriusNet?
      link: /quickstart/what-is-caeriusnet
    - theme: alt
      text: Get Started
      link: /quickstart/getting-started
    - theme: alt
      text: Examples
      link: /examples/
    - theme: alt
      text: GitHub
      link: https://github.com/CaeriusNET/CaeriusNet

features:
  - icon: 🛠️
    title: Stored Procedures, first and only
    details: Data access lives in T-SQL where it belongs. CaeriusNet binds Stored Procedure result sets to typed C# DTOs with a fluent builder, DI-first design, and a deliberately small API surface — no LINQ translation, no change tracking, no surprises.
  - icon: ⚡
    title: Compile-time mapping via Roslyn generators
    details: <code>[GenerateDto]</code> and <code>[GenerateTvp]</code> emit <code>ISpMapper&lt;T&gt;</code> / <code>ITvpMapper&lt;T&gt;</code> at build time. A dedicated analyzer enforces the <code>sealed partial</code> + primary-constructor shape, so contract drift surfaces in your IDE — never at runtime.
  - icon: 🚀
    title: Ordinal, allocation-aware reads
    details: Columns are read by index — never by name — through <code>SqlDataReader</code> with <code>SequentialAccess</code>. Result lists are pre-sized via your declared capacity, populated through <code>CollectionsMarshal</code>, and pooled with <code>ArrayPool&lt;T&gt;</code> for <code>ImmutableArray</code> outputs.
  - icon: 📦
    title: Table-Valued Parameters, zero-copy
    details: Pass thousands of IDs, GUIDs, or composite keys in a single round-trip. The generator emits a streaming <code>IEnumerable&lt;SqlDataRecord&gt;</code> that reuses a single record instance across rows — no <code>DataTable</code>, no boxing, no IN-list size limits.
  - icon: 🔀
    title: Multiple result sets in one call
    details: Return up to five typed result sets from a single Stored Procedure execution. Helpers come in three collection flavours — <code>IEnumerable</code>, <code>ReadOnlyCollection</code>, <code>ImmutableArray</code> — destructured straight into a tuple at the call site.
  - icon: 🧊
    title: Three-tier opt-in caching
    details: Pick the right tier per call directly on the parameters builder. <strong>Frozen</strong> for static reference data, <strong>InMemory</strong> for short-TTL hot paths, <strong>Redis</strong> for shared distributed cache — zero overhead when not used, zero allocation on cache hits.
  - icon: 🔐
    title: Atomic transactions with safety nets
    details: <code>BeginTransactionAsync</code> returns a thread-safe scope with a strict state machine (Active → Committed / RolledBack / Poisoned). Failures auto-poison and rollback on dispose; child SP spans nest under a parent <code>TX</code> activity for one cohesive trace per unit of work.
  - icon: 📡
    title: OpenTelemetry & Aspire, native
    details: A built-in <code>ActivitySource</code> and <code>Meter</code> emit OTel-compliant spans (<code>db.system</code>, <code>db.operation</code>, <code>db.statement</code>) and metrics (duration, executions, errors, cache lookups). <code>WithAspireSqlServer</code> / <code>WithAspireRedis</code> wire it into the Aspire dashboard in two lines.
---

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
