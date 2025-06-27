namespace CaeriusNet.Attributes.Dto;

/// <summary>
///     Identifies a DTO class or record for which a strongly-typed <see cref="ISpMapper{T}" /> implementation
///     should be automatically generated at compile time. This attribute enables seamless, type-safe
///     data mapping from SQL Server stored procedure result sets directly into annotated .NET types.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>The target type must be declared as <c>sealed</c> and <c>partial</c>.</item>
///         <item>
///             Constructor parameters define the mapping order — positionally match C# parameters with SQL result
///             columns (0, 1, 2, ...).
///         </item>
///         <item>Use nullable types to accept possible <c>NULL</c> values from SQL.</item>
///         <item>
///             Only supported primitive types, value types, <c>string</c>, and <c>byte[]</c> are permitted as
///             parameters.
///         </item>
///     </list>
///     <para>
///         Unsupported features (will be ignored or cause errors): custom complex properties, properties outside the
///         primary constructor, mapping by property name, or modifying mapped members.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// [GenerateDto]
/// public sealed partial record ProductDto(int Id, string Name, decimal Price, string? Description, DateTime? DiscontinuedAt);
/// </code>
/// </example>
/// <para>
///     <b>Instructions:</b><br />
///     1. Decorate your <c>sealed partial</c> record with <c>[GenerateDto]</c>.<br />
///     2. Add only supported and mapped parameters in the primary constructor, maintaining correct buffer order.<br />
///     3. The generated <c>ISpMapper&lt;T&gt;</c> implementation appears at compile time.
/// </para>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateDtoAttribute : Attribute
{
}