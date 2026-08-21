namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsEmitter
{
    internal static string Emit(AutoContractsManifest manifest)
    {
        var sb = new StringBuilder(16_384);
        AutoContractsGeneratedSource.AppendPreamble(sb, manifest.Namespace);
        foreach (var tableType in manifest.TableTypes)
        {
            AppendTableType(sb, tableType);
            sb.AppendLine();
        }

        foreach (var procedure in manifest.Procedures)
        {
            AppendProcedure(sb, procedure);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendTableType(StringBuilder sb, AutoContractsTableType tableType)
    {
        sb.Append("[AutoContractGenerateTvp(Schema = ").Append(Literal(tableType.Schema))
            .Append(", TypeName = ").Append(Literal(tableType.Name))
            .Append(", ContractHash = ").Append(Literal(tableType.ContractHash)).AppendLine(")] ");
        sb.Append("public readonly partial record struct ").Append(tableType.ClrName).Append('(');
        AppendColumns(sb, tableType.Columns);
        sb.Append(") : ITvpMapper<").Append(tableType.ClrName).AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public static string SqlTypeName => ").Append(Literal(tableType.Schema + "." + tableType.Name))
            .AppendLine(";");
        sb.Append("    public const string ContractHash = ").Append(Literal(tableType.ContractHash)).AppendLine(";");
        sb.AppendLine("    private static readonly ImmutableArray<SqlMetaData> _metadata = ImmutableArray.Create(");
        for (var index = 0; index < tableType.Columns.Count; index++)
            sb.Append("        ").Append(AutoContractsSqlEmitter.BuildSqlMetaDataExpression(tableType.Columns[index]))
                .AppendLine(index + 1 == tableType.Columns.Count ? string.Empty : ",");
        sb.AppendLine("    );");
        sb.AppendLine("    private static readonly SqlMetaData[] _recordMetadata = _metadata.ToArray();");
        sb.AppendLine("    public static ImmutableArray<SqlMetaData> Metadata => _metadata;");
        sb.AppendLine("    public static SqlDataRecord CreateRecord() => new(_recordMetadata);");
        AutoContractsGeneratedSource.AppendGeneratedCodeAttribute(sb, "    ");
        sb.Append("    public static void WriteRow(SqlDataRecord record, ").Append(tableType.ClrName)
            .AppendLine(" row)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(record);");
        for (var index = 0; index < tableType.Columns.Count; index++)
        {
            var column = tableType.Columns[index];
            var property = "row." + AutoContractsSqlEmitter.ToIdentifier(column.Name);
            var set = AutoContractsSqlEmitter.BuildSetExpression(column, index, property);
            if (column.Nullable)
                sb.Append("        if (").Append(property).Append(" is null) record.SetDBNull(").Append(index)
                    .Append("); else ").Append(set).AppendLine(";");
            else
                sb.Append("        ").Append(set).AppendLine(";");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendProcedure(StringBuilder sb, AutoContractsProcedure procedure)
    {
        var availableSets = procedure.ResultSets.Where(IsAvailable).ToArray();
        var outputs = procedure.Parameters.Where(parameter => parameter.Direction != "Input").ToArray();
        AppendDescriptor(sb, procedure, availableSets.Length);
        sb.AppendLine();
        AppendParameters(sb, procedure);
        if (outputs.Length > 0)
        {
            sb.AppendLine();
            AppendOutput(sb, procedure, outputs);
        }

        foreach (var resultSet in availableSets)
        {
            sb.AppendLine();
            AppendResult(sb, procedure, resultSet);
        }

        sb.AppendLine();
        AppendBuilder(sb, procedure);
        if (availableSets.Length > 0)
        {
            sb.AppendLine();
            AppendCodecRegistration(sb, procedure, availableSets);
        }

        sb.AppendLine();
        AppendExecution(sb, procedure, availableSets, outputs.Length > 0);
    }

    private static void AppendDescriptor(StringBuilder sb, AutoContractsProcedure procedure, int resultSetCount)
    {
        sb.Append("[AutoContractGenerateProcedure(Schema = ").Append(Literal(procedure.Schema))
            .Append(", Name = ").Append(Literal(procedure.Name))
            .Append(", ContractHash = ").Append(Literal(procedure.ContractHash)).AppendLine(")] ");
        sb.Append("public readonly partial struct ").Append(procedure.ClrName)
            .Append(" : ICaeriusGeneratedProcedure<").Append(procedure.ClrName).AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public static string SchemaName => ").Append(Literal(procedure.Schema)).AppendLine(";");
        sb.Append("    public static string ProcedureName => ").Append(Literal(procedure.Name)).AppendLine(";");
        sb.Append("    public static string FullName => ").Append(Literal(procedure.Schema + "." + procedure.Name))
            .AppendLine(";");
        sb.Append("    public static string ContractHash => ").Append(Literal(procedure.ContractHash)).AppendLine(";");
        sb.Append("    public static int ParameterCount => ").Append(procedure.Parameters.Count).AppendLine(";");
        sb.Append("    public static int ResultSetCount => ").Append(resultSetCount).AppendLine(";");
        sb.AppendLine("}");
    }

    private static void AppendParameters(StringBuilder sb, AutoContractsProcedure procedure)
    {
        var inputs = procedure.Parameters.Where(parameter => parameter.Direction is "Input" or "InputOutput").ToArray();
        sb.Append("public sealed partial record ").Append(procedure.ParametersClrName);
        if (inputs.Length > 0)
        {
            sb.Append('(');
            for (var index = 0; index < inputs.Length; index++)
            {
                if (index > 0) sb.Append(", ");
                sb.Append(inputs[index].ClrType).Append(' ').Append(Identifier(inputs[index].Name));
            }

            sb.Append(')');
        }

        sb.Append(" : ICaeriusGeneratedProcedureParameters<").Append(procedure.ClrName).Append(", ")
            .Append(procedure.ParametersClrName).AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public static void Bind(StoredProcedureCommandBuilder<").Append(procedure.ClrName)
            .Append("> builder, ")
            .Append(procedure.ParametersClrName).AppendLine(" parameters) => builder.WithParameters(parameters);");
        sb.AppendLine("}");
    }

    private static void AppendOutput(StringBuilder sb, AutoContractsProcedure procedure,
        AutoContractsParameter[] outputs)
    {
        sb.Append("public sealed record ").Append(OutputName(procedure)).Append('(');
        for (var index = 0; index < outputs.Length; index++)
        {
            if (index > 0) sb.Append(", ");
            sb.Append(AutoContractsSqlEmitter.ApplyNullability(outputs[index].ClrType, true)).Append(' ')
                .Append(Identifier(outputs[index].Name));
        }

        sb.AppendLine(")");
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(OutputName(procedure))
            .AppendLine(" From(CaeriusOutputValues values) => new(");
        for (var index = 0; index < outputs.Length; index++)
        {
            var output = outputs[index];
            sb.Append("        values.Get<").Append(AutoContractsSqlEmitter.ApplyNullability(output.ClrType, true))
                .Append(">(")
                .Append(Literal("@" + output.Name.TrimStart('@'))).Append(')')
                .AppendLine(index + 1 == outputs.Length ? string.Empty : ",");
        }

        sb.AppendLine("    );");
        sb.AppendLine("}");
    }

    private static void AppendResult(StringBuilder sb, AutoContractsProcedure procedure,
        AutoContractsResultSet resultSet)
    {
        var clrName = resultSet.ClrName!;
        sb.Append("[AutoContractGenerateDto(Schema = ").Append(Literal(procedure.Schema))
            .Append(", Procedure = ").Append(Literal(procedure.Name)).Append(", ResultSetIndex = ")
            .Append(resultSet.Index).Append(", ContractHash = ").Append(Literal(resultSet.ContractHash))
            .AppendLine(")] ");
        sb.Append("public sealed partial record ").Append(clrName).Append('(');
        AppendColumns(sb, resultSet.Columns);
        sb.Append(") : ISpMapper<").Append(clrName).AppendLine(">");
        sb.AppendLine("{");
        AutoContractsGeneratedSource.AppendGeneratedCodeAttribute(sb, "    ");
        sb.Append("    public static ").Append(clrName).AppendLine(" MapFromDataReader(SqlDataReader reader) => new(");
        for (var index = 0; index < resultSet.Columns.Count; index++)
            sb.Append("        ").Append(AutoContractsSqlEmitter.BuildReaderExpression(resultSet.Columns[index], index))
                .AppendLine(index + 1 == resultSet.Columns.Count ? string.Empty : ",");
        sb.AppendLine("    );");
        sb.AppendLine("}");
        sb.AppendLine();
        AppendResultCodec(sb, clrName, resultSet);
    }

    private static void AppendResultCodec(StringBuilder sb, string clrName, AutoContractsResultSet resultSet)
    {
        var codecName = clrName + "CaeriusCacheCodec";
        var bitmapLength = (resultSet.Columns.Count + 7) / 8;
        sb.Append("public sealed class ").Append(codecName).Append(" : ICaeriusCacheCodec<").Append(clrName)
            .AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public string CodecId => ").Append(Literal(clrName + "/autocontract/v2")).AppendLine(";");
        sb.Append("    public string ContractHash => ").Append(Literal(resultSet.ContractHash)).AppendLine(";");
        sb.Append("    public void Encode(IBufferWriter<byte> writer, ").Append(clrName).AppendLine(" value)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(writer);");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(value);");
        sb.Append("        Span<byte> nullBitmap = stackalloc byte[").Append(bitmapLength).AppendLine("];");
        sb.AppendLine("        nullBitmap.Clear();");
        for (var index = 0; index < resultSet.Columns.Count; index++)
        {
            var column = resultSet.Columns[index];
            if (column.Nullable)
                sb.Append("        if (value.").Append(Identifier(column.Name)).Append(" is null) nullBitmap[")
                    .Append(index / 8).Append("] |= ").Append(1 << (index % 8)).AppendLine(";");
        }

        sb.AppendLine("        var binary = new CaeriusBinaryWriter(writer);");
        sb.AppendLine("        binary.WriteBytes(nullBitmap);");
        for (var index = 0; index < resultSet.Columns.Count; index++)
        {
            var column = resultSet.Columns[index];
            if (column.Nullable)
            {
                var nonNullValue = "item" + index;
                var write = BuildWrite(column, nonNullValue);
                sb.Append("        if (value.").Append(Identifier(column.Name)).Append(" is { } ")
                    .Append(nonNullValue).Append(") ").Append(write).AppendLine(";");
            }
            else
            {
                sb.Append("        ").Append(BuildWrite(column, "value." + Identifier(column.Name)))
                    .AppendLine(";");
            }
        }

        sb.AppendLine("    }");
        sb.Append("    public ").Append(clrName).AppendLine(" Decode(ReadOnlySpan<byte> payload)");
        sb.AppendLine("    {");
        sb.AppendLine("        var binary = new CaeriusBinaryReader(payload);");
        sb.AppendLine("        var nullBitmap = binary.ReadLengthPrefixedBytes();");
        sb.Append("        if (nullBitmap.Length != ").Append(bitmapLength)
            .AppendLine(
                ") throw new global::CaeriusNet.Exceptions.CaeriusContractException(\"The AutoContracts null bitmap is invalid.\");");
        for (var index = 0; index < resultSet.Columns.Count; index++)
        {
            var column = resultSet.Columns[index];
            var read = BuildRead(column);
            if (column.Nullable)
                read =
                    $"(nullBitmap[{index / 8}] & {1 << (index % 8)}) != 0 ? ({AutoContractsSqlEmitter.ToNullableClrType(column)})null : {read}";
            sb.Append("        var item").Append(index).Append(" = ").Append(read).AppendLine(";");
        }

        sb.AppendLine(
            "        if (binary.Remaining != 0) throw new global::CaeriusNet.Exceptions.CaeriusContractException(\"The AutoContracts payload contains trailing data.\");");
        sb.Append("        return new ").Append(clrName).AppendLine("(");
        for (var index = 0; index < resultSet.Columns.Count; index++)
            sb.Append("            item").Append(index)
                .AppendLine(index + 1 == resultSet.Columns.Count ? string.Empty : ",");
        sb.AppendLine("        );");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendBuilder(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("public static partial class ").Append(procedure.ClrName).AppendLine("BuilderExtensions");
        sb.AppendLine("{");
        sb.Append("    public static StoredProcedureCommandBuilder<").Append(procedure.ClrName)
            .Append("> WithParameters(this StoredProcedureCommandBuilder<").Append(procedure.ClrName)
            .Append("> builder, ")
            .Append(procedure.ParametersClrName).AppendLine(" parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(builder);");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(parameters);");
        foreach (var parameter in procedure.Parameters)
        {
            var property = parameter.Direction is "Input" or "InputOutput"
                ? "parameters." + Identifier(parameter.Name)
                : "null";
            if (parameter.IsTableType)
            {
                sb.Append("        builder.AddTvpParameter<").Append(ExtractTvpRowType(parameter.ClrType)).Append(">(")
                    .Append(Literal("@" + parameter.Name.TrimStart('@'))).Append(", ").Append(property)
                    .Append(", ordinal: ").Append(parameter.Ordinal).AppendLine(");");
                continue;
            }

            sb.Append("        builder.AddParameter(").Append(Literal("@" + parameter.Name.TrimStart('@')))
                .Append(", ").Append(property).Append(", ")
                .Append(AutoContractsSqlEmitter.BuildSqlDbTypeExpression(parameter.SqlType))
                .Append(", ParameterDirection.").Append(parameter.Direction);
            if (AutoContractsSqlEmitter.NormalizeSqlType(parameter.SqlType) == "vector")
            {
                // SqlClient infers vector size from the value, but an output/null vector needs its SQL
                // dimension to create SqlVector<float>.CreateNull at command materialization time.
                _ = AutoContractsSqlServer2025.TryGetVectorDimensions(parameter.SqlType, out var dimensions);
                sb.Append(", size: ").Append(dimensions);
            }
            else
            {
                var facets = AutoContractsSqlFacets.BuildParameterArguments(parameter);
                if (!string.IsNullOrEmpty(facets)) sb.Append(", ").Append(facets);
            }

            sb.Append(", ordinal: ").Append(parameter.Ordinal).AppendLine(");");
        }

        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.Append("    public static StoredProcedureCommandBuilder<").Append(procedure.ClrName)
            .Append("> CreateBuilder(")
            .Append(procedure.ParametersClrName).AppendLine(" parameters, int commandTimeout = 30) =>");
        sb.Append("        StoredProcedureCommandBuilder<").Append(procedure.ClrName)
            .AppendLine(">.Create(parameters, commandTimeout);");
        sb.AppendLine("}");
    }

    private static void AppendCodecRegistration(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        AutoContractsResultSet[] resultSets)
    {
        sb.Append("public static class ").Append(procedure.ClrName).AppendLine("CodecRegistrationExtensions");
        sb.AppendLine("{");
        sb.Append("    public static IServiceCollection Add").Append(procedure.ClrName)
            .AppendLine("CaeriusCodecs(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(services);");
        foreach (var resultSet in resultSets)
            sb.Append("        services.AddCaeriusGeneratedCacheCodecs<").Append(resultSet.ClrName).Append(", ")
                .Append(resultSet.ClrName).AppendLine("CaeriusCacheCodec>();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendExecution(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        AutoContractsResultSet[] resultSets,
        bool hasOutputs)
    {
        sb.Append("public static partial class ").Append(procedure.ClrName).AppendLine("ExecutionExtensions");
        sb.AppendLine("{");
        if (resultSets.Length == 0)
        {
            AppendZeroExecution(sb, procedure, hasOutputs, false);
            sb.AppendLine();
            AppendZeroExecution(sb, procedure, hasOutputs, true);
        }
        else if (resultSets.Length == 1)
        {
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryAsync", "QueryImmutableArrayAsync", "ImmutableArray", "result.Data", false);
            sb.AppendLine();
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryEnumerableAsync", "QueryAsync", "IEnumerable",
                "global::System.Linq.Enumerable.AsEnumerable(result.Data)", false);
            sb.AppendLine();
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryReadOnlyCollectionAsync", "QueryReadOnlyCollectionAsync", "ReadOnlyCollection",
                "new ReadOnlyCollection<" + resultSets[0].ClrName + ">(new List<" + resultSets[0].ClrName +
                ">(result.Data))", false);
            sb.AppendLine();
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryAsync", "QueryImmutableArrayAsync", "ImmutableArray", "result.Data", true);
            sb.AppendLine();
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryEnumerableAsync", "QueryAsync", "IEnumerable",
                "global::System.Linq.Enumerable.AsEnumerable(result.Data)", true);
            sb.AppendLine();
            AppendSingleExecution(sb, procedure, resultSets[0].ClrName!, hasOutputs,
                "QueryReadOnlyCollectionAsync", "QueryReadOnlyCollectionAsync", "ReadOnlyCollection",
                "new ReadOnlyCollection<" + resultSets[0].ClrName + ">(new List<" + resultSets[0].ClrName +
                ">(result.Data))", true);
        }
        else
        {
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryAsync", "QueryMultipleImmutableArray", "ImmutableArray", false);
            sb.AppendLine();
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryEnumerableAsync", "QueryMultiple", "IEnumerable", false);
            sb.AppendLine();
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryReadOnlyCollectionAsync", "QueryMultipleReadOnlyCollection", "ReadOnlyCollection", false);
            sb.AppendLine();
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryAsync", "QueryMultipleImmutableArray", "ImmutableArray", true);
            sb.AppendLine();
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryEnumerableAsync", "QueryMultiple", "IEnumerable", true);
            sb.AppendLine();
            AppendMultiExecution(sb, procedure, resultSets, hasOutputs,
                "QueryReadOnlyCollectionAsync", "QueryMultipleReadOnlyCollection", "ReadOnlyCollection", true);
        }

        sb.AppendLine("}");
    }

    private static void AppendSingleExecution(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        string result,
        bool hasOutputs,
        string publicMethodName,
        string runtimeMethodName,
        string collectionType,
        string outputDataExpression,
        bool transactional)
    {
        var receiverType = transactional ? "ICaeriusNetTransaction" : "ICaeriusNetDbContext";
        var receiverName = transactional ? "transaction" : "context";
        var extensions = transactional ? "CaeriusNetTransactionExtensions" : "CaeriusNetCommandExtensions";
        if (!hasOutputs)
        {
            sb.Append("    public static ValueTask<").Append(collectionType).Append('<').Append(result)
                .Append(">> ").Append(publicMethodName)
                .Append("(this ").Append(receiverType).AppendLine(" " + receiverName +
                                                                  ", StoredProcedureCommand command, int? capacity = null, CancellationToken cancellationToken = default) =>");
            sb.Append("        ").Append(extensions).Append('.').Append(runtimeMethodName).Append('<').Append(result)
                .Append(">(").Append(receiverName).AppendLine(", command, capacity, cancellationToken);");
            return;
        }

        sb.Append("    public static async ValueTask<CaeriusExecutionResult<").Append(collectionType).Append('<')
            .Append(result)
            .Append(">, ").Append(OutputName(procedure)).Append(">> ").Append(publicMethodName)
            .Append("(this ").Append(receiverType).AppendLine(" " + receiverName +
                                                              ", StoredProcedureCommand command, int? capacity = null, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.Append("        var result = await ").Append(extensions).Append(".QueryImmutableArrayWithOutputsAsync<")
            .Append(result)
            .Append(">(").Append(receiverName)
            .AppendLine(", command, capacity, cancellationToken).ConfigureAwait(false);");
        sb.Append("        return new CaeriusExecutionResult<").Append(collectionType).Append('<').Append(result)
            .Append(">, ")
            .Append(OutputName(procedure)).Append(">(").Append(outputDataExpression).Append(", ")
            .Append(OutputName(procedure)).AppendLine(".From(result.Output));");
        sb.AppendLine("    }");
    }

    private static void AppendMultiExecution(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        AutoContractsResultSet[] resultSets,
        bool hasOutputs,
        string publicMethodName,
        string runtimeMethodName,
        string collectionType,
        bool transactional)
    {
        var receiverType = transactional ? "ICaeriusNetTransaction" : "ICaeriusNetDbContext";
        var receiverName = transactional ? "transaction" : "context";
        var dataType = BuildTupleType(resultSets, collectionType);
        var returnType = hasOutputs
            ? $"CaeriusExecutionResult<{dataType}, {OutputName(procedure)}>"
            : dataType;
        sb.Append("    public static async ValueTask<").Append(returnType)
            .Append("> ").Append(publicMethodName).Append("(this ").Append(receiverType).Append(' ')
            .Append(receiverName).AppendLine(", StoredProcedureCommand command,");
        for (var index = 0; index < resultSets.Length; index++)
            sb.Append("        int capacity").Append(index).AppendLine(" = -1,");
        sb.AppendLine("        CaeriusResultSetCountPolicy countPolicy = CaeriusResultSetCountPolicy.Exactly,");
        sb.AppendLine("        CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.Append("        var result = await CaeriusNetMultiResultSetExtensions.").Append(runtimeMethodName)
            .Append(hasOutputs ? "WithOutputs" : string.Empty).Append("Async<");
        for (var index = 0; index < resultSets.Length; index++)
        {
            if (index > 0) sb.Append(", ");
            sb.Append(resultSets[index].ClrName);
        }

        sb.Append(">(").Append(receiverName).AppendLine(", command,");
        for (var index = 0; index < resultSets.Length; index++)
            sb.Append("            capacity").Append(index).AppendLine(",");
        sb.AppendLine("            countPolicy, cancellationToken).ConfigureAwait(false);");
        if (hasOutputs)
            sb.Append("        return new CaeriusExecutionResult<").Append(dataType).Append(", ")
                .Append(OutputName(procedure))
                .Append(">(result.Data, ").Append(OutputName(procedure)).AppendLine(".From(result.Output));");
        else
            sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    private static void AppendZeroExecution(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        bool hasOutputs,
        bool transactional)
    {
        var receiverType = transactional ? "ICaeriusNetTransaction" : "ICaeriusNetDbContext";
        var receiverName = transactional ? "transaction" : "context";
        var extensions = transactional ? "CaeriusNetTransactionExtensions" : "CaeriusNetCommandExtensions";
        if (!hasOutputs)
        {
            sb.Append("    public static ValueTask ExecuteAsync(this ").Append(receiverType).Append(' ')
                .Append(receiverName)
                .AppendLine(", StoredProcedureCommand command, CancellationToken cancellationToken = default) =>");
            sb.Append("        ").Append(extensions).Append(".ExecuteAsync(").Append(receiverName)
                .AppendLine(", command, cancellationToken);");
            return;
        }

        sb.Append("    public static async ValueTask<CaeriusCommandResult<").Append(OutputName(procedure))
            .Append(">> ExecuteAsync(this ").Append(receiverType).Append(' ').Append(receiverName)
            .AppendLine(", StoredProcedureCommand command, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.Append("        var result = await ").Append(extensions).Append(".ExecuteWithOutputsAsync(")
            .Append(receiverName)
            .AppendLine(", command, cancellationToken).ConfigureAwait(false);");
        sb.Append("        return new CaeriusCommandResult<").Append(OutputName(procedure)).Append(">(")
            .Append(OutputName(procedure)).AppendLine(".From(result.Output));");
        sb.AppendLine("    }");
    }

    private static string BuildTupleType(AutoContractsResultSet[] resultSets, string collectionType)
    {
        return "(" + string.Join(", ", resultSets.Select((set, index) =>
            $"{collectionType}<{set.ClrName}> Set{index + 1}")) + ")";
    }

    private static void AppendColumns(StringBuilder sb, EquatableArray<AutoContractsColumn> columns)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0) sb.Append(", ");
            sb.Append(AutoContractsSqlEmitter.ToNullableClrType(columns[index])).Append(' ')
                .Append(Identifier(columns[index].Name));
        }
    }

    private static string BuildWrite(AutoContractsColumn column, string value)
    {
        var sqlType = column.SqlType;
        return AutoContractsSqlEmitter.NormalizeSqlType(sqlType) switch
        {
            "bit" => $"binary.WriteBoolean({value})",
            "tinyint" => $"binary.WriteByte({value})",
            "smallint" => $"binary.WriteInt16({value})",
            "int" => $"binary.WriteInt32({value})",
            "bigint" => $"binary.WriteInt64({value})",
            "real" => $"binary.WriteSingle({value})",
            "float" => $"binary.WriteDouble({value})",
            "decimal" or "numeric" when AutoContractsClrTypes.IsSqlDecimal(column.ClrType) =>
                $"binary.WriteSqlDecimal({value})",
            "decimal" or "numeric" or "money" or "smallmoney" => $"binary.WriteDecimal({value})",
            "date" => $"binary.WriteDateOnly({value})",
            "time" => $"binary.WriteTimeOnly({value})",
            "datetime" or "datetime2" or "smalldatetime" => $"binary.WriteDateTime({value})",
            "datetimeoffset" => $"binary.WriteDateTimeOffset({value})",
            "uniqueidentifier" => $"binary.WriteGuid({value})",
            "nvarchar" or "varchar" or "nchar" or "char" => $"binary.WriteString({value})",
            "varbinary" or "binary" => $"binary.WriteBytes({value})",
            "json" => $"binary.WriteString({value})",
            "vector" => $"binary.WriteSqlVector({value})",
            _ => throw new InvalidOperationException("Unsupported AutoContracts codec type: " + sqlType)
        };
    }

    private static string BuildRead(AutoContractsColumn column)
    {
        var sqlType = column.SqlType;
        return AutoContractsSqlEmitter.NormalizeSqlType(sqlType) switch
        {
            "bit" => "binary.ReadBoolean()",
            "tinyint" => "binary.ReadByte()",
            "smallint" => "binary.ReadInt16()",
            "int" => "binary.ReadInt32()",
            "bigint" => "binary.ReadInt64()",
            "real" => "binary.ReadSingle()",
            "float" => "binary.ReadDouble()",
            "decimal" or "numeric" when AutoContractsClrTypes.IsSqlDecimal(column.ClrType) =>
                "binary.ReadSqlDecimal()",
            "decimal" or "numeric" or "money" or "smallmoney" => "binary.ReadDecimal()",
            "date" => "binary.ReadDateOnly()",
            "time" => "binary.ReadTimeOnly()",
            "datetime" or "datetime2" or "smalldatetime" => "binary.ReadDateTime()",
            "datetimeoffset" => "binary.ReadDateTimeOffset()",
            "uniqueidentifier" => "binary.ReadGuid()",
            "nvarchar" or "varchar" or "nchar" or "char" => "binary.ReadString()",
            "varbinary" or "binary" => "binary.ReadBytes()",
            "json" => "binary.ReadString()",
            "vector" => "binary.ReadSqlVector()",
            _ => throw new InvalidOperationException("Unsupported AutoContracts codec type: " + sqlType)
        };
    }

    private static bool IsAvailable(AutoContractsResultSet resultSet)
    {
        return string.Equals(resultSet.Status, "Available", StringComparison.OrdinalIgnoreCase) &&
               resultSet.Columns.Count > 0 && !string.IsNullOrWhiteSpace(resultSet.ClrName);
    }

    private static string ExtractTvpRowType(string clrType)
    {
        var start = clrType.IndexOf('<');
        return start >= 0 && clrType.EndsWith(">", StringComparison.Ordinal)
            ? clrType.Substring(start + 1, clrType.Length - start - 2)
            : clrType;
    }

    private static string OutputName(AutoContractsProcedure procedure)
    {
        return procedure.ClrName + "Output";
    }

    private static string Identifier(string value)
    {
        return AutoContractsSqlEmitter.ToIdentifier(value);
    }

    private static string Literal(string value)
    {
        return AutoContractsSqlEmitter.ToStringLiteral(value);
    }
}
