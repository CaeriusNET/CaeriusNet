namespace CaeriusNet.Abstractions;

/// <summary>
///     Represents an abstraction for managing database connections, providing the ability to create a new connection
///     instance.
/// </summary>
public interface ICaeriusNetDbContext
{
    /// <summary>
    ///     Gets the Redis cache manager for distributed caching.
    /// </summary>
    IRedisCacheManager? RedisCacheManager { get; }

    /// <summary>
    ///     Creates and returns a new database connection instance.
    /// </summary>
    /// <returns>
    ///     A new instance of <see cref="IDbConnection" /> that provides access to the database.
    ///     The caller is responsible for properly disposing the connection when finished.
    /// </returns>
    /// <remarks>
    ///     Each call to this method creates a new connection that must be disposed. It is recommended to wrap
    ///     the connection in a using statement or using declaration to ensure proper resource cleanup.
    /// </remarks>
    /// <example>
    ///     <code>
    /// 	 using (var connection = dbContext.DbConnection())
    /// 	 {
    /// 		 // Use the connection here
    /// 	 } // Connection is automatically disposed
    /// 	 </code>
    /// </example>
    /// <summary>
    ///     Creates and returns a new opened database connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A <see cref="ValueTask{SqlConnection}" /> representing the asynchronous operation.
    ///     The result is an opened <see cref="SqlConnection" /> ready for use.
    /// </returns>
    /// <example>
    ///     <code>
    /// 	 await using var connection = await dbContext.DbConnectionAsync(cancellationToken);
    /// 	 </code>
    /// </example>
    ValueTask<SqlConnection> DbConnectionAsync(CancellationToken cancellationToken = default);
}
