namespace CaeriusNet.Helpers;

/// <summary>
///     Contains a collection of static utility methods designed to streamline the execution of SQL commands,
///     mapping of stored procedure parameters, and processing of result sets in an asynchronous database context.
/// </summary>
/// <remarks>
///     This utility class provides methods for:
///     <list type="bullet">
///         <item>
///             <description>Executing scalar queries and mapping results to strongly-typed objects</description>
///         </item>
///         <item>
///             <description>Streaming result sets asynchronously for memory-efficient processing</description>
///         </item>
///         <item>
///             <description>Creating immutable and read-only collections from result sets</description>
///         </item>
///         <item>
///             <description>Processing multiple result sets with custom mapping functions</description>
///         </item>
///         <item>
///             <description>Handling SQL command execution with proper resource management</description>
///         </item>
///     </list>
/// </remarks>
/// <seealso cref="MultiResultSetHelper" />
/// <seealso cref="CacheHelper" />
/// <seealso cref="EmptyCollections" />
internal static class SqlCommandHelper
{
	/// <summary>
	///     Executes a stored procedure query asynchronously and returns a single scalar result mapped to the specified result
	///     set type.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of object that the scalar result should be mapped to. The type must implement
	///     <see cref="ISpMapper{TResultSet}" />.
	/// </typeparam>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>CacheKey - Optional. Key for caching the results</description>
	///         </item>
	///         <item>
	///             <description>CacheExpiration - Optional. How long to cache the results</description>
	///         </item>
	///         <item>
	///             <description>CacheType - Optional. What type of caching to use</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">An open database connection that will be used to execute the stored procedure.</param>
	/// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
	/// <returns>
	///     Returns an instance of <typeparamref name="TResultSet" /> if the query returns a result, or
	///     <see langword="null" /> if no result is found.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown when the provided connection is not a SqlConnection.</exception>
	/// <exception cref="CaeriusNetSqlException">Thrown when the execution of the stored procedure fails.</exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<TResultSet?> ScalarQueryAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        CancellationToken cancellationToken = default)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        await using var command =
            await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
        await using var reader = await command
            .ExecuteReaderAsync(
                CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess,
                cancellationToken)
            .ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? TResultSet.MapFromDataReader(reader)
            : null;
    }

	/// <summary>
	///     Executes a stored procedure query asynchronously and streams the result set as an asynchronous enumerable of the
	///     specified type.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of objects that represent each item in the result set. The type must implement
	///     <see cref="ISpMapper{TResultSet}" />.
	/// </typeparam>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>CacheKey - Optional. Key for caching the results</description>
	///         </item>
	///         <item>
	///             <description>CacheExpiration - Optional. How long to cache the results</description>
	///         </item>
	///         <item>
	///             <description>CacheType - Optional. What type of caching to use</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
	/// <returns>
	///     An asynchronous enumerable of <typeparamref name="TResultSet" /> instances, where each instance maps a
	///     corresponding row in the result set.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown when the provided connection is not a SqlConnection.</exception>
	/// <exception cref="CaeriusNetSqlException">Thrown when the execution of the stored procedure fails.</exception>
	internal static async IAsyncEnumerable<TResultSet> StreamQueryAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        await using var command =
            await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return TResultSet.MapFromDataReader(reader);
    }

	/// <summary>
	///     Executes a stored procedure query asynchronously and returns the result set as a read-only collection of the
	///     specified type.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of objects in the result set. The type must implement <see cref="ISpMapper{TResultSet}" />.
	/// </typeparam>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>CacheKey - Optional. Key for caching the results</description>
	///         </item>
	///         <item>
	///             <description>CacheExpiration - Optional. How long to cache the results</description>
	///         </item>
	///         <item>
	///             <description>CacheType - Optional. What type of caching to use</description>
	///         </item>
	///         <item>
	///             <description>Capacity - The initial capacity to allocate for the result set</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">An open database connection that will be used to execute the stored procedure.</param>
	/// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
	/// <returns>
	///     A <see cref="ReadOnlyCollection{T}" /> containing instances of <typeparamref name="TResultSet" /> populated
	///     from the query result.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown when the provided connection is not a SqlConnection.</exception>
	/// <exception cref="CaeriusNetSqlException">Thrown when the execution of the stored procedure fails.</exception>
	/// <remarks>
	///     This method efficiently processes the result set using array pooling to minimize allocations.
	///     The result is returned as a read-only collection to prevent modifications to the underlying data.
	///     If the initial capacity estimate is too small, the buffer will be resized automatically.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<ReadOnlyCollection<TResultSet>> ResultSetAsReadOnlyCollectionAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        CancellationToken cancellationToken = default)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var results = new List<TResultSet>(spParameters.Capacity);

        await using var command =
            await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
        await using var reader = await command
            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var item = TResultSet.MapFromDataReader(reader);

            CollectionsMarshal.SetCount(results, results.Count + 1);
            CollectionsMarshal.AsSpan(results)[^1] = item;
        }

        return results.AsReadOnly();
    }

	/// <summary>
	///     Executes a stored procedure asynchronously and returns the result set as an immutable array.
	/// </summary>
	/// <typeparam name="TResultSet">
	///     The type of object that the result set should be mapped to. The type must implement
	///     <see cref="ISpMapper{TResultSet}" />.
	/// </typeparam>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>CacheKey - Optional. Key for caching the results</description>
	///         </item>
	///         <item>
	///             <description>CacheExpiration - Optional. How long to cache the results</description>
	///         </item>
	///         <item>
	///             <description>CacheType - Optional. What type of caching to use</description>
	///         </item>
	///         <item>
	///             <description>Capacity - Initial capacity for the result set builder</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <param name="cancellationToken">Optional. A token to cancel the asynchronous operation.</param>
	/// <returns>
	///     Returns an immutable array of <typeparamref name="TResultSet" /> instances representing the result set of the
	///     query.
	/// </returns>
	/// <exception cref="InvalidOperationException">Thrown when the provided connection is not a SqlConnection.</exception>
	/// <exception cref="CaeriusNetSqlException">Thrown when the execution of the stored procedure fails.</exception>
	/// <remarks>
	///     This method uses an ImmutableArray.Builder for efficient construction of the result set.
	///     If the final count matches the initial capacity, it uses MoveToImmutable for better performance.
	///     Otherwise it calls ToImmutable which may require an extra array allocation.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<ImmutableArray<TResultSet>> ResultSetAsImmutableArrayAsync<TResultSet>(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        CancellationToken cancellationToken = default)
        where TResultSet : class, ISpMapper<TResultSet>
    {
        var buffer = ArrayPool<TResultSet>.Shared.Rent(spParameters.Capacity);
        var count = 0;

        try
        {
            await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken)
                .ConfigureAwait(false);
            await using var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                .ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (count >= buffer.Length)
                {
                    var newBuffer = ArrayPool<TResultSet>.Shared.Rent(buffer.Length * 3 / 2);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<TResultSet>.Shared.Return(buffer);
                    buffer = newBuffer;
                }

                buffer[count++] = TResultSet.MapFromDataReader(reader);
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(buffer.AsSpan(0, count).ToArray());
        }
        finally
        {
            ArrayPool<TResultSet>.Shared.Return(buffer, true);
        }
    }

	/// <summary>
	///     Creates and configures a <see cref="SqlCommand" /> for executing a stored procedure using the specified parameters
	///     and database connection.
	/// </summary>
	/// <param name="spParameters">
	///     The stored procedure parameters, including the procedure name and the list of SQL parameters. The ProcedureName
	///     property
	///     specifies the name of the stored procedure to execute. The Parameters property contains the SQL parameters to pass
	///     to
	///     the stored procedure.
	/// </param>
	/// <param name="connection">
	///     The open database connection that will be used to execute the command. The connection must be
	///     of type <see cref="SqlConnection" />. If the connection is not open, it will be opened automatically.
	/// </param>
	/// <param name="cancellationToken">
	///     A cancellation token that can be used to cancel the asynchronous operation of opening the connection if needed.
	/// </param>
	/// <returns>
	///     Returns a configured <see cref="SqlCommand" /> instance ready for execution. The command will be configured with:
	///     - CommandText set to the stored procedure name
	///     - CommandType set to StoredProcedure
	///     - CommandTimeout set to 30 seconds
	///     - Any parameters from spParameters added to the command's Parameters collection
	/// </returns>
	/// <exception cref="InvalidOperationException">
	///     Thrown when the provided connection is not of type <see cref="SqlConnection" />. The connection must be a
	///     SqlConnection
	///     instance to execute SQL Server stored procedures.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static async ValueTask<SqlCommand> ExecuteSqlCommandAsync(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (connection is not SqlConnection sqlConnection)
            throw new InvalidOperationException("Connection must be of type SqlConnection.");

        if (sqlConnection.State != ConnectionState.Open)
            await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = new SqlCommand($"{spParameters.SchemaName}.{spParameters.ProcedureName}", sqlConnection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        // ULTRA: Use Span for zero-copy parameter addition
        var paramsSpan = spParameters.GetParametersSpan();
        ref var searchSpace = ref MemoryMarshal.GetReference(paramsSpan);

        for (var i = 0; i < paramsSpan.Length; i++)
            command.Parameters.Add(Unsafe.Add(ref searchSpace, i));

        return command;
    }

	/// <summary>
	///     Executes an asynchronous SQL command using a stored procedure and a provided execution function.
	/// </summary>
	/// <typeparam name="T">The type of the result produced by the execution function.</typeparam>
	/// <param name="dbContext">The database context providing access to the underlying database connection.</param>
	/// <param name="spParameters">
	///     An object containing the stored procedure name, parameters, and optional cache configuration.
	///     The following properties are included:
	///     <list type="bullet">
	///         <item>
	///             <description><c>ProcedureName</c>: The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description><c>Parameters</c>: The SQL parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description><c>CacheKey</c>: Optional. Key for caching the results</description>
	///         </item>
	///         <item>
	///             <description><c>CacheExpiration</c>: Optional. How long to cache the results</description>
	///         </item>
	///         <item>
	///             <description><c>CacheType</c>: Optional. What type of caching to use</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="execute">
	///     A function used to process the <see cref="SqlCommand" /> and produce a result of type
	///     <typeparamref name="T" />. The function takes a <see cref="SqlCommand" /> as input and returns
	///     a <see cref="ValueTask{T}" />.
	/// </param>
	/// <param name="cancellationToken">
	///     Optional. A token that can be used to request cancellation of the asynchronous
	///     operation.
	/// </param>
	/// <returns>
	///     Returns a <see cref="ValueTask{T}" /> representing the asynchronous operation that produces a result of type
	///     <typeparamref name="T" />.
	/// </returns>
	/// <exception cref="CaeriusNetSqlException">
	///     Thrown when the execution of the stored procedure fails due to an underlying SQL
	///     exception. The exception includes the procedure name in the message and wraps the original SqlException.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<T> ExecuteCommandAsync<T>(
        ICaeriusNetDbContext dbContext,
        StoredProcedureParameters spParameters,
        Func<SqlCommand, ValueTask<T>> execute,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = dbContext.DbConnection();
            await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken)
                .ConfigureAwait(false);

            return await execute(command).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            throw new CaeriusNetSqlException($"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
        }
    }

	/// <summary>
	///     Executes a stored procedure query asynchronously and processes multiple result sets using the specified mapper
	///     functions for each result set.
	/// </summary>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>Capacity - Initial capacity for the result collections</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <param name="mappers">
	///     Functions that map the data from the result sets to strongly-typed objects. Each function corresponds
	///     to a specific result set in the order they are returned from the stored procedure.
	/// </param>
	/// <returns>
	///     A list where each element is a read-only collection of objects representing a single result set, processed
	///     using the corresponding mapper function. The collections are immutable to prevent modifications.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when no mapper functions are provided.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the connection is not a SqlConnection.</exception>
	/// <remarks>
	///     This method efficiently processes multiple result sets from a single stored procedure execution.
	///     Each result set is mapped to objects using the corresponding mapper function and returned as a
	///     read-only collection. The method handles sequential access to optimize performance.
	/// </remarks>
	internal static async Task<List<IReadOnlyCollection<object>>> ExecuteMultipleReadOnlyResultSetsAsync(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        params Func<SqlDataReader, object>[] mappers)
    {
        if (mappers.Length == 0)
            throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

        await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
        await using var reader =
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

        var results = new List<IReadOnlyCollection<object>>(mappers.Length);

        var mappersCount = mappers.Length;

        for (var i = 0; i < mappersCount; i++)
        {
            var items = new List<object>(spParameters.Capacity);
            var currentMapper = mappers[i];

            while (await reader.ReadAsync().ConfigureAwait(false))
                items.Add(currentMapper(reader));

            results.Add(items.AsReadOnly());

            if (i < mappersCount - 1 && !await reader.NextResultAsync().ConfigureAwait(false))
                break;
        }

        return results;
    }

	/// <summary>
	///     Executes a stored procedure query asynchronously and maps its multiple result sets to immutable arrays using
	///     the provided mapper functions.
	/// </summary>
	/// <param name="spParameters">
	///     An object representing the stored procedure name, its parameters, and optional cache configuration.
	///     Contains:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>Capacity - Initial capacity for the result set builder</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">
	///     An open database connection used to execute the stored procedure query. Must be a <see cref="SqlConnection" />.
	/// </param>
	/// <param name="mappers">
	///     An array of functions that define how each result set from the query is mapped to an object. Each function must
	///     correspond to a result set returned by the query in the order they are returned.
	/// </param>
	/// <returns>
	///     A list of immutable arrays, where each array contains objects of the corresponding result sets mapped by the
	///     provided mapper functions. The arrays are immutable to prevent modifications.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when no mapper functions are provided.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the connection is not a <see cref="SqlConnection" />.</exception>
	/// <remarks>
	///     This method efficiently processes multiple result sets from a single stored procedure execution.
	///     Each result set is mapped using the corresponding mapper function and stored in an immutable array.
	///     The number of result sets processed matches the number of mapper functions provided.
	///     If there are fewer result sets than mappers, processing stops after the last available result set.
	/// </remarks>
	internal static async Task<List<ImmutableArray<object>>> ExecuteMultipleImmutableResultSetsAsync(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        params Func<SqlDataReader, object>[] mappers)
    {
        if (mappers.Length == 0)
            throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

        await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
        await using var reader =
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

        var results = new List<ImmutableArray<object>>(mappers.Length);
        var mappersCount = mappers.Length;

        for (var i = 0; i < mappersCount; i++)
        {
            var buffer = ArrayPool<object>.Shared.Rent(spParameters.Capacity);
            var count = 0;
            var currentMapper = mappers[i];

            try
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (count >= buffer.Length)
                    {
                        var newBuffer = ArrayPool<object>.Shared.Rent(buffer.Length * 2);
                        buffer.AsSpan(0, count).CopyTo(newBuffer);
                        ArrayPool<object>.Shared.Return(buffer);
                        buffer = newBuffer;
                    }

                    buffer[count++] = currentMapper(reader);
                }

                results.Add(ImmutableCollectionsMarshal.AsImmutableArray(buffer.AsSpan(0, count).ToArray()));
            }
            finally
            {
                ArrayPool<object>.Shared.Return(buffer, true);
            }

            if (i < mappersCount - 1 && !await reader.NextResultAsync().ConfigureAwait(false))
                break;
        }

        return results;
    }

	/// <summary>
	///     Executes a stored procedure query asynchronously and returns multiple result sets,
	///     each mapped to an <see cref="IEnumerable{T}" /> of objects using the provided mapping functions.
	/// </summary>
	/// <param name="spParameters">
	///     An object containing:
	///     <list type="bullet">
	///         <item>
	///             <description>ProcedureName - The name of the stored procedure to execute</description>
	///         </item>
	///         <item>
	///             <description>Parameters - The parameters to pass to the stored procedure</description>
	///         </item>
	///         <item>
	///             <description>Capacity - Initial capacity for each result set collection</description>
	///         </item>
	///     </list>
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	///     Must be an instance of <see cref="SqlConnection" />.
	/// </param>
	/// <param name="mappers">
	///     An array of mapping functions that process each result set. Each function takes a
	///     <see cref="SqlDataReader" /> as input and maps a single row to an object.
	///     The number of mappers determines how many result sets are processed.
	/// </param>
	/// <returns>
	///     A list where each element is an <see cref="IEnumerable{T}" /> containing the mapped objects
	///     for a single result set. The order of the result sets matches the order of the mapper functions.
	/// </returns>
	/// <exception cref="ArgumentException">
	///     Thrown when no mapper functions are provided in the <paramref name="mappers" /> array.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///     Thrown when the provided <paramref name="connection" /> is not a <see cref="SqlConnection" />.
	/// </exception>
	/// <remarks>
	///     This method processes multiple result sets from a single stored procedure execution.
	///     Each result set is processed sequentially using the corresponding mapper function.
	///     If there are fewer result sets available than mapper functions, processing stops
	///     after the last available result set.
	///     The method uses <see cref="CommandBehavior.SequentialAccess" /> for optimal performance.
	/// </remarks>
	internal static async Task<List<IEnumerable<object>>> ExecuteMultipleIEnumerableResultSetsAsync(
        StoredProcedureParameters spParameters,
        IDbConnection connection,
        params Func<SqlDataReader, object>[] mappers)
    {
        if (mappers.Length == 0)
            throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

        await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
        await using var reader =
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

        var results = new List<IEnumerable<object>>(mappers.Length);
        var mappersCount = mappers.Length;

        for (var i = 0; i < mappersCount; i++)
        {
            var resultSet = new List<object>(spParameters.Capacity);
            var currentMapper = mappers[i];

            while (await reader.ReadAsync().ConfigureAwait(false))
                resultSet.Add(currentMapper(reader));

            results.Add(resultSet);

            if (i < mappersCount - 1 && !await reader.NextResultAsync().ConfigureAwait(false))
                break;
        }

        return results;
    }
}