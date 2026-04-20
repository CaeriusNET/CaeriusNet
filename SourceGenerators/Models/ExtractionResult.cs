namespace CaeriusNet.Generator.Models;

/// <summary>
///     Generic pipeline result carrying an optional extracted <typeparamref name="TModel" /> alongside any
///     diagnostics produced during extraction. Both fields are value-equatable so the wrapper is too.
/// </summary>
/// <remarks>
///     <para>
///         When <see cref="Model" /> is <see langword="null" /> the user-facing type was non-conformant and
///         only diagnostics should be reported. When <see cref="Model" /> is non-null and
///         <see cref="Diagnostics" /> is empty, the emitter can produce source unconditionally.
///     </para>
/// </remarks>
internal sealed record ExtractionResult<TModel>(TModel? Model, EquatableArray<DiagnosticInfo> Diagnostics)
    where TModel : class, IEquatable<TModel>
{
    public static ExtractionResult<TModel> None { get; } =
        new(null, EquatableArray<DiagnosticInfo>.Empty);

    public bool HasModel => Model is not null;
}