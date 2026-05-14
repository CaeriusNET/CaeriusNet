namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable description of a single primary-constructor parameter participating in source generation.
/// </summary>
/// <remarks>
///     <para>
///         All fields are <see cref="string" />, primitives, or value-typed enums — there are no Roslyn
///         <c>ISymbol</c> references — so instances flow safely through Roslyn's incremental pipeline
///         and are correctly cached when source code is unchanged.
///     </para>
///     <para>
///         <see cref="EnumUnderlyingType" /> is non-null only when <see cref="Kind" /> is <see cref="ColumnKind.Enum" />,
///         and contains the fully qualified name of the integral underlying type (e.g. <c>int</c>, <c>byte</c>).
///     </para>
/// </remarks>
internal sealed record ColumnModel(
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsNullableValueType,
    int OrdinalPosition,
    string SqlType,
    string ReaderMethod,
    string SqlMetaDataConstructor,
    ColumnKind Kind,
    string? EnumUnderlyingType);
