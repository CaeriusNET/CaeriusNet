namespace CaeriusNet.Abstractions;

public interface ICaeriusNetDbContext
{
    string Name { get; }
}

public interface ICaeriusNetTransaction : IAsyncDisposable
{
    bool IsActive { get; }
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}

public interface ICaeriusSqlConnectionLease : IAsyncDisposable
{
    SqlConnection Connection { get; }
}

public interface ICaeriusCacheCodec
{
    string CodecId { get; }
    string ContractHash { get; }
    Type ValueType { get; }
    void EncodeObject(IBufferWriter<byte> writer, object value);
    object DecodeObject(ReadOnlySpan<byte> payload);
}

public interface ICaeriusCacheCodec<T> : ICaeriusCacheCodec
{
    Type ICaeriusCacheCodec.ValueType => typeof(T);

    void ICaeriusCacheCodec.EncodeObject(IBufferWriter<byte> writer, object value)
    {
        Encode(writer, (T)value);
    }

    object ICaeriusCacheCodec.DecodeObject(ReadOnlySpan<byte> payload)
    {
        return Decode(payload)!;
    }

    void Encode(IBufferWriter<byte> writer, T value);
    T Decode(ReadOnlySpan<byte> payload);
}

public readonly record struct CaeriusCacheLookup<T>(bool Found, T? Value)
{
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The generic factory preserves the cache value type while avoiding an allocation for a miss.")]
    public static CaeriusCacheLookup<T> Miss => default;

    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The generic factory preserves the cache value type while avoiding an allocation for a hit.")]
    public static CaeriusCacheLookup<T> Hit(T value)
    {
        return new CaeriusCacheLookup<T>(true, value);
    }
}

public interface IInMemoryCacheManager
{
    bool TryGet<T>(string key, out T? value);
    void SetEntry<T>(string key, T value, TimeSpan expiration, bool slidingExpiration = false) where T : notnull;

    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan expiration,
        bool slidingExpiration,
        Func<CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default) where T : notnull;

    ValueTask<T> GetOrCreateAsync<T, TState>(
        string key,
        TimeSpan expiration,
        bool slidingExpiration,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        CancellationToken cancellationToken = default) where T : notnull;

    void Remove(string key);
    void Clear();
}

public interface IFrozenCacheManager
{
    bool TryGet<T>(string key, out T? value);
    void ReplaceSnapshot<T>(IEnumerable<KeyValuePair<string, T>> entries) where T : notnull;
    void Remove(string key);
    void Clear();
}

public interface ICaeriusRemoteCacheManager
{
    ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync<T>(
        string key,
        T value,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default) where T : notnull;

    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
