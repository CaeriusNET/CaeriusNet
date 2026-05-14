using System.Text;

namespace CaeriusNet.Analyzer.Tests.Utilities;

internal static class AnalyzerTestHelper
{
    private const string AttributeDefinitions = """
                                                using System;

                                                namespace CaeriusNet.Attributes.Dto
                                                {
                                                    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
                                                    public sealed class GenerateDtoAttribute : Attribute
                                                    {
                                                    }
                                                }

                                                namespace CaeriusNet.Attributes.Tvp
                                                {
                                                    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
                                                    public sealed class GenerateTvpAttribute : Attribute
                                                    {
                                                        public string Schema { get; init; } = "dbo";

                                                        public string TvpName { get; init; } = string.Empty;
                                                    }
                                                }
                                                """;

    internal static IReadOnlyList<Diagnostic> RunAnalyzer(string source, params AdditionalText[] additionalTexts)
    {
        return RunAnalyzer(source, globalOptions: null, additionalTexts);
    }

    internal static IReadOnlyList<Diagnostic> RunAnalyzer(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions,
        params AdditionalText[] additionalTexts)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var attributeTree = CSharpSyntaxTree.ParseText(AttributeDefinitions, parseOptions);
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(attributeTree, sourceTree);

        var analyzerOptions = new AnalyzerOptions(
            additionalTexts.ToImmutableArray(),
            new TestAnalyzerConfigOptionsProvider(globalOptions));
        var diagnostics = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new GeneratorUsageAnalyzer(),
                    new AutoContractsManifestAnalyzer()),
                analyzerOptions)
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return diagnostics
            .Where(diagnostic =>
                additionalTexts.Length > 0 ||
                globalOptions is not null ||
                ReferenceEquals(diagnostic.Location.SourceTree, sourceTree))
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees)
    {
        return CSharpCompilation.Create(
            "AnalyzerTests",
            syntaxTrees,
            BuildMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
    {
        var dotnetDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>();

        foreach (var dll in Directory.EnumerateFiles(dotnetDir, "*.dll"))
            try
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
            // Test metadata discovery is best-effort: some runtime DLLs cannot be loaded as Roslyn metadata references.
            catch (BadImageFormatException)
            {
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

        return references;
    }
}

internal sealed class TestAnalyzerConfigOptionsProvider(
    IReadOnlyDictionary<string, string>? globalOptions) : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions(null);

    public override AnalyzerConfigOptions GlobalOptions { get; } =
        new TestAnalyzerConfigOptions(globalOptions);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return EmptyOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return EmptyOptions;
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

internal sealed class TestAdditionalText(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(text, Encoding.UTF8);
    }
}
