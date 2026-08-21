namespace CaeriusNet.Analyzer.Tests;

public sealed class GeneratorUsageAnalyzerTests
{
    [Fact]
    public void GenerateDto_NonSealed_Reports_CAERIUS001()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public partial record FooDto(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS001", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateDto_NonPartial_Reports_CAERIUS002()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed record FooDto(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS002", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateDto_NoPrimaryConstructor_Reports_CAERIUS003()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial class FooDto
                              {
                                  public int Id { get; set; }
                              }
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS003", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateDto_SplitPartial_WithPrimaryConstructorOnOtherPart_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto;

                              public sealed partial record FooDto(int Id, string Name);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        Assert.Empty(FilterCaeriusDiagnostics(diagnostics));
    }

    [Fact]
    public void GenerateTvp_WhitespaceTvpName_Reports_CAERIUS004()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateTvp(TvpName = "   ")]
                              public sealed partial record FooTvp(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS004", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateDto_UnsupportedType_Reports_CAERIUS005()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS005", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_UnsupportedType_Reports_CAERIUS005()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS005", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_StructuralFailure_DoesNotAlsoReport_CAERIUS005()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public partial record FooTvp(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = FilterCaeriusDiagnostics(AnalyzerTestHelper.RunAnalyzer(source)).ToList();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CAERIUS001");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CAERIUS005");
    }

    [Fact]
    public void ValidTargets_Report_NoCaeriusDiagnostics()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto(int Id, string Name);

                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        Assert.Empty(FilterCaeriusDiagnostics(diagnostics));
    }

    [Fact]
    public void GenerateDto_GenericTarget_Reports_CAERIUS006()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto<T>(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS006", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_NestedTarget_Reports_CAERIUS006()
    {
        const string source = """
                              using CaeriusNet;

                              namespace TestNs;

                              public sealed partial class Container
                              {
                                  [GenerateTvp(TvpName = "tvp_Foo")]
                                  public sealed partial record FooTvp(int Id);
                              }
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS006", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_Ordinary_PrimaryConstructor_Class_Reports_CAERIUS006()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial class FooTvp(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS006", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_Readonly_Record_Struct_Reports_NoCaeriusDiagnostics()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public readonly partial record struct FooTvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        Assert.Empty(FilterCaeriusDiagnostics(diagnostics));
    }

    [Fact]
    public void AttributeAlias_Is_Analyzed()
    {
        const string source = """
                              using DtoAlias = CaeriusNet.GenerateDtoAttribute;

                              namespace TestNs;

                              [DtoAlias]
                              public partial record FooDto(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS001", DiagnosticSeverity.Error);
    }

    [Fact]
    public void SplitPartialWithUnrelatedAttribute_Reports_EachGeneratorDiagnostic_Once()
    {
        const string source = """
                              using System;
                              using CaeriusNet;

                              namespace TestNs;

                              [AttributeUsage(AttributeTargets.Class)]
                              public sealed class MarkerAttribute : Attribute;

                              [GenerateDto]
                              public partial record Customer(int Id);

                              [Marker]
                              public partial record Customer;
                              """;

        var diagnostics = FilterCaeriusDiagnostics(AnalyzerTestHelper.RunAnalyzer(source)).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("CAERIUS001", diagnostic.Id);
    }

    [Fact]
    public void GenerateTvp_MissingColumnAttribute_Reports_CAERIUS007()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS007", DiagnosticSeverity.Error);
    }

    [Fact]
    public void GenerateTvp_ExplicitManualColumns_WithFacets_Report_NoCaeriusDiagnostics()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(
                                  [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.VarChar, false, MaxLength = 64, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.IgnoreCase)] string Name,
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Amount", 2, global::System.Data.SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal Amount);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        Assert.Empty(FilterCaeriusDiagnostics(diagnostics));
    }

    [Theory]
    [InlineData(
        "public sealed partial record FooTvp([SqlServerTvpColumn(\"Name\", 0, global::System.Data.SqlDbType.NVarChar, false)] string Name);")]
    [InlineData(
        "public sealed partial record FooTvp([SqlServerTvpColumn(\"Name\", 2, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);")]
    [InlineData(
        "public sealed partial record FooTvp([SqlServerTvpColumn(\"Amount\", 0, global::System.Data.SqlDbType.Decimal, false, Precision = 38, Scale = 4)] decimal Amount);")]
    public void GenerateTvp_InvalidManualColumnMetadata_Reports_CAERIUS007(string declaration)
    {
        var source = """
                     using CaeriusNet;
                     namespace TestNs;
                     [GenerateTvp(TvpName = "tvp_Foo")]
                     """ + declaration;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS007", DiagnosticSeverity.Error);
    }

    private static IEnumerable<Diagnostic> FilterCaeriusDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
    }

    private static void AssertDiagnostic(
        IEnumerable<Diagnostic> diagnostics,
        string id,
        DiagnosticSeverity severity)
    {
        Assert.Contains(
            FilterCaeriusDiagnostics(diagnostics),
            diagnostic => diagnostic.Id == id && diagnostic.Severity == severity);
    }
}
