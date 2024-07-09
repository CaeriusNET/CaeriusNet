using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

public static class MultiImmutableArrayReadSqlAsyncCommands
{
    public static async Task<(ImmutableArray<TResultSets1>, ImmutableArray<TResultSets2>)>
        QueryMultipleImmutableArrayAsync<TResultSets1, TResultSets2>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ImmutableArray<TResultSets1>)results[0], (ImmutableArray<TResultSets2>)results[1]);
    }

    public static async Task<(ImmutableArray<TResultSets1>, ImmutableArray<TResultSets2>, ImmutableArray<TResultSets3>)>
        QueryMultipleImmutableArrayAsync<TResultSets1, TResultSets2, TResultSets3>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ImmutableArray<TResultSets1>)results[0], (ImmutableArray<TResultSets2>)results[1],
            (ImmutableArray<TResultSets3>)results[2]);
    }

    public static async Task<(ImmutableArray<TResultSets1>, ImmutableArray<TResultSets2>, ImmutableArray<TResultSets3>,
            ImmutableArray<TResultSets4>)>
        QueryMultipleImmutableArrayAsync<TResultSets1, TResultSets2, TResultSets3, TResultSets4>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
        where TResultSets4 : class, ISpMapper<TResultSets4>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ImmutableArray<TResultSets1>)results[0], (ImmutableArray<TResultSets2>)results[1],
            (ImmutableArray<TResultSets3>)results[2],
            (ImmutableArray<TResultSets4>)results[3]);
    }

    public static async Task<(ImmutableArray<TResultSets1>, ImmutableArray<TResultSets2>, ImmutableArray<TResultSets3>,
            ImmutableArray<TResultSets4>, ImmutableArray<TResultSets5>)>
        QueryMultipleImmutableArrayAsync<TResultSets1, TResultSets2, TResultSets3, TResultSets4, TResultSets5>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSets1 : class, ISpMapper<TResultSets1>
        where TResultSets2 : class, ISpMapper<TResultSets2>
        where TResultSets3 : class, ISpMapper<TResultSets3>
        where TResultSets4 : class, ISpMapper<TResultSets4>
        where TResultSets5 : class, ISpMapper<TResultSets5>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((ImmutableArray<TResultSets1>)results[0], (ImmutableArray<TResultSets2>)results[1],
            (ImmutableArray<TResultSets3>)results[2],
            (ImmutableArray<TResultSets4>)results[3], (ImmutableArray<TResultSets5>)results[4]);
    }

    private static async Task<object[]> QueryMultipleAsync(this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        var connection = connectionFactory.DbConnection();
        return await SqlCommandUtility.MultipleQueryAsync(connection, spParameters);
    }
}