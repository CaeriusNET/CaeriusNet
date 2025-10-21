namespace CaeriusNet.Utilities;

/// <summary>
///     Defines categories for logging to help organize and filter log messages across different parts of the application.
/// </summary>
public enum LogCategory : byte
{
    /// <summary>
    ///     Used for database-related operations and events.
    /// </summary>
    Database,

    /// <summary>
    ///     Used for Redis cache operations and events.
    /// </summary>
    Redis,

    /// <summary>
    ///     Used for in-memory cache operations and events.
    /// </summary>
    InMemoryCache,

    /// <summary>
    ///     Used for frozen cache operations and events.
    /// </summary>
    FrozenCache
}