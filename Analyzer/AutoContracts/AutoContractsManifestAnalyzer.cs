using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace CaeriusNet.Analyzer.AutoContracts;

/// <summary>
///     Reports user-facing diagnostics for the SQL Server contracts manifest consumed by AutoContracts generation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AutoContractsManifestAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.ContractManifestMissing,
        DiagnosticDescriptors.ContractTableTypeMissing,
        DiagnosticDescriptors.ContractUnsupportedTableTypeSqlType,
        DiagnosticDescriptors.ContractUndeterminedResultSet,
        DiagnosticDescriptors.ContractNoResultSet,
        DiagnosticDescriptors.ContractOutputParameterUnsupported,
        DiagnosticDescriptors.ContractUnsupportedSqlType,
        DiagnosticDescriptors.ContractNullableResultColumn,
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
            if (!AutoContractsManifestFile.IsManifest(
                    additionalFile,
                    context.Options.AnalyzerConfigOptionsProvider))
                continue;

            manifestFound = true;
            AnalyzeManifest(context, additionalFile);
        }

        if (!manifestFound && RequiresManifest(context))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                Location.None,
                AutoContractsManifestFile.ManifestFileName));
    }

    private static bool RequiresManifest(CompilationAnalysisContext context)
    {
        if (!context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.CaeriusContractsMode",
                out var mode))
            return false;

        mode = mode.Trim();
        return mode.Equals("Pull", StringComparison.OrdinalIgnoreCase) ||
               mode.Equals("Verify", StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeManifest(CompilationAnalysisContext context, AdditionalText manifestFile)
    {
        var sourceText = manifestFile.GetText(context.CancellationToken);
        var source = sourceText?.ToString();
        var location = CreateManifestLocation(manifestFile, sourceText);
        if (string.IsNullOrWhiteSpace(source))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                location,
                AutoContractsManifestFile.ManifestFileName));
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
                location,
                manifestFile.Path,
                ex.Message));
            return;
        }

        ValidateManifest(context, manifest, manifestFile.Path, location);
    }

    private static void ValidateManifest(
        CompilationAnalysisContext context,
        AutoContractsManifest manifest,
        string manifestPath,
        Location location)
    {
        if (manifest.Version != AutoContractsManifestParser.SupportedManifestVersion)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                location,
                manifestPath,
                $"Unsupported manifest version '{manifest.Version}'. Expected version '{AutoContractsManifestParser.SupportedManifestVersion}'."));

        if (!IsValidNamespace(manifest.Namespace))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                location,
                manifestPath,
                $"Manifest namespace '{manifest.Namespace}' is not a valid C# namespace."));

        var tableTypes = BuildTableTypeLookup(manifest);
        ValidateGenerationNames(context, manifest, manifestPath, location);

        foreach (var tableType in manifest.TableTypes.Items)
            ValidateTableType(context, tableType, location);

        foreach (var procedure in manifest.Procedures.Items)
            ValidateProcedure(context, procedure, tableTypes, location);
    }

    private static HashSet<string> BuildTableTypeLookup(AutoContractsManifest manifest)
    {
        var tableTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableType in manifest.TableTypes.Items)
            tableTypes.Add(BuildFullName(tableType.Schema, tableType.Name));

        return tableTypes;
    }

    private static void ValidateTableType(
        CompilationAnalysisContext context,
        AutoContractsTableType tableType,
        Location location)
    {
        foreach (var column in tableType.Columns.Items)
        {
            if (AutoContractsSqlTypes.IsSupported(column.SqlType))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedTableTypeSqlType,
                location,
                BuildFullName(tableType.Schema, tableType.Name, column.Name),
                column.SqlType));
        }
    }

    private static void ValidateProcedure(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        HashSet<string> tableTypes,
        Location location)
    {
        foreach (var parameter in procedure.Parameters.Items)
            ValidateParameter(context, procedure, parameter, tableTypes, location);

        foreach (var column in procedure.ResultSet.Columns.Items)
            ValidateResultColumn(context, procedure, column, location);

        ValidateResultSetStatus(context, procedure, location);
    }

    private static void ValidateParameter(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        AutoContractsParameter parameter,
        HashSet<string> tableTypes,
        Location location)
    {
        if (parameter.IsOutput)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractOutputParameterUnsupported,
                location,
                BuildFullName(procedure.Schema, procedure.Name),
                parameter.Name));

        if (parameter.IsTableType)
        {
            if (!tableTypes.Contains(parameter.SqlType))
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractTableTypeMissing,
                    location,
                    parameter.SqlType));

            return;
        }

        if (!AutoContractsSqlTypes.IsSupported(parameter.SqlType))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedSqlType,
                location,
                parameter.SqlType));
    }

    private static void ValidateResultColumn(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        AutoContractsColumn column,
        Location location)
    {
        if (!AutoContractsSqlTypes.IsSupported(column.SqlType))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUnsupportedSqlType,
                location,
                column.SqlType));

        if (column.Nullable)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractNullableResultColumn,
                location,
                BuildFullName(procedure.Schema, procedure.Name, column.Name)));
    }

    private static void ValidateResultSetStatus(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        Location location)
    {
        if (string.Equals(procedure.ResultSet.Status, "Undetermined", StringComparison.OrdinalIgnoreCase))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractUndeterminedResultSet,
                location,
                BuildFullName(procedure.Schema, procedure.Name)));
            return;
        }

        if (string.Equals(procedure.ResultSet.Status, "None", StringComparison.OrdinalIgnoreCase))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractNoResultSet,
                location,
                BuildFullName(procedure.Schema, procedure.Name)));
    }

    private static void ValidateGenerationNames(
        CompilationAnalysisContext context,
        AutoContractsManifest manifest,
        string manifestPath,
        Location location)
    {
        var generatedTypes = new HashSet<string>(StringComparer.Ordinal);
        var tableSqlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableType in manifest.TableTypes.Items)
        {
            ReportIfInvalidIdentifier(context, manifestPath, location, tableType.ClrName, "table type CLR name");
            ReportIfDuplicate(context, manifestPath, location, tableSqlNames, BuildFullName(tableType.Schema, tableType.Name));
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, tableType.ClrName);
            ValidateUniqueMembers(context, manifestPath, location, tableType.ClrName, tableType.Columns.Items);
        }

        var procedureSqlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var procedure in manifest.Procedures.Items)
        {
            ReportIfInvalidIdentifier(context, manifestPath, location, procedure.ClrName, "procedure CLR name");
            ReportIfInvalidIdentifier(context, manifestPath, location, procedure.ParametersClrName, "parameters CLR name");
            ReportIfDuplicate(context, manifestPath, location, procedureSqlNames, BuildFullName(procedure.Schema, procedure.Name));
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, procedure.ClrName);
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, procedure.ParametersClrName);
            ValidateUniqueMembers(context, manifestPath, location, procedure.ParametersClrName, procedure.Parameters.Items);

            if (procedure.ResultClrName is { Length: > 0 } resultClrName)
            {
                ReportIfInvalidIdentifier(context, manifestPath, location, resultClrName, "result CLR name");
                ReportIfDuplicate(context, manifestPath, location, generatedTypes, resultClrName);
                ValidateUniqueMembers(context, manifestPath, location, resultClrName, procedure.ResultSet.Columns.Items);
            }
        }
    }

    private static void ValidateUniqueMembers(
        CompilationAnalysisContext context,
        string manifestPath,
        Location location,
        string containerName,
        ImmutableArray<AutoContractsColumn> columns)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var column in columns)
            ReportIfDuplicate(context, manifestPath, location, identifiers, NormalizeIdentifier(column.Name), containerName);
    }

    private static void ValidateUniqueMembers(
        CompilationAnalysisContext context,
        string manifestPath,
        Location location,
        string containerName,
        ImmutableArray<AutoContractsParameter> parameters)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
            ReportIfDuplicate(context, manifestPath, location, identifiers, NormalizeIdentifier(parameter.Name), containerName);
    }

    private static void ReportIfInvalidIdentifier(
        CompilationAnalysisContext context,
        string manifestPath,
        Location location,
        string identifier,
        string label)
    {
        if (IsValidIdentifier(identifier))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ContractPossibleIncompatibility,
            location,
            manifestPath,
            $"Manifest {label} '{identifier}' is not a valid C# identifier."));
    }

    private static void ReportIfDuplicate(
        CompilationAnalysisContext context,
        string manifestPath,
        Location location,
        HashSet<string> values,
        string value,
        string? containerName = null)
    {
        if (values.Add(value))
            return;

        var message = containerName is null
            ? $"Manifest contains duplicate generated name or SQL name '{value}'."
            : $"Manifest members in '{containerName}' collide after C# identifier normalization as '{value}'.";

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ContractPossibleIncompatibility,
            location,
            manifestPath,
            message));
    }

    private static bool IsValidNamespace(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            return false;

        var parts = @namespace.Split('.');
        foreach (var part in parts)
            if (!IsValidIdentifier(part))
                return false;

        return true;
    }

    private static bool IsValidIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier) &&
               SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
               SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None;
    }

    private static string NormalizeIdentifier(string name)
    {
        return AutoContractsCSharpNames.ToIdentifier(name).TrimStart('@');
    }

    private static Location CreateManifestLocation(AdditionalText manifestFile, SourceText? sourceText)
    {
        if (sourceText is null)
            return Location.None;

        var span = new TextSpan(0, sourceText.Length);
        return Location.Create(
            manifestFile.Path,
            span,
            sourceText.Lines.GetLinePositionSpan(span));
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
