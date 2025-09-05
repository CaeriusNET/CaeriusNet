using System.Runtime.CompilerServices;

namespace CaeriusNet.Utilities;

/// <summary>
///     Contains a collection of static utility methods designed to streamline the execution of SQL commands,
///     mapping of stored procedure parameters, and processing of result sets in an asynchronous database context.
/// </summary>
static internal class SqlCommandUtility
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
	///     An object containing the stored procedure name, parameters, and optional cache
	///     configuration.
	/// </param>
	/// <param name="connection">An open database connection that will be used to execute the stored procedure.</param>
	/// <returns>
	///     Returns an instance of <typeparamref name="TResultSet" /> if the query returns a result, or
	///     <see langword="null" /> if no result is found.
	/// </returns>
	static internal async Task<TResultSet?> ScalarQueryAsync<TResultSet>(StoredProcedureParameters spParameters,
		IDbConnection connection, CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
		await using var reader = await command
			.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow | CommandBehavior.SequentialAccess, cancellationToken)
			.ConfigureAwait(false);

		if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			return TResultSet.MapFromDataReader(reader);

		return null;
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
	///     An object containing the stored procedure name, parameters, and optional cache configuration.
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <returns>
	///     An asynchronous enumerable of <typeparamref name="TResultSet" /> instances, where each instance maps a
	///     corresponding row in the result set.
	/// </returns>
	static internal async IAsyncEnumerable<TResultSet> StreamQueryAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
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
	///     The type of objects in the result set. The type must implement
	///     <see cref="ISpMapper{TResultSet}" />.
	/// </typeparam>
	/// <param name="spParameters">
	///     An object containing the stored procedure name, parameters, and optional cache
	///     configuration.
	/// </param>
	/// <param name="connection">An open database connection that will be used to execute the stored procedure.</param>
	/// <returns>
	///     A <see cref="ReadOnlyCollection{T}" /> containing instances of <typeparamref name="TResultSet" /> populated
	///     from the query result.
	/// </returns>
	static internal async Task<ReadOnlyCollection<TResultSet>> ResultSetAsReadOnlyCollectionAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection, CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		var results = new List<TResultSet>(spParameters.Capacity);
		await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
		await using var reader = await command
			.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
			.ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			results.Add(TResultSet.MapFromDataReader(reader));

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
	///     An object containing the stored procedure name, parameters, and optional cache configuration.
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <returns>
	///     Returns an immutable array of <typeparamref name="TResultSet" /> instances representing the result set of the
	///     query.
	/// </returns>
	static internal async Task<ImmutableArray<TResultSet>> ResultSetAsImmutableArrayAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection, CancellationToken cancellationToken = default)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		var builder = ImmutableArray.CreateBuilder<TResultSet>(spParameters.Capacity);
		await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);
		await using var reader = await command
			.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
			.ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			builder.Add(TResultSet.MapFromDataReader(reader));

		return builder.ToImmutable();
	}

	/// <summary>
	///     Creates and configures a <see cref="SqlCommand" /> for executing a stored procedure using the specified parameters
	///     and database connection.
	/// </summary>
	/// <param name="spParameters">
	///     The stored procedure parameters, including the procedure name and the list of SQL
	///     parameters.
	/// </param>
	/// <param name="connection">
	///     The open database connection that will be used to execute the command. The connection must be
	///     of type <see cref="SqlConnection" />.
	/// </param>
	/// <returns>Returns a configured <see cref="SqlCommand" /> instance ready for execution.</returns>
	/// <exception cref="InvalidOperationException">
	///     Thrown when the provided connection is not of type
	///     <see cref="SqlConnection" />.
	/// </exception>
	private static async Task<SqlCommand> ExecuteSqlCommandAsync(StoredProcedureParameters spParameters, IDbConnection connection, CancellationToken cancellationToken = default)
	{
		if (connection is not SqlConnection sqlConnection)
			throw new InvalidOperationException("Connection must be of type SqlConnection.");

		if (sqlConnection.State != ConnectionState.Open)
			await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var command = new SqlCommand(spParameters.ProcedureName, sqlConnection)
		{
			CommandType = CommandType.StoredProcedure
		};

		// Avoid intermediate spread copies; prefer AddRange if already an array.
		if (spParameters.Parameters is not {} p)
			return command;

		foreach (var prm in p)
			command.Parameters.Add(prm);

		return command;
	}

	/// <summary>
	///     Executes an asynchronous SQL command using a stored procedure and a provided execution function.
	/// </summary>
	/// <typeparam name="T">The type of the result produced by the execution function.</typeparam>
	/// <param name="dbContext">The database context providing access to the underlying database connection.</param>
	/// <param name="spParameters">
	///     An object containing the stored procedure name, parameters, and optional cache
	///     configuration.
	/// </param>
	/// <param name="execute">
	///     A function used to process the <see cref="SqlCommand" /> and produce a result of type
	///     <typeparamref name="T" />.
	/// </param>
	/// <returns>Returns a task that produces a result of type <typeparamref name="T" />.</returns>
	/// <exception cref="CaeriusSqlException">
	///     Thrown when the execution of the stored procedure fails due to an underlying SQL
	///     exception.
	/// </exception>
	static internal async Task<T> ExecuteCommandAsync<T>(ICaeriusDbContext dbContext,
		StoredProcedureParameters spParameters, Func<SqlCommand, Task<T>> execute, CancellationToken cancellationToken = default)
	{
		try{
			using var connection = dbContext.DbConnection();
			await using var command = await ExecuteSqlCommandAsync(spParameters, connection, cancellationToken).ConfigureAwait(false);

			return await execute(command).ConfigureAwait(false);
		}
		catch (SqlException ex){ throw new CaeriusSqlException($"Failed to execute stored procedure: {spParameters.ProcedureName}", ex); }
	}

	/// <summary>
	///     Executes a stored procedure query asynchronously and processes multiple result sets using the specified mapper
	///     functions for each result set.
	/// </summary>
	/// <param name="spParameters">
	///     An object containing the stored procedure name, parameters, and optional cache configurations.
	/// </param>
	/// <param name="connection">An open database connection used to execute the stored procedure.</param>
	/// <param name="mappers">
	///     Functions that map the data from the result sets to strongly-typed objects. Each function corresponds
	///     to a specific result set.
	/// </param>
	/// <returns>
	///     A list where each element is a read-only collection of objects representing a single result set, processed
	///     using the corresponding mapper function.
	/// </returns>
	static internal async Task<List<IReadOnlyCollection<object>>> ExecuteMultipleReadOnlyResultSetsAsync(
		StoredProcedureParameters spParameters, IDbConnection connection, params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
		await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

		var results = new List<IReadOnlyCollection<object>>(mappers.Length);

		foreach (var mapper in mappers){
			var items = new List<object>(spParameters.Capacity);
			while (await reader.ReadAsync().ConfigureAwait(false))
				items.Add(mapper(reader));

			results.Add(items.AsReadOnly());

			if (!await reader.NextResultAsync().ConfigureAwait(false))
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
	/// </param>
	/// <param name="connection">
	///     An open database connection used to execute the stored procedure query.
	/// </param>
	/// <param name="mappers">
	///     An array of functions that define how each result set from the query is mapped to an object. Each function must
	///     correspond to a result set returned by the query.
	/// </param>
	/// <returns>
	///     A list of immutable arrays, where each array contains objects of the corresponding result sets mapped by the
	///     provided mapper functions.
	/// </returns>
	static internal async Task<List<ImmutableArray<object>>> ExecuteMultipleImmutableResultSetsAsync(
		StoredProcedureParameters spParameters, IDbConnection connection, params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
		await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

		var results = new List<ImmutableArray<object>>(mappers.Length);
		foreach (var mapper in mappers){
			var builder = ImmutableArray.CreateBuilder<object>(spParameters.Capacity);
			while (await reader.ReadAsync().ConfigureAwait(false))
				builder.Add(mapper(reader));

			results.Add(builder.ToImmutable());

			if (!await reader.NextResultAsync().ConfigureAwait(false))
				break;
		}

		return results;
	}

	/// <summary>
	///     Executes a stored procedure query asynchronously and returns multiple result sets,
	///     each mapped to an <see cref="IEnumerable{T}" /> of objects using the provided mapping functions.
	/// </summary>
	/// <param name="spParameters">
	///     An object containing the stored procedure name, parameters, and optional cache configuration.
	/// </param>
	/// <param name="connection">
	///     An open database connection that will be used to execute the stored procedure.
	/// </param>
	/// <param name="mappers">
	///     An array of mapping functions, each taking a <see cref="SqlDataReader" /> as input and returning a single object.
	///     The functions are used to process and map each result set retrieved by the query.
	/// </param>
	/// <returns>
	///     A list of <see cref="IEnumerable{T}" /> objects, where each enumerable represents a result set mapped according
	///     to the corresponding mapping function provided in <paramref name="mappers" />.
	/// </returns>
	static internal async Task<List<IEnumerable<object>>> ExecuteMultipleIEnumerableResultSetsAsync(
		StoredProcedureParameters spParameters, IDbConnection connection,
		params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommandAsync(spParameters, connection).ConfigureAwait(false);
		await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);

		var results = new List<IEnumerable<object>>(mappers.Length);

		foreach (var mapper in mappers){
			var resultSet = new List<object>(spParameters.Capacity);
			while (await reader.ReadAsync().ConfigureAwait(false))
				resultSet.Add(mapper(reader));

			results.Add(resultSet);

			if (!await reader.NextResultAsync().ConfigureAwait(false))
				break;
		}

		return results;
	}
}