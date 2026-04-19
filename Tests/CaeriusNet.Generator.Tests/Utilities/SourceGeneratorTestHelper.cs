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
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(syntaxTree);

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .WithUpdatedParseOptions(parseOptions);

        return driver.RunGenerators(compilation).GetRunResult();
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

        return references;
    }
}