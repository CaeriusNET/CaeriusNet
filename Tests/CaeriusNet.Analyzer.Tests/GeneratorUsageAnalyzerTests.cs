namespace CaeriusNet.Analyzer.Tests;

public sealed class GeneratorUsageAnalyzerTests
{
    [Fact]
    public void GenerateDto_NonSealed_Reports_CAERIUS001()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;

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
                              using CaeriusNet.Attributes.Dto;

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
                              using CaeriusNet.Attributes.Dto;

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
                              using CaeriusNet.Attributes.Dto;

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
                              using CaeriusNet.Attributes.Tvp;

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
                              using CaeriusNet.Attributes.Dto;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS005", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void GenerateTvp_UnsupportedType_Reports_CAERIUS005()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;

                              namespace TestNs;

                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        AssertDiagnostic(diagnostics, "CAERIUS005", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void GenerateTvp_StructuralFailure_DoesNotAlsoReport_CAERIUS005()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;

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
                              using CaeriusNet.Attributes.Dto;
                              using CaeriusNet.Attributes.Tvp;

                              namespace TestNs;

                              [GenerateDto]
                              public sealed partial record FooDto(int Id, string Name);

                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, string Name);
                              """;

        var diagnostics = AnalyzerTestHelper.RunAnalyzer(source);

        Assert.Empty(FilterCaeriusDiagnostics(diagnostics));
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