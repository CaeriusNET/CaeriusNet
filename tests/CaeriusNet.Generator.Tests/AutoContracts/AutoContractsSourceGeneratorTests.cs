namespace CaeriusNet.Generator.Tests.AutoContracts;

/// <summary>
///     Tests the public AutoContracts v2 source-generator boundary.  Deep emission, output and multi-result
///     contracts live in <c>ContractGeneration</c>; this suite pins manifest discovery and v2-only diagnostics.
/// </summary>
public sealed class AutoContractsSourceGeneratorTests
{
    private const string ValidManifest = """
                                         {
                                           "version": 2,
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
                                                       "ordinal": 1,
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
    public void V2Manifest_Generates_Procedure_And_Result_Without_PerProcedure_ShaEmitter()
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
    public void V2Manifest_WithExplicitAdditionalFileMetadata_Is_Consumed()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest, "contracts/generated.json", true));

        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void InvalidV2Manifest_Reports_A_SecretFree_GeneratorDiagnostic()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("{ not-json"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CAERIUSG001", diagnostic.Id);
        Assert.DoesNotContain("connection", diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
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
