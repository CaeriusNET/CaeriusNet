namespace CaeriusNet.Analyzer.Tests;

public sealed class AutoContractsManifestAnalyzerTests
{
    private const string SqlServer2025Manifest = """
                                                 {
                                                   "version": 3,
                                                   "serverMajor": 17,
                                                   "features": ["SqlServer2025Types"],
                                                   "namespace": "Consumer.Contracts",
                                                   "tableTypes": [],
                                                   "procedures": [{
                                                     "schema": "dbo",
                                                     "name": "SearchEmbeddings",
                                                     "clrName": "SearchEmbeddingsProcedure",
                                                     "parametersClrName": "SearchEmbeddingsParameters",
                                                     "parameters": [
                                                       { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue" },
                                                       { "ordinal": 1, "name": "filter", "sqlType": "json", "clrType": "string", "direction": "Input" }
                                                     ],
                                                     "resultSets": [{
                                                       "index": 0,
                                                       "clrName": "SearchEmbeddingsResult",
                                                       "status": "Available",
                                                       "columns": [
                                                         { "ordinal": 0, "name": "Payload", "sqlType": "json", "clrType": "string", "nullable": false },
                                                         { "ordinal": 1, "name": "Embedding", "sqlType": "vector(3)", "clrType": "global::Microsoft.Data.SqlTypes.SqlVector<float>", "nullable": false }
                                                       ]
                                                     }]
                                                   }]
                                                 }
                                                 """;

    private const string ValidEmptyManifest = """
                                              {
                                                "version": 3,
                                                "serverMajor": 16,
                                                "features": [],
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
    public void V2Manifest_WithUndeterminedFirstResultSet_Reports_CAERIUS204()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "SomeProcedure",
                           "clrName": "SomeProcedure",
                           "parametersClrName": "SomeProcedureParameters",
                           "parameters": [],
                           "resultSets": [
                             { "index": 0, "status": "Undetermined", "columns": [] }
                           ],
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
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": []
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS210", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void V2Manifest_WithNoFirstResultSet_Reports_CAERIUS205()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "NoRows",
                           "clrName": "NoRowsProcedure",
                           "parametersClrName": "NoRowsParameters",
                           "parameters": [],
                           "resultSets": [
                             { "index": 0, "status": "None", "columns": [] }
                           ],
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS205", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void V2Manifest_AllowsOutputInputOutputAndReturnValueParameters()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "SaveOrder",
                           "clrName": "SaveOrderProcedure",
                           "parametersClrName": "SaveOrderParameters",
                           "parameters": [
                             { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "ReturnValue" },
                             { "ordinal": 1, "name": "orderId", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "Input" },
                             { "ordinal": 2, "name": "version", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "InputOutput" },
                             { "ordinal": 3, "name": "createdId", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "Output" }
                           ],
                           "resultSets": [
                             {
                               "index": 0,
                               "clrName": "SaveOrderResult",
                               "status": "Available",
                               "columns": [
                                 { "ordinal": 0, "name": "OrderId", "sqlType": "int", "clrType": "int", "nullable": false }
                               ]
                             }
                           ],
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "CAERIUS206" or "CAERIUS210");
    }

    [Fact]
    public void V2Manifest_InvalidParameterDirection_Reports_CAERIUS206()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "Broken",
                           "clrName": "BrokenProcedure",
                           "parametersClrName": "BrokenParameters",
                           "parameters": [
                             { "ordinal": 0, "name": "mode", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "ReadOnly" }
                           ],
                           "resultSets": [
                             { "index": 0, "status": "None", "columns": [] }
                           ]
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS206", DiagnosticSeverity.Error);
    }

    [Fact]
    public void V2Manifest_OutputTableType_Reports_CAERIUS206()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "Rows",
                           "clrName": "RowsTvp",
                           "columns": [
                             { "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }
                           ]
                         }
                       ],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "Broken",
                           "clrName": "BrokenProcedure",
                           "parametersClrName": "BrokenParameters",
                           "parameters": [
                             { "ordinal": 0, "name": "rows", "sqlType": "dbo.Rows", "clrType": "IReadOnlyList<RowsTvp>", "isTableType": true, "direction": "Output" }
                           ],
                           "resultSets": [
                             { "index": 0, "status": "None", "columns": [] }
                           ]
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS206", DiagnosticSeverity.Error);
    }

    [Fact]
    public void V2Manifest_NonContinuousOrDuplicateResultSetIndices_Report_CAERIUS210()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "Search",
                           "clrName": "SearchProcedure",
                           "parametersClrName": "SearchParameters",
                           "parameters": [],
                           "resultSets": [
                             { "index": 0, "clrName": "SearchHeader", "status": "Available", "columns": [{ "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }] },
                             { "index": 2, "clrName": "SearchDetail", "status": "Available", "columns": [{ "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }] },
                             { "index": 2, "clrName": "SearchDuplicate", "status": "Available", "columns": [{ "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }] }
                           ]
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS210", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void V2Overrides_CompleteTheResultSetSequence()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "Search",
                           "clrName": "SearchProcedure",
                           "parametersClrName": "SearchParameters",
                           "parameters": [],
                           "resultSets": [
                             { "index": 0, "clrName": "SearchHeader", "status": "Available", "columns": [{ "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }] }
                           ]
                         }
                       ]
                     }
                     """),
            Overrides("""
                      {
                        "version": 3,
                        "procedures": [
                          {
                            "schema": "dbo",
                            "name": "Search",
                            "resultSets": [
                              { "index": 1, "clrName": "SearchDetail", "status": "Available", "columns": [{ "ordinal": 0, "name": "Id", "sqlType": "int", "clrType": "int", "nullable": false }] }
                            ]
                          }
                        ]
                      }
                      """));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CAERIUS210");
    }

    [Fact]
    public void ContractShapeProblems_ReportExpectedDiagnostics()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "InputRows",
                           "clrName": "InputRowsTvp",
                           "columns": [
                             { "ordinal": 0, "name": "Payload", "sqlType": "xml", "clrType": "string", "nullable": false }
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
                           "parameters": [
                             { "ordinal": 1, "name": "Rows", "sqlType": "dbo.MissingRows", "clrType": "ReadOnlyMemory<InputRowsTvp>", "isTableType": true, "direction": "Input" },
                             { "ordinal": 2, "name": "Legacy", "sqlType": "sql_variant", "clrType": "object", "isTableType": false, "direction": "Input" },
                             { "ordinal": 3, "name": "OutId", "sqlType": "int", "clrType": "int", "isTableType": false, "direction": "Output" }
                           ],
                           "resultSets": [
                             {
                               "index": 0,
                               "clrName": "SearchResult",
                               "status": "Available",
                               "columns": [
                                 { "ordinal": 0, "name": "UserId", "sqlType": "int", "clrType": "int", "nullable": true },
                                 { "ordinal": 2, "name": "Legacy", "sqlType": "cursor", "clrType": "object", "nullable": false }
                               ]
                             }
                           ],
                           "contractHash": "sha256:procedure"
                         }
                       ]
                     }
                     """));

        AssertDiagnostic(diagnostics, "CAERIUS202", DiagnosticSeverity.Error);
        AssertDiagnostic(diagnostics, "CAERIUS203", DiagnosticSeverity.Error);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CAERIUS206");
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
                       "version": 3,
                       "serverMajor": 16,
                       "features": [],
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "InputRows",
                           "clrName": "InputRowsTvp",
                           "columns": [
                             { "ordinal": 0, "name": "Payload", "sqlType": "xml", "clrType": "string", "nullable": false }
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

    [Fact]
    public void SqlServer2025NativeTypes_WithoutOptIn_ReportManifestIncompatibility()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            Manifest(SqlServer2025Manifest));

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CAERIUS210" &&
                                                   diagnostic.GetMessage().Contains("CaeriusSqlServer2025Types=Enabled",
                                                       StringComparison.Ordinal));
    }

    [Fact]
    public void SqlServer2025NativeTypes_WithOptInAndServer17_AreNotReportedAsUnsupported()
    {
        var diagnostics = AnalyzerTestHelper.RunAnalyzer(
            "namespace Consumer;",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.CaeriusSqlServer2025Types"] = "Enabled"
            },
            Manifest(SqlServer2025Manifest));

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "CAERIUS207" or "CAERIUS210");
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

    private static TestAdditionalText Overrides(string json)
    {
        return new TestAdditionalText("caerius.contracts.overrides.json", json);
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
