namespace CaeriusNet.Commands.Writes;

/// <summary>
///     Provides a set of extension methods for asynchronous execution of SQL commands related to data writes,
///     enabling scalar queries and non-query commands through <see cref="ICaeriusDbContext" />.
/// </summary>
public static class WriteSqlAsyncCommands
{
	/// <summary>
	///     Executes a scalar SQL command asynchronously using the provided <see cref="ICaeriusDbContext" />
	///     and stored procedure parameters, returning the result as an instance of the specified type.
	/// </summary>
	/// <typeparam name="T">
	///     The type of the result expected from the scalar SQL command.
	///     If the result from the database is <see cref="DBNull" />, the method returns <c>default</c>.
	/// </typeparam>
	/// <param name="dbContext">
	///     The database context used to establish a connection for executing the SQL command.
	///     Must implement <see cref="ICaeriusDbContext" />.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name,
	///     associated parameters, and optional caching details.
	/// </param>
	/// <returns>
	///     A task representing the asynchronous operation. The task result contains the scalar value retrieved
	///     from the database, converted to the specified type <typeparamref name="T" />.
	///     Returns <c>default</c> if the result is <see cref="DBNull" />.
	/// </returns>
	public static async Task<T?> ExecuteScalarAsync<T>(this ICaeriusDbContext dbContext,
		StoredProcedureParameters spParameters)
	{
		return await SqlCommandUtility.ExecuteCommandAsync(dbContext, spParameters, async command =>
		{
			var result = await command.ExecuteScalarAsync();
			return result is DBNull
				? default
				: (T?)result;
		});
	}

	/// <summary>
	///     Executes a non-query SQL command asynchronously using the provided <see cref="ICaeriusDbContext" />
	///     and stored procedure parameters, returning the number of rows affected.
	/// </summary>
	/// <param name="dbContext">
	///     The database context used to establish a connection for executing the SQL command.
	///     Must implement <see cref="ICaeriusDbContext" />.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name,
	///     associated parameters, and optional caching details.
	/// </param>
	/// <returns>
	///     A task representing the asynchronous operation. The task result contains the number
	///     of rows affected by the executed SQL command.
	/// </returns>
	public static async Task<int> ExecuteNonQueryAsync(this ICaeriusDbContext dbContext,
		StoredProcedureParameters spParameters)
	{
		return await SqlCommandUtility.ExecuteCommandAsync(dbContext, spParameters, async command =>
			await command.ExecuteNonQueryAsync());
	}

	/// <summary>
	///     Executes a non-query SQL command asynchronously using the provided <see cref="ICaeriusDbContext" />
	///     and stored procedure parameters, as Fire and Forget, this method does not return any result.
	/// </summary>
	/// <param name="dbContext">
	///     The database context used to establish a connection for executing the SQL command.
	///     Must implement <see cref="ICaeriusDbContext" />.
	/// </param>
	/// <param name="spParameters">
	///     The parameters required for the stored procedure, including the procedure name,
	///     associated parameters, and optional caching details.
	/// </param>
	/// <returns>
	///     A task representing the asynchronous operation. As this method is Fire and Forget, it does not return any result.
	/// </returns>
	public static async Task ExecuteAsync(this ICaeriusDbContext dbContext, StoredProcedureParameters spParameters)
	{
		await SqlCommandUtility.ExecuteCommandAsync<object?>(dbContext, spParameters, async command =>
		{
			await command.ExecuteNonQueryAsync();
			return null;
		});
	}
}