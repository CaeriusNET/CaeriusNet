# Migrating from CaeriusNet v11 to v12

CaeriusNet 12 is a deliberate major-version migration. It keeps the SQL Server stored-procedure model, but replaces v11's builder/global-state surface with dependency injection, immutable commands, explicit cache dependencies and generated contracts.

## Before you start

- Install SDK `10.0.400` (with `rollForward: latestPatch`) and use Roslyn 5.9 or later.
- Target `net10.0` and C# 14.
- Use SQL Server 2022 (major 16) or SQL Server 2025 (major 17).
- Treat production transport security as required: `Encrypt=Strict;TrustServerCertificate=False` and a server certificate trusted by the application host.
- Read the [API breaks report](API-BREAKS-v11-v12.md) before doing a solution-wide rename.

## 1. Move to DI and immutable commands

Register one database context at composition time, then inject `ICaeriusNetDbContext` (or use a keyed registration for a second database). Every runtime call remains a `CommandType.StoredProcedure`; v12 does not accept SQL text or provide a query builder.

```csharp
services.AddCaeriusNet(options =>
    options.UseSqlServer("Server=sql01;Database=Orders;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"));

var command = new StoredProcedureCommandBuilder("dbo", "usp_Order_Get")
    .AddParameter("Id", id, SqlDbType.Int)
    .Build();

var order = await database.QuerySingleAsync<OrderDto>(command, ct);
```

Commands are immutable after `Build()`. Declare parameter type and relevant facets explicitly; `AddWithValue` and inferred SQL types are not part of the v12 contract. Outputs are available only after the reader closes, through the output-aware execution APIs.

## 2. Replace cache-key invalidation with logical tags

v11-style literal cache-key invalidation cannot target generated cache keys reliably. Reads declare logical dependencies and durable writes invalidate the same tags:

```csharp
var read = new StoredProcedureCommandBuilder("dbo", "usp_Product_List")
    .DependsOn("product", "catalog")
    .UseRedisCache(TimeSpan.FromMinutes(2))
    .Build();

var write = new StoredProcedureCommandBuilder("dbo", "usp_Product_Rename")
    .AddParameter("Id", id, SqlDbType.Int)
    .AddParameter("Name", name, SqlDbType.NVarChar, size: 200)
    .Invalidates("product", "catalog")
    .Build();
```

Cache identities are centrally calculated as `caerius:v2:<sha256>` from length-prefixed binary fields; applications must not derive equivalent string keys. Redis stores tag generations with the payload and validates them atomically before a population is published. Configure a 1 MiB Redis payload ceiling by default and local size/entry-weight/population ceilings to match the host.

Invalidations run after durable SQL success or transaction commit with `CancellationToken.None`. A
`CaeriusCacheInvalidationException` means SQL has already committed (`SqlWasCommitted == true`): reconcile the cache or apply an idempotency protocol. Do not blindly repeat a non-idempotent stored procedure.

## 3. Make TVP lifetime explicit

The ambiguous raw `IEnumerable<T>` TVP form is gone. Choose the semantic lifetime at the call site:

```csharp
IReadOnlyCollection<OrderLine> replayable = lines.ToArray();

var replayableCommand = new StoredProcedureCommandBuilder("dbo", "usp_Order_AddLines")
    .AddTvpParameter("Lines", replayable)
    .Build();

var streamingCommand = new StoredProcedureCommandBuilder("dbo", "usp_Order_AddLines")
    .AddTvpParameter("Lines", static cancellationToken => ReadLines(cancellationToken))
    .Build();
```

`IReadOnlyCollection<T>` is replayable and can participate in a cacheable command. The factory streams without materialization and makes the command non-cacheable. Generated TVPs are limited to a sealed positional record class or readonly positional record struct. For manual TVPs, decorate each positional parameter with `SqlServerTvpColumnAttribute`, including SQL name, ordinal, type, nullability and applicable facets/collation; `CAERIUS007` identifies incomplete metadata.

## 4. Regenerate AutoContracts as manifest v3

v12 does not silently upgrade a v2 manifest. Install `CaeriusNet.Contracts`, use `Pull`, review/commit the canonical v3 manifest, and run `Verify` in CI:

```xml
<PropertyGroup>
  <CaeriusContractsMode>Pull</CaeriusContractsMode>
  <CaeriusContractsConnectionName>Orders</CaeriusContractsConnectionName>
</PropertyGroup>
```

```bash
dotnet build
```

Manifest v3 adds server major/features, continuous zero-based ordinals, exact facets/collations, schema-prefixed CLR names, and semantic hashes. `Pull` writes atomically only when discovery has no errors; `Verify` recalculates hashes, so hand edits with a stale hash fail. One optional override may replace result set 0 only if it was `None`/`Undetermined`, then append continuous sets 1–9. Dynamic SQL, `#temp` and later result sets require that explicit override or an exclusion.

Use named configuration, User Secrets or a connection-string environment variable. The MSBuild target no longer takes a raw connection string. The direct CLI keeps `--connection-string` solely as a deprecated troubleshooting option and redacts it from errors.

## 5. Review generator diagnostics and decimal mappings

Unsupported DTO/TVP types no longer become `sql_variant`. `CAERIUS005` is an error and no source is emitted. Decimal precision 29–38 now uses `SqlDecimal`; ensure handwritten mappers, output readers and custom codecs preserve that type. `CAERIUS008` is a generator safety boundary: it emits no source rather than leaking a `CS8785`; report a minimal valid reproduction if it remains after preceding diagnostics are fixed.

Review `CAERIUS206` (invalid direction/type pair), `CAERIUS208` (nullable generated result member) and `CAERIUS210` (dynamic/ambiguous procedure shape) as part of the contract change. Details are in the [diagnostics reference](site/documentation/diagnostics.md).

## 6. Opt in to SQL Server 2025 native types only when ready

Keep the default behavior for SQL Server 2022: JSON remains `nvarchar(max)`. For SQL Server 2025 native JSON/vector, pull a v3 manifest from major 17 and explicitly set:

```xml
<PropertyGroup>
  <CaeriusSqlServer2025Types>Enabled</CaeriusSqlServer2025Types>
</PropertyGroup>
```

Native JSON maps to `string`/`SqlDbType.Json`; float32 vector maps to `SqlVector<float>`/`SqlDbType.Vector`. v12 supports parameters, outputs and result columns only. JSON/vector TVP columns, spatial and CLR UDT mappings remain intentionally unsupported.

## 7. Move test and package automation to MTP

The test projects run natively on Microsoft.Testing.Platform. Replace VSTest logger/collector options with:

```bash
dotnet test --project tests/CaeriusNet.Tests/CaeriusNet.Tests.csproj --configuration Release --report-trx --coverage
dotnet test --solution CaeriusNet.slnx --configuration Release --report-trx --coverage
```

`Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage` own these reports; Coverlet is no longer required. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` remain temporarily for Rider compatibility.

Packages now contain only the analyzer and generator under `analyzers/dotnet/cs`, together with their Source Link PDBs. The runtime's multi-result API is compiled into `CaeriusNet.dll`; no consumer `MultiResultApiGenerator` is shipped. Package CI validates Source Link and bit-for-bit reproducible `.nupkg`/`.snupkg` output.

## Final checklist

- Re-run `Pull`; commit v3 manifest and any valid override.
- Convert each TVP call to replayable collection or streaming factory.
- Replace cache-key invalidation with matching `DependsOn`/`Invalidates` tags.
- Update connection strings for strict production TLS.
- Run locked restore, Release build, native MTP tests and package validation.
- Run the SQL Server 2022/2025 and Redis integration matrix before publishing.
