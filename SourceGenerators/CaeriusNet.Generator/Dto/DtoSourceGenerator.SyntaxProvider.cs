namespace CaeriusNet.Generator.Dto;

public sealed partial class DtoSourceGenerator
{
	private static bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return syntaxNode switch
		{
			// Check if the node is either a class or record declaration
			ClassDeclarationSyntax classDeclaration => classDeclaration.AttributeLists.SelectMany(x => x.Attributes)
				.Any(x => x.Name.ToString() is SourceGeneratedDtoAttribute.Name or "GenerateDto"),
			RecordDeclarationSyntax recordDeclaration => recordDeclaration.AttributeLists.SelectMany(x => x.Attributes)
				.Any(x => x.Name.ToString() is SourceGeneratedDtoAttribute.Name or "GenerateDto"),
			_ => false
		};
	}

	private static DtoRecord? Transform(GeneratorSyntaxContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		INamedTypeSymbol? typeSymbol = null;
		var isRecord = false;
		var hasPrimaryConstructor = false;

		switch (context.Node)
		{
			// Handle both class and record declarations
			case ClassDeclarationSyntax classDeclaration:
				typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
				break;
			case RecordDeclarationSyntax recordDeclaration:
			{
				typeSymbol = context.SemanticModel.GetDeclaredSymbol(recordDeclaration) as INamedTypeSymbol;
				isRecord = true;

				// Check if this is a record with a primary constructor
				if (recordDeclaration.ParameterList != null && recordDeclaration.ParameterList.Parameters.Count > 0)
					hasPrimaryConstructor = true;

				break;
			}
		}

		if (typeSymbol == null)
			return null;

		var attributeData = typeSymbol.GetAttributes()
			.FirstOrDefault(a => a.AttributeClass?.Name is SourceGeneratedDtoAttribute.Name or "GenerateDto");

		if (attributeData == null)
			return null;

		var record = new DtoRecord
		{
			RecordTypeName = typeSymbol.Name,
			RecordFullName = typeSymbol.ToDisplayString(),
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			IsRecord = isRecord,
			HasPrimaryConstructor = hasPrimaryConstructor,
			Properties = []
		};

		// For DTOs, prioritize primary constructor parameters for records
		var constructorParameters = GetConstructorParameters(typeSymbol);
		if (constructorParameters.Count > 0)
			foreach (var parameter in constructorParameters)
			{
				var sqlType = GetSqlType(parameter.Type);

				// Even if we don't recognize the SQL type, we still need to include the parameter
				// to preserve the constructor signature
				record.Properties.Add(new DtoProperty
				{
					Name = parameter.Name,
					TypeName = parameter.Type.ToDisplayString(),
					IsNullable = parameter.NullableAnnotation == NullableAnnotation.Annotated ||
					             IsNullableType(parameter.Type) ||
					             IsReferenceType(parameter.Type),
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

				record.Properties.Add(new DtoProperty
				{
					Name = property.Name,
					TypeName = property.Type.ToDisplayString(),
					IsNullable = property.NullableAnnotation == NullableAnnotation.Annotated ||
					             IsNullableType(property.Type) ||
					             (IsReferenceType(property.Type) && property.Type.NullableAnnotation !=
						             NullableAnnotation.NotAnnotated),
					SqlTypeName = sqlType
				});
			}

		return record;
	}

	private static bool IsNullableType(ITypeSymbol type)
	{
		return type is INamedTypeSymbol
		{
			IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
		};
	}

	private static bool IsReferenceType(ITypeSymbol type)
	{
		return !type.IsValueType;
	}

	private static List<IParameterSymbol> GetConstructorParameters(INamedTypeSymbol typeSymbol)
	{
		var result = new List<IParameterSymbol>();

		// For records, find the primary constructor
		if (typeSymbol.IsRecord)
			foreach (var member in typeSymbol.GetMembers())
			{
				if (member is not IMethodSymbol { IsImplicitlyDeclared: true } method ||
				    !method.MethodKind.HasFlag(MethodKind.Constructor))
					continue;

				// Primary constructor for a record
				if (method.Parameters.Length > 0) return method.Parameters.ToList();
			}

		// For regular classes, or as a fallback, look for the constructor with most parameters
		foreach (var member in typeSymbol.GetMembers())
		{
			if (member is not IMethodSymbol method || !method.MethodKind.HasFlag(MethodKind.Constructor))
				continue;

			if (method.Parameters.Length > result.Count)
				result = method.Parameters.ToList();
		}

		return result;
	}

	private static string GetSqlType(ITypeSymbol type)
	{
		// Handle nullable value types
		if (IsNullableType(type) && type is INamedTypeSymbol namedType)
			type = namedType.TypeArguments[0];

		return type.SpecialType switch
		{
			SpecialType.System_Boolean => "bit",
			SpecialType.System_Byte => "tinyint",
			SpecialType.System_SByte => "smallint",
			SpecialType.System_Int16 => "smallint",
			SpecialType.System_UInt16 => "int",
			SpecialType.System_Int32 => "int",
			SpecialType.System_UInt32 => "bigint",
			SpecialType.System_Int64 => "bigint",
			SpecialType.System_UInt64 => "decimal",
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
				"System.DateOnly" => "date",
				"System.TimeOnly" => "time",
				"byte[]" or "System.Byte[]" => "varbinary",
				"System.Uri" => "nvarchar",
				"System.Version" => "nvarchar",
				_ => "sql_variant" // Default to most flexible SQL type
			}
		};
	}
}