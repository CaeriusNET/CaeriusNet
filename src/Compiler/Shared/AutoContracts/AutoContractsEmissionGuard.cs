using CaeriusNet.Analyzer.Roslyn;

namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsEmissionGuard
{
    internal static bool CanEmit(AutoContractsManifest manifest)
    {
        return GetValidationErrors(manifest).Length == 0;
    }

    internal static ImmutableArray<string> GetValidationErrors(AutoContractsManifest manifest)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var tableTypes = BuildTableTypeLookup(manifest);

        if (manifest.Version != AutoContractsManifestParser.SupportedManifestVersion)
            errors.Add($"The manifest must declare version {AutoContractsManifestParser.SupportedManifestVersion}.");
        if (!IsValidNamespace(manifest.Namespace))
            errors.Add("The manifest namespace is not a valid C# namespace.");
        if (!HasNoGenerationNameCollisions(manifest))
            errors.Add("The manifest contains duplicate SQL or generated C# names.");

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
        valid &= IsValidIdentifier(procedure.ClrName);
        valid &= IsValidIdentifier(procedure.ParametersClrName);
        valid &= procedure.ResultSets.Count is >= 1 and <= 10;
        var seenIndices = new HashSet<int>();
        for (var index = 0; index < procedure.ResultSets.Count; index++)
        {
            var resultSet = procedure.ResultSets[index];
            valid &= resultSet.Index == index && resultSet.Index is >= 0 and <= 9;
            valid &= seenIndices.Add(resultSet.Index);
            valid &= resultSet.ClrName is not { Length: > 0 } resultClrName || IsValidIdentifier(resultClrName);
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

        if (parameter.Direction == "ReturnValue" && parameter.Ordinal != 0)
            return false;

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

    private static string NormalizeIdentifier(string name)
    {
        return AutoContractsNaming.ToIdentifier(name).TrimStart('@');
    }
}
