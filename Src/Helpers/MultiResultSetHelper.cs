namespace CaeriusNet.Helpers;

/// <summary>
///     Internal helpers for reading multiple result sets from SQL Server data readers
/// </summary>
internal static class MultiResultSetHelper
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
    internal static async ValueTask<List<T>> ReadResultSetAsync<T>(
        SqlDataReader reader,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var list = new List<T>(capacity);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var item = T.MapFromDataReader(reader);

            list.Add(item);
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
    internal static async ValueTask<ImmutableArray<T>> ReadResultSetAsImmutableArrayAsync<T>(
        SqlDataReader reader,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var buffer = ArrayPool<T>.Shared.Rent(NormalizeCapacity(capacity));
        var count = 0;

        try
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (count >= buffer.Length)
                {
                    var newCapacity = GrowCapacity(buffer.Length);
                    var newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T>.Shared.Return(buffer);
                    buffer = newBuffer;
                }

                buffer[count++] = T.MapFromDataReader(reader);
            }

            return [..buffer.AsSpan(0, count)];
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer);
        }
    }

    /// <summary>
    ///     Tries to move to the next result set in the data reader.
    /// </summary>
    /// <param name="reader">The SqlDataReader to advance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully moved to next result set, false if no more results</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask<bool> TryMoveNextAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var task = reader.NextResultAsync(cancellationToken);
        return task.IsCompletedSuccessfully
            ? new ValueTask<bool>(task.Result)
            : new ValueTask<bool>(task);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeCapacity(int capacity)
    {
        return Math.Max(capacity, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GrowCapacity(int capacity)
    {
        return capacity <= 1 ? 2 : capacity * 3 / 2;
    }
}
