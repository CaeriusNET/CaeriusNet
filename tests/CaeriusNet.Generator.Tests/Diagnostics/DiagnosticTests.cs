namespace CaeriusNet.Generator.Tests.Diagnostics;

/// <summary>
///     Validates that generators stay silent and only decide whether source can be emitted.
/// </summary>
public sealed class DiagnosticTests
{
    private static IEnumerable<Diagnostic> RunDto(string source)
    {
        return SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source).Diagnostics;
    }

    private static IEnumerable<Diagnostic> RunTvp(string source)
    {
        return SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source).Diagnostics;
    }

    private static GeneratorDriverRunResult RunDtoFull(string source)
    {
        return SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);
    }

    private static GeneratorDriverRunResult RunTvpFull(string source)
    {
        return SourceGeneratorTestHelper.RunGenerator<TvpSourceGenerator>(source);
    }

    [Fact]
    public void Dto_NonSealed_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public partial record FooDto(int Id);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_NonPartial_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed record FooDto(int Id);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
    }

    [Fact]
    public void Dto_NoPrimaryConstructor_Reports_NoCaeriusDiagnostic()
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

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_Valid_Sealed_Partial_Record_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto(int Id, string Name);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_SplitPartial_AttributeOnOneSide_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto;
                              public sealed partial record FooDto(int Id, string Name);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_NonSealed_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public partial record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_NonPartial_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
    }

    [Fact]
    public void Tvp_NoPrimaryConstructor_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial class FooTvp
                              {
                                  public int Id { get; set; }
                              }
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_EmptyTvpName_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "   ")]
                              public sealed partial record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_ValidExplicitContract_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Name", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string Name);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_UnsupportedType_Reports_NoCaeriusDiagnostic_And_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_UnsupportedType_Reports_CAERIUS005_And_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("Endpoint", 1, global::System.Data.SqlDbType.NVarChar, false, MaxLength = 128, LocaleId = 1033, CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] System.Uri Endpoint);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CAERIUS005" &&
                                                   diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_Int128_Property_Reports_NoCaeriusDiagnostic_And_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record BigNumDto(int Id, System.Int128 BigValue);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_Int128_Property_Reports_CAERIUS005_And_Does_Not_Generate()
    {
        const string source = """
                              using CaeriusNet;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_BigNum")]
                              public sealed partial record BigNumTvp(
                                  [SqlServerTvpColumn("Id", 0, global::System.Data.SqlDbType.Int, false)] int Id,
                                  [SqlServerTvpColumn("BigValue", 1, global::System.Data.SqlDbType.Decimal, false, Precision = 38, Scale = 0)] System.Int128 BigValue);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CAERIUS005" &&
                                                   diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }
}
