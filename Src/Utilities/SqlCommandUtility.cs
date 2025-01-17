namespace CaeriusNet.Utilities;

/// <summary>
///     Contains a collection of static utility methods designed to streamline the execution of SQL commands,
///     mapping of stored procedure parameters, and processing of result sets in an asynchronous database context.
/// </summary>
internal static class SqlCommandUtility
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
	internal static async Task<TResultSet?> ScalarQueryAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		await using var command = await ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		if (await reader.ReadAsync())
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
	internal static async IAsyncEnumerable<TResultSet> StreamQueryAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		await using var command = await ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
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
	internal static async Task<ReadOnlyCollection<TResultSet>> ResultSetAsReadOnlyCollectionAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		var results = new List<TResultSet>(spParameters.Capacity);
		await foreach (var item in StreamQueryAsync<TResultSet>(spParameters, connection)) results.Add(item);
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
	internal static async Task<ImmutableArray<TResultSet>> ResultSetAsImmutableArrayAsync<TResultSet>(
		StoredProcedureParameters spParameters, IDbConnection connection)
		where TResultSet : class, ISpMapper<TResultSet>
	{
		var builder = ImmutableArray.CreateBuilder<TResultSet>(spParameters.Capacity);
		await foreach (var item in StreamQueryAsync<TResultSet>(spParameters, connection)) builder.Add(item);
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
	internal static Task<SqlCommand> ExecuteSqlCommand(
		StoredProcedureParameters spParameters,
		IDbConnection connection)
	{
		if (connection is not SqlConnection sqlConnection)
			throw new InvalidOperationException("Connection must be of type SqlConnection.");

		var command = new SqlCommand(spParameters.ProcedureName, sqlConnection)
		{
			CommandType = CommandType.StoredProcedure
		};

		command.Parameters.AddRange([..spParameters.Parameters]);

		return Task.FromResult(command);
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
	internal static async Task<T> ExecuteCommandAsync<T>(
		ICaeriusDbContext dbContext, StoredProcedureParameters spParameters, Func<SqlCommand, Task<T>> execute)
	{
		try
		{
			using var connection = dbContext.DbConnection();
			await using var command = await ExecuteSqlCommand(spParameters, connection);

			return await execute(command);
		}
		catch (SqlException ex)
		{
			throw new CaeriusSqlException($"Failed to execute stored procedure: {spParameters.ProcedureName}", ex);
		}
	}

	internal static async Task<List<IReadOnlyCollection<object>>> ExecuteMultipleResultSetsAsync(
		StoredProcedureParameters spParameters, IDbConnection connection, params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<IReadOnlyCollection<object>>(mappers.Length);

		foreach (var mapper in mappers)
		{
			var items = new List<object>();
			while (await reader.ReadAsync())
				items.Add(mapper(reader));

			results.Add(items.AsReadOnly());

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}

	internal static async Task<List<ImmutableArray<object>>> ExecuteMultipleImmutableResultSetsAsync(
		StoredProcedureParameters spParameters, IDbConnection connection, params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<ImmutableArray<object>>(mappers.Length);
		foreach (var mapper in mappers)
		{
			var builder = ImmutableArray.CreateBuilder<object>();
			while (await reader.ReadAsync())
				builder.Add(mapper(reader));

			results.Add(builder.ToImmutable());

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}

	internal static async Task<List<IEnumerable<object>>> ExecuteMultipleIEnumerableResultSetsAsync(
		StoredProcedureParameters spParameters,
		IDbConnection connection,
		params Func<SqlDataReader, object>[] mappers)
	{
		if (mappers.Length == 0)
			throw new ArgumentException("At least one mapper function must be provided.", nameof(mappers));

		await using var command = await ExecuteSqlCommand(spParameters, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var results = new List<IEnumerable<object>>(mappers.Length);

		foreach (var mapper in mappers)
		{
			var resultSet = new List<object>();
			while (await reader.ReadAsync())
				resultSet.Add(mapper(reader));

			results.Add(resultSet);

			if (!await reader.NextResultAsync())
				break;
		}

		return results;
	}
}