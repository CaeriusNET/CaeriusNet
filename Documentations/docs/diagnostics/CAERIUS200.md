# CAERIUS200 — AutoContracts manifest is missing

**Severity**: Error
**Category**: CaeriusNet.AutoContracts

## Cause

AutoContracts is enabled, but the project does not have a `caerius.contracts.json` manifest available during compilation.

## How to fix

Install `CaeriusNet` and let its built-in AutoContracts MSBuild integration create the manifest.

```bash
dotnet add package CaeriusNet
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

After the manifest exists and is committed, use `Verify` mode in CI.
