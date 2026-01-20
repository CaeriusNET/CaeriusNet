namespace CaeriusNet.Generator.Models;

/// <summary>
///     Represents comprehensive metadata about a type targeted for source generation.
/// </summary>
/// <remarks>
///     <para>
///         This class serves as the primary data container during source generation, encapsulating
///         all information needed to generate both <see cref="ISpMapper{T}" /> and <see cref="ITvpMapper{T}" />
///         implementations.
///         It combines syntactic and semantic information from Roslyn analysis with attribute configuration.
///     </para>
///     <para>
///         Instances are created during the semantic analysis phase after a type has been validated
///         as a generation candidate, and are subsequently used throughout the code generation process.
///     </para>
/// </remarks>
internal sealed class Metadata
{
	/// <summary>
	///     Initializes a new instance of the <see cref="Metadata" /> class.
	/// </summary>
	/// <param name="classSymbol">The Roslyn type symbol for the target type.</param>
	/// <param name="declarationSyntax">The syntax node for the type declaration.</param>
	/// <param name="namespaceName">The fully qualified namespace name.</param>
	public Metadata(INamedTypeSymbol classSymbol, TypeDeclarationSyntax declarationSyntax, string namespaceName)
	{
		ClassSymbol = classSymbol;
		DeclarationSyntax = declarationSyntax;
		RecordName = classSymbol.Name;
		Namespace = namespaceName;
	}

	/// <summary>
	///     Gets the Roslyn type symbol representing the target type.
	/// </summary>
	/// <value>The <see cref="INamedTypeSymbol" /> for semantic analysis and type resolution.</value>
	public INamedTypeSymbol ClassSymbol { get; }

	/// <summary>
	///     Gets the syntax node for the type declaration.
	/// </summary>
	/// <value>
	///     The <see cref="TypeDeclarationSyntax" /> representing the class or record declaration in source code.
	/// </value>
	public TypeDeclarationSyntax DeclarationSyntax { get; }

	/// <summary>
	///     Gets the name of the target type.
	/// </summary>
	/// <value>The simple type name (without namespace qualification).</value>
	public string RecordName { get; }

	/// <summary>
	///     Gets the fully qualified namespace of the target type.
	/// </summary>
	/// <value>
	///     The namespace name, or "global" if the type is in the global namespace.
	/// </value>
	public string Namespace { get; }

	/// <summary>
	///     Gets or sets the SQL Server schema name for TVP generation.
	/// </summary>
	/// <value>
	///     The schema name (e.g., "dbo", "app"), or <see langword="null" /> if not configured.
	///     Defaults to "dbo" during TVP generation if not explicitly set.
	/// </value>
	/// <remarks>
	///     This property is only relevant for types decorated with <see cref="GenerateTvpAttribute" />.
	/// </remarks>
	public string? TvpSchema { get; set; }

	/// <summary>
	///     Gets or sets the TVP type name (without schema qualification).
	/// </summary>
	/// <value>
	///     The TVP type name as specified in the <see cref="GenerateTvpAttribute" />,
	///     or <see langword="null" /> if not configured.
	/// </value>
	/// <remarks>
	///     This property is required for TVP generation and must be provided by the user
	///     through the <see cref="GenerateTvpAttribute" />.
	/// </remarks>
	public string? TvpName { get; set; }

	/// <summary>
	///     Gets the collection of constructor parameter metadata.
	/// </summary>
	/// <value>
	///     A mutable list of <see cref="ParameterMetadata" /> objects representing each primary constructor parameter.
	/// </value>
	/// <remarks>
	///     This collection is populated during the metadata extraction phase and preserves parameter order,
	///     which is critical for generating correct ordinal-based data access code.
	/// </remarks>
	public List<ParameterMetadata> Parameters { get; } = [];
}