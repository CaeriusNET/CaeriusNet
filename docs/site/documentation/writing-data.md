# Writing data and scalars

Use an immutable `StoredProcedureCommand` for every write. `ExecuteAsync` is an awaited no-business-result command; it is not fire-and-forget. Use `ExecuteNonQueryAsync` when affected-row count is meaningful.

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_User_UpdateName")
    .AddParameter("Id", id, SqlDbType.Int)
    .AddParameter("Name", name, SqlDbType.NVarChar, size: 200)
    .Invalidates("users")
    .Build();

var affected = await database.ExecuteNonQueryAsync(command, ct);
```

Declared invalidations are run only after a successful standalone write. In a transaction they are deferred until commit and discarded by rollback.

## Scalars

```csharp
var id = await database.ExecuteScalarAsync<int>(command, ct);
var optionalId = await database.ExecuteScalarOrDefaultAsync<int>(command, ct);
```

`ExecuteScalarAsync<T>` rejects a missing scalar row, SQL `NULL`, and incompatible conversion. `ExecuteScalarOrDefaultAsync<T>` returns `default` for an absent or null scalar but still rejects invalid conversions.

## Output and return parameters

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Order_Create")
    .AddParameter("CustomerId", customerId, SqlDbType.Int)
    .AddOutputParameter("OrderId", SqlDbType.Int)
    .AddInputOutputParameter("Reference", reference, SqlDbType.NVarChar, size: 64)
    .AddReturnValue()
    .Build();

var result = await database.ExecuteWithOutputsAsync(command, ct);
var orderId = result.Output.Get<int>("OrderId");
```

Outputs are collected only after the reader has been consumed. AutoContracts emits typed output records for contracts declaring output, input/output or return parameters.

SQL Server failures become `CaeriusNetSqlException`; contract and conversion failures become `CaeriusContractException`. Cancellation remains `OperationCanceledException`.
