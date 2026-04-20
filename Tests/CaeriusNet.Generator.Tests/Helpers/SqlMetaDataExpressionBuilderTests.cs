namespace CaeriusNet.Generator.Tests.Helpers;

/// <summary>
///     Pin-down tests for <see cref="SqlMetaDataExpressionBuilder"/>. The 19-case switch encodes
///     non-trivial precision/length policy (decimal(18,4), datetime2(7), nvarchar(MAX)…) — drift here
///     would corrupt every TVP shipped via the generator, so we lock it in at the unit-test level.
/// </summary>
public sealed class SqlMetaDataExpressionBuilderTests
{
    [Theory]
    [InlineData("Active",       "bit",              "new SqlMetaData(\"Active\", SqlDbType.Bit)")]
    [InlineData("Tiny",         "tinyint",          "new SqlMetaData(\"Tiny\", SqlDbType.TinyInt)")]
    [InlineData("Small",        "smallint",         "new SqlMetaData(\"Small\", SqlDbType.SmallInt)")]
    [InlineData("N",            "int",              "new SqlMetaData(\"N\", SqlDbType.Int)")]
    [InlineData("Big",          "bigint",           "new SqlMetaData(\"Big\", SqlDbType.BigInt)")]
    [InlineData("Price",        "decimal",          "new SqlMetaData(\"Price\", SqlDbType.Decimal, 18, 4)")]
    [InlineData("R",            "real",             "new SqlMetaData(\"R\", SqlDbType.Real)")]
    [InlineData("F",            "float",            "new SqlMetaData(\"F\", SqlDbType.Float)")]
    [InlineData("Name",         "nvarchar",         "new SqlMetaData(\"Name\", SqlDbType.NVarChar, SqlMetaData.Max)")]
    [InlineData("Code",         "varchar",          "new SqlMetaData(\"Code\", SqlDbType.VarChar, SqlMetaData.Max)")]
    [InlineData("Letter",       "nchar",            "new SqlMetaData(\"Letter\", SqlDbType.NChar, 1)")]
    [InlineData("L",            "char",             "new SqlMetaData(\"L\", SqlDbType.Char, 1)")]
    [InlineData("CreatedAt",    "datetime2",        "new SqlMetaData(\"CreatedAt\", SqlDbType.DateTime2, 7)")]
    [InlineData("Legacy",       "datetime",         "new SqlMetaData(\"Legacy\", SqlDbType.DateTime)")]
    [InlineData("Day",          "date",             "new SqlMetaData(\"Day\", SqlDbType.Date)")]
    [InlineData("Hour",         "time",             "new SqlMetaData(\"Hour\", SqlDbType.Time, 7)")]
    [InlineData("UtcOffset",    "datetimeoffset",   "new SqlMetaData(\"UtcOffset\", SqlDbType.DateTimeOffset, 7)")]
    [InlineData("Id",           "uniqueidentifier", "new SqlMetaData(\"Id\", SqlDbType.UniqueIdentifier)")]
    [InlineData("Blob",         "varbinary",        "new SqlMetaData(\"Blob\", SqlDbType.VarBinary, SqlMetaData.Max)")]
    public void Build_Maps_Each_Known_SqlType(string columnName, string sqlType, string expected)
    {
        Assert.Equal(expected, SqlMetaDataExpressionBuilder.Build(columnName, sqlType));
    }

    [Fact]
    public void Build_Falls_Back_To_Variant_For_Unknown_Type()
    {
        var result = SqlMetaDataExpressionBuilder.Build("Whatever", "money");
        Assert.Equal("new SqlMetaData(\"Whatever\", SqlDbType.Variant)", result);
    }

    [Fact]
    public void Build_Preserves_Column_Name_With_Special_Characters()
    {
        // Generator policy: column names are emitted as-is into the generated string literal.
        // Test exists to flag the day someone introduces escaping by accident.
        var result = SqlMetaDataExpressionBuilder.Build("My Column", "int");
        Assert.Equal("new SqlMetaData(\"My Column\", SqlDbType.Int)", result);
    }
}
