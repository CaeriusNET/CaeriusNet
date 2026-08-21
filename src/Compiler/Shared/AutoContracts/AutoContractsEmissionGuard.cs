namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsEmissionGuard
{
    internal static bool CanEmit(AutoContractsManifest manifest)
    {
        return CanEmit(manifest, false);
    }

    internal static bool CanEmit(AutoContractsManifest manifest, bool sqlServer2025TypesEnabled)
    {
        return GetValidationErrors(manifest, sqlServer2025TypesEnabled).Length == 0;
    }

    internal static ImmutableArray<string> GetValidationErrors(AutoContractsManifest manifest)
    {
        return GetValidationErrors(manifest, false);
    }

    internal static ImmutableArray<string> GetValidationErrors(
        AutoContractsManifest manifest,
        bool sqlServer2025TypesEnabled)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var tableTypes = BuildTableTypeLookup(manifest);

        if (manifest.Version != AutoContractsManifestParser.SupportedManifestVersion)
            errors.Add($"The manifest must declare version {AutoContractsManifestParser.SupportedManifestVersion}.");
        if (manifest.ServerMajor is not (16 or 17))
            errors.Add("The v3 manifest must declare serverMajor 16 (SQL Server 2022) or 17 (SQL Server 2025).");
        foreach (var feature in manifest.Features)
            if (!string.Equals(feature, AutoContractsSqlServer2025.FeatureName, StringComparison.OrdinalIgnoreCase))
                errors.Add($"The v3 manifest declares unknown SQL Server feature '{feature}'.");
        if (manifest.ServerMajor != 17 && manifest.Features.Any(static feature =>
                string.Equals(feature, AutoContractsSqlServer2025.FeatureName, StringComparison.OrdinalIgnoreCase)))
            errors.Add("The SqlServer2025Types feature requires serverMajor 17.");
        if (!IsValidNamespace(manifest.Namespace))
            errors.Add("The manifest namespace is not a valid C# namespace.");
        if (!HasNoGenerationNameCollisions(manifest))
            errors.Add("The manifest contains duplicate SQL or generated C# names.");
        if (AutoContractsSqlServer2025.ContainsNativeTvpTypes(manifest))
            errors.Add("SQL Server 2025 JSON and vector columns are not supported in TVPs.");
        if (!AutoContractsSqlServer2025.HasOnlyValidVectorDimensions(manifest))
            errors.Add(
                "SQL Server vector contracts must declare a float32 dimension between 1 and 1998, for example vector(1536). ");
        if (AutoContractsSqlServer2025.ContainsNativeTypes(manifest) &&
            !AutoContractsSqlServer2025.CanUse(manifest, sqlServer2025TypesEnabled))
            errors.Add(
                "SQL Server 2025 JSON/vector contracts require CaeriusSqlServer2025Types=Enabled and a server-major-17 manifest with the SqlServer2025Types feature.");

        AppendRequiredFacetErrors(manifest, errors);

        foreach (var tableType in manifest.TableTypes)
            if (!CanEmitTableType(tableType))
                errors.Add(
                    $"Table type '{BuildFullName(tableType.Schema, tableType.Name)}' is not representable by AutoContracts.");

        foreach (var procedure in manifest.Procedures)
            if (!CanEmitProcedure(procedure, tableTypes))
                errors.Add(
                    $"Procedure '{BuildFullName(procedure.Schema, procedure.Name)}' is not representable by AutoContracts.");

        return errors.ToImmutable();
    }

    private static void AppendRequiredFacetErrors(
        AutoContractsManifest manifest,
        ImmutableArray<string>.Builder errors)
    {
        foreach (var tableType in manifest.TableTypes)
        foreach (var column in tableType.Columns)
            AppendColumnFacetErrors(
                errors,
                $"TVP column '{BuildFullName(tableType.Schema, tableType.Name)}.{column.Name}'",
                column);

        foreach (var procedure in manifest.Procedures)
        {
            var procedureName = BuildFullName(procedure.Schema, procedure.Name);
            foreach (var parameter in procedure.Parameters)
                AppendParameterFacetErrors(errors, $"Parameter '{procedureName}.@{parameter.Name.TrimStart('@')}'",
                    parameter);
            foreach (var resultSet in procedure.ResultSets)
            foreach (var column in resultSet.Columns)
                AppendColumnFacetErrors(
                    errors,
                    $"Result column '{procedureName}.Set{resultSet.Index}.{column.Name}'",
                    column);
        }
    }

    private static void AppendColumnFacetErrors(
        ImmutableArray<string>.Builder errors,
        string subject,
        AutoContractsColumn column)
    {
        var sqlType = AutoContractsNaming.NormalizeSqlType(column.SqlType);
        AppendCommonFacetErrors(errors, subject, sqlType, column.MaxLength, column.Precision, column.Scale);
        if (IsCollatedString(sqlType) &&
            (string.IsNullOrWhiteSpace(column.Collation) || column.LocaleId is null ||
             column.SqlCompareOptions is null))
            errors.Add($"{subject} requires collation, localeId, and sqlCompareOptions in the v3 manifest.");
    }

    private static void AppendParameterFacetErrors(
        ImmutableArray<string>.Builder errors,
        string subject,
        AutoContractsParameter parameter)
    {
        if (parameter.IsTableType)
            return;
        var sqlType = AutoContractsNaming.NormalizeSqlType(parameter.SqlType);
        AppendCommonFacetErrors(errors, subject, sqlType, parameter.MaxLength, parameter.Precision, parameter.Scale);
    }

    private static void AppendCommonFacetErrors(
        ImmutableArray<string>.Builder errors,
        string subject,
        string sqlType,
        int? maxLength,
        byte? precision,
        byte? scale)
    {
        if (IsSizedType(sqlType) && maxLength is null)
            errors.Add($"{subject} requires maxLength in the v3 manifest.");
        else if (IsSizedType(sqlType) && maxLength is { } length && length != -1 && length <= 0)
            errors.Add($"{subject} has an invalid maxLength.");

        if (sqlType is "decimal" or "numeric")
        {
            if (precision is null || scale is null)
                errors.Add($"{subject} requires precision and scale in the v3 manifest.");
            else if (precision is 0 or > 38 || scale > precision)
                errors.Add($"{subject} has invalid decimal precision or scale.");
        }

        if (sqlType is "time" or "datetime2" or "datetimeoffset")
        {
            if (scale is null)
                errors.Add($"{subject} requires scale in the v3 manifest.");
            else if (scale > 7)
                errors.Add($"{subject} has an invalid temporal scale.");
        }
    }

    private static bool IsSizedType(string sqlType)
    {
        return sqlType is "nvarchar" or "nchar" or "varchar" or "char" or "varbinary" or "binary";
    }

    private static bool IsCollatedString(string sqlType)
    {
        return sqlType is "nvarchar" or "nchar" or "varchar" or "char";
    }

    private static HashSet<string> BuildTableTypeLookup(AutoContractsManifest manifest)
    {
        var tableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableType in manifest.TableTypes)
            tableTypes.Add(BuildFullName(tableType.Schema, tableType.Name));

        return tableTypes;
    }

    private static bool CanEmitTableType(AutoContractsTableType tableType)
    {
        var valid = true;
        valid &= IsValidSqlIdentifier(tableType.Schema) && IsValidSqlIdentifier(tableType.Name);
        valid &= IsValidIdentifier(tableType.ClrName);
        valid &= tableType.Columns.Count is >= 1 and <= 1024;
        for (var index = 0; index < tableType.Columns.Count; index++)
        {
            var column = tableType.Columns[index];
            valid &= column.Ordinal == index && IsValidSqlIdentifier(column.Name);
            if (AutoContractsNaming.IsSupportedSqlType(column.SqlType))
                continue;

            valid = false;
        }

        return valid;
    }

    private static bool CanEmitProcedure(
        AutoContractsProcedure procedure,
        HashSet<string> tableTypes)
    {
        var valid = true;
        valid &= IsValidSqlIdentifier(procedure.Schema) && IsValidSqlIdentifier(procedure.Name);
        valid &= IsValidIdentifier(procedure.ClrName);
        valid &= IsValidIdentifier(procedure.ParametersClrName);
        valid &= procedure.Parameters.Count <= 2100;
        var expectedParameterOrdinal = procedure.Parameters.Count > 0 &&
                                       procedure.Parameters[0].Direction == "ReturnValue"
            ? 0
            : 1;
        foreach (var parameter in procedure.Parameters)
        {
            valid &= parameter.Ordinal == expectedParameterOrdinal++;
            valid &= IsValidSqlIdentifier(parameter.Name.TrimStart('@'));
        }

        valid &= procedure.ResultSets.Count is >= 1 and <= 10;
        var seenIndices = new HashSet<int>();
        for (var index = 0; index < procedure.ResultSets.Count; index++)
        {
            var resultSet = procedure.ResultSets[index];
            valid &= resultSet.Index == index && resultSet.Index is >= 0 and <= 9;
            valid &= seenIndices.Add(resultSet.Index);
            valid &= resultSet.ClrName is not { Length: > 0 } resultClrName || IsValidIdentifier(resultClrName);
            valid &= resultSet.Columns.Count <= 4096;
            valid &= index == procedure.ResultSets.Count - 1 ||
                     resultSet.Status is not ("None" or "Undetermined");
            for (var columnIndex = 0; columnIndex < resultSet.Columns.Count; columnIndex++)
                valid &= resultSet.Columns[columnIndex].Ordinal == columnIndex &&
                         IsValidSqlIdentifier(resultSet.Columns[columnIndex].Name);
        }

        foreach (var parameter in procedure.Parameters)
            valid &= CanEmitParameter(parameter, tableTypes);

        foreach (var resultSet in procedure.ResultSets)
        {
            foreach (var column in resultSet.Columns)
                valid &= AutoContractsNaming.IsSupportedSqlType(column.SqlType);
            valid &= !string.Equals(resultSet.Status, "Undetermined", StringComparison.OrdinalIgnoreCase);
        }

        return valid;
    }

    private static bool CanEmitParameter(
        AutoContractsParameter parameter,
        HashSet<string> tableTypes)
    {
        if (parameter.Direction is not ("Input" or "Output" or "InputOutput" or "ReturnValue"))
            return false;

        if (parameter.IsTableType)
            return parameter.Direction == "Input" && tableTypes.Contains(parameter.SqlType);

        if (parameter.Direction == "ReturnValue")
            return parameter.Ordinal == 0 &&
                   string.Equals(parameter.SqlType, "int", StringComparison.OrdinalIgnoreCase);

        return AutoContractsNaming.IsSupportedSqlType(parameter.SqlType);
    }

    private static string BuildFullName(string schema, string name)
    {
        return AutoContractsNaming.BuildSqlName(schema, name);
    }

    private static bool HasNoGenerationNameCollisions(AutoContractsManifest manifest)
    {
        var valid = true;
        var generatedTypes = new HashSet<string>(StringComparer.Ordinal);
        var tableSqlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableType in manifest.TableTypes)
        {
            valid &= tableSqlNames.Add(BuildFullName(tableType.Schema, tableType.Name));
            valid &= generatedTypes.Add(tableType.ClrName);
            valid &= HasNoMemberCollisions(tableType.Columns);
        }

        var procedureSqlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var procedure in manifest.Procedures)
        {
            valid &= procedureSqlNames.Add(BuildFullName(procedure.Schema, procedure.Name));
            valid &= generatedTypes.Add(procedure.ClrName);
            valid &= generatedTypes.Add(procedure.ParametersClrName);
            valid &= HasNoMemberCollisions(procedure.Parameters);

            foreach (var resultSet in procedure.ResultSets)
            {
                if (resultSet.ClrName is not { Length: > 0 } resultClrName)
                    continue;
                valid &= generatedTypes.Add(resultClrName);
                valid &= HasNoMemberCollisions(resultSet.Columns);
            }
        }

        return valid;
    }

    private static bool HasNoMemberCollisions(EquatableArray<AutoContractsColumn> columns)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in columns)
            if (!identifiers.Add(NormalizeIdentifier(column.Name)))
                return false;

        return true;
    }

    private static bool HasNoMemberCollisions(EquatableArray<AutoContractsParameter> parameters)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
            if (!identifiers.Add(NormalizeIdentifier(parameter.Name)))
                return false;

        return true;
    }

    private static bool IsValidNamespace(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            return false;

        var parts = @namespace.Split('.');
        foreach (var part in parts)
            if (!IsValidIdentifier(part))
                return false;

        return true;
    }

    private static bool IsValidIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier) &&
               SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None;
    }

    private static bool IsValidSqlIdentifier(string identifier)
    {
        if (identifier.Length is < 1 or > 128 ||
            (!IsAsciiLetter(identifier[0]) && identifier[0] != '_'))
            return false;

        for (var index = 1; index < identifier.Length; index++)
            if (!IsAsciiLetter(identifier[index]) && !char.IsDigit(identifier[index]) && identifier[index] != '_')
                return false;

        return true;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static string NormalizeIdentifier(string name)
    {
        return AutoContractsNaming.ToIdentifier(name).TrimStart('@');
    }
}
