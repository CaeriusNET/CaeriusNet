using Microsoft.Extensions.Configuration;

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

	/// <summary>
	///     Creates a new instance of the CaeriusNet builder.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <returns>A new CaeriusNetBuilder instance.</returns>
	public static CaeriusNetBuilder Create(IServiceCollection services)
	{
		return new CaeriusNetBuilder(services);
	}

	/// <summary>
	///     Creates a new instance of the CaeriusNet builder for Aspire scenarios.
	/// </summary>
	/// <param name="builder">The Aspire host application builder.</param>
	/// <returns>A new CaeriusNetBuilder instance.</returns>
	public static CaeriusNetBuilder Create(IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var caeriusBuilder = new CaeriusNetBuilder(builder.Services)
		{
			_aspireBuilder = builder
		};
		return caeriusBuilder;
	}

	/// <summary>
	///     Configures the SQL Server connection string for on-premise deployment.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder instance for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when connection string is null or whitespace.</exception>
	public CaeriusNetBuilder WithSqlServer(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));

		_sqlServerConnectionString = connectionString;
		return this;
	}

	/// <summary>
	///     Configures the Redis connection string for on-premise deployment.
	/// </summary>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The builder instance for chaining.</returns>
	public CaeriusNetBuilder WithRedis(string? connectionString)
	{
		_redisConnectionString = connectionString;
		return this;
	}

	/// <summary>
	///     Configures SQL Server connection using Aspire integration.
	/// </summary>
	/// <param name="connectionName">The name of the SQL Server connection in Aspire configuration.</param>
	/// <returns>The builder instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown when builder was not created with Aspire context.</exception>
	public CaeriusNetBuilder WithAspireSqlServer(string connectionName)
	{
		if (_aspireBuilder == null)
			throw new InvalidOperationException("Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

		if (string.IsNullOrWhiteSpace(connectionName))
			throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

		_aspireSqlServerConnectionName = connectionName;
		return this;
	}

	/// <summary>
	///     Configures Redis connection using Aspire integration.
	/// </summary>
	/// <param name="connectionName">The name of the Redis connection in Aspire configuration (default: "cache").</param>
	/// <returns>The builder instance for chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown when builder was not created with Aspire context.</exception>
	public CaeriusNetBuilder WithAspireRedis(string connectionName = "cache")
	{
		if (_aspireBuilder == null)
			throw new InvalidOperationException("Aspire builder is required. Use Create(IHostApplicationBuilder) instead.");

		if (string.IsNullOrWhiteSpace(connectionName))
			throw new ArgumentException("Connection name cannot be null or whitespace.", nameof(connectionName));

		_aspireRedisConnectionName = connectionName;
		return this;
	}

	/// <summary>
	///     Builds and configures all registered services.
	/// </summary>
	/// <returns>The configured IServiceCollection.</returns>
	/// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
	public IServiceCollection Build()
	{
		// Validate that at least SQL Server is configured
		if (string.IsNullOrWhiteSpace(_sqlServerConnectionString) && string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName))
			throw new InvalidOperationException("SQL Server connection must be configured using WithSqlServer() or WithAspireSqlServer().");

		// Configure SQL Server
		ConfigureSqlServer();

		// Configure Redis (optional)
		ConfigureRedis();

		return _services;
	}

	private void ConfigureSqlServer()
	{
		if (!string.IsNullOrWhiteSpace(_aspireSqlServerConnectionName) && _aspireBuilder != null){
			// Aspire SQL Server configuration
			_aspireBuilder.AddSqlServerClient(_aspireSqlServerConnectionName);

			// Register CaeriusNetDbContext using connection string from configuration
			_services.AddSingleton<ICaeriusNetDbContext>(_ => {
				string? connectionString = _aspireBuilder.Configuration.GetConnectionString(_aspireSqlServerConnectionName);
				return new CaeriusNetDbContext(connectionString!);
			});
		}
		else if (!string.IsNullOrWhiteSpace(_sqlServerConnectionString)){
			// On-premise SQL Server configuration
			_services.AddSingleton<ICaeriusNetDbContext>(_ => new CaeriusNetDbContext(_sqlServerConnectionString));
		}
	}

	private void ConfigureRedis()
	{
		if (!string.IsNullOrWhiteSpace(_aspireRedisConnectionName) && _aspireBuilder != null){
			// Aspire Redis configuration
			_aspireBuilder.AddRedisClient(_aspireRedisConnectionName);

			_services.AddSingleton<IRedisCacheConnection>(provider =>
				new RedisCacheConnection(provider.GetRequiredService<IConnectionMultiplexer>()));

			_services.AddSingleton<IRedisCacheManager>(provider =>
				new RedisCacheManager(
				provider.GetRequiredService<IRedisCacheConnection>(),
				provider.GetService<ILogger<RedisCacheManager>>()));
		}
		else if (!string.IsNullOrWhiteSpace(_redisConnectionString)){
			// On-premise Redis configuration
			_services.AddSingleton<IRedisCacheManager>(sp =>
				new RedisCacheManager(
				_redisConnectionString,
				sp.GetService<ILogger<RedisCacheManager>>()));
		}
	}
}