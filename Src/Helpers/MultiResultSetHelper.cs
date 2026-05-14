namespace CaeriusNet.Helpers;

/// <summary>
///     Internal helpers for reading multiple result sets from SQL Server data readers
/// </summary>
internal static class MultiResultSetHelper
{
    /// <summary>
    ///     Reads a single result set into a List.
    /// </summary>
    /// <typeparam name="T">The type to map the result set rows to</typeparam>
    /// <param name="reader">The SqlDataReader to read from</param>
    /// <param name="capacity">Initial capacity for the List</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A List containing the mapped rows</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static ValueTask<List<T>> ReadResultSetAsync<T>(
        SqlDataReader reader,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return ResultSetMaterializer.ReadListAsync<T>(reader, capacity, cancellationToken);
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
    internal static ValueTask<ImmutableArray<T>> ReadResultSetAsImmutableArrayAsync<T>(
        SqlDataReader reader,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        return ResultSetMaterializer.ReadImmutableArrayAsync<T>(reader, capacity, cancellationToken);
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

}
