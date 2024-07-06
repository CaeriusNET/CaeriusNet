namespace CaeriusNet.Factories;

public sealed record CaeriusDbConnectionFactory(string ConnectionString) : ICaeriusDbConnectionFactory
{
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