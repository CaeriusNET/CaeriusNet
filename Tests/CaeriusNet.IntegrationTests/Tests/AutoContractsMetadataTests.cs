using DotNet.Testcontainers.Builders;

namespace CaeriusNet.IntegrationTests.Tests;

internal sealed class SqlServerAvailableFactAttribute : FactAttribute
{
    public SqlServerAvailableFactAttribute()
    {
        SqlServerFixture? fixture = null;
        try
        {
            fixture = new SqlServerFixture();
        }
        catch (DockerUnavailableException ex)
        {
            Skip = CreateSkipReason(ex);
        }
        catch (DockerConfigurationException ex)
        {
            Skip = CreateSkipReason(ex);
        }
        catch (TimeoutException ex)
        {
            Skip = CreateSkipReason(ex);
        }
        finally
        {
            if (fixture is not null)
                fixture.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    private static string CreateSkipReason(Exception exception)
    {
        return $"SQL Server AutoContracts metadata test skipped because SQL Server is unavailable: {exception.Message}";
    }
}

/// <summary>
///     Verifies the SQL-only metadata fixtures intended for AutoContracts discovery.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AutoContractsMetadataTests(SqlServerFixture fixture)
{
    [SqlServerAvailableFact]
    public async Task Sql_Metadata_Exposes_AutoContracts_Read_Shapes()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await ResetExecutionProbeAsync(connection);
        var executionCountBeforeMetadata = await ReadExecutionProbeCountAsync(connection);

        var searchColumns =
            await ReadFirstResultSetColumnsAsync(connection, "dbo.usp_AutoContracts_SearchWidgets");
        AssertSequentialOrdinals(searchColumns);
        Assert.Collection(searchColumns,
            column => AssertResultColumn(column, 1, "Id", "int", false),
            column => AssertResultColumn(column, 2, "Name", "nvarchar(100)", false),
            column => AssertResultColumn(column, 3, "Quantity", "int", false),
            column => AssertResultColumn(column, 4, "CreatedAt", "datetime2(3)", false));

        var byIdColumns =
            await ReadFirstResultSetColumnsAsync(connection, "dbo.usp_AutoContracts_GetWidgetById");
        AssertSequentialOrdinals(byIdColumns);
        Assert.Collection(byIdColumns,
            column => AssertResultColumn(column, 1, "Id", "int", false),
            column => AssertResultColumn(column, 2, "Name", "nvarchar(100)", false),
            column => AssertResultColumn(column, 3, "Quantity", "int", false),
            column => AssertResultColumn(column, 4, "CreatedAt", "datetime2(3)", false));

        var batchColumns =
            await ReadFirstResultSetColumnsAsync(connection, "dbo.usp_AutoContracts_PreviewWidgetBatch");
        AssertSequentialOrdinals(batchColumns);
        Assert.Collection(batchColumns,
            column => AssertResultColumn(column, 1, "ExternalId", "uniqueidentifier", false),
            column => AssertResultColumn(column, 2, "Name", "nvarchar(100)", false),
            column => AssertResultColumn(column, 3, "Quantity", "int", false),
            column => AssertResultColumn(column, 4, "UnitPrice", "decimal(18,4)", false),
            column => AssertResultColumn(column, 5, "EffectiveDate", "date", true),
            column => AssertResultColumn(column, 6, "ChangedAt", "datetimeoffset(3)", true));

        var quoteColumns =
            await ReadFirstResultSetColumnsAsync(connection, "dbo.usp_AutoContracts_QuoteWidget");
        AssertSequentialOrdinals(quoteColumns);
        Assert.Collection(quoteColumns,
            column => AssertResultColumn(column, 1, "Name", "nvarchar(75)", true),
            column => AssertResultColumn(column, 2, "Quantity", "int", true),
            column => AssertResultColumn(column, 3, "UnitPrice", "decimal(18,4)", true),
            column => AssertResultColumn(column, 4, "RequestedAt", "datetime2(2)", true),
            column => AssertResultColumn(column, 5, "QuoteTotal", "decimal(19,4)", true));

        var searchParameters =
            await ReadProcedureParametersAsync(connection, "dbo.usp_AutoContracts_SearchWidgets");
        Assert.Collection(searchParameters,
            parameter => AssertParameter(parameter, 1, "@NamePrefix", "sys", "nvarchar", false, false, false, 200),
            parameter => AssertParameter(parameter, 2, "@MinimumQuantity", "sys", "int", false, false, false),
            parameter =>
                AssertParameter(parameter, 3, "@CreatedAfter", "sys", "datetime2", false, false, false, scale: 3));

        var byIdParameters =
            await ReadProcedureParametersAsync(connection, "dbo.usp_AutoContracts_GetWidgetById");
        Assert.Collection(byIdParameters,
            parameter => AssertParameter(parameter, 1, "@Id", "sys", "int", false, false, false),
            parameter => AssertParameter(parameter, 2, "@IncludeArchived", "sys", "bit", false, false, false));

        var batchParameters =
            await ReadProcedureParametersAsync(connection, "dbo.usp_AutoContracts_PreviewWidgetBatch");
        var itemsParameter = Assert.Single(batchParameters);
        AssertParameter(itemsParameter, 1, "@Items", "dbo", "AutoContractsWidgetTvp", false, true, true);

        var quoteParameters =
            await ReadProcedureParametersAsync(connection, "dbo.usp_AutoContracts_QuoteWidget");
        Assert.Collection(quoteParameters,
            parameter => AssertParameter(parameter, 1, "@Name", "sys", "nvarchar", false, false, false, 150),
            parameter => AssertParameter(parameter, 2, "@Quantity", "sys", "int", false, false, false),
            parameter => AssertParameter(parameter, 3, "@UnitPrice", "sys", "decimal", false, false, false,
                precision: 18, scale: 4),
            parameter =>
                AssertParameter(parameter, 4, "@RequestedAt", "sys", "datetime2", false, false, false, scale: 2),
            parameter => AssertParameter(parameter, 5, "@QuoteTotal", "sys", "decimal", true, false, false,
                precision: 19, scale: 4));

        var tableTypeColumns = await ReadTableTypeColumnsAsync(connection, "dbo", "AutoContractsWidgetTvp");
        Assert.Collection(tableTypeColumns,
            column => AssertTableTypeColumn(column, 1, "ExternalId", "uniqueidentifier", false),
            column => AssertTableTypeColumn(column, 2, "Name", "nvarchar", false, 200),
            column => AssertTableTypeColumn(column, 3, "Quantity", "int", false),
            column => AssertTableTypeColumn(column, 4, "UnitPrice", "decimal", false, precision: 18, scale: 4),
            column => AssertTableTypeColumn(column, 5, "EffectiveDate", "date", true),
            column => AssertTableTypeColumn(column, 6, "ChangedAt", "datetimeoffset", true, scale: 3));

        var executionCountAfterMetadata = await ReadExecutionProbeCountAsync(connection);
        Assert.Equal(executionCountBeforeMetadata, executionCountAfterMetadata);
    }

    private static async Task<ImmutableArray<ResultSetColumn>> ReadFirstResultSetColumnsAsync(
        SqlConnection connection,
        string procedureName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT column_ordinal,
                                     name,
                                     system_type_name,
                                     is_nullable,
                                     error_number,
                                     error_message
                              FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(@ProcedureName), 0)
                              WHERE is_hidden = 0 OR error_number IS NOT NULL
                              ORDER BY column_ordinal;
                              """;
        command.Parameters.Add(new SqlParameter("@ProcedureName", SqlDbType.NVarChar, 776)
        {
            Value = procedureName
        });

        var columns = ImmutableArray.CreateBuilder<ResultSetColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(4))
                throw new InvalidOperationException(reader.GetString(5));

            columns.Add(new ResultSetColumn(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)));
        }

        return columns.ToImmutable();
    }

    private static async Task<ImmutableArray<ProcedureParameter>> ReadProcedureParametersAsync(
        SqlConnection connection,
        string procedureName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT p.parameter_id,
                                     p.name,
                                     SCHEMA_NAME(t.schema_id) AS TypeSchemaName,
                                     t.name AS TypeName,
                                     p.max_length,
                                     p.precision,
                                     p.scale,
                                     p.is_output,
                                     p.is_readonly,
                                     t.is_table_type
                              FROM sys.parameters AS p
                              INNER JOIN sys.types AS t
                                  ON p.user_type_id = t.user_type_id
                              WHERE p.object_id = OBJECT_ID(@ProcedureName)
                              ORDER BY p.parameter_id;
                              """;
        command.Parameters.Add(new SqlParameter("@ProcedureName", SqlDbType.NVarChar, 776)
        {
            Value = procedureName
        });

        var parameters = ImmutableArray.CreateBuilder<ProcedureParameter>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            parameters.Add(new ProcedureParameter(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt16(4),
                reader.GetByte(5),
                reader.GetByte(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.GetBoolean(9)));

        return parameters.ToImmutable();
    }

    private static async Task<ImmutableArray<TableTypeColumn>> ReadTableTypeColumnsAsync(
        SqlConnection connection,
        string schemaName,
        string typeName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT c.column_id,
                                     c.name,
                                     st.name AS TypeName,
                                     c.max_length,
                                     c.precision,
                                     c.scale,
                                     c.is_nullable
                              FROM sys.table_types AS tt
                              INNER JOIN sys.columns AS c
                                  ON tt.type_table_object_id = c.object_id
                              INNER JOIN sys.types AS st
                                  ON c.user_type_id = st.user_type_id
                              WHERE SCHEMA_NAME(tt.schema_id) = @SchemaName
                                AND tt.name = @TypeName
                              ORDER BY c.column_id;
                              """;
        command.Parameters.Add(new SqlParameter("@SchemaName", SqlDbType.NVarChar, 128)
        {
            Value = schemaName
        });
        command.Parameters.Add(new SqlParameter("@TypeName", SqlDbType.NVarChar, 128)
        {
            Value = typeName
        });

        var columns = ImmutableArray.CreateBuilder<TableTypeColumn>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(new TableTypeColumn(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt16(3),
                reader.GetByte(4),
                reader.GetByte(5),
                reader.GetBoolean(6)));

        return columns.ToImmutable();
    }

    private static async Task ResetExecutionProbeAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE dbo.AutoContractsExecutionProbe
                              SET ExecutionCount = 0;
                              """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadExecutionProbeCountAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ExecutionCount FROM dbo.AutoContractsExecutionProbe;";
        var value = await command.ExecuteScalarAsync();
        return Assert.IsType<int>(value);
    }

    private static void AssertSequentialOrdinals(ImmutableArray<ResultSetColumn> columns)
    {
        Assert.Equal(Enumerable.Range(1, columns.Length), columns.Select(column => column.Ordinal));
    }

    private static void AssertResultColumn(
        ResultSetColumn column,
        int ordinal,
        string name,
        string systemTypeName,
        bool isNullable)
    {
        Assert.Equal(ordinal, column.Ordinal);
        Assert.Equal(name, column.Name);
        Assert.Equal(systemTypeName, column.SystemTypeName);
        Assert.Equal(isNullable, column.IsNullable);
    }

    private static void AssertParameter(
        ProcedureParameter parameter,
        int ordinal,
        string name,
        string typeSchemaName,
        string typeName,
        bool isOutput,
        bool isReadonly,
        bool isTableType,
        short? maxLength = null,
        byte? precision = null,
        byte? scale = null)
    {
        Assert.Equal(ordinal, parameter.Ordinal);
        Assert.Equal(name, parameter.Name);
        Assert.Equal(typeSchemaName, parameter.TypeSchemaName);
        Assert.Equal(typeName, parameter.TypeName);
        Assert.Equal(isOutput, parameter.IsOutput);
        Assert.Equal(isReadonly, parameter.IsReadonly);
        Assert.Equal(isTableType, parameter.IsTableType);
        if (maxLength is not null)
            Assert.Equal(maxLength.Value, parameter.MaxLength);
        if (precision is not null)
            Assert.Equal(precision.Value, parameter.Precision);
        if (scale is not null)
            Assert.Equal(scale.Value, parameter.Scale);
    }

    private static void AssertTableTypeColumn(
        TableTypeColumn column,
        int ordinal,
        string name,
        string typeName,
        bool isNullable,
        short? maxLength = null,
        byte? precision = null,
        byte? scale = null)
    {
        Assert.Equal(ordinal, column.Ordinal);
        Assert.Equal(name, column.Name);
        Assert.Equal(typeName, column.TypeName);
        Assert.Equal(isNullable, column.IsNullable);
        if (maxLength is not null)
            Assert.Equal(maxLength.Value, column.MaxLength);
        if (precision is not null)
            Assert.Equal(precision.Value, column.Precision);
        if (scale is not null)
            Assert.Equal(scale.Value, column.Scale);
    }

    private sealed record ResultSetColumn(int Ordinal, string Name, string SystemTypeName, bool IsNullable);

    private sealed record ProcedureParameter(
        int Ordinal,
        string Name,
        string TypeSchemaName,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsOutput,
        bool IsReadonly,
        bool IsTableType);

    private sealed record TableTypeColumn(
        int Ordinal,
        string Name,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsNullable);
}
