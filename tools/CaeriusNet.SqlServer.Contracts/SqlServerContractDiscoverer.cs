using System.Globalization;
using System.IO.Enumeration;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.SqlServer.Contracts;

internal static class SqlServerContractDiscoverer
{
    private const string MetadataCommand = """
                                           SET NOCOUNT ON;

                                           SELECT DB_NAME(), CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)), compatibility_level
                                           FROM sys.databases WHERE database_id = DB_ID();

                                           SELECT s.name, tt.name, tt.user_type_id, c.column_id, c.name,
                                                  COALESCE(system_type.name, user_type.name), c.max_length, c.precision, c.scale, c.is_nullable,
                                                  c.collation_name,
                                                  CONVERT(int, COLLATIONPROPERTY(c.collation_name, 'LCID')),
                                                  CONVERT(int, COLLATIONPROPERTY(c.collation_name, 'ComparisonStyle')),
                                                  tt.type_table_object_id
                                           FROM sys.table_types AS tt
                                           JOIN sys.columns AS c ON c.object_id = tt.type_table_object_id
                                           JOIN sys.types AS user_type ON user_type.user_type_id = c.user_type_id
                                           LEFT JOIN sys.types AS system_type
                                             ON system_type.system_type_id = c.system_type_id
                                            AND system_type.user_type_id = system_type.system_type_id
                                           JOIN sys.schemas AS s ON s.schema_id = tt.schema_id
                                           WHERE s.name NOT IN (N'sys', N'INFORMATION_SCHEMA')
                                           ORDER BY s.name, tt.name, c.column_id;

                                           SELECT p.object_id, s.name, p.name
                                           FROM sys.procedures AS p
                                           JOIN sys.schemas AS s ON s.schema_id = p.schema_id
                                           WHERE p.is_ms_shipped = 0 AND s.name NOT IN (N'sys', N'INFORMATION_SCHEMA')
                                           ORDER BY s.name, p.name;

                                           SELECT prm.object_id, prm.parameter_id,
                                                  COALESCE(NULLIF(prm.name, N''), N'@returnValue'), prm.is_output, prm.is_readonly,
                                                  prm.max_length, prm.precision, prm.scale,
                                                  COALESCE(system_type.name, user_type.name), user_type.is_table_type,
                                                  SCHEMA_NAME(user_type.schema_id), TYPE_NAME(prm.user_type_id)
                                           FROM sys.parameters AS prm
                                           JOIN sys.procedures AS p ON p.object_id = prm.object_id
                                           JOIN sys.schemas AS s ON s.schema_id = p.schema_id
                                           JOIN sys.types AS user_type ON user_type.user_type_id = prm.user_type_id
                                           LEFT JOIN sys.types AS system_type
                                             ON system_type.system_type_id = prm.system_type_id
                                            AND system_type.user_type_id = system_type.system_type_id
                                           WHERE p.is_ms_shipped = 0 AND s.name NOT IN (N'sys', N'INFORMATION_SCHEMA')
                                           ORDER BY prm.object_id, prm.parameter_id;

                                           SELECT p.object_id, described.column_ordinal, described.name, described.is_nullable,
                                                  described.system_type_name, described.max_length, described.precision, described.scale,
                                                  described.error_number, described.error_message, described.collation_name,
                                                  CONVERT(int, COLLATIONPROPERTY(described.collation_name, 'LCID')),
                                                  CONVERT(int, COLLATIONPROPERTY(described.collation_name, 'ComparisonStyle'))
                                           FROM sys.procedures AS p
                                           JOIN sys.schemas AS s ON s.schema_id = p.schema_id
                                           OUTER APPLY sys.dm_exec_describe_first_result_set_for_object(p.object_id, 0) AS described
                                           WHERE p.is_ms_shipped = 0
                                             AND s.name NOT IN (N'sys', N'INFORMATION_SCHEMA')
                                             AND (described.is_hidden = 0 OR described.error_number IS NOT NULL OR described.column_ordinal IS NULL)
                                           ORDER BY p.object_id, described.column_ordinal;
                                           """;

    // vector_dimensions/vector_base_type do not exist in SQL Server 2022 catalog views.  Execute this
    // query only after a major-17 preflight so the main discovery query remains valid against both servers.
    private const string VectorMetadataCommand = """
                                                 SET NOCOUNT ON;

                                                 SELECT object_id, column_id, vector_dimensions, vector_base_type
                                                 FROM sys.columns
                                                 WHERE vector_dimensions IS NOT NULL;

                                                 SELECT object_id, parameter_id, vector_dimensions, vector_base_type
                                                 FROM sys.parameters
                                                 WHERE vector_dimensions IS NOT NULL;
                                                 """;

    internal static async Task<ContractManifest> DiscoverAsync(
        CommandLineOptions options,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken = default)
    {
        var connectionString = ConnectionStringResolver.Resolve(options);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var serverMajor = await ReadServerMajorAsync(connection, options.TimeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        var vectorMetadata = await ReadVectorMetadataAsync(
                connection,
                serverMajor,
                options.TimeoutSeconds,
                cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = MetadataCommand;
        command.CommandTimeout = options.TimeoutSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var database = await ReadDatabaseAsync(reader, diagnostics, cancellationToken).ConfigureAwait(false);
        await MoveNextResultAsync(reader, cancellationToken).ConfigureAwait(false);
        var tableTypes = await ReadTableTypesAsync(reader, diagnostics, vectorMetadata, cancellationToken)
            .ConfigureAwait(false);
        await MoveNextResultAsync(reader, cancellationToken).ConfigureAwait(false);
        var procedures = await ReadProcedureHeadersAsync(reader, options, cancellationToken).ConfigureAwait(false);
        await MoveNextResultAsync(reader, cancellationToken).ConfigureAwait(false);
        await ReadParametersAsync(reader, procedures, tableTypes, diagnostics, vectorMetadata, cancellationToken)
            .ConfigureAwait(false);
        await MoveNextResultAsync(reader, cancellationToken).ConfigureAwait(false);
        await ReadFirstResultsAsync(reader, procedures, diagnostics, cancellationToken).ConfigureAwait(false);

        return new ContractManifest(
            3,
            "CaeriusNet.Generated",
            database,
            BuildTableTypeContracts(tableTypes, procedures),
            BuildProcedureContracts(procedures, diagnostics),
            database.ServerMajor,
            database.ServerMajor == 17 ? ["SqlServer2025Types"] : []);
    }

    private static async Task<DatabaseInfo> ReadDatabaseAsync(
        SqlDataReader reader,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Could not read SQL Server database metadata.");
        var version = reader.GetString(1);
        var majorText = version.Split('.')[0];
        if (!int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            major is not (16 or 17))
            diagnostics.Error("CAERIUS212", "AutoContracts supports SQL Server product major versions 16 and 17 only.");
        return new DatabaseInfo(reader.GetString(0), version, major, reader.GetByte(2));
    }

    private static async Task<int> ReadServerMajorAsync(
        SqlConnection connection,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));";
        command.CommandTimeout = timeoutSeconds;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        var majorText = value?.Split('.')[0];
        return int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            ? major
            : 0;
    }

    private static async Task<VectorMetadataLookup> ReadVectorMetadataAsync(
        SqlConnection connection,
        int serverMajor,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (serverMajor != 17)
            return VectorMetadataLookup.Empty;

        await using var command = connection.CreateCommand();
        command.CommandText = VectorMetadataCommand;
        command.CommandTimeout = timeoutSeconds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var columns = await ReadVectorRowsAsync(reader, cancellationToken).ConfigureAwait(false);
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("SQL Server returned incomplete vector metadata.");
        var parameters = await ReadVectorRowsAsync(reader, cancellationToken).ConfigureAwait(false);
        return new VectorMetadataLookup(columns, parameters);
    }

    private static async Task<Dictionary<(int ObjectId, int Ordinal), VectorMetadata>> ReadVectorRowsAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<(int ObjectId, int Ordinal), VectorMetadata>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.IsDBNull(2) || reader.IsDBNull(3))
                continue;
            values[(reader.GetInt32(0), reader.GetInt32(1))] = new VectorMetadata(
                reader.GetInt32(2),
                reader.GetByte(3));
        }

        return values;
    }

    private static async Task<Dictionary<string, MutableTableType>> ReadTableTypesAsync(
        SqlDataReader reader,
        ContractDiagnosticSink diagnostics,
        VectorMetadataLookup vectorMetadata,
        CancellationToken cancellationToken)
    {
        var tableTypes = new Dictionary<string, MutableTableType>(StringComparer.OrdinalIgnoreCase);
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
            var typeName = reader.GetString(5);
            var vector = vectorMetadata.GetColumn(reader.GetInt32(13), reader.GetInt32(3));
            var sqlType = SqlServerTypeName.Format(typeName, maxLength, precision, scale, vector?.Dimensions);
            var columnName = reader.GetString(4);
            var nullable = reader.GetBoolean(9);
            if (!SqlServerTypeMapper.IsSupported(sqlType) ||
                !ValidateVectorMetadata(typeName, vector))
                diagnostics.Error("CAERIUS203",
                    $"TVP column '{key}.{columnName}' uses unsupported SQL type '{sqlType}'.");
            tableType.Columns.Add(new ColumnContract(
                reader.GetInt32(3) - 1, columnName, sqlType, SqlServerTypeMapper.GetClrType(sqlType, nullable),
                nullable, maxLength, precision, scale,
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return tableTypes;
    }

    private static async Task<Dictionary<int, MutableProcedure>> ReadProcedureHeadersAsync(
        SqlDataReader reader,
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        var procedures = new Dictionary<int, MutableProcedure>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var header = new ProcedureHeader(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
            if (IsSelected(header.Schema + "." + header.Name, options))
                procedures.Add(header.ObjectId, new MutableProcedure(header));
        }

        return procedures;
    }

    private static async Task ReadParametersAsync(
        SqlDataReader reader,
        Dictionary<int, MutableProcedure> procedures,
        Dictionary<string, MutableTableType> tableTypes,
        ContractDiagnosticSink diagnostics,
        VectorMetadataLookup vectorMetadata,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!procedures.TryGetValue(reader.GetInt32(0), out var procedure))
                continue;
            var ordinal = reader.GetInt32(1);
            var parameterName = reader.GetString(2).TrimStart('@');
            var isOutput = reader.GetBoolean(3);
            var maxLength = reader.GetInt16(5);
            var precision = reader.GetByte(6);
            var scale = reader.GetByte(7);
            var typeName = reader.GetString(8);
            var isTableType = reader.GetBoolean(9);
            var vector = vectorMetadata.GetParameter(reader.GetInt32(0), ordinal);
            var sqlType = SqlServerTypeName.Format(typeName, maxLength, precision, scale, vector?.Dimensions);
            var clrType = SqlServerTypeMapper.GetClrType(sqlType, false);
            // sys.parameters only tells us that a parameter is output-capable.  It cannot tell
            // whether a caller must also supply an input value, so discovery deliberately chooses
            // the safe output-only ADO.NET direction.  Applications that need InputOutput opt in
            // explicitly through caerius.contracts.overrides.json; no procedure is executed here.
            var direction = ordinal == 0 ? "ReturnValue" : isOutput ? "Output" : "Input";

            if (isTableType)
            {
                var schema = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
                var name = reader.IsDBNull(11) ? typeName : reader.GetString(11);
                sqlType = schema + "." + name;
                clrType = tableTypes.TryGetValue(sqlType, out var tableType)
                    ? $"IReadOnlyList<{BuildClrPrefix(tableType.Schema, tableType.Name)}Tvp>"
                    : "IReadOnlyList<object>";
                if (!tableTypes.ContainsKey(sqlType))
                    diagnostics.Error("CAERIUS202",
                        $"TVP '{sqlType}' is referenced but is missing from the contract manifest.");
            }
            else if (!SqlServerTypeMapper.IsSupported(sqlType) ||
                     !ValidateVectorMetadata(typeName, vector))
            {
                diagnostics.Error("CAERIUS207", $"SQL type '{sqlType}' cannot be mapped to generated C# code.");
            }

            procedure.Parameters.Add(new ParameterContract(
                ordinal, parameterName, sqlType, clrType, isTableType, direction, false,
                maxLength, precision, scale));
        }
    }

    private static async Task ReadFirstResultsAsync(
        SqlDataReader reader,
        Dictionary<int, MutableProcedure> procedures,
        ContractDiagnosticSink diagnostics,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!procedures.TryGetValue(reader.GetInt32(0), out var procedure))
                continue;
            if (!reader.IsDBNull(8))
            {
                procedure.ResultSetError = reader.IsDBNull(9)
                    ? "SQL Server could not describe the first result set."
                    : reader.GetString(9);
                continue;
            }

            if (reader.IsDBNull(1))
                continue;
            var sqlOrdinal = reader.GetInt32(1);
            var ordinal = sqlOrdinal - 1;
            var procedureName = procedure.Header.Schema + "." + procedure.Header.Name;
            var columnName = reader.IsDBNull(2)
                ? "Column" + sqlOrdinal.ToString(CultureInfo.InvariantCulture)
                : reader.GetString(2);
            if (reader.IsDBNull(2))
                diagnostics.Warning("CAERIUS210",
                    $"Procedure '{procedureName}' result column {ordinal} has no name; generated contracts use '{columnName}'.");
            var nullable = reader.GetBoolean(3);
            var maxLength = reader.GetInt16(5);
            var precision = reader.GetByte(6);
            var scale = reader.GetByte(7);
            var sqlType = reader.IsDBNull(4)
                ? "unknown"
                : SqlServerTypeName.Format(reader.GetString(4), maxLength, precision, scale);
            if (!SqlServerTypeMapper.IsSupported(sqlType) ||
                (IsVectorType(sqlType) && !HasVectorDimensions(sqlType)))
                diagnostics.Error("CAERIUS207", $"SQL type '{sqlType}' cannot be mapped to generated C# code.");
            procedure.FirstResultColumns.Add(new ColumnContract(
                ordinal, columnName, sqlType, SqlServerTypeMapper.GetClrType(sqlType, nullable), nullable,
                maxLength, precision, scale,
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }
    }

    private static TableTypeContract[] BuildTableTypeContracts(
        Dictionary<string, MutableTableType> tableTypes,
        Dictionary<int, MutableProcedure> procedures)
    {
        var usedTableTypes = procedures.Values
            .SelectMany(procedure => procedure.Parameters)
            .Where(parameter => parameter.IsTableType)
            .Select(parameter => parameter.SqlType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tableTypes.Values
            .Where(value => usedTableTypes.Contains(value.Schema + "." + value.Name))
            .OrderBy(value => value.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(value =>
            {
                var columns = value.Columns.OrderBy(column => column.Ordinal).ToArray();
                var clrName = BuildClrPrefix(value.Schema, value.Name) + "Tvp";
                return new TableTypeContract(value.Schema, value.Name, clrName,
                    columns, ContractHasher.HashTableType(value.Schema, value.Name, clrName, columns));
            }).ToArray();
    }

    private static ProcedureContract[] BuildProcedureContracts(
        Dictionary<int, MutableProcedure> procedures,
        ContractDiagnosticSink diagnostics)
    {
        return procedures.Values
            .OrderBy(value => value.Header.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Header.Name, StringComparer.OrdinalIgnoreCase)
            .Select(value =>
            {
                var fullName = value.Header.Schema + "." + value.Header.Name;
                string status;
                if (value.ResultSetError is not null)
                {
                    status = "Undetermined";
                    diagnostics.Error("CAERIUS204",
                        $"The first result set of '{fullName}' cannot be determined: {value.ResultSetError}");
                }
                else if (value.FirstResultColumns.Count == 0)
                {
                    status = "None";
                    diagnostics.Warning("CAERIUS205",
                        $"Procedure '{fullName}' has no statically described result set.");
                }
                else
                {
                    status = "Available";
                }

                var columns = value.FirstResultColumns.OrderBy(column => column.Ordinal).ToArray();
                var clrPrefix = BuildClrPrefix(value.Header.Schema, value.Header.Name);
                var resultClrName = status == "Available" ? clrPrefix + "Result" : null;
                var resultHash = ContractHasher.HashResultSet(0, resultClrName, status, value.ResultSetError, columns);
                var resultSets = new[]
                {
                    new ResultSetContract(0,
                        resultClrName,
                        status, columns, value.ResultSetError, resultHash)
                };
                var parameterList = value.Parameters.OrderBy(parameter => parameter.Ordinal).ToList();
                if (parameterList.Count == 0 || parameterList[0].Ordinal != 0)
                    parameterList.Insert(0, new ParameterContract(
                        0, "returnValue", "int", "int", false, "ReturnValue", false,
                        4, 10, 0));
                var parameters = parameterList.ToArray();
                var clrName = clrPrefix + "Procedure";
                var parametersClrName = clrPrefix + "Parameters";
                var hash = ContractHasher.HashProcedure(
                    value.Header.Schema, value.Header.Name, clrName, parametersClrName, parameters, resultSets);
                return new ProcedureContract(value.Header.Schema, value.Header.Name,
                    clrName,
                    parametersClrName,
                    parameters, resultSets, hash);
            }).ToArray();
    }

    private static string BuildClrPrefix(string schema, string name)
    {
        return CSharpName.FromSqlName(schema) + "_" + CSharpName.FromSqlName(name);
    }

    private static bool IsSelected(string fullName, CommandLineOptions options)
    {
        var included = options.Includes.Count == 0 || options.Includes.Any(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, fullName));
        return included && !options.Excludes.Any(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, fullName));
    }

    private static async Task MoveNextResultAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("SQL Server returned an incomplete AutoContracts metadata response.");
    }

    private static bool ValidateVectorMetadata(string typeName, VectorMetadata? metadata)
    {
        if (!IsVectorType(typeName))
            return true;

        return metadata is { Dimensions: >= 1 and <= 1998, BaseType: 0 };
    }

    private static bool IsVectorType(string sqlType)
    {
        var span = sqlType.AsSpan().Trim();
        var open = span.IndexOf('(');
        if (open >= 0)
            span = span.Slice(0, open);
        var dot = span.LastIndexOf('.');
        if (dot >= 0)
            span = span.Slice(dot + 1);
        return string.Equals(span.ToString(), "vector", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVectorDimensions(string sqlType)
    {
        return sqlType.StartsWith("vector(", StringComparison.OrdinalIgnoreCase) &&
               sqlType.EndsWith(')');
    }

    private sealed record VectorMetadata(int Dimensions, byte BaseType);

    private sealed class VectorMetadataLookup(
        Dictionary<(int ObjectId, int Ordinal), VectorMetadata> columns,
        Dictionary<(int ObjectId, int Ordinal), VectorMetadata> parameters)
    {
        internal static VectorMetadataLookup Empty { get; } = new([], []);

        internal VectorMetadata? GetColumn(int objectId, int ordinal)
        {
            return columns.TryGetValue((objectId, ordinal), out var value) ? value : null;
        }

        internal VectorMetadata? GetParameter(int objectId, int ordinal)
        {
            return parameters.TryGetValue((objectId, ordinal), out var value) ? value : null;
        }
    }
}
