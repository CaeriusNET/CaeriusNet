namespace CaeriusNet.Attributes.Tvp;

/// <summary>
///     Identifies a class or record for which a strongly-typed <see cref="ITvpMapper{T}" /> implementation
///     should be automatically generated at compile time. This attribute enables seamless conversion
///     of .NET objects into SQL Server Table-Valued Parameters (TVP).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>The target type must be declared as <c>sealed</c> and <c>partial</c>.</item>
///         <item>
///             Constructor parameters define the columns in the DataTable — each parameter becomes a column in the
///             generated TVP.
///         </item>
///         <item>Use nullable types to handle possible <c>NULL</c> values.</item>
///         <item>
///             Supported types: all primitive types, value types, <c>string</c>, <c>byte[]</c>, and other SQL Server
///             compatible types.
///         </item>
///         <item>Both <c>TvpName</c> (required) and <c>Schema</c> (defaults to "dbo") must be specified.</item>
///     </list>
/// </remarks>
/// <example>
///     <code>
/// [GenerateTvp(TvpName = "tvp_Product")]
/// public sealed partial record Product(int Id, string Name, decimal Price, string? Description, DateTime? DiscontinuedAt);
///
/// [GenerateTvp(TvpName = "tvp_Order", Schema = "Sales")]
/// public sealed partial record Order(int OrderId, DateTime OrderDate, decimal Total);
/// </code>
/// </example>
/// <para>
///     <b>Instructions:</b><br />
///     1. Decorate your <c>sealed partial</c> class or record with <c>[GenerateTvp]</c>.<br />
///     2. Specify the required TVP name: <c>TvpName = "tvp_MyType"</c>.<br />
///     3. Optionally specify the schema: <c>Schema = "MySchema"</c> (defaults to "dbo").<br />
///     4. Add parameters to the primary constructor representing the desired columns in your TVP.<br />
///     5. The generated <c>ITvpMapper&lt;T&gt;</c> implementation appears at compile time with the fully qualified name <c>[Schema].[TvpName]</c>.
/// </para>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateTvpAttribute : Attribute
{
	/// <summary>
	///     Gets or sets the SQL Server schema name to use for this TVP.
	///     Defaults to "dbo" if not specified.
	/// </summary>
	public required string Schema { get; init; } = "dbo";

	/// <summary>
	///     Gets or sets the SQL Server table type name (without schema) to use for this TVP.
	///     This property is required and must be specified by the user.
	/// </summary>
	public required string TvpName { get; init; }
}