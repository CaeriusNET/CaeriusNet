namespace CaeriusNet.Logging;

/// <summary>
///     Provides a simple, process-wide provider for the current logger instance.
/// </summary>
/// <remarks>
///     <para>
///         This type centralizes access to a single logger instance for the application.
///         Intended usage is to set the logger once during application startup and retrieve it as needed elsewhere.
///         If no logger is configured, retrieval returns null.
///     </para>
///     <para>
///         Thread-safety: this provider does not perform synchronization for reads or writes. Initialize the logger before
///         any concurrent access and avoid changing it at runtime. Subsequent calls to SetLogger replace the previous
///         instance.
///     </para>
///     <para>
///         Guidance: prefer configuring the logger as early as possible; consider a no-op logger if consumers should not
///         have to guard for null values.
///     </para>
/// </remarks>
static internal class LoggerProvider
{
	/// <summary>
	///     The private logger instance used for logging throughout the application.
	/// </summary>
	private static ILogger? _logger;

	/// <summary>
	///     Gets the current logger instance.
	/// </summary>
	/// <returns>
	///     The configured <see cref="ILogger" /> instance, or null if no instance has been configured.
	/// </returns>
	/// <remarks>
	///     <para>
	///         This method does not throw when no logger has been configured.
	///         Typical patterns include:
	///         - Providing a default no-op logger when null is returned.
	///         - Failing fast during startup if a logger is required for the application.
	///     </para>
	///     <para>
	///         No locking is performed during the read operation. The method uses <see cref="Volatile.Read" />
	///         to ensure thread-safe access to the logger instance.
	///     </para>
	/// </remarks>
	static internal ILogger? GetLogger()
	{
		return Volatile.Read(ref _logger);
	}
}