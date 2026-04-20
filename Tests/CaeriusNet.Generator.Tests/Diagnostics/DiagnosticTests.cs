namespace CaeriusNet.Generator.Tests.Diagnostics;

/// <summary>
///     Validates that the source generators surface user-friendly compile-time diagnostics
///     (CAERIUS001-004) instead of silently skipping invalid candidates.
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
    public void Dto_NonSealed_Reports_CAERIUS001()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public partial record FooDto(int Id);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS001" && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_NonPartial_Reports_CAERIUS002()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed record FooDto(int Id);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Dto_NoPrimaryConstructor_Reports_CAERIUS003()
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

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS003" && d.Severity == DiagnosticSeverity.Error);
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

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_SplitPartial_AttributeOnOneSide_Reports_NoCaeriusDiagnostic()
    {
        // The attribute is carried by the declaration that does not include the primary constructor;
        // the validator must walk all DeclaringSyntaxReferences to confirm the type is well-formed.
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto;
                              public sealed partial record FooDto(int Id, string Name);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_NonSealed_Reports_CAERIUS001()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public partial record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS001" && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_NonPartial_Reports_CAERIUS002()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Tvp_NoPrimaryConstructor_Reports_CAERIUS003()
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

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS003" && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_EmptyTvpName_Reports_CAERIUS004()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "   ")]
                              public sealed partial record FooTvp(int Id);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS004" && d.Severity == DiagnosticSeverity.Error);
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

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("CAERIUS", StringComparison.Ordinal));
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }

    [Fact]
    public void Dto_UnsupportedType_Reports_CAERIUS005_Warning()
    {
        // System.Uri has no native SQL Server mapping → falls back to sql_variant.
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record FooDto(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS005" && d.Severity == DiagnosticSeverity.Warning);
        // Warning, not error: generator must still emit the partial.
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_UnsupportedType_Reports_CAERIUS005_Warning()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_Foo")]
                              public sealed partial record FooTvp(int Id, System.Uri Endpoint);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS005" && d.Severity == DiagnosticSeverity.Warning);
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }

    /// <summary>
    ///     CAERIUS006 (UnsupportedTypeWarning) is defined in DiagnosticDescriptors but is not currently
    ///     emitted by either generator. Both DtoExtractor and TvpExtractor emit CAERIUS005 for types that
    ///     fall back to sql_variant. This test verifies that Int128 triggers CAERIUS005 (not CAERIUS006).
    /// </summary>
    [Fact]
    public void Dto_Int128_Property_Reports_CAERIUS005_Not_CAERIUS006()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace TestNs;
                              [GenerateDto]
                              public sealed partial record BigNumDto(int Id, System.Int128 BigValue);
                              """;

        var diagnostics = RunDto(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS005" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CAERIUS006");
        Assert.NotEmpty(RunDtoFull(source).GeneratedTrees);
    }

    [Fact]
    public void Tvp_Int128_Property_Reports_CAERIUS005_Not_CAERIUS006()
    {
        const string source = """
                              using CaeriusNet.Attributes.Tvp;
                              namespace TestNs;
                              [GenerateTvp(TvpName = "tvp_BigNum")]
                              public sealed partial record BigNumTvp(int Id, System.Int128 BigValue);
                              """;

        var diagnostics = RunTvp(source).ToList();

        Assert.Contains(diagnostics, d => d.Id == "CAERIUS005" && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CAERIUS006");
        Assert.NotEmpty(RunTvpFull(source).GeneratedTrees);
    }
}