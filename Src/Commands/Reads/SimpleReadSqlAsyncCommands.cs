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
            var logger = LoggerProvider.GetLogger();

            if (spParameters.CacheType.HasValue && !string.IsNullOrEmpty(spParameters.CacheKey))
                if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                        out TResultSet? cachedResult))
                {
                    if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                        logger.LogCacheHitSkippingExecution(spParameters.CacheKey);
                    return cachedResult;
                }

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

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        result is null ? 0 : 1);

                return result;
            }
            catch (SqlException ex)
            {
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
            var logger = LoggerProvider.GetLogger();

            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out ReadOnlyCollection<TResultSet>? cachedResult) &&
                cachedResult != null)
            {
                if (spParameters.CacheKey is null) return cachedResult;
                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogCacheHitSkippingExecution(spParameters.CacheKey);

                return cachedResult;
            }

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

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        results.Count);

                return results;
            }
            catch (SqlException ex)
            {
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
            var logger = LoggerProvider.GetLogger();

            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out IEnumerable<TResultSet>? cachedResult) &&
                cachedResult != null)
            {
                if (spParameters.CacheKey is null) return cachedResult;
                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogCacheHitSkippingExecution(spParameters.CacheKey);

                return cachedResult;
            }

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

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        results.Count);

                return results;
            }
            catch (SqlException ex)
            {
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
            var logger = LoggerProvider.GetLogger();

            if (CacheHelper.TryRetrieveFromCache(spParameters, context.RedisCacheManager,
                    out ImmutableArray<TResultSet>? cachedResult) &&
                cachedResult.HasValue)
            {
                if (spParameters.CacheKey is null) return cachedResult.Value;
                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogCacheHitSkippingExecution(spParameters.CacheKey);

                return cachedResult.Value;
            }

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

                if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                    logger.LogProcedureCompleted(
                        spParameters.SchemaName,
                        spParameters.ProcedureName,
                        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                        results.Length);

                return results;
            }
            catch (SqlException ex)
            {
                throw new CaeriusNetSqlException(
                    $"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
            }
        }
    }
}