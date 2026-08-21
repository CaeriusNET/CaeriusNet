namespace CaeriusNet.Analyzer.Tests.Utilities;

internal static class AnalyzerTestHelper
{
    private const string AttributeDefinitions = """
                                                using System;
                                                using System.Data;
                                                using System.Data.SqlTypes;

                                                namespace CaeriusNet
                                                {
                                                    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
                                                    public sealed class GenerateDtoAttribute : Attribute
                                                    {
                                                    }
                                                    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
                                                    public sealed class GenerateTvpAttribute : Attribute
                                                    {
                                                        public string Schema { get; init; } = "dbo";

                                                        public string TvpName { get; init; } = string.Empty;
                                                    }
                                                    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
                                                    public sealed class SqlServerTvpColumnAttribute : Attribute
                                                    {
                                                        public SqlServerTvpColumnAttribute(string name, int ordinal, SqlDbType sqlDbType, bool isNullable)
                                                        {
                                                            Name = name;
                                                            Ordinal = ordinal;
                                                            SqlDbType = sqlDbType;
                                                            IsNullable = isNullable;
                                                        }

                                                        public string Name { get; }
                                                        public int Ordinal { get; }
                                                        public SqlDbType SqlDbType { get; }
                                                        public bool IsNullable { get; }
                                                        public long MaxLength { get; init; } = long.MinValue;
                                                        public byte Precision { get; init; } = byte.MaxValue;
                                                        public byte Scale { get; init; } = byte.MaxValue;
                                                        public long LocaleId { get; init; } = -1;
                                                        public SqlCompareOptions CompareOptions { get; init; } = (SqlCompareOptions)(-1);
                                                    }
                                                }
                                                """;

    internal static IReadOnlyList<Diagnostic> RunAnalyzer(string source, params AdditionalText[] additionalTexts)
    {
        return RunAnalyzer(source, null, additionalTexts);
    }

    internal static IReadOnlyList<Diagnostic> RunAnalyzer(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions,
        params AdditionalText[] additionalTexts)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var attributeTree = CSharpSyntaxTree.ParseText(AttributeDefinitions, parseOptions);
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(attributeTree, sourceTree);

        var analyzerOptions = new AnalyzerOptions(
            additionalTexts.ToImmutableArray(),
            new TestAnalyzerConfigOptionsProvider(globalOptions, additionalTexts));
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
