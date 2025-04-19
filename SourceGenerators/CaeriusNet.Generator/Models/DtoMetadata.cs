using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CaeriusNet.Generator.Models;

/// <summary>
///     Contains metadata about a DTO class that was discovered with the [GenerateDto] attribute.
///     This information is used to generate the ISpMapper implementation.
/// </summary>
public sealed class DtoMetadata
{
	/// <summary>
	///     Creates a new instance of DTOMetadata.
	/// </summary>
	/// <param name="classSymbol">The compiled symbol for the DTO class.</param>
	/// <param name="declarationSyntax">The syntax node for the class declaration.</param>
	/// <param name="namespaceName">The namespace of the DTO class.</param>
	public DtoMetadata(
		INamedTypeSymbol classSymbol,
		TypeDeclarationSyntax declarationSyntax,
		string namespaceName)
	{
		ClassSymbol = classSymbol;
		DeclarationSyntax = declarationSyntax;
		ClassName = classSymbol.Name;
		Namespace = namespaceName;
	}

	/// <summary>
	///     The name of the DTO class or record.
	/// </summary>
	public string ClassName { get; }

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