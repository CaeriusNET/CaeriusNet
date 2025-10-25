using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace CaeriusNet.Generator.Models;

/// <summary>
///     Contains metadata about a DTO class that was discovered with the [GenerateDto] attribute.
///     This information is used to generate the ISpMapper implementation.
/// </summary>
public sealed class Metadata
{
	/// <summary>
	///     Creates a new instance of DTOMetadata.
	/// </summary>
	/// <param name="classSymbol">The compiled symbol for the DTO class.</param>
	/// <param name="declarationSyntax">The syntax node for the class declaration.</param>
	/// <param name="namespaceName">The namespace of the DTO class.</param>
	public Metadata(INamedTypeSymbol classSymbol, TypeDeclarationSyntax declarationSyntax, string namespaceName)
	{
		ClassSymbol = classSymbol;
		DeclarationSyntax = declarationSyntax;
		RecordName = classSymbol.Name;
		Namespace = namespaceName;
	}

	/// <summary>
	///     Gets or sets the SQL Server schema name for the TVP.
	///     Defaults to "dbo" if not specified by the user.
	/// </summary>
	public string? TvpSchema { get; set; }

	/// <summary>
	///     Gets or sets the TVP name (without schema) specified by the GenerateTvp attribute.
	///     This is required and must be provided by the user.
	/// </summary>
	public string? TvpName { get; set; }

	/// <summary>
	///     The name of the DTO class or record.
	/// </summary>
	public string RecordName { get; }

	/// <summary>
	///     The full namespace of the DTO class.
	/// </summary>
	public string Namespace { get; }

	/// <summary>
	///     The compiled symbol representing the DTO class.
	/// </summary>
	public INamedTypeSymbol ClassSymbol { get; }

	/// <summary>
	///     The syntax node for the class/record declaration.
	/// </summary>
	public TypeDeclarationSyntax DeclarationSyntax { get; }

	/// <summary>
	///     List of constructor parameters from the primary constructor.
	/// </summary>
	public List<ParameterMetadata> Parameters { get; } = [];
}