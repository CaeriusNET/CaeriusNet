# Changelog

All notable changes to **CaeriusNet** are documented in this file.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Source-generator artefacts (`CaeriusNet.Generator`) ship together with the runtime
> package and follow the same version cadence.

## [Unreleased]

### Added
- **Project governance** — `CONTRIBUTING.md`, `SECURITY.md`, `SUPPORT.md`,
  `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `.github/PULL_REQUEST_TEMPLATE.md`
  and structured issue templates (`bug_report.yml`, `feature_request.yml`).
- **Repo automation** — path-based labeller (`.github/labeler.yml` + workflow), stale issue
  reaper (`.github/workflows/stale.yml`) and OSSF Scorecard supply-chain audit
  (`.github/workflows/scorecard.yml`, SARIF upload to GitHub code-scanning).
- **Unit tests** — `EmptyCollectionsTests`, `CacheHelperTests`, `LoggerProviderTests`,
  `LogMessagesSmokeTests`, `NamespaceHelperTests`, `SqlMetaDataExpressionBuilderTests`.
  +30 assertions covering previously untested internal helpers and the
  `LoggerMessageGenerator` surface.
- **Integration tests** (`Tests/CaeriusNet.IntegrationTests`) — real SQL Server 2022 backed
  by `Testcontainers.MsSql`, exercising the public surface end-to-end (stored procedures,
  TVPs, transactions, isolation-level pass-through). Gated behind a dedicated GitHub
  Actions workflow (`workflow_dispatch` + targeted PR paths) so the default CI stays
  Docker-free. Now uses `.WithReuse(true)` for fast inner-loop runs in devcontainers.
- **Devcontainer** (`.devcontainer/`) — Testcontainers reuse env vars
  (`TESTCONTAINERS_REUSE_ENABLE`, `TESTCONTAINERS_RYUK_DISABLED=false`), persistent
  NuGet volume, expanded VS Code extensions, dedicated `README.md`.

### Changed
- **`ci.yml`** — modernised coverage pipeline: `actions/upload-artifact@v5`, ReportGenerator
  (`MarkdownSummaryGithub` + `HtmlInline_AzurePipelines` + `Cobertura`), `$GITHUB_STEP_SUMMARY`
  integration, sticky PR comment via `marocchino/sticky-pull-request-comment@v2`, HTML coverage
  artefact, `pull-requests: write` permission scope.
- **`integration-tests.yml`** — fixed Ryuk anti-pattern (`TESTCONTAINERS_RYUK_DISABLED`
  forced to `'false'` in CI to ensure orphaned containers are reaped on cancel/timeout);
  bumped `actions/upload-artifact@v4` → `@v5`.
- **`benchmark.yml`** — bumped `actions/upload-artifact@v4` → `@v5`.

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
