---
layout: home

hero:
  name: "CaeriusNet"
  text: "SQL Server Stored Procedures → C# DTOs"
  tagline: Compile-time safe mapping. No reflection. No DataTable. Just SQL Server + C#.
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
      text: Usage
      link: /documentation/usage
    - theme: alt
      text: GitHub
      link: https://github.com/CaeriusNET/CaeriusNet

features:
  - icon: 🛠️
    title: Stored Procedures only
    details: Data access lives in T/SQL Stored Procedures, results land in typed C# DTOs. CaeriusNet binds both sides with minimal boilerplate and DI-first design.
  - icon: 🚀
    title: Ordinal mapping — no reflection
    details: Allocation-aware, ordinal-indexed mapping via <code>SqlDataReader</code>. Pre-sized collections, <code>SequentialAccess</code> readers, and compile-time source generators.
  - icon: 💪
    title: TVP + multi-result in one call
    details: Stream thousands of IDs via Table-Valued Parameters, combine them with scalar parameters, and return up to 5 result sets in a single round-trip.
  - icon: 🔄
    title: Async-only I/O
    details: Every database call is async and <code>CancellationToken</code>-aware for throughput and thread-pool health.
  - icon: 🧊
    title: Per-call caching
    details: Opt into Frozen (immutable), InMemory (TTL), or Redis (distributed) caching directly on the parameters builder — zero overhead when not used.
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