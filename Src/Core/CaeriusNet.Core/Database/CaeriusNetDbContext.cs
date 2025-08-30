namespace CaeriusNet.Core.Database;

/// <summary>
///     Represents a factory for creating and managing database connections based on a provided connection string.
/// </summary>
public sealed record CaeriusNetDbContext(string ConnectionString) : ICaeriusNetDbContext
{
    /// <summary>
    ///     Creates and opens a database connection.
    /// </summary>
    /// <returns>An open <see cref="IDbConnection" />.</returns>
    /// <exception cref="CaeriusSqlException">Thrown when the connection fails to open.</exception>
    public IDbConnection DbConnection()
    {
        try
        {
            SqlConnection connection = new(ConnectionString);
            connection.Open();

            return connection;
        }
        catch (SqlException ex)
        {
            throw new CaeriusSqlException("Failed to open database connection : ", ex);
        }
    }
}