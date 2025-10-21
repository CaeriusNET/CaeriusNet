namespace CaeriusNet.Utilities;

/// <summary>
///     Defines log severity levels in ascending order (Trace is least severe).
/// </summary>
public enum LogLevel : byte
{
	/// <summary>
	///     Fine-grained diagnostic events primarily useful for developers.
	/// </summary>
	Trace,

	/// <summary>
	///     Short-term debugging information.
	/// </summary>
	Debug,

	/// <summary>
	///     General informational messages that highlight application progress.
	/// </summary>
	Information,

	/// <summary>
	///     Potentially harmful situations that may require attention.
	/// </summary>
	Warning,

	/// <summary>
	///     Error events that might still allow the application to continue running.
	/// </summary>
	Error,

	/// <summary>
	///     Severe error events that are unrecoverable or require immediate attention.
	/// </summary>
	Critical
}