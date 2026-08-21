namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsSqlTypes
{
    internal static bool IsSupported(string sqlType)
    {
        return AutoContractsNaming.IsSupportedSqlType(sqlType);
    }

    internal static string Normalize(string sqlType)
    {
        return AutoContractsNaming.NormalizeSqlType(sqlType);
    }
}
