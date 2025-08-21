using System.Data;

namespace CaeriusNet.Core.Factories;

/// <summary>
///     Represents an abstraction for managing database connections, providing the ability to create a new connection
///     instance.
/// </summary>
public interface ICaeriusDbContext
{
    /// <summary>
    ///     Creates and returns a new database connection instance.
    /// </summary>
    /// <returns>A new instance of <see cref="IDbConnection" />.</returns>
    IDbConnection DbConnection();
}