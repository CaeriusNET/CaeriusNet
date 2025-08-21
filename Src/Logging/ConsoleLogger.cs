namespace CaeriusNet.Logging;

/// <summary>
///     Implémentation de <see cref="ICaeriusLogger" /> qui journalise les messages dans la console.
/// </summary>
internal sealed record ConsoleLogger : ICaeriusLogger
{
	private static readonly object Lock = new();

	/// <inheritdoc />
	public bool IsEnabled { get; } = true;

	/// <inheritdoc />
	public void Log(LogLevel level, LogCategory category, string message)
	{
		if (!IsEnabled) return;

		lock (Lock){
			Console.ForegroundColor = GetColorForLevel(level);
			string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			Console.Write($"[{timestamp}] [{level}] ");

			Console.ForegroundColor = GetColorForCategory(category);
			Console.Write($"[{category}] ");

			Console.ResetColor();
			Console.WriteLine(message);
		}
	}

	/// <inheritdoc />
	public void LogInformation(LogCategory category, string message)
	{
		Log(LogLevel.Information, category, message);
	}

	/// <inheritdoc />
	public void LogDebug(LogCategory category, string message)
	{
		Log(LogLevel.Debug, category, message);
	}

	/// <inheritdoc />
	public void LogWarning(LogCategory category, string message)
	{
		Log(LogLevel.Warning, category, message);
	}

	/// <inheritdoc />
	public void LogError(LogCategory category, string message)
	{
		Log(LogLevel.Error, category, message);
	}

	/// <inheritdoc />
	public void LogError(LogCategory category, string message, Exception exception)
	{
		LogError(category, message);
		LogError(category, $"Exception: {exception.Message}");
		if (exception.InnerException != null)
			LogError(category, $"Inner exception: {exception.InnerException.Message}");
	}

	/// <summary>
	///     Obtient la couleur à utiliser pour le niveau de log spécifié.
	/// </summary>
	private static ConsoleColor GetColorForLevel(LogLevel level)
	{
		return level switch
		{
			LogLevel.Trace => ConsoleColor.Gray,
			LogLevel.Debug => ConsoleColor.DarkGray,
			LogLevel.Information => ConsoleColor.Green,
			LogLevel.Warning => ConsoleColor.Yellow,
			LogLevel.Error => ConsoleColor.Red,
			LogLevel.Critical => ConsoleColor.DarkRed,
			_ => ConsoleColor.White
		};
	}

	/// <summary>
	///     Obtient la couleur à utiliser pour la catégorie de log spécifiée.
	/// </summary>
	private static ConsoleColor GetColorForCategory(LogCategory category)
	{
		return category switch
		{
			LogCategory.Database => ConsoleColor.Cyan,
			LogCategory.Redis => ConsoleColor.Magenta,
			LogCategory.InMemoryCache => ConsoleColor.Blue,
			LogCategory.FrozenCache => ConsoleColor.DarkBlue,
			LogCategory.General => ConsoleColor.White,
			_ => ConsoleColor.White
		};
	}
}