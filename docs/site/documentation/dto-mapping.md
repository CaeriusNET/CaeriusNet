# DTO mapping

Rows are mapped by ordinal with the static `ISpMapper<T>` contract. CaeriusNet uses no runtime reflection to find constructors or writable properties.

```csharp
public sealed record Product(int Id, string Name, decimal Price) : ISpMapper<Product>
{
    public static Product MapFromDataReader(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetString(1),
        reader.GetDecimal(2));
}
```

Use `[GenerateDto]` on a supported partial DTO to generate the same static mapper. Generated DTOs also supply AOT-safe binary cache-codec support and a DI registration helper for the DTO plus supported result collections.

Column ordering and nullability are contract details. A conversion/ordinal failure raises `CaeriusMappingException`, which includes the result-set and row indices. Use `QuerySingle...` or `QueryFirst...` to express row count separately from mapping.

Manual types can be mapped normally, but must implement and register `ICaeriusCacheCodec<T>` before a Redis policy can cache their result shape.
