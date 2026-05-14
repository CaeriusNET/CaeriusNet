namespace CaeriusNet.Tests.Builders;

public sealed class StoredProcedureParametersBuilderTypedTests
{
    [Fact]
    public void Create_Uses_Generated_Procedure_Metadata()
    {
        var sp = StoredProcedureParametersBuilder<TestNoParameterProcedure>
            .Create(32, 45)
            .Build();

        Assert.Equal("dbo", sp.SchemaName);
        Assert.Equal("usp_NoParameters", sp.ProcedureName);
        Assert.Equal("dbo.usp_NoParameters", sp.FullName);
        Assert.Equal("sha256:test", sp.ContractHash);
        Assert.Equal(0, sp.ParameterCount);
        Assert.Equal(1, sp.ResultSetCount);
        Assert.Equal(32, sp.Capacity);
        Assert.Equal(45, sp.CommandTimeout);
    }

    [Fact]
    public void Build_With_Unbound_Generated_Parameters_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("WithParameters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_With_Bound_Generated_Scalar_Parameters_Returns_Typed_Parameters()
    {
        var sp = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16)
            .AddGeneratedParameter(1, "Id", 42, SqlDbType.Int)
            .MarkGeneratedParametersBound()
            .AddRedisCache("test:key", TimeSpan.FromMinutes(5))
            .Build();

        var parameters = sp.GetParametersSpan();

        Assert.Equal("dbo", sp.SchemaName);
        Assert.Equal("usp_ById", sp.ProcedureName);
        Assert.Equal(CacheType.Redis, sp.CacheType);
        Assert.Equal("test:key", sp.CacheKey);
        Assert.Single(parameters.ToArray());
        Assert.Equal("@Id", parameters[0].ParameterName);
        Assert.Equal(SqlDbType.Int, parameters[0].SqlDbType);
        Assert.Equal(42, parameters[0].Value);
    }

    [Fact]
    public void AddGeneratedParameter_Invalid_Name_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16);

        Assert.Throws<ArgumentException>(() =>
            builder.AddGeneratedParameter(1, "@@Id", 42, SqlDbType.Int));
    }

    [Fact]
    public void Create_With_Generated_Parameters_Binds_In_One_Call()
    {
        var sp = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(new TestScalarParameters(42), 16)
            .Build();

        var parameters = sp.GetParametersSpan();

        Assert.Equal(16, sp.Capacity);
        Assert.Single(parameters.ToArray());
        Assert.Equal("@Id", parameters[0].ParameterName);
        Assert.Equal(42, parameters[0].Value);
    }

    [Fact]
    public void AddGeneratedParameter_Preserves_Sql_Facets()
    {
        var sp = StoredProcedureParametersBuilder<TestFacetProcedure>
            .Create(16)
            .AddGeneratedParameter(1, "@Name", "ari", SqlDbType.NVarChar, 64)
            .AddGeneratedParameter(2, "@Amount", 12.50m, SqlDbType.Decimal, precision: 18, scale: 2)
            .MarkGeneratedParametersBound()
            .Build();

        var parameters = sp.GetParametersSpan();

        Assert.Equal(64, parameters[0].Size);
        Assert.Equal(18, parameters[1].Precision);
        Assert.Equal(2, parameters[1].Scale);
    }

    [Fact]
    public void AddGeneratedParameter_With_Configured_SqlParameter_Preserves_Facets()
    {
        var parameter = new SqlParameter("Amount", SqlDbType.Decimal)
        {
            Value = 12.50m,
            Precision = 18,
            Scale = 4
        };

        var sp = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16)
            .AddGeneratedParameter(1, parameter)
            .MarkGeneratedParametersBound()
            .Build();

        var actual = sp.GetParametersSpan()[0];

        Assert.Equal("@Amount", actual.ParameterName);
        Assert.Equal(SqlDbType.Decimal, actual.SqlDbType);
        Assert.Equal(12.50m, actual.Value);
        Assert.Equal(18, actual.Precision);
        Assert.Equal(4, actual.Scale);
    }

    [Fact]
    public void AddGeneratedParameter_With_Configured_SqlParameter_Does_Not_Mutate_Input()
    {
        var parameter = new SqlParameter("Id", SqlDbType.Int) { Value = 42 };

        var sp = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16)
            .AddGeneratedParameter(1, parameter)
            .MarkGeneratedParametersBound()
            .Build();

        Assert.Equal("Id", parameter.ParameterName);
        Assert.Equal("@Id", sp.GetParametersSpan()[0].ParameterName);
    }

    [Fact]
    public void AddGeneratedParameter_With_Configured_SqlParameter_Converts_Null_Value_To_DBNull()
    {
        var parameter = new SqlParameter("@Value", SqlDbType.NVarChar);

        var sp = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16)
            .AddGeneratedParameter(1, parameter)
            .MarkGeneratedParametersBound()
            .Build();

        Assert.Same(DBNull.Value, sp.GetParametersSpan()[0].Value);
    }

    [Fact]
    public void AddGeneratedTvpParameter_Preserves_Structured_TypeName_And_Streams_Rows()
    {
        var rows = new[]
        {
            new TestGeneratedTvpRow(1),
            new TestGeneratedTvpRow(2)
        };

        var sp = StoredProcedureParametersBuilder<TestTvpProcedure>
            .Create(16)
            .AddGeneratedTvpParameter<TestGeneratedTvpRow>(
                1,
                "Ids",
                "dbo.UserIdList",
                [new SqlMetaData("UserId", SqlDbType.Int)],
                rows,
                static (record, row) => record.SetInt32(0, row.UserId))
            .MarkGeneratedParametersBound()
            .Build();

        var parameter = sp.GetParametersSpan()[0];
        var records = Assert.IsAssignableFrom<IEnumerable<SqlDataRecord>>(parameter.Value);
        var values = records.Select(record => record.GetInt32(0)).ToArray();

        Assert.Equal(SqlDbType.Structured, parameter.SqlDbType);
        Assert.Equal("@Ids", parameter.ParameterName);
        Assert.Equal("dbo.UserIdList", parameter.TypeName);
        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void AddGeneratedTvpParameter_Invalid_Name_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestTvpProcedure>
            .Create(16);

        Assert.Throws<ArgumentException>(() =>
            builder.AddGeneratedTvpParameter(
                1,
                "@@Ids",
                "dbo.UserIdList",
                [new SqlMetaData("UserId", SqlDbType.Int)],
                ReadOnlyMemory<TestGeneratedTvpRow>.Empty,
                static (_, _) => { }));
    }

    [Fact]
    public void AddGeneratedTvpParameter_Empty_Metadata_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestTvpProcedure>
            .Create(16);

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.AddGeneratedTvpParameter(
                1,
                "@Ids",
                "dbo.UserIdList",
                [],
                ReadOnlyMemory<TestGeneratedTvpRow>.Empty,
                static (_, _) => { }));

        Assert.Equal("metadata", exception.ParamName);
    }

    [Fact]
    public void AddGeneratedParameter_Out_Of_Order_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddGeneratedParameter(2, "@Id", 42, SqlDbType.Int));
    }

    [Fact]
    public void AddGeneratedParameter_Ordinal_Zero_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddGeneratedParameter(0, "@Id", 42, SqlDbType.Int));

        Assert.Contains("1-based", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddGeneratedParameter_Beyond_Declared_Count_Throws()
    {
        var builder = StoredProcedureParametersBuilder<TestScalarProcedure>
            .Create(16)
            .AddGeneratedParameter(1, "@Id", 42, SqlDbType.Int);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddGeneratedParameter(2, "@Extra", 43, SqlDbType.Int));

        Assert.Contains("expects only 1 generated parameter(s)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkGeneratedParametersBound_With_Missing_Parameters_Throws_With_Counts()
    {
        var builder = StoredProcedureParametersBuilder<TestFacetProcedure>
            .Create(16)
            .AddGeneratedParameter(1, "@Name", "ari", SqlDbType.NVarChar, 64);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.MarkGeneratedParametersBound());

        Assert.Contains("expects 2 generated parameter(s) but 1 were bound", exception.Message,
            StringComparison.Ordinal);
    }

    private readonly record struct TestGeneratedTvpRow(int UserId);

    private sealed record TestScalarParameters(int Id)
        : ICaeriusGeneratedProcedureParameters<TestScalarProcedure, TestScalarParameters>
    {
        public static void Bind(
            StoredProcedureParametersBuilder<TestScalarProcedure> builder,
            TestScalarParameters parameters)
        {
            builder
                .AddGeneratedParameter(1, "@Id", parameters.Id, SqlDbType.Int)
                .MarkGeneratedParametersBound();
        }
    }

    private readonly struct TestNoParameterProcedure : ICaeriusGeneratedProcedure<TestNoParameterProcedure>
    {
        public static string SchemaName => "dbo";
        public static string ProcedureName => "usp_NoParameters";
        public static string FullName => "dbo.usp_NoParameters";
        public static string ContractHash => "sha256:test";
        public static int ParameterCount => 0;
        public static int ResultSetCount => 1;
    }

    private readonly struct TestScalarProcedure : ICaeriusGeneratedProcedure<TestScalarProcedure>
    {
        public static string SchemaName => "dbo";
        public static string ProcedureName => "usp_ById";
        public static string FullName => "dbo.usp_ById";
        public static string ContractHash => "sha256:test";
        public static int ParameterCount => 1;
        public static int ResultSetCount => 1;
    }

    private readonly struct TestFacetProcedure : ICaeriusGeneratedProcedure<TestFacetProcedure>
    {
        public static string SchemaName => "dbo";
        public static string ProcedureName => "usp_ByFacets";
        public static string FullName => "dbo.usp_ByFacets";
        public static string ContractHash => "sha256:test";
        public static int ParameterCount => 2;
        public static int ResultSetCount => 1;
    }

    private readonly struct TestTvpProcedure : ICaeriusGeneratedProcedure<TestTvpProcedure>
    {
        public static string SchemaName => "dbo";
        public static string ProcedureName => "usp_ByIds";
        public static string FullName => "dbo.usp_ByIds";
        public static string ContractHash => "sha256:test";
        public static int ParameterCount => 1;
        public static int ResultSetCount => 1;
    }
}
