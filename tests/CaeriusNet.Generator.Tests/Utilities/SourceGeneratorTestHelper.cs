using System.Collections.Immutable;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

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
        return RunGeneratorForAssembly<TGenerator>(source, "TestAssembly", additionalTexts);
    }

    internal static GeneratorDriverRunResult RunGeneratorForAssembly<TGenerator>(
        string source,
        string assemblyName,
        params AdditionalText[] additionalTexts)
        where TGenerator : IIncrementalGenerator, new()
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(syntaxTree, assemblyName);

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(additionalTexts.ToImmutableArray())
            .WithUpdatedParseOptions(parseOptions)
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(additionalTexts));

        return driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
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
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilation = CreateCompilation(syntaxTree);

        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(additionalTexts.ToImmutableArray())
            .WithUpdatedParseOptions(parseOptions)
            .WithUpdatedAnalyzerConfigOptions(new TestAnalyzerConfigOptionsProvider(additionalTexts));

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _,
            TestContext.Current.CancellationToken);
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
        return CreateCompilation(syntaxTree, "TestAssembly");
    }

    private static CSharpCompilation CreateCompilation(SyntaxTree syntaxTree, string assemblyName)
    {
        var references = BuildMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName,
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

        var dependencyInjectionLocation = typeof(IServiceCollection).Assembly.Location;
        if (!string.IsNullOrEmpty(dependencyInjectionLocation))
            references.Add(MetadataReference.CreateFromFile(dependencyInjectionLocation));

        return references;
    }
}