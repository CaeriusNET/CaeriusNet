namespace CaeriusNet.Tests.Builders;

public sealed class StoredProcedureParametersBuilderTests
{
    [Fact]
    public void Build_ValidSchema_And_ProcedureName_Returns_Correct_Properties()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_GetUsers").Build();

        Assert.Equal("dbo", sp.SchemaName);
        Assert.Equal("sp_GetUsers", sp.ProcedureName);
        Assert.Equal(16, sp.Capacity);
        Assert.Equal(30, sp.CommandTimeout);
    }

    [Fact]
    public void Build_Custom_Capacity_And_Timeout()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test", 256, 60).Build();

        Assert.Equal(256, sp.Capacity);
        Assert.Equal(60, sp.CommandTimeout);
    }

    [Fact]
    public void Build_Underscore_Starting_Identifier_Is_Valid()
    {
        var sp = new StoredProcedureParametersBuilder("_hidden", "_sp_Internal").Build();

        Assert.Equal("_hidden", sp.SchemaName);
        Assert.Equal("_sp_Internal", sp.ProcedureName);
    }

    [Theory]
    [InlineData("123schema")]
    [InlineData("schema-name")]
    [InlineData("schema name")]
    [InlineData("")]
    [InlineData("schema!")]
    public void Build_Invalid_Schema_Throws_ArgumentException(string invalid)
    {
        var builder = new StoredProcedureParametersBuilder(invalid, "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Theory]
    [InlineData("123proc")]
    [InlineData("sp-GetUsers")]
    [InlineData("sp GetUsers")]
    [InlineData("")]
    [InlineData("sp!GetUsers")]
    public void Build_Invalid_ProcedureName_Throws_ArgumentException(string invalid)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", invalid);

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Theory]
    [InlineData("dbo")]
    [InlineData("_schema")]
    [InlineData("schema123")]
    [InlineData("My_Schema_2")]
    public void Build_Valid_Identifiers_Does_Not_Throw(string validIdentifier)
    {
        var builder = new StoredProcedureParametersBuilder(validIdentifier, validIdentifier);

        var exception = Record.Exception(() => builder.Build());

        Assert.Null(exception);
    }

    [Fact]
    public void Build_No_Parameters_Returns_Empty_Span()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test").Build();

        Assert.Equal(0, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void AddParameter_Single_Parameter_Appears_In_Span()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("Age", 30, SqlDbType.Int)
            .Build();

        Assert.Equal(1, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void AddParameter_Multiple_Parameters_All_Appear_In_Span()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("Guid", Guid.NewGuid(), SqlDbType.UniqueIdentifier)
            .AddParameter("Age", (byte)25, SqlDbType.TinyInt)
            .Build();

        Assert.Equal(2, sp.GetParametersSpan().Length);
    }

    [Theory]
    [InlineData("")]
    public void AddParameter_Empty_Name_Throws(string paramName)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.AddParameter(paramName, 1, SqlDbType.Int));
    }

    [Fact]
    public void AddTvpParameter_Empty_Collection_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.AddTvpParameter("Ids", new List<TestTvpItem>()));
    }

    [Fact]
    public void AddTvpParameter_NonEmpty_Collection_Adds_One_Parameter()
    {
        var items = new List<TestTvpItem> { new(1), new(2) };
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("Ids", items)
            .Build();

        Assert.Equal(1, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void AddInMemoryCache_Sets_CacheType_And_Key_And_Expiration()
    {
        var expiration = TimeSpan.FromMinutes(5);
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddInMemoryCache("my_key", expiration)
            .Build();

        Assert.Equal(CacheType.InMemory, sp.CacheType);
        Assert.Equal("my_key", sp.CacheKey);
        Assert.Equal(expiration, sp.CacheExpiration);
    }

    [Fact]
    public void AddFrozenCache_Sets_CacheType_And_Null_Expiration()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddFrozenCache("frozen_key")
            .Build();

        Assert.Equal(CacheType.Frozen, sp.CacheType);
        Assert.Equal("frozen_key", sp.CacheKey);
        Assert.Null(sp.CacheExpiration);
    }

    [Fact]
    public void AddRedisCache_With_Expiration_Sets_All_Properties()
    {
        var expiration = TimeSpan.FromMinutes(10);
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddRedisCache("redis_key", expiration)
            .Build();

        Assert.Equal(CacheType.Redis, sp.CacheType);
        Assert.Equal("redis_key", sp.CacheKey);
        Assert.Equal(expiration, sp.CacheExpiration);
    }

    [Fact]
    public void AddRedisCache_Without_Expiration_Sets_Null_Expiration()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddRedisCache("redis_key")
            .Build();

        Assert.Equal(CacheType.Redis, sp.CacheType);
        Assert.Null(sp.CacheExpiration);
    }

    [Fact]
    public void Build_No_Cache_Methods_Leaves_Cache_Properties_Null()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test").Build();

        Assert.Null(sp.CacheType);
        Assert.Null(sp.CacheKey);
        Assert.Null(sp.CacheExpiration);
    }

    [Fact]
    public void Builder_Is_Chainable_Returns_Same_Instance()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");
        var returned = builder.AddParameter("Id", 1, SqlDbType.Int);

        Assert.Same(builder, returned);
    }

    [Fact]
    public void AddParameter_CorrectParameterName_SetInSpan()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@UserId", 42, SqlDbType.Int)
            .Build();

        Assert.Equal("@UserId", sp.GetParametersSpan()[0].ParameterName);
    }

    [Fact]
    public void AddParameter_CorrectSqlDbType_SetInSpan()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@Code", (short)7, SqlDbType.SmallInt)
            .Build();

        Assert.Equal(SqlDbType.SmallInt, sp.GetParametersSpan()[0].SqlDbType);
    }

    [Fact]
    public void AddParameter_CorrectValue_SetInSpan()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@Score", 99, SqlDbType.Int)
            .Build();

        Assert.Equal(99, sp.GetParametersSpan()[0].Value);
    }

    [Fact]
    public void AddParameter_NullValue_SetsValueToNull()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@OptName", null!, SqlDbType.NVarChar)
            .Build();

        Assert.Null(sp.GetParametersSpan()[0].Value);
    }

    [Fact]
    public void Builder_Capacity_Zero_Is_Accepted()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test", 0, 30);

        var exception = Record.Exception(() => builder.Build());

        Assert.Null(exception);
    }

    [Fact]
    public void Builder_Capacity_One_Is_Accepted()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test", 1, 30).Build();

        Assert.Equal(1, sp.Capacity);
    }

    [Fact]
    public void Builder_Timeout_Zero_Is_Accepted()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test", 16, 0).Build();

        Assert.Equal(0, sp.CommandTimeout);
    }

    [Fact]
    public void AddTvpParameter_Sets_SqlDbType_Structured()
    {
        var items = new List<TestTvpItem> { new(1) };
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids", items)
            .Build();

        Assert.Equal(SqlDbType.Structured, sp.GetParametersSpan()[0].SqlDbType);
    }

    [Fact]
    public void AddTvpParameter_Sets_TypeName_Correctly()
    {
        var items = new List<TestTvpItem> { new(1) };
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids", items)
            .Build();

        Assert.Equal(TestTvpItem.TvpTypeName, sp.GetParametersSpan()[0].TypeName);
    }

    [Fact]
    public void AddTvpParameter_Multiple_TVPs_Adds_Multiple_Parameters()
    {
        var items = new List<TestTvpItem> { new(1), new(2) };
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids1", items)
            .AddTvpParameter("@Ids2", items)
            .Build();

        Assert.Equal(2, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void GetParametersSpan_LargeParameterCount_AllPresent()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        for (var i = 0; i < 50; i++)
            builder.AddParameter($"@Param{i}", i, SqlDbType.Int);

        var sp = builder.Build();

        Assert.Equal(50, sp.GetParametersSpan().Length);
    }
}