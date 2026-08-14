using CaeriusNet;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaeriusNet.Internal;

internal interface ICaeriusExecutionContext
{
    string Name { get; }
    CaeriusNetOptions Options { get; }
    ILogger Logger { get; }
    IInMemoryCacheManager MemoryCache { get; }
    IFrozenCacheManager FrozenCache { get; }
    RedisReadThroughCoordinator RedisReadThrough { get; }
    IReadOnlyList<ICaeriusRemoteCacheManager> RemoteCaches { get; }
    IReadOnlyList<ICaeriusCacheCodec> Codecs { get; }
    CaeriusInstrumentation Instrumentation { get; }
    ValueTask<ICaeriusSqlConnectionLease> OpenLeaseAsync(CancellationToken cancellationToken);
}

internal sealed class CaeriusNetDbContext : ICaeriusNetDbContext, ICaeriusExecutionContext
{
    private readonly IServiceProvider _services;

    private CaeriusNetDbContext(
        IServiceProvider services,
        string name,
        CaeriusNetOptions options,
        ILogger<CaeriusNetDbContext> logger,
        IInMemoryCacheManager memoryCache,
        IFrozenCacheManager frozenCache,
        RedisReadThroughCoordinator redisReadThrough,
        IReadOnlyList<ICaeriusRemoteCacheManager> remoteCaches,
        IReadOnlyList<ICaeriusCacheCodec> codecs,
        CaeriusInstrumentation instrumentation)
    {
        _services = services;
        Name = name;
        Options = options;
        Logger = logger;
        MemoryCache = memoryCache;
        FrozenCache = frozenCache;
        RedisReadThrough = redisReadThrough;
        RemoteCaches = remoteCaches;
        Codecs = codecs;
        Instrumentation = instrumentation;
    }

    public CaeriusNetOptions Options { get; }
    public ILogger Logger { get; }
    public IInMemoryCacheManager MemoryCache { get; }
    public IFrozenCacheManager FrozenCache { get; }
    public RedisReadThroughCoordinator RedisReadThrough { get; }
    public IReadOnlyList<ICaeriusRemoteCacheManager> RemoteCaches { get; }
    public IReadOnlyList<ICaeriusCacheCodec> Codecs { get; }
    public CaeriusInstrumentation Instrumentation { get; }

    public async ValueTask<ICaeriusSqlConnectionLease> OpenLeaseAsync(CancellationToken cancellationToken)
    {
        var lease = await Options.LeaseFactory!(_services, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The SQL connection lease factory returned null.");
        try
        {
            if (lease.Connection.State == ConnectionState.Closed)
                await lease.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (lease.Connection.State != ConnectionState.Open)
                throw new InvalidOperationException("The SQL connection lease did not provide an open connection.");
            return lease;
        }
        catch
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public string Name { get; }

    public static CaeriusNetDbContext Create(IServiceProvider services, string name)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CaeriusNetOptions>>().Get(name);
        options.Validate(name);
        return new CaeriusNetDbContext(
            services,
            name,
            options,
            services.GetService<ILogger<CaeriusNetDbContext>>() ??
            NullLogger<CaeriusNetDbContext>.Instance,
            services.GetRequiredService<IInMemoryCacheManager>(),
            services.GetRequiredService<IFrozenCacheManager>(),
            services.GetRequiredService<RedisReadThroughCoordinator>(),
            services.GetServices<ICaeriusRemoteCacheManager>().ToArray(),
            services.GetServices<ICaeriusCacheCodec>().ToArray(),
            services.GetRequiredService<CaeriusInstrumentation>());
    }
}
