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
///     </list>
/// </remarks>
/// <example>
///     <code>
/// [GenerateTvp("custom_tvp_name")]
/// public sealed partial record Product(int Id, string Name, decimal Price, string? Description, DateTime? DiscontinuedAt);
/// </code>
/// </example>
/// <para>
///     <b>Instructions:</b><br />
///     1. Decorate your <c>sealed partial</c> class or record with <c>[GenerateTvp]</c>.<br />
///     2. Optionally specify a custom TVP name: <c>[GenerateTvp("my_custom_tvp")]</c>.<br />
///     3. Add parameters to the primary constructor representing the desired columns in your TVP.<br />
///     4. The generated <c>ITvpMapper&lt;T&gt;</c> implementation appears at compile time.
/// </para>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateTvpAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GenerateTvpAttribute" /> class
    ///     with the default naming convention.
    /// </summary>
    public GenerateTvpAttribute()
	{
	}

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenerateTvpAttribute" /> class
    ///     with a custom SQL Server table type name.
    /// </summary>
    /// <param name="name">The SQL Server table type name (without schema) to use for this TVP.</param>
    public GenerateTvpAttribute(string name)
	{
		Name = name;
	}

    /// <summary>
    ///     Gets the custom SQL Server table type name (without schema) for this TVP.
    ///     If not specified, a default name will be generated based on the class name.
    /// </summary>
    public string? Name { get; }
}