namespace CaeriusNet.Generator.Dto.Models;

/// <summary>
///     Represents a record or class that should have DTO mapping functionality generated.
///     This class holds metadata about classes/records decorated with [GenerateDto] attribute.
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
    ///     These will be mapped from SQL reader buffer positions in the exact order they appear in this list.
    ///     The index in this list corresponds to the SQL buffer position.
    /// </summary>
    public List<DtoProperty> Properties { get; init; } = [];

    /// <summary>
    ///     Gets or sets a value indicating whether this is a record type (as opposed to a class).
    /// </summary>
    public bool IsRecord { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether this type has a primary constructor.
    ///     Records typically have primary constructors when parameters are declared inline.
    /// </summary>
    public bool HasPrimaryConstructor { get; init; }
}