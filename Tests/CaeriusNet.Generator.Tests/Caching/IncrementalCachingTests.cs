namespace CaeriusNet.Generator.Tests.Caching;

/// <summary>
///     Validates that the incremental pipeline preserves cached output when source code is unchanged.
///     Guards against accidentally flowing non-equatable Roslyn types through the pipeline state.
/// </summary>
public sealed class IncrementalCachingTests
{
    private const string DtoSource =
        """
        using CaeriusNet.Attributes.Dto;
        namespace Test.Models;
        [GenerateDto]
        public sealed partial record UserDto(int Id, string Name);
        """;

    private const string TvpSource =
        """
        using CaeriusNet.Attributes.Tvp;
        namespace Test.Models;
        [GenerateTvp(Schema = "dbo", TvpName = "tvp_int")]
        public sealed partial record UserIdTvp(int Id);
        """;

    [Fact]
    public void Dto_RerunWithSameSource_Returns_Cached_Result() =>
        AssertExtractionCached<DtoSourceGenerator>(DtoSource);

    [Fact]
    public void Tvp_RerunWithSameSource_Returns_Cached_Result() =>
        AssertExtractionCached<TvpSourceGenerator>(TvpSource);

    private static void AssertExtractionCached<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = SourceGeneratorTestHelper.CreateTestCompilation(syntaxTree);

        var generator = new TGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: default,
            parseOptions: parseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        // Second run on a clone of the same compilation: every step must hit the cache.
        var clone = compilation.WithOptions(compilation.Options);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(clone);

        var runResult = driver.GetRunResult().Results[0];
        foreach (var (_, steps) in runResult.TrackedOutputSteps)
        foreach (var step in steps)
        foreach (var (_, reason) in step.Outputs)
            Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected Cached/Unchanged, got {reason}. A non-equatable value is flowing through the pipeline.");
    }
}
