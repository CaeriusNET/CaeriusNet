namespace CaeriusNet.Utilities;

/// <summary>
///     Définit les catégories de log pour identifier facilement la source des messages.
/// </summary>
public enum LogCategory : byte
{
	Database,
	Redis,
	InMemoryCache,
	FrozenCache,
	General
}