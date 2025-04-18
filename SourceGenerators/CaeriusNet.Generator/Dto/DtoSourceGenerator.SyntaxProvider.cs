namespace CaeriusNet.Generator.Dto;

public sealed partial class DtoSourceGenerator
{
	private static bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Check if the node is either a class or record declaration
		if (syntaxNode is ClassDeclarationSyntax classDeclaration)
			return classDeclaration
				.AttributeLists
				.SelectMany(x => x.Attributes)
				.Any(x => x.Name.ToString() is SourceGenerateDtoAttribute.Name or "GenerateDto");

		if (syntaxNode is RecordDeclarationSyntax recordDeclaration)
			return recordDeclaration
				.AttributeLists
				.SelectMany(x => x.Attributes)
				.Any(x => x.Name.ToString() is SourceGenerateDtoAttribute.Name or "GenerateDto");

		return false;
	}

	private static DtoRecord? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		INamedTypeSymbol? typeSymbol = null;

		// Handle both class and record declarations
		if (context.Node is ClassDeclarationSyntax classDeclaration)
			typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
		else if (context.Node is RecordDeclarationSyntax recordDeclaration)
			typeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration) as INamedTypeSymbol;

		if (typeSymbol == null)
			return null;

		var attributeData = typeSymbol.GetAttributes()
			.FirstOrDefault(a => a.AttributeClass?.Name is SourceGenerateDtoAttribute.Name or "GenerateDto");

		if (attributeData == null)
			return null;

		var record = new DtoRecord
		{
			RecordTypeName = typeSymbol.Name,
			RecordFullName = typeSymbol.ToDisplayString(),
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			Properties = []
		};

		// Prioritize constructor parameters for records
		var constructorParameters = GetConstructorParameters(typeSymbol);
		if (constructorParameters.Count > 0)
			foreach (var parameter in constructorParameters)
			{
				var sqlType = GetSqlType(parameter.Type);
				if (string.IsNullOrEmpty(sqlType))
					continue;

				record.Properties.Add(new DtoProperty
				{
					Name = parameter.Name,
					TypeName = parameter.Type.ToDisplayString(),
					IsNullable = parameter.NullableAnnotation == NullableAnnotation.Annotated ||
					             IsNullableType(parameter.Type),
					SqlTypeName = sqlType
				});
			}
		else
			// Fall back to properties when no constructor parameters are found
			foreach (var member in typeSymbol.GetMembers())
			{
				if (member is not IPropertySymbol property)
					continue;

				if (property.IsStatic || !property.DeclaredAccessibility.HasFlag(Accessibility.Public))
					continue;

				var sqlType = GetSqlType(property.Type);
				if (string.IsNullOrEmpty(sqlType))
					continue;

				record.Properties.Add(new DtoProperty
				{
					Name = property.Name,
					TypeName = property.Type.ToDisplayString(),
					IsNullable = property.NullableAnnotation == NullableAnnotation.Annotated ||
					             IsNullableType(property.Type),
					SqlTypeName = sqlType
				});
			}

		return record;
	}

	private static bool IsNullableType(ITypeSymbol type)
	{
		return type is INamedTypeSymbol namedType &&
		       namedType.IsValueType &&
		       namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
	}

	private static List<IParameterSymbol> GetConstructorParameters(INamedTypeSymbol typeSymbol)
	{
		var result = new List<IParameterSymbol>();

		foreach (var member in typeSymbol.GetMembers())
		{
			if (member is not IMethodSymbol method)
				continue;

			// For records with primary constructor, prefer that one
			if (method.Parameters.Length > 0 && method.ContainingType.IsRecord) return method.Parameters.ToList();

			// Otherwise look for the constructor with most parameters
			if (method.Parameters.Length > result.Count) result = method.Parameters.ToList();
		}

		return result;
	}

	private static string GetSqlType(ITypeSymbol type)
	{
		// Handle nullable value types
		if (IsNullableType(type) && type is INamedTypeSymbol namedType) type = namedType.TypeArguments[0];

		return type.SpecialType switch
		{
			SpecialType.System_Boolean => "bit",
			SpecialType.System_Byte => "tinyint",
			SpecialType.System_Int16 => "smallint",
			SpecialType.System_Int32 => "int",
			SpecialType.System_Int64 => "bigint",
			SpecialType.System_Decimal => "decimal",
			SpecialType.System_Single => "real",
			SpecialType.System_Double => "float",
			SpecialType.System_String => "nvarchar",
			SpecialType.System_Char => "nchar",
			SpecialType.System_DateTime => "datetime2",
			_ => type.ToString() switch
			{
				"System.Guid" => "uniqueidentifier",
				"System.DateTimeOffset" => "datetimeoffset",
				"System.TimeSpan" => "time",
				"byte[]" => "varbinary",
				_ => string.Empty
			}
		};
	}
}