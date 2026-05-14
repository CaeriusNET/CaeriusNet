using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.SqlClient;

namespace CaeriusNet.Generator.Tests.Utilities;

/// <summary>
///     Helper for running Roslyn source generators in-memory during unit tests.
/// </summary>
internal static class SourceGeneratorTestHelper
{
    /// <summary>
    ///     Compiles <paramref name="source" /> through <typeparamref name="TGenerator" /> and returns the full run result.
    /// </summary>
    internal static GeneratorDriverRunResult RunGenerator<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        return RunGenerator<TGenerator>(source, []);
    }

    internal static GeneratorDriverRunResult RunGenerator<TGenerator>(
        string source,
        params AdditionalText[] additionalTexts)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(syntaxTree);

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(additionalTexts.ToImmutableArray())
            .WithUpdatedParseOptions(parseOptions)
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(additionalTexts));

        return driver.RunGenerators(compilation).GetRunResult();
    }

    internal static (GeneratorDriverRunResult RunResult, Compilation Compilation)
        RunGeneratorWithCompilation<TGenerator>(
            string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        return RunGeneratorWithCompilation<TGenerator>(source, []);
    }

    internal static (GeneratorDriverRunResult RunResult, Compilation Compilation)
        RunGeneratorWithCompilation<TGenerator>(
            string source,
            params AdditionalText[] additionalTexts)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(syntaxTree);

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(additionalTexts.ToImmutableArray())
            .WithUpdatedParseOptions(parseOptions)
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(additionalTexts));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return (driver.GetRunResult(), outputCompilation);
    }

    /// <summary>
    ///     Builds a test compilation seeded with the same references as <see cref="RunGenerator{TGenerator}" />.
    ///     Exposed so caching tests can drive the generator across multiple runs on the same compilation.
    /// </summary>
    internal static CSharpCompilation CreateTestCompilation(SyntaxTree syntaxTree)
    {
        return CreateCompilation(syntaxTree);
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree syntaxTree)
    {
        var references = BuildMetadataReferences();

        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    ///     Collects metadata references: all currently loaded .NET runtime DLLs plus the CaeriusNet assembly
    ///     so that <c>[GenerateDto]</c>, <c>[GenerateTvp]</c>, <c>ISpMapper&lt;T&gt;</c> and <c>ITvpMapper&lt;T&gt;</c>
    ///     are resolvable during compilation.
    /// </summary>
    private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
    {
        var dotnetDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>();

        foreach (var dll in Directory.EnumerateFiles(dotnetDir, "*.dll"))
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            catch
            {
                // Skip DLLs that cannot be loaded as metadata (e.g., native helpers)
            }

        // Add CaeriusNet assembly — required for attribute and interface resolution
        var caeriusNetLocation = typeof(ISpMapper<>).Assembly.Location;
        if (!string.IsNullOrEmpty(caeriusNetLocation))
            references.Add(MetadataReference.CreateFromFile(caeriusNetLocation));

        var sqlClientLocation = typeof(SqlDataReader).Assembly.Location;
        if (!string.IsNullOrEmpty(sqlClientLocation))
            references.Add(MetadataReference.CreateFromFile(sqlClientLocation));

        return references;
    }
}

internal sealed class TestAnalyzerConfigOptionsProvider(
    IReadOnlyList<AdditionalText>? additionalTexts = null) : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions(null);
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _additionalOptions =
        BuildAdditionalOptions(additionalTexts);

    public override AnalyzerConfigOptions GlobalOptions => EmptyOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return EmptyOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return _additionalOptions.TryGetValue(textFile.Path, out var options)
            ? new TestAnalyzerConfigOptions(options)
            : EmptyOptions;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildAdditionalOptions(
        IReadOnlyList<AdditionalText>? additionalTexts)
    {
        if (additionalTexts is null || additionalTexts.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        var options = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var additionalText in additionalTexts)
            if (additionalText is TestAdditionalText testAdditionalText && testAdditionalText.Options.Count > 0)
                options[testAdditionalText.Path] = testAdditionalText.Options;

        return options;
    }
}

internal sealed class TestAnalyzerConfigOptions(
    IReadOnlyDictionary<string, string>? options) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        if (options is not null && options.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal sealed class TestAdditionalText(
    string path,
    string text,
    IReadOnlyDictionary<string, string>? options = null) : AdditionalText
{
    public override string Path { get; } = path;
    internal IReadOnlyDictionary<string, string> Options { get; } =
        options ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(text, Encoding.UTF8);
    }
}
