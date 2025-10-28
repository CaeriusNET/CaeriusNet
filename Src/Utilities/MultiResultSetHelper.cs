namespace CaeriusNet.Utilities;

/// <summary>
///     Internal helpers for reading multiple result sets from SQL Server data readers
/// </summary>
static internal class MultiResultSetHelper
{
	/// <summary>
	///     Reads a single result set into a List using CollectionsMarshal for zero-copy access.
	/// </summary>
	/// <typeparam name="T">The type to map the result set rows to</typeparam>
	/// <param name="reader">The SqlDataReader to read from</param>
	/// <param name="capacity">Initial capacity for the List</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>A List containing the mapped rows</returns>
	/// <remarks>
	///     Uses CollectionsMarshal for optimized list access without additional allocations
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static internal async ValueTask<List<T>> ReadResultSetAsync<T>(
		SqlDataReader reader,
		int capacity,
		CancellationToken cancellationToken)
		where T : class, ISpMapper<T>
	{
		var list = new List<T>(capacity);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){
			var item = T.MapFromDataReader(reader);
			CollectionsMarshal.SetCount(list, list.Count + 1);
			CollectionsMarshal.AsSpan(list)[^1] = item;
		}

		return list;
	}

	/// <summary>
	///     Reads a single result set directly into an ImmutableArray.
	/// </summary>
	/// <typeparam name="T">The type to map the result set rows to</typeparam>
	/// <param name="reader">The SqlDataReader to read from</param>
	/// <param name="capacity">Initial capacity for the array builder</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>An ImmutableArray containing the mapped rows</returns>
	/// <remarks>
	///     Uses direct creation of ImmutableArray to avoid an extra allocation compared to Builder + ToImmutable
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	static internal async ValueTask<ImmutableArray<T>> ReadResultSetAsImmutableArrayAsync<T>(
		SqlDataReader reader,
		int capacity,
		CancellationToken cancellationToken)
		where T : class, ISpMapper<T>
	{
		if (capacity <= 256){
			var buffer = ArrayPool<T>.Shared.Rent(capacity);
			int count = 0;

			try{
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && count < capacity)
					buffer[count++] = T.MapFromDataReader(reader);

				return ImmutableCollectionsMarshal.AsImmutableArray(buffer.AsSpan(0, count).ToArray());
			}
			finally{ ArrayPool<T>.Shared.Return(buffer); }
		}

		var builder = ImmutableArray.CreateBuilder<T>(capacity);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			builder.Add(T.MapFromDataReader(reader));

		return builder.Count == capacity
			? builder.MoveToImmutable()
			: builder.ToImmutable();
	}

	/// <summary>
	///     Tries to move to the next result set in the data reader.
	/// </summary>
	/// <param name="reader">The SqlDataReader to advance</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>True if successfully moved to next result set, false if no more results</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static internal ValueTask<bool> TryMoveNextAsync(
		SqlDataReader reader,
		CancellationToken cancellationToken)
	{
		return new ValueTask<bool>(reader.NextResultAsync(cancellationToken));
	}
}