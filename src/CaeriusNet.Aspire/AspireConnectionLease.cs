using CaeriusNet.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace CaeriusNet.Aspire;

internal sealed class AspireConnectionLease(AsyncServiceScope scope, SqlConnection connection)
    : ICaeriusSqlConnectionLease
{
    public SqlConnection Connection { get; } = connection;

    public ValueTask DisposeAsync()
    {
        return scope.DisposeAsync();
    }
}
