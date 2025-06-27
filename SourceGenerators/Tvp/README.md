# TVP Source Generator

# TVP Source Generator

## Overview

The TVP (Table-Valued Parameter) Source Generator automatically creates `ITvpMapper<T>` implementations for classes or
records marked with the `[GenerateTvp]` attribute. This enables seamless conversion of .NET objects into SQL Server
Table-Valued Parameters.

## Usage

1. Add the `[GenerateTvp]` attribute to your class or record:

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp]
public sealed partial record Product(int Id, string Name, decimal Price, string? Description);
```

2. The source generator will automatically create an implementation of `ITvpMapper<T>` that converts collections of your
   class/record into a DataTable:

```csharp
// Generated code
public sealed partial record Product : ITvpMapper<Product>
{
    public DataTable MapAsDataTable(IEnumerable<Product> items)
    {
        // Implementation provided by the source generator
    }

    public static SqlParameter CreateTvpParameter(string parameterName, IEnumerable<Product> items)
    {
        // Creates a SqlParameter with the TVP ready for use in a stored procedure
    }
}
```

3. Use the TVP mapper in your code:

```csharp
// Create a collection of objects
var products = new List<Product>
{
    new Product(1, "Widget", 10.99m, "A simple widget"),
    new Product(2, "Gadget", 24.99m, "An advanced gadget"),
    new Product(3, "Doohickey", 5.99m, null)
};

// Method 1: Create a DataTable for use as a TVP
var product = new Product();
var dataTable = product.MapAsDataTable(products);

// Method 2: Create a SqlParameter directly (easier)
var parameter = Product.CreateTvpParameter("@Products", products);

// Use the parameter in a SqlCommand
var command = new SqlCommand("dbo.ProcessProducts", connection);
command.CommandType = CommandType.StoredProcedure;
command.Parameters.Add(parameter);
```

## Requirements

- The class or record must be marked as `sealed` and `partial`
- Constructor parameters define the columns in the generated DataTable
- Supported types: primitive types, value types, strings, and other SQL Server compatible types
- Use nullable types to handle NULL values

## Features

- Automatic conversion of C# objects to SQL Server TVPs
- Handles nullable types correctly, converting null values to DBNull.Value
- Special handling for DateOnly, TimeOnly, enums, and byte arrays
- Configurable schema name and table type name through static properties
- Convenient helper method to create SqlParameters directly

## SQL Server Setup

To use the generated TVP, you need to create a corresponding table type in SQL Server:

```sql
CREATE TYPE dbo.ProductType AS TABLE (
    Id int NOT NULL,
    Name nvarchar(100) NOT NULL,
    Price decimal(18,2) NOT NULL,
    Description nvarchar(max) NULL
);
```

Then use it in a stored procedure:

```sql
CREATE PROCEDURE dbo.ProcessProducts
    @Products dbo.ProductType READONLY
AS
BEGIN
    -- Process the TVP data
    SELECT * FROM @Products;

    -- Or insert into a permanent table
    -- INSERT INTO Products(Id, Name, Price, Description)
    -- SELECT Id, Name, Price, Description FROM @Products;
END;
```

## Overview

The TVP (Table-Valued Parameter) Source Generator automatically creates `ITvpMapper<T>` implementations for classes or
records marked with the `[GenerateTvp]` attribute. This enables seamless conversion of .NET objects into SQL Server
Table-Valued Parameters.

## Usage

1. Add the `[GenerateTvp]` attribute to your class or record:

```csharp
using CaeriusNet.Attributes.Tvp;

[GenerateTvp]
public sealed partial record Product(int Id, string Name, decimal Price, string? Description);
```

2. The source generator will automatically create an implementation of `ITvpMapper<T>` that converts collections of your
   class/record into a DataTable:

```csharp
// Generated code
public sealed partial record Product : ITvpMapper<Product>
{
    public DataTable MapAsDataTable(IEnumerable<Product> items)
    {
        // Implementation provided by the source generator
    }
}
```

3. Use the TVP mapper in your code:

```csharp
var products = GetProducts(); // Your collection of products
var product = new Product(1, "Example", 10.99m, "Description");
var dataTable = product.MapAsDataTable(products);

// Use with SqlParameter
var parameter = new SqlParameter("@Products", SqlDbType.Structured)
{
    TypeName = "dbo.ProductTableType", // Your SQL Server TVP type name
    Value = dataTable
};
```

## Requirements

- The class or record must be marked as `sealed` and `partial`
- Constructor parameters define the columns in the generated DataTable
- Supported types: primitive types, value types, strings, and other SQL Server compatible types
- Optional: Use nullable types to handle NULL values

## Benefits

- Eliminates boilerplate code for DataTable creation
- Type-safe conversion from C# objects to SQL Server TVPs
- Compile-time generation ensures optimal performance
- No runtime reflection for better performance
