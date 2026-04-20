using System.Diagnostics;

namespace CaeriusNet.Commands.Writes;

/// <summary>
///     Execute asynchronous stored procedure writes for <see cref="ICaeriusNetDbContext" />.
/// </summary>
public static class WriteSqlAsyncCommands
{
    /// <param name="dbContext">Database context used to open the connection.</param>
    extension(ICaeriusNetDbContext dbContext)
    {
        /// <summary>
        ///     Execute a stored procedure and return its scalar result.
        /// </summary>
        /// <typeparam name="T">Scalar result type.</typeparam>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The converted scalar value, or the default value when the result is <see cref="DBNull" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<T?> ExecuteScalarAsync<T>(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
        {
            var logger = LoggerProvider.GetLogger();
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            var result = await SqlCommandHelper.ExecuteCommandAsync(dbContext, spParameters, async command =>
            {
                var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return scalar is DBNull ? default : (T?)scalar;
            }, cancellationToken).ConfigureAwait(false);

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogProcedureScalarCompleted(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            return result;
        }

        /// <summary>
        ///     Execute a stored procedure and return the number of affected rows.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask<int> ExecuteNonQueryAsync(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
        {
            var logger = LoggerProvider.GetLogger();
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            var rowsAffected = await SqlCommandHelper.ExecuteCommandAsync(dbContext, spParameters,
                command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
                cancellationToken).ConfigureAwait(false);

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogProcedureNonQueryCompleted(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    rowsAffected);

            return rowsAffected;
        }

        /// <summary>
        ///     Execute a stored procedure without returning its result.
        /// </summary>
        /// <param name="spParameters">Stored procedure metadata and parameters.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task that completes when the command finishes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public async ValueTask ExecuteAsync(
            StoredProcedureParameters spParameters,
            CancellationToken cancellationToken = default)
        {
            var logger = LoggerProvider.GetLogger();
            var startTimestamp = Stopwatch.GetTimestamp();

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogExecutingProcedure(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    spParameters.GetParametersSpan().Length);

            var rowsAffected = await SqlCommandHelper.ExecuteCommandAsync(dbContext, spParameters,
                command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
                cancellationToken).ConfigureAwait(false);

            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogProcedureNonQueryCompleted(
                    spParameters.SchemaName,
                    spParameters.ProcedureName,
                    (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
                    rowsAffected);
        }
    }
}