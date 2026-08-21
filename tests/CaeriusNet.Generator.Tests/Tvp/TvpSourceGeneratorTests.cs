using System.Text.RegularExpressions;

namespace CaeriusNet.Generator.Tests.Tvp;

public sealed class TvpSourceGeneratorTests
{
    [Fact]
    public void BasicRecord_Generates_ITvpMapper_Implementation()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("ITvpMapper<UserIdTvp>", generated);
        Assert.Contains("CreateRecord", generated);
        Assert.Contains("WriteRow", generated);
    }

    [Fact]
    public void Generates_Static_SqlTypeName_Property()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlTypeName", generated);
        Assert.Contains("dbo.tvp_int", generated);
    }

    [Fact]
    public void SqlTypeName_Escapes_Generated_String_Literal()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "db\"o", TvpName = "tvp\\int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("""public static string SqlTypeName => "db\"o.tvp\\int";""", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void Generates_Static_MetaData_Array_Field()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("private static readonly SqlMetaData[] _tvpMetaData", generated);
        Assert.Contains("SqlDbType.Int", generated);
    }

    [Fact]
    public void Custom_Schema_Produces_Correct_TvpTypeName()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "Types", TvpName = "tvp_UserId")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("Types.tvp_UserId", generated);
    }

    [Fact]
    public void NullableField_Generates_SetDBNull_Guard()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_opt")]
                              public sealed partial record OptionalTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("OptionalValue", 1, global::System.Data.SqlDbType.Int, true)] int? OptionalValue);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SetDBNull", generated);
    }

    [Fact]
    public void DateOnly_Field_Generates_ToDateTime_Conversion()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_date")]
                              public sealed partial record DateTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("EventDate", 1, global::System.Data.SqlDbType.Date, false)] System.DateOnly EventDate);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("ToDateTime", generated);
    }

    [Fact]
    public void TimeOnly_Field_Generates_ToTimeSpan_Conversion()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_time")]
                              public sealed partial record TimeTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("StartTime", 1, global::System.Data.SqlDbType.Time, false, Scale = 7)] System.TimeOnly StartTime);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("ToTimeSpan", generated);
    }

    [Fact]
    public void String_Field_Generates_NVarChar_MetaData()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_str")]
                              public sealed partial record StringTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.NVarChar", generated);
    }

    [Fact]
    public void GeneratedFile_Uses_Stable_Unique_HintName()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_item")]
                              public sealed partial record ItemTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        Assert.Contains("Test.Models.ItemTvp.Tvp.g.cs", result.GeneratedTrees[0].FilePath);
    }

    [Fact]
    public void Duplicate_Type_Names_In_Different_Namespaces_Generate_Distinct_HintNames()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Alpha.Models
                              {
                                  [GenerateTvp(Schema = "dbo", TvpName = "tvp_user")]
                                  public sealed partial record UserTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              }
                              namespace Beta.Models
                              {
                                  [GenerateTvp(Schema = "dbo", TvpName = "tvp_user")]
                                  public sealed partial record UserTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              }
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var filePaths = result.GeneratedTrees
            .Select(static tree => tree.FilePath)
            .OrderBy(static path => path)
            .ToArray();

        Assert.Equal(2, filePaths.Length);
        Assert.Contains(filePaths, path => path.Contains("Alpha.Models.UserTvp.Tvp.g.cs", StringComparison.Ordinal));
        Assert.Contains(filePaths, path => path.Contains("Beta.Models.UserTvp.Tvp.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateRecord_Reuses_Immutable_Metadata()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("CreateRecord() => new(_tvpMetaData);", generated);
        Assert.DoesNotContain("yield return", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRow_Generates_Immediate_Record_Null_Guard()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed partial record UserIdTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("ArgumentNullException.ThrowIfNull(record);", generated);
        Assert.Contains("public static void WriteRow(SqlDataRecord record, UserIdTvp item)", generated);
    }

    [Fact]
    public void Bool_Field_Generates_SqlDbType_Bit()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_flag")]
                              public sealed partial record FlagTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("IsActive", 1, global::System.Data.SqlDbType.Bit, false)] bool IsActive);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.Bit", generated);
    }

    [Fact]
    public void Long_Field_Generates_SqlDbType_BigInt()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_big")]
                              public sealed partial record BigIntTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Ticks", 1, global::System.Data.SqlDbType.BigInt, false)] long Ticks);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.BigInt", generated);
    }

    [Fact]
    public void Decimal_Field_Generates_SqlDbType_Decimal()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_price")]
                              public sealed partial record PriceTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Price", 1, global::System.Data.SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal Price);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.Decimal", generated);
    }

    [Fact]
    public void Guid_Field_Generates_SqlDbType_UniqueIdentifier()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_guid")]
                              public sealed partial record GuidTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("TraceId", 1, global::System.Data.SqlDbType.UniqueIdentifier, false)] System.Guid TraceId);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.UniqueIdentifier", generated);
    }

    [Fact]
    public void Nullable_String_Generates_SetDBNull_And_NVarChar()
    {
        const string source = """
                              #nullable enable
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_optstr")]
                              public sealed partial record OptStrTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, true, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string? Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.NVarChar", generated);
        Assert.Contains("SetDBNull", generated);
    }

    [Fact]
    public void MultiColumn_MetaData_Array_Has_Correct_Length()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_multi")]
                              public sealed partial record MultiTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name, [SqlServerTvpColumn("Price", 2, global::System.Data.SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal Price, [SqlServerTvpColumn("IsActive", 3, global::System.Data.SqlDbType.Bit, false)] bool IsActive);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        // Generator uses C# collection expressions; verify all 4 properties map to SqlMetaData entries
        var metaDataEntries = Regex.Matches(generated, @"new SqlMetaData\(").Count;
        Assert.Equal(4, metaDataEntries);
    }

    [Fact]
    public void NonSealed_Type_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public partial record UserIdTvp(int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void NonPartial_Type_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
                              public sealed record UserIdTvp(int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Half_Field_Generates_SqlDbType_Real()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_sensor")]
                              public sealed partial record SensorTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Temperature", 1, global::System.Data.SqlDbType.Real, false)] System.Half Temperature);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SqlDbType.Real", generated);
        Assert.Contains("SetFloat", generated);
        Assert.Contains("(float)", generated);
    }

    [Fact]
    public void Nullable_Half_Field_Generates_SetDBNull_Guard()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_sensor")]
                              public sealed partial record SensorTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Temperature", 1, global::System.Data.SqlDbType.Real, true)] System.Half? Temperature);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("SetDBNull", generated);
        Assert.Contains("SetFloat", generated);
        Assert.Contains("(float)", generated);
    }

    [Fact]
    public void Tvp_Generated_Code_Contains_GeneratedCodeAttribute()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_test")]
                              public sealed partial record TestTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("[GeneratedCode(\"CaeriusNet.Generator\"", generated);
    }

    [Fact]
    public void Tvp_Generated_Code_Contains_PragmaWarningDisable()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_test")]
                              public sealed partial record TestTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("#pragma warning disable CS1591", generated);
    }

    [Fact]
    public void Tvp_Generated_Code_Contains_XmlDocComments()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_test")]
                              public sealed partial record TestTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("/// <summary>", generated);
    }

    [Fact]
    public void Ordinary_PrimaryConstructor_Class_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_cls")]
                              public sealed partial class ClassTvp(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
        Assert.Null(Assert.Single(result.Results).Exception);
    }

    [Fact]
    public void Readonly_Record_Struct_Generates_And_Compiles()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_struct")]
                              public readonly partial record struct StructTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id, [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("public readonly partial record struct StructTvp", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void Internal_Type_Generates_Internal_Partial_Declaration_And_Compiles()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_internal")]
                              internal sealed partial record InternalTvp([SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("internal sealed partial record InternalTvp", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void Nested_And_Generic_Types_Do_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              public sealed partial class Container
                              {
                                  [GenerateTvp(Schema = "dbo", TvpName = "tvp_nested")]
                                  public sealed partial record NestedTvp(int Id);
                              }
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_generic")]
                              public sealed partial record GenericTvp<T>(int Id);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void EscapedAndUnicodeIdentifiers_Are_Valid_In_Tvp_Writer()
    {
        const string source = """
                              using CaeriusNet;
                              namespace @event;
                              [GenerateTvp(Schema = "dbo", TvpName = "tvp_keywords")]
                              public sealed partial record @class([SqlServerTvpColumn("FooLower", 0, global::System.Data.SqlDbType.Int, false)] int foo, [SqlServerTvpColumn("FooUpper", 1, global::System.Data.SqlDbType.Int, false)] int Foo, [SqlServerTvpColumn("Cafe", 2, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string café);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("namespace @event;", generated);
        Assert.Contains("partial record @class : ITvpMapper<@class>", generated);
        Assert.Contains("item.foo", generated);
        Assert.Contains("item.Foo", generated);
        Assert.Contains("item.café", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void ManualColumns_UseExplicitFacetsAndSqlOrdinal_WhenGeneratedCodeCompiles()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(TvpName = "tvp_explicit")]
                              public sealed partial record ExplicitTvp(
                                  [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.VarChar, false, MaxLength = 64, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.IgnoreCase)] string Name,
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Amount", 2, global::System.Data.SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal Amount);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("new SqlMetaData(\"Name\", SqlDbType.VarChar, 64, 1033", generated);
        Assert.Contains("new SqlMetaData(\"Amount\", SqlDbType.Decimal, 18, 4)", generated);
        Assert.Contains("record.SetInt32(0, item.Id);", generated);
        Assert.Contains("record.SetString(1, item.Name);", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void SqlDecimalPrecision38_UsesExactMetadataAndSetSqlDecimal_WhenGeneratedCodeCompiles()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(TvpName = "tvp_decimal38")]
                              public sealed partial record Decimal38Tvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Amount", 1, global::System.Data.SqlDbType.Decimal, false, Precision = 38, Scale = 12)] global::System.Data.SqlTypes.SqlDecimal Amount);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("new SqlMetaData(\"Amount\", SqlDbType.Decimal, 38, 12)", generated);
        Assert.Contains("record.SetSqlDecimal(1, item.Amount);", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Fact]
    public void UInt64DecimalColumn_UsesCheckedConversion_WhenGeneratedCodeCompiles()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Test.Models;
                              [GenerateTvp(TvpName = "tvp_uint64")]
                              public sealed partial record UInt64Tvp(
                                  [SqlServerTvpColumn("Value", 0, global::System.Data.SqlDbType.Decimal, false, Precision = 20, Scale = 0)] ulong Value);
                              """;

        var (result, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("record.SetDecimal(0, checked((decimal)item.Value));", generated);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0009"));
    }

    [Theory]
    [InlineData("public sealed partial record MissingAttributeTvp(int Id);", "CAERIUS007")]
    [InlineData(
        "public sealed partial record BadFacetsTvp([SqlServerTvpColumn(\"Name\", 0, global::System.Data.SqlDbType.NVarChar, false)] string Name);",
        "CAERIUS007")]
    [InlineData(
        "public sealed partial record BadNameTvp([SqlServerTvpColumn(\"not-valid\", 0, global::System.Data.SqlDbType.Int, false)] int Id);",
        "CAERIUS007")]
    [InlineData(
        "public sealed partial record BadNullabilityTvp([SqlServerTvpColumn(\"Id\", 0, global::System.Data.SqlDbType.Int, false)] int? Id);",
        "CAERIUS007")]
    [InlineData(
        "public sealed partial record BadDecimalTvp([SqlServerTvpColumn(\"Amount\", 0, global::System.Data.SqlDbType.Decimal, false, Precision = 38, Scale = 0)] decimal Amount);",
        "CAERIUS007")]
    [InlineData(
        "public sealed partial record UnsupportedTvp([SqlServerTvpColumn(\"Endpoint\", 0, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 64, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] System.Uri Endpoint);",
        "CAERIUS005")]
    public void InvalidManualTvpContract_ReportsBlockingDiagnosticAndDoesNotGenerate(string declaration,
        string diagnosticId)
    {
        var source = """
                     using CaeriusNet;
                     namespace Test.Models;
                     [GenerateTvp(TvpName = "tvp_invalid")]
                     """ + declaration;

        var result = SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);

        Assert.Null(Assert.Single(result.Results).Exception);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId &&
                                                          diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(result.GeneratedTrees);
    }
}
