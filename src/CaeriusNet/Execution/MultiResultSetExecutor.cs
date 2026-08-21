using CaeriusNet.Mappers;
using CaeriusNet.Transactions;

namespace CaeriusNet.Execution;

/// <summary>
///     Immutable, per-closed-generic metadata for a generated multi-result API. The source generator creates one
///     instance in a static generic holder, so the readers, empty-value factories, result types and cache shape are
///     never allocated on an individual database call.
/// </summary>
internal sealed class MultiResultSetPlan
{
    internal MultiResultSetPlan(
        MultiResultSetExecutor.ResultSetReader[] readers,
        Func<object>[] emptyFactories,
        Type[] resultTypes,
        string shape)
    {
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentNullException.ThrowIfNull(emptyFactories);
        ArgumentNullException.ThrowIfNull(resultTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(shape);

        if (readers.Length is < 2 or > 10 || emptyFactories.Length != readers.Length ||
            resultTypes.Length != readers.Length)
            throw new ArgumentException("Multi-result metadata must have a matching arity between two and ten.");

        Readers = readers;
        EmptyFactories = emptyFactories;
        ResultTypes = resultTypes;
        Shape = shape;
        AllowMissingShape = string.Concat(shape, ":result-set-policy:allow-missing");
        AllowAdditionalShape = string.Concat(shape, ":result-set-policy:allow-additional");
        AllowMissingOrAdditionalShape = string.Concat(shape, ":result-set-policy:allow-missing-or-additional");
    }

    internal MultiResultSetExecutor.ResultSetReader[] Readers { get; }
    internal Func<object>[] EmptyFactories { get; }
    internal Type[] ResultTypes { get; }
    internal string Shape { get; }
    private string AllowMissingShape { get; }
    private string AllowAdditionalShape { get; }
    private string AllowMissingOrAdditionalShape { get; }
    internal int Arity => Readers.Length;

    internal string GetCacheShape(CaeriusResultSetCountPolicy policy)
    {
        return policy switch
        {
            CaeriusResultSetCountPolicy.Exactly => Shape,
            CaeriusResultSetCountPolicy.AllowMissing => AllowMissingShape,
            CaeriusResultSetCountPolicy.AllowAdditional => AllowAdditionalShape,
            CaeriusResultSetCountPolicy.AllowMissingOrAdditional => AllowMissingOrAdditionalShape,
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
    }
}

/// <summary>
///     A stack/value-type carrier for the caller supplied result-set capacities. It intentionally replaces the
///     short-lived <c>int[]</c> previously created by every generated multi-result invocation.
/// </summary>
internal readonly struct MultiResultSetCapacities
{
    private readonly int _first;
    private readonly int _second;
    private readonly int _third;
    private readonly int _fourth;
    private readonly int _fifth;
    private readonly int _sixth;
    private readonly int _seventh;
    private readonly int _eighth;
    private readonly int _ninth;
    private readonly int _tenth;

    private MultiResultSetCapacities(
        int count,
        int first,
        int second,
        int third = default,
        int fourth = default,
        int fifth = default,
        int sixth = default,
        int seventh = default,
        int eighth = default,
        int ninth = default,
        int tenth = default)
    {
        Count = count;
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        _fifth = fifth;
        _sixth = sixth;
        _seventh = seventh;
        _eighth = eighth;
        _ninth = ninth;
        _tenth = tenth;
    }

    internal int Count { get; }

    internal int this[int index] => index switch
    {
        0 => _first,
        1 => _second,
        2 => _third,
        3 => _fourth,
        4 => _fifth,
        5 => _sixth,
        6 => _seventh,
        7 => _eighth,
        8 => _ninth,
        9 => _tenth,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    internal static MultiResultSetCapacities Create2(int first, int second)
    {
        return new MultiResultSetCapacities(2, first, second);
    }

    internal static MultiResultSetCapacities Create3(int first, int second, int third)
    {
        return new MultiResultSetCapacities(3, first, second, third);
    }

    internal static MultiResultSetCapacities Create4(int first, int second, int third, int fourth)
    {
        return new MultiResultSetCapacities(4, first, second, third, fourth);
    }

    internal static MultiResultSetCapacities Create5(int first, int second, int third, int fourth, int fifth)
    {
        return new MultiResultSetCapacities(5, first, second, third, fourth, fifth);
    }

    internal static MultiResultSetCapacities Create6(int first, int second, int third, int fourth, int fifth, int sixth)
    {
        return new MultiResultSetCapacities(6, first, second, third, fourth, fifth, sixth);
    }

    internal static MultiResultSetCapacities Create7(
        int first, int second, int third, int fourth, int fifth, int sixth, int seventh)
    {
        return new MultiResultSetCapacities(7, first, second, third, fourth, fifth, sixth, seventh);
    }

    internal static MultiResultSetCapacities Create8(
        int first, int second, int third, int fourth, int fifth, int sixth, int seventh, int eighth)
    {
        return new MultiResultSetCapacities(8, first, second, third, fourth, fifth, sixth, seventh, eighth);
    }

    internal static MultiResultSetCapacities Create9(
        int first, int second, int third, int fourth, int fifth, int sixth, int seventh, int eighth, int ninth)
    {
        return new MultiResultSetCapacities(9, first, second, third, fourth, fifth, sixth, seventh, eighth, ninth);
    }

    internal static MultiResultSetCapacities Create10(
        int first, int second, int third, int fourth, int fifth, int sixth, int seventh, int eighth, int ninth,
        int tenth)
    {
        return new MultiResultSetCapacities(10, first, second, third, fourth, fifth, sixth, seventh, eighth, ninth,
            tenth);
    }

    internal void Validate(MultiResultSetPlan plan, string capacityParameterName)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(capacityParameterName);
        if (Count != plan.Arity)
            throw new ArgumentException("Multi-result capacities must match the generated result-set arity.");

        for (var index = 0; index < Count; index++)
            if (this[index] < StoredProcedureCommand.UnspecifiedResultSetCapacity)
                throw new ArgumentOutOfRangeException(
                    capacityParameterName,
                    "Result-set capacities must be -1 or non-negative.");
    }
}

internal static class MultiResultSetExecutor
{
    internal static async ValueTask<object[]> ExecuteAsync(
        ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        capacities.Validate(plan, nameof(capacities));
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));

        var execution = CaeriusExecutor.GetContext(context);
        ICaeriusCacheCodec<object[]>? codec = null;
        if (command.CachePolicy?.Tier == CaeriusCacheTier.Redis)
            codec = ResolveAggregateCodec(execution, plan);
        return await CaeriusExecutor.CachedAsync<object[], MultiResultCacheState>(
            execution,
            command,
            plan.GetCacheShape(policy),
            new MultiResultCacheState(command, capacities, policy, plan),
            static (activeContext, state, token) => ExecuteSqlAsync(
                activeContext,
                state.Command,
                state.Capacities,
                state.Policy,
                state.Plan,
                token),
            cancellationToken,
            codec).ConfigureAwait(false);
    }

    /// <summary>
    ///     Transactional counterpart of the non-transactional generated-plan execution path.
    ///     It deliberately bypasses all read-through caches and relies on <see cref="CaeriusExecutor.RunAsync{T}" />
    ///     to defer declared invalidations until the owning transaction commits.
    /// </summary>
    internal static async ValueTask<object[]> ExecuteAsync(
        ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        ValidateResultSetArguments(capacities, policy, plan);
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                return await ReadResultSetsAsync(reader, command, capacities, policy, plan, token)
                    .ConfigureAwait(false);
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<CaeriusExecutionResult<object[], CaeriusOutputValues>> ExecuteWithOutputsAsync(
        ICaeriusNetDbContext context,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        ValidateResultSetArguments(capacities, policy, plan);

        var execution = CaeriusExecutor.GetContext(context);
        var result = await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                object[] results;
                try
                {
                    results = await ReadResultSetsAsync(
                        reader, command, capacities, policy, plan, token).ConfigureAwait(false);
                }
                finally
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                }

                return new CaeriusExecutionResult<object[], CaeriusOutputValues>(
                    results,
                    CaeriusExecutor.ReadOutputs(sqlCommand));
            },
            null,
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    ///     Executes multi-result stored procedures in one transaction, fully consumes and disposes the reader,
    ///     then observes SQL outputs. Cache invalidations are registered by the transaction and never run before
    ///     a successful commit.
    /// </summary>
    internal static async ValueTask<CaeriusExecutionResult<object[], CaeriusOutputValues>> ExecuteWithOutputsAsync(
        ICaeriusNetTransaction transaction,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        ValidateResultSetArguments(capacities, policy, plan);
        var tx = GetTransaction(transaction);
        return await CaeriusExecutor.RunAsync(
            tx.Context,
            command,
            async (sqlCommand, token) =>
            {
                var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                object[] results;
                try
                {
                    results = await ReadResultSetsAsync(
                        reader, command, capacities, policy, plan, token).ConfigureAwait(false);
                }
                finally
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                }

                return new CaeriusExecutionResult<object[], CaeriusOutputValues>(
                    results,
                    CaeriusExecutor.ReadOutputs(sqlCommand));
            },
            tx,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<object> ReadArrayAsync<T>(
        SqlDataReader reader,
        StoredProcedureCommand command,
        int resultSetIndex,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return (await ReadListAsync<T>(reader, command, resultSetIndex, capacity, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    }

    internal static async ValueTask<object> ReadReadOnlyCollectionAsync<T>(
        SqlDataReader reader,
        StoredProcedureCommand command,
        int resultSetIndex,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return new ReadOnlyCollection<T>(
            await ReadListAsync<T>(reader, command, resultSetIndex, capacity, cancellationToken)
                .ConfigureAwait(false));
    }

    internal static async ValueTask<object> ReadImmutableArrayAsync<T>(
        SqlDataReader reader,
        StoredProcedureCommand command,
        int resultSetIndex,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return (await ReadListAsync<T>(reader, command, resultSetIndex, capacity, cancellationToken)
                .ConfigureAwait(false))
            .ToImmutableArray();
    }

    private static async ValueTask<object[]> ExecuteSqlAsync(
        ICaeriusExecutionContext execution,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        return await CaeriusExecutor.RunAsync(
            execution,
            command,
            async (sqlCommand, token) =>
            {
                await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, token)
                    .ConfigureAwait(false);
                return await ReadResultSetsAsync(reader, command, capacities, policy, plan, token)
                    .ConfigureAwait(false);
            },
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<object[]> ReadResultSetsAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan,
        CancellationToken cancellationToken)
    {
        var results = new object[plan.Arity];
        var missing = !await CaeriusExecutor.MoveToFirstColumnResultAsync(reader, cancellationToken)
            .ConfigureAwait(false);
        for (var index = 0; index < plan.Arity; index++)
        {
            if (missing)
            {
                if (!AllowsMissing(policy))
                {
                    await CaeriusExecutor.DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
                    throw new CaeriusContractException(
                        $"Stored procedure '{command.FullName}' omitted result set {index} of {plan.Arity}.");
                }

                for (var emptyIndex = index; emptyIndex < plan.Arity; emptyIndex++)
                    results[emptyIndex] = plan.EmptyFactories[emptyIndex]();
                break;
            }

            var capacity = CaeriusNetCommandExtensions.ResolveCapacity(command, capacities[index], index);
            results[index] = await plan.Readers[index](reader, command, index, capacity, cancellationToken)
                .ConfigureAwait(false);
            if (index < plan.Arity - 1)
                missing = !await CaeriusExecutor.MoveToNextColumnResultAsync(reader, cancellationToken)
                    .ConfigureAwait(false);
        }

        if (!missing && await CaeriusExecutor.MoveToNextColumnResultAsync(reader, cancellationToken)
                .ConfigureAwait(false))
        {
            await CaeriusExecutor.DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
            if (!AllowsAdditional(policy))
                throw new CaeriusContractException(
                    $"Stored procedure '{command.FullName}' returned more than {plan.Arity} result sets.");
        }

        return results;
    }

    private static async ValueTask<List<T>> ReadListAsync<T>(
        SqlDataReader reader,
        StoredProcedureCommand command,
        int resultSetIndex,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var result = new List<T>(capacity);
        long row = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                result.Add(T.MapFromDataReader(reader));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new CaeriusMappingException(command.FullName, resultSetIndex, row, exception);
            }

            row++;
        }

        return result;
    }

    private static bool AllowsMissing(CaeriusResultSetCountPolicy policy)
    {
        return policy is CaeriusResultSetCountPolicy.AllowMissing
            or CaeriusResultSetCountPolicy.AllowMissingOrAdditional;
    }

    private static bool AllowsAdditional(CaeriusResultSetCountPolicy policy)
    {
        return policy is CaeriusResultSetCountPolicy.AllowAdditional
            or CaeriusResultSetCountPolicy.AllowMissingOrAdditional;
    }

    private static void ValidateResultSetArguments(
        MultiResultSetCapacities capacities,
        CaeriusResultSetCountPolicy policy,
        MultiResultSetPlan plan)
    {
        capacities.Validate(plan, nameof(capacities));
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    private static CaeriusAggregateCacheCodec ResolveAggregateCodec(
        ICaeriusExecutionContext execution,
        MultiResultSetPlan plan)
    {
        var codecs = new ICaeriusCacheCodec[plan.Arity];
        for (var resultIndex = 0; resultIndex < plan.Arity; resultIndex++)
        {
            var resultType = plan.ResultTypes[resultIndex];
            ICaeriusCacheCodec? match = null;
            for (var codecIndex = 0; codecIndex < execution.Codecs.Count; codecIndex++)
            {
                var candidate = execution.Codecs[codecIndex];
                if (candidate.ValueType != resultType)
                    continue;
                if (match is not null)
                    throw new CaeriusContractException(
                        $"Redis multi-result caching found more than one codec for {resultType.FullName}.");
                match = candidate;
            }

            codecs[resultIndex] = match ?? throw new CaeriusContractException(
                $"Redis multi-result caching requires a codec for {resultType.FullName}.");
        }

        return new CaeriusAggregateCacheCodec(codecs);
    }

    private static CaeriusNetTransaction GetTransaction(ICaeriusNetTransaction transaction)
    {
        return transaction as CaeriusNetTransaction
               ?? throw new ArgumentException("The transaction was not created by CaeriusNet.", nameof(transaction));
    }

    internal delegate ValueTask<object> ResultSetReader(
        SqlDataReader reader,
        StoredProcedureCommand command,
        int resultSetIndex,
        int capacity,
        CancellationToken cancellationToken);

    private readonly record struct MultiResultCacheState(
        StoredProcedureCommand Command,
        MultiResultSetCapacities Capacities,
        CaeriusResultSetCountPolicy Policy,
        MultiResultSetPlan Plan);
}
