namespace CaeriusNet.Tests.Builders;

/// <summary>
///     Verifies that <see cref="StoredProcedureParametersBuilder.AddTvpParameter{T}" /> produces a
///     <see cref="SqlParameter" /> with <see cref="SqlDbType.Structured" /> and the correct
///     <see cref="SqlParameter.TypeName" /> — the information that the telemetry layer reads by
///     scanning <see cref="StoredProcedureParameters.GetParametersSpan" />.
/// </summary>
public sealed class StoredProcedureParametersBuilderTelemetryTests
{
    [Fact]
    public void Build_NoTvp_HasNoStructuredParameters()
    {
        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddParameter("@id", 1, SqlDbType.Int)
            .Build();

        var structured = sp.GetParametersSpan()
            .ToArray()
            .Where(p => p.SqlDbType == SqlDbType.Structured)
            .ToList();

        Assert.Empty(structured);
    }

    [Fact]
    public void AddTvpParameter_Single_EmitsStructuredParameterWithTypeName()
    {
        var items = new List<TestTvpItem> { new(1) };

        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@Ids", items)
            .Build();

        var structured = sp.GetParametersSpan()
            .ToArray()
            .Where(p => p.SqlDbType == SqlDbType.Structured)
            .ToList();

        Assert.Single(structured);
        Assert.Equal(TestTvpItem.TvpTypeName, structured[0].TypeName);
    }

    [Fact]
    public void AddTvpParameter_Multiple_EmitsAllStructuredParameters()
    {
        var items = new List<TestTvpItem> { new(1), new(2) };

        var sp = new StoredProcedureParametersBuilder("dbo", "sp_Test")
            .AddTvpParameter("@A", items)
            .AddTvpParameter("@B", items)
            .Build();

        var structured = sp.GetParametersSpan()
            .ToArray()
            .Where(p => p.SqlDbType == SqlDbType.Structured)
            .ToList();

        Assert.Equal(2, structured.Count);
        Assert.All(structured, p => Assert.Equal(TestTvpItem.TvpTypeName, p.TypeName));
    }
}