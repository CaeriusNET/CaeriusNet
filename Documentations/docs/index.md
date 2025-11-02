---
layout: home

hero:
  name: "Caerius.NET"
  text: "SQL Server Stored Procedures to C# DTOs — fast, simple, reliable"
  tagline: Stored Procedure ➜ DTO in microseconds, with compile‑time safety.
  image:
    alt: Caerius Logo
  actions:
    - theme: brand
      text: What is Caerius.NET?
      link: /quickstart/what-is-caeriusnet
    - theme: alt
      text: Quickstart
      link: /quickstart/getting-started
    - theme: alt
      text: Caching
      link: /documentation/cache
    - theme: alt
      text: GitHub
      link: https://github.com/CaeriusNET/CaeriusNet

features:
  - icon: 🛠️
    title: Two stacks, one pipeline
    details: Write your data access where it belongs <b>T/SQL</b> Stored Procedures in SQL Server and <b>C# DTOs</b> in your app. Caerius.NET binds them with minimal API and DI.
  - icon: 🚀
    title: Mapping in µseconds
    details: Ordinal, allocation‑aware mapping — no reflection on the hot path, pre‑sized collections, pooling, and <code>SequentialAccess</code> readers.
  - icon: 💪
    title: Heavy inputs, one call
    details: Pass thousands of IDs or GUIDs via TVP. Combine parameters + TVP, return multiple result sets, and keep latency predictable.
  - icon: 🔄
    title: Async‑only I/O
    details: All database calls are asynchronous by design for throughput and thread‑pool health.
  - icon: 🧊
    title: Caching when it counts
    details: Enable per‑call caching Frozen (immutable), In‑Memory (TTL), or Redis (distributed) — pick the right layer for your workload.
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