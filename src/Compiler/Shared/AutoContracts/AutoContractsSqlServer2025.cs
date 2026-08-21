namespace CaeriusNet.Analyzer.AutoContracts;

/// <summary>
///     Centralizes the deliberately opt-in SQL Server 2025 surface shared by the analyzer and generator.
///     The source is linked into both Roslyn components so they cannot accept different manifest shapes.
/// </summary>
internal static class AutoContractsSqlServer2025
{
    internal const string MsBuildPropertyName = "build_property.CaeriusSqlServer2025Types";
    internal const string FeatureName = "SqlServer2025Types";

    internal static bool IsNativeType(string sqlType)
    {
        return AutoContractsNaming.NormalizeSqlType(sqlType) is "json" or "vector";
    }

    internal static bool IsEnabled(AnalyzerConfigOptions options)
    {
        return options.TryGetValue(MsBuildPropertyName, out var value) &&
               string.Equals(value?.Trim(), "Enabled", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsManifestCapable(AutoContractsManifest manifest)
    {
        return manifest.ServerMajor == 17 && manifest.Features.Any(static feature =>
            string.Equals(feature, FeatureName, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool CanUse(AutoContractsManifest manifest, bool isEnabled)
    {
        return isEnabled && IsManifestCapable(manifest);
    }

    internal static bool ContainsNativeTypes(AutoContractsManifest manifest)
    {
        foreach (var tableType in manifest.TableTypes)
            if (tableType.Columns.Any(static column => IsNativeType(column.SqlType)))
                return true;

        foreach (var procedure in manifest.Procedures)
        {
            if (procedure.Parameters.Any(static parameter => !parameter.IsTableType && IsNativeType(parameter.SqlType)))
                return true;
            foreach (var resultSet in procedure.ResultSets)
                if (resultSet.Columns.Any(static column => IsNativeType(column.SqlType)))
                    return true;
        }

        return false;
    }

    internal static bool ContainsNativeTvpTypes(AutoContractsManifest manifest)
    {
        return manifest.TableTypes.Any(static tableType =>
            tableType.Columns.Any(static column => IsNativeType(column.SqlType)));
    }

    internal static bool TryGetVectorDimensions(string sqlType, out int dimensions)
    {
        dimensions = 0;
        var span = sqlType.AsSpan().Trim();
        var open = span.IndexOf('(');
        if (open < 0 || span.Length < open + 3 || span[span.Length - 1] != ')')
            return false;

        var typeName = AutoContractsNaming.NormalizeSqlType(span.Slice(0, open).ToString());
        if (!string.Equals(typeName, "vector", StringComparison.Ordinal))
            return false;

        var dimensionText = span.Slice(open + 1, span.Length - open - 2).Trim().ToString();
        return int.TryParse(dimensionText, NumberStyles.None, CultureInfo.InvariantCulture, out dimensions) &&
               dimensions is >= 1 and <= 1998;
    }

    internal static bool HasOnlyValidVectorDimensions(AutoContractsManifest manifest)
    {
        foreach (var tableType in manifest.TableTypes)
        foreach (var column in tableType.Columns)
            if (AutoContractsNaming.NormalizeSqlType(column.SqlType) == "vector" &&
                !TryGetVectorDimensions(column.SqlType, out _))
                return false;

        foreach (var procedure in manifest.Procedures)
        {
            foreach (var parameter in procedure.Parameters)
                if (!parameter.IsTableType && AutoContractsNaming.NormalizeSqlType(parameter.SqlType) == "vector" &&
                    !TryGetVectorDimensions(parameter.SqlType, out _))
                    return false;
            foreach (var resultSet in procedure.ResultSets)
            foreach (var column in resultSet.Columns)
                if (AutoContractsNaming.NormalizeSqlType(column.SqlType) == "vector" &&
                    !TryGetVectorDimensions(column.SqlType, out _))
                    return false;
        }

        return true;
    }
}
