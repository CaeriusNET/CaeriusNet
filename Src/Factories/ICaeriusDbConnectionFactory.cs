namespace CaeriusNet.Factories;

public interface ICaeriusDbConnectionFactory
{
    IDbConnection DbConnection();
}