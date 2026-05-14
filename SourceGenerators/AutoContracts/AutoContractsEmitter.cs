namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsEmitter
{
    internal static string Emit(AutoContractsManifest manifest)
    {
        var sb = new StringBuilder(4096);

        AutoContractsGeneratedSource.AppendPreamble(sb, manifest.Namespace);
        var tableTypesBySqlName = manifest.TableTypes.ToDictionary(
            static tableType => tableType.Schema + "." + tableType.Name,
            static tableType => tableType,
            StringComparer.OrdinalIgnoreCase);

        foreach (var tableType in manifest.TableTypes)
        {
            AppendTableType(sb, tableType);
            sb.AppendLine();
        }

        foreach (var procedure in manifest.Procedures)
        {
            AppendProcedure(sb, procedure, tableTypesBySqlName);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendTableType(StringBuilder sb, AutoContractsTableType tableType)
    {
        sb.Append("[AutoContractGenerateTvp(Schema = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.Schema))
            .Append(", TypeName = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.Name))
            .Append(", ContractHash = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.ContractHash))
            .AppendLine(")]");

        sb.Append("public readonly partial record struct ")
            .Append(tableType.ClrName)
            .Append('(');

        for (var i = 0; i < tableType.Columns.Count; i++)
        {
            var column = tableType.Columns[i];
            if (i > 0)
                sb.Append(", ");

            sb.Append(AutoContractsSqlEmitter.ToNullableClrType(column))
                .Append(' ')
                .Append(AutoContractsSqlEmitter.ToIdentifier(column.Name));
        }

        sb.AppendLine(")");
        sb.AppendLine("{");
        sb.Append("    public const string SchemaName = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.Schema)).AppendLine(";");
        sb.Append("    public const string SqlTypeName = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.Name)).AppendLine(";");
        sb.Append("    public const string FullSqlTypeName = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.Schema + "." + tableType.Name)).AppendLine(";");
        sb.Append("    public const string ContractHash = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(tableType.ContractHash)).AppendLine(";");
        sb.AppendLine();

        sb.AppendLine("    private static readonly SqlMetaData[] _metadata =");
        sb.AppendLine("    [");
        for (var i = 0; i < tableType.Columns.Count; i++)
        {
            var trailing = i + 1 < tableType.Columns.Count ? "," : string.Empty;
            sb.Append("        ")
                .Append(AutoContractsSqlEmitter.BuildSqlMetaDataExpression(tableType.Columns[i]))
                .AppendLine(trailing);
        }

        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    public static SqlMetaData[] Metadata => _metadata;");
        sb.AppendLine();

        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        AutoContractsGeneratedSource.AppendGeneratedCodeAttribute(sb, "    ");
        sb.Append("    public static void MapRow(SqlDataRecord record, ")
            .Append(tableType.ClrName)
            .AppendLine(" row)");
        sb.AppendLine("    {");
        for (var i = 0; i < tableType.Columns.Count; i++)
        {
            var column = tableType.Columns[i];
            var property = "row." + AutoContractsSqlEmitter.ToIdentifier(column.Name);
            var setExpression = AutoContractsSqlEmitter.BuildSetExpression(column, i, property);
            if (column.Nullable)
            {
                sb.Append("        if (").Append(property).AppendLine(" is null)");
                sb.Append("            record.SetDBNull(").Append(i).AppendLine(");");
                sb.AppendLine("        else");
                sb.Append("            ").Append(setExpression).AppendLine(";");
            }
            else
            {
                sb.Append("        ").Append(setExpression).AppendLine(";");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendProcedure(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        Dictionary<string, AutoContractsTableType> tableTypesBySqlName)
    {
        AppendProcedureDescriptor(sb, procedure);
        sb.AppendLine();
        AppendParametersRecord(sb, procedure);
        sb.AppendLine();

        if (IsAvailable(procedure.ResultSet) && !string.IsNullOrEmpty(procedure.ResultClrName))
        {
            AppendResultRecord(sb, procedure);
            sb.AppendLine();
        }

        AppendBuilderExtensions(sb, procedure);
        sb.AppendLine();
        AutoContractsCacheKeyEmitter.Append(sb, procedure, tableTypesBySqlName);

        if (IsAvailable(procedure.ResultSet) && !string.IsNullOrEmpty(procedure.ResultClrName))
        {
            sb.AppendLine();
            AppendExecutionExtensions(sb, procedure);
        }
    }

    private static void AppendProcedureDescriptor(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("[AutoContractGenerateProcedure(Schema = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Schema))
            .Append(", Name = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Name))
            .Append(", ContractHash = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.ContractHash))
            .AppendLine(")]");

        sb.Append("public readonly partial struct ")
            .Append(procedure.ClrName)
            .Append(" : ICaeriusGeneratedProcedure<")
            .Append(procedure.ClrName)
            .AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public static string SchemaName => ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Schema)).AppendLine(";");
        sb.Append("    public static string ProcedureName => ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Name)).AppendLine(";");
        sb.Append("    public static string FullName => ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Schema + "." + procedure.Name)).AppendLine(";");
        sb.Append("    public static string ContractHash => ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.ContractHash)).AppendLine(";");
        sb.Append("    public static int ParameterCount => ")
            .Append(procedure.Parameters.Count).AppendLine(";");
        sb.Append("    public static int ResultSetCount => ")
            .Append(IsAvailable(procedure.ResultSet) ? 1 : 0).AppendLine(";");
        sb.AppendLine("}");
    }

    private static void AppendParametersRecord(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("public sealed partial record ")
            .Append(procedure.ParametersClrName)
            .Append('(');

        for (var i = 0; i < procedure.Parameters.Count; i++)
        {
            var parameter = procedure.Parameters[i];
            if (i > 0)
                sb.Append(", ");

            sb.Append(parameter.ClrType)
                .Append(' ')
                .Append(AutoContractsSqlEmitter.ToIdentifier(parameter.Name));
        }

        sb.Append(") : ICaeriusGeneratedProcedureParameters<")
            .Append(procedure.ClrName)
            .Append(", ")
            .Append(procedure.ParametersClrName)
            .AppendLine(">");
        sb.AppendLine("{");
        sb.Append("    public static void Bind(StoredProcedureParametersBuilder<")
            .Append(procedure.ClrName)
            .Append("> builder, ")
            .Append(procedure.ParametersClrName)
            .AppendLine(" parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(builder);");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(parameters);");
        sb.AppendLine("        builder.WithParameters(parameters);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendResultRecord(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("[AutoContractGenerateDto(Schema = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Schema))
            .Append(", Procedure = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.Name))
            .Append(", ResultSetIndex = 0, ContractHash = ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(procedure.ContractHash))
            .AppendLine(")]");

        sb.Append("public sealed partial record ")
            .Append(procedure.ResultClrName)
            .Append('(');

        for (var i = 0; i < procedure.ResultSet.Columns.Count; i++)
        {
            var column = procedure.ResultSet.Columns[i];
            if (i > 0)
                sb.Append(", ");

            sb.Append(AutoContractsSqlEmitter.ToNullableClrType(column))
                .Append(' ')
                .Append(AutoContractsSqlEmitter.ToIdentifier(column.Name));
        }

        sb.Append(") : ISpMapper<").Append(procedure.ResultClrName).AppendLine(">");
        sb.AppendLine("{");
        sb.AppendLine("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        AutoContractsGeneratedSource.AppendGeneratedCodeAttribute(sb, "    ");
        sb.Append("    public static ").Append(procedure.ResultClrName)
            .AppendLine(" MapFromDataReader(SqlDataReader reader)");
        sb.AppendLine("    {");
        sb.Append("        return new ").Append(procedure.ResultClrName).AppendLine("(");
        for (var i = 0; i < procedure.ResultSet.Columns.Count; i++)
        {
            var trailing = i + 1 < procedure.ResultSet.Columns.Count ? "," : string.Empty;
            sb.Append("            ")
                .Append(AutoContractsSqlEmitter.BuildReaderExpression(procedure.ResultSet.Columns[i], i))
                .AppendLine(trailing);
        }

        sb.AppendLine("        );");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void AppendBuilderExtensions(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("public static partial class ")
            .Append(procedure.ClrName)
            .AppendLine("BuilderExtensions");
        sb.AppendLine("{");

        sb.Append("    public static StoredProcedureParametersBuilder<")
            .Append(procedure.ClrName)
            .Append("> WithParameters(this StoredProcedureParametersBuilder<")
            .Append(procedure.ClrName)
            .Append("> builder, ")
            .Append(procedure.ParametersClrName)
            .AppendLine(" parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(builder);");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(parameters);");

        foreach (var parameter in procedure.Parameters)
        {
            var property = "parameters." + AutoContractsSqlEmitter.ToIdentifier(parameter.Name);
            if (parameter.IsTableType)
                AppendTvpParameterBinding(sb, parameter, property);
            else
                AppendScalarParameterBinding(sb, parameter, property);
        }

        sb.AppendLine("        return builder.MarkGeneratedParametersBound();");
        sb.AppendLine("    }");

        if (procedure.Parameters.Count > 0)
        {
            sb.AppendLine();
            AppendBuilderValuesOverload(sb, procedure);
        }

        sb.AppendLine("}");
    }

    private static void AppendBuilderValuesOverload(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("    public static StoredProcedureParametersBuilder<")
            .Append(procedure.ClrName)
            .Append("> WithParameters(this StoredProcedureParametersBuilder<")
            .Append(procedure.ClrName)
            .AppendLine("> builder,");

        for (var i = 0; i < procedure.Parameters.Count; i++)
        {
            var parameter = procedure.Parameters[i];
            var trailing = i + 1 < procedure.Parameters.Count ? "," : string.Empty;
            sb.Append("        ")
                .Append(parameter.ClrType)
                .Append(' ')
                .Append(AutoContractsSqlEmitter.ToIdentifier(parameter.Name))
                .AppendLine(trailing);
        }

        sb.AppendLine("    )");
        sb.AppendLine("    {");
        sb.Append("        return builder.WithParameters(new ")
            .Append(procedure.ParametersClrName)
            .Append('(');

        for (var i = 0; i < procedure.Parameters.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");

            sb.Append(AutoContractsSqlEmitter.ToIdentifier(procedure.Parameters[i].Name));
        }

        sb.AppendLine("));");
        sb.AppendLine("    }");
    }

    private static void AppendScalarParameterBinding(
        StringBuilder sb,
        AutoContractsParameter parameter,
        string property)
    {
        sb.Append("        builder.AddGeneratedParameter(")
            .Append(parameter.Ordinal)
            .Append(", ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral("@" + parameter.Name.TrimStart('@')))
            .Append(", ")
            .Append(property)
            .Append(", ")
            .Append(AutoContractsSqlEmitter.BuildSqlDbTypeExpression(parameter.SqlType));

        var facets = AutoContractsSqlFacets.BuildParameterArguments(parameter);
        if (!string.IsNullOrEmpty(facets))
            sb.Append(", ").Append(facets);

        sb.AppendLine(");");
    }

    private static void AppendTvpParameterBinding(
        StringBuilder sb,
        AutoContractsParameter parameter,
        string property)
    {
        var rowType = ExtractTvpRowType(parameter.ClrType);
        sb.Append("        builder.AddGeneratedTvpParameter<")
            .Append(rowType)
            .Append(">(")
            .Append(parameter.Ordinal)
            .Append(", ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral("@" + parameter.Name.TrimStart('@')))
            .Append(", ")
            .Append(rowType)
            .Append(".FullSqlTypeName, ")
            .Append(rowType)
            .Append(".Metadata, ")
            .Append(property)
            .Append(", ")
            .Append(rowType)
            .Append(".MapRow")
            .AppendLine(");");
    }

    private static void AppendExecutionExtensions(StringBuilder sb, AutoContractsProcedure procedure)
    {
        sb.Append("public static partial class ")
            .Append(procedure.ClrName)
            .AppendLine("ExecutionExtensions");
        sb.AppendLine("{");
        sb.Append("    public static ValueTask<ImmutableArray<")
            .Append(procedure.ResultClrName)
            .Append(">> QueryAsImmutableArrayAsync(this ICaeriusNetDbContext dbContext, StoredProcedureParameters<")
            .Append(procedure.ClrName)
            .AppendLine("> parameters, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(dbContext);");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(parameters);");
        sb.Append("        return dbContext.QueryAsImmutableArrayAsync<")
            .Append(procedure.ResultClrName)
            .AppendLine(">(parameters.AsUntyped(), cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static bool IsAvailable(AutoContractsResultSet resultSet)
    {
        return string.Equals(resultSet.Status, "Available", StringComparison.OrdinalIgnoreCase)
               && resultSet.Columns.Count > 0;
    }

    private static string ExtractTvpRowType(string clrType)
    {
        const string Prefix = "ReadOnlyMemory<";
        if (clrType.StartsWith(Prefix, StringComparison.Ordinal) && clrType.EndsWith(">", StringComparison.Ordinal))
            return clrType.Substring(Prefix.Length, clrType.Length - Prefix.Length - 1);

        return clrType;
    }
}
