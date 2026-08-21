using CaeriusNet.Generator.AutoContracts;
using CaeriusNet.Generator.Dto;
using CaeriusNet.Generator.Tvp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CaeriusNet.Benchmark.Generator;

/// <summary>
///     Measures the Roslyn host path used by a consumer project that combines DTO, TVP and AutoContracts generation.
///     The cold case creates a new driver, while the incremental case reuses the driver over an unchanged compilation.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("generator")]
public class GeneratorDriverBenchmarks
{
    private const string ConsumerSource = """
                                          using System;
                                          using System.Data;
                                          using CaeriusNet;

                                          namespace BenchmarkConsumer.Models;

                                          [GenerateDto]
                                          public sealed partial record CustomerDashboard(
                                              int CustomerId,
                                              string DisplayName,
                                              decimal OutstandingBalance,
                                              DateOnly RegisteredOn,
                                              Guid Version,
                                              string? Region);

                                          [GenerateTvp(Schema = "sales", TvpName = "OrderLineType")]
                                          public sealed partial record OrderLine(
                                              [SqlServerTvpColumn("OrderId", 0, SqlDbType.Int, false)] int OrderId,
                                              [SqlServerTvpColumn("LineNumber", 1, SqlDbType.Int, false)] int LineNumber,
                                              [SqlServerTvpColumn(
                                                  "StockKeepingUnit",
                                                  2,
                                                  SqlDbType.NVarChar,
                                                  false,
                                                  MaxLength = 128,
                                                  LocaleId = 1033,
                                                  CompareOptions = global::System.Data.SqlTypes.SqlCompareOptions.None)] string StockKeepingUnit,
                                              [SqlServerTvpColumn("Quantity", 3, SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal Quantity,
                                              [SqlServerTvpColumn("UnitPrice", 4, SqlDbType.Decimal, false, Precision = 18, Scale = 4)] decimal UnitPrice,
                                              [SqlServerTvpColumn("RequestedAt", 5, SqlDbType.DateTime2, false, Scale = 7)] DateTime RequestedAt);
                                          """;

    // This is intentionally representative rather than minimal: it has a TVP, an output/return-value command,
    // nullable/faceted columns and a two-result procedure, which exercises the generator's merge and emission paths.
    private const string Manifest = """
                                    {
                                      "version": 3,
                                      "namespace": "BenchmarkConsumer.Contracts",
                                      "tableTypes": [
                                        {
                                          "schema": "sales",
                                          "name": "OrderLineType",
                                          "clrName": "SalesOrderLineTypeTvp",
                                          "columns": [
                                            { "ordinal": 0, "name": "OrderId", "sqlType": "int", "clrType": "int", "nullable": false },
                                            { "ordinal": 1, "name": "LineNumber", "sqlType": "int", "clrType": "int", "nullable": false },
                                            { "ordinal": 2, "name": "StockKeepingUnit", "sqlType": "nvarchar(64)", "clrType": "string", "nullable": false, "maxLength": 128 },
                                            { "ordinal": 3, "name": "Quantity", "sqlType": "decimal(18,4)", "clrType": "decimal", "nullable": false, "precision": 18, "scale": 4 },
                                            { "ordinal": 4, "name": "RequestedAt", "sqlType": "datetime2(7)", "clrType": "DateTime", "nullable": false, "scale": 7 }
                                          ]
                                        }
                                      ],
                                      "procedures": [
                                        {
                                          "schema": "sales",
                                          "name": "GetCustomerDashboard",
                                          "clrName": "SalesGetCustomerDashboardProcedure",
                                          "parametersClrName": "SalesGetCustomerDashboardParameters",
                                          "parameters": [
                                            { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false },
                                            { "ordinal": 1, "name": "customerId", "sqlType": "int", "clrType": "int", "direction": "Input", "nullable": false },
                                            { "ordinal": 2, "name": "asOf", "sqlType": "datetime2(7)", "clrType": "DateTime", "direction": "Input", "nullable": true, "scale": 7 }
                                          ],
                                          "resultSets": [
                                            {
                                              "index": 0,
                                              "clrName": "SalesCustomerDashboardRow",
                                              "status": "Available",
                                              "columns": [
                                                { "ordinal": 0, "name": "CustomerId", "sqlType": "int", "clrType": "int", "nullable": false },
                                                { "ordinal": 1, "name": "DisplayName", "sqlType": "nvarchar(128)", "clrType": "string", "nullable": false, "maxLength": 256 },
                                                { "ordinal": 2, "name": "OutstandingBalance", "sqlType": "decimal(18,2)", "clrType": "decimal", "nullable": false, "precision": 18, "scale": 2 },
                                                { "ordinal": 3, "name": "Region", "sqlType": "nvarchar(32)", "clrType": "string", "nullable": true, "maxLength": 64 }
                                              ]
                                            },
                                            {
                                              "index": 1,
                                              "clrName": "SalesCustomerAlertRow",
                                              "status": "Available",
                                              "columns": [
                                                { "ordinal": 0, "name": "AlertCode", "sqlType": "nvarchar(24)", "clrType": "string", "nullable": false, "maxLength": 48 },
                                                { "ordinal": 1, "name": "CreatedAt", "sqlType": "datetimeoffset(7)", "clrType": "DateTimeOffset", "nullable": false, "scale": 7 }
                                              ]
                                            }
                                          ]
                                        },
                                        {
                                          "schema": "sales",
                                          "name": "RefreshCustomerStatistics",
                                          "clrName": "SalesRefreshCustomerStatisticsProcedure",
                                          "parametersClrName": "SalesRefreshCustomerStatisticsParameters",
                                          "parameters": [
                                            { "ordinal": 0, "name": "returnValue", "sqlType": "int", "clrType": "int", "direction": "ReturnValue", "nullable": false },
                                            { "ordinal": 1, "name": "customerId", "sqlType": "int", "clrType": "int", "direction": "Input", "nullable": false },
                                            { "ordinal": 2, "name": "refreshedAt", "sqlType": "datetime2(7)", "clrType": "DateTime", "direction": "Output", "nullable": false, "scale": 7 }
                                          ],
                                          "resultSets": [ { "index": 0, "status": "None", "columns": [] } ]
                                        }
                                      ]
                                    }
                                    """;

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp14);

    private static readonly ImmutableArray<MetadataReference> MetadataReferences = CreateMetadataReferences();

    private CSharpCompilation _compilation = null!;
    private CSharpGeneratorDriver _incrementalDriver = null!;
    private CSharpCompilation _unchangedCompilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CSharpCompilation.Create(
            "CaeriusNetGeneratorBenchmarkConsumer",
            [CSharpSyntaxTree.ParseText(ConsumerSource, ParseOptions)],
            MetadataReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // A clone is how Roslyn hosts normally represent a project that was evaluated without a source edit.
        _unchangedCompilation = _compilation.WithOptions(_compilation.Options);
        _incrementalDriver = CreateDriver(false);
        _incrementalDriver = (CSharpGeneratorDriver)_incrementalDriver.RunGeneratorsAndUpdateCompilation(
            _compilation,
            out _,
            out _,
            CancellationToken.None);
    }

    [Benchmark(Baseline = true)]
    public Compilation ColdDriver()
    {
        var driver = CreateDriver(false);
        _ = driver.RunGeneratorsAndUpdateCompilation(
            _compilation,
            out var updatedCompilation,
            out _,
            CancellationToken.None);
        return updatedCompilation;
    }

    [Benchmark]
    public Compilation IncrementalDriverWithoutSourceChanges()
    {
        _incrementalDriver = (CSharpGeneratorDriver)_incrementalDriver.RunGeneratorsAndUpdateCompilation(
            _unchangedCompilation,
            out var updatedCompilation,
            out _,
            CancellationToken.None);
        return updatedCompilation;
    }

    private static CSharpGeneratorDriver CreateDriver(bool trackIncrementalSteps)
    {
        return CSharpGeneratorDriver.Create(
            [
                new DtoSourceGenerator().AsSourceGenerator(),
                new TvpSourceGenerator().AsSourceGenerator(),
                new AutoContractsSourceGenerator().AsSourceGenerator()
            ],
            [new StringAdditionalText("caerius.contracts.json", Manifest)],
            ParseOptions,
            null,
            new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalSteps));
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
                                        ?? throw new InvalidOperationException(
                                            "The .NET trusted platform assembly list is unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(typeof(GenerateDtoAttribute).Assembly.Location)
            .Append(typeof(SqlDataReader).Assembly.Location)
            .Append(typeof(IServiceCollection).Assembly.Location)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private sealed class StringAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(content, Encoding.UTF8);
        }
    }
}
