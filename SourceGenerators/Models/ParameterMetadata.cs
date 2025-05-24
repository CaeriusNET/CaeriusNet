using Microsoft.CodeAnalysis;

namespace CaeriusNet.Generator.Models;

/// <summary>
///     Contains metadata about a constructor parameter in a DTO.
/// </summary>
public sealed class ParameterMetadata
{
	/// <summary>
	///     Creates a new instance of ParameterMetadata.
	/// </summary>
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
	///     The name of the parameter.
	/// </summary>
	public string Name { get; }

	/// <summary>
	///     The full type name of the parameter.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	///     The type symbol representing the parameter type.
	/// </summary>
	public ITypeSymbol TypeSymbol { get; }

	/// <summary>
	///     Indicates whether the parameter is nullable.
	/// </summary>
	public bool IsNullable { get; }

	/// <summary>
	///     The ordinal position of the parameter in the constructor.
	/// </summary>
	public int OrdinalPosition { get; }

	/// <summary>
	///     The SQL type corresponding to the C# type.
	/// </summary>
	public string SqlType { get; }

	/// <summary>
	///     The appropriate SqlDataReader method to use for this type.
	/// </summary>
	public string ReaderMethod { get; }

	/// <summary>
	///     Whether the type requires special conversion logic.
	/// </summary>
	public bool RequiresSpecialConversion { get; }
}