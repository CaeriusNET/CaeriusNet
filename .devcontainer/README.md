# CaeriusNet — Dev Container

A reproducible, batteries-included development environment for the **CaeriusNet**
repository. Open the repo in a Dev Containers-aware editor (VS Code, JetBrains
Rider, GitHub Codespaces) and you get the exact toolchain CI uses — no host-side
.NET install required.

## What's inside

| Layer | Provided by |
| ----- | ----------- |
| .NET 10 SDK (Bookworm) | `mcr.microsoft.com/devcontainers/dotnet:1-10.0-bookworm` |
| Docker (for Testcontainers MSSQL) | `docker-outside-of-docker` feature |
| GitHub CLI | `github-cli` feature |
| Node.js LTS | `node` feature |
| SQL Server 2022 image | Pre-pulled by `post-create.sh` |
| Roslyn / C# Dev Kit / SQL tools / GitHub Actions | VS Code extensions |
| Persistent NuGet cache | Named volume `caeriusnet-nuget` |

## Environment variables

| Variable | Value | Why |
| -------- | ----- | --- |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `1` | Quiet, GDPR-friendly. |
| `DOTNET_NOLOGO` | `1` | Cleaner CLI output. |
| `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` | `1` | Skip warm-up on first build. |
| `TESTCONTAINERS_RYUK_DISABLED` | `false` | Keep the reaper on; it cleans up on crash/cancel. |
| `TESTCONTAINERS_REUSE_ENABLE` | `true` | Allow tests to opt in to reusing containers. |
| `NUGET_PACKAGES` | `/home/vscode/.nuget/packages` | Mounted on a persistent volume. |

`~/.testcontainers.properties` is also seeded with `testcontainers.reuse.enable=true`
so opting in to reuse from C# (`.WithReuse(true)`) actually takes effect.

## Common workflows

```bash
# Restore + build + unit tests (fast, no Docker)
dotnet restore CaeriusNet.slnx
dotnet build   CaeriusNet.slnx -c Release
dotnet test    CaeriusNet.slnx -c Release \
  --filter "FullyQualifiedName!~CaeriusNet.IntegrationTests"

# Integration tests (Testcontainers MSSQL — first run downloads ~1.4 GB image)
dotnet test Tests/CaeriusNet.IntegrationTests/CaeriusNet.IntegrationTests.csproj -c Release

# Coverage with markdown summary
dotnet test CaeriusNet.slnx -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
dotnet tool install --global dotnet-reportgenerator-globaltool || true
reportgenerator -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:"MarkdownSummaryGithub;HtmlInline"
```

## JetBrains Rider

The container exposes `"jetbrains": { "backend": "Rider" }` so JetBrains Gateway
will boot a remote Rider backend automatically.

## Troubleshooting

- **`docker: command not found`** — re-run `bash .devcontainer/post-create.sh`
  after the Docker feature finishes initialising (rare race on first build).
- **Testcontainers fails to find Docker** — make sure your host Docker socket is
  mounted; the `docker-outside-of-docker` feature handles this on Linux/macOS.
- **NuGet cache feels slow** — the cache lives on a named volume (`caeriusnet-nuget`).
  Drop it (`docker volume rm caeriusnet-nuget`) to start fresh.
