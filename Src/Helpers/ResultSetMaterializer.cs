namespace CaeriusNet.Helpers;

internal static class ResultSetMaterializer
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<List<T>> ReadListAsync<T>(
        SqlDataReader reader,
        int capacity,
        CancellationToken cancellationToken)
        where T : class, ISpMapper<T>
    {
        var list = new List<T>(capacity);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(T.MapFromDataReader(reader));

        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static async ValueTask<ImmutableArray<T>> ReadImmutableArrayAsync<T>(
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
                    buffer = GrowBuffer(buffer, count);

                buffer[count++] = T.MapFromDataReader(reader);
            }

            return [..buffer.AsSpan(0, count)];
        }
        finally
        {
            ReturnBuffer(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int NormalizeCapacity(int capacity)
    {
        return Math.Max(capacity, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GrowCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (capacity <= 1)
            return 2;

        if (capacity >= Array.MaxLength)
            throw new InvalidOperationException("The result set is too large to materialize.");

        var grown = capacity + (long)(capacity >> 1);
        return grown > Array.MaxLength ? Array.MaxLength : (int)grown;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static T[] GrowBuffer<T>(T[] buffer, int count)
        where T : class
    {
        var newBuffer = ArrayPool<T>.Shared.Rent(GrowCapacity(buffer.Length));
        buffer.AsSpan(0, count).CopyTo(newBuffer);
        ReturnBuffer(buffer);
        return newBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnBuffer<T>(T[] buffer)
        where T : class
    {
        ArrayPool<T>.Shared.Return(buffer, clearArray: true);
    }
}
