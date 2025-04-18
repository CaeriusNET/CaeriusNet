namespace CaeriusNet.Attributes;

/// <summary>
///     Instructs the source generator to generate an <c>ISpMapper</c> implementation for the annotated class or record.
///     This enables automatic mapping from SQL Server stored procedure results to strongly-typed DTOs.
/// </summary>
/// <remarks>
///     The target type must be declared as <c>partial</c> to allow the source generator to augment it
///     with the generated <see cref="ISpMapper{T}" /> implementation.
/// </remarks>
/// <example>
///     <code>
/// [GenerateDto]
/// public sealed partial record UserDto(int UserId, Guid UserGuid,
/// string UserName, DateOnly UserBirthday, DateTime? UserCreatedAt);
/// </code>
/// </example>
/// <para>
///     <b>Usage Instructions:</b>
/// </para>
/// <para>Follow these steps to enable automatic mapping:</para>
/// <para>1. Apply the <c>[GenerateDto]</c> attribute to a sealed <c>partial</c> record or class.</para>
/// <para>2. The constructor parameters will be mapped in order from the SQL buffer position (0, 1, 2, ...).</para>
/// <para>3. Use nullable types for parameters that may receive <c>NULL</c> values.</para>
/// <para>4. The source generator will automatically implement <c>ISpMapper</c> for the decorated type.</para>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateDtoAttribute : Attribute {}