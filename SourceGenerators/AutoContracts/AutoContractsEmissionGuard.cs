namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsEmissionGuard
{
    internal static bool CanEmit(AutoContractsManifest manifest)
    {
        var valid = true;
        var tableTypes = BuildTableTypeLookup(manifest);

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
}
