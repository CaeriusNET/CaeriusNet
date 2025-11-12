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
	private string? _redisConnectionString;
	private string? _sqlServerConnectionString;

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

	public CaeriusNetBuilder WithAspireSqlServer(string connectionName = "sqlserver")
	{
		if (_aspireBuilder == null)
			throw new InvalidOperationException("Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

		if (string.IsNullOrWhiteSpace(connectionName))
			throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

		_aspireSqlServerConnectionName = connectionName;
		return this;
	}

	public CaeriusNetBuilder WithAspireRedis(string connectionName = "redis")
	{
		if (_aspireBuilder == null)
			throw new InvalidOperationException("Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

		if (string.IsNullOrWhiteSpace(connectionName))
			throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

		_aspireRedisConnectionName = connectionName;
		return this;
	}

	public IServiceCollection Build()
	{
		if (string.IsNullOrWhiteSpace(_sqlServerConnectionString) && string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName))
			throw new InvalidOperationException("SQL Server connection must be configured using WithSqlServer() or WithAspireSqlServer().");

		ConfigureRedis();
		ConfigureSqlServer();

		return _services;
	}

	private void ConfigureSqlServer()
	{
		if (!string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName) && _aspireBuilder != null){
			_aspireBuilder.AddSqlServerClient(_aspireSqlServerConnectionName);

			string? cs = _aspireBuilder.Configuration.GetConnectionString(_aspireSqlServerConnectionName);

			if (string.IsNullOrWhiteSpace(cs))
				throw new InvalidOperationException(
				$"ConnectionStrings:{_aspireSqlServerConnectionName} is missing. " +
				"Run via AppHost, ensure the project uses WithReference(AddSqlServer(...).AddDatabase(name)) " +
				"and the same name is passed to WithAspireSqlServer/AddSqlServerClient."
				);

			_services.AddScoped<ICaeriusNetDbContext>(sp => {
				var redis = sp.GetService<IRedisCacheManager>();
				return new CaeriusNetDbContext(Factory, redis);

				SqlConnection Factory()
				{
					return new SqlConnection(cs);
				}
			});
		}
		else if (!string.IsNullOrWhiteSpace(_sqlServerConnectionString)){
			_services.AddScoped<ICaeriusNetDbContext>(sp => {
				var redis = sp.GetService<IRedisCacheManager>();
				string connectionString = _sqlServerConnectionString!;
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
		if (!string.IsNullOrWhiteSpace(_aspireRedisConnectionName) && _aspireBuilder != null){
			_aspireBuilder.AddRedisDistributedCache(_aspireRedisConnectionName);

			_services.AddSingleton<IRedisCacheManager>(provider => {
				var distributedCache = provider.GetService<IDistributedCache>();
				var loggerFactory = provider.GetService<ILoggerFactory>();
				return new RedisCacheManager(distributedCache, loggerFactory);
			});
		}
		else if (!string.IsNullOrWhiteSpace(_redisConnectionString)){
			_services.AddStackExchangeRedisCache(options => {
				options.Configuration = _redisConnectionString;
				options.InstanceName = "CaeriusNet:";
			});

			_services.AddSingleton<IRedisCacheManager>(provider => {
				var distributedCache = provider.GetService<IDistributedCache>();
				var loggerFactory = provider.GetService<ILoggerFactory>();
				return new RedisCacheManager(distributedCache, loggerFactory);
			});
		}
		else{
			_services.AddSingleton<IRedisCacheManager>(provider =>
				new RedisCacheManager(null, provider.GetService<ILoggerFactory>()));
		}
	}
}