namespace CaeriusNet.Generator.Dto.Models;

/// <summary>
///     Represents a record or class that should have DTO mapping functionality generated.
/// </summary>
public sealed class DtoRecord
{
    /// <summary>
    ///     Gets or sets the simple name of the record/class.
    /// </summary>
    public string RecordTypeName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the fully qualified name of the record/class.
    /// </summary>
    public string RecordFullName { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the namespace that contains the record/class.
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the list of properties or constructor parameters for the record/class.
    /// </summary>
    public List<DtoProperty> Properties { get; init; } = [];
}