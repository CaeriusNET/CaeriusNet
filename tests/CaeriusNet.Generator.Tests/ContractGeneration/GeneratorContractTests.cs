using CaeriusNet;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace CaeriusNet.Generator.Tests.ContractGeneration;

public sealed class GeneratorContractTests
{
    [Fact]
    public void Dto_generator_emits_mapper_binary_codec_and_registration()
    {
        const string source = """
                              using CaeriusNet;
                              namespace Consumer;
                              [GenerateDto]
                              public sealed partial record Customer(int Id, string? Name);
                              """;
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new DtoSourceGenerator().AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(updated);
        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        Assert.Contains("CustomerCaeriusCacheCodec", generated, StringComparison.Ordinal);
        Assert.Contains("AddCustomerCaeriusCodec", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("AggressiveInlining", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContracts_v2_merges_contiguous_override_result_sets()
    {
        const string manifest = """
                                {
                                  "version": 2,
                                  "namespace": "Consumer.Contracts",
                                  "tableTypes": [],
                                  "procedures": [{
                                    "schema": "dbo",
                                    "name": "GetReport",
                                    "clrName": "GetReportProcedure",
                                    "parametersClrName": "GetReportParameters",
                                    "parameters": [
                                      { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "ReturnValue", "nullable": false },
                                      { "ordinal": 1, "name": "year", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "Input", "nullable": false }
                                    ],
                                    "resultSets": [{
                                      "index": 0,
                                      "clrName": "GetReportResult",
                                      "status": "Available",
                                      "columns": [{ "ordinal": 1, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }],
                                      "contractHash": "sha256:first"
                                    }],
                                    "contractHash": "sha256:procedure"
                                  }]
                                }
                                """;
        const string overrides = """
                                 {
                                   "version": 2,
                                   "procedures": [{
                                     "schema": "dbo",
                                     "name": "GetReport",
                                     "resultSets": [{
                                       "index": 1,
                                       "clrName": "GetReportSummary",
                                       "status": "Available",
                                       "columns": [{ "ordinal": 1, "name": "Total", "sqlType": "bigint", "clrType": "long", "nullable": false }],
                                       "contractHash": "sha256:second"
                                     }]
                                   }]
                                 }
                                 """;
        var compilation = CreateCompilation("namespace Consumer; public static class EntryPoint { }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AutoContractsSourceGenerator().AsSourceGenerator()],
            [
                new TextFile("caerius.contracts.json", manifest),
                new TextFile("caerius.contracts.overrides.json", overrides)
            ],
            new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(updated);
        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));
        Assert.Contains("GetReportSummary", generated, StringComparison.Ordinal);
        Assert.Contains("QueryMultipleImmutableArrayWithOutputsAsync", generated, StringComparison.Ordinal);
        Assert.Contains("QueryMultipleWithOutputsAsync", generated, StringComparison.Ordinal);
        Assert.Contains("QueryMultipleReadOnlyCollectionWithOutputsAsync", generated, StringComparison.Ordinal);
        Assert.Contains("QueryEnumerableAsync", generated, StringComparison.Ordinal);
        Assert.Contains("QueryReadOnlyCollectionAsync", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("StoredProcedureParametersBuilder", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("AggressiveInlining", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContracts_v2_emits_pure_output_without_an_artificial_input_value()
    {
        const string manifest = """
                                {
                                  "version": 2,
                                  "namespace": "Consumer.Contracts",
                                  "tableTypes": [],
                                  "procedures": [{
                                    "schema": "dbo",
                                    "name": "ApplyDiscount",
                                    "clrName": "ApplyDiscountProcedure",
                                    "parametersClrName": "ApplyDiscountParameters",
                                    "parameters": [
                                      { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false },
                                      { "ordinal": 1, "name": "customerId", "sqlType": "int", "clrType": "int", "direction": "Input", "nullable": false },
                                      { "ordinal": 2, "name": "discount", "sqlType": "decimal(18,2)", "clrType": "decimal", "direction": "Output", "nullable": false, "precision": 18, "scale": 2 }
                                    ],
                                    "resultSets": [{
                                      "index": 0,
                                      "clrName": "ApplyDiscountResult",
                                      "status": "Available",
                                      "columns": [{ "ordinal": 1, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }]
                                    }]
                                  }]
                                }
                                """;

        var compilation = CreateCompilation("namespace Consumer; public static class EntryPoint { }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AutoContractsSourceGenerator().AsSourceGenerator()],
            [new TextFile("caerius.contracts.json", manifest)],
            new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(updated);
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
        Assert.Contains("public sealed partial record ApplyDiscountParameters(int customerId)", generated,
            StringComparison.Ordinal);
        Assert.Contains("ParameterDirection.Output", generated, StringComparison.Ordinal);
        Assert.Contains("null, SqlDbType.Decimal, ParameterDirection.Output", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters.discount", generated, StringComparison.Ordinal);
        Assert.Contains("public sealed record ApplyDiscountProcedureOutput", generated, StringComparison.Ordinal);
        Assert.Contains("QueryReadOnlyCollectionAsync", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContracts_v2_uses_explicit_override_for_input_output_without_executing_sql()
    {
        const string manifest = """
                                {
                                  "version": 2,
                                  "namespace": "Consumer.Contracts",
                                  "tableTypes": [],
                                  "procedures": [{
                                    "schema": "dbo",
                                    "name": "ApplyDiscount",
                                    "clrName": "ApplyDiscountProcedure",
                                    "parametersClrName": "ApplyDiscountParameters",
                                    "parameters": [
                                      { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false },
                                      { "ordinal": 1, "name": "discount", "sqlType": "decimal(18,2)", "clrType": "decimal", "direction": "Output", "nullable": false, "precision": 18, "scale": 2 }
                                    ],
                                    "resultSets": [{ "index": 0, "status": "None", "columns": [] }]
                                  }]
                                }
                                """;
        const string overrides = """
                                 {
                                   "version": 2,
                                   "procedures": [{
                                     "schema": "dbo",
                                     "name": "ApplyDiscount",
                                     "parameters": [{ "ordinal": 1, "direction": "InputOutput" }],
                                     "resultSets": []
                                   }]
                                 }
                                 """;

        var compilation = CreateCompilation("namespace Consumer; public static class EntryPoint { }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AutoContractsSourceGenerator().AsSourceGenerator()],
            [
                new TextFile("caerius.contracts.json", manifest),
                new TextFile("caerius.contracts.overrides.json", overrides)
            ],
            new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(updated);
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
        Assert.Contains("public sealed partial record ApplyDiscountParameters(decimal discount)", generated,
            StringComparison.Ordinal);
        Assert.Contains("parameters.discount, SqlDbType.Decimal, ParameterDirection.InputOutput", generated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContracts_v2_recalculates_hashes_and_reports_invalid_inputs()
    {
        const string validManifest = """
                                     {
                                       "version": 2,
                                       "namespace": "Consumer.Contracts",
                                       "tableTypes": [],
                                       "procedures": [{
                                         "schema": "dbo",
                                         "name": "Search",
                                         "clrName": "SearchProcedure",
                                         "parametersClrName": "SearchParameters",
                                         "parameters": [{ "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false }],
                                         "resultSets": [{
                                           "index": 0,
                                           "clrName": "SearchResult",
                                           "status": "Available",
                                           "columns": [{ "ordinal": 1, "name": "Name", "sqlType": "nvarchar(64)", "clrType": "string", "nullable": false, "maxLength": 128 }],
                                           "contractHash": "sha256:untrusted"
                                         }],
                                         "contractHash": "sha256:untrusted"
                                       }]
                                     }
                                     """;
        var changedManifest =
            validManifest.Replace("\"maxLength\": 128", "\"maxLength\": 256", StringComparison.Ordinal);

        var first = RunAutoContracts(validManifest);
        var second = RunAutoContracts(changedManifest);
        Assert.Empty(first.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(second.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var firstSource = Assert.Single(first.GeneratedTrees).ToString();
        var secondSource = Assert.Single(second.GeneratedTrees).ToString();
        Assert.DoesNotContain("sha256:untrusted", firstSource, StringComparison.Ordinal);
        Assert.NotEqual(firstSource, secondSource);

        var malformed = RunAutoContracts("{ not-json");
        Assert.Contains(malformed.Diagnostics, diagnostic => diagnostic.Id == "CAERIUSG001");
        Assert.Empty(malformed.GeneratedTrees);

        const string unknownOverride = """
                                       { "version": 2, "procedures": [{ "schema": "dbo", "name": "Unknown", "parameters": [], "resultSets": [] }] }
                                       """;
        var unknown = RunAutoContracts(validManifest, unknownOverride);
        Assert.Contains(unknown.Diagnostics, diagnostic => diagnostic.Id == "CAERIUSG003" &&
                                                           diagnostic.GetMessage().Contains("unknown procedure",
                                                               StringComparison.OrdinalIgnoreCase));
        Assert.Empty(unknown.GeneratedTrees);
    }

    [Fact]
    public void AutoContracts_v2_tvp_metadata_is_immutable_and_reuses_record_metadata()
    {
        const string manifest = """
                                {
                                  "version": 2,
                                  "namespace": "Consumer.Contracts",
                                  "tableTypes": [{
                                    "schema": "dbo",
                                    "name": "Ids",
                                    "clrName": "IdsTvp",
                                    "columns": [{ "ordinal": 1, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }]
                                  }],
                                  "procedures": []
                                }
                                """;

        var result = RunAutoContracts(manifest);
        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees).ToString();
        Assert.Contains("ImmutableArray<SqlMetaData> Metadata => _metadata", generated, StringComparison.Ordinal);
        Assert.Contains("private static readonly SqlMetaData[] _recordMetadata = _metadata.ToArray()", generated,
            StringComparison.Ordinal);
        Assert.Contains("CreateRecord() => new(_recordMetadata)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoContracts_v2_nullable_value_result_codec_compiles_without_nullable_value_warnings()
    {
        const string manifest = """
                                {
                                  "version": 2,
                                  "namespace": "Consumer.Contracts",
                                  "tableTypes": [],
                                  "procedures": [{
                                    "schema": "dbo",
                                    "name": "GetOptionalCount",
                                    "clrName": "GetOptionalCountProcedure",
                                    "parametersClrName": "GetOptionalCountParameters",
                                    "parameters": [{ "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false }],
                                    "resultSets": [{
                                      "index": 0,
                                      "clrName": "GetOptionalCountResult",
                                      "status": "Available",
                                      "columns": [{ "ordinal": 1, "name": "Count", "sqlType": "int", "clrType": "int", "nullable": true }]
                                    }]
                                  }]
                                }
                                """;

        var compilation = CreateCompilation("namespace Consumer; public static class EntryPoint { }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AutoContractsSourceGenerator().AsSourceGenerator()],
            [new TextFile("caerius.contracts.json", manifest)],
            new CSharpParseOptions(LanguageVersion.CSharp14));
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var updated, out var diagnostics, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertNoCompilationErrors(updated);
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
        Assert.Contains("(int?)null : binary.ReadInt32()", generated, StringComparison.Ordinal);
        Assert.Contains("value.Count is { } item0", generated, StringComparison.Ordinal);
    }

    private static GeneratorDriverRunResult RunAutoContracts(string manifest, string? overrides = null)
    {
        var compilation = CreateCompilation("namespace Consumer; public static class EntryPoint { }");
        var files = overrides is null
            ? new AdditionalText[] { new TextFile("caerius.contracts.json", manifest) }
            :
            [
                new TextFile("caerius.contracts.json", manifest),
                new TextFile("caerius.contracts.overrides.json", overrides)
            ];
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new AutoContractsSourceGenerator().AsSourceGenerator()],
            files,
            new CSharpParseOptions(LanguageVersion.CSharp14));
        return driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat([
                typeof(GenerateDtoAttribute).Assembly.Location,
                typeof(SqlDataReader).Assembly.Location,
                typeof(IServiceCollection).Assembly.Location
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "GeneratorConsumer",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }

    private sealed class TextFile(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(content);
        }
    }
}
