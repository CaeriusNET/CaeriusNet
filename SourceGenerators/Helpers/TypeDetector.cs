namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Provides high-performance type detection and SQL mapping services for C# types.
/// </summary>
/// <remarks>
///     This static class offers thread-safe, allocation-efficient methods for analyzing C# type symbols
///     and mapping them to their corresponding SQL Server types. It leverages C# 8+ features including
///     pattern matching, nullable reference types, and span-based operations for optimal performance
///     in source generator scenarios.
/// </remarks>
internal static class TypeDetector
{
	/// <summary>
	///     Determines whether the specified type is a nullable value type (Nullable&lt;T&gt;).
	/// </summary>
	/// <param name="type">The type symbol to examine.</param>
	/// <returns>
	///     <see langword="true" /> if the type is <see cref="Nullable{T}" />; otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     This method uses pattern matching to efficiently check if the type is a value type
	///     with the special type <see cref="SpecialType.System_Nullable_T" />.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNullableType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol
        {
            IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
        };
    }

	/// <summary>
	///     Determines whether the specified type is a reference type.
	/// </summary>
	/// <param name="type">The type symbol to examine.</param>
	/// <returns>
	///     <see langword="true" /> if the type is a reference type; otherwise, <see langword="false" />.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsReferenceType(ITypeSymbol type)
    {
        return !type.IsValueType;
    }

	/// <summary>
	///     Determines whether the specified type is an enumeration type.
	/// </summary>
	/// <param name="type">The type symbol to examine.</param>
	/// <returns>
	///     <see langword="true" /> if the type is an enum; otherwise, <see langword="false" />.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEnumType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Enum;
    }

	/// <summary>
	///     Retrieves the underlying integral type of an enumeration.
	/// </summary>
	/// <param name="type">The enum type symbol to analyze.</param>
	/// <returns>
	///     The underlying type symbol of the enum (e.g., <see cref="int" />, <see cref="byte" />),
	///     or <see langword="null" /> if the type is not an enum.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ITypeSymbol? GetEnumUnderlyingType(ITypeSymbol type)
    {
        return IsEnumType(type) ? ((INamedTypeSymbol)type).EnumUnderlyingType : null;
    }

	/// <summary>
	///     Maps a C# type to its corresponding SQL Server data type.
	/// </summary>
	/// <param name="type">The type symbol to map.</param>
	/// <returns>
	///     A string representing the SQL Server data type name (e.g., "int", "nvarchar", "datetime2").
	///     Returns "sql_variant" for unmapped types.
	/// </returns>
	/// <remarks>
	///     This method handles nullable types, enums, and special BCL types like <see cref="DateOnly" /> and
	///     <see cref="TimeOnly" />.
	///     Enum types are mapped based on their underlying integral type.
	/// </remarks>
	public static string GetSqlType(ITypeSymbol type)
    {
        // Handle nullable types
        if (IsNullableType(type) && type is INamedTypeSymbol namedType)
            type = namedType.TypeArguments[0];

        // Handle enums
        if (IsEnumType(type))
            type = GetEnumUnderlyingType(type) ?? type;

        // Map types to SQL types
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
            _ => type.ToString() switch
            {
                "System.Guid" => "uniqueidentifier",
                "System.DateTimeOffset" => "datetimeoffset",
                "System.TimeSpan" => "time",
                "System.DateOnly" => "date",
                "System.TimeOnly" => "time",
                "byte[]" or "System.Byte[]" => "varbinary",
                _ => "sql_variant"
            }
        };
    }

	/// <summary>
	///     Checks if a type is explicitly marked as nullable in the syntax (e.g., "int?", "string?").
	/// </summary>
	/// <param name="typeSyntax">The type syntax node to examine.</param>
	/// <returns>
	///     <see langword="true" /> if the syntax explicitly includes a nullable annotation (?);
	///     otherwise, <see langword="false" />.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsExplicitlyNullableInSyntax(TypeSyntax? typeSyntax)
    {
        if (typeSyntax is null)
            return false;

        var typeText = typeSyntax.ToString();
        return typeText.Length > 0 && typeText[typeText.Length - 1] == '?';
    }

	/// <summary>
	///     Determines whether a type is nullable considering multiple C# nullability mechanisms.
	/// </summary>
	/// <param name="type">The type symbol to examine.</param>
	/// <param name="typeSyntax">Optional type syntax for explicit nullability check.</param>
	/// <param name="nullableAnnotation">Optional nullable annotation from the semantic model.</param>
	/// <returns>
	///     <see langword="true" /> if the type accepts null values; otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     This method considers:
	///     <list type="bullet">
	///         <item>
	///             <description>Nullable value types (Nullable&lt;T&gt;)</description>
	///         </item>
	///         <item>
	///             <description>Nullable reference types (C# 8+)</description>
	///         </item>
	///         <item>
	///             <description>Explicit nullable syntax annotations (?)</description>
	///         </item>
	///         <item>
	///             <description>Semantic nullable annotations</description>
	///         </item>
	///     </list>
	/// </remarks>
	public static bool IsTypeNullable(ITypeSymbol type, TypeSyntax? typeSyntax = null,
        NullableAnnotation? nullableAnnotation = null)
    {
        return nullableAnnotation is NullableAnnotation.Annotated
               || IsNullableType(type)
               || (IsReferenceType(type) && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
               || IsExplicitlyNullableInSyntax(typeSyntax);
    }

	/// <summary>
	///     Gets the appropriate <see cref="System.Data.SqlClient.SqlDataReader" /> method name for reading a SQL type.
	/// </summary>
	/// <param name="sqlType">The SQL Server data type name.</param>
	/// <returns>
	///     The name of the SqlDataReader method to use (e.g., "GetInt32", "GetString", "GetDateTime").
	///     Returns "GetValue" for unmapped or variant types.
	/// </returns>
	/// <remarks>
	///     This method maps SQL Server types to their corresponding strongly-typed reader methods
	///     for optimal performance when reading data from result sets.
	/// </remarks>
	public static string GetReaderMethodForSqlType(string sqlType)
    {
        return sqlType switch
        {
            "bit" => "GetBoolean",
            "tinyint" => "GetByte",
            "smallint" => "GetInt16",
            "int" => "GetInt32",
            "bigint" => "GetInt64",
            "decimal" => "GetDecimal",
            "real" => "GetFloat",
            "float" => "GetDouble",
            "nvarchar" or "nchar" or "varchar" or "char" or "text" => "GetString",
            "datetime" or "datetime2" or "date" or "smalldatetime" => "GetDateTime",
            "uniqueidentifier" => "GetGuid",
            "datetimeoffset" => "GetDateTimeOffset",
            "time" => "GetTimeSpan",
            _ => "GetValue"
        };
    }

	/// <summary>
	///     Determines whether a type requires special conversion logic beyond direct SqlDataReader calls.
	/// </summary>
	/// <param name="typeName">The fully qualified type name to examine.</param>
	/// <returns>
	///     <see langword="true" /> if the type requires custom conversion (e.g., <see cref="DateOnly" />,
	///     <see cref="TimeOnly" />, byte arrays);
	///     otherwise, <see langword="false" />.
	/// </returns>
	/// <remarks>
	///     Special conversion is needed for types like:
	///     <list type="bullet">
	///         <item>
	///             <description><see cref="DateOnly" /> - converted from DateTime</description>
	///         </item>
	///         <item>
	///             <description><see cref="TimeOnly" /> - converted from DateTime</description>
	///         </item>
	///         <item>
	///             <description>byte[] - requires casting from object</description>
	///         </item>
	///     </list>
	/// </remarks>
	public static bool RequiresSpecialConversion(string typeName)
    {
        var baseTypeName = ExtractBaseTypeFromNullable(typeName.AsSpan());

        return baseTypeName switch
        {
            "System.DateOnly" or "DateOnly" => true,
            "System.TimeOnly" or "TimeOnly" => true,
            "byte[]" or "System.Byte[]" => true,
            _ => false
        };
    }

	/// <summary>
	///     Extracts the base type name from a potentially nullable type representation.
	/// </summary>
	/// <param name="typeName">The type name span to process.</param>
	/// <returns>
	///     A span representing the base type name without the <see cref="Nullable{T}" /> wrapper.
	/// </returns>
	/// <remarks>
	///     This method efficiently extracts the inner type from "System.Nullable&lt;T&gt;" syntax
	///     using span operations to avoid string allocations.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> ExtractBaseTypeFromNullable(ReadOnlySpan<char> typeName)
    {
        const string nullablePrefix = "System.Nullable<";

        if (typeName.Length > nullablePrefix.Length + 1 &&
            typeName.StartsWith(nullablePrefix.AsSpan()) &&
            typeName[typeName.Length - 1] == '>')
            return typeName.Slice(nullablePrefix.Length, typeName.Length - nullablePrefix.Length - 1);

        return typeName;
    }
}