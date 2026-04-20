# Changelog

All notable changes to **CaeriusNet** are documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Source-generator artefacts (`CaeriusNet.Generator`) ship together with the runtime
> package and follow the same version cadence.

## [Unreleased]

### Added
- **Integration tests** (`Tests/CaeriusNet.IntegrationTests`) — real SQL Server 2022 backed
  by `Testcontainers.MsSql`, exercising the public surface end-to-end (stored procedures,
  TVPs, transactions, isolation-level pass-through). Gated behind a dedicated GitHub
  Actions workflow (`workflow_dispatch` + targeted PR paths) so the default CI stays
  Docker-free.
- **Devcontainer** (`.devcontainer/`) based on the official .NET 10 Bookworm image with
  `docker-outside-of-docker`, GitHub CLI and Node features. The post-create hook restores
  the solution and pre-pulls the SQL Server image to shorten the first run.

### Changed
- `ci.yml` now filters out the integration project (`FullyQualifiedName!~CaeriusNet.IntegrationTests`)
  to keep PR feedback fast.

## [10.3.0] — Audit follow-up wave

### Added
- **Cache invalidation API** (`ICaeriusNetCache`) — explicit, multi-provider façade that
  invalidates by key or pattern across in-memory / Redis / frozen layers without leaking
  the underlying `IMemoryCache` / `IDistributedCache` instances. Configurable in-memory
  size limit on `WithInMemoryCache(...)`.
- **Transactions API** (`ICaeriusNetTransaction`) — `await using` scope obtained via
  `ICaeriusNetDbContext.BeginTransactionAsync(IsolationLevel)`. Provides `CommitAsync`,
  `RollbackAsync`, dispose-time implicit rollback, command-slot serialisation,
  `Poison()` on SQL failure, and explicit rejection of nested
  `BeginTransactionAsync` (`NotSupportedException`). Cache writes are bypassed for any
  command issued inside a transaction (read-your-writes guarantee).
- **Source-generator diagnostics** `CAERIUS001`-`CAERIUS004` — actionable errors when a
  `[GenerateDto]` / `[GenerateTvp]` candidate violates the partial-record contract,
  declares unsupported member shapes, or omits the required TVP type name.

### Changed
- `WithSqlServer(...)` now validates the connection string at registration time so
  misconfiguration fails fast at `Build()`.
- `Microsoft.Data.SqlClient` upgraded to `7.0.0`.

## [10.2.0] — Benchmark hardening

### Added
- Professional **BenchmarkDotNet** suite with heavy-load profiles, GitHub-Markdown +
  JSON exporters, and a VitePress documentation pipeline that publishes the latest
  performance reports.

## [10.1.0] — Source generator GA

### Added
- `[GenerateDto]` / `[GenerateTvp]` source generators emit `ISpMapper<T>` /
  `ITvpMapper<T>` implementations for partial records, eliminating reflection-based
  mapping from the hot path.

## [10.0.0] — .NET 10 / C# 14 baseline

### Changed
- Target framework moved to `net10.0`, language version to `latest` (C# 14).
- All read APIs return `ValueTask<T>`; all write APIs return `ValueTask<int>` /
  `ValueTask<T?>`. Synchronous overloads removed.

[Unreleased]: https://github.com/CaeriusNET/CaeriusNet/compare/v10.3.0...HEAD
[10.3.0]: https://github.com/CaeriusNET/CaeriusNet/releases/tag/v10.3.0
[10.2.0]: https://github.com/CaeriusNET/CaeriusNet/releases/tag/v10.2.0
[10.1.0]: https://github.com/CaeriusNET/CaeriusNet/releases/tag/v10.1.0
[10.0.0]: https://github.com/CaeriusNET/CaeriusNet/releases/tag/v10.0.0
