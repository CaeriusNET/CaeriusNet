# Getting Started
To successfully integrate Caerius.NET into your project, ensure the following prerequisites are met:
- C# .NET 8.0 or higher
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

```csharp
using CaeriusNet.Extensions;
using Microsoft.Extensions.Configuration;

var services = new ServiceCollection();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .Build();

services.AddSingleton<IConfiguration>(configuration);

var connectionString = configuration.GetConnectionString("Default");

services // [!code focus]
    .AddCaeriusNet(connectionString); // [!code focus]
    // ... additional DI configuration // [!code --]

var serviceProvider = services.BuildServiceProvider();
```

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

public sealed class TestRepository(ICaeriusDbContext DbContext)
    : ITestRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(int usersAge)
    {
        var spParams = new StoredProcedureParametersBuilder("dbo.sp_GetUser_By_Age")
            .AddParameter("Age", usersAge, SqlDbType.Int)
            .Build();

        var users = await DbContext.FirstQueryAsync<UserDto>(spParams);

        return users;
    }
}
```
```csharp [Record (Recommended)]
namespace TestProject.Repositories;

public sealed record TestRepository(ICaeriusDbContext DbContext)
    : ITestRepository
{
    public async Task<IEnumerable<UserDto>> GetUsersOlderThanAsync(int usersAge)
    {
        var spParams = new StoredProcedureParametersBuilder("dbo.sp_GetUser_By_Age")
            .AddParameter("Age", usersAge, SqlDbType.Int)
            .Build();

        var users = await DbContext.FirstQueryAsync<UserDto>(spParams);

        return users;
    }
}
```
:::

Refer to the provided DTO classes in your implementation:

::: code-group
```csharp [Class]
namespace TestProject.Models.Dtos;

public sealed class UserDto
    : ISpMapper<UserDto>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public byte Age { get; set; }

    public static UserDto MapFromReader(SqlDataReader reader)
    {
        return new UserDto
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Age = reader.GetByte(2)
        };
    }
}
```
```csharp [Record (Recommended)]
namespace TestProject.Models.Dtos;

public sealed record UserDto(int Id, string Name, byte Age)
    : ISpMapper<UserDto>
{
    public static UserDto MapFromReader(SqlDataReader reader)
    {
        return new UserDto(
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Age = reader.GetByte(2)
        );
    }
}
```
:::

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