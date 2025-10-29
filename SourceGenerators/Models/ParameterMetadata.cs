namespace CaeriusNet.Generator.Models;

/// <summary>
///     Represents metadata about a constructor parameter used in source generation.
/// </summary>
/// <remarks>
///     <para>
///         This immutable class encapsulates all information needed to generate code that maps
///         a constructor parameter to SQL Server data, including type information, nullability,
///         SQL type mapping, and special conversion requirements.
///     </para>
///     <para>
///         Instances are created during the semantic analysis phase and used during code generation
///         to produce appropriate SqlDataReader expressions and DataTable column definitions.
///     </para>
/// </remarks>
public sealed class ParameterMetadata
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ParameterMetadata" /> class.
	/// </summary>
	/// <param name="name">The parameter name.</param>
	/// <param name="typeName">The fully qualified type name as it appears in code.</param>
	/// <param name="typeSymbol">The Roslyn type symbol for semantic analysis.</param>
	/// <param name="isNullable">Indicates whether the parameter accepts null values.</param>
	/// <param name="ordinalPosition">The zero-based position of the parameter in the constructor.</param>
	/// <param name="sqlType">The corresponding SQL Server data type name.</param>
	/// <param name="readerMethod">The SqlDataReader method name for reading this type (e.g., "GetInt32").</param>
	/// <param name="requiresSpecialConversion">Indicates whether special conversion logic is needed (e.g., DateOnly, enums).</param>
	public ParameterMetadata(
		string name,
		string typeName,
		ITypeSymbol typeSymbol,
		bool isNullable,
		int ordinalPosition,
		string sqlType,
		string readerMethod,
		bool requiresSpecialConversion)
	{
		Name = name;
		TypeName = typeName;
		TypeSymbol = typeSymbol;
		IsNullable = isNullable;
		OrdinalPosition = ordinalPosition;
		SqlType = sqlType;
		ReaderMethod = readerMethod;
		RequiresSpecialConversion = requiresSpecialConversion;
	}

	/// <summary>
	///     Gets the name of the parameter.
	/// </summary>
	/// <value>The parameter name as declared in the constructor.</value>
	public string Name { get; }

	/// <summary>
	///     Gets the fully qualified type name of the parameter.
	/// </summary>
	/// <value>The type name as it should appear in generated code (e.g., "int?", "System.DateTime").</value>
	public string TypeName { get; }

	/// <summary>
	///     Gets the Roslyn type symbol representing the parameter type.
	/// </summary>
	/// <value>The <see cref="ITypeSymbol" /> for semantic analysis and type checking.</value>
	public ITypeSymbol TypeSymbol { get; }

	/// <summary>
	///     Gets a value indicating whether the parameter is nullable.
	/// </summary>
	/// <value>
	///     <see langword="true" /> if the parameter accepts null values (nullable value types or nullable reference types);
	///     otherwise, <see langword="false" />.
	/// </value>
	public bool IsNullable { get; }

	/// <summary>
	///     Gets the zero-based ordinal position of the parameter in the constructor.
	/// </summary>
	/// <value>The parameter's position, used for ordinal-based SqlDataReader column access.</value>
	/// <remarks>
	///     This position is critical for generating efficient data reader code that accesses columns by index.
	/// </remarks>
	public int OrdinalPosition { get; }

	/// <summary>
	///     Gets the SQL Server data type corresponding to the C# type.
	/// </summary>
	/// <value>The SQL type name (e.g., "int", "nvarchar", "datetime2").</value>
	public string SqlType { get; }

	/// <summary>
	///     Gets the SqlDataReader method name appropriate for reading this type.
	/// </summary>
	/// <value>The reader method name without the "reader." prefix (e.g., "GetInt32", "GetString").</value>
	public string ReaderMethod { get; }

	/// <summary>
	///     Gets a value indicating whether the type requires special conversion logic.
	/// </summary>
	/// <value>
	///     <see langword="true" /> if the type needs custom conversion (e.g., <see cref="DateOnly" />, <see cref="TimeOnly" />
	///     , enums, byte arrays);
	///     otherwise, <see langword="false" />.
	/// </value>
	public bool RequiresSpecialConversion { get; }
}