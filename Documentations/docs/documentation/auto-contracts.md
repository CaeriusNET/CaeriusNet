# AutoContracts

AutoContracts keeps stored procedure call contracts aligned with SQL Server metadata. It is designed as a read-only safety layer: CaeriusNet reads metadata from SQL Server, but does not create, update, or delete database objects and does not change application data.

## What it tracks

AutoContracts records the contract shape used by typed stored procedure calls:

- procedure identity
- parameter names, SQL types, direction, and nullability
- result-set columns when SQL Server can expose them through metadata
- the typed call surface represented by `StoredProcedureParametersBuilder<TProcedure>`

Generated contract artifacts are marked with `AutoContractGenerateDto`, `AutoContractGenerateTvp`, and
`AutoContractGenerateProcedure`. Use these exact AutoContracts marker names in public
documentation, samples, and generated API names.

## Manifest location

The snapshot is stored in `caerius.contracts.json`.

When AutoContracts runs through MSBuild, the default path is
`$(ProjectDir)caerius.contracts.json`. Override `CaeriusContractsOutput` when a project needs a
different path. The build targets pass that same path to the CLI and include it as an
`AdditionalFiles` item for the source generator.

When the CLI is run directly, `pull` writes to `--output` when provided, then `--manifest` when
provided, and otherwise `caerius.contracts.json` in the current working directory. `verify` reads
from `--manifest` when provided, then `--output` when provided, and otherwise
`caerius.contracts.json` in the current working directory.

## Build responsibilities

AutoContracts has two separate execution phases:

- The CLI/MSBuild phase connects to SQL Server in read-only mode, reads metadata, and writes or
  verifies `caerius.contracts.json`.
- The Roslyn source generator does not connect to SQL Server. It only reads the
  `caerius.contracts.json` file supplied through `AdditionalFiles`.

## Modes

`CaeriusContractsMode` controls how the snapshot is used:

| Mode | Behavior |
|---|---|
| `Pull` | Reads SQL Server metadata and refreshes `caerius.contracts.json`. Use this after an intentional stored procedure contract change. |
| `Verify` | Reads SQL Server metadata and compares it with `caerius.contracts.json`. Use this in validation workflows to detect drift. |
| `Off` | Skips AutoContracts work. Use this when contract checks are not needed for a run. |

## Recommended workflow

1. Change the stored procedure contract intentionally.
2. Run AutoContracts with `Pull`.
3. Review the updated `caerius.contracts.json`.
4. Commit the application change and the refreshed contract snapshot together.
5. Run AutoContracts with `Verify` in validation to catch unexpected SQL Server drift.

## Read-only guarantees

AutoContracts uses SQL Server metadata reads only. `Pull` updates `caerius.contracts.json` in the application workspace; it does not apply SQL changes back to the database. `Verify` reports mismatches and leaves both SQL Server and the snapshot unchanged.

Keep `Off` available for local scenarios where SQL Server is unavailable, but prefer `Verify` wherever a real database is part of the validation path.
