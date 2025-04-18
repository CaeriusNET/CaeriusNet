Instruction du projet : [CaeriusNet.Generator.csproj](CaeriusNet.Generator.csproj)
Ressource Informatives : [ISpMapper.cs](../../Src/Mappers/ISpMapper.cs) et ceci

```csharp
// User partial record manual creation
[GenerateDto]
public sealed partial record UserDto(
	bool IsActive,
	byte Age,
	byte[] Photo,
	short ShortValue,
	int IntValue,
	long LongValue,
	float FloatValue,
	double DoubleValue,
	decimal DecimalValue,
	char CharValue,
	string StringValue,
	Guid GuidValue,
	DateTime DateTimeValue,
	DateOnly DateValue,
	TimeOnly TimeValue,
	Uri UriValue,
	TimeSpan TimeSpanValue,
	Version VersionValue,
	DateTimeOffset DateTimeOffsetValue,
	object? NullableObjectValue,
	int? NullableIntValue,
	bool? NullableBoolValue,
	string? NullableStringValue,
	byte[]? NullableByteArrayValue);

// Source generated code based from the Attribute : [GenerateDto]
// 1. Take implement the ISpMapper<T>.
// 2. implement the MapFromDataReader method, and return directly the creation of the "Dto", based on the T-SQL Buffer position, like below
public sealed partial record UserDto : ISpMapper<UserDto>
{
	public static UserDto MapFromDataReader(SqlDataReader reader)
	{
		return new UserDto(
			reader.GetBoolean(0),
			reader.GetByte(1),
			(byte[])reader.GetValue(2),
			reader.GetInt16(3),
			reader.GetInt32(4), 
			reader.GetInt64(5),
			reader.GetFloat(6),
			reader.GetDouble(7),
			reader.GetDecimal(8),
			reader.GetString(9)[0],
			reader.GetString(10),
			reader.GetGuid(11),
			reader.GetDateTime(12),
			DateOnly.FromDateTime(reader.GetDateTime(13)),
			TimeOnly.FromDateTime(reader.GetDateTime(14)),
			new Uri(reader.GetString(15)),
			reader.GetTimeSpan(16),
			Version.Parse(reader.GetString(17)),
			reader.GetDateTimeOffset(18),
			reader.IsDBNull(19) ? null : reader.GetValue(19),
			reader.IsDBNull(20) ? null : reader.GetInt32(20),
			reader.IsDBNull(21) ? null : reader.GetBoolean(21),
			reader.IsDBNull(22) ? null : reader.GetString(22),
			reader.IsDBNull(23) ? null : (byte[])reader.GetValue(23));
	}
}
```

Nous voulons donc créer un Source Generator qui permet d'automatiser la création de DTO en se basant sur le
ISpMapper<T>, et ainsi implémenter la methode MapFromDataReader.
Nous ne modifiront pas le fonctionnement de l'interface ISpMapper.

En ce qui concerne la gestion du "null" nous nous basons sur les informations explicites fourni pas l'utilisateur à la
creation du record et des éléments mis dans le constructeur primaire (propriétés).

Si un propriété dans le Primary Constructor n'est pas null, alors nous cherchons pas à gérer le cas du nullable, si elle
est définie comme null, alors nous ajouterons la gestion de la valeur nullable, et aussi la gestion si il y a une valeur
par defaut.

le reader doit uniquement se baser par la gestion de la position du buffer (et pas par le nom des colonnes) : (0), (
1), (2), ...

Il est important de bien gérer la gestion des Types, via le mapping t-sql vers le C#. pareil pour la gestion du Cast.

Merci à toi de suivre les bonnes pratiques en C# 12 .NET 8