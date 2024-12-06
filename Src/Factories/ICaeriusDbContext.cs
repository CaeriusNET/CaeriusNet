namespace CaeriusNet.Factories;

/// <summary>
///     Defines the interface for a factory that creates database connections.
/// </summary>
public interface ICaeriusDbContext
{
    /// <summary>
    ///     Creates and returns a new database connection instance.
    /// </summary>
    /// <returns>A new instance of <see cref="IDbConnection" />.</returns>
    IDbConnection DbConnection();
}