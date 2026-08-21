using System.Security.Cryptography;

namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsManifestParser
{
    internal const int SupportedManifestVersion = 3;
    internal const int MaxAdditionalFileBytes = 8 * 1024 * 1024;
    internal const int MaxJsonDepth = 64;

    internal static AutoContractsParseResult<AutoContractsManifest> ParseManifest(
        AdditionalText text,
        CancellationToken cancellationToken)
    {
        var source = text.GetText(cancellationToken)?.ToString();
        if (string.IsNullOrWhiteSpace(source))
            return new AutoContractsParseResult<AutoContractsManifest>(
                text.Path, null, "The manifest is empty.");
        if (Encoding.UTF8.GetByteCount(source!) > MaxAdditionalFileBytes)
            return new AutoContractsParseResult<AutoContractsManifest>(
                text.Path, null, "The manifest exceeds the 8 MiB AdditionalFile limit.");

        try
        {
            var json = SimpleJsonParser.Parse(source!, cancellationToken);
            var root = json.AsObject();
            var version = root.GetRequiredInt32("version");
            var ns = root.GetRequiredString("namespace");
            var tableTypes = ParseTableTypes(root.GetRequiredArray("tableTypes"));
            var procedures = ParseProcedures(root.GetRequiredArray("procedures"));
            var serverMajor = root.GetInt32("serverMajor", 0);
            var features = ParseFeatures(root.GetArray("features"));

            return new AutoContractsParseResult<AutoContractsManifest>(
                text.Path,
                NormalizeManifest(new AutoContractsManifest(
                    text.Path, version, ns, tableTypes, procedures, serverMajor, features)),
                null);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return new AutoContractsParseResult<AutoContractsManifest>(
                text.Path, null, "The JSON does not match the AutoContracts v3 manifest schema.");
        }
    }

    internal static AutoContractsParseResult<AutoContractsOverrides> ParseOverrides(
        AdditionalText text,
        CancellationToken cancellationToken)
    {
        var source = text.GetText(cancellationToken)?.ToString();
        if (string.IsNullOrWhiteSpace(source))
            return new AutoContractsParseResult<AutoContractsOverrides>(
                text.Path, null, "The overrides file is empty.");
        if (Encoding.UTF8.GetByteCount(source!) > MaxAdditionalFileBytes)
            return new AutoContractsParseResult<AutoContractsOverrides>(
                text.Path, null, "The overrides file exceeds the 8 MiB AdditionalFile limit.");
        try
        {
            var root = SimpleJsonParser.Parse(source!, cancellationToken).AsObject();
            var proceduresJson = root.GetRequiredArray("procedures");
            var procedures = new AutoContractsProcedureOverride[proceduresJson.Count];
            for (var index = 0; index < proceduresJson.Count; index++)
            {
                var procedure = proceduresJson[index].AsObject();
                procedures[index] = new AutoContractsProcedureOverride(
                    procedure.GetRequiredString("schema"),
                    procedure.GetRequiredString("name"),
                    ParseResultSets(procedure.GetRequiredArray("resultSets")),
                    ParseParameterDirectionOverrides(procedure.GetArray("parameters")));
            }

            return new AutoContractsParseResult<AutoContractsOverrides>(
                text.Path,
                new AutoContractsOverrides(
                    text.Path,
                    root.GetRequiredInt32("version"),
                    new EquatableArray<AutoContractsProcedureOverride>(procedures.ToImmutableArray())),
                null);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            return new AutoContractsParseResult<AutoContractsOverrides>(
                text.Path, null, "The JSON does not match the AutoContracts v3 overrides schema.");
        }
    }

    internal static AutoContractsMergeResult Merge(
        AutoContractsManifest manifest,
        EquatableArray<AutoContractsOverrides> overrides,
        bool sqlServer2025TypesEnabled = false)
    {
        var issues = ImmutableArray.CreateBuilder<AutoContractsGeneratorIssue>();
        if (overrides.Count > 1)
            issues.Add(new AutoContractsGeneratorIssue(
                manifest.SourcePath,
                "CAERIUSG005",
                "Only one AutoContracts overrides AdditionalFile is allowed per compilation."));
        var byProcedure = new Dictionary<string, List<AutoContractsResultSet>>(StringComparer.OrdinalIgnoreCase);
        var directionsByProcedure =
            new Dictionary<string, List<AutoContractsParameterDirectionOverride>>(StringComparer.OrdinalIgnoreCase);
        var manifestProcedures = new Dictionary<string, AutoContractsProcedure>(StringComparer.OrdinalIgnoreCase);
        foreach (var procedure in manifest.Procedures)
        {
            var key = BuildFullName(procedure.Schema, procedure.Name);
            if (manifestProcedures.ContainsKey(key))
            {
                issues.Add(new AutoContractsGeneratorIssue(
                    manifest.SourcePath,
                    "CAERIUSG005",
                    $"The manifest declares procedure '{key}' more than once."));
                continue;
            }

            manifestProcedures.Add(key, procedure);
        }

        foreach (var file in overrides.Take(1))
        {
            if (file.Version != SupportedManifestVersion)
            {
                issues.Add(new AutoContractsGeneratorIssue(
                    file.SourcePath,
                    "CAERIUSG003",
                    $"AutoContracts overrides must declare version {SupportedManifestVersion}."));
                continue;
            }

            foreach (var procedure in file.Procedures)
            {
                var key = BuildFullName(procedure.Schema, procedure.Name);
                if (!manifestProcedures.ContainsKey(key))
                {
                    issues.Add(new AutoContractsGeneratorIssue(
                        file.SourcePath,
                        "CAERIUSG004",
                        $"The overrides file references unknown procedure '{key}'."));
                    continue;
                }

                if (!byProcedure.TryGetValue(key, out var sets))
                {
                    sets = [];
                    byProcedure.Add(key, sets);
                }

                sets.AddRange(procedure.ResultSets);

                if (!directionsByProcedure.TryGetValue(key, out var directions))
                {
                    directions = [];
                    directionsByProcedure.Add(key, directions);
                }

                directions.AddRange(procedure.ParameterDirections);
            }
        }

        var procedures = manifest.Procedures.Select(procedure =>
        {
            var key = BuildFullName(procedure.Schema, procedure.Name);
            var sets = procedure.ResultSets.AsImmutableArray();
            if (byProcedure.TryGetValue(key, out var additional))
            {
                var additions = additional.OrderBy(set => set.Index).ToImmutableArray();
                if (additions.Any(set => set.Index == 0))
                {
                    if (sets.Length == 0 || sets[0].Index != 0 ||
                        sets[0].Status is not ("None" or "Undetermined"))
                        issues.Add(new AutoContractsGeneratorIssue(
                            manifest.SourcePath,
                            "CAERIUSG005",
                            $"Procedure '{key}' can override result set 0 only when discovery marked it None or Undetermined."));
                    else
                        sets = sets.RemoveAt(0);
                }

                sets = sets.Concat(additions).OrderBy(set => set.Index).ToImmutableArray();
                for (var index = 0; index < sets.Length; index++)
                    if (sets[index].Index != index ||
                        (index < sets.Length - 1 && sets[index].Status is "None" or "Undetermined"))
                        issues.Add(new AutoContractsGeneratorIssue(
                            manifest.SourcePath,
                            "CAERIUSG005",
                            $"Procedure '{key}' overrides must form continuous available result sets 0 through {sets.Length - 1}."));
            }

            var parameters = procedure.Parameters.AsImmutableArray();
            if (directionsByProcedure.TryGetValue(key, out var directionOverrides))
            {
                var requestedDirections = new Dictionary<int, string>();
                foreach (var directionOverride in directionOverrides)
                {
                    if (requestedDirections.ContainsKey(directionOverride.Ordinal))
                    {
                        issues.Add(new AutoContractsGeneratorIssue(
                            manifest.SourcePath,
                            "CAERIUSG005",
                            $"Procedure '{key}' has more than one direction override for parameter ordinal {directionOverride.Ordinal}."));
                        continue;
                    }

                    requestedDirections.Add(directionOverride.Ordinal, directionOverride.Direction);
                }

                foreach (var directionOverride in requestedDirections)
                {
                    var parameterIndex = -1;
                    for (var index = 0; index < parameters.Length; index++)
                    {
                        if (parameters[index].Ordinal != directionOverride.Key)
                            continue;

                        parameterIndex = index;
                        break;
                    }

                    if (parameterIndex < 0)
                    {
                        issues.Add(new AutoContractsGeneratorIssue(
                            manifest.SourcePath,
                            "CAERIUSG005",
                            $"Procedure '{key}' has a direction override for unknown parameter ordinal {directionOverride.Key}."));
                        continue;
                    }

                    parameters = parameters.SetItem(parameterIndex,
                        parameters[parameterIndex] with { Direction = directionOverride.Value });
                }
            }

            return NormalizeProcedure(procedure with
            {
                Parameters = new EquatableArray<AutoContractsParameter>(parameters),
                ResultSets = new EquatableArray<AutoContractsResultSet>(sets)
            });
        }).ToImmutableArray();
        var merged = manifest with { Procedures = new EquatableArray<AutoContractsProcedure>(procedures) };

        foreach (var message in AutoContractsEmissionGuard.GetValidationErrors(merged, sqlServer2025TypesEnabled))
            issues.Add(new AutoContractsGeneratorIssue(merged.SourcePath, "CAERIUSG005", message));

        return new AutoContractsMergeResult(
            merged,
            new EquatableArray<AutoContractsGeneratorIssue>(issues.ToImmutable()));
    }

    private static EquatableArray<AutoContractsTableType> ParseTableTypes(IReadOnlyList<JsonValue> values)
    {
        var tableTypes = new AutoContractsTableType[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            tableTypes[i] = new AutoContractsTableType(
                value.GetRequiredString("schema"),
                value.GetRequiredString("name"),
                value.GetRequiredString("clrName"),
                ParseColumns(value.GetRequiredArray("columns")),
                string.Empty);
        }

        return new EquatableArray<AutoContractsTableType>(tableTypes.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsProcedure> ParseProcedures(IReadOnlyList<JsonValue> values)
    {
        var procedures = new AutoContractsProcedure[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            procedures[i] = new AutoContractsProcedure(
                value.GetRequiredString("schema"),
                value.GetRequiredString("name"),
                value.GetRequiredString("clrName"),
                value.GetRequiredString("parametersClrName"),
                ParseParameters(value.GetRequiredArray("parameters")),
                ParseResultSets(value.GetRequiredArray("resultSets")),
                string.Empty);
        }

        return new EquatableArray<AutoContractsProcedure>(procedures.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsResultSet> ParseResultSets(IReadOnlyList<JsonValue> values)
    {
        var resultSets = new AutoContractsResultSet[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index].AsObject();
            var resultIndex = value.GetInt32("index", index);
            var columns = ParseColumns(value.GetRequiredArray("columns"));
            resultSets[index] = new AutoContractsResultSet(
                resultIndex,
                value.GetNullableString("clrName"),
                value.GetString("status", "Available"),
                columns,
                value.GetNullableString("errorMessage"),
                string.Empty);
        }

        return new EquatableArray<AutoContractsResultSet>(resultSets.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsParameter> ParseParameters(IReadOnlyList<JsonValue> values)
    {
        var parameters = new AutoContractsParameter[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            parameters[i] = new AutoContractsParameter(
                value.GetInt32("ordinal", i),
                value.GetRequiredString("name"),
                value.GetRequiredString("sqlType"),
                value.GetRequiredString("clrType"),
                value.GetBoolean("isTableType", false),
                value.GetString("direction", "Input"),
                value.GetBoolean("nullable", false),
                value.GetNullableInt32("maxLength"),
                value.GetNullableByte("precision"),
                value.GetNullableByte("scale"));
        }

        return new EquatableArray<AutoContractsParameter>(parameters.ToImmutableArray());
    }

    private static EquatableArray<string> ParseFeatures(IReadOnlyList<JsonValue> values)
    {
        var features = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
            features[index] = values[index].AsString();
        return new EquatableArray<string>(features.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsParameterDirectionOverride> ParseParameterDirectionOverrides(
        IReadOnlyList<JsonValue> values)
    {
        var directions = new AutoContractsParameterDirectionOverride[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index].AsObject();
            directions[index] = new AutoContractsParameterDirectionOverride(
                value.GetRequiredInt32("ordinal"),
                value.GetRequiredString("direction"));
        }

        return new EquatableArray<AutoContractsParameterDirectionOverride>(directions.ToImmutableArray());
    }

    private static AutoContractsManifest NormalizeManifest(AutoContractsManifest manifest)
    {
        var tableTypes = manifest.TableTypes.Select(tableType => tableType with
        {
            ContractHash = HashTableType(tableType)
        }).ToImmutableArray();
        var procedures = manifest.Procedures.Select(NormalizeProcedure).ToImmutableArray();
        return manifest with
        {
            TableTypes = new EquatableArray<AutoContractsTableType>(tableTypes),
            Procedures = new EquatableArray<AutoContractsProcedure>(procedures),
            Features = new EquatableArray<string>(manifest.Features.AsImmutableArray()
                .Where(static feature => !string.IsNullOrWhiteSpace(feature))
                .Select(static feature => string.Equals(
                    feature.Trim(),
                    AutoContractsSqlServer2025.FeatureName,
                    StringComparison.OrdinalIgnoreCase)
                    ? AutoContractsSqlServer2025.FeatureName
                    : feature.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static feature => feature, StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray())
        };
    }

    private static AutoContractsProcedure NormalizeProcedure(AutoContractsProcedure procedure)
    {
        var resultSets = procedure.ResultSets.Select(resultSet => resultSet with
        {
            ContractHash = HashResultSet(resultSet)
        }).ToImmutableArray();
        var normalized = procedure with
        {
            ResultSets = new EquatableArray<AutoContractsResultSet>(resultSets)
        };
        return normalized with { ContractHash = HashProcedure(normalized) };
    }

    private static string HashTableType(AutoContractsTableType tableType)
    {
        var builder = new StringBuilder();
        AppendValue(builder, "table-type");
        AppendValue(builder, tableType.Schema);
        AppendValue(builder, tableType.Name);
        AppendValue(builder, tableType.ClrName);
        foreach (var column in tableType.Columns.OrderBy(column => column.Ordinal))
            AppendColumn(builder, column);
        return HashText(builder);
    }

    private static string HashProcedure(AutoContractsProcedure procedure)
    {
        var builder = new StringBuilder();
        AppendValue(builder, "procedure");
        AppendValue(builder, procedure.Schema);
        AppendValue(builder, procedure.Name);
        AppendValue(builder, procedure.ClrName);
        AppendValue(builder, procedure.ParametersClrName);
        foreach (var parameter in procedure.Parameters.OrderBy(parameter => parameter.Ordinal))
            AppendParameter(builder, parameter);
        foreach (var resultSet in procedure.ResultSets.OrderBy(resultSet => resultSet.Index))
            AppendResultSet(builder, resultSet);
        return HashText(builder);
    }

    private static string HashResultSet(AutoContractsResultSet resultSet)
    {
        var builder = new StringBuilder();
        AppendResultSet(builder, resultSet);
        return HashText(builder);
    }

    private static void AppendResultSet(StringBuilder builder, AutoContractsResultSet resultSet)
    {
        AppendValue(builder, "result-set");
        AppendValue(builder, resultSet.Index);
        AppendValue(builder, resultSet.ClrName);
        AppendValue(builder, resultSet.Status);
        AppendValue(builder, resultSet.ErrorMessage);
        foreach (var column in resultSet.Columns.OrderBy(column => column.Ordinal))
            AppendColumn(builder, column);
    }

    private static void AppendParameter(StringBuilder builder, AutoContractsParameter parameter)
    {
        AppendValue(builder, parameter.Ordinal);
        AppendValue(builder, parameter.Name);
        AppendValue(builder, parameter.SqlType);
        AppendValue(builder, parameter.ClrType);
        AppendValue(builder, parameter.IsTableType);
        AppendValue(builder, parameter.Direction);
        AppendValue(builder, parameter.Nullable);
        AppendValue(builder, parameter.MaxLength);
        AppendValue(builder, parameter.Precision);
        AppendValue(builder, parameter.Scale);
    }

    private static void AppendColumn(StringBuilder builder, AutoContractsColumn column)
    {
        AppendValue(builder, column.Ordinal);
        AppendValue(builder, column.Name);
        AppendValue(builder, column.SqlType);
        AppendValue(builder, column.ClrType);
        AppendValue(builder, column.Nullable);
        AppendValue(builder, column.MaxLength);
        AppendValue(builder, column.Precision);
        AppendValue(builder, column.Scale);
        AppendValue(builder, column.LocaleId);
        AppendValue(builder, column.SqlCompareOptions);
        AppendValue(builder, column.Collation);
    }

    private static void AppendValue(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("null;");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
    }

    private static void AppendValue(StringBuilder builder, int value)
    {
        AppendValue(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, int? value)
    {
        AppendValue(builder, value?.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, byte? value)
    {
        AppendValue(builder, value?.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, bool value)
    {
        AppendValue(builder, value ? "true" : "false");
    }

    private static string HashText(StringBuilder builder)
    {
        using var algorithm = SHA256.Create();
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
        return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string BuildFullName(string schema, string name)
    {
        return schema + "." + name;
    }

    private static EquatableArray<AutoContractsColumn> ParseColumns(IReadOnlyList<JsonValue> values)
    {
        var columns = new AutoContractsColumn[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            columns[i] = new AutoContractsColumn(
                value.GetInt32("ordinal", i),
                value.GetRequiredString("name"),
                value.GetRequiredString("sqlType"),
                value.GetRequiredString("clrType"),
                value.GetBoolean("nullable", false),
                value.GetNullableInt32("maxLength"),
                value.GetNullableByte("precision"),
                value.GetNullableByte("scale"),
                value.GetNullableInt32("localeId"),
                value.GetNullableInt32("sqlCompareOptions"),
                value.GetNullableString("collation"));
        }

        return new EquatableArray<AutoContractsColumn>(columns.ToImmutableArray());
    }

    private sealed class SimpleJsonParser(string text, CancellationToken cancellationToken)
    {
        private int _position;

        private bool IsEnd => _position >= text.Length;

        internal static JsonValue Parse(string text, CancellationToken cancellationToken)
        {
            var parser = new SimpleJsonParser(text, cancellationToken);
            var value = parser.ParseValue(0);
            parser.SkipWhiteSpace();
            if (!parser.IsEnd)
                throw new FormatException("Unexpected trailing content in contract manifest.");

            return value;
        }

        private JsonValue ParseValue(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > MaxJsonDepth)
                throw new FormatException($"JSON nesting exceeds the maximum depth of {MaxJsonDepth}.");
            SkipWhiteSpace();
            if (IsEnd)
                throw new FormatException("Unexpected end of JSON.");

            return text[_position] switch
            {
                '{' => ParseObject(depth + 1),
                '[' => ParseArray(depth + 1),
                '"' => new JsonValue(ParseString()),
                't' => ParseLiteral("true", new JsonValue(true)),
                'f' => ParseLiteral("false", new JsonValue(false)),
                'n' => ParseLiteral("null", JsonValue.Null),
                '-' or >= '0' and <= '9' => new JsonValue(ParseNumber()),
                _ => throw new FormatException($"Unexpected character '{text[_position]}' in JSON.")
            };
        }

        private JsonValue ParseObject(int depth)
        {
            Expect('{');
            var values = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            SkipWhiteSpace();
            if (TryConsume('}'))
                return new JsonValue(new JsonObject(values));

            while (true)
            {
                SkipWhiteSpace();
                var name = ParseString();
                SkipWhiteSpace();
                Expect(':');
                values[name] = ParseValue(depth);
                SkipWhiteSpace();
                if (TryConsume('}'))
                    return new JsonValue(new JsonObject(values));
                Expect(',');
            }
        }

        private JsonValue ParseArray(int depth)
        {
            Expect('[');
            var values = new List<JsonValue>();
            SkipWhiteSpace();
            if (TryConsume(']'))
                return new JsonValue(values);

            while (true)
            {
                values.Add(ParseValue(depth));
                SkipWhiteSpace();
                if (TryConsume(']'))
                    return new JsonValue(values);
                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (!IsEnd)
            {
                var ch = text[_position++];
                if (ch == '"')
                    return sb.ToString();

                if (ch != '\\')
                {
                    sb.Append(ch);
                    continue;
                }

                if (IsEnd)
                    throw new FormatException("Unterminated JSON escape sequence.");

                var escaped = text[_position++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(escaped);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        sb.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw new FormatException($"Unsupported JSON escape sequence '\\{escaped}'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_position + 4 > text.Length)
                throw new FormatException("Invalid JSON unicode escape.");

            var value = Convert.ToInt32(text.Substring(_position, 4), 16);
            _position += 4;
            return (char)value;
        }

        private double ParseNumber()
        {
            var start = _position;
            if (text[_position] == '-')
                _position++;

            while (!IsEnd && char.IsDigit(text[_position]))
                _position++;

            if (!IsEnd && text[_position] == '.')
            {
                _position++;
                while (!IsEnd && char.IsDigit(text[_position]))
                    _position++;
            }

            if (!IsEnd && (text[_position] == 'e' || text[_position] == 'E'))
            {
                _position++;
                if (!IsEnd && (text[_position] == '+' || text[_position] == '-'))
                    _position++;
                while (!IsEnd && char.IsDigit(text[_position]))
                    _position++;
            }

            return double.Parse(
                text.Substring(start, _position - start),
                CultureInfo.InvariantCulture);
        }

        private JsonValue ParseLiteral(string literal, JsonValue value)
        {
            if (_position + literal.Length > text.Length ||
                !string.Equals(
                    text.Substring(_position, literal.Length),
                    literal,
                    StringComparison.Ordinal))
                throw new FormatException($"Invalid JSON literal at position {_position}.");

            _position += literal.Length;
            return value;
        }

        private void SkipWhiteSpace()
        {
            while (!IsEnd && char.IsWhiteSpace(text[_position]))
                _position++;
        }

        private bool TryConsume(char expected)
        {
            if (IsEnd || text[_position] != expected)
                return false;

            _position++;
            return true;
        }

        private void Expect(char expected)
        {
            if (!TryConsume(expected))
                throw new FormatException($"Expected '{expected}' at position {_position}.");
        }
    }

    private sealed class JsonValue
    {
        internal static readonly JsonValue Null = new();

        private readonly object? _value;

        private JsonValue()
        {
        }

        internal JsonValue(string value)
        {
            _value = value;
        }

        internal JsonValue(double value)
        {
            _value = value;
        }

        internal JsonValue(bool value)
        {
            _value = value;
        }

        internal JsonValue(JsonObject value)
        {
            _value = value;
        }

        internal JsonValue(IReadOnlyList<JsonValue> value)
        {
            _value = value;
        }

        internal bool IsNull => _value is null;

        internal JsonObject AsObject()
        {
            return _value as JsonObject
                   ?? throw new FormatException("Expected JSON object.");
        }

        internal IReadOnlyList<JsonValue> AsArray()
        {
            return _value as IReadOnlyList<JsonValue>
                   ?? throw new FormatException("Expected JSON array.");
        }

        internal string AsString()
        {
            return _value as string
                   ?? throw new FormatException("Expected JSON string.");
        }

        internal int AsInt32()
        {
            return _value is double value &&
                   value >= int.MinValue &&
                   value <= int.MaxValue &&
                   IsInteger(value)
                ? (int)value
                : throw new FormatException("Expected JSON integer.");
        }

        internal byte AsByte()
        {
            return _value is double value &&
                   value >= byte.MinValue &&
                   value <= byte.MaxValue &&
                   IsInteger(value)
                ? (byte)value
                : throw new FormatException("Expected JSON byte.");
        }

        private static bool IsInteger(double value)
        {
            // double.IsInteger is unavailable on the netstandard2.0 Roslyn component target.
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   Math.Abs(value % 1d) < double.Epsilon;
        }

        internal bool AsBoolean()
        {
            return _value is bool value
                ? value
                : throw new FormatException("Expected JSON boolean.");
        }
    }

    private sealed class JsonObject(Dictionary<string, JsonValue> values)
    {
        internal string GetRequiredString(string name)
        {
            if (!values.TryGetValue(name, out var value) || value.IsNull)
                throw new FormatException($"Required manifest property '{name}' is missing.");

            var text = value.AsString();
            return string.IsNullOrWhiteSpace(text)
                ? throw new FormatException($"Required manifest property '{name}' is empty.")
                : text;
        }

        internal string GetString(string name, string? defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsString()
                : defaultValue ?? string.Empty;
        }

        internal string? GetNullableString(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsString()
                : null;
        }

        internal int GetInt32(string name, int defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : defaultValue;
        }

        internal int GetRequiredInt32(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : throw new FormatException($"Required manifest property '{name}' is missing.");
        }

        internal int? GetNullableInt32(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : null;
        }

        internal byte? GetNullableByte(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsByte()
                : null;
        }

        internal bool GetBoolean(string name, bool defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsBoolean()
                : defaultValue;
        }

        internal IReadOnlyList<JsonValue> GetArray(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsArray()
                : [];
        }

        internal IReadOnlyList<JsonValue> GetRequiredArray(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsArray()
                : throw new FormatException($"Required manifest property '{name}' is missing.");
        }

        internal bool TryGetObject(string name, out JsonObject obj)
        {
            if (values.TryGetValue(name, out var value) && !value.IsNull)
            {
                obj = value.AsObject();
                return true;
            }

            obj = null!;
            return false;
        }
    }
}
