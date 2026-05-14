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
                              using CaeriusNet.Attributes.Dto;
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
                              using CaeriusNet.Attributes.Dto;
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
                              using CaeriusNet.Attributes.Dto;
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
                              using CaeriusNet.Attributes.Dto;
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
                              using CaeriusNet.Attributes.Dto;
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
                              using CaeriusNet.Attributes.Tvp;
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
                              using CaeriusNet.Attributes.Tvp;
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
                              using CaeriusNet.Attributes.Tvp;
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
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "   ")]
                              public sealed partial record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_Valid_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, string Name);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_UnsupportedType_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_UnsupportedType_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_Int128_Property_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record BigNumDto(int Id, System.Int128 BigValue);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_Int128_Property_Reports_NoCaeriusDiagnostic()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_BigNum")]
                              public sealed partial record BigNumTvp(int Id, System.Int128 BigValue);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }
}