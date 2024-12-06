namespace CaeriusNet.Factories;

/// <summary>
///     Factory for creating database connections using a specified connection string.
/// </summary>
public sealed record CaeriusDbContext(string ConnectionString) : ICaeriusDbContext
{
    /// <summary>
    ///     Creates and opens a database connection.
    /// </summary>
    /// <returns>An open <see cref="IDbConnection" />.</returns>
    /// <exception cref="Exception">Thrown when the connection fails to open.</exception>
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
            throw new Exception("Failed to open database connection ; ", ex);
        }
    }
}