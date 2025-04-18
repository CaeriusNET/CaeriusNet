namespace CaeriusNet.Generator.Dto;

// Méthodes auxiliaires déplacées vers le fichier principal
public sealed partial class DtoSourceGenerator
{
	// Les méthodes IsNullableType et IsReferenceType sont conservées ici pour compatibilité
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

		// Pour les records, chercher le constructeur primaire
		if (typeSymbol.IsRecord)
			foreach (var member in typeSymbol.GetMembers())
			{
				if (member is not IMethodSymbol { IsImplicitlyDeclared: true } method ||
				    !method.MethodKind.HasFlag(MethodKind.Constructor))
					continue;

				// Constructeur primaire pour un record
				if (method.Parameters.Length > 0)
					return method.Parameters.ToList();
			}

		// Pour les classes régulières, ou comme fallback, chercher le constructeur avec le plus de paramètres
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
		// Gérer les types valeur nullables
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
				_ => "sql_variant" // Type SQL le plus flexible par défaut
			}
		};
	}
}