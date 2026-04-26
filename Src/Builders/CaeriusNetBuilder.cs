namespace CaeriusNet.Builders;

/// <summary>
///     Builder for configuring CaeriusNet dependency injection with flexible connection options.
/// </summary>
public sealed class CaeriusNetBuilder
{
    private readonly IServiceCollection _services;
    private IHostApplicationBuilder? _aspireBuilder;
    private string? _aspireRedisConnectionName;
    private string? _aspireSqlServerConnectionName;
    private MemoryCacheOptions? _inMemoryCacheOptions;
    private string? _redisConnectionString;
    private string? _sqlServerConnectionString;
    private CaeriusTelemetryOptions? _telemetryOptions;

    private CaeriusNetBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public static CaeriusNetBuilder Create(IServiceCollection services)
    {
        return new CaeriusNetBuilder(services);
    }

    public static CaeriusNetBuilder Create(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var caeriusBuilder = new CaeriusNetBuilder(builder.Services)
        {
            _aspireBuilder = builder
        };
        return caeriusBuilder;
    }

    public CaeriusNetBuilder WithSqlServer(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));

        _sqlServerConnectionString = connectionString;
        return this;
    }

    public CaeriusNetBuilder WithRedis(string? connectionString)
    {
        _redisConnectionString = connectionString;
        return this;
    }

    /// <summary>
    ///     Configures OpenTelemetry emission behaviour for this CaeriusNet instance.
    ///     Must be called before <see cref="Build" /> to take effect.
    /// </summary>
    /// <param name="options">Telemetry options to apply.</param>
    public CaeriusNetBuilder WithTelemetryOptions(CaeriusTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _telemetryOptions = options;
        return this;
    }

    /// <summary>
    ///     Configures the underlying <see cref="MemoryCache" /> used by the InMemory cache tier.
    ///     Call this BEFORE the first cached read; later calls replace the underlying cache instance and
    ///     drop any items it already held. Set <see cref="MemoryCacheOptions.SizeLimit" /> to bound the
    ///     maximum number of resident entries (each entry is sized as 1).
    /// </summary>
    public CaeriusNetBuilder WithInMemoryCacheOptions(MemoryCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _inMemoryCacheOptions = options;
        return this;
    }

    public CaeriusNetBuilder WithAspireSqlServer(string connectionName = "sqlserver")
    {
        if (_aspireBuilder == null)
            throw new InvalidOperationException(
                "Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

        _aspireSqlServerConnectionName = connectionName;
        return this;
    }

    public CaeriusNetBuilder WithAspireRedis(string connectionName = "redis")
    {
        if (_aspireBuilder == null)
            throw new InvalidOperationException(
                "Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

        if (string.IsNullOrWhiteSpace(connectionName))
            throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

        _aspireRedisConnectionName = connectionName;
        return this;
    }

    public IServiceCollection Build()
    {
        if (string.IsNullOrWhiteSpace(_sqlServerConnectionString) &&
            string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName))
            throw new InvalidOperationException(
                "SQL Server connection must be configured using WithSqlServer() or WithAspireSqlServer().");

        if (_telemetryOptions is not null)
            CaeriusDiagnostics.TelemetryOptions = _telemetryOptions;

        if (_inMemoryCacheOptions is not null)
            InMemoryCacheManager.Configure(_inMemoryCacheOptions);

        ConfigureRedis();
        ConfigureSqlServer();

        _services.AddSingleton<ICaeriusNetCache>(sp =>
            new CaeriusNetCache(sp.GetService<IRedisCacheManager>()));

        return _services;
    }

    private void ConfigureSqlServer()
    {
        if (!string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName) && _aspireBuilder != null)
        {
            _aspireBuilder.AddSqlServerClient(_aspireSqlServerConnectionName);

            var cs = _aspireBuilder.Configuration.GetConnectionString(_aspireSqlServerConnectionName);

            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException(
                    $"ConnectionStrings:{_aspireSqlServerConnectionName} is missing. " +
                    "Run via AppHost, ensure the project uses WithReference(AddSqlServer(...).AddDatabase(name)) " +
                    "and the same name is passed to WithAspireSqlServer/AddSqlServerClient."
                );

            _services.AddScoped<ICaeriusNetDbContext>(sp =>
            {
                var redis = sp.GetService<IRedisCacheManager>();
                return new CaeriusNetDbContext(Factory, redis);

                SqlConnection Factory()
                {
                    return new SqlConnection(cs);
                }
            });
        }
        else if (!string.IsNullOrWhiteSpace(_sqlServerConnectionString))
        {
            _services.AddScoped<ICaeriusNetDbContext>(sp =>
            {
                var redis = sp.GetService<IRedisCacheManager>();
                var connectionString = _sqlServerConnectionString!;
                return new CaeriusNetDbContext(Factory, redis);

                SqlConnection Factory()
                {
                    return new SqlConnection(connectionString);
                }
            });
        }
    }

    private void ConfigureRedis()
    {
        if (!string.IsNullOrWhiteSpace(_aspireRedisConnectionName) && _aspireBuilder != null)
        {
            _aspireBuilder.AddRedisDistributedCache(_aspireRedisConnectionName);

            _services.AddSingleton<IRedisCacheManager>(provider =>
            {
                var distributedCache = provider.GetService<IDistributedCache>();
                var loggerFactory = provider.GetService<ILoggerFactory>();
                return new RedisCacheManager(distributedCache, loggerFactory);
            });
        }
        else if (!string.IsNullOrWhiteSpace(_redisConnectionString))
        {
            _services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _redisConnectionString;
                options.InstanceName = "CaeriusNet:";
            });

            _services.AddSingleton<IRedisCacheManager>(provider =>
            {
                var distributedCache = provider.GetService<IDistributedCache>();
                var loggerFactory = provider.GetService<ILoggerFactory>();
                return new RedisCacheManager(distributedCache, loggerFactory);
            });
        }
        else
        {
            _services.AddSingleton<IRedisCacheManager>(provider =>
                new RedisCacheManager(null, provider.GetService<ILoggerFactory>()));
        }
    }
}