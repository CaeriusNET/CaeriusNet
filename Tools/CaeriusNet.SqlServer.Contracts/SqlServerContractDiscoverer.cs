using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.SqlServer.Contracts;

internal static class SqlServerContractDiscoverer
{
    internal static async Task<ContractManifest> DiscoverAsync(
        CommandLineOptions options,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken = default)
    {
        var connectionString = ConnectionStringResolver.Resolve(options);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var database = await DiscoverDatabaseAsync(connection, cancellationToken).ConfigureAwait(false);
        var tableTypes =
            await DiscoverTableTypesAsync(connection, diagnostics, cancellationToken).ConfigureAwait(false);
        var procedures = await DiscoverProceduresAsync(connection, tableTypes, diagnostics, cancellationToken)
            .ConfigureAwait(false);

        return new ContractManifest(
            1,
            "CaeriusNet.Generated",
            database,
            tableTypes,
            procedures);
    }

    private static async Task<DatabaseInfo> DiscoverDatabaseAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  DB_NAME() AS database_name,
                                  CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS server_version,
                                  compatibility_level
                              FROM sys.databases
                              WHERE database_id = DB_ID();
                              """;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Could not read SQL Server database metadata.");

        return new DatabaseInfo(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetByte(2));
    }

    private static async Task<IReadOnlyList<TableTypeContract>> DiscoverTableTypesAsync(
        SqlConnection connection,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  s.name AS schema_name,
                                  tt.name AS table_type_name,
                                  tt.user_type_id,
                                  c.column_id,
                                  c.name AS column_name,
                                  COALESCE(sysTyp.name, userTyp.name) AS column_type_name,
                                  c.max_length,
                                  c.precision,
                                  c.scale,
                                  c.is_nullable
                              FROM sys.table_types AS tt
                              JOIN sys.columns AS c
                                  ON c.object_id = tt.type_table_object_id
                              JOIN sys.types AS userTyp
                                  ON userTyp.user_type_id = c.user_type_id
                              LEFT JOIN sys.types AS sysTyp
                                  ON sysTyp.system_type_id = c.system_type_id
                                  AND sysTyp.user_type_id = sysTyp.system_type_id
                              JOIN sys.schemas AS s
                                  ON s.schema_id = tt.schema_id
                              WHERE s.name NOT IN (
                                  N'sys',
                                  N'INFORMATION_SCHEMA',
                                  N'guest',
                                  N'db_owner',
                                  N'db_accessadmin',
                                  N'db_securityadmin',
                                  N'db_ddladmin',
                                  N'db_backupoperator',
                                  N'db_datareader',
                                  N'db_datawriter',
                                  N'db_denydatareader',
                                  N'db_denydatawriter'
                              )
                              ORDER BY
                                  s.name,
                                  tt.name,
                                  c.column_id;
                              """;

        var tableTypes = new Dictionary<string, MutableTableType>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var key = schema + "." + name;
            if (!tableTypes.TryGetValue(key, out var tableType))
            {
                tableType = new MutableTableType(schema, name, reader.GetInt32(2));
                tableTypes.Add(key, tableType);
            }

            var maxLength = reader.GetInt16(6);
            var precision = reader.GetByte(7);
            var scale = reader.GetByte(8);
            var sqlType = SqlServerTypeName.Format(reader.GetString(5), maxLength, precision, scale);
            var columnName = reader.GetString(4);
            var nullable = reader.GetBoolean(9);
            if (!SqlServerTypeMapper.IsSupported(sqlType))
                diagnostics.Error(
                    "CAERIUS203",
                    $"TVP column '{schema}.{name}.{columnName}' uses unsupported SQL type '{sqlType}'.");

            tableType.Columns.Add(new ColumnContract(
                reader.GetInt32(3),
                columnName,
                sqlType,
                SqlServerTypeMapper.GetClrType(sqlType, nullable),
                nullable,
                maxLength,
                precision,
                scale));
        }

        return tableTypes.Values
            .OrderBy(value => value.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(value =>
            {
                var columns = value.Columns.OrderBy(column => column.Ordinal).ToArray();
                var contractHash = ContractHasher.HashTableType(value.Schema, value.Name, columns);
                return new TableTypeContract(
                    value.Schema,
                    value.Name,
                    CSharpName.FromSqlName(value.Name) + "Tvp",
                    columns,
                    contractHash);
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<ProcedureContract>> DiscoverProceduresAsync(
        SqlConnection connection,
        IReadOnlyList<TableTypeContract> tableTypes,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        var procedures = await DiscoverProcedureHeadersAsync(connection, cancellationToken).ConfigureAwait(false);
        var tableTypesBySqlName = tableTypes.ToDictionary(
            tableType => tableType.Schema + "." + tableType.Name,
            StringComparer.OrdinalIgnoreCase);

        var contracts = new List<ProcedureContract>(procedures.Count);
        foreach (var procedure in procedures)
        {
            var parameters = await DiscoverParametersAsync(
                connection,
                procedure,
                tableTypesBySqlName,
                diagnostics,
                cancellationToken).ConfigureAwait(false);
            var resultSet = await DiscoverResultSetAsync(connection, procedure, diagnostics, cancellationToken)
                .ConfigureAwait(false);

            var clrName = CSharpName.FromSqlName(procedure.Name) + "Procedure";
            var contractHash = ContractHasher.HashProcedure(procedure.Schema, procedure.Name, parameters, resultSet);

            contracts.Add(new ProcedureContract(
                procedure.Schema,
                procedure.Name,
                clrName,
                CSharpName.FromSqlName(procedure.Name) + "Parameters",
                resultSet.Status == "Available" ? CSharpName.FromSqlName(procedure.Name) + "Result" : null,
                parameters,
                resultSet,
                contractHash));
        }

        return contracts
            .OrderBy(contract => contract.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => contract.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<List<ProcedureHeader>> DiscoverProcedureHeadersAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  p.object_id,
                                  s.name AS schema_name,
                                  p.name AS procedure_name
                              FROM sys.procedures AS p
                              JOIN sys.schemas AS s
                                  ON s.schema_id = p.schema_id
                              WHERE p.is_ms_shipped = 0
                                AND s.name NOT IN (
                                    N'sys',
                                    N'INFORMATION_SCHEMA',
                                    N'guest',
                                    N'db_owner',
                                    N'db_accessadmin',
                                    N'db_securityadmin',
                                    N'db_ddladmin',
                                    N'db_backupoperator',
                                    N'db_datareader',
                                    N'db_datawriter',
                                    N'db_denydatareader',
                                    N'db_denydatawriter'
                                )
                              ORDER BY
                                  s.name,
                                  p.name;
                              """;

        var procedures = new List<ProcedureHeader>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            procedures.Add(new ProcedureHeader(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

        return procedures;
    }

    private static async Task<IReadOnlyList<ParameterContract>> DiscoverParametersAsync(
        SqlConnection connection,
        ProcedureHeader procedure,
        Dictionary<string, TableTypeContract> tableTypesBySqlName,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  prm.parameter_id,
                                  prm.name AS parameter_name,
                                  prm.is_output,
                                  prm.is_readonly,
                                  prm.max_length,
                                  prm.precision,
                                  prm.scale,
                                  COALESCE(sysTyp.name, userTyp.name) AS type_name,
                                  userTyp.is_table_type,
                                  SCHEMA_NAME(userTyp.schema_id) AS user_type_schema,
                                  TYPE_NAME(prm.user_type_id) AS user_type_name
                              FROM sys.parameters AS prm
                              JOIN sys.types AS userTyp
                                  ON userTyp.user_type_id = prm.user_type_id
                              LEFT JOIN sys.types AS sysTyp
                                  ON sysTyp.system_type_id = prm.system_type_id
                                  AND sysTyp.user_type_id = sysTyp.system_type_id
                              WHERE prm.object_id = @ProcedureObjectId
                              ORDER BY prm.parameter_id;
                              """;
        command.Parameters.Add(new SqlParameter("@ProcedureObjectId", SqlDbType.Int) { Value = procedure.ObjectId });

        var parameters = new List<ParameterContract>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var maxLength = reader.GetInt16(4);
            var precision = reader.GetByte(5);
            var scale = reader.GetByte(6);
            var isTableType = reader.GetBoolean(8);
            var isOutput = reader.GetBoolean(2);
            var typeName = reader.GetString(7);
            var sqlType = SqlServerTypeName.Format(typeName, maxLength, precision, scale);
            var clrType = SqlServerTypeMapper.GetClrType(sqlType, false);
            var parameterName = reader.GetString(1).TrimStart('@');
            var procedureName = procedure.Schema + "." + procedure.Name;

            if (isOutput)
                diagnostics.Error(
                    "CAERIUS206",
                    $"Procedure '{procedureName}' has output parameter '{parameterName}', which is not supported by generated contracts.");

            if (isTableType)
            {
                var schema = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
                var name = reader.IsDBNull(10) ? typeName : reader.GetString(10);
                sqlType = schema + "." + name;
                clrType = tableTypesBySqlName.TryGetValue(sqlType, out var tableType)
                    ? $"ReadOnlyMemory<{tableType.ClrName}>"
                    : "ReadOnlyMemory<object>";

                if (!tableTypesBySqlName.ContainsKey(sqlType))
                    diagnostics.Error("CAERIUS202",
                        $"TVP '{sqlType}' is referenced but is missing from the contract manifest.");
            }
            else if (!SqlServerTypeMapper.IsSupported(sqlType))
            {
                diagnostics.Error("CAERIUS207", $"SQL type '{sqlType}' cannot be mapped to generated C# code.");
            }

            parameters.Add(new ParameterContract(
                reader.GetInt32(0),
                parameterName,
                sqlType,
                clrType,
                isTableType,
                isOutput,
                false,
                maxLength,
                precision,
                scale));
        }

        return parameters;
    }

    private static async Task<ResultSetContract> DiscoverResultSetAsync(
        SqlConnection connection,
        ProcedureHeader procedure,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT
                                  column_ordinal,
                                  name AS column_name,
                                  is_nullable,
                                  system_type_name,
                                  max_length,
                                  precision,
                                  scale,
                                  error_number,
                                  error_message
                              FROM sys.dm_exec_describe_first_result_set_for_object(
                                  @ProcedureObjectId,
                                  0
                              )
                              WHERE is_hidden = 0 OR error_number IS NOT NULL
                              ORDER BY column_ordinal;
                              """;
        command.Parameters.Add(new SqlParameter("@ProcedureObjectId", SqlDbType.Int) { Value = procedure.ObjectId });

        var columns = new List<ColumnContract>();
        string? errorMessage = null;
        var procedureName = procedure.Schema + "." + procedure.Name;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(7))
            {
                errorMessage = reader.IsDBNull(8)
                    ? "SQL Server could not describe the first result set."
                    : reader.GetString(8);
                continue;
            }

            if (reader.IsDBNull(0))
                continue;

            var ordinal = reader.GetInt32(0);
            if (reader.IsDBNull(1))
                diagnostics.Warning(
                    "CAERIUS210",
                    $"Procedure '{procedureName}' result column {ordinal} has no name; generated contracts will use 'Column{ordinal}'.");

            var columnName = reader.IsDBNull(1)
                ? "Column" + ordinal.ToString(CultureInfo.InvariantCulture)
                : reader.GetString(1);
            var maxLength = reader.GetInt16(4);
            var precision = reader.GetByte(5);
            var scale = reader.GetByte(6);
            var sqlType = reader.IsDBNull(3)
                ? "unknown"
                : SqlServerTypeName.Format(reader.GetString(3), maxLength, precision, scale);
            var nullable = reader.GetBoolean(2);

            if (!SqlServerTypeMapper.IsSupported(sqlType))
                diagnostics.Error("CAERIUS207", $"SQL type '{sqlType}' cannot be mapped to generated C# code.");

            if (nullable)
                diagnostics.Warning(
                    "CAERIUS208",
                    $"Result column '{procedureName}.{columnName}' is nullable and will be emitted as a nullable CLR type.");

            columns.Add(new ColumnContract(
                ordinal,
                columnName,
                sqlType,
                SqlServerTypeMapper.GetClrType(sqlType, nullable),
                nullable,
                maxLength,
                precision,
                scale));
        }

        if (errorMessage is not null)
        {
            diagnostics.Error(
                "CAERIUS204",
                $"The first result set of procedure '{procedureName}' cannot be determined by SQL Server: {errorMessage}");
            return new ResultSetContract("Undetermined", [], errorMessage);
        }

        if (columns.Count == 0)
        {
            diagnostics.Warning(
                "CAERIUS205",
                $"Procedure '{procedureName}' has no result set; no result DTO will be generated.");
            return new ResultSetContract("None", [], null);
        }

        return new ResultSetContract("Available", columns.OrderBy(column => column.Ordinal).ToArray(), null);
    }
}
