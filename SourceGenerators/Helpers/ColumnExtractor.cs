namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Builds value-equatable <see cref="ColumnModel" /> instances from <see cref="IParameterSymbol" />s.
///     Centralises every C#→SQL mapping decision so DTO and TVP generators share identical behaviour.
/// </summary>
internal static class ColumnExtractor
{
    /// <summary>
    ///     Projects a primary-constructor parameter onto a fully populated <see cref="ColumnModel" />.
    /// </summary>
    /// <param name="parameter">The constructor parameter symbol resolved from the semantic model.</param>
    /// <param name="ordinal">The zero-based ordinal of the parameter (drives column index).</param>
    /// <returns>An immutable, value-equatable model ready to flow through the incremental pipeline.</returns>
    internal static ColumnModel Extract(IParameterSymbol parameter, int ordinal)
    {
        var parameterType = parameter.Type;
        var typeDisplay = parameterType.ToDisplayString();
        var unwrapped = UnwrapNullable(parameterType);
        var isNullable = TypeDetector.IsTypeNullable(parameterType, nullableAnnotation: parameter.NullableAnnotation);
        var isNullableValueType = parameterType is INamedTypeSymbol
        {
            IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        };

        var kind = ClassifyKind(unwrapped);
        string? enumUnderlying = null;
        var sqlMappingType = unwrapped;

        if (kind == ColumnKind.Enum && unwrapped is INamedTypeSymbol enumType &&
            enumType.EnumUnderlyingType is not null)
        {
            sqlMappingType = enumType.EnumUnderlyingType;
            enumUnderlying = enumType.EnumUnderlyingType.ToDisplayString();
        }

        var sqlType = TypeDetector.GetSqlType(sqlMappingType);
        var readerMethod = TypeDetector.GetReaderMethodForSqlType(sqlType);
        var sqlMetaDataCtor = SqlMetaDataExpressionBuilder.Build(parameter.Name, sqlType);

        return new ColumnModel(
            parameter.Name,
            typeDisplay,
            isNullable,
            isNullableValueType,
            ordinal,
            sqlType,
            readerMethod,
            sqlMetaDataCtor,
            kind,
            enumUnderlying);
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol
            {
                IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: > 0
            } named)
            return named.TypeArguments[0];

        return type;
    }

    private static ColumnKind ClassifyKind(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
            return ColumnKind.Enum;

        if (type.SpecialType == SpecialType.System_Char)
            return ColumnKind.Char;

        // byte[] is special: SqlDataReader exposes it via GetValue, and we cannot use SetBytes streaming
        // through a single SqlDataRecord without state. Using SetValue keeps the contract simple.
        if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            return ColumnKind.ByteArray;

        // DateOnly / TimeOnly require manual conversion: SQL Server has no native CLR mapping for them.
        var fullName = type.ToDisplayString();
        return fullName switch
        {
            "System.DateOnly" => ColumnKind.DateOnly,
            "System.TimeOnly" => ColumnKind.TimeOnly,
            "System.Half" => ColumnKind.Half,
            _ => ColumnKind.Standard
        };
    }
}
