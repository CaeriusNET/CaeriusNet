# Reading data

The read method communicates the SQL contract. Choose cardinality before choosing a collection shape.

| Method | Contract |
| --- | --- |
| `QueryFirstAsync<T>` | At least one row; ignores later rows. |
| `QueryFirstOrDefaultAsync<T>` | Zero or more rows; returns `default` when empty. |
| `QuerySingleAsync<T>` | Exactly one row. |
| `QuerySingleOrDefaultAsync<T>` | Zero or one row. |
| `QueryAsync<T>` | All rows, materialized as `IEnumerable<T>`. |
| `QueryReadOnlyCollectionAsync<T>` | All rows in a read-only collection. |
| `QueryImmutableArrayAsync<T>` | All rows in an immutable array. |
| `StreamAsync<T>` | One result set, reader-backed async stream, no cache. |

```csharp
var command = new StoredProcedureCommandBuilder("dbo", "usp_User_Get")
    .AddParameter("Id", id, SqlDbType.Int)
    .Build();

var user = await database.QuerySingleOrDefaultAsync<User>(command, ct);
```

An empty required result or multiple rows for a single contract raises `CaeriusContractException`. A mapper failure identifies the result-set and row index through `CaeriusMappingException`.

## Materialized collections

Pass an optional capacity when the usual cardinality is known:

```csharp
var users = await database.QueryReadOnlyCollectionAsync<User>(command, capacity: 256, cancellationToken: ct);
```

`QueryAsync<T>` is materialized before it returns. Its `IEnumerable<T>` shape is for consumption ergonomics, not deferred database enumeration.

## Stream intentionally

```csharp
await foreach (var user in database.StreamAsync<User>(command, ct))
{
    await HandleAsync(user, ct);
}
```

The active async enumeration owns the connection and reader. Do not begin another operation on the same transaction during the enumeration; streaming is for a single result set, bypasses caches, and rejects a command declaring output, input/output or return parameters.

Next: [Writing data](/documentation/writing-data).
