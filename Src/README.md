# CaeriusNet Source Generator

CaeriusNet Source Generator is a powerful .NET tool that automatically generates DTO (Data Transfer Object) mappers and
TVP (Table-Valued Parameter) converters, streamlining the development process for .NET applications.

## Features

- **DTO Generation**: Automatically generates DTO mapping functionality for classes and records
- **SQL Type Mapping**: Built-in support for mapping .NET types to SQL Server data types
- **Nullable Support**: Handles nullable types appropriately in both C# and SQL contexts
- **Constructor & Property Support**: Works with both constructor parameters and public properties
- **Attribute-Based**: Simple to use with the `[GenerateDto]` attribute

## Installation

Install the package via NuGet:
# CaeriusNet Redis Integration Guide

CaeriusNet supports Redis caching with both traditional connection string and Aspire integration approaches.

## Traditional Redis Integration

To use Redis with a connection string:

```csharp
// In your startup or program file
services.AddCaeriusNet("your-sql-connection-string")
        .AddCaeriusRedisCache("your-redis-connection-string");

// Use Redis caching in stored procedure calls
var result = await dbContext.ExecuteStoredProcedureAsync<YourResultType>(
    new StoredProcedureParametersBuilder("YourStoredProcedureName")
        .AddParameter("@param1", value1, SqlDbType.NVarChar)
        .AddRedisCache("your-cache-key", TimeSpan.FromMinutes(10))
        .Build()
);