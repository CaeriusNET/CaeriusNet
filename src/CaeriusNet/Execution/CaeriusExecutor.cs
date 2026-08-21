using System.Data.SqlTypes;
using CaeriusNet.Telemetry;
using CaeriusNet.Transactions;

namespace CaeriusNet.Execution;

internal static class CaeriusExecutor
{
    internal static void EnsureStreamHasNoOutputs(StoredProcedureCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Parameters.Any(static parameter => parameter.Direction != ParameterDirection.Input))
            throw new CaeriusContractException(
                $"Stored procedure '{command.FullName}' declares output, input/output, or return-value parameters; StreamAsync cannot expose their values.");
    }

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
        cancellationToken.ThrowIfCancellationRequested();

        ICaeriusSqlConnectionLease? lease = null;
        var ownsLease = transaction is null;
        if (transaction is not null)
            transaction.AcquireCommandSlot();

        var instrumentation = context.Instrumentation.Start(
            command,
            transaction is not null,
            context.Options);
        SqlCommand? sqlCommand = null;
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

            sqlCommand = command.CreateSqlCommand(
                connection,
                transaction?.Transaction,
                cancellationToken);
            try
            {
                var result = await operation(sqlCommand, cancellationToken).ConfigureAwait(false);
                // A transactional command is never allowed to publish an invalidation before its
                // enclosing SQL transaction has committed. Register it while the command still owns
                // the transaction slot; CommitAsync performs the strict invalidation afterwards.
                // Applying this here also covers procedures which return rows or output parameters.
                if (transaction is null)
                    // The procedure has completed successfully at this point, but SqlCommand/lease
                    // disposal still remains.  Invalidate before that best-effort cleanup so a
                    // cleanup failure cannot leave a durable SQL write behind stale cache data.
                    await InvalidateAfterDurableSqlAsync(context, command.InvalidationTags).ConfigureAwait(false);
                else
                    transaction.DeferInvalidations(command.InvalidationTags);
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
            // Cleanup occurs after the durable-SQL invalidation above.  Every resource gets an
            // opportunity to clean up, and a provider teardown fault cannot replace the SQL,
            // mapper, cancellation, or cache-invalidation failure which brought us here.
            if (sqlCommand is not null)
                await DisposeWithoutMaskingAsync(sqlCommand).ConfigureAwait(false);
            DisposeInstrumentationWithoutMasking(instrumentation);
            if (transaction is not null)
                transaction.ReleaseCommandSlot();
            if (ownsLease && lease is not null)
                await DisposeWithoutMaskingAsync(lease).ConfigureAwait(false);
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

        if (!await MoveToFirstColumnResultAsync(reader, cancellationToken).ConfigureAwait(false))
            return CaeriusScalarRow.None;

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
            static (_, factoryState, token) => factoryState(token),
            cancellationToken,
            codecOverride);
    }

    internal static async ValueTask<TValue> CachedAsync<TValue, TState>(
        ICaeriusExecutionContext context,
        StoredProcedureCommand command,
        string shape,
        TState state,
        Func<ICaeriusExecutionContext, TState, CancellationToken, ValueTask<TValue>> sqlFactory,
        CancellationToken cancellationToken,
        ICaeriusCacheCodec<TValue>? codecOverride = null) where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(sqlFactory);
        var policy = command.CachePolicy;
        if (policy is null)
            return await sqlFactory(context, state, cancellationToken).ConfigureAwait(false);

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
        var localKey = context.CacheTags.CreateVersionedKey(key, command.DependencyTags);

        switch (policy.Tier)
        {
            case CaeriusCacheTier.Memory:
                var memoryHit = context.MemoryCache.TryGet<TValue>(localKey, out var memoryValue);
                context.Instrumentation.CacheLookup(CaeriusCacheTier.Memory, memoryHit);
                if (memoryHit)
                    return memoryValue!;
                return await context.MemoryCache.GetOrCreateAsync(
                    localKey,
                    policy.Expiration ?? throw new CaeriusContractException("Memory cache requires an expiration."),
                    policy.SlidingExpiration,
                    state,
                    (factoryState, token) => sqlFactory(context, factoryState, token),
                    cancellationToken).ConfigureAwait(false);

            case CaeriusCacheTier.Frozen:
                var frozenHit = context.FrozenCache.TryGet<TValue>(localKey, out var frozenValue);
                context.Instrumentation.CacheLookup(CaeriusCacheTier.Frozen, frozenHit);
                return frozenHit
                    ? frozenValue!
                    : await sqlFactory(context, state, cancellationToken).ConfigureAwait(false);

            case CaeriusCacheTier.Redis:
                if (codec is null)
                    throw new CaeriusContractException(
                        $"Redis caching '{shape}' requires an ICaeriusCacheCodec<{typeof(TValue).FullName}> registration.");
                if (context.RemoteCaches.Count == 0)
                    throw new CaeriusContractException(
                        $"Redis caching '{shape}' requires an IRedisCacheManager registration. Call AddCaeriusNetRedis(...).");
                var redisState = new RedisReadThroughState<TValue, TState>(
                    context.ScopeFactory,
                    context.Name,
                    context.IsKeyed,
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
        // A singleton coordinator must not retain a request scope. Every shared population owns a fresh child scope,
        // so an early caller can dispose or cancel its own scope without invalidating a surviving cache miss.
        await using var scope = state.ScopeFactory.CreateAsyncScope();
        var database = state.IsKeyed
            ? scope.ServiceProvider.GetRequiredKeyedService<ICaeriusNetDbContext>(state.ContextName)
            : scope.ServiceProvider.GetRequiredService<ICaeriusNetDbContext>();
        var context = GetContext(database);

        var snapshots = new CaeriusCacheTagSnapshot[context.RemoteCaches.Count];
        for (var index = 0; index < context.RemoteCaches.Count; index++)
            try
            {
                var remote = context.RemoteCaches[index];
                snapshots[index] = await remote.CaptureTagSnapshotAsync(
                    state.Command.DependencyTags,
                    cancellationToken).ConfigureAwait(false);
                var lookup = await remote.TryGetAsync(
                    state.Key,
                    state.Command.DependencyTags,
                    state.Codec,
                    cancellationToken).ConfigureAwait(false);
                context.Instrumentation.CacheLookup(CaeriusCacheTier.Redis, lookup.Found);
                if (lookup.Found)
                    return lookup.Value!;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                RecordRedisLookupFailure(context, state, snapshots, index, exception);
            }

        var value = await state.SqlFactory(context, state.FactoryState, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < context.RemoteCaches.Count; index++)
            try
            {
                var remote = context.RemoteCaches[index];
                _ = await remote.SetIfTagSnapshotUnchangedAsync(
                        state.Key,
                        value,
                        state.Command.DependencyTags,
                        snapshots[index],
                        state.Codec,
                        state.Expiration,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                RecordRedisPopulationFailure(context, state, exception);
            }

        return value;
    }

    private static void RecordRedisLookupFailure<TValue, TState>(
        ICaeriusExecutionContext context,
        RedisReadThroughState<TValue, TState> state,
        CaeriusCacheTagSnapshot[] snapshots,
        int index,
        Exception exception) where TValue : notnull
    {
        CaeriusRuntimeLog.RedisLookupFailed(
            context.Logger,
            state.Command.FullName,
            exception.GetType().FullName ?? exception.GetType().Name);
        context.Instrumentation.CacheError(CaeriusCacheTier.Redis);
        snapshots[index] = new CaeriusCacheTagSnapshot(
            ImmutableArray.CreateRange(Enumerable.Repeat(0L, state.Command.DependencyTags.Length)));
    }

    private static void RecordRedisPopulationFailure<TValue, TState>(
        ICaeriusExecutionContext context,
        RedisReadThroughState<TValue, TState> state,
        Exception exception) where TValue : notnull
    {
        CaeriusRuntimeLog.RedisPopulationFailed(
            context.Logger,
            state.Command.FullName,
            exception.GetType().FullName ?? exception.GetType().Name);
        context.Instrumentation.CacheError(CaeriusCacheTier.Redis);
    }

    private static bool IsNonFatal(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException
            and not AppDomainUnloadedException
            and not BadImageFormatException
            and not CannotUnloadAppDomainException
            and not System.Threading.ThreadAbortException;
    }

    internal static async ValueTask InvalidateStrictAsync(
        ICaeriusExecutionContext context,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
        if (distinctTags.Length == 0)
            return;

        context.CacheTags.Invalidate(distinctTags);
        foreach (var remote in context.RemoteCaches)
            try
            {
                await remote.InvalidateTagsAsync(distinctTags, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CaeriusCacheException)
            {
                throw;
            }
            catch (Exception exception) when (IsNonFatal(exception))
            {
                throw new CaeriusCacheException("invalidate-tags", exception);
            }
    }

    internal static async ValueTask InvalidateAfterDurableSqlAsync(
        ICaeriusExecutionContext context,
        IEnumerable<string> tags)
    {
        try
        {
            await InvalidateStrictAsync(context, tags, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not CaeriusCacheInvalidationException && IsNonFatal(exception))
        {
            throw new CaeriusCacheInvalidationException(exception);
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

            // Value requests SqlClient's CLR-decimal projection.  That projection overflows for
            // SQL decimal(29..38), even though SqlValue can faithfully expose the SQL value as a
            // SqlDecimal.  Do not touch Value for decimals: merely reading it is enough to lose a
            // perfectly valid output parameter.
            var value = parameter.SqlDbType == SqlDbType.Decimal
                ? parameter.SqlValue
                : parameter.Value;
            values[parameter.ParameterName] = value is DBNull or INullable { IsNull: true } ? null : value;
        }

        return new CaeriusOutputValues(new ReadOnlyDictionary<string, object?>(values));
    }

    internal static async ValueTask DrainRemainingAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Intentionally drain every remaining row in the current result set.
        }

        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Intentionally drain every row in each additional result set.
        }
    }

    internal static async ValueTask EnsureNoAdditionalResultSetsAsync(
        SqlDataReader reader,
        StoredProcedureCommand command,
        CancellationToken cancellationToken)
    {
        if (!await MoveToNextColumnResultAsync(reader, cancellationToken).ConfigureAwait(false))
            return;
        await DrainRemainingAsync(reader, cancellationToken).ConfigureAwait(false);
        throw new CaeriusContractException(
            $"Stored procedure '{command.FullName}' returned additional result sets; exactly one was expected.");
    }

    internal static T ConvertScalar<T>(object value, StoredProcedureCommand command)
    {
        if (value is T typed)
            return typed;
        try
        {
            return (T)CaeriusValueConverter.Convert(value, typeof(T));
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or
                                              SqlTypeException)
        {
            throw new CaeriusContractException(
                $"Scalar from '{command.FullName}' cannot be converted from {value.GetType().FullName} to {typeof(T).FullName}.",
                exception);
        }
    }

    internal static async ValueTask<bool> MoveToFirstColumnResultAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        if (reader.FieldCount > 0)
            return true;
        return await MoveToNextColumnResultAsync(reader, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask<bool> MoveToNextColumnResultAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            if (reader.FieldCount > 0)
                return true;
        return false;
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

    private static async ValueTask DisposeWithoutMaskingAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // Cleanup is best effort. A closed/broken pooled connection is discarded by SqlClient;
            // it must not make a durable-write outcome or its cache-invalidation exception ambiguous.
        }
    }

    private static void DisposeInstrumentationWithoutMasking(CaeriusInstrumentation.Operation instrumentation)
    {
        try
        {
            instrumentation.Dispose();
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // Observability is deliberately non-authoritative for database correctness.
        }
    }
}

internal readonly record struct RedisReadThroughState<TValue, TState>(
    IServiceScopeFactory ScopeFactory,
    string ContextName,
    bool IsKeyed,
    StoredProcedureCommand Command,
    string Key,
    TimeSpan? Expiration,
    ICaeriusCacheCodec<TValue> Codec,
    TState FactoryState,
    Func<ICaeriusExecutionContext, TState, CancellationToken, ValueTask<TValue>> SqlFactory)
    where TValue : notnull;

internal readonly record struct CaeriusScalarRow(bool HasRow, object? Value)
{
    internal static CaeriusScalarRow None => default;
}
