# What is CaeriusNet?

CaeriusNet is a focused data-access runtime for **C# 14**, **.NET 10**, and **SQL Server 2022/2025 stored procedures**. It lets an application build an explicit stored-procedure command, map rows to static DTO contracts, pass TVPs, use cache policies and execute transactions without a global runtime state.

## The boundary

CaeriusNet deliberately does not translate LINQ, track entities, create migrations, build ad hoc SQL, or abstract other database engines. Every production command is `CommandType.StoredProcedure`.

| Capability | v12 behavior |
| --- | --- |
| Registration | `AddCaeriusNet` through DI; default or keyed database contexts. |
| Mapping | Static `ISpMapper<T>` contracts, manually written or source generated. |
| TVPs | Static row writer contract that reuses a record and handles empty input. |
| Reads | First/single cardinality APIs, three materialized forms, or one explicit stream. |
| Result sets | 2–10 generated tuple arities with exact result-set validation by default. |
| Caching | Memory, Frozen snapshot and typed Redis policies without global state. |
| Transactions | One connection, one transaction, no concurrent commands, deferred invalidations. |
| Observability | BCL `ActivitySource` and `Meter`, with stable SQL semantic conventions. |

## A typical call

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_Customer_Get")
    .AddParameter("Id", customerId, SqlDbType.Int)
    .Build();

var customer = await database.QuerySingleOrDefaultAsync<Customer>(command, ct);
```

The chosen method is part of the contract: `QuerySingleOrDefaultAsync` permits zero or one row; `QuerySingleAsync` requires exactly one. `QueryAsync<T>` is materialized even though it returns `IEnumerable<T>`; `StreamAsync<T>` is the explicit reader-backed option.

Use CaeriusNet when stored procedures are already your stable data-access boundary. Use a different tool if you need database portability, query translation or entity state tracking.

Next: [Install and configure](/quickstart/getting-started).
