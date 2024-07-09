using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

public static class MultiReadOnlyCollectionReadSqlAsyncCommands
{
    public static async Task<(ReadOnlyCollection<TResultSets1>, ReadOnlyCollection<TResultSets3>)>
        QueryMultipleReadOnlyCollectionAsync<TResultSets1,
            TResultSets3>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets3 : class, ISpMapper<TResultSets3>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ReadOnlyCollection<TResultSets1>)results[0], (ReadOnlyCollection<TResultSets3>)results[1]);
    }

    public static async Task<(ReadOnlyCollection<TResultSets1>, ReadOnlyCollection<TResultSets2>,
            ReadOnlyCollection<TResultSets3>)>
        QueryMultipleReadOnlyCollectionAsync<TResultSets1, TResultSets2, TResultSets3>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ReadOnlyCollection<TResultSets1>)results[0], (ReadOnlyCollection<TResultSets2>)results[1],
            (ReadOnlyCollection<TResultSets3>)results[2]);
    }

    public static async Task<(ReadOnlyCollection<TResultSets1>, ReadOnlyCollection<TResultSets2>,
            ReadOnlyCollection<TResultSets3>, ReadOnlyCollection<TResultSets4>)>
        QueryMultipleReadOnlyCollectionAsync<TResultSets1, TResultSets2, TResultSets3, TResultSets4>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
        where TResultSets4 : class, ISpMapper<TResultSets4>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ReadOnlyCollection<TResultSets1>)results[0], (ReadOnlyCollection<TResultSets2>)results[1],
            (ReadOnlyCollection<TResultSets3>)results[2],
            (ReadOnlyCollection<TResultSets4>)results[3]);
    }

    public static async Task<(ReadOnlyCollection<TResultSets1>, ReadOnlyCollection<TResultSets2>,
            ReadOnlyCollection<TResultSets3>, ReadOnlyCollection<TResultSets4>, ReadOnlyCollection<TResultSets5>)>
        QueryMultipleReadOnlyCollectionAsync<TResultSets1, TResultSets2, TResultSets3, TResultSets4, TResultSets5>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
        where TResultSets4 : class, ISpMapper<TResultSets4>
        where TResultSets5 : class, ISpMapper<TResultSets5>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ReadOnlyCollection<TResultSets1>)results[0], (ReadOnlyCollection<TResultSets2>)results[1],
            (ReadOnlyCollection<TResultSets3>)results[2],
            (ReadOnlyCollection<TResultSets4>)results[3], (ReadOnlyCollection<TResultSets5>)results[4]);
    }

    private static async Task<object[]> QueryMultipleAsync(this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        var connection = connectionFactory.DbConnection();
        return await SqlCommandUtility.MultipleQueryAsync(connection, spParameters);
    }
}