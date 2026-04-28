using System.Diagnostics;

namespace CaeriusNet.Commands.Reads;

/// <summary>
///     Execute asynchronous stored procedure reads for <see cref="ICaeriusNetDbContext" />.
/// </summary>
public static class SimpleReadSqlAsyncCommands
{
    /// <param name="context">Database context used to open the connection.</param>
    extension(ICaeriusNetDbContext context)
    {
        /// <summary>
        ///     Execute a stored procedure and map the first returned row.
        /// </summary>
        /// <typeparam name="TResultSet">Mapped result type.</typeparam>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The mapped row, or <see langword="null" /> when no row is returned.</returns>
        /// <exception cref="CaeriusNetSqlException">Thrown when the stored procedure fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<TResultSet?> FirstQueryAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            const string Operation = nameof(FirstQueryAsync);
            var logger = LoggerProvider.GetLogger();

            // Check cache before starting the SP span so that cache hits do not emit a
            // misleading DB span or record SP duration/execution metrics.
            if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
                if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                        out TResultSet? cachedResult))
                {
                    CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType.Value, true);
                    if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        logger.LogCacheHitSkippingExecution(spParameters.CacheKey);
                    return cachedResult;
                }
                else
                {
                    CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType.Value, false);
                }

            using var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(spParameters, Operation);
            var tags = CaeriusActivityExtensions.BuildMetricTags(spParameters, Operation);
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                await using var connection = await context.DbConnectionAsync(cancellationToken).ConfigureAwait(false);
                var result = await SqlCommandHelper.ScalarQueryAsync<TResultSet>(
                    spParameters, connection, cancellationToken).ConfigureAwait(false);

                if (result is not null)
                    CacheHelper.StoreInCache(spParameters, context.RedisCacheManager, result);

                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                var rows = result is null ? 0 : 1;
                CaeriusActivityExtensions.RecordSuccess(activity, tags, elapsedMs, rows);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)elapsedMs,
                        rows);

                return result;
            }
            catch (SqlException ex)
            {
                CaeriusActivityExtensions.RecordError(activity, tags, ex);
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
        }

        /// <summary>
        ///     Execute a stored procedure and materialize the result set as a read-only collection.
        /// </summary>
        /// <typeparam name="TResultSet">Mapped result type.</typeparam>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A read-only collection containing the mapped rows.</returns>
        /// <exception cref="CaeriusNetSqlException">Thrown when the stored procedure fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<ReadOnlyCollection<TResultSet>> QueryAsReadOnlyCollectionAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            const string Operation = nameof(QueryAsReadOnlyCollectionAsync);
            var logger = LoggerProvider.GetLogger();

            // Check cache before starting the SP span so that cache hits do not emit a
            // misleading DB span or record SP duration/execution metrics.
            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out ReadOnlyCollection<TResultSet>? cachedResult) &&
                cachedResult != null)
            {
                if (spParameters.CacheKey is not null)
                {
                    CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType!.Value, true);
                    if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        logger.LogCacheHitSkippingExecution(spParameters.CacheKey);
                }

                return cachedResult;
            }

            if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
                CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType.Value, false);

            using var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(spParameters, Operation);
            var tags = CaeriusActivityExtensions.BuildMetricTags(spParameters, Operation);
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                await using var connection = await context.DbConnectionAsync(cancellationToken).ConfigureAwait(false);
                var results = await SqlCommandHelper.ResultSetAsReadOnlyCollectionAsync<TResultSet>(
                    spParameters, connection, cancellationToken).ConfigureAwait(false);

                if (results.Count == 0)
                    results = EmptyCollections.ReadOnlyCollection<TResultSet>();

                CacheHelper.StoreInCache(spParameters, context.RedisCacheManager, results);

                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                CaeriusActivityExtensions.RecordSuccess(activity, tags, elapsedMs, results.Count);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)elapsedMs,
                        results.Count);

                return results;
            }
            catch (SqlException ex)
            {
                CaeriusActivityExtensions.RecordError(activity, tags, ex);
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
        }

        /// <summary>
        ///     Execute a stored procedure and materialize the result set as an enumerable sequence.
        /// </summary>
        /// <typeparam name="TResultSet">Mapped result type.</typeparam>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>An enumerable sequence containing the mapped rows.</returns>
        /// <exception cref="CaeriusNetSqlException">Thrown when the stored procedure fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<IEnumerable<TResultSet>> QueryAsIEnumerableAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            const string Operation = nameof(QueryAsIEnumerableAsync);
            var logger = LoggerProvider.GetLogger();

            // Check cache before starting the SP span so that cache hits do not emit a
            // misleading DB span or record SP duration/execution metrics.
            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out IEnumerable<TResultSet>? cachedResult) &&
                cachedResult != null)
            {
                if (spParameters.CacheKey is not null)
                {
                    CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType!.Value, true);
                    if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        logger.LogCacheHitSkippingExecution(spParameters.CacheKey);
                }

                return cachedResult;
            }

            if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
                CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType.Value, false);

            using var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(spParameters, Operation);
            var tags = CaeriusActivityExtensions.BuildMetricTags(spParameters, Operation);
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                await using var connection = await context.DbConnectionAsync(cancellationToken).ConfigureAwait(false);
                var results = await SqlCommandHelper.ResultSetAsReadOnlyCollectionAsync<TResultSet>(
                    spParameters, connection, cancellationToken).ConfigureAwait(false);

                if (results.Count == 0)
                    results = EmptyCollections.ReadOnlyCollection<TResultSet>();

                CacheHelper.StoreInCache(spParameters, context.RedisCacheManager, results);

                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                CaeriusActivityExtensions.RecordSuccess(activity, tags, elapsedMs, results.Count);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)elapsedMs,
                        results.Count);

                return results;
            }
            catch (SqlException ex)
            {
                CaeriusActivityExtensions.RecordError(activity, tags, ex);
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
        }

        /// <summary>
        ///     Execute a stored procedure and materialize the result set as an immutable array.
        /// </summary>
        /// <typeparam name="TResultSet">Mapped result type.</typeparam>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>An immutable array containing the mapped rows.</returns>
        /// <exception cref="CaeriusNetSqlException">Thrown when the stored procedure fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<ImmutableArray<TResultSet>> QueryAsImmutableArrayAsync<TResultSet>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
            where TResultSet : class, ISpMapper<TResultSet>
        {
            const string Operation = nameof(QueryAsImmutableArrayAsync);
            var logger = LoggerProvider.GetLogger();

            // Check cache before starting the SP span so that cache hits do not emit a
            // misleading DB span or record SP duration/execution metrics.
            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out ImmutableArray<TResultSet>? cachedResult) &&
                cachedResult.HasValue)
            {
                if (spParameters.CacheKey is not null)
                {
                    CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType!.Value, true);
                    if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        logger.LogCacheHitSkippingExecution(spParameters.CacheKey);
                }

                return cachedResult.Value;
            }

            if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
                CaeriusActivityExtensions.RecordCacheLookup(spParameters, spParameters.CacheType.Value, false);

            using var activity = CaeriusActivityExtensions.StartStoredProcedureActivity(spParameters, Operation);
            var tags = CaeriusActivityExtensions.BuildMetricTags(spParameters, Operation);
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            try
            {
                await using var connection = await context.DbConnectionAsync(cancellationToken).ConfigureAwait(false);
                var results = await SqlCommandHelper.ResultSetAsImmutableArrayAsync<TResultSet>(
                    spParameters, connection, cancellationToken).ConfigureAwait(false);

                CacheHelper.StoreInCache(spParameters, context.RedisCacheManager, results);

                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                CaeriusActivityExtensions.RecordSuccess(activity, tags, elapsedMs, results.Length);

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)elapsedMs,
                        results.Length);

                return results;
            }
            catch (SqlException ex)
            {
                CaeriusActivityExtensions.RecordError(activity, tags, ex);
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
        }
    }
}
