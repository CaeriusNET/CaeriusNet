namespace CaeriusNet.Generator.Tests.Tvp;

public sealed class TvpSourceGeneratorTests
{
    [Fact]
    public void BasicRecord_Generates_ITvpMapper_Implementation()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
            public sealed partial record UserIdTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("ITvpMapper<UserIdTvp>", generated);
        Assert.Contains("MapAsSqlDataRecords", generated);
    }

    [Fact]
    public void Generates_Static_TvpTypeName_Property()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
            public sealed partial record UserIdTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("TvpTypeName", generated);
        Assert.Contains("dbo.tvp_int", generated);
    }

    [Fact]
    public void Generates_Static_MetaData_Array_Field()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
            public sealed partial record UserIdTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("private static readonly SqlMetaData[] _tvpMetaData", generated);
        Assert.Contains("SqlDbType.Int", generated);
    }

    [Fact]
    public void Custom_Schema_Produces_Correct_TvpTypeName()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "Types", TvpName = "tvp_UserId")]
            public sealed partial record UserIdTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("Types.tvp_UserId", generated);
    }

    [Fact]
    public void NullableField_Generates_SetDBNull_Guard()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_opt")]
            public sealed partial record OptionalTvp(int Id, int? OptionalValue);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("SetDBNull", generated);
    }

    [Fact]
    public void DateOnly_Field_Generates_ToDateTime_Conversion()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_date")]
            public sealed partial record DateTvp(int Id, System.DateOnly EventDate);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("ToDateTime", generated);
    }

    [Fact]
    public void TimeOnly_Field_Generates_ToTimeSpan_Conversion()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_time")]
            public sealed partial record TimeTvp(int Id, System.TimeOnly StartTime);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("ToTimeSpan", generated);
    }

    [Fact]
    public void String_Field_Generates_NVarChar_MetaData()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_str")]
            public sealed partial record StringTvp(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("SqlDbType.NVarChar", generated);
    }

    [Fact]
    public void GeneratedFile_Is_Named_After_Type()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_item")]
            public sealed partial record ItemTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        Assert.Contains("ItemTvp.g.cs", result.GeneratedTrees[0].FilePath);
    }

    [Fact]
    public void SingleRecord_Reuses_SqlDataRecord_Instance()
    {
        const string source = """
            using CaeriusNet.Attributes.Tvp;
            namespace Test.Models;
            [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
            public sealed partial record UserIdTvp(int Id);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        // Verify the generator creates a single SqlDataRecord and reuses it (no new inside loop)
        Assert.Contains("var record = new SqlDataRecord(_tvpMetaData);", generated);
        Assert.Contains("yield return record;", generated);
    }
}
