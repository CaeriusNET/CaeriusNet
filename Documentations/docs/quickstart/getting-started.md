# Getting Started
To successfully integrate Caerius.NET into your project, ensure the following prerequisites are met:
- C# .NET 10.0 or higher
- SQL Server 2019 or higher

Once you have verified the prerequisites, you can proceed with installing Caerius.NET.

## Installation
Caerius.NET can be installed using the NuGet Package Manager available in your preferred IDE or directly via the .NET CLI:

```bash
dotnet add package Caerius.NET
```

Following installation, you are ready to incorporate Caerius.NET into your project.

## Configuration
Begin by configuring Caerius.NET within your C# project.  

Add the following entries to your `appsettings.json` file:

### Setting up the Connection String
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=sandbox;User Id=sa;Password=HashedPassword!;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;", // [!code focus]
    "Template": "Server=<>;Database=<>;User Id=<>;Password=<>;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;" // [!code focus]
  }
}
```

::: details

Utilize the Template connection string to establish a new database connection.  
Replace placeholders (<>) with actual values.  

Visit https://www.connectionstrings.com/sql-server/ to generate a connection string specific to your SQL Server setup.  

The parameters `Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=True;` are crucial for proper connection functionality due to `Microsoft.Data.SqlClient` requirements.
:::
This step requires the `Microsoft.Extensions.Configuration.Json` package to read the configuration settings from the `appsettings.json` file.

### Configuring Program.cs

In your `Program.cs` file, integrate Caerius.NET into your `ServiceCollection` using `Dependency Injection`:
::: code-group
```csharp [Manual]
using CaeriusNet.Builders;
using Microsoft.Extensions.Configuration;

var services = new ServiceCollection();

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

services.AddSingleton<IConfiguration>(configuration);

var connectionString = configuration.GetConnectionString("Default")!;

CaeriusNetBuilder
    .Create(services)
    .WithSqlServer(connectionString)
    // .WithRedis("localhost:6379") // optional distributed cache
    .Build();

var serviceProvider = services.BuildServiceProvider();
```
```csharp [Aspire]
using CaeriusNet.Builders;
using Microsoft.Extensions.Hosting;

var builder = new HostApplicationBuilder();

builder.AddServiceDefaults();

CaeriusNetBuilder.Create(builder)
	.WithAspireSqlServer("CaeriusNet")
	.WithAspireRedis() // Default is : redis
	.Build();

var app = builder.Build();

await app.RunAsync();
```
:::
## Utilizing Caerius.NET
Post-configuration, Caerius.NET is ready for use within your `Repository` classes.  

Employing clean architecture principles such as `SOLID`, the `Repository Pattern`, and `Dependency Injection` enhances the structure and maintainability of your code.

::: code-group
```csharp [Interface]
namespace TestProject.Repositories;

public interface ITestRepository
{
    Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(int usersAge);
}
```
```csharp [Class]
namespace TestProject.Repositories;

public sealed class TestRepository(ICaeriusNetDbContext DbContext)
    : ITestRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(int usersAge)
    {
        var spParams = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Age", 128)
            .AddParameter("Age", usersAge, SqlDbType.Int)
            .Build();

        var users = await DbContext.QueryAsIEnumerableAsync<UserDto>(spParams);

        return users ?? Array.Empty<UserDto>();
    }
}
```
```csharp [Record (Recommended)]
namespace TestProject.Repositories;

public sealed record TestRepository(ICaeriusNetDbContext DbContext)
    : ITestRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(int usersAge)
    {
        var spParams = new StoredProcedureParametersBuilder("dbo", "sp_GetUser_By_Age", 128)
            .AddParameter("Age", usersAge, SqlDbType.Int)
            .Build();

        var users = await DbContext.QueryAsIEnumerableAsync<UserDto>(spParams);

        return users ?? Array.Empty<UserDto>();
    }
}
```
:::

Refer to the provided DTO classes in your implementation:

```csharp
using CaeriusNet.Attributes.Dto;

namespace TestProject.Models.Dtos;

[GenerateDto]
public partial sealed record UserDto(int Id, string Name, byte Age);
```

Refer to the provided Stored Procedure:
::: code-group
```sql [Stored Procedure (simple)]
CREATE PROCEDURE dbo.sp_GetUser_By_Age
    @Age INT
AS
BEGIN
    SELECT Id, Name, Age
    FROM dbo.Users
    WHERE Age > @Age
END
```
```sql [Stored Procedure (transaction)]
CREATE PROCEDURE dbo.sp_GetUser_By_Age
    @Age INT
AS
BEGIN
    SET NOCOUNT ON
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED
    
    BEGIN TRY
        BEGIN TRANSACTION
            
        SELECT Id, Name, Age
        FROM dbo.Users
        WHERE Age >= @Age
        
        IF @@TRANCOUNT > 0
            COMMIT TRANSACTION
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
    END CATCH        
END
```