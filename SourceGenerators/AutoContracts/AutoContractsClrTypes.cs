namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsClrTypes
{
    internal static bool IsNullableValueType(string clrType)
    {
        var normalized = clrType.TrimEnd('?');
        return normalized is "bool" or "byte" or "short" or "int" or "long" or "float" or "double" or "decimal" or
            "DateOnly" or "TimeOnly" or "DateTime" or "DateTimeOffset" or "Guid";
    }

    internal static bool IsReferenceType(string clrType)
    {
        var normalized = clrType.TrimEnd('?');
        return normalized is "string" or "byte[]" or "object" || normalized.Contains("<");
    }
}
