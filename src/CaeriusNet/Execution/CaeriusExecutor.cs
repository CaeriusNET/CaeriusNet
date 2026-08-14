using CaeriusNet.Transactions;

namespace CaeriusNet.Execution;

internal static class CaeriusExecutor
{
    internal static ICaeriusExecutionContext GetContext(ICaeriusNetDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context as ICaeriusExecutionContext
               ?? throw new ArgumentException("The context was not created by AddCaeriusNet.", nameof(context));
    }

    internal static async ValueTask<T> RunAsync<T>(
        ICaeriusExecutionContext context,
        StoredProcedureCommand command,
        Func<SqlCommand, CancellationToken, ValueTask<T>> operation,
        CaeriusNetTransaction? transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(operation);
        RethrowCancellationIfRequested(cancellationToken, transaction);

        ICaeriusSqlConnectionLease? lease = null;
        var ownsLease = transaction is null;
        if (transaction is not null)
            transaction.AcquireCommandSlot();

        using var instrumentation = context.Instrumentation.Start(
            command,
            transaction is not null,
            context.Options);
        try
        {
            var connection = transaction?.Connection;
            if (connection is null)
                try
                {
                    lease = await context.OpenLeaseAsync(cancellationToken).ConfigureAwait(false);
                    connection = lease.Connection;
                    instrumentation.SetConnection(connection);
                }
                catch (OperationCanceledException)
                {
                    transaction?.Poison();
                    throw;
                }
                catch (SqlException exception)
                {
                    RethrowCancellationIfRequested(cancellationToken, transaction);
                    var wrapped = new CaeriusNetSqlException(command.FullName, exception);
                    instrumentation.Failure(wrapped);
                    throw wrapped;
                }
                catch (Exception exception)
                {
                    instrumentation.Failure(exception);
                    throw;
                }
            else
                instrumentation.SetConnection(connection);

            await using var sqlCommand = command.CreateSqlCommand(connection, transaction?.Transaction);
            try
            {
                var result = await operation(sqlCommand, cancellationToken).ConfigureAwait(false);
                // A transactional command is never allowed to publish an invalidation before its
                // enclosing SQL transaction has committed. Register it while the command still owns
                // the transaction slot; CommitAsync performs the strict invalidation afterwards.
                // Applying this here also covers procedures which return rows or output parameters.
                transaction?.DeferInvalidations(command.InvalidationKeys);
                instrumentation.Success();
                return result;
            }
            catch (OperationCanceledException)
            {
                // Cancellation may interrupt a TDS exchange after SQL Server has accepted the command.
                // A shared transaction cannot safely execute another command or commit after that point.
                transaction?.Poison();
                throw;
            }
            catch (SqlException exception)
            {
                RethrowCancellationIfRequested(cancellationToken, transaction);
                var wrapped = new CaeriusNetSqlException(command.FullName, exception);
                instrumentation.Failure(wrapped);
                transaction?.Poison();
                throw wrapped;
            }
            catch (Exception exception)
            {
                instrumentation.Failure(exception);
                transaction?.Poison();
                throw;
            }
        }
        finally
        {
            if (transaction is not null)
                transaction.ReleaseCommandSlot();
            if (ownsLease && lease is not null)
                await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reads the scalar contract through a data reader instead of
    ///     <see cref="SqlCommand.ExecuteScalarAsync(CancellationToken)" />.
    ///     Doing so lets the runtime distinguish an empty result from a SQL NULL, validate cardinality, and fully consume the
    ///     reader before output parameters are observed.
    /// </summary>
    internal static async ValueTask<CaeriusScalarRow> ReadScalarAsync(
        SqlCommand sqlCommand,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
            .ConfigureAwait(false);

        if (reader.FieldCount != 1)
        {
            await DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
            throw new CaeriusContractException(
                $"Stored procedure '{command.FullName}' returned {reader.FieldCount} scalar columns; exactly one was expected.");
        }

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await EnsureNoAdditionalResultSetsAsync(reader, command, cancellationToken).ConfigureAwait(false);
            return CaeriusScalarRow.None;
        }

        var value = reader.GetValue(0);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
            throw new CaeriusContractException(
                $"Stored procedure '{command.FullName}' returned more than one scalar row.");
        }

        await EnsureNoAdditionalResultSetsAsync(reader, command, cancellationToken).ConfigureAwait(false);
        return new CaeriusScalarRow(true, value);
    }

    /// <summary>
    ///     Executes a stored procedure which is contractually forbidden from returning business result sets.
    ///     The reader is fully consumed and disposed before this method completes, which also makes SQL output
    ///     and return-value parameters observable by its caller.
    /// </summary>
    internal static async ValueTask ExecuteNoResultAsync(
        SqlCommand sqlCommand,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await sqlCommand.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            if (reader.FieldCount != 0)
            {
                await DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
                throw new CaeriusContractException(
                    $"Stored procedure '{command.FullName}' returned a result set, but this execution contract expects no business result.");
            }

            if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                return;
        }
    }

    internal static ValueTask<TValue> CachedAsync<TValue>(
        ICaeriusExecutionContext context,
        StoredProcedureCommand command,
        string shape,
        Func<CancellationToken, ValueTask<TValue>> sqlFactory,
        CancellationToken cancellationToken,
        ICaeriusCacheCodec<TValue>? codecOverride = null) where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(sqlFactory);
        return CachedAsync<TValue, Func<CancellationToken, ValueTask<TValue>>>(
            context,
            command,
            shape,
            sqlFactory,
            static (factoryState, token) => factoryState(token),
            cancellationToken,
            codecOverride);
    }

    internal static async ValueTask<TValue> CachedAsync<TValue, TState>(
        ICaeriusExecutionContext context,
        StoredProcedureCommand command,
        string shape,
        TState state,
        Func<TState, CancellationToken, ValueTask<TValue>> sqlFactory,
        CancellationToken cancellationToken,
        ICaeriusCacheCodec<TValue>? codecOverride = null) where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(sqlFactory);
        var policy = command.CachePolicy;
        if (policy is null)
            return await sqlFactory(state, cancellationToken).ConfigureAwait(false);

        var databaseIdentity = context.Options.CacheDatabaseIdentity;
        if (policy.Tier == CaeriusCacheTier.Redis && string.IsNullOrWhiteSpace(databaseIdentity))
            throw new CaeriusContractException(
                "Redis caching requires CaeriusNetOptions.CacheDatabaseIdentity. Use UseSqlServer(string) or set a stable non-secret database identity for a connection or lease factory.");

        var codec = codecOverride ?? FindCodec<TValue>(context);
        var key = CaeriusCacheKeyBuilder.Create(
            context.Options.ApplicationNamespace,
            databaseIdentity ?? context.Name,
            command,
            shape,
            codec?.ContractHash);

        switch (policy.Tier)
        {
            case CaeriusCacheTier.Memory:
                var memoryHit = context.MemoryCache.TryGet<TValue>(key, out var memoryValue);
                context.Instrumentation.CacheLookup(CaeriusCacheTier.Memory, memoryHit);
                if (memoryHit)
                    return memoryValue!;
                return await context.MemoryCache.GetOrCreateAsync(
                    key,
                    policy.Expiration ?? throw new CaeriusContractException("Memory cache requires an expiration."),
                    policy.SlidingExpiration,
                    state,
                    sqlFactory,
                    cancellationToken).ConfigureAwait(false);

            case CaeriusCacheTier.Frozen:
                var frozenHit = context.FrozenCache.TryGet<TValue>(key, out var frozenValue);
                context.Instrumentation.CacheLookup(CaeriusCacheTier.Frozen, frozenHit);
                return frozenHit
                    ? frozenValue!
                    : await sqlFactory(state, cancellationToken).ConfigureAwait(false);

            case CaeriusCacheTier.Redis:
                if (codec is null)
                    throw new CaeriusContractException(
                        $"Redis caching '{shape}' requires an ICaeriusCacheCodec<{typeof(TValue).FullName}> registration.");
                if (context.RemoteCaches.Count == 0)
                    throw new CaeriusContractException(
                        $"Redis caching '{shape}' requires an IRedisCacheManager registration. Call AddCaeriusNetRedis(...).");
                var redisState = new RedisReadThroughState<TValue, TState>(
                    context,
                    command,
                    key,
                    policy.Expiration,
                    codec,
                    state,
                    sqlFactory);
                return await context.RedisReadThrough.GetOrCreateAsync<TValue, RedisReadThroughState<TValue, TState>>(
                    key,
                    redisState,
                    static (readThroughState, token) => ReadThroughRedisAsync(readThroughState, token),
                    cancellationToken).ConfigureAwait(false);

            default:
                throw new ArgumentOutOfRangeException(nameof(command), policy.Tier,
                    "The command specifies an unknown Caerius cache tier.");
        }
    }

    private static async ValueTask<TValue> ReadThroughRedisAsync<TValue, TState>(
        RedisReadThroughState<TValue, TState> state,
        CancellationToken cancellationToken) where TValue : notnull
    {
        foreach (var remote in state.Context.RemoteCaches)
            try
            {
                var lookup = await remote.TryGetAsync(state.Key, state.Codec, cancellationToken).ConfigureAwait(false);
                state.Context.Instrumentation.CacheLookup(CaeriusCacheTier.Redis, lookup.Found);
                if (lookup.Found)
                    return lookup.Value!;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Telemetry.CaeriusRuntimeLog.RedisLookupFailed(
                    state.Context.Logger,
                    state.Command.FullName,
                    exception.GetType().FullName ?? exception.GetType().Name);
                state.Context.Instrumentation.CacheError(CaeriusCacheTier.Redis);
            }

        var value = await state.SqlFactory(state.FactoryState, cancellationToken).ConfigureAwait(false);
        foreach (var remote in state.Context.RemoteCaches)
            try
            {
                await remote.SetAsync(state.Key, value, state.Codec, state.Expiration, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Telemetry.CaeriusRuntimeLog.RedisPopulationFailed(
                    state.Context.Logger,
                    state.Command.FullName,
                    exception.GetType().FullName ?? exception.GetType().Name);
                state.Context.Instrumentation.CacheError(CaeriusCacheTier.Redis);
            }

        return value;
    }

    internal static async ValueTask InvalidateStrictAsync(
        ICaeriusExecutionContext context,
        IEnumerable<string> keys,
        CancellationToken cancellationToken)
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            context.MemoryCache.Remove(key);
            context.FrozenCache.Remove(key);
            foreach (var remote in context.RemoteCaches)
                try
                {
                    await remote.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (CaeriusCacheException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new CaeriusCacheException("remove", exception);
                }
        }
    }

    internal static ICaeriusCacheCodec<T>? FindCodec<T>(ICaeriusExecutionContext context)
    {
        return context.Codecs.OfType<ICaeriusCacheCodec<T>>().SingleOrDefault();
    }

    /// <summary>
    ///     SqlClient may surface an operation cancelled by the supplied token as <see cref="SqlException" />, notably
    ///     while it is completing an async reader. The public contract keeps cancellation distinguishable from a SQL
    ///     fault; a transaction is poisoned before the original cancellation is rethrown.
    /// </summary>
    internal static void RethrowCancellationIfRequested(
        CancellationToken cancellationToken,
        CaeriusNetTransaction? transaction = null)
    {
        if (!cancellationToken.IsCancellationRequested)
            return;

        transaction?.Poison();
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static CaeriusOutputValues ReadOutputs(SqlCommand sqlCommand)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (SqlParameter parameter in sqlCommand.Parameters)
        {
            if (parameter.Direction == ParameterDirection.Input)
                continue;
            values[parameter.ParameterName] = parameter.Value is DBNull ? null : parameter.Value;
        }

        return new CaeriusOutputValues(new ReadOnlyDictionary<string, object?>(values));
    }

    internal static async ValueTask DrainRemainingAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
        }

        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
        }
    }

    internal static async ValueTask EnsureNoAdditionalResultSetsAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            return;
        await DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
        throw new CaeriusContractException(
            $"Stored procedure '{command.FullName}' returned additional result sets; exactly one was expected.");
    }

    internal static T ConvertScalar<T>(object value, StoredProcedureCommand command)
    {
        if (value is T typed)
            return typed;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        try
        {
            if (target.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(target);
                var numeric = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture)!;
                return (T)Enum.ToObject(target, numeric);
            }

            if (target == typeof(Guid))
                return (T)(object)(value switch
                {
                    Guid guid => guid,
                    string text => Guid.Parse(text),
                    byte[] bytes => new Guid(bytes),
                    _ => throw new InvalidCastException()
                });
            if (target == typeof(DateOnly) && value is DateTime date)
                return (T)(object)DateOnly.FromDateTime(date);
            if (target == typeof(TimeOnly) && value is TimeSpan time)
                return (T)(object)TimeOnly.FromTimeSpan(time);
            return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            throw new CaeriusContractException(
                $"Scalar from '{command.FullName}' cannot be converted from {value.GetType().FullName} to {typeof(T).FullName}.",
                exception);
        }
    }

    internal static T ConvertRequiredScalar<T>(CaeriusScalarRow row, StoredProcedureCommand command)
    {
        if (!row.HasRow)
            throw new CaeriusContractException($"Stored procedure '{command.FullName}' returned no scalar row.");
        return ConvertNullableScalar<T>(row.Value, command);
    }

    internal static T? ConvertOptionalScalar<T>(CaeriusScalarRow row, StoredProcedureCommand command)
    {
        return row.HasRow ? ConvertNullableScalar<T>(row.Value, command) : default;
    }

    private static T ConvertNullableScalar<T>(object? value, StoredProcedureCommand command)
    {
        if (value is null or DBNull)
        {
            if (default(T) is null)
                return default!;
            throw new CaeriusContractException(
                $"Stored procedure '{command.FullName}' returned SQL NULL for its scalar value of non-nullable type {typeof(T).FullName}.");
        }

        return ConvertScalar<T>(value, command);
    }
}

internal readonly record struct RedisReadThroughState<TValue, TState>(
    ICaeriusExecutionContext Context,
    StoredProcedureCommand Command,
    string Key,
    TimeSpan? Expiration,
    ICaeriusCacheCodec<TValue> Codec,
    TState FactoryState,
    Func<TState, CancellationToken, ValueTask<TValue>> SqlFactory)
    where TValue : notnull;

internal readonly record struct CaeriusScalarRow(bool HasRow, object? Value)
{
    internal static CaeriusScalarRow None => default;
}
