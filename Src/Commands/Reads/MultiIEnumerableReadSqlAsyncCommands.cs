using CaeriusNet.Builders;
using CaeriusNet.Factories;
using CaeriusNet.Mappers;
using CaeriusNet.Utilities;

namespace CaeriusNet.Commands.Reads;

public static class MultiIEnumerableReadSqlAsyncCommands
{
    public static async Task<(IEnumerable<TResulSet1>, IEnumerable<TResultSet2>)> QueryMultipleIEnumerableAsync<
        TResulSet1, TResultSet2>(
        this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
        where TResulSet1 : class, ISpMapper<TResulSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((IEnumerable<TResulSet1>)results[0], (IEnumerable<TResultSet2>)results[1]);
    }

    public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2,
            TResultSet3>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSet1 : class, ISpMapper<TResultSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
        where TResultSet3 : class, ISpMapper<TResultSet3>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((IEnumerable<TResultSet1>)results[0], (IEnumerable<TResultSet2>)results[1],
            (IEnumerable<TResultSet3>)results[2]);
    }

    public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
            IEnumerable<TResultSet4>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSet1 : class, ISpMapper<TResultSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
        where TResultSet3 : class, ISpMapper<TResultSet3>
        where TResultSet4 : class, ISpMapper<TResultSet4>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((IEnumerable<TResultSet1>)results[0], (IEnumerable<TResultSet2>)results[1],
            (IEnumerable<TResultSet3>)results[2],
            (IEnumerable<TResultSet4>)results[3]);
    }

    public static async Task<(IEnumerable<TResultSet1>, IEnumerable<TResultSet2>, IEnumerable<TResultSet3>,
            IEnumerable<TResultSet4>, IEnumerable<TResultSet5>)>
        QueryMultipleIEnumerableAsync<TResultSet1, TResultSet2, TResultSet3, TResultSet4, TResultSet5>(
            this ICaeriusDbConnectionFactory connectionFactory,
            StoredProcedureParametersBuilder spParameters)
        where TResultSet1 : class, ISpMapper<TResultSet1>
        where TResultSet2 : class, ISpMapper<TResultSet2>
        where TResultSet3 : class, ISpMapper<TResultSet3>
        where TResultSet4 : class, ISpMapper<TResultSet4>
        where TResultSet5 : class, ISpMapper<TResultSet5>
    {
        var results = await connectionFactory.QueryMultipleAsync(spParameters);
        return ((IEnumerable<TResultSet1>)results[0], (IEnumerable<TResultSet2>)results[1],
            (IEnumerable<TResultSet3>)results[2],
            (IEnumerable<TResultSet4>)results[3], (IEnumerable<TResultSet5>)results[4]);
    }

    private static async Task<object[]> QueryMultipleAsync(this ICaeriusDbConnectionFactory connectionFactory,
        StoredProcedureParametersBuilder spParameters)
    {
        var connection = connectionFactory.DbConnection();
        return await SqlCommandUtility.MultipleQueryAsync(connection, spParameters);
    }
}