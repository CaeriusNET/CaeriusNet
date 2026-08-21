# CAERIUS200 — AutoContracts manifest is missing

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

AutoContracts is enabled, but the project does not have exactly one canonical v3 `caerius.contracts.json` manifest
available as an `AdditionalFiles` item during compilation.

## How to fix

Install `CaeriusNet.Contracts` and let its opt-in AutoContracts MSBuild integration create the manifest.

```bash
dotnet add package CaeriusNet.Contracts
```

Set `Pull` mode for an intentional refresh:

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>DefaultConnection</CaeriusContractsConnectionName>
</PropertyGroup>
```

Then build:

```bash
dotnet build
```

The default manifest path is `$(ProjectDir)caerius.contracts.json`. Override `CaeriusContractsOutput` only when the file intentionally lives elsewhere.

After the v3 manifest exists and is committed, use `Verify` mode in CI. Do not hand-create a v2 file: run a new `Pull`
so server features, zero-based ordinals, facets/collations and semantic hashes are complete.
