namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable description of a <c>[GenerateDto]</c> target, ready for emission.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="TypeKindKeyword" /> is the C# keyword to emit on the partial declaration — either
///         <c>"class"</c> or <c>"record"</c>. <see cref="Namespace" /> is empty for the global namespace
///         (in which case the emitter omits a <c>namespace</c> directive entirely).
///     </para>
/// </remarks>
internal sealed record DtoModel(
    string Namespace,
    string TypeName,
    string AccessibilityKeyword,
    string TypeKindKeyword,
    EquatableArray<ColumnModel> Columns);
