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

    internal static IReadOnlyList<Diagnostic> RunAnalyzer(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var attributeTree = CSharpSyntaxTree.ParseText(AttributeDefinitions, parseOptions);
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(attributeTree, sourceTree);

        var diagnostics = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new GeneratorUsageAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return diagnostics
            .Where(diagnostic => ReferenceEquals(diagnostic.Location.SourceTree, sourceTree))
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
            catch
            {
            }

        return references;
    }
}