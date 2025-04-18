namespace CaeriusNet.Generator.Dto.Models;

/// <summary>
///     Represents a property or parameter in a DTO class/record that will be mapped from a SqlDataReader.
/// </summary>
public sealed class DtoProperty
{
	/// <summary>
	///     Gets or sets the name of the property/parameter.
	/// </summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>
	///     Gets or sets the fully qualified type name of the property/parameter.
	/// </summary>
	public string TypeName { get; init; } = string.Empty;

	/// <summary>
	///     Gets or sets the SQL type name that corresponds to this property.
	/// </summary>
	public string SqlTypeName { get; init; } = string.Empty;

	/// <summary>
	///     Gets or sets a value indicating whether this property can accept null values.
	/// </summary>
	public bool IsNullable { get; init; }
}