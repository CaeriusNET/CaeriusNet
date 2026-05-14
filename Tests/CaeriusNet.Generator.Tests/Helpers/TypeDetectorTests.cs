namespace CaeriusNet.Generator.Tests.Helpers;

public sealed class TypeDetectorTests
{
    [Theory]
    [InlineData("bit", "GetBoolean")]
    [InlineData("tinyint", "GetByte")]
    [InlineData("smallint", "GetInt16")]
    [InlineData("int", "GetInt32")]
    [InlineData("bigint", "GetInt64")]
    [InlineData("decimal", "GetDecimal")]
    [InlineData("real", "GetFloat")]
    [InlineData("float", "GetDouble")]
    [InlineData("nvarchar", "GetString")]
    [InlineData("nchar", "GetString")]
    [InlineData("varchar", "GetString")]
    [InlineData("char", "GetString")]
    [InlineData("text", "GetString")]
    [InlineData("datetime", "GetDateTime")]
    [InlineData("datetime2", "GetDateTime")]
    [InlineData("date", "GetDateTime")]
    [InlineData("smalldatetime", "GetDateTime")]
    [InlineData("uniqueidentifier", "GetGuid")]
    [InlineData("datetimeoffset", "GetDateTimeOffset")]
    [InlineData("time", "GetTimeSpan")]
    [InlineData("varbinary", "GetValue")]
    [InlineData("sql_variant", "GetValue")]
    [InlineData("unknown_type", "GetValue")]
    public void GetReaderMethodForSqlType_Returns_Expected_Method(string sqlType, string expectedMethod)
    {
        var actual = TypeDetector.GetReaderMethodForSqlType(sqlType);

        Assert.Equal(expectedMethod, actual);
    }
}
