namespace CaeriusNet.Analyzer.Tests;

public sealed class AutoContractsManifestAnalyzerTests
{
    private const string ValidEmptyManifest = """
                                              {
                                                "version": 1,
                                                "namespace": "Consumer.Contracts",
                                                "tableTypes": [],
                                                "procedures": []
                                              }
                                              """;

    [Fact]
    public void EmptyManifest_Reports_CAERIUS200()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest(""));

        AssertDiagnostic(diagnostics, "CAERIUS200", DiagnosticSeverity.Error);
    }

    [Fact]
    public void CustomManifestPath_WithMetadata_Is_Consumed_And_Satisfies_Pull_Mode()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.CaeriusContractsMode"] = " Pull "
            },
            Manifest(ValidEmptyManifest, "generated.contracts.json", true));

        Assert.DoesNotContain(diagnostics,
            diagnostic => diagnostic.Id.StartsWith("CAERIUS2", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomManifestPath_WithoutMetadata_Is_Ignored()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.CaeriusContractsMode"] = "Pull"
            },
            Manifest(ValidEmptyManifest, "generated.contracts.json"));

        AssertDiagnostic(diagnostics, "CAERIUS200", DiagnosticSeverity.Error);
    }

    [Fact]
    public void PullModeWithoutManifest_Reports_CAERIUS200()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.CaeriusContractsMode"] = "Pull"
            });

        AssertDiagnostic(diagnostics, "CAERIUS200", DiagnosticSeverity.Error);
    }

    [Fact]
    public void UndeterminedResultSet_Reports_CAERIUS204()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "SomeProcedure",
                           "clrName": "SomeProcedure",
                           "parametersClrName": "SomeProcedureParameters",
                           "resultClrName": "SomeProcedureResult",
                           "parameters": [],
                           "resultSet": { "status": "Undetermined", "columns": [] },
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS204", DiagnosticSeverity.Error);
    }

    [Fact]
    public void InvalidManifest_Reports_CAERIUS210_FromAnalyzer()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("{"));

        AssertDiagnostic(diagnostics, "CAERIUS210", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void EmptyJsonObject_Reports_CAERIUS210_FromAnalyzer()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("{}"));

        AssertDiagnostic(diagnostics, "CAERIUS210", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void UnsupportedManifestVersion_Reports_CAERIUS210()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 2,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": []
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS210", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void NoResultSet_Reports_CAERIUS205()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "NoRows",
                           "clrName": "NoRowsProcedure",
                           "parametersClrName": "NoRowsParameters",
                           "parameters": [],
                           "resultSet": { "status": "None", "columns": [] },
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS205", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ContractShapeProblems_ReportExpectedDiagnostics()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "InputRows",
                           "clrName": "InputRowsTvp",
                           "columns": [
                             { "ordinal": 1, "name": "Payload", "sqlType": "xml", "clrType": "string", "nullable": false }
                           ],
                           "contractHash": "sha256:table"
                         }
                       ],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "Search",
                           "clrName": "SearchProcedure",
                           "parametersClrName": "SearchParameters",
                           "resultClrName": "SearchResult",
                           "parameters": [
                             { "ordinal": 1, "name": "Rows", "sqlType": "dbo.MissingRows", "clrType": "ReadOnlyMemory<InputRowsTvp>", "isTableType": true },
                             { "ordinal": 2, "name": "Legacy", "sqlType": "sql_variant", "clrType": "object", "isTableType": false },
                             { "ordinal": 3, "name": "OutId", "sqlType": "int", "clrType": "int", "isOutput": true }
                           ],
                           "resultSet": {
                             "status": "Available",
                             "columns": [
                               { "ordinal": 1, "name": "UserId", "sqlType": "int", "clrType": "int", "nullable": true },
                               { "ordinal": 2, "name": "Legacy", "sqlType": "cursor", "clrType": "object", "nullable": false }
                             ]
                           },
                           "contractHash": "sha256:procedure"
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS202", DiagnosticSeverity.Error);
        AssertDiagnostic(diagnostics, "CAERIUS203", DiagnosticSeverity.Error);
        AssertDiagnostic(diagnostics, "CAERIUS206", DiagnosticSeverity.Error);
        AssertDiagnostic(diagnostics, "CAERIUS207", DiagnosticSeverity.Error);
        AssertDiagnostic(diagnostics, "CAERIUS208", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ManifestDiagnostics_Are_Reported_On_AdditionalFile_Location()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "InputRows",
                           "clrName": "InputRowsTvp",
                           "columns": [
                             { "ordinal": 1, "name": "Payload", "sqlType": "xml", "clrType": "string", "nullable": false }
                           ],
                           "contractHash": "sha256:table"
                         }
                       ],
                       "procedures": []
                     }
                     """, "contracts/custom.contracts.json", true));

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "CAERIUS203");
        Assert.NotEqual(Location.None, diagnostic.Location);
        Assert.Equal("contracts/custom.contracts.json", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void NoManifestAdditionalFile_Reports_NoAutoContractsDiagnostics()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer("namespace Consumer;");

        Assert.DoesNotContain(diagnostics,
            diagnostic => diagnostic.Id.StartsWith("CAERIUS2", StringComparison.Ordinal));
    }

    private static TestAdditionalText Manifest(
        string json,
        string path = "caerius.contracts.json",
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

    private static void AssertDiagnostic(
        IEnumerable<Diagnostic> diagnostics,
        string id,
        DiagnosticSeverity severity)
    {
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Id == id && diagnostic.Severity == severity);
    }
}
