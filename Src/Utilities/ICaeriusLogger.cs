namespace CaeriusNet.Utilities;

/// <summary>
///     Interface définissant les méthodes pour journaliser les événements dans l'application.
/// </summary>
public interface ICaeriusLogger
{
	/// <summary>
	///     Active ou désactive le logging.
	/// </summary>
	bool IsEnabled { get; }

	/// <summary>
	///     Journalise un message avec le niveau de gravité et la catégorie spécifiés.
	/// </summary>
	/// <param name="level">Niveau de gravité du message.</param>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	void Log(LogLevel level, LogCategory category, string message);

	/// <summary>
	///     Journalise un message d'information.
	/// </summary>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	void LogInformation(LogCategory category, string message);

	/// <summary>
	///     Journalise un message de débogage.
	/// </summary>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	void LogDebug(LogCategory category, string message);

	/// <summary>
	///     Journalise un message d'avertissement.
	/// </summary>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	void LogWarning(LogCategory category, string message);

	/// <summary>
	///     Journalise un message d'erreur.
	/// </summary>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	void LogError(LogCategory category, string message);

	/// <summary>
	///     Journalise un message d'erreur avec l'exception associée.
	/// </summary>
	/// <param name="category">Catégorie du message.</param>
	/// <param name="message">Message à journaliser.</param>
	/// <param name="exception">Exception associée au message.</param>
	void LogError(LogCategory category, string message, Exception exception);
}