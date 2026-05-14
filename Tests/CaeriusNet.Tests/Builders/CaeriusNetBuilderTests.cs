namespace CaeriusNet.Tests.Builders;

public sealed class CaeriusNetBuilderTests
{
    [Fact]
    public void Create_WithNull_ServiceCollection_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CaeriusNetBuilder.Create((IServiceCollection)null!));
    }

    [Fact]
    public void Create_WithNull_HostApplicationBuilder_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CaeriusNetBuilder.Create((IHostApplicationBuilder)null!));
    }

    [Fact]
    public void Create_WithValidServices_Returns_NonNull_Builder()
    {
        var services = new ServiceCollection();

        var builder = CaeriusNetBuilder.Create(services);

        Assert.NotNull(builder);
    }

    [Fact]
    public void WithSqlServer_EmptyString_Throws_ArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            CaeriusNetBuilder.Create(services).WithSqlServer(""));
    }

    [Fact]
    public void WithSqlServer_NullString_Throws_ArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            CaeriusNetBuilder.Create(services).WithSqlServer(null!));
    }

    [Fact]
    public void WithSqlServer_WhitespaceString_Throws_ArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            CaeriusNetBuilder.Create(services).WithSqlServer("   "));
    }

    [Fact]
    public void Build_WithoutSqlServer_Throws_InvalidOperationException()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            CaeriusNetBuilder.Create(services).Build());
    }

    [Fact]
    public void WithSqlServer_ValidString_Returns_SameBuilder_For_Chaining()
    {
        var services = new ServiceCollection();
        var builder = CaeriusNetBuilder.Create(services);

        var result = builder.WithSqlServer("Server=.;Database=test;");

        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_AfterWithSqlServer_Returns_ServiceCollection()
    {
        var services = new ServiceCollection();

        var result = CaeriusNetBuilder
            .Create(services)
            .WithSqlServer("Server=.;Database=test;Integrated Security=true;")
            .Build();

        Assert.NotNull(result);
    }

    [Fact]
    public void Build_AfterWithSqlServer_Registers_ICaeriusNetDbContext()
    {
        var services = new ServiceCollection();

        CaeriusNetBuilder
            .Create(services)
            .WithSqlServer("Server=.;Database=test;Integrated Security=true;")
            .Build();

        var descriptor = Assert.Single(
            services,
            sd => sd.ServiceType == typeof(ICaeriusNetDbContext));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void WithAspireSqlServer_WithoutAspireBuilder_Throws_InvalidOperationException()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            CaeriusNetBuilder.Create(services).WithAspireSqlServer());
    }

    [Fact]
    public void WithAspireRedis_WithoutAspireBuilder_Throws_InvalidOperationException()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            CaeriusNetBuilder.Create(services).WithAspireRedis());
    }

    [Fact]
    public void WithRedis_ValidConnectionString_Returns_SameBuilder_For_Chaining()
    {
        var services = new ServiceCollection();
        var builder = CaeriusNetBuilder.Create(services);

        var result = builder.WithRedis("localhost:6379");

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithSqlServer_And_WithRedis_Build_RegistersServices_WithoutThrowing()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
            CaeriusNetBuilder
                .Create(services)
                .WithSqlServer("Server=.;Database=test;Integrated Security=true;")
                .WithRedis("localhost:6379")
                .Build());

        Assert.Null(exception);
    }
}
