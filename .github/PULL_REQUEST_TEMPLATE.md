## Summary

<!-- One sentence describing the change. -->

## Motivation

<!-- Why is this change needed? Link to the issue it closes (Fixes #123). -->

## Type of change

- [ ] 🐛 Bug fix (non-breaking change which fixes an issue)
- [ ] ✨ New feature (non-breaking change which adds functionality)
- [ ] 💥 Breaking change (fix or feature that would break existing API contracts)
- [ ] 📚 Documentation only
- [ ] ♻️ Refactor / internal cleanup (no behaviour change)
- [ ] ⚡ Performance improvement (please attach BenchmarkDotNet diff)
- [ ] 🧪 Tests only
- [ ] 🔧 CI / tooling

## Checklist

- [ ] I have read [`CONTRIBUTING.md`](../CONTRIBUTING.md).
- [ ] My commits follow the [Conventional Commits](https://www.conventionalcommits.org/) specification.
- [ ] I have added or updated tests covering my change.
- [ ] `dotnet build CaeriusNet.slnx -c Release -p:TreatWarningsAsErrors=true` succeeds locally.
- [ ] `dotnet test CaeriusNet.slnx -c Release --filter "FullyQualifiedName!~IntegrationTests"` is green.
- [ ] `pwsh ./eng/ValidatePackage.ps1 -Configuration Release` succeeds for package/public API changes.
- [ ] `cd Documentations && npm install && npm run docs:build` succeeds for documentation changes (or `npm ci` when a lockfile exists).
- [ ] Docker-backed integration tests were run when storage, transactions, SQL, or TVP behaviour changed.
- [ ] I have updated [`CHANGELOG.md`](../CHANGELOG.md) under `[Unreleased]` if user-visible.
- [ ] I have updated public XML documentation comments for any public API change.
- [ ] No new dependencies introduced (or discussed in an issue first).

## Public-API impact

<!-- Describe any change to the public surface, including source-generator diagnostics. -->
<!-- If breaking, document the migration path here. -->

## Benchmark / performance notes

<!-- For perf changes, include the BenchmarkDotNet table and the workload context. -->

## Screenshots / logs (optional)

<!-- Drop relevant CI logs, profiler captures, or query plans here. -->
