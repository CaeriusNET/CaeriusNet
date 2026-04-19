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
    public void Two_Distinct_Instances_With_Same_Values_Are_Not_ReferenceEqual()
    {
        var params1 = new SqlParameter[] { new("@Id", SqlDbType.Int) };
        var params2 = new SqlParameter[] { new("@Id", SqlDbType.Int) };

        var sp1 = new StoredProcedureParameters("dbo", "sp_Test", 16, params1, null, null, null);
        var sp2 = new StoredProcedureParameters("dbo", "sp_Test", 16, params2, null, null, null);

        Assert.False(ReferenceEquals(sp1, sp2));
    }
}