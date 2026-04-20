# Contributing to CaeriusNet

Thanks for considering a contribution! CaeriusNet is a tightly-scoped, opinionated
micro-ORM for **C# 14 / .NET 10 / SQL Server**, focused on stored procedures, TVPs,
and transactions. This document explains how to set up, the conventions we follow,
and how to get a PR merged efficiently.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Quick start](#quick-start)
- [Project layout](#project-layout)
- [Branching & commits](#branching--commits)
- [Build, test & coverage](#build-test--coverage)
- [Coding conventions](#coding-conventions)
- [Pull request process](#pull-request-process)
- [Reporting bugs / requesting features](#reporting-bugs--requesting-features)
- [Security](#security)

## Code of Conduct

This project follows the [Contributor Covenant 2.1](./CODE_OF_CONDUCT.md). By
participating you agree to uphold it.

## Quick start

### Local

```bash
git clone https://github.com/CaeriusNET/CaeriusNet.git
cd CaeriusNet
dotnet restore CaeriusNet.slnx
dotnet build   CaeriusNet.slnx -c Release
dotnet test    CaeriusNet.slnx -c Release --filter "FullyQualifiedName!~CaeriusNet.IntegrationTests"
```

### Devcontainer (recommended)

Open the repository in VS Code or JetBrains Rider with the **Dev Containers**
extension. The container ships .NET 10, Docker-outside-of-Docker (so
Testcontainers works), the GitHub CLI and pre-pulls the SQL Server 2022 image.

```bash
# Inside the container — run the full integration suite
dotnet test Tests/CaeriusNet.IntegrationTests/CaeriusNet.IntegrationTests.csproj -c Release
```

## Project layout

```
Src/                       # Public runtime API (the NuGet package)
SourceGenerators/          # Roslyn source generators (DTO + TVP mappers, diagnostics)
Tests/CaeriusNet.Tests/                # Pure unit tests (no IO)
Tests/CaeriusNet.Generator.Tests/      # Source-generator emit & diagnostic tests
Tests/CaeriusNet.IntegrationTests/     # End-to-end tests (Testcontainers MSSQL)
Benchmark/                 # BenchmarkDotNet suites
Exemples/                  # Sample consumer apps
Documentations/            # MkDocs site (GitHub Pages)
.devcontainer/             # Reproducible dev environment
.github/                   # Workflows, templates, issue forms
```

## Branching & commits

- Base branch: **`main`**.
- Feature branches: **`feature/<short-kebab>`**.
- Fix branches: **`fix/<short-kebab>`**.
- Chore/docs branches: **`chore/<short-kebab>`** / **`docs/<short-kebab>`**.

We use [Conventional Commits](https://www.conventionalcommits.org/). Common types:

| Type       | When                                              |
| ---------- | ------------------------------------------------- |
| `feat`     | A new public API or behaviour                     |
| `fix`      | A bug fix                                         |
| `perf`     | A performance improvement (back it with a bench)  |
| `refactor` | Internal change with no external behaviour delta  |
| `test`     | Adding or improving tests                         |
| `docs`     | Documentation only                                |
| `chore`    | Tooling, CI, dependency updates                   |

If your change is breaking, append `!` (e.g. `feat(sproc)!:`) and mention the
migration path in the PR description.

## Build, test & coverage

| Command | Purpose |
| ------- | ------- |
| `dotnet build CaeriusNet.slnx -c Release -p:TreatWarningsAsErrors=true` | Mirrors CI. |
| `dotnet test CaeriusNet.slnx -c Release --filter "FullyQualifiedName!~IntegrationTests"` | Unit + generator tests (fast, no Docker). |
| `dotnet test Tests/CaeriusNet.IntegrationTests` | End-to-end tests; needs Docker. |
| `dotnet test --collect:"XPlat Code Coverage"` | Generates Cobertura coverage. |

Coverage is reported on every PR via the CI workflow. Aim to **never decrease**
line coverage on `Src/` and `SourceGenerators/`.

## Coding conventions

- **Target framework:** `net10.0` (no multi-target).
- **Language version:** `latest` (C# 14).
- **Nullable:** enabled everywhere.
- **Async only.** No sync over async; use `ConfigureAwait(false)` in library code.
- **Stored procedures only.** Do not introduce inline SQL or query builders.
- **No reflection on the hot path.** Reach for source generators or
  `Span<T>`/`Memory<T>` first.
- **`sealed` by default** for both DTOs and helper classes.
- **Single-responsibility files.** One public type per file when practical.
- **XML doc comments** on every public API (we ship as a NuGet package).
- **No new dependencies** without prior discussion in an issue.

Static analysis is enforced via `TreatWarningsAsErrors=true`. Prefer fixing the
root cause over a `#pragma warning disable`.

## Pull request process

1. **Open an issue first** for non-trivial changes so we can align on direction.
2. Use a focused branch with the prefix described above.
3. Keep PRs small and reviewable. Split refactors from features.
4. **CI must be green** (build, unit tests, CodeQL, dependency review).
5. Update [`CHANGELOG.md`](./CHANGELOG.md) under the `[Unreleased]` section.
6. Update README / docs when public behaviour changes.
7. At least **one approving review** is required before merge.
8. PRs are squash-merged onto `main` with a Conventional Commit title.

## Reporting bugs / requesting features

Use the [issue templates](.github/ISSUE_TEMPLATE/) — they ask for the minimum
context (target framework, SqlClient version, repro snippet) we need to triage
quickly.

## Security

Please **do not** report vulnerabilities via public issues. Follow the process
in [SECURITY.md](./SECURITY.md).
