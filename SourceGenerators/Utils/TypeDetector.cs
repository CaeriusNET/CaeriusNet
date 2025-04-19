using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CaeriusNet.Generator.Utils;

/// <summary>
///     Service responsible for detecting and analyzing C# types for SQL mapping.
/// </summary>
public static class TypeDetector
{
	/// <summary>
	///     Checks if the given type is a Nullable&lt;T&gt; type.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>True if the type is Nullable&lt;T&gt;, otherwise False.</returns>
	private static bool IsNullableType(ITypeSymbol type)
	{
		return type is INamedTypeSymbol
		{
			IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
		};
	}

	/// <summary>
	///     Checks if the given type is a reference type (class, interface, delegate, etc.).
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>True if the type is a reference type, otherwise False.</returns>
	private static bool IsReferenceType(ITypeSymbol type)
	{
		return !type.IsValueType;
	}

	/// <summary>
	///     Gets the SQL type corresponding to the specified C# type.
	/// </summary>
	/// <param name="type">The C# type to map to an SQL type.</param>
	/// <returns>Name of the corresponding SQL type.</returns>
	public static string GetSqlType(ITypeSymbol type)
	{
		// If it's a Nullable<T>, get its underlying type
		if (IsNullableType(type) && type is INamedTypeSymbol namedType)
			type = namedType.TypeArguments[0];

		// Determine the SQL type based on the C# type
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
				_ => "sql_variant" // Most flexible SQL type by default
			}
		};
	}

	/// <summary>
	///     Determines if the type is explicitly marked as nullable in the syntax.
	/// </summary>
	/// <param name="typeSyntax">The type syntax to analyze.</param>
	/// <returns>True if the type ends with "?", indicating it is nullable.</returns>
	private static bool IsExplicitlyNullableInSyntax(TypeSyntax? typeSyntax)
	{
		// Check if the type ends with "?" in the source code
		return typeSyntax != null && typeSyntax.ToString().EndsWith("?");
	}

	/// <summary>
	///     Complete determination of a type's nullability taking into account all sources of information.
	/// </summary>
	/// <param name="type">The type symbol to analyze.</param>
	/// <param name="typeSyntax">The type syntax (optional).</param>
	/// <param name="nullableAnnotation">The type's nullable annotation (optional).</param>
	/// <returns>True if the type can accept null, otherwise False.</returns>
	public static bool IsTypeNullable(
		ITypeSymbol type,
		TypeSyntax? typeSyntax = null,
		NullableAnnotation? nullableAnnotation = null)
	{
		// 1. Check for explicit nullable annotation
		if (nullableAnnotation is NullableAnnotation.Annotated)
			return true;

		// 2. Check if it's a Nullable<T>
		if (IsNullableType(type))
			return true;

		// 3. Check if it's a reference type (unless explicitly non-nullable)
		if (IsReferenceType(type) && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
			return true;

		// 4. Check if the syntax uses "?" explicitly
		return typeSyntax != null && IsExplicitlyNullableInSyntax(typeSyntax);
		// By default, consider that the type is not nullable
	}

	/// <summary>
	///     Gets the appropriate SqlDataReader method for a specific SQL type.
	/// </summary>
	/// <param name="sqlType">The SQL type for which to determine the reader method.</param>
	/// <returns>Name of the reader method to use in SqlDataReader.</returns>
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
			"nvarchar" => "GetString",
			"nchar" => "GetString",
			"varchar" => "GetString",
			"char" => "GetString",
			"text" => "GetString",
			"datetime" => "GetDateTime",
			"datetime2" => "GetDateTime",
			"date" => "GetDateTime",
			"smalldatetime" => "GetDateTime",
			"uniqueidentifier" => "GetGuid",
			"datetimeoffset" => "GetDateTimeOffset",
			"time" => "GetTimeSpan",
			"varbinary" => "GetValue", // Special conversion required
			"binary" => "GetValue", // Special conversion required
			"image" => "GetValue", // Special conversion required
			_ => "GetValue" // Default method
		};
	}

	/// <summary>
	///     Checks if the type requires a special conversion that can't be handled directly
	///     by the standard SqlDataReader methods.
	/// </summary>
	/// <param name="typeName">The full name of the type.</param>
	/// <returns>True if the type requires special conversion, otherwise False.</returns>
	public static bool RequiresSpecialConversion(string typeName)
	{
		// Extract the base type if it's a Nullable<T>
		var baseTypeName = ExtractBaseTypeFromNullable(typeName);

		// Types requiring special conversions
		return baseTypeName is
			"System.DateOnly" or "DateOnly" or
			"System.TimeOnly" or "TimeOnly" or
			"byte[]" or "System.Byte[]";
	}

	/// <summary>
	///     Extracts the base type from a Nullable type.
	/// </summary>
	/// <param name="typeName">The full name of the type.</param>
	/// <returns>The base type or the original type if it's not a Nullable.</returns>
	private static string ExtractBaseTypeFromNullable(string typeName)
	{
		// If it's a Nullable<T>, extract T
		return typeName.StartsWith("System.Nullable<") ? typeName.Substring(16, typeName.Length - 17) : typeName;
	}
}