namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsEmissionGuard
{
    internal static bool CanEmit(AutoContractsManifest manifest)
    {
        var valid = true;
        var tableTypes = BuildTableTypeLookup(manifest);

        valid &= manifest.Version == AutoContractsManifestParser.SupportedManifestVersion;
        valid &= IsValidNamespace(manifest.Namespace);
        valid &= HasNoGenerationNameCollisions(manifest);

        foreach (var tableType in manifest.TableTypes)
            valid &= CanEmitTableType(tableType);

        foreach (var procedure in manifest.Procedures)
            valid &= CanEmitProcedure(procedure, tableTypes);

        return valid;
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
        valid &= IsValidIdentifier(tableType.ClrName);
        foreach (var column in tableType.Columns)
        {
            if (AutoContractsSqlEmitter.IsSupportedSqlType(column.SqlType))
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
        valid &= IsValidIdentifier(procedure.ClrName);
        valid &= IsValidIdentifier(procedure.ParametersClrName);
        valid &= procedure.ResultClrName is not { Length: > 0 } resultClrName ||
                 IsValidIdentifier(resultClrName);

        foreach (var parameter in procedure.Parameters)
            valid &= CanEmitParameter(parameter, tableTypes);

        foreach (var column in procedure.ResultSet.Columns)
            valid &= AutoContractsSqlEmitter.IsSupportedSqlType(column.SqlType);

        valid &= !string.Equals(procedure.ResultSet.Status, "Undetermined", StringComparison.OrdinalIgnoreCase);
        return valid;
    }

    private static bool CanEmitParameter(
        AutoContractsParameter parameter,
        HashSet<string> tableTypes)
    {
        if (parameter.IsOutput)
            return false;

        if (parameter.IsTableType)
            return tableTypes.Contains(parameter.SqlType);

        return AutoContractsSqlEmitter.IsSupportedSqlType(parameter.SqlType);
    }

    private static string BuildFullName(string schema, string name)
    {
        return schema + "." + name;
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

            if (procedure.ResultClrName is { Length: > 0 } resultClrName)
            {
                valid &= generatedTypes.Add(resultClrName);
                valid &= HasNoMemberCollisions(procedure.ResultSet.Columns);
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

    private static string NormalizeIdentifier(string name)
    {
        return AutoContractsSqlEmitter.ToIdentifier(name).TrimStart('@');
    }
}
