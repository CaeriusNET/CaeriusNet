namespace CaeriusNet.Generator.Dto.Models;

public sealed class DtoProperty
{
	public string Name { get; init; } = string.Empty;
	public string TypeName { get; init; } = string.Empty;
	public string SqlTypeName { get; init; } = string.Empty;
	public bool IsNullable { get; init; }
}