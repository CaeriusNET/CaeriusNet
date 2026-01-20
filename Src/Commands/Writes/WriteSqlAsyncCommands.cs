namespace CaeriusNet.Commands.Writes;

/// <summary>
///     Provides a set of extension methods for asynchronous execution of SQL commands related to data writes,
///     enabling scalar queries and non-query commands through <see cref="ICaeriusNetDbContext" />.
/// </summary>
public static class WriteSqlAsyncCommands
{
	/// <param name="dbContext">The database context used to execute the command.</param>
	extension(ICaeriusNetDbContext dbContext)
	{
		/// <summary>
		///     Executes a scalar SQL command asynchronously and returns the result as the specified type.
		/// </summary>
		/// <typeparam name="T">The expected type of the scalar result.</typeparam>
		/// <param name="spParameters">The stored procedure name and parameters.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>
		///     A <see cref="ValueTask{T}" /> representing the asynchronous operation. The task result contains
		///     the scalar value converted to type <typeparamref name="T" />, or default(T) if the result is DBNull.
		/// </returns>
		/// <remarks>
		///     This method executes a SQL command that returns a single value. The value is converted to the specified
		///     type T. If the database returns DBNull, the method returns the default value for type T.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public async ValueTask<T?> ExecuteScalarAsync<T>(StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		{
			return await SqlCommandHelper.ExecuteCommandAsync(dbContext, spParameters, async command =>
			{
				var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				return result is DBNull ? default : (T?)result;
			}, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		///     Executes a non-query SQL command asynchronously and returns the number of rows affected.
		/// </summary>
		/// <param name="spParameters">The stored procedure name and parameters.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>
		///     A <see cref="ValueTask{T}" /> representing the asynchronous operation. The task result contains
		///     the number of rows affected by the command.
		/// </returns>
		/// <remarks>
		///     This method is typically used for INSERT, UPDATE, DELETE, or other SQL commands that modify data
		///     but don't return results. The return value indicates how many rows were affected.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public async ValueTask<int> ExecuteNonQueryAsync(StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		{
			return await SqlCommandHelper.ExecuteCommandAsync(dbContext, spParameters,
				command => new ValueTask<int>(command.ExecuteNonQueryAsync(cancellationToken)),
				cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		///     Executes a non-query SQL command asynchronously without returning any results (Fire and Forget).
		/// </summary>
		/// <param name="spParameters">The stored procedure name and parameters.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A <see cref="ValueTask" /> representing the asynchronous operation.</returns>
		/// <remarks>
		///     This method executes a SQL command without waiting for or processing any results.
		///     It's useful for operations where you don't need to know the outcome or affected rows count.
		///     The operation is "fire and forget" - the method returns as soon as the command is sent.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		public async ValueTask ExecuteAsync(StoredProcedureParameters spParameters,
			CancellationToken cancellationToken = default)
		{
			await SqlCommandHelper.ExecuteCommandAsync<object?>(dbContext, spParameters, async command =>
			{
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				return null;
			}, cancellationToken).ConfigureAwait(false);
		}
	}
}