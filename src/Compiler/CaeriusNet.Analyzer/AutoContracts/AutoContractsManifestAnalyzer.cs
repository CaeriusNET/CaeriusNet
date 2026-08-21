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
        var manifests = new List<ParsedManifest>();
        var overrides = new List<ParsedOverrides>();

        foreach (var additionalFile in context.Options.AdditionalFiles)
        {
            if (AutoContractsManifestFile.IsManifest(
                    additionalFile,
                    context.Options.AnalyzerConfigOptionsProvider))
            {
                manifestFound = true;
                if (TryParseManifest(context, additionalFile, out var manifest))
                    manifests.Add(manifest);
                continue;
            }

            if (AutoContractsManifestFile.IsOverrides(
                    additionalFile,
                    context.Options.AnalyzerConfigOptionsProvider) &&
                TryParseOverrides(context, additionalFile, out var parsedOverrides))
                overrides.Add(parsedOverrides);
        }

        if (!manifestFound && RequiresManifest(context))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                Location.None,
                AutoContractsManifestFile.ManifestFileName));

        if (manifests.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                Location.None,
                AutoContractsManifestFile.ManifestFileName,
                "Only one AutoContracts manifest AdditionalFile is allowed per compilation."));
        }
        else if (manifests.Count == 1)
        {
            var manifest = manifests[0];
            var merge = AutoContractsManifestParser.Merge(
                manifest.Manifest,
                new EquatableArray<AutoContractsOverrides>(
                    overrides.Select(value => value.Overrides).ToImmutableArray()),
                AutoContractsSqlServer2025.IsEnabled(
                    context.Options.AnalyzerConfigOptionsProvider.GlobalOptions));
            foreach (var issue in merge.Issues)
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractPossibleIncompatibility,
                    manifest.Location,
                    issue.SourcePath,
                    issue.Message));
            ValidateManifest(context, merge.Manifest, manifest.Path, manifest.Location);
        }

        ValidateUnmatchedOverrides(context, manifests, overrides);
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

    private static bool TryParseManifest(
        CompilationAnalysisContext context,
        AdditionalText manifestFile,
        out ParsedManifest manifest)
    {
        var sourceText = manifestFile.GetText(context.CancellationToken);
        var location = CreateManifestLocation(manifestFile, sourceText);
        var source = sourceText?.ToString();
        if (string.IsNullOrWhiteSpace(source))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractManifestMissing,
                location,
                AutoContractsManifestFile.ManifestFileName));
            manifest = default;
            return false;
        }

        try
        {
            var parsed = AutoContractsManifestParser.ParseManifest(manifestFile, context.CancellationToken);
            if (!parsed.IsSuccess)
                throw new FormatException(parsed.Error ??
                                          "The JSON does not match the AutoContracts v3 manifest schema.");

            manifest = new ParsedManifest(parsed.Value!, manifestFile.Path, location);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                location,
                manifestFile.Path,
                ex.Message));
            manifest = default;
            return false;
        }
    }

    private static bool TryParseOverrides(
        CompilationAnalysisContext context,
        AdditionalText overridesFile,
        out ParsedOverrides overrides)
    {
        var sourceText = overridesFile.GetText(context.CancellationToken);
        var location = CreateManifestLocation(overridesFile, sourceText);
        var source = sourceText?.ToString();
        if (string.IsNullOrWhiteSpace(source))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                location,
                overridesFile.Path,
                "The AutoContracts overrides manifest is empty."));
            overrides = default;
            return false;
        }

        try
        {
            var parsed = AutoContractsManifestParser.ParseOverrides(overridesFile, context.CancellationToken);
            if (!parsed.IsSuccess)
                throw new FormatException(parsed.Error ??
                                          "The JSON does not match the AutoContracts v3 overrides schema.");

            overrides = new ParsedOverrides(parsed.Value!, overridesFile.Path, location);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractPossibleIncompatibility,
                location,
                overridesFile.Path,
                ex.Message));
            overrides = default;
            return false;
        }
    }

    private static AutoContractsManifest MergeOverrides(
        AutoContractsManifest manifest,
        List<ParsedOverrides> overrides)
    {
        if (overrides.Count == 0)
            return manifest;

        var procedureOverrides = new Dictionary<string, List<AutoContractsResultSet>>(StringComparer.OrdinalIgnoreCase);
        foreach (var overrideFile in overrides)
        {
            if (overrideFile.Overrides.Version != AutoContractsManifestParser.SupportedManifestVersion)
                continue;

            foreach (var procedure in overrideFile.Overrides.Procedures.Items)
            {
                var key = BuildFullName(procedure.Schema, procedure.Name);
                if (!procedureOverrides.TryGetValue(key, out var resultSets))
                {
                    resultSets = [];
                    procedureOverrides.Add(key, resultSets);
                }

                resultSets.AddRange(procedure.ResultSets.Items);
            }
        }

        if (procedureOverrides.Count == 0)
            return manifest;

        var procedures = ImmutableArray.CreateBuilder<AutoContractsProcedure>(manifest.Procedures.Items.Length);
        foreach (var procedure in manifest.Procedures.Items)
        {
            if (!procedureOverrides.TryGetValue(BuildFullName(procedure.Schema, procedure.Name), out var resultSets))
            {
                procedures.Add(procedure);
                continue;
            }

            var merged = procedure.ResultSets.Items.AddRange(resultSets);
            procedures.Add(procedure with
            {
                ResultSets = new EquatableArray<AutoContractsResultSet>(merged)
            });
        }

        return manifest with
        {
            Procedures = new EquatableArray<AutoContractsProcedure>(procedures.MoveToImmutable())
        };
    }

    private static void ValidateUnmatchedOverrides(
        CompilationAnalysisContext context,
        List<ParsedManifest> manifests,
        List<ParsedOverrides> overrides)
    {
        var knownProcedures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in manifests)
        foreach (var procedure in manifest.Manifest.Procedures.Items)
            knownProcedures.Add(BuildFullName(procedure.Schema, procedure.Name));

        foreach (var overrideFile in overrides)
        {
            if (overrideFile.Overrides.Version != AutoContractsManifestParser.SupportedManifestVersion)
                ReportUnsupportedVersion(context, overrideFile.Overrides.Version, overrideFile.Path,
                    overrideFile.Location);

            foreach (var procedure in overrideFile.Overrides.Procedures.Items)
            {
                var procedureName = BuildFullName(procedure.Schema, procedure.Name);
                if (!knownProcedures.Contains(procedureName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ContractPossibleIncompatibility,
                        overrideFile.Location,
                        overrideFile.Path,
                        $"Overrides reference procedure '{procedureName}', which is not present in a contract manifest."));
                    ValidateResultSets(context, procedureName, procedure.ResultSets.Items, overrideFile.Path,
                        overrideFile.Location);
                }
            }
        }
    }

    private static void ValidateManifest(
        CompilationAnalysisContext context,
        AutoContractsManifest manifest,
        string manifestPath,
        Location location)
    {
        if (manifest.Version != AutoContractsManifestParser.SupportedManifestVersion)
            ReportUnsupportedVersion(context, manifest.Version, manifestPath, location);

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
            ValidateProcedure(context, procedure, tableTypes, manifestPath, location);
    }

    private static void ReportUnsupportedVersion(
        CompilationAnalysisContext context,
        int version,
        string manifestPath,
        Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ContractPossibleIncompatibility,
            location,
            manifestPath,
            $"Unsupported manifest version '{version}'. Expected version '{AutoContractsManifestParser.SupportedManifestVersion}'."));
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
        string manifestPath,
        Location location)
    {
        foreach (var parameter in procedure.Parameters.Items)
            ValidateParameter(context, procedure, parameter, tableTypes, location);

        ValidateResultSets(
            context,
            BuildFullName(procedure.Schema, procedure.Name),
            procedure.ResultSets.Items,
            manifestPath,
            location);
    }

    private static void ValidateParameter(
        CompilationAnalysisContext context,
        AutoContractsProcedure procedure,
        AutoContractsParameter parameter,
        HashSet<string> tableTypes,
        Location location)
    {
        var supportedDirection = IsSupportedDirection(parameter.Direction);
        if (!supportedDirection)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ContractOutputParameterUnsupported,
                location,
                BuildFullName(procedure.Schema, procedure.Name),
                parameter.Name,
                parameter.Direction));

        if (parameter.IsTableType)
        {
            if (supportedDirection && !string.Equals(parameter.Direction, "Input", StringComparison.Ordinal))
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractOutputParameterUnsupported,
                    location,
                    BuildFullName(procedure.Schema, procedure.Name),
                    parameter.Name,
                    parameter.Direction));

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

    private static bool IsSupportedDirection(string direction)
    {
        return direction is "Input" or "Output" or "InputOutput" or "ReturnValue";
    }

    private static void ValidateResultSets(
        CompilationAnalysisContext context,
        string procedureName,
        ImmutableArray<AutoContractsResultSet> resultSets,
        string manifestPath,
        Location location)
    {
        if (resultSets.Length is < 1 or > 10)
            ReportManifestProblem(
                context,
                manifestPath,
                location,
                $"Procedure '{procedureName}' must declare between one and ten result sets.");

        var seenIndices = new HashSet<int>();
        foreach (var resultSet in resultSets)
        {
            if (resultSet.Index is < 0 or > 9)
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Procedure '{procedureName}' result-set index '{resultSet.Index}' must be between 0 and 9.");
            else if (!seenIndices.Add(resultSet.Index))
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Procedure '{procedureName}' declares result-set index '{resultSet.Index}' more than once.");

            ValidateResultSet(context, procedureName, resultSet, manifestPath, location);
        }

        for (var expected = 0; expected < resultSets.Length && expected <= 9; expected++)
            if (!seenIndices.Contains(expected))
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Procedure '{procedureName}' result-set indices must be continuous from 0; index '{expected}' is missing.");
    }

    private static void ValidateResultSet(
        CompilationAnalysisContext context,
        string procedureName,
        AutoContractsResultSet resultSet,
        string manifestPath,
        Location location)
    {
        if (string.Equals(resultSet.Status, "Available", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(resultSet.ClrName))
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Available result set '{resultSet.Index}' of procedure '{procedureName}' requires a CLR name.");
            if (resultSet.Columns.Items.Length == 0)
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Available result set '{resultSet.Index}' of procedure '{procedureName}' requires at least one column.");

            foreach (var column in resultSet.Columns.Items)
                ValidateResultColumn(context, procedureName, resultSet.Index, column, location);
            return;
        }

        if (string.Equals(resultSet.Status, "Undetermined", StringComparison.OrdinalIgnoreCase))
        {
            if (resultSet.Index == 0)
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractUndeterminedResultSet,
                    location,
                    procedureName));
            else
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Result set '{resultSet.Index}' of procedure '{procedureName}' cannot be undetermined.");
            return;
        }

        if (string.Equals(resultSet.Status, "None", StringComparison.OrdinalIgnoreCase))
        {
            if (resultSet.Index == 0)
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ContractNoResultSet,
                    location,
                    procedureName));
            else
                ReportManifestProblem(
                    context,
                    manifestPath,
                    location,
                    $"Only the first result set of procedure '{procedureName}' can have status 'None'.");
            return;
        }

        ReportManifestProblem(
            context,
            manifestPath,
            location,
            $"Result set '{resultSet.Index}' of procedure '{procedureName}' has unsupported status '{resultSet.Status}'.");
    }

    private static void ValidateResultColumn(
        CompilationAnalysisContext context,
        string procedureName,
        int resultSetIndex,
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
                procedureName + ".Set" + resultSetIndex + "." + column.Name));
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
            ReportIfDuplicate(context, manifestPath, location, tableSqlNames,
                BuildFullName(tableType.Schema, tableType.Name));
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, tableType.ClrName);
            ValidateUniqueMembers(context, manifestPath, location, tableType.ClrName, tableType.Columns.Items);
        }

        var procedureSqlNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var procedure in manifest.Procedures.Items)
        {
            ReportIfInvalidIdentifier(context, manifestPath, location, procedure.ClrName, "procedure CLR name");
            ReportIfInvalidIdentifier(context, manifestPath, location, procedure.ParametersClrName,
                "parameters CLR name");
            ReportIfDuplicate(context, manifestPath, location, procedureSqlNames,
                BuildFullName(procedure.Schema, procedure.Name));
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, procedure.ClrName);
            ReportIfDuplicate(context, manifestPath, location, generatedTypes, procedure.ParametersClrName);
            ValidateUniqueMembers(context, manifestPath, location, procedure.ParametersClrName,
                procedure.Parameters.Items);

            if (procedure.Parameters.Items.Any(static parameter => parameter.Direction != "Input"))
                ReportIfDuplicate(context, manifestPath, location, generatedTypes, procedure.ClrName + "Output");

            foreach (var resultSet in procedure.ResultSets.Items)
            {
                if (resultSet.ClrName is not { Length: > 0 } resultClrName)
                    continue;

                ReportIfInvalidIdentifier(context, manifestPath, location, resultClrName, "result-set CLR name");
                ReportIfDuplicate(context, manifestPath, location, generatedTypes, resultClrName);
                ValidateUniqueMembers(context, manifestPath, location, resultClrName, resultSet.Columns.Items);
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
            ReportIfDuplicate(context, manifestPath, location, identifiers, NormalizeIdentifier(column.Name),
                containerName);
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
            ReportIfDuplicate(context, manifestPath, location, identifiers, NormalizeIdentifier(parameter.Name),
                containerName);
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

        ReportManifestProblem(
            context,
            manifestPath,
            location,
            $"Manifest {label} '{identifier}' is not a valid C# identifier.");
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

        ReportManifestProblem(context, manifestPath, location, message);
    }

    private static void ReportManifestProblem(
        CompilationAnalysisContext context,
        string manifestPath,
        Location location,
        string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ContractPossibleIncompatibility,
            location,
            manifestPath,
            message));
    }

    private static bool IsValidNamespace(string @namespace)
    {
        return AutoContractsNaming.IsValidNamespace(@namespace);
    }

    private static bool IsValidIdentifier(string identifier)
    {
        return AutoContractsNaming.IsValidIdentifier(identifier);
    }

    private static string NormalizeIdentifier(string name)
    {
        return AutoContractsNaming.ToIdentifier(name).TrimStart('@');
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
        return AutoContractsNaming.BuildSqlName(schema, name);
    }

    private static string BuildFullName(string schema, string name, string member)
    {
        return AutoContractsNaming.BuildSqlName(schema, name, member);
    }

    private readonly record struct ParsedManifest(
        AutoContractsManifest Manifest,
        string Path,
        Location Location);

    private readonly record struct ParsedOverrides(
        AutoContractsOverrides Overrides,
        string Path,
        Location Location);
}
