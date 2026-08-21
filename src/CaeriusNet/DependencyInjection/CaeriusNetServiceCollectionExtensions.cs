using CaeriusNet;

namespace Microsoft.Extensions.DependencyInjection;

public static class CaeriusNetServiceCollectionExtensions
{
    public static IServiceCollection AddCaeriusNet(
        this IServiceCollection services,
        Action<CaeriusNetOptions> configure)
    {
        return AddCaeriusNetCore(services, Options.Options.DefaultName, configure, false);
    }

    public static IServiceCollection AddCaeriusNet(
        this IServiceCollection services,
        string name,
        Action<CaeriusNetOptions> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return AddCaeriusNetCore(services, name, configure, true);
    }

    public static IServiceCollection AddCaeriusCacheCodec<
        T,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TCodec>(
        this IServiceCollection services)
        where TCodec : class, ICaeriusCacheCodec<T>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<TCodec>();
        services.TryAddSingleton<ICaeriusCacheCodec<T>>(static provider => provider.GetRequiredService<TCodec>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICaeriusCacheCodec, CaeriusCacheCodecRegistration<T, TCodec>>());
        AddMaterializedCollectionCodecs<T>(services);
        return services;
    }

    public static IServiceCollection AddCaeriusGeneratedCacheCodecs<
        T,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TCodec>(
        this IServiceCollection services)
        where TCodec : class, ICaeriusCacheCodec<T>
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddCaeriusCacheCodec<T, TCodec>();
    }

    private static void AddMaterializedCollectionCodecs<T>(IServiceCollection services)
    {
        AddCollectionCodec<T[], CaeriusArrayCacheCodec<T>>(services);
        AddCollectionCodec<ReadOnlyCollection<T>, CaeriusReadOnlyCollectionCacheCodec<T>>(services);
        AddCollectionCodec<ImmutableArray<T>, CaeriusImmutableArrayCacheCodec<T>>(services);
    }

    private static void AddCollectionCodec<
        TCollection,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TCodec>(
        IServiceCollection services)
        where TCodec : class, ICaeriusCacheCodec<TCollection>
    {
        services.TryAddSingleton<TCodec>();
        services.TryAddSingleton<ICaeriusCacheCodec<TCollection>>(static provider =>
            provider.GetRequiredService<TCodec>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICaeriusCacheCodec, CaeriusCacheCodecRegistration<TCollection, TCodec>>());
    }

    private static IServiceCollection AddCaeriusNetCore(
        IServiceCollection services,
        string name,
        Action<CaeriusNetOptions> configure,
        bool keyed)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CaeriusNetOptions>(name).Configure(configure);
        services.AddOptions<CaeriusMemoryCacheOptions>();
        services.TryAddSingleton<IInMemoryCacheManager, InMemoryCacheManager>();
        services.TryAddSingleton<IFrozenCacheManager, FrozenCacheManager>();
        services.TryAddSingleton<RedisReadThroughCoordinator>();
        services.TryAddSingleton<CacheTagVersionRegistry>();
        services.TryAddSingleton<CaeriusInstrumentation>();

        if (keyed)
            services.TryAddKeyedScoped<ICaeriusNetDbContext>(name, (provider, _) =>
                CaeriusNetDbContext.Create(provider, name));
        else
            services.TryAddScoped<ICaeriusNetDbContext>(provider =>
                CaeriusNetDbContext.Create(provider, Options.Options.DefaultName));

        return services;
    }
}

internal sealed class CaeriusCacheCodecRegistration<T, TCodec>(TCodec codec) : ICaeriusCacheCodec<T>
    where TCodec : class, ICaeriusCacheCodec<T>
{
    public string CodecId => codec.CodecId;
    public string ContractHash => codec.ContractHash;

    public void Encode(IBufferWriter<byte> writer, T value)
    {
        codec.Encode(writer, value);
    }

    public T Decode(ReadOnlySpan<byte> payload)
    {
        return codec.Decode(payload);
    }
}
