namespace CaeriusNet.Generator.Tests.Caching;

/// <summary>
///     Validates that the incremental pipeline preserves cached output when source code is unchanged.
///     Guards against accidentally flowing non-equatable Roslyn types through the pipeline state.
/// </summary>
public sealed class IncrementalCachingTests
{
    private const string DtoSource =
        """
        using CaeriusNet;
        namespace Test.Models;
        [GenerateDto]
        public sealed partial record UserDto(int Id, string Name);
        """;

    private const string TvpSource =
        """
        using CaeriusNet;
        namespace Test.Models;
        [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
        public sealed partial record UserIdTvp(int Id);
        """;

    private const string AutoContractsManifest =
        """
        {
          "version": 2,
          "namespace": "Test.Contracts",
          "tableTypes": [],
          "procedures": []
        }
        """;

    [Fact]
    public void Dto_RerunWithSameSource_Returns_Cached_Result()
    {
        AssertExtractionCached<DtoSourceGenerator>(DtoSource);
    }

    [Fact]
    public void Tvp_RerunWithSameSource_Returns_Cached_Result()
    {
        AssertExtractionCached<TvpSourceGenerator>(TvpSource);
    }

    [Fact]
    public void AutoContracts_RerunWithSameManifest_Returns_Cached_Result()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "namespace Test;",
            parseOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        var compilation = SourceGeneratorTestHelper.CreateTestCompilation(syntaxTree);
        var manifest = new TestAdditionalText("caerius.contracts.json", AutoContractsManifest);
        var generator = new AutoContractsSourceGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(
            [generator],
            [manifest],
            parseOptions,
            null,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            compilation,
            TestContext.Current.CancellationToken);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            compilation.WithOptions(compilation.Options),
            TestContext.Current.CancellationToken);

        AssertOutputStepsCached(driver);
    }

    private static void AssertExtractionCached<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = SourceGeneratorTestHelper.CreateTestCompilation(syntaxTree);

        var generator = new TGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(
            [generator],
            default,
            parseOptions,
            null,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            compilation,
            TestContext.Current.CancellationToken);
        // Second run on a clone of the same compilation: every step must hit the cache.
        var clone = compilation.WithOptions(compilation.Options);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            clone,
            TestContext.Current.CancellationToken);

        AssertOutputStepsCached(driver);
    }

    private static void AssertOutputStepsCached(GeneratorDriver driver)
    {
        var runResult = Assert.Single(driver.GetRunResult().Results);
        Assert.NotEmpty(runResult.TrackedOutputSteps);
        foreach (var (_, steps) in runResult.TrackedOutputSteps)
        foreach (var step in steps)
        foreach (var (_, reason) in step.Outputs)
            Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected Cached/Unchanged, got {reason}. A non-equatable value is flowing through the pipeline.");
    }
}
