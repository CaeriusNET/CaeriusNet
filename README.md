
# CaeriusNet

<p align="center">
    <img src="https://custom-icon-badges.demolab.com/badge/C%23-%23239120.svg?logoColor=white" alt="CSharp">
    <img src="https://img.shields.io/badge/.NET%2010-512BD4.svg?style=flat&logo=dotnet&logoColor=white" alt=".NET 10 +">
    <img src="https://img.shields.io/badge/SQL%20Server-CC2927.svg?style=flat&logo=microsoft-sql-server&logoColor=white" alt="SQL Server 2019 +">
</p>

## Overview

**CaeriusNet** is a high-performance framework developed in C# .NET 10 + and optimized for SQL Server 2019 +. It emphasizes code quality, maintainability, and scalability by providing advanced tools for executing stored procedures and managing caching mechanisms.

Official documentation: [CaeriusNet Documentation](https://caerius.net/)

Key Features:
- Advanced stored procedure management with support for Table-Valued Parameters (TVPs).
- Performance optimization for microsecond-level interactions.
- Extensible design based on a modular architecture.
- Easy integration via .NET dependency injection extensions.

---

## Performance Overview

|    | Feature              | Description                                                                                       |
|----|-----------------------|---------------------------------------------------------------------------------------------------|
| ⚙️  | **Micro ORM**          | Lightweight, based on stored procedures, with direct DTO mapping for maximum performance.        |
| 🔒 | **Cache Management**   | InMemory and Frozen cache systems for efficient data management.                                  |
| 🛠️  | **Dependency Injection** | Extensions to seamlessly integrate the framework via IServiceCollection.                         |
| 🔄 | **Modularity**         | Solution organized into multiple projects for enhanced reusability and scalability.              |
| ⚡ | **Performance**        | Optimized for rapid SQL Server interactions using Microsoft.Data.SqlClient.                      |

---

## Detailed Information

**CaeriusNet** aims to bridge the gap between high-level abstraction and low-level performance in database interactions. By leveraging advanced .NET features such as async/await, custom attributes, and dependency injection, the framework ensures:

- **Ease of Use**: Developers can easily define, execute, and retrieve results from stored procedures without boilerplate code.
- **Customizable Caching**: Supports multiple caching strategies (InMemory, Frozen) to optimize data retrieval and reduce redundant database queries.
- **Error Handling**: Provides comprehensive exception handling tailored to SQL operations, ensuring clear diagnostic messages and reduced debugging time.

The modular architecture allows you to extend functionality by creating additional projects or integrating third-party libraries. With built-in support for CI/CD via GitHub Actions, the framework encourages best practices in modern software development.

---

## Usage Examples

### Model Mapping (DTO)

1. Define a Data Transfer Object (DTO) implementing the `ISpMapper` interface :

```csharp
public sealed record UserModel(int Id, string Name, string Email) : ISpMapper<UserDto>
{
    public static UserModel MapFromDataReader(SqlDataReader reader)
    {
        return new UserModel(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2)
        );
    }
}
```

### Scenario 1: Single Value Retrieval

```csharp
var spParameters = new StoredProcedureParametersBuilder("dbo.usp_CreateNewAccount")
        .AddParameter("email", email, SqlDbType.VarChar) 
        .Build();

var dbResult = await dbContext.FirstQueryAsync(spParameters);
```

### Scenario 2: Caching Results

```csharp
var resultWithCache = await dbContext.FirstQueryAsync<UserDto>(
    new StoredProcedureParametersBuilder("dbo.usp_GetAllUsers")
        .WithCache("UserCache", TimeSpan.FromHours(1))
        .Build()
);
```

For more usage examples, refer to the [documentation](https://github.com/your-username/CaeriusNet/wiki).

## Advanced Features

- **Table-Valued Parameters (TVPs)**: Support for complex data structures in stored procedures.

### Scenario 4: Using Table-Valued Parameters

1. Define a custom Table-Valued Parameter (TVP) in SQL Server :

```sql
CREATE TYPE dbo.tvp_int AS TABLE (Id int NOT NULL);
```

2. Create a corresponding C# record for the TVP, implementing the `ITvpMapper` interface :

```csharp
public sealed record UsersIdsTvp(int Id) : ITvpMapper<UsersIdsTvp>
{
    public DataTable MapToDataTable(IEnumerable<UsersIdsTvp> items)
    {
        var dataTable = new DataTable("dbo.tvp_int");
        dataTable.Columns.Add("Id", typeof(int));

        foreach (var tvp in items) dataTable.Rows.Add(tvp.Id);

        return dataTable;
    }
}
```
---

## Cloning the Repository

1. Clone the repository:

```bash
git clone https://github.com/CaeriusNET/CaeriusNet.git
```

2. Navigate to the project directory:

```bash
cd CaeriusNet
```

3. Restore and build dependencies:

```bash
dotnet restore
dotnet build
```

---

## Contributing

Contributions are welcome!

- **Report Issues**: Use the [Issues](https://github.com/CaeriusNET/CaeriusNet/issues) section to document problems.
- **Submit Pull Requests**: Fork the repository, create a branch, and submit your changes.

### Contribution Workflow

1. **Fork the repository**.
2. **Clone your fork**.
3. **Create a branch**:

```bash
git checkout -b feature/feature-name
```

4. **Make your changes and test them**.
5. **Submit a Pull Request**.

---

## License

This project is licensed under the [MIT](https://choosealicense.com/licenses/mit/) License. See the [LICENSE](LICENSE.md) file for details.

---

## Acknowledgments

- **.NET 8 +** : For its language and runtime enhancements.
- - **Microsoft.Data.SqlClient** : For its performance and reliability in SQL Server connectivity.
- **SQL Server 2019 +** : For its power and flexibility in database management.
