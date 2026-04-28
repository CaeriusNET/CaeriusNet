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

    [Theory]
    [InlineData("System.DateOnly", true)]
    [InlineData("DateOnly", true)]
    [InlineData("System.TimeOnly", true)]
    [InlineData("TimeOnly", true)]
    [InlineData("byte[]", true)]
    [InlineData("System.Byte[]", true)]
    [InlineData("System.Nullable<System.DateOnly>", true)]
    [InlineData("System.Nullable<System.TimeOnly>", true)]
    [InlineData("System.Nullable<System.Byte[]>", true)]
    [InlineData("System.Half", true)]
    [InlineData("Half", true)]
    [InlineData("System.Int32", false)]
    [InlineData("System.String", false)]
    [InlineData("System.Guid", false)]
    [InlineData("System.DateTime", false)]
    [InlineData("System.Nullable<System.Int32>", false)]
    public void RequiresSpecialConversion_Returns_Expected_Result(string typeName, bool expected)
    {
        var actual = TypeDetector.RequiresSpecialConversion(typeName);

        Assert.Equal(expected, actual);
    }
}
