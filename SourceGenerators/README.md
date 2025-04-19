# CaeriusNet.Generator

A source generator for CaeriusNet that automatically generates ISpMapper implementations for DTO classes.

## Overview

CaeriusNet.Generator is a Roslyn-based source generator that creates type-safe mappings between SQL Server stored
procedure results and .NET DTO classes. By simply decorating your DTO classes with the `[GenerateDto]` attribute, the
generator will automatically create an implementation of `ISpMapper<T>` for your class at compile time.

## Requirements

- Targets .NET Standard 2.0 (for compatibility)

## Usage

1. Add the NuGet package to your project:
   ```
   dotnet add package CaeriusNet.Generator
   ```

2. Define your DTO using the `[GenerateDto]` attribute:
   ```csharp
   using CaeriusNet.Attributes;

   [GenerateDto]
   public sealed partial record ProductDto(
       int ProductId,
       string Name,
       decimal Price,
       string? Description,
       DateTime? DiscontinuedAt
   );
   ```

3. The generator will automatically create the implementation of `ISpMapper<ProductDto>` at compile time.

## Requirements for DTOs

- Must be decorated with `[GenerateDto]` attribute
- Must be declared as `sealed partial` record
- Constructor parameters must match the order of columns in the SQL result set
- Only primitive types, common value types, strings, and byte arrays are supported
- Use nullable types for columns that may contain NULL values

## Generated Mapping

For each DTO, the generator creates an implementation of the `ISpMapper<T>` interface with a `MapFromDataReader` method
that maps the current row of a SqlDataReader to a new DTO instance.

Example generated code:

```csharp
public sealed partial record ProductDto : ISpMapper<ProductDto>
{
    public static ProductDto MapFromDataReader(SqlDataReader reader)
    {
        return new ProductDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4)
        );
    }
}
