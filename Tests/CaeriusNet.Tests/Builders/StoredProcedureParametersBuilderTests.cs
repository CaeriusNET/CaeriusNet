using System.Collections;

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
    [InlineData(" ")]
    [InlineData("@")]
    [InlineData("@@Id")]
    [InlineData(" Id")]
    [InlineData("Id ")]
    public void AddParameter_Invalid_Name_Throws(string paramName)
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddInMemoryCache_Invalid_Key_Throws(string cacheKey)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.AddInMemoryCache(cacheKey, TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddInMemoryCache_NonPositive_Expiration_Throws(int seconds)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddInMemoryCache("my_key", TimeSpan.FromSeconds(seconds)));
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddFrozenCache_Invalid_Key_Throws(string cacheKey)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.AddFrozenCache(cacheKey));
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddRedisCache_Invalid_Key_Throws(string cacheKey)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.AddRedisCache(cacheKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddRedisCache_NonPositive_Expiration_Throws(int seconds)
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddRedisCache("redis_key", TimeSpan.FromSeconds(seconds)));
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
    public void AddParameter_Name_Without_Prefix_Is_Normalized()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("UserId", 42, SqlDbType.Int)
            .Build();

        Assert.Equal("@UserId", sp.GetParametersSpan()[0].ParameterName);
    }

    [Fact]
    public void AddParameter_With_Facets_And_Direction_Preserves_Metadata()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter(
                "@Amount",
                12.34m,
                SqlDbType.Decimal,
                precision: 19,
                scale: 4,
                direction: ParameterDirection.InputOutput)
            .Build();

        var parameter = sp.GetParametersSpan()[0];

        Assert.Equal(19, parameter.Precision);
        Assert.Equal(4, parameter.Scale);
        Assert.Equal(ParameterDirection.InputOutput, parameter.Direction);
    }

    [Fact]
    public void AddParameter_With_Size_Preserves_Metadata()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@Name", "ari", SqlDbType.NVarChar, 64)
            .Build();

        Assert.Equal(64, sp.GetParametersSpan()[0].Size);
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
    public void AddParameter_NullValue_SetsValueToDBNull()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@OptName", null!, SqlDbType.NVarChar)
            .Build();

        Assert.Same(DBNull.Value, sp.GetParametersSpan()[0].Value);
    }

    [Fact]
    public void Builder_Capacity_Zero_Is_Accepted()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test", 0);

        var exception = Record.Exception(() => builder.Build());

        Assert.Null(exception);
    }

    [Fact]
    public void Builder_Capacity_One_Is_Accepted()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test", 1).Build();

        Assert.Equal(1, sp.Capacity);
    }

    [Fact]
    public void Builder_Timeout_Zero_Is_Accepted()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test", 16, 0).Build();

        Assert.Equal(0, sp.CommandTimeout);
    }

    [Fact]
    public void Builder_Negative_Capacity_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test", -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Fact]
    public void Builder_Negative_Timeout_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "sp_Test", 16, -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
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
    public void AddTvpParameter_Name_Without_Prefix_Is_Normalized()
    {
        var items = new List<TestTvpItem> { new(1) };
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("Ids", items)
            .Build();

        Assert.Equal("@Ids", sp.GetParametersSpan()[0].ParameterName);
    }

    [Fact]
    public void AddTvpParameter_ReadOnlyList_Does_Not_Materialize_During_Build()
    {
        var items = new CountingReadOnlyList<TestTvpItem>([new TestTvpItem(1), new TestTvpItem(2)]);

        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids", items)
            .Build();

        Assert.Equal(0, items.EnumerationCount);

        var records = Assert.IsAssignableFrom<IEnumerable<SqlDataRecord>>(sp.GetParametersSpan()[0].Value);
        var values = records.Select(record => record.GetInt32(0)).ToArray();

        Assert.Equal(1, items.EnumerationCount);
        Assert.Equal([1, 2], values);
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

    [Fact]
    public void Constructor_InvalidSchemaName_WithSpecialChars_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo$", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void Constructor_InvalidProcedureName_WithSpaces_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "my proc");

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void Constructor_ValidIdentifiers_WithUnderscores_Succeeds()
    {
        var builder = new StoredProcedureParametersBuilder("my_schema", "my_proc");

        var ex = Record.Exception(() => builder.Build());

        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_EmptySchemaName_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("", "sp_Test");

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public void Constructor_EmptyProcedureName_Throws()
    {
        var builder = new StoredProcedureParametersBuilder("dbo", "");

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    private sealed class CountingReadOnlyList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
    {
        public int EnumerationCount { get; private set; }

        public int Count => items.Count;

        public T this[int index] => items[index];

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
