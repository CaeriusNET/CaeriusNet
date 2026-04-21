namespace CaeriusNet.Analyzer.Helpers;

/// <summary>
///     Minimal SQL mapping helper used by analyzer rules to mirror generator behavior.
/// </summary>
internal static class SqlTypeDetector
{
    internal static string GetSqlType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol
            {
                IsValueType: true,
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: > 0
            } nullableType)
            type = nullableType.TypeArguments[0];

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: not null } enumType)
            type = enumType.EnumUnderlyingType;

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "bit",
            SpecialType.System_Byte => "tinyint",
            SpecialType.System_SByte => "smallint",
            SpecialType.System_Int16 => "smallint",
            SpecialType.System_UInt16 => "int",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "bigint",
            SpecialType.System_Int64 => "bigint",
            SpecialType.System_UInt64 => "decimal",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Single => "real",
            SpecialType.System_Double => "float",
            SpecialType.System_String => "nvarchar",
            SpecialType.System_Char => "nchar",
            SpecialType.System_DateTime => "datetime2",
            _ => type.ToDisplayString() switch
            {
                "System.Guid" => "uniqueidentifier",
                "System.DateTimeOffset" => "datetimeoffset",
                "System.TimeSpan" => "time",
                "System.DateOnly" => "date",
                "System.TimeOnly" => "time",
                "System.Half" => "real",
                "byte[]" or "System.Byte[]" => "varbinary",
                _ => "sql_variant"
            }
        };
    }
}