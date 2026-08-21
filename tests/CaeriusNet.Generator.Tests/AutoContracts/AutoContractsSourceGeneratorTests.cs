using CaeriusNet.Analyzer.AutoContracts;

namespace CaeriusNet.Generator.Tests.AutoContracts;

/// <summary>
///     Tests the public AutoContracts v3 source-generator boundary. Deep emission, output and multi-result
///     contracts live in <c>ContractGeneration</c>; this suite pins manifest discovery and bounded diagnostics.
/// </summary>
public sealed class AutoContractsSourceGeneratorTests
{
    private const string SqlServer2025Manifest = """
                                                   {
                                                     "version": 3,
                                                   "serverMajor": 17,
                                                   "features": ["SqlServer2025Types"],
                                                   "namespace": "Consumer.Contracts",
                                                   "tableTypes": [],
                                                   "procedures": [
                                                     {
                                                       "schema": "dbo",
                                                       "name": "SearchEmbeddings",
                                                       "clrName": "SearchEmbeddingsProcedure",
                                                       "parametersClrName": "SearchEmbeddingsParameters",
                                                       "parameters": [
                                                         { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false },
                                                         { "ordinal": 1, "name": "filter", "sqlType": "json", "clrType": "string", "direction": "Input", "nullable": true },
                                                         { "ordinal": 2, "name": "embedding", "sqlType": "vector(3)", "clrType": "global::Microsoft.Data.SqlTypes.SqlVector<float>", "direction": "Output", "nullable": true }
                                                       ],
                                                       "resultSets": [
                                                         {
                                                           "index": 0,
                                                           "clrName": "SearchEmbeddingsResult",
                                                           "status": "Available",
                                                           "columns": [
                                                             { "ordinal": 0, "name": "Payload", "sqlType": "json", "clrType": "string", "nullable": false },
                                                             { "ordinal": 1, "name": "Embedding", "sqlType": "vector(3)", "clrType": "global::Microsoft.Data.SqlTypes.SqlVector<float>", "nullable": false }
                                                           ]
                                                         }
                                                       ]
                                                     }
                                                   ]
                                                 }
                                                 """;

    private const string ValidManifest = """
                                         {
                                           "version": 3,
                                           "serverMajor": 16,
                                           "features": [],
                                           "namespace": "Consumer.Contracts",
                                           "tableTypes": [],
                                           "procedures": [
                                             {
                                               "schema": "dbo",
                                               "name": "Lookup",
                                               "clrName": "LookupProcedure",
                                               "parametersClrName": "LookupParameters",
                                               "parameters": [],
                                               "resultSets": [
                                                 {
                                                   "index": 0,
                                                   "clrName": "LookupResult",
                                                   "status": "Available",
                                                   "columns": [
                                                     {
                                                       "ordinal": 0,
                                                       "name": "Id",
                                                       "sqlType": "int",
                                                       "clrType": "int",
                                                       "nullable": false
                                                     }
                                                   ]
                                                 }
                                               ]
                                             }
                                           ]
                                         }
                                         """;

    [Fact]
    public void V3Manifest_Generates_Procedure_And_Result_Without_PerProcedure_ShaEmitter()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest));

        var generated = Assert.Single(result.GeneratedTrees).GetText(TestContext.Current.CancellationToken).ToString();
        Assert.Contains("LookupProcedure", generated);
        Assert.Contains("LookupResult", generated);
        Assert.DoesNotContain("SHA256", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void V3Manifest_WithExplicitAdditionalFileMetadata_Is_Consumed()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest, "contracts/generated.json", true));

        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void InvalidV3Manifest_Reports_A_SecretFree_GeneratorDiagnostic()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("{ not-json"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG001", diagnostic.Id);
        Assert.DoesNotContain("connection", diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("3e0", true)]
    [InlineData("-3", true)]
    [InlineData("3.5", false)]
    [InlineData("3e-1", false)]
    [InlineData("1e999", false)]
    public void ManifestParser_AcceptsOnlyFiniteIntegerVersionValues(string version, bool isSuccess)
    {
        var manifest = new TestAdditionalText(
            "caerius.contracts.json",
            ValidManifest.Replace("\"version\": 3", $"\"version\": {version}", StringComparison.Ordinal));

        var parsed = AutoContractsManifestParser.ParseManifest(manifest, TestContext.Current.CancellationToken);

        Assert.Equal(isSuccess, parsed.IsSuccess);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ManifestParser_RejectsNonJsonSpecialFloatingPointVersionValues(string version)
    {
        var manifest = new TestAdditionalText(
            "caerius.contracts.json",
            ValidManifest.Replace("\"version\": 3", $"\"version\": {version}", StringComparison.Ordinal));

        var parsed = AutoContractsManifestParser.ParseManifest(manifest, TestContext.Current.CancellationToken);

        Assert.False(parsed.IsSuccess);
    }

    [Fact]
    public void V2Manifest_IsRejectedWithoutEmittingSource()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest.Replace("\"version\": 3", "\"version\": 2", StringComparison.Ordinal)));

        Assert.Empty(result.GeneratedTrees);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG003", diagnostic.Id);
        Assert.Contains("version 3", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleManifests_AreRejectedWithoutEmittingSource()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest),
            Manifest(ValidManifest, "contracts/second.json", true));

        Assert.Empty(result.GeneratedTrees);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG003", diagnostic.Id);
        Assert.Contains("Only one", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AdditionalFileOverEightMiB_IsRejectedBeforeParsing()
    {
        var oversized = ValidManifest + new string(' ', 8 * 1024 * 1024);
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(oversized));

        Assert.Empty(result.GeneratedTrees);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG001", diagnostic.Id);
        Assert.Contains("8 MiB", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonNestingBeyondSixtyFour_IsRejectedBeforeEmission()
    {
        var nested = new string('[', 65) + "0" + new string(']', 65);
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(nested));

        Assert.Empty(result.GeneratedTrees);
        Assert.Equal("CAERIUSG001", Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void V3GeneratedSourceCompilesWithoutErrors()
    {
        var (runResult, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<AutoContractsSourceGenerator>(
                "namespace Consumer;",
                Manifest(ValidManifest));

        Assert.Single(runResult.GeneratedTrees);
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SqlServer2025NativeTypes_WithoutExplicitOptIn_ReportBlockingDiagnosticAndEmitNothing()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(SqlServer2025Manifest));

        Assert.Empty(result.GeneratedTrees);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG003", diagnostic.Id);
        Assert.Contains("CaeriusSqlServer2025Types=Enabled", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer2025NativeTypes_RequireAServerMajor17ManifestEvenWhenEnabled()
    {
        var server16Manifest = SqlServer2025Manifest
            .Replace("\"serverMajor\": 17", "\"serverMajor\": 16", StringComparison.Ordinal)
            .Replace("\"features\": [\"SqlServer2025Types\"]", "\"features\": []", StringComparison.Ordinal);
        var (runResult, _) = SourceGeneratorTestHelper.RunGeneratorWithCompilation<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            SqlServer2025Enabled(),
            Manifest(server16Manifest));

        Assert.Empty(runResult.GeneratedTrees);
        var diagnostic = Assert.Single(runResult.Diagnostics);
        Assert.Equal("CAERIUSG003", diagnostic.Id);
        Assert.Contains("server-major-17", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer2025NativeJsonAndVector_GenerateAndCompile_WhenOptedInOnServer17()
    {
        var (runResult, compilation) =
            SourceGeneratorTestHelper.RunGeneratorWithCompilation<AutoContractsSourceGenerator>(
                "namespace Consumer;",
                SqlServer2025Enabled(),
                Manifest(SqlServer2025Manifest));

        var generated = Assert.Single(runResult.GeneratedTrees).GetText(TestContext.Current.CancellationToken)
            .ToString();
        Assert.Contains("SqlDbType.Json", generated, StringComparison.Ordinal);
        Assert.Contains("SqlDbType.Vector", generated, StringComparison.Ordinal);
        Assert.Contains("reader.GetSqlVector<float>(1)", generated, StringComparison.Ordinal);
        Assert.Contains("size: 3", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SqlServer2025NativeTypes_AreNeverAllowedInTvps()
    {
        var tvpManifest = SqlServer2025Manifest.Replace(
            "\"tableTypes\": []",
            "\"tableTypes\": [{ \"schema\": \"dbo\", \"name\": \"Vectors\", \"clrName\": \"VectorsTvp\", \"columns\": [{ \"ordinal\": 0, \"name\": \"Value\", \"sqlType\": \"vector(3)\", \"clrType\": \"global::Microsoft.Data.SqlTypes.SqlVector<float>\", \"nullable\": false }] }]",
            StringComparison.Ordinal);
        var (result, _) = SourceGeneratorTestHelper.RunGeneratorWithCompilation<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            SqlServer2025Enabled(),
            Manifest(tvpManifest));

        Assert.Empty(result.GeneratedTrees);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG003", diagnostic.Id);
        Assert.Contains("not supported in TVPs", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> SqlServer2025Enabled()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.CaeriusSqlServer2025Types"] = "Enabled"
        };
    }

    private static TestAdditionalText Manifest(string json, string path = "caerius.contracts.json",
        bool? isManifest = null)
    {
        var options = isManifest.HasValue
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_metadata.AdditionalFiles.CaeriusContractManifest"] =
                    isManifest.Value ? "true" : "false"
            }
            : null;

        return new TestAdditionalText(path, json, options);
    }
}
