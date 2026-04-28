namespace CaeriusNet.Tests.Mappers;

public sealed class StoredProcedureParametersTests
{
    [Fact]
    public void Constructor_Sets_All_Properties_Correctly()
    {
        var parameters = new SqlParameter[] { new("@Age", SqlDbType.Int) };
        var expiration = TimeSpan.FromMinutes(5);

        var sp = new StoredProcedureParameters(
            "dbo",
            "sp_Test",
            100,
            parameters,
            "cache_key",
            expiration,
            CacheType.InMemory,
            45);

        Assert.Equal("dbo", sp.SchemaName);
        Assert.Equal("sp_Test", sp.ProcedureName);
        Assert.Equal(100, sp.Capacity);
        Assert.Equal("cache_key", sp.CacheKey);
        Assert.Equal(expiration, sp.CacheExpiration);
        Assert.Equal(CacheType.InMemory, sp.CacheType);
        Assert.Equal(45, sp.CommandTimeout);
    }

    [Fact]
    public void GetParametersSpan_Returns_All_Parameters()
    {
        var parameters = new SqlParameter[]
        {
            new("@Age", SqlDbType.Int),
            new("@Guid", SqlDbType.UniqueIdentifier)
        };

        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, parameters, null, null, null);

        Assert.Equal(2, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void GetParametersSpan_Empty_Array_Returns_Empty_Span()
    {
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, [], null, null, null);

        Assert.Equal(0, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void Default_CommandTimeout_Is_30()
    {
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, [], null, null, null);

        Assert.Equal(30, sp.CommandTimeout);
    }

    [Fact]
    public void Constructor_Negative_Capacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StoredProcedureParameters("dbo", "sp_Test", -1, [], null, null, null));
    }

    [Fact]
    public void Constructor_Negative_CommandTimeout_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StoredProcedureParameters("dbo", "sp_Test", 16, [], null, null, null, -1));
    }

    [Fact]
    public void Constructor_No_Cache_Leaves_Cache_Properties_Null()
    {
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, [], null, null, null);

        Assert.Null(sp.CacheKey);
        Assert.Null(sp.CacheExpiration);
        Assert.Null(sp.CacheType);
    }

    [Fact]
    public void GetParametersSpan_Returns_Correct_Parameter_Name()
    {
        var parameters = new SqlParameter[] { new("@UserId", SqlDbType.Int) };
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, parameters, null, null, null);

        var span = sp.GetParametersSpan();

        Assert.Equal("@UserId", span[0].ParameterName);
    }

    [Fact]
    public void GetParametersSpan_Returns_Correct_SqlDbType()
    {
        var parameters = new SqlParameter[] { new("@Flag", SqlDbType.Bit) };
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, parameters, null, null, null);

        var span = sp.GetParametersSpan();

        Assert.Equal(SqlDbType.Bit, span[0].SqlDbType);
    }

    [Fact]
    public void GetParametersSpan_LargeArray_ReturnsCorrectLength()
    {
        var parameters = Enumerable.Range(0, 100)
            .Select(i => new SqlParameter($"@P{i}", SqlDbType.Int))
            .ToArray();
        var sp = new StoredProcedureParameters("dbo", "sp_Test", 16, parameters, null, null, null);

        Assert.Equal(100, sp.GetParametersSpan().Length);
    }

    [Fact]
    public void Two_Distinct_Instances_HaveIndependent_ParameterSpans()
    {
        var params1 = new SqlParameter[] { new("@Id", SqlDbType.Int) };
        var params2 = new SqlParameter[] { new("@Name", SqlDbType.NVarChar) };

        var sp1 = new StoredProcedureParameters("dbo", "sp_Test", 16, params1, null, null, null);
        var sp2 = new StoredProcedureParameters("dbo", "sp_Test", 16, params2, null, null, null);

        Assert.Equal("@Id", sp1.GetParametersSpan()[0].ParameterName);
        Assert.Equal("@Name", sp2.GetParametersSpan()[0].ParameterName);
    }

    [Fact]
    public void AddParametersTo_Clones_Parameters_For_Each_Command()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@Id", 42, SqlDbType.Int)
            .Build();

        using var command1 = new SqlCommand();
        using var command2 = new SqlCommand();

        sp.AddParametersTo(command1.Parameters);
        sp.AddParametersTo(command2.Parameters);

        Assert.NotSame(sp.GetParametersSpan()[0], command1.Parameters[0]);
        Assert.NotSame(command1.Parameters[0], command2.Parameters[0]);
        Assert.Equal(42, command1.Parameters[0].Value);
        Assert.Equal(42, command2.Parameters[0].Value);
    }

    [Fact]
    public void AddParametersTo_Preserves_DBNull_Null_Normalization()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@Value", null, SqlDbType.NVarChar)
            .Build();

        using var command = new SqlCommand();

        sp.AddParametersTo(command.Parameters);

        Assert.Same(DBNull.Value, command.Parameters[0].Value);
    }

    [Fact]
    public void AddParametersTo_Tvp_Clone_Creates_Reenumerable_Value()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids", new[] { new TestTvpItem(1), new TestTvpItem(2) })
            .Build();

        using var command1 = new SqlCommand();
        using var command2 = new SqlCommand();

        sp.AddParametersTo(command1.Parameters);
        sp.AddParametersTo(command2.Parameters);

        Assert.NotSame(command1.Parameters[0], command2.Parameters[0]);

        var tvp1 = Assert.IsAssignableFrom<IEnumerable<SqlDataRecord>>(command1.Parameters[0].Value);
        var tvp2 = Assert.IsAssignableFrom<IEnumerable<SqlDataRecord>>(command2.Parameters[0].Value);

        using var enumerator1 = tvp1.GetEnumerator();
        using var enumerator2 = tvp2.GetEnumerator();

        Assert.True(enumerator1.MoveNext());
        Assert.True(enumerator2.MoveNext());
        Assert.NotSame(enumerator1.Current, enumerator2.Current);
        Assert.Equal(1, enumerator1.Current.GetInt32(0));
        Assert.Equal(1, enumerator2.Current.GetInt32(0));
    }
}
