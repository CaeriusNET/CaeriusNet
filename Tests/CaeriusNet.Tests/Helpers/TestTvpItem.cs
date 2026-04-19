namespace CaeriusNet.Tests.Helpers;

/// <summary>
///     Minimal <see cref="ITvpMapper{T}" /> implementation used exclusively in unit tests.
/// </summary>
internal sealed record TestTvpItem(int Id) : ITvpMapper<TestTvpItem>
{
    public static string TvpTypeName => "dbo.tvp_test";

    public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<TestTvpItem> items)
    {
        var metaData = new[] { new SqlMetaData("Id", SqlDbType.Int) };
        var record = new SqlDataRecord(metaData);
        foreach (var item in items)
        {
            record.SetInt32(0, item.Id);
            yield return record;
        }
    }
}