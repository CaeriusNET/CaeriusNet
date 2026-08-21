namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsClrTypes
{
    internal static bool IsNullableValueType(string clrType)
    {
        var normalized = clrType.TrimEnd('?');
        return normalized is "bool" or "byte" or "short" or "int" or "long" or "float" or "double" or "decimal" or
            "DateOnly" or "TimeOnly" or "DateTime" or "DateTimeOffset" or "Guid" or
            "SqlDecimal" or "System.Data.SqlTypes.SqlDecimal" or
            "global::Microsoft.Data.SqlTypes.SqlVector<float>" or "Microsoft.Data.SqlTypes.SqlVector<float>";
    }

    internal static bool IsReferenceType(string clrType)
    {
        var normalized = clrType.TrimEnd('?');
        return normalized is "string" or "byte[]" or "object" || normalized.Contains("<");
    }

    internal static bool IsSqlDecimal(string clrType)
    {
        return clrType.TrimEnd('?') is "SqlDecimal" or "System.Data.SqlTypes.SqlDecimal";
    }
}
