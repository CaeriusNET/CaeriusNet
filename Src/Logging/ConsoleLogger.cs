namespace CaeriusNet.Logging;

/// <summary>
///     A lightweight, thread-safe logger that writes colorized, timestamped messages to the console.
/// </summary>
/// <remarks>
///     <para>
///         This logger targets development and diagnostics scenarios where human-readable, immediate feedback is
///         important.
///         It prefixes each line with a timestamp and standardized tags for level and category, then writes the message
///         body.
///         The output is color-coded by both log level and category to improve scan-ability; colors are reset after each
///         write
///         so subsequent console output remains unaffected.
///     </para>
///     <para>
///         Format:
///         [yyyy-MM-dd HH:mm:ss.fff] [Level] [Category] Message
///     </para>
///     <para>
///         Behavior highlights:
///         - Thread safety: writes are synchronized to prevent interleaved lines across concurrent threads.
///         - Levels: Trace, Debug, Information, Warning, Error, and Critical map to distinct console colors.
///         - Categories: subsystem categories map to specific colors; unknown values fall back to a neutral color.
///         - Exceptions: an overload logs the main message, the exception message, and the inner exception (when present).
///     </para>
///     <para>
///         Guidance:
///         - Use appropriate levels to convey severity and intent; avoid logging secrets or PII.
///         - For high-volume production scenarios, consider complementing or replacing console logging with a centralized,
///         structured logging provider.
///     </para>
/// </remarks>
internal sealed record ConsoleNetLogger : ICaeriusNetLogger
{
	private static readonly Lock Lock = new();

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
	///     Gets the console color to use for the specified log level.
	/// </summary>
	/// <remarks>
	///     Each log level maps to a distinct color to reflect severity and improve readability in the terminal.
	///     If the level is not recognized, a neutral fallback color is used.
	/// </remarks>
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
	///     Gets the console color to use for the specified log category.
	/// </summary>
	/// <remarks>
	///     Known categories map to specific colors to visually group related messages; unknown categories fall back to a
	///     neutral color.
	/// </remarks>
	private static ConsoleColor GetColorForCategory(LogCategory category)
	{
		return category switch
		{
			LogCategory.Database => ConsoleColor.Cyan,
			LogCategory.Redis => ConsoleColor.Magenta,
			LogCategory.InMemoryCache => ConsoleColor.Blue,
			LogCategory.FrozenCache => ConsoleColor.DarkBlue,
			_ => ConsoleColor.White
		};
	}
}