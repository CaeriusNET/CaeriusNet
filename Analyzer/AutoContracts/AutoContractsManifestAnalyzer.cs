using System.Collections.Generic;
using System.IO;

namespace CaeriusNet.Analyzer.AutoContracts;

/// <summary>
///     Reports user-facing diagnostics for the SQL Server contracts manifest consumed by AutoContracts generation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AutoContractsManifestAnalyzer : DiagnosticAnalyzer
{
    private const string ManifestFileName = "caerius.contracts.json";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.ContractManifestMissing,
        DiagnosticDescriptors.ContractProcedureMissing,
        DiagnosticDescriptors.ContractTableTypeMissing,
        DiagnosticDescriptors.ContractUnsupportedTableTypeSqlType,
        DiagnosticDescriptors.ContractUndeterminedResultSet,
        DiagnosticDescriptors.ContractNoResultSet,
        DiagnosticDescriptors.ContractOutputParameterUnsupported,
        DiagnosticDescriptors.ContractUnsupportedSqlType,
        DiagnosticDescriptors.ContractNullableResultColumn,
        DiagnosticDescriptors.ContractHashMismatch,
        DiagnosticDescriptors.ContractPossibleIncompatibility
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var manifestFound = false;
        foreach (var additionalFile in context.Options.AdditionalFiles)
        {
            if (!string.Equals(
                    Path.GetFileName(additionalFile.Path),
                    ManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            manifestFound = true;
            AnalyzeManifest(context, additionalFile);
        }

        if (!manifestFound && RequiresManifest(context))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                Location.None,
                ManifestFileName));
    }

    private static bool RequiresManifest(CompilationAnalysisContext context)
    {
        if (!context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.CaeriusContractsMode",
                out var mode))
            return false;

        return mode.Equals("Pull", StringComparison.OrdinalIgnoreCase) ||
               mode.Equals("Verify", StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeManifest(CompilationAnalysisContext context, AdditionalText manifestFile)
    {
        var sourceText = manifestFile.GetText(context.CancellationToken);
        var source = sourceText?.ToString();
        if (string.IsNullOrWhiteSpace(source))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                Location.None,
                ManifestFileName));
            return;
        }

        AutoContractsManifest manifest;
        try
        {
            manifest = AutoContractsManifestParser.Parse(source!);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                Location.None,
                ManifestFileName,
                ex.Message));
            return;
        }

        ValidateManifest(context, manifest);
    }

    private static void ValidateManifest(CompilationAnalysisContext context, AutoContractsManifest manifest)
    {
        var tableTypes = BuildTableTypeLookup(manifest);

        foreach (var tableType in manifest.TableTypes.Items)
            ValidateTableType(context, tableType);

        foreach (var procedure in manifest.Procedures.Items)
            ValidateProcedure(context, procedure, tableTypes);
    }

    private static HashSet<string> BuildTableTypeLookup(AutoContractsManifest manifest)
    {
        var tableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableType in manifest.TableTypes.Items)
            tableTypes.Add(BuildFullName(tableType.Schema, tableType.Name));

        return tableTypes;
    }

    private static void ValidateTableType(CompilationAnalysisContext context, AutoContractsTableType tableType)
    {
        foreach (var column in tableType.Columns.Items)
        {
            if (AutoContractsSqlTypes.IsSupported(column.SqlType))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedTableTypeSqlType,
                Location.None,
                BuildFullName(tableType.Schema, tableType.Name, column.Name),
                column.SqlType));
        }
    }

    private static void ValidateProcedure(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        HashSet<string> tableTypes)
    {
        foreach (var parameter in procedure.Parameters.Items)
            ValidateParameter(context, procedure, parameter, tableTypes);

        foreach (var column in procedure.ResultSet.Columns.Items)
            ValidateResultColumn(context, procedure, column);

        ValidateResultSetStatus(context, procedure);
    }

    private static void ValidateParameter(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        AutoContractsParameter parameter,
        HashSet<string> tableTypes)
    {
        if (parameter.IsOutput)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractOutputParameterUnsupported,
                Location.None,
                BuildFullName(procedure.Schema, procedure.Name),
                parameter.Name));

        if (parameter.IsTableType)
        {
            if (!tableTypes.Contains(parameter.SqlType))
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractTableTypeMissing,
                    Location.None,
                    parameter.SqlType));

            return;
        }

        if (!AutoContractsSqlTypes.IsSupported(parameter.SqlType))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedSqlType,
                Location.None,
                parameter.SqlType));
    }

    private static void ValidateResultColumn(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        AutoContractsColumn column)
    {
        if (!AutoContractsSqlTypes.IsSupported(column.SqlType))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedSqlType,
                Location.None,
                column.SqlType));

        if (column.Nullable)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractNullableResultColumn,
                Location.None,
                BuildFullName(procedure.Schema, procedure.Name, column.Name)));
    }

    private static void ValidateResultSetStatus(CompilationAnalysisContext context, AutoContractsProcedure procedure)
    {
        if (string.Equals(procedure.ResultSet.Status, "Undetermined", StringComparison.OrdinalIgnoreCase))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUndeterminedResultSet,
                Location.None,
                BuildFullName(procedure.Schema, procedure.Name)));
            return;
        }

        if (string.Equals(procedure.ResultSet.Status, "None", StringComparison.OrdinalIgnoreCase))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractNoResultSet,
                Location.None,
                BuildFullName(procedure.Schema, procedure.Name)));
    }

    private static string BuildFullName(string schema, string name)
    {
        return schema + "." + name;
    }

    private static string BuildFullName(string schema, string name, string member)
    {
        return schema + "." + name + "." + member;
    }
}
