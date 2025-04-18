namespace CaeriusNet.Generator.Dto;

/// <summary>
///     Source generator for generating ISpMapper implementations for DTO classes/records.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class DtoSourceGenerator : IIncrementalGenerator
{
	// Qualified name used with ForAttributeWithMetadataName
	private const string AttributeFullName = "CaeriusNet.Mappers.Attributes.GenerateDtoAttribute";

	/// <summary>
	///     Initializes the source generator.
	/// </summary>
	/// <param name="context">The initialization context.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Register the attribute that will be available to users in their code
		context.RegisterPostInitializationOutput(ctx =>
		{
			ctx.AddSource(SourceGeneratedDtoAttribute.GlobalName, SourceGeneratedDtoAttribute.Source);
		});

		// Approche 1: Trouver les attributs par leur nom qualifié complet (avec using)
		var qualifiedProvider = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				AttributeFullName,
				static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
				static (context, _) =>
				{
					// Extraire le type déclaré
					INamedTypeSymbol? typeSymbol = null;
					if (context.TargetSymbol is INamedTypeSymbol symbol) typeSymbol = symbol;

					return typeSymbol == null
						? null
						:
						// Créer le DtoRecord à partir du symbole
						CreateDtoRecord(typeSymbol, context.TargetNode);
				})
			.Where(record => record is not null);

		// Approche 2: Trouver les attributs par analyse syntaxique (sans using)
		var syntaxProvider = context.SyntaxProvider
			.CreateSyntaxProvider(
				static (node, _) => (node is ClassDeclarationSyntax classDecl &&
				                     HasGenerateDtoAttribute(classDecl.AttributeLists)) ||
				                    (node is RecordDeclarationSyntax recordDecl &&
				                     HasGenerateDtoAttribute(recordDecl.AttributeLists)),
				static (context, _) =>
				{
					// Récupérer le symbole de la classe/record

					var typeSymbol = context.Node switch
					{
						ClassDeclarationSyntax classDecl =>
							context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol,
						RecordDeclarationSyntax recordDecl =>
							context.SemanticModel.GetDeclaredSymbol(recordDecl) as INamedTypeSymbol,
						_ => null
					};

					if (typeSymbol == null)
						return null;

					// Vérifier que ce n'est pas déjà traité par l'autre provider
					// en s'assurant que l'attribut n'est PAS le nom qualifié complet
					var hasFullyQualifiedAttribute = typeSymbol.GetAttributes()
						.Any(attr => attr.AttributeClass?.ToDisplayString() == AttributeFullName);

					return hasFullyQualifiedAttribute
						? null
						:
						// Créer le DtoRecord
						CreateDtoRecord(typeSymbol, context.Node);
				})
			.Where(record => record is not null);

		// Combiner les deux providers
		var combinedProvider = qualifiedProvider.Collect()
			.Combine(syntaxProvider.Collect())
			.Select((pair, _) => pair.Left.AddRange(pair.Right).ToImmutableArray());

		// Vérifier si le générateur est activé dans la configuration du projet
		var enabled = context.AnalyzerConfigOptionsProvider.IsEnabled("GenerateDto");

		// Combiner le fournisseur avec le flag d'activation
		var provider = combinedProvider.Combine(enabled);

		// Enregistrer la sortie pour la génération de source
		context.RegisterSourceOutput(provider, (spc, pair) => Generate(spc, pair.Left, pair.Right));
	}

	private static bool HasGenerateDtoAttribute(SyntaxList<AttributeListSyntax> attributeLists)
	{
		foreach (var attrName in attributeLists.SelectMany(attrList =>
			         attrList.Attributes.Select(attr => attr.Name.ToString())))
			if (attrName is "GenerateDto" or "GenerateDtoAttribute")
				return true;

		return false;
	}

	private static DtoRecord CreateDtoRecord(INamedTypeSymbol typeSymbol, SyntaxNode node)
	{
		var isRecord = typeSymbol.IsRecord;
		var hasPrimaryConstructor = false;
		RecordDeclarationSyntax? recordSyntax = null;

		// Vérifier pour le constructeur primaire dans le cas d'un record
		if (isRecord && node is RecordDeclarationSyntax syntax)
		{
			recordSyntax = syntax;
			hasPrimaryConstructor = recordSyntax.ParameterList is { Parameters.Count: > 0 };
		}

		var record = new DtoRecord
		{
			RecordTypeName = typeSymbol.Name,
			RecordFullName = typeSymbol.ToDisplayString(),
			Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
			IsRecord = isRecord,
			HasPrimaryConstructor = hasPrimaryConstructor,
			Properties = []
		};

		// Pour les DTOs, priorité aux paramètres du constructeur principal pour les records
		var constructorParameters = GetConstructorParameters(typeSymbol);
		if (constructorParameters.Count > 0)
			for (var i = 0; i < constructorParameters.Count; i++)
			{
				var parameter = constructorParameters[i];
				var sqlType = GetSqlType(parameter.Type);

				// Détecter si le type est nullable à partir de la syntaxe
				var isExplicitlyNullable = false;

				if (recordSyntax is { ParameterList: not null } &&
				    i < recordSyntax.ParameterList.Parameters.Count)
				{
					var paramSyntax = recordSyntax.ParameterList.Parameters[i];

					// Vérifier si le type contient un "?" explicite dans la syntaxe
					isExplicitlyNullable = paramSyntax.Type?.ToString().EndsWith("?") ?? false;
				}

				// Déterminer la nullabilité à partir de plusieurs sources d'information
				var isNullable = parameter.NullableAnnotation == NullableAnnotation.Annotated ||
				                 IsNullableType(parameter.Type) ||
				                 (IsReferenceType(parameter.Type) && !parameter.Type.IsValueType) ||
				                 isExplicitlyNullable ||
				                 parameter.Type.NullableAnnotation == NullableAnnotation.Annotated;

				record.Properties.Add(new DtoProperty
				{
					Name = parameter.Name,
					TypeName = parameter.Type.ToDisplayString(),
					IsNullable = isNullable,
					SqlTypeName = sqlType
				});
			}
		else
			// Fallback sur les propriétés quand aucun paramètre de constructeur n'est trouvé
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
					             (IsReferenceType(property.Type) &&
					              property.Type.NullableAnnotation != NullableAnnotation.NotAnnotated),
					SqlTypeName = sqlType
				});
			}

		return record;
	}
}