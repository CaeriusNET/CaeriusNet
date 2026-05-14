namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable description of a <c>[GenerateTvp]</c> target, ready for emission.
/// </summary>
/// <remarks>
///     <see cref="Namespace" /> is empty for the global namespace. <see cref="Schema" /> falls back to
///     <c>"dbo"</c> at extract time when not supplied. <see cref="TvpName" /> is guaranteed non-empty
///     before emission.
/// </remarks>
internal sealed record TvpModel(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string TypeKindKeyword,
    string Schema,
    string TvpName,
    EquatableArray<ColumnModel> Columns);
